using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The vertical profile of every road piece of a tile for one height sampler (owner feedback: make roads smoother):
    /// the terrain drape along the smoothed centreline, low-passed (Gaussian over a class-dependent length) and with its
    /// grade changes capped, but held inside a band of ± <see cref="BandM"/> around the drawn terrain plus the ribbon
    /// lift at the carriageway centre, with a cross-fall (bank) that follows the terrain's cross slope: up to the
    /// comfortable ± <see cref="MaxBank"/>, and further (up to <see cref="MaxSteepBank"/>) wherever holding that would leave
    /// an edge more than <see cref="MaxBuryM"/> off the terrain, so a hill road hugs the hillside instead of standing on a
    /// causeway. So the road stays on the terrain TerrainMesher draws (no draped vertex more than <see cref="MaxBuryM"/>
    /// under the lifted terrain below it, and the centre within the band above it) while the DEM steps and facet creases
    /// are ironed out.
    /// <para>
    /// Seams are pinned exactly: at tile-border cuts, junction-cap and ring-entry cuts and knee joins the surface fades
    /// (over <see cref="PinFadeM"/>, an analytic weight that is exactly 1 at the seam) to the sampled terrain plus the
    /// lift both sides agree on, so neighbouring tiles, caps and ribbons meet without steps; the shading normal fades to
    /// the terrain's own normal there. Structures (<see cref="RoadStructureRecord.DeckY"/>, absolute surface heights per
    /// road point, NaN where draped): decks follow their heights (Catmull-Rom between points, + <see cref="DeckSurfacingM"/>;
    /// the bridges package draws the slab below) and blend linearly to the drape over a segment that leaves the deck;
    /// at-grade pieces that end on a deck ramp to it. OSM bridges without deck heights run straight between their lifted
    /// ends (never below the lifted terrain). Underpasses whose clearance is under
    /// <see cref="RoadClearance.MinUnderpassClearanceM"/> are lowered with <see cref="RampGrade"/> ramps (the terrain
    /// mesher must cut the terrain above them). Built once per (tile, sampler) and cached on the sampler; immutable and
    /// thread-safe afterwards.
    /// </para>
    /// </summary>
    public sealed class RoadGrade
    {
        /// <summary>The smoothed profile stays within this of the banked plane through the lifted terrain across the
        /// carriageway at its highest point (at most <see cref="MaxDishM"/> above the centre).</summary>
        public const float BandM = 0.12f;

        /// <summary>No draped surface vertex lies more than this under the lifted terrain below it.</summary>
        public const float MaxBuryM = 0.12f;

        /// <summary>Where the terrain across the road is concave (a road along a gully), the profile rises by at most this
        /// above the lifted terrain at the centre to keep the edges out of the ground; beyond that the per-vertex floor
        /// lifts the low columns (a dished cross-section) instead of the whole road floating.</summary>
        public const float MaxDishM = 0.20f;

        /// <summary>Station spacing of the profile along the curve.</summary>
        public const float StationM = 2f;

        /// <summary>Length over which a pinned seam fades into the smoothed profile.</summary>
        public const float PinFadeM = 12f;

        /// <summary>Comfortable cross-fall of the carriageway (25 %): on gentle cross slopes the road leans with the terrain up
        /// to this; where holding it would leave an edge more than <see cref="MaxBuryM"/> off the terrain (the road standing
        /// on a causeway above the downhill side, or cut into the uphill side), the bank follows the slope further, up to
        /// <see cref="MaxSteepBank"/>, so hill roads hug the hillside.</summary>
        public const float MaxBank = 0.25f;

        /// <summary>Steepest cross-fall a road follows (45°). The terrain mesher draws the 30 m DEM's hillsides; a true bench
        /// (cut uphill, fill downhill) needs it to cut and fill to the drawn road surface (open issue, ref_roads.md §8).</summary>
        public const float MaxSteepBank = 1.0f;

        /// <summary>Road surfacing above an elevated deck's surface height (DeckY), so the two never z-fight.</summary>
        public const float DeckSurfacingM = 0.03f;

        /// <summary>Grade of the ramps into a lowered underpass and onto a deck end.</summary>
        public const float RampGrade = 0.08f;

        /// <summary>Largest grade change per metre of the smoothed profile (a 100 m vertical curve), where the band
        /// allows.</summary>
        public const float MaxGradeChangePerM = 0.01f;

        /// <summary>Smoothing length of the cross-fall.</summary>
        public const double BankSmoothingM = 6.0;

        private struct Pin
        {
            public double L, Fade;
            public float Rel, AbsY;
            public bool Abs;
        }

        private sealed class Piece
        {
            public int N;
            public double Step, Length;

            /// <summary>Profile at the centreline (absolute y, lift included; lowering applied) and bank per station.</summary>
            public float[] P, B;

            /// <summary>Weight and height of the absolute (deck) surface per station; null when the piece has none.</summary>
            public float[] DeckW, DeckY;

            /// <summary>Underpass lowering per station (already in <see cref="P"/>), or null.</summary>
            public float[] Lower;

            public Pin[] Pins = new Pin[0];
            public int PinCount;
            public float Lift;

            /// <summary>Every station on a deck (P holds the deck heights).</summary>
            public bool AllDeck;

            /// <summary>An OSM bridge without deck data: a straight deck between the lifted ends.</summary>
            public bool Synthetic;

            public void AddPin(in Pin p)
            {
                if (PinCount == Pins.Length) Array.Resize(ref Pins, Math.Max(4, 2 * PinCount));
                Pins[PinCount++] = p;
            }
        }

        /// <summary>Everything about one cross-section row of a piece that depends only on the along position.</summary>
        internal struct Row
        {
            public float P, B, G;
            public float PinW, PinRel, PinAbsY;
            public bool PinAbs;
            public float DeckW, DeckY, DeckG;
            public bool Clamp;
            public float Lift;
            public bool Valid;
        }

        private sealed class Key
        {
            public TileData Tile;
            public float LiftM, ClassLiftStepM, PieceLiftStepM, KerbHeightM;
            public int PieceLiftLevels;
            public bool Smooth;
            public RoadGrade Grade;
        }

        private static readonly ConditionalWeakTable<IHeightSampler, Key> Cache = new ConditionalWeakTable<IHeightSampler, Key>();
        private static readonly object CacheLock = new object();

        public readonly TileData Tile;
        public readonly RoadLayout Layout;
        private readonly IHeightSampler _h;
        private readonly TileHeightSampler _ths;
        private readonly double _x0, _z0, _size;
        private readonly Piece[] _pieces;
        private readonly RoadOptions _o;

        /// <summary>The grade of a tile's roads on a sampler (cached on the sampler for the same tile and lifts).</summary>
        public static RoadGrade For(TileData t, IHeightSampler h, RoadOptions o)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (o == null) o = new RoadOptions();
            lock (CacheLock)
            {
                Key k;
                if (Cache.TryGetValue(h, out k) && ReferenceEquals(k.Tile, t) && k.LiftM == o.LiftM && k.ClassLiftStepM == o.ClassLiftStepM &&
                    k.PieceLiftStepM == o.PieceLiftStepM && k.PieceLiftLevels == o.PieceLiftLevels && k.KerbHeightM == o.KerbHeightM &&
                    k.Smooth == o.SmoothProfile) return k.Grade;
            }
            var g = new RoadGrade(t, h, o);
            lock (CacheLock)
            {
                Cache.Remove(h);
                Cache.Add(h, new Key
                {
                    Tile = t, LiftM = o.LiftM, ClassLiftStepM = o.ClassLiftStepM, PieceLiftStepM = o.PieceLiftStepM, PieceLiftLevels = o.PieceLiftLevels,
                    KerbHeightM = o.KerbHeightM, Smooth = o.SmoothProfile, Grade = g,
                });
            }
            return g;
        }

        private RoadGrade(TileData t, IHeightSampler h, RoadOptions o)
        {
            Tile = t;
            Layout = RoadLayout.For(t);
            _h = h;
            _ths = h as TileHeightSampler;
            _o = new RoadOptions
            {
                LiftM = o.LiftM, ClassLiftStepM = o.ClassLiftStepM, PieceLiftStepM = o.PieceLiftStepM, PieceLiftLevels = o.PieceLiftLevels,
                KerbHeightM = o.KerbHeightM, SmoothProfile = o.SmoothProfile,
            };
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _size = t.Tile.Size;
            _pieces = new Piece[t.Roads.Count];
            // Deck end heights by end point, for at-grade pieces that ramp onto a deck.
            var deckEnds = new Dictionary<long, float>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadStructureRecord s = Layout.Structures[i];
                RoadRecord r = t.Roads[i];
                if (s.DeckY == null || s.DeckY.Length != r.PointCount) continue;
                int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? r.PointCount - 2 : r.PointCount - 1;
                if (!float.IsNaN(s.DeckY[first])) deckEnds[EndKey(r, first)] = s.DeckY[first] + DeckSurfacingM;
                if (!float.IsNaN(s.DeckY[last])) deckEnds[EndKey(r, last)] = s.DeckY[last] + DeckSurfacingM;
            }
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!RoadMesher.IsDrawn(t, i, null)) continue;
                RoadCentreline c = Layout.Centres[i];
                if (c == null || c.Count < 2) continue;
                _pieces[i] = Build(i, deckEnds);
            }
        }

        private static long EndKey(RoadRecord r, int k)
        {
            return (long)r.Points[2 * k] << 32 ^ (uint)r.Points[2 * k + 1];
        }

        // -------------------------------------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Terrain height (drawn surface) at tile-local metres, clamped onto the tile.</summary>
        public float Terrain(double lx, double lz)
        {
            float v;
            if (_h.TryHeight(_x0 + lx, _z0 + lz, out v)) return v;
            if (_ths != null)
            {
                if (_ths.TryHeightClamped(_x0 + lx, _z0 + lz, out v)) return v;
                return 0f;
            }
            double cx = lx < 0 ? 0 : lx > _size ? _size : lx, cz = lz < 0 ? 0 : lz > _size ? _size : lz;
            return _h.TryHeight(_x0 + cx, _z0 + cz, out v) ? v : 0f;
        }

        /// <summary>Terrain shading normal at tile-local metres (the terrain mesh's own smooth normal when the sampler is a
        /// <see cref="TileHeightSampler"/>, else central differences 1 m either side).</summary>
        public void TerrainNormal(double lx, double lz, out float nx, out float ny, out float nz)
        {
            if (_ths != null && _ths.TrySmoothNormal(_x0 + lx, _z0 + lz, out nx, out ny, out nz)) return;
            const double e = 1.0;
            double gx = (Terrain(lx + e, lz) - (double)Terrain(lx - e, lz)) / (2 * e);
            double gz = (Terrain(lx, lz + e) - (double)Terrain(lx, lz - e)) / (2 * e);
            TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
        }

        /// <summary>True when the piece has a profile (drawn).</summary>
        public bool Has(int road)
        {
            return road >= 0 && road < _pieces.Length && _pieces[road] != null;
        }

        /// <summary>The whole piece is drawn at absolute deck heights (an elevated bridge or flyover with deck data, or an
        /// OSM bridge without, run straight between its ends), not held to the terrain.</summary>
        public bool IsAbsolute(int road)
        {
            Piece p = _pieces[road];
            return p != null && (p.AllDeck || p.Synthetic);
        }

        /// <summary>The piece is an OSM bridge drawn as a straight synthetic deck (no deck data).</summary>
        public bool IsSyntheticDeck(int road)
        {
            Piece p = _pieces[road];
            return p != null && p.Synthetic;
        }

        /// <summary>The piece is lowered (an underpass made to clear 5.5 m) somewhere.</summary>
        public bool IsLowered(int road)
        {
            Piece p = _pieces[road];
            return p != null && p.Lower != null;
        }

        /// <summary>How far an underpass is lowered (metres below its draped profile) at raw along <paramref name="s"/> of
        /// road <paramref name="road"/>; 0 where it is not (the terrain mesher must cut the terrain above a lowered
        /// stretch: <see cref="Roads.RoadSurfaceQuery.TryCut"/>).</summary>
        public float LoweringAt(int road, double s)
        {
            Piece p = road >= 0 && road < _pieces.Length ? _pieces[road] : null;
            if (p == null || p.Lower == null) return 0f;
            double f = p.Step > 0 ? Layout.Centres[road].ArcAt(s) / p.Step : 0;
            return Lerp(p.Lower, p.N, f);
        }

        /// <summary>The ribbon lift of the piece (as <see cref="RoadMesher.LiftOf"/> with this grade's options).</summary>
        public float LiftOf(int road)
        {
            Piece p = _pieces[road];
            return p != null ? p.Lift : RoadMesher.LiftOf(Tile.Roads[road], _o);
        }

        /// <summary>True when raw along <paramref name="s"/> of the piece lies on a deck (more than half the deck height
        /// weight).</summary>
        public bool OnDeck(int road, double s)
        {
            Piece p = _pieces[road];
            if (p == null) return false;
            if (p.AllDeck || p.Synthetic) return true;
            if (p.DeckW == null) return false;
            double f = Layout.Centres[road].ArcAt(s) / p.Step;
            return Lerp(p.DeckW, p.N, f) > 0.5f;
        }

        /// <summary>
        /// The carriageway surface height of road <paramref name="road"/> at raw along <paramref name="s"/> and lateral
        /// offset <paramref name="off"/> (metres left of the centreline), for the point at tile-local (x, z).
        /// </summary>
        public float SurfaceY(int road, double s, double off, double x, double z)
        {
            Row r = RowAt(road, s);
            return Y(r, off, x, z);
        }

        /// <summary>Unit surface normal of road <paramref name="road"/> at raw along <paramref name="s"/> for the point at
        /// tile-local (x, z): along-grade and cross-fall of the profile, faded to the terrain normal at seams.</summary>
        public void SurfaceNormal(int road, double s, double x, double z, out float nx, out float ny, out float nz)
        {
            RoadCentreline c = Layout.Centres[road];
            double cx, cz, tx, tz;
            c.At(s, out cx, out cz, out tx, out tz);
            Row r = RowAt(road, s);
            Normal(r, tx, tz, x, z, out nx, out ny, out nz);
        }

        /// <summary>
        /// The drawn road surface at tile-local (x, z) of road <paramref name="road"/>: the nearest point of its smoothed
        /// centreline gives the along position and lateral offset. False when the point is farther than
        /// <paramref name="maxLateralM"/> from the centreline or the piece has no profile.
        /// </summary>
        public bool TrySurfaceAt(int road, double x, double z, double maxLateralM, out float y)
        {
            y = 0f;
            if (!Has(road)) return false;
            RoadCentreline c = Layout.Centres[road];
            int seg;
            double along, lateral, dist;
            if (!c.Nearest(x, z, out seg, out along, out lateral, out dist) || Math.Abs(lateral) > maxLateralM) return false;
            y = SurfaceY(road, along, lateral, x, z);
            return true;
        }

        internal Row RowAt(int road, double s)
        {
            var r = new Row();
            Piece p = _pieces[road];
            if (p == null) return r;
            r.Valid = true;
            r.Lift = p.Lift;
            double l = Layout.Centres[road].ArcAt(s);
            double f = p.Step > 0 ? l / p.Step : 0;
            r.P = CatmullRom(p.P, p.N, f);
            r.B = Lerp(p.B, p.N, f);
            r.G = Slope(p.P, p.N, f) / (float)Math.Max(1e-6, p.Step);
            if (p.AllDeck || p.Synthetic)
            {
                r.DeckW = 0f;
                r.Clamp = false;
                return r;
            }
            float best = 0f;
            for (int k = 0; k < p.PinCount; k++)
            {
                Pin pin = p.Pins[k];
                double d = Math.Abs(l - pin.L);
                if (d >= pin.Fade) continue;
                float w = (float)(1.0 - Smoothstep(d / pin.Fade));
                if (w <= best) continue;
                best = w;
                r.PinAbs = pin.Abs;
                r.PinRel = pin.Rel;
                r.PinAbsY = pin.AbsY;
            }
            r.PinW = best;
            bool lowered = false;
            if (p.Lower != null) lowered = Lerp(p.Lower, p.N, f) > 0.01f;
            if (p.DeckW != null)
            {
                r.DeckW = Lerp(p.DeckW, p.N, f);
                if (r.DeckW > 0f)
                {
                    r.DeckY = CatmullRom(p.DeckY, p.N, f);
                    r.DeckG = Slope(p.DeckY, p.N, f) / (float)Math.Max(1e-6, p.Step);
                }
            }
            r.Clamp = r.DeckW <= 0f && !lowered;
            if (!_o.SmoothProfile && r.DeckW <= 0f && !lowered)
            {
                // Unsmoothed: exactly the terrain plus the lift at every vertex (a full-weight pin to the terrain).
                r.PinW = 1f;
                r.PinAbs = false;
                r.PinRel = p.Lift;
            }
            return r;
        }

        internal float Y(in Row r, double off, double x, double z)
        {
            if (!r.Valid) return Terrain(x, z) + _o.LiftM;
            float y = r.P + (float)off * r.B;
            bool needTer = r.Clamp || r.PinW > 0f && !r.PinAbs;
            float ter = needTer ? Terrain(x, z) : 0f;
            if (r.PinW > 0f)
            {
                float target = r.PinAbs ? r.PinAbsY : ter + r.PinRel;
                y += (target - y) * r.PinW;
            }
            if (r.DeckW > 0f) y += (r.DeckY - y) * r.DeckW;
            if (r.Clamp)
            {
                float floor = ter + r.Lift - MaxBuryM;
                if (y < floor) y = floor;
            }
            return y;
        }

        internal void Normal(in Row r, double tx, double tz, double x, double z, out float nx, out float ny, out float nz)
        {
            double g = r.G, b = r.B;
            if (r.DeckW > 0f)
            {
                g += (r.DeckG - g) * r.DeckW;
                b *= 1f - r.DeckW;
            }
            double ux = -tz, uz = tx;
            double gx = g * tx + b * ux, gz = g * tz + b * uz;
            double inv = 1.0 / Math.Sqrt(gx * gx + 1 + gz * gz);
            nx = (float)(-gx * inv);
            ny = (float)inv;
            nz = (float)(-gz * inv);
            if (r.PinW > 0f && !r.PinAbs && r.DeckW <= 0f)
            {
                float tnx, tny, tnz;
                TerrainNormal(x, z, out tnx, out tny, out tnz);
                float w = r.PinW;
                RoadSweep.Normalise(nx + (tnx - nx) * w, ny + (tny - ny) * w, nz + (tnz - nz) * w, out nx, out ny, out nz);
            }
        }

        private static float Lerp(float[] v, int n, double f)
        {
            if (n <= 1) return v[0];
            if (f <= 0) return v[0];
            if (f >= n - 1) return v[n - 1];
            int i = (int)f;
            float t = (float)(f - i);
            return v[i] + (v[i + 1] - v[i]) * t;
        }

        private static float Slope(float[] v, int n, double f)
        {
            if (n <= 1) return 0f;
            int i = (int)Math.Floor(f);
            if (i < 0) i = 0;
            if (i > n - 2) i = n - 2;
            return v[i + 1] - v[i];
        }

        /// <summary>Uniform Catmull-Rom interpolation of station values (end points repeated).</summary>
        private static float CatmullRom(float[] v, int n, double f)
        {
            if (n <= 1) return v[0];
            if (f <= 0) return v[0];
            if (f >= n - 1) return v[n - 1];
            int i = (int)f;
            float t = (float)(f - i);
            float p0 = v[i > 0 ? i - 1 : 0], p1 = v[i], p2 = v[i + 1], p3 = v[i + 2 < n ? i + 2 : n - 1];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // -------------------------------------------------------------------------------------------------------
        // Building
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Gaussian smoothing length (sigma) of the profile by class (game metres).</summary>
        public static double SmoothingM(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                case RoadClass.Primary: return 12;
                case RoadClass.Secondary:
                case RoadClass.Tertiary: return 9;
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway: return 4;
                default: return 6;
            }
        }

        /// <summary>Per-thread working arrays (one thread-static lookup per piece, not per element).</summary>
        private sealed class Scratch
        {
            public float[] Tmp = new float[0], Base = new float[0], H0 = new float[0], H1 = new float[0], H2 = new float[0], H3 = new float[0];
            public float[] H4 = new float[0], Bank = new float[0], Off = new float[0];
            public double[] Kernel = new double[64];

            public void Ensure(int n)
            {
                if (Tmp.Length >= n) return;
                int cap = Math.Max(n, 256);
                Tmp = new float[cap];
                Base = new float[cap];
                H0 = new float[cap];
                H1 = new float[cap];
                H2 = new float[cap];
                H3 = new float[cap];
                H4 = new float[cap];
                Bank = new float[cap];
                Off = new float[cap * 2];
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        private Piece Build(int i, Dictionary<long, float> deckEnds)
        {
            RoadRecord r = Tile.Roads[i];
            RoadCentreline c = Layout.Centres[i];
            RoadWidthProfile prof = Layout.Profiles[i];
            RoadStructureRecord st = Layout.Structures[i];
            double len = c.LengthM;
            int n = Math.Max(2, (int)Math.Ceiling(len / StationM) + 1);
            var p = new Piece { N = n, Step = len / (n - 1), Length = len, P = new float[n], B = new float[n], Lift = RoadMesher.LiftOf(r, _o) };
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            sc.Ensure(n);
            float[] tmp = sc.Tmp, bas = sc.Base, h0 = sc.H0, h1 = sc.H1, h2 = sc.H2, h3 = sc.H3, h4 = sc.H4, bank = sc.Bank, offs = sc.Off;
            float lift = p.Lift;
            bool hasDeck = st.DeckY != null && st.DeckY.Length == r.PointCount;
            if (hasDeck) DeckStations(i, r, st.DeckY, p);
            if (p.AllDeck)
            {
                Array.Copy(p.DeckY, p.P, n);
                p.DeckW = null;
                p.DeckY = null;
                return p;
            }
            bool synthetic = !hasDeck && (r.Flags & RoadFlags.Bridge) != 0;
            // Terrain across the road at every station: five points over the carriageway (edges at q = ±2).
            for (int k = 0; k < n; k++)
            {
                double s = c.AlongAtArc(k * p.Step);
                double x, z, tx, tz;
                c.At(s, out x, out z, out tx, out tz);
                double ux = -tz, uz = tx;
                double w = prof.DrawnAt(s);
                double half = 0.5 * w;
                double shift = prof.ShiftAt(s);
                offs[2 * k] = (float)shift;
                offs[2 * k + 1] = (float)half;
                double xl = x + ux * (shift + half), zl = z + uz * (shift + half), xr = x + ux * (shift - half), zr = z + uz * (shift - half);
                h0[k] = Terrain(xr, zr);
                h1[k] = Terrain(x + ux * (shift - 0.5 * half), z + uz * (shift - 0.5 * half));
                h2[k] = Terrain(x + ux * shift, z + uz * shift);
                h3[k] = Terrain(x + ux * (shift + 0.5 * half), z + uz * (shift + 0.5 * half));
                h4[k] = Terrain(xl, zl);
                tmp[k] = (float)((h4[k] - h0[k]) / Math.Max(0.5, 2 * half));
            }
            if (synthetic)
            {
                // A level deck on the straight line between the lifted ends, never below the lifted terrain under it.
                p.Synthetic = true;
                float hs = h2[0] + lift, he = h2[n - 1] + lift;
                // Cut ends: the lifted terrain at the centreline point itself (on the border line, so both tiles sample
                // the same height; a dual carriageway's shifted centre would depend on this tile's real width).
                if (r.HasPrevContext) hs = Terrain(c.X[0], c.Z[0]) + lift;
                if (r.HasNextContext) he = Terrain(c.X[c.Count - 1], c.Z[c.Count - 1]) + lift;
                for (int k = 0; k < n; k++)
                {
                    float line = hs + (he - hs) * k / (n - 1);
                    float ground = Math.Max(h2[k], Math.Max(h0[k], h4[k])) + lift;
                    // A tile-border cut end stays exactly at its lifted centre (the edges' terrain may lie in the
                    // neighbouring tile), so both pieces of a cut bridge meet level at the same height.
                    bool cutEnd = k == 0 && r.HasPrevContext || k == n - 1 && r.HasNextContext;
                    p.P[k] = cutEnd ? line : Math.Max(line, ground);
                    p.B[k] = 0f;
                }
                return p;
            }
            // Cross-fall: the terrain's cross slope, held to the comfortable bank where that keeps both edges within
            // MaxBuryM of the terrain, else following the slope (hill roads hug the hillside instead of standing on a
            // causeway above it), then smoothed along the road.
            for (int k = 0; k < n; k++)
            {
                float raw = tmp[k], a = Math.Abs(raw), half = Math.Max(0.25f, offs[2 * k + 1]);
                // Up to MaxBank the bank is the slope; just above it the bank stays near MaxBank while the edges are within
                // MaxBuryM of the terrain; on steeper hillsides it is the slope itself (smoothly: no edge more than
                // MaxBuryM off the terrain, no kink in the bank).
                float excess = a - MaxBank, allow = MaxBuryM / half;
                float want = excess <= 0f ? a : excess >= allow ? a : MaxBank + excess * (float)Smoothstep(excess / allow);
                want = Math.Min(MaxSteepBank, want);
                bank[k] = raw < 0 ? -want : want;
            }
            Smooth(bank, n, BankSmoothingM / p.Step, p.B, sc);
            // The banked plane through the terrain across the carriageway at its highest point (five points), so no
            // column is buried, but never more than MaxDishM above the centre (a strongly concave cross-section, a road
            // along a gully, is dished by the per-vertex floor instead of floating the whole road).
            for (int k = 0; k < n; k++)
            {
                float b = p.B[k], shift = offs[2 * k], half = offs[2 * k + 1];
                float q0 = h0[k] - (shift - half) * b, q1 = h1[k] - (shift - 0.5f * half) * b, q2 = h2[k] - shift * b;
                float q3 = h3[k] - (shift + 0.5f * half) * b, q4 = h4[k] - (shift + half) * b;
                float top = Math.Max(Math.Max(q0, q1), Math.Max(q2, Math.Max(q3, q4)));
                bas[k] = Math.Min(top, q2 + MaxDishM) + lift;
            }
            // Low-pass inside the band, then cap the grade changes (still inside the band).
            Array.Copy(bas, p.P, n);
            double sigma = SmoothingM(r.RoadClass) / p.Step;
            for (int it = 0; it < 4; it++)
            {
                Smooth(p.P, n, sigma, tmp, sc);
                for (int k = 0; k < n; k++) p.P[k] = Math.Max(bas[k] - BandM, Math.Min(bas[k] + BandM, tmp[k]));
            }
            float cap = (float)(MaxGradeChangePerM * p.Step * p.Step);
            for (int it = 0; it < 8; it++)
            {
                bool changed = false;
                for (int k = 1; k + 1 < n; k++)
                {
                    float d2 = p.P[k - 1] - 2f * p.P[k] + p.P[k + 1];
                    if (Math.Abs(d2) <= cap) continue;
                    float target = p.P[k] + 0.5f * (d2 - Math.Sign(d2) * cap);
                    float v = Math.Max(bas[k] - BandM, Math.Min(bas[k] + BandM, target));
                    if (v != p.P[k])
                    {
                        p.P[k] = v;
                        changed = true;
                    }
                }
                if (!changed) break;
            }
            // Pins: the seams and the lift (or absolute deck height) each one meets.
            if (r.HasPrevContext) p.AddPin(new Pin { L = 0, Fade = PinFadeM, Rel = lift });
            if (r.HasNextContext) p.AddPin(new Pin { L = len, Fade = PinFadeM, Rel = lift });
            RoadKnee ks = Layout.Knees[2 * i], ke = Layout.Knees[2 * i + 1];
            if (ks.Has) p.AddPin(new Pin { L = 0, Fade = PinFadeM, Rel = RoadMesher.LiftOf(Tile.Roads[ks.LiftRoad], _o) });
            if (ke.Has) p.AddPin(new Pin { L = len, Fade = PinFadeM, Rel = RoadMesher.LiftOf(Tile.Roads[ke.LiftRoad], _o) });
            RoadCut[] cuts = Layout.Cuts[i];
            int[] tops = Layout.CutTops[i];
            if (cuts != null)
            {
                for (int g = 0; g < cuts.Length; g++)
                {
                    int top = tops != null && g < tops.Length ? tops[g] : -1;
                    float pinLift = top >= 0 ? RoadMesher.LiftOf(Tile.Roads[top], _o) + JunctionMesher.CapExtraLiftM : lift;
                    p.AddPin(new Pin { L = c.ArcAt(cuts[g].S), Fade = PinFadeM, Rel = pinLift });
                }
            }
            // Free ends on a deck: ramp to the deck height.
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? r.PointCount - 2 : r.PointCount - 1;
            float dy;
            if (!r.HasPrevContext && !ks.Has && deckEnds.TryGetValue(EndKey(r, first), out dy) && !OnOwnDeck(p, 0)) AbsolutePin(p, 0.0, dy, len);
            if (!r.HasNextContext && !ke.Has && deckEnds.TryGetValue(EndKey(r, last), out dy) && !OnOwnDeck(p, n - 1)) AbsolutePin(p, len, dy, len);
            // Underpass clearance.
            if (st.Kind == RoadStructureKind.Underpass && !hasDeck && st.ClearanceM > 0f && st.ClearanceM < RoadClearance.MinUnderpassClearanceM)
            {
                float drop = RoadClearance.MinUnderpassClearanceM - st.ClearanceM;
                float fit = (float)(0.5 * len * RampGrade);
                if (drop > fit) drop = fit;
                if (drop > 0.01f)
                {
                    p.Lower = new float[n];
                    double ramp = drop / RampGrade;
                    for (int k = 0; k < n; k++)
                    {
                        double l = k * p.Step;
                        double w = Math.Min(1.0, Math.Min(l, len - l) / Math.Max(1e-6, ramp));
                        float lw = (float)(drop * Smoothstep(w));
                        p.Lower[k] = lw;
                        p.P[k] -= lw;
                    }
                }
            }
            return p;
        }

        private static bool OnOwnDeck(Piece p, int k)
        {
            return p.DeckW != null && p.DeckW[k] > 0.5f;
        }

        /// <summary>Pin to an absolute height (a deck end) with a ramp long enough for <see cref="RampGrade"/>.</summary>
        private void AbsolutePin(Piece p, double at, float y, double len)
        {
            int k = at <= 0 ? 0 : p.N - 1;
            float ground = p.P[k];
            double fade = Math.Max(PinFadeM, Math.Abs(y - ground) / RampGrade);
            fade = Math.Min(fade, Math.Max(1.0, len));
            p.AddPin(new Pin { L = at, Fade = fade, Abs = true, AbsY = y });
        }

        /// <summary>
        /// Deck weights and heights at every station from per-point heights (NaN where draped): a segment between two deck
        /// points follows the deck (Catmull-Rom through the finite points), a segment from a deck point to a draped point
        /// blends linearly from the deck to the drape.
        /// </summary>
        private void DeckStations(int ri, RoadRecord r, float[] deck, Piece p)
        {
            int n = p.N;
            RoadCentreline c = Layout.Centres[ri];
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? r.PointCount - 2 : r.PointCount - 1;
            bool any = false, all = true;
            var w = new float[n];
            var y = new float[n];
            int seg = first;
            for (int k = 0; k < n; k++)
            {
                double s = c.AlongAtArc(k * p.Step);
                while (seg + 1 < last && Layout.AlongAt(ri, seg + 1) < s) seg++;
                int j0 = seg, j1 = Math.Min(seg + 1, last);
                double s0 = Layout.AlongAt(ri, j0), s1 = Layout.AlongAt(ri, j1);
                double f = s1 - s0 > 1e-9 ? (s - s0) / (s1 - s0) : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                bool a = !float.IsNaN(deck[j0]), b = !float.IsNaN(deck[j1]);
                if (a && b)
                {
                    float p0 = Finite(deck, j0 - 1 >= 0 ? j0 - 1 : j0, deck[j0]), p3 = Finite(deck, j1 + 1 < deck.Length ? j1 + 1 : j1, deck[j1]);
                    float t = (float)f, t2 = t * t, t3 = t2 * t, p1 = deck[j0], p2 = deck[j1];
                    w[k] = 1f;
                    y[k] = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3) + DeckSurfacingM;
                }
                else if (a)
                {
                    w[k] = (float)(1 - f);
                    y[k] = deck[j0] + DeckSurfacingM;
                }
                else if (b)
                {
                    w[k] = (float)f;
                    y[k] = deck[j1] + DeckSurfacingM;
                }
                if (w[k] > 0f) any = true;
                if (w[k] < 1f) all = false;
            }
            if (!any) return;
            p.DeckW = w;
            p.DeckY = y;
            p.AllDeck = all;
        }

        private static float Finite(float[] v, int i, float fallback)
        {
            return float.IsNaN(v[i]) ? fallback : v[i];
        }

        private static double Smoothstep(double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        /// <summary>Gaussian smoothing of v (n values) with sigma in stations into dst (ends clamped); the kernel is built
        /// once per call.</summary>
        private static void Smooth(float[] v, int n, double sigma, float[] dst, Scratch sc)
        {
            if (!(sigma >= 0.3) || n < 3)
            {
                Array.Copy(v, dst, n);
                return;
            }
            int rad = (int)Math.Ceiling(2.5 * Math.Min(sigma, n));
            if (sc.Kernel.Length < 2 * rad + 1) sc.Kernel = new double[2 * rad + 1];
            double[] kernel = sc.Kernel;
            double inv = 1.0 / (2 * sigma * sigma), wsum = 0;
            for (int q = -rad; q <= rad; q++)
            {
                double w = Math.Exp(-q * q * inv);
                kernel[q + rad] = w;
                wsum += w;
            }
            for (int q = 0; q <= 2 * rad; q++) kernel[q] /= wsum;
            for (int k = 0; k < n; k++)
            {
                double sum = 0;
                if (k >= rad && k + rad < n)
                {
                    for (int q = -rad; q <= rad; q++) sum += kernel[q + rad] * v[k + q];
                }
                else
                {
                    for (int q = -rad; q <= rad; q++)
                    {
                        int j = k + q;
                        j = j < 0 ? 0 : j > n - 1 ? n - 1 : j;
                        sum += kernel[q + rad] * v[j];
                    }
                }
                dst[k] = (float)sum;
            }
        }
    }
}
