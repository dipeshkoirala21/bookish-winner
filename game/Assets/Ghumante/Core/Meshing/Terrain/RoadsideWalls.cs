using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>A barrier the carve builds, for the solids (game metres): a parapet or guard rail along a drop beside a
    /// road, or a breast wall holding a cut (<see cref="TerrainCarve.CopyBarriers"/>).</summary>
    public struct CarveBarrier
    {
        public double AX, AZ, BX, BZ;
        public float Bottom, Top;

        /// <summary>A breast wall holding a cut (else a parapet or guard rail along a drop).</summary>
        public bool Wall;
    }

    public sealed partial class TerrainCarve
    {
        /// <summary>Depth of a retaining wall block beyond the corridor (one terrain sub-quad, so the steep carved face
        /// inside it is hidden).</summary>
        public double WallDepthM
        {
            get { return SubM; }
        }

        /// <summary>Course height of a gabion wall (one wire basket).</summary>
        public const float GabionCourseM = 1.0f;

        /// <summary>Basket length along the wall.</summary>
        public const float GabionLengthM = 2.0f;

        /// <summary>How far each course steps back (the wall's batter).</summary>
        public const float GabionStepM = 0.15f;

        /// <summary>Spacing of the parapet posts along a big drop (the Low tier's coarse grids: twice that).</summary>
        public const float PostSpacingM = 3f;

        /// <summary>Drop of the carved ground <see cref="ParapetReachM"/> beyond the road's drawn extent above which its edge
        /// gets parapet posts (a real drop: the hillside falls more than 50 % below the embankment's crest).</summary>
        public const float ParapetDropM = 2f;

        /// <summary>How far beyond the drawn extent the drop is measured.</summary>
        public const float ParapetReachM = 4f;

        private const uint GabionStone = 0x8F8B82FFu, MasonryStone = 0x9C978EFFu, Coping = 0xB3AEA4FFu;
        private const uint PostWhite = 0xEDEBE3FFu, PostBlack = 0x2B2B2BFFu, RailSteel = 0xB2B6B8FFu;

        /// <summary>
        /// The retaining walls and parapets of the carve, appended to <paramref name="m"/> (area-local x/z, absolute y, the
        /// terrain mesh's frame): wherever the carve built a wall (<see cref="WallMinM"/> and up), a gabion wall of 1 m wire
        /// baskets stepping back course by course (fills, and cuts beside main roads) or a battered dry stone wall with a
        /// coping (cuts beside minor roads), as a block one terrain sub-quad deep that hides the carved face inside it and
        /// meets the ground at its top or toe; and along big drops (a fill wall, or more than <see cref="ParapetDropM"/>
        /// within 2.5 m of the corridor) white parapet posts with a black band, carrying a steel W-beam rail beside main
        /// roads. UV0 channels (Stone, Concrete, Paint, Metal) with baked AO. Returns the triangles added.
        /// </summary>
        public int AppendWalls(MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int before = m.IndexCount;
            for (int e = 0; e < _segCount; e++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float wa = side == 0 ? _wla[e] : _wra[e], wb = side == 0 ? _wlb[e] : _wrb[e];
                    if (Math.Max(Math.Abs(wa), Math.Abs(wb)) >= 0.5f) Wall(m, e, side, wa, wb);
                    Parapet(m, e, side, wa, wb);
                }
            }
            return (m.IndexCount - before) / 3;
        }

        private static bool Major(RoadClass c)
        {
            return c == RoadClass.Motorway || c == RoadClass.Trunk || c == RoadClass.Primary || c == RoadClass.Secondary || c == RoadClass.Tertiary;
        }

        /// <summary>Point of segment <paramref name="e"/> at fraction <paramref name="t"/>, offset <paramref name="off"/>
        /// toward <paramref name="side"/> (tile-local).</summary>
        private void At(int e, double t, int side, double off, out double x, out double z)
        {
            double sign = side == 0 ? 1 : -1;
            // Along the ribbon's section direction there, as the extent's offsets are measured.
            double wx = _wxa[e] + (_wxb[e] - _wxa[e]) * t, wz = _wza[e] + (_wzb[e] - _wza[e]) * t;
            x = _ax[e] + _dx[e] * _len[e] * t + wx * sign * off;
            z = _az[e] + _dz[e] * _len[e] * t + wz * sign * off;
        }

        private float Ext(int e, int side, float t)
        {
            return side == 0 ? _cla[e] + (_clb[e] - _cla[e]) * t : _cra[e] + (_crb[e] - _cra[e]) * t;
        }

        private float EdgeBench(int e, int side, float t)
        {
            float edge = side == 0 ? _ela[e] + (_elb[e] - _ela[e]) * t : _era[e] + (_erb[e] - _era[e]) * t;
            return _a0a[e] + (_a0b[e] - _a0a[e]) * t + edge * (_a1a[e] + (_a1b[e] - _a1a[e]) * t);
        }

        /// <summary>The drawn (carved) terrain at tile-local (x, z).</summary>
        private float Drawn(double lx, double lz)
        {
            double gx, gz;
            return (float)DrawnSurface((lx - _offX) / Grid.CellM, (lz - _offZ) / Grid.CellM, out gx, out gz);
        }

        /// <summary>How deep a wall block may reach outward from tile-local (x0, z0) along (ux, uz) before it would stand on
        /// another road's drawn extent (a lane or path running along the top of the cut, a street below the fill): the
        /// largest of <paramref name="depth"/> and the clear distance less a margin; 0 when there is no room for a wall.</summary>
        private double ClearDepth(double x0, double z0, double ux, double uz, double depth)
        {
            const double Step = 0.25, Margin = 0.15;
            for (double d = 0.0; d <= depth + 1e-6; d += Step)
            {
                float b;
                double gx, gz;
                if (!ExtentAt(x0 + ux * d, z0 + uz * d, out b, out gx, out gz)) continue;
                double clear = d - Margin;
                return clear < 0.3 ? 0.0 : clear;
            }
            return depth;
        }

        private void Wall(MeshData m, int e, int side, float wa, float wb)
        {
            double len = _len[e];
            int n = Math.Max(1, (int)Math.Ceiling(len / GabionLengthM));
            double sign = side == 0 ? 1 : -1;
            double ux = -_dz[e] * sign, uz = _dx[e] * sign; // outward (away from the road)
            double fullDepth = WallDepthM;
            RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
            bool cutSide = wa + wb > 0f;
            bool gabion = !cutSide || Major(cls);
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n, tm = 0.5f * (t0 + t1);
                float w = Math.Abs(wa + (wb - wa) * tm);
                if (w < 0.5f) continue;
                // A breast wall stands where the cut slope starts (beyond the verge), a fill wall at the drawn edge.
                float e0 = Ext(e, side, t0) + (cutSide ? WallVergeM : 0f), e1 = Ext(e, side, t1) + (cutSide ? WallVergeM : 0f);
                float b0 = EdgeBench(e, side, t0), b1 = EdgeBench(e, side, t1);
                double fx0, fz0, fx1, fz1;
                At(e, t0, side, e0, out fx0, out fz0);
                At(e, t1, side, e1, out fx1, out fz1);
                // Never over another road: the block stops short of any drawn extent behind it (a breast wall) or between
                // it and the valley (a fill wall's body); no room at all, no wall (the terrain carve meets that road).
                double depth = Math.Min(ClearDepth(fx0 + ux * 0.05, fz0 + uz * 0.05, ux, uz, fullDepth), ClearDepth(fx1 + ux * 0.05, fz1 + uz * 0.05, ux, uz, fullDepth));
                double fxm = 0.5 * (fx0 + fx1), fzm = 0.5 * (fz0 + fz1);
                depth = Math.Min(depth, ClearDepth(fxm + ux * 0.05, fzm + uz * 0.05, ux, uz, fullDepth));
                if (depth <= 0.0) continue;
                uint jitter = WallHash(e, side, i);
                if (!_refine)
                {
                    // The Low tier's coarse grids: one battered stone face per bay.
                    float lb0 = cutSide ? b0 - 0.15f : Math.Min(b0 - 0.6f, b0 - w), lb1 = cutSide ? b1 - 0.15f : Math.Min(b1 - 0.6f, b1 - w);
                    float lt0 = cutSide ? b0 + w : b0 + 0.06f, lt1 = cutSide ? b1 + w : b1 + 0.06f;
                    double bx = cutSide ? ux : -ux, bz = cutSide ? uz : -uz;
                    double sx = cutSide ? 0 : depth;
                    Quad(m, fx0 + ux * sx, lb0, fz0 + uz * sx, fx1 + ux * sx, lb1, fz1 + uz * sx, fx1 + ux * sx + bx * 0.1 * w, lt1, fz1 + uz * sx + bz * 0.1 * w,
                         fx0 + ux * sx + bx * 0.1 * w, lt0, fz0 + uz * sx + bz * 0.1 * w, (float)-bx, 0.1f, (float)-bz, MasonryStone, MaterialChannel.Stone, 0.85f);
                    continue;
                }
                if (cutSide)
                {
                    // A breast wall: from the verge up to the cut ground behind it, the courses stepping back into the hill.
                    float base0 = b0 - 0.15f, base1 = b1 - 0.15f;
                    float top0 = Math.Max(base0 + 0.6f, Drawn(fx0 + ux * depth, fz0 + uz * depth)), top1 = Math.Max(base1 + 0.6f, Drawn(fx1 + ux * depth, fz1 + uz * depth));
                    WallFace(m, fx0, fz0, fx1, fz1, ux, uz, base0, base1, top0, top1, depth, gabion, true, jitter);
                }
                else
                {
                    // A wall holding the fill: from the toe up to the verge, the courses stepping back toward the road.
                    float top0 = b0 + 0.06f, top1 = b1 + 0.06f;
                    double tx0 = fx0 + ux * (depth + 0.3), tz0 = fz0 + uz * (depth + 0.3), tx1 = fx1 + ux * (depth + 0.3), tz1 = fz1 + uz * (depth + 0.3);
                    float base0 = Math.Min(top0 - 0.6f, Drawn(tx0, tz0) - 0.3f), base1 = Math.Min(top1 - 0.6f, Drawn(tx1, tz1) - 0.3f);
                    WallFace(m, fx0 + ux * depth, fz0 + uz * depth, fx1 + ux * depth, fz1 + uz * depth, -ux, -uz, base0, base1, top0, top1, depth, gabion, false,
                             jitter);
                }
            }
        }

        private static uint WallHash(int e, int side, int i)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)e) * 16777619u;
                h = (h ^ (uint)side) * 16777619u;
                h = (h ^ (uint)i) * 16777619u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>
        /// One 2 m bay of wall between front points (x0, z0) and (x1, z1) on its foot line; (bx, bz) points from the face into
        /// the wall's body. A cut wall's face looks at the road (its courses step back toward +b as they rise); a fill wall's
        /// face looks at the valley (its courses step back toward +b, the road). Gabion: baskets with dark gaps between them
        /// and a ledge per course; masonry: one battered face and a coping.
        /// </summary>
        private static void WallFace(MeshData m, double x0, double z0, double x1, double z1, double bx, double bz, float base0, float base1, float top0, float top1,
                                     double depth, bool gabion, bool cut, uint jitter)
        {
            // Face normal: away from the body.
            float nx = (float)-bx, nz = (float)-bz;
            if (!gabion)
            {
                // Dry stone, 10 % batter, and a coping slab on top.
                float h0 = top0 - base0, h1 = top1 - base1;
                double ax0 = x0 + bx * 0.1 * h0, az0 = z0 + bz * 0.1 * h0, ax1 = x1 + bx * 0.1 * h1, az1 = z1 + bz * 0.1 * h1;
                uint stone = MeshColor.Scale(MasonryStone, 0.92f + 0.16f * ((jitter & 255) / 255f));
                Quad(m, x0, base0, z0, x1, base1, z1, ax1, top1, az1, ax0, top0, az0, nx, 0.1f, nz, stone, MaterialChannel.Stone, 0.85f);
                double cx0 = ax0 + bx * depth, cz0 = az0 + bz * depth, cx1 = ax1 + bx * depth, cz1 = az1 + bz * depth;
                Quad(m, ax0 - bx * 0.08, top0 + 0.12f, az0 - bz * 0.08, ax1 - bx * 0.08, top1 + 0.12f, az1 - bz * 0.08, cx1, top1 + 0.12f, cz1, cx0, top0 + 0.12f, cz0,
                     0f, 1f, 0f, Coping, MaterialChannel.Concrete, 1f);
                Quad(m, ax0 - bx * 0.08, top0 - 0.02f, az0 - bz * 0.08, ax1 - bx * 0.08, top1 - 0.02f, az1 - bz * 0.08, ax1 - bx * 0.08, top1 + 0.12f,
                     az1 - bz * 0.08, ax0 - bx * 0.08, top0 + 0.12f, az0 - bz * 0.08, nx, 0f, nz, Coping, MaterialChannel.Concrete, 0.9f);
                return;
            }
            float hMax = Math.Max(top0 - base0, top1 - base1);
            int courses = Math.Max(1, Math.Min(4, (int)Math.Ceiling(hMax / GabionCourseM - 0.15f)));
            const double Gap = 0.0; // courses meet (no backing behind them)
            double lx = x1 - x0, lz = z1 - z0, l = Math.Sqrt(lx * lx + lz * lz);
            double gx = l > 1e-6 ? lx / l * Gap : 0, gz = l > 1e-6 ? lz / l * Gap : 0;
            for (int c = 0; c < courses; c++)
            {
                // Course c: from base + c to base + c + 1 (the last one stops at the top), set back c steps.
                float f0 = (float)c / courses, f1 = (float)(c + 1) / courses;
                float y00 = base0 + (top0 - base0) * f0, y01 = base0 + (top0 - base0) * f1, y10 = base1 + (top1 - base1) * f0, y11 = base1 + (top1 - base1) * f1;
                double set = GabionStepM * c;
                double ax0 = x0 + bx * set, az0 = z0 + bz * set, ax1 = x1 + bx * set, az1 = z1 + bz * set;
                uint stone = MeshColor.Scale(GabionStone, 0.9f + 0.2f * (((jitter >> (c * 3 % 24)) & 255) / 255f));
                float ao = 0.75f + 0.25f * f1;
                // The basket face (the course's own shade; the course steps and ledges read as the baskets).
                Quad(m, ax0 + gx, y00 + (float)Gap, az0 + gz, ax1 - gx, y10 + (float)Gap, az1 - gz, ax1 - gx, y11 - (float)Gap, az1 - gz, ax0 + gx,
                     y01 - (float)Gap, az0 + gz, nx, 0.05f, nz, stone, MaterialChannel.Stone, ao);
                // The ledge on top of this course (to the next course's face, or the wall's top surface).
                double ledge = c + 1 < courses ? GabionStepM : depth - set;
                if (ledge > 0.01)
                {
                    Quad(m, ax0 + gx, y01 - (float)Gap, az0 + gz, ax1 - gx, y11 - (float)Gap, az1 - gz, ax1 - gx + bx * ledge, y11 - (float)Gap,
                         az1 - gz + bz * ledge, ax0 + gx + bx * ledge, y01 - (float)Gap, az0 + gz + bz * ledge, 0f, 1f, 0f, MeshColor.Scale(stone, 0.95f),
                         MaterialChannel.Stone, ao);
                }
            }
        }

        private void Parapet(MeshData m, int e, int side, float wa, float wb)
        {
            double len = _len[e];
            RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
            if (!HasParapet(cls, wa, wb)) return;
            int n = Math.Max(1, (int)Math.Round(len / (_refine ? PostSpacingM : 2f * PostSpacingM)));
            double sign = side == 0 ? 1 : -1;
            double ux = -_dz[e] * sign, uz = _dx[e] * sign;
            bool rail = Major(cls) && _refine;
            float lift = RoadMesher.LiftOf(Source.Roads[_road[e]], new RoadOptions());
            double prevX = 0, prevZ = 0;
            float prevY = 0f;
            bool prev = false;
            for (int k = 0; k <= n; k++)
            {
                if (k == 0 && (_flags[e] & ClipStart) == 0 && e > 0) { } // posts at segment starts are shared with the previous end
                float t = (float)k / n;
                float ext = Ext(e, side, t) + PostOffsetM;
                float bench = EdgeBench(e, side, t);
                double px, pz;
                At(e, t, side, ext, out px, out pz);
                bool drop = DropAt(e, side, t, wa, wb) && !OnOtherRoad(px, pz);
                if (!drop)
                {
                    prev = false;
                    continue;
                }
                float gy = bench + lift; // on the road's outer edge (shoulder or footpath)
                if (k < n || (_flags[e] & ClipEnd) != 0) Post(m, px, gy, pz, ux, uz, !_refine);
                if (rail && prev)
                {
                    // A W-beam between this post and the previous one, on the road side of the posts.
                    double rx0 = prevX - ux * 0.12, rz0 = prevZ - uz * 0.12, rx1 = px - ux * 0.12, rz1 = pz - uz * 0.12;
                    float ya = prevY + 0.42f, yb = gy + 0.42f, h = 0.3f;
                    Quad(m, rx0, ya, rz0, rx1, yb, rz1, rx1, yb + h, rz1, rx0, ya + h, rz0, (float)-ux, 0f, (float)-uz, RailSteel, MaterialChannel.Metal, 0.95f);
                    Quad(m, rx1, yb, rz1, rx0, ya, rz0, rx0, ya + h, rz0, rx1, yb + h, rz1, (float)ux, 0f, (float)uz, MeshColor.Scale(RailSteel, 0.8f),
                         MaterialChannel.Metal, 0.8f);
                }
                prev = true;
                prevX = px;
                prevZ = pz;
                prevY = gy;
            }
        }

        /// <summary>True when tile-local (x, z) (beyond a road's own extent) lies on another road's drawn extent.</summary>
        private bool OnOtherRoad(double x, double z)
        {
            float b;
            double gx, gz;
            return ExtentAt(x, z, out b, out gx, out gz);
        }

        /// <summary>Which roads get parapet posts along their drops: the main roads (trunk to tertiary) wherever the ground
        /// falls away, other carriageways only along a wall holding their fill (Kathmandu's lanes run beside open drops);
        /// never footways, paths or steps.</summary>
        private static bool HasParapet(RoadClass cls, float wa, float wb)
        {
            if (RoadWidthModel.IsFootClass(cls)) return false;
            return Major(cls) || Math.Min(wa, wb) < -0.5f;
        }

        /// <summary>True when the ground falls away beside segment <paramref name="e"/> at fraction <paramref name="t"/> on
        /// <paramref name="side"/>: a wall holds the fill there, or the carved ground <see cref="ParapetReachM"/> beyond the
        /// drawn extent lies more than <see cref="ParapetDropM"/> below the road's edge. Measured on the carve of the tile's full-resolution ground,
        /// so every tier and the solids agree.</summary>
        private bool DropAt(int e, int side, float t, float wa, float wb)
        {
            float w = wa + (wb - wa) * t;
            if (w < -0.5f) return true;
            double ox, oz;
            At(e, t, side, Ext(e, side, t) + ParapetReachM, out ox, out oz);
            return EdgeBench(e, side, t) - CarvedAt(ox, oz, SourceNatural(ox, oz)) > ParapetDropM;
        }

        /// <summary>
        /// The barriers of the carve for the solids (game metres; appended to <paramref name="into"/>, returns how many):
        /// the parapets and guard rails along every drop (<see cref="AppendWalls"/> draws them; a body walking or riding off
        /// the road's edge there is stopped, from below the bench to 0.9 m above the road) and the breast walls holding the
        /// cuts (their face, from the verge up to the cut ground behind). Built from the bench and the full-resolution
        /// ground, so the solids of the 8 m sampler match what every tier draws.
        /// </summary>
        public int CopyBarriers(List<CarveBarrier> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int before = into.Count;
            double x0 = Source.Tile.X0, z0 = Source.Tile.Z0;
            for (int e = 0; e < _segCount; e++)
            {
                RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
                float lift = _road[e] >= 0 && _road[e] < Source.Roads.Count ? RoadMesher.LiftOf(Source.Roads[_road[e]], new RoadOptions()) : 0.25f;
                for (int side = 0; side < 2; side++)
                {
                    float wa = side == 0 ? _wla[e] : _wra[e], wb = side == 0 ? _wlb[e] : _wrb[e];
                    // Parapets: the drop stretches between posts (as Parapet draws them).
                    if (HasParapet(cls, wa, wb))
                    {
                        int n = Math.Max(1, (int)Math.Round(_len[e] / (_refine ? PostSpacingM : 2f * PostSpacingM)));
                        bool prev = false;
                        double pxPrev = 0, pzPrev = 0;
                        float bPrev = 0f;
                        for (int k = 0; k <= n; k++)
                        {
                            float t = (float)k / n;
                            double px, pz;
                            At(e, t, side, Ext(e, side, t) + PostOffsetM, out px, out pz);
                            if (!DropAt(e, side, t, wa, wb) || OnOtherRoad(px, pz))
                            {
                                prev = false;
                                continue;
                            }
                            float bench = EdgeBench(e, side, t);
                            if (prev)
                                into.Add(new CarveBarrier
                                {
                                    AX = x0 + pxPrev, AZ = z0 + pzPrev, BX = x0 + px, BZ = z0 + pz, Bottom = Math.Min(bPrev, bench) - 0.3f,
                                    Top = Math.Max(bPrev, bench) + lift + 0.9f,
                                });
                            prev = true;
                            pxPrev = px;
                            pzPrev = pz;
                            bPrev = bench;
                        }
                    }
                    // Breast walls: the face line of every bay with a cut wall.
                    if (wa + wb <= 0f || Math.Max(wa, wb) < 0.5f) continue;
                    int bays = Math.Max(1, (int)Math.Ceiling(_len[e] / GabionLengthM));
                    for (int i = 0; i < bays; i++)
                    {
                        float t0 = (float)i / bays, t1 = (float)(i + 1) / bays, tm = 0.5f * (t0 + t1);
                        float w = wa + (wb - wa) * tm;
                        if (w < 0.5f) continue;
                        double ax, az, bx, bz;
                        At(e, t0, side, Ext(e, side, t0) + WallVergeM, out ax, out az);
                        At(e, t1, side, Ext(e, side, t1) + WallVergeM, out bx, out bz);
                        // As Wall draws it: no bay where another road leaves no room behind the face.
                        double sgn = side == 0 ? 1 : -1, ox = -_dz[e] * sgn, oz = _dx[e] * sgn;
                        if (ClearDepth(ax + ox * 0.05, az + oz * 0.05, ox, oz, WallDepthM) <= 0.0 || ClearDepth(bx + ox * 0.05, bz + oz * 0.05, ox, oz, WallDepthM) <= 0.0 ||
                            ClearDepth(0.5 * (ax + bx) + ox * 0.05, 0.5 * (az + bz) + oz * 0.05, ox, oz, WallDepthM) <= 0.0) continue;
                        float b0 = EdgeBench(e, side, t0), b1 = EdgeBench(e, side, t1);
                        into.Add(new CarveBarrier
                        {
                            AX = x0 + ax, AZ = z0 + az, BX = x0 + bx, BZ = z0 + bz, Bottom = Math.Min(b0, b1) - 0.3f, Top = Math.Max(b0, b1) + w, Wall = true,
                        });
                    }
                }
            }
            return into.Count - before;
        }

        /// <summary>A parapet post: white concrete, 0.18 m square, standing 0.78 m above the road surface
        /// <paramref name="y"/> with a black band at the top, its foot sunk 0.5 m into the embankment's crest.</summary>
        private static void Post(MeshData m, double x, float y, double z, double ux, double uz, bool lite)
        {
            const double H = 0.09;
            double vx = -uz, vz = ux; // along the road
            if (lite)
            {
                Box(m, x, y - 0.5f, z, ux, uz, vx, vz, H, 1.28f, PostWhite, MaterialChannel.Paint, 0.9f);
                return;
            }
            Box(m, x, y - 0.5f, z, ux, uz, vx, vz, H, 1.08f, PostWhite, MaterialChannel.Paint, 0.9f);
            Box(m, x, y + 0.58f, z, ux, uz, vx, vz, H, 0.2f, PostBlack, MaterialChannel.Paint, 1f);
        }

        /// <summary>Parapet posts and guard rails stand this far beyond the road's drawn extent, on the crest of the
        /// embankment (never on the carriageway, footpath or shoulder).</summary>
        public const float PostOffsetM = 0.15f;

        /// <summary>An upright box (four sides and a top) of half side <paramref name="h"/> and height <paramref name="height"/>
        /// standing at (x, y, z).</summary>
        private static void Box(MeshData m, double x, float y, double z, double ux, double uz, double vx, double vz, double h, float height, uint c,
                                MaterialChannel ch, float ao)
        {
            float y1 = y + height;
            for (int f = 0; f < 4; f++)
            {
                double nx = f == 0 ? ux : f == 1 ? vx : f == 2 ? -ux : -vx, nz = f == 0 ? uz : f == 1 ? vz : f == 2 ? -uz : -vz;
                double tx = -nz, tz = nx;
                double cx = x + nx * h, cz = z + nz * h;
                Quad(m, cx - tx * h, y, cz - tz * h, cx + tx * h, y, cz + tz * h, cx + tx * h, y1, cz + tz * h, cx - tx * h, y1, cz - tz * h, (float)nx, 0f,
                     (float)nz, c, ch, ao);
            }
            Quad(m, x - ux * h - vx * h, y1, z - uz * h - vz * h, x + ux * h - vx * h, y1, z + uz * h - vz * h, x + ux * h + vx * h, y1, z + uz * h + vz * h,
                 x - ux * h + vx * h, y1, z - uz * h + vz * h, 0f, 1f, 0f, c, ch, ao);
        }

        /// <summary>A quad (a, b, c, d in order around it) facing the normal (nx, ny, nz): the winding is chosen so the front
        /// (Unity: cross(b - a, c - a)) faces it.</summary>
        private static void Quad(MeshData m, double ax, float ay, double az, double bx, float by, double bz, double cx, float cy, double cz, double dx, float dy,
                                 double dz, float nx, float ny, float nz, uint rgba, MaterialChannel ch, float ao)
        {
            float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-6f) return;
            nx /= l;
            ny /= l;
            nz /= l;
            int v = m.AddVertex((float)ax, ay, (float)az, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)bx, by, (float)bz, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)cx, cy, (float)cz, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)dx, dy, (float)dz, nx, ny, nz, rgba, (float)ch, ao);
            double ex = bx - ax, ey = by - ay, ez = bz - az, fx = cx - ax, fy = cy - ay, fz = cz - az;
            double cxp = ey * fz - ez * fy, cyp = ez * fx - ex * fz, czp = ex * fy - ey * fx;
            if (cxp * nx + cyp * ny + czp * nz >= 0)
            {
                m.AddTriangle(v, v + 1, v + 2);
                m.AddTriangle(v, v + 2, v + 3);
            }
            else
            {
                m.AddTriangle(v, v + 2, v + 1);
                m.AddTriangle(v, v + 3, v + 2);
            }
        }
    }
}
