using System;
using System.Collections.Generic;

namespace Ghumante.Core.Data
{
    /// <summary>One sacred zone (W2_DESIGN 10.3). <see cref="CX"/>/<see cref="CZ"/> is the kora centre (stupa or
    /// temple) in game metres when <see cref="Kora"/>, else the zone's centroid.</summary>
    public struct SacredZone
    {
        public long AreaRef;
        public SacredZoneKind Kind;
        public string HeroId;
        public EntryRule Rule;
        public bool Kora;

        /// <summary>Walking direction of the kora (clockwise except at Bon sites).</summary>
        public KoraDirection KoraDir;

        public double CX, CZ;

        /// <summary>Plan area in m² (the smallest zone wins where zones nest).</summary>
        public double AreaM2;
    }

    /// <summary>
    /// The one answer to "is this point sacred?" (W2_DESIGN 10.3): compounds, courtyards, heritage squares, stupa koras
    /// and ghats, built per tile from AREA RELIGIOUS / COURTYARD (38) / pedestrian squares named as Durbar squares,
    /// the courtyard holes of building multipolygons (bahals and chowks) and curated compounds. Traffic uses it to
    /// keep motor vehicles out (L16 SACRED_NO_VEHICLE), gameplay for calm mode and prompts, audio for the courtyard
    /// snapshot and pedestrians for the clockwise kora. Thread-safe (one lock); queries are a grid lookup plus
    /// point-in-triangle tests. Coordinates are game metres.
    /// </summary>
    public sealed class SacredZoneIndex
    {
        /// <summary>Courtyard holes smaller than this (m²) are light wells, not walkable courtyards.</summary>
        public const double MinCourtyardM2 = 40.0;

        private const double CellM = 32.0;

        private sealed class Zone
        {
            public SacredZone Info;

            /// <summary>Triangles as game-metre xyz... flattened (x0, z0, x1, z1, x2, z2) per triangle.</summary>
            public double[] Tris;

            public int TriCount;
            public double MinX, MinZ, MaxX, MaxZ;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<ulong, List<Zone>> _byTile = new Dictionary<ulong, List<Zone>>();
        private readonly Dictionary<long, List<Zone>> _grid = new Dictionary<long, List<Zone>>();

        public int ZoneCount
        {
            get
            {
                lock (_lock)
                {
                    int n = 0;
                    foreach (var l in _byTile.Values) n += l.Count;
                    return n;
                }
            }
        }

        private static long Key(long cx, long cz)
        {
            return cx << 32 ^ (cz & 0xFFFFFFFFL);
        }

        /// <summary>Index the zones of a leaf tile (replacing an earlier add). <paramref name="db"/> may be null; when
        /// given, a zone whose area or contained building is a curated anchor or compound gets its hero id, entry rule
        /// and kora flag.</summary>
        public void AddTile(TileId id, TileData t, CuratedDb db)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var zones = new List<Zone>();
            double x0 = id.X0, z0 = id.Z0;
            foreach (AreaRecord a in t.Areas)
            {
                SacredZoneKind kind;
                if (!ZoneKindOf(t, a, out kind)) continue;
                Zone z = FromArea(a, x0, z0);
                if (z == null) continue;
                z.Info.Kind = kind;
                z.Info.AreaRef = (long)a.OsmRef;
                Curate(z, db, a.OsmRef, t, x0, z0);
                zones.Add(z);
            }
            // Bahal and chowk holes arrive as AREA COURTYARD (sacred.py prepare_courtyards, the set D14 routing
            // uses). For packs without them, only a bahal/chowk-named building's holes fall back to courtyards, never
            // a ministry's, mall's or school's court.
            HashSet<ulong> areaRefs = null;
            foreach (BuildingRecord b in t.Buildings)
            {
                if (b.Rings.Length < 2 || !IsCourtyardName(t, b.NameRef)) continue;
                if (areaRefs == null)
                {
                    areaRefs = new HashSet<ulong>();
                    foreach (AreaRecord a in t.Areas) areaRefs.Add(a.OsmRef);
                }
                if (areaRefs.Contains(b.OsmRef)) continue;
                for (int r = 1; r < b.Rings.Length; r++)
                {
                    double area = Math.Abs(AreaTypeGrid.RingArea(b.Rings[r]));
                    if (area < MinCourtyardM2) continue;
                    Zone z = FromRing(b.Rings[r], x0, z0);
                    if (z == null) continue;
                    z.Info.Kind = SacredZoneKind.Courtyard;
                    z.Info.AreaRef = (long)b.OsmRef;
                    Curate(z, db, b.OsmRef, t, x0, z0);
                    zones.Add(z);
                }
            }
            lock (_lock)
            {
                RemoveLocked(id.Key);
                _byTile[id.Key] = zones;
                foreach (Zone z in zones) Insert(z);
            }
        }

        public void RemoveTile(TileId id)
        {
            lock (_lock) RemoveLocked(id.Key);
        }

        private void RemoveLocked(ulong key)
        {
            List<Zone> old;
            if (!_byTile.TryGetValue(key, out old)) return;
            foreach (Zone z in old)
                ForCells(z.MinX, z.MinZ, z.MaxX, z.MaxZ, k =>
                {
                    List<Zone> l;
                    if (_grid.TryGetValue(k, out l))
                    {
                        l.Remove(z);
                        if (l.Count == 0) _grid.Remove(k);
                    }
                });
            _byTile.Remove(key);
        }

        private void Insert(Zone z)
        {
            ForCells(z.MinX, z.MinZ, z.MaxX, z.MaxZ, k =>
            {
                List<Zone> l;
                if (!_grid.TryGetValue(k, out l)) _grid[k] = l = new List<Zone>(2);
                l.Add(z);
            });
        }

        private static void ForCells(double minX, double minZ, double maxX, double maxZ, Action<long> f)
        {
            long cx0 = (long)Math.Floor(minX / CellM), cx1 = (long)Math.Floor(maxX / CellM);
            long cz0 = (long)Math.Floor(minZ / CellM), cz1 = (long)Math.Floor(maxZ / CellM);
            for (long cz = cz0; cz <= cz1; cz++)
            for (long cx = cx0; cx <= cx1; cx++)
                f(Key(cx, cz));
        }

        /// <summary>Which AREA records are sacred zones: RELIGIOUS (compounds, or stupa koras when a stupa stands in
        /// them), COURTYARD, anything the pipeline flagged SACRED_NO_VEHICLE or HERITAGE_ZONE (the same records D14
        /// routing uses), and, for packs without those flags, pedestrian squares named as Durbar squares or ghats.</summary>
        internal static bool ZoneKindOf(TileData t, AreaRecord a, out SacredZoneKind kind)
        {
            kind = SacredZoneKind.Compound;
            if (a.Kind == AreaKind.Religious) return true;
            if (a.Kind == AreaKind.Courtyard)
            {
                kind = SacredZoneKind.Courtyard;
                return true;
            }
            bool heritage = (a.Flags & AreaFlags.HeritageZone) != 0;
            if (heritage || (a.Flags & AreaFlags.SacredNoVehicle) != 0)
            {
                kind = heritage && a.Kind != AreaKind.Religious ? SacredZoneKind.HeritageSquare : SacredZoneKind.Compound;
                if (a.Kind == AreaKind.Pedestrian && IsGhat(t, a)) kind = SacredZoneKind.Ghat;
                return true;
            }
            if (a.Kind == AreaKind.Pedestrian)
            {
                NameRecord n = a.NameRef > 0 && a.NameRef <= t.Names.Count ? t.Name(a.NameRef) : null;
                string s = n == null ? null : NameText(n);
                if (s != null)
                {
                    string l = s.ToLowerInvariant();
                    if (l.Contains("durbar") || l.Contains("darbar") || l.Contains("दरबार"))
                    {
                        kind = SacredZoneKind.HeritageSquare;
                        return true;
                    }
                    if (l.Contains("ghat") || l.Contains("घाट"))
                    {
                        kind = SacredZoneKind.Ghat;
                        return true;
                    }
                }
            }
            return false;
        }

        // tags.py COURTYARD_NAME_RE: bah(?:a|al|il|i)\b | baha\b | bahal | chowk | \bchok\b | dabali | vihar | बहाल |
        // बही | चोक | दबली | विहार. Mode 0: anywhere, 1: word end, 2: word start and end.
        private static readonly string[] CourtyardWords =
            { "bahal", "chowk", "dabali", "vihar", "बहाल", "बही", "चोक", "दबली", "विहार", "baha", "bahil", "bahi", "chok" };

        private static readonly byte[] CourtyardModes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2 };

        /// <summary>The courtyard name rule of the pipeline (tags.py COURTYARD_NAME_RE: bahal, bahi, chowk, dabali,
        /// vihar) on the default, English and Nepali names.</summary>
        internal static bool IsCourtyardName(TileData t, int nameRef)
        {
            if (nameRef <= 0 || nameRef > t.Names.Count) return false;
            NameRecord n = t.Name(nameRef);
            if (n == null) return false;
            string s = NameText(n).ToLowerInvariant();
            for (int k = 0; k < CourtyardWords.Length; k++)
            {
                string w = CourtyardWords[k];
                int i = 0;
                while ((i = s.IndexOf(w, i, StringComparison.Ordinal)) >= 0)
                {
                    int e = i + w.Length;
                    bool ok = CourtyardModes[k] == 0
                              || (!IsWordChar(s, e) && (CourtyardModes[k] == 1 || !IsWordChar(s, i - 1)));
                    if (ok) return true;
                    i++;
                }
            }
            return false;
        }

        private static bool IsWordChar(string s, int i)
        {
            if (i < 0 || i >= s.Length) return false;
            char c = s[i];
            return char.IsLetterOrDigit(c) || c == '_' || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark
                   || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.SpacingCombiningMark;
        }

        /// <summary>The <see cref="SacredZone.AreaRef"/> of every zone indexed for a tile (tests).</summary>
        internal List<long> ZoneRefs(TileId id)
        {
            var l = new List<long>();
            lock (_lock)
            {
                List<Zone> zs;
                if (_byTile.TryGetValue(id.Key, out zs))
                    foreach (Zone z in zs) l.Add(z.Info.AreaRef);
            }
            return l;
        }

        private static bool IsGhat(TileData t, AreaRecord a)
        {
            NameRecord n = a.NameRef > 0 && a.NameRef <= t.Names.Count ? t.Name(a.NameRef) : null;
            string s = n == null ? null : NameText(n).ToLowerInvariant();
            return s != null && (s.Contains("ghat") || s.Contains("घाट"));
        }

        private static string NameText(NameRecord n)
        {
            return (n.Default ?? "") + " " + (n.En ?? "") + " " + (n.Ne ?? "");
        }

        private static Zone FromArea(AreaRecord a, double x0, double z0)
        {
            int nt = a.Indices.Length / 3;
            if (nt == 0) return null;
            var z = new Zone { Tris = new double[nt * 6], TriCount = nt, MinX = double.MaxValue, MinZ = double.MaxValue, MaxX = double.MinValue, MaxZ = double.MinValue };
            double area = 0, sx = 0, sz = 0;
            for (int t = 0; t < nt; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = a.Indices[3 * t + k];
                    double x = x0 + a.Vertices[2 * v] / 100.0, zz = z0 + a.Vertices[2 * v + 1] / 100.0;
                    z.Tris[6 * t + 2 * k] = x;
                    z.Tris[6 * t + 2 * k + 1] = zz;
                    Grow(z, x, zz);
                }
                double ta = TriArea(z.Tris, t);
                area += ta;
                sx += ta * (z.Tris[6 * t] + z.Tris[6 * t + 2] + z.Tris[6 * t + 4]) / 3;
                sz += ta * (z.Tris[6 * t + 1] + z.Tris[6 * t + 3] + z.Tris[6 * t + 5]) / 3;
            }
            z.Info.AreaM2 = area;
            z.Info.CX = area > 0 ? sx / area : z.MinX;
            z.Info.CZ = area > 0 ? sz / area : z.MinZ;
            return z;
        }

        /// <summary>A ring (any winding) as a zone, triangulated by ear clipping.</summary>
        private static Zone FromRing(int[] ring, double x0, double z0)
        {
            int n = ring.Length / 2;
            if (n < 3) return null;
            var xs = new double[n];
            var zs = new double[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = x0 + ring[2 * i] / 100.0;
                zs[i] = z0 + ring[2 * i + 1] / 100.0;
            }
            double signed = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) signed += xs[j] * zs[i] - xs[i] * zs[j];
            if (signed < 0)
            {
                Array.Reverse(xs);
                Array.Reverse(zs);
            }
            var tris = new int[3 * (n - 2)];
            int nt = Meshing.Polygon.Triangulate(xs, zs, n, tris, new int[n], new int[n], Meshing.Polygon.IsConvex(xs, zs, n, 0.0));
            if (nt == 0) return null;
            var z = new Zone { Tris = new double[nt * 6], TriCount = nt, MinX = double.MaxValue, MinZ = double.MaxValue, MaxX = double.MinValue, MaxZ = double.MinValue };
            double area = 0;
            for (int t = 0; t < nt; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[3 * t + k];
                    z.Tris[6 * t + 2 * k] = xs[v];
                    z.Tris[6 * t + 2 * k + 1] = zs[v];
                    Grow(z, xs[v], zs[v]);
                }
                area += TriArea(z.Tris, t);
            }
            double cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += xs[i];
                cz += zs[i];
            }
            z.Info.AreaM2 = area;
            z.Info.CX = cx / n;
            z.Info.CZ = cz / n;
            return z;
        }

        private static void Grow(Zone z, double x, double zz)
        {
            if (x < z.MinX) z.MinX = x;
            if (x > z.MaxX) z.MaxX = x;
            if (zz < z.MinZ) z.MinZ = zz;
            if (zz > z.MaxZ) z.MaxZ = zz;
        }

        private static double TriArea(double[] tr, int t)
        {
            int o = 6 * t;
            return 0.5 * Math.Abs((tr[o + 2] - tr[o]) * (tr[o + 5] - tr[o + 1]) - (tr[o + 3] - tr[o + 1]) * (tr[o + 4] - tr[o]));
        }

        /// <summary>Hero id, entry rule and kora from the curated DB; without a DB, a RELIGIOUS area holding a stupa at
        /// least 15 m across becomes a stupa kora centred on the stupa. A curated stupa kora is centred on its anchor
        /// building (else the largest stupa inside), never on the compound's centroid: Swayambhu's landuse centroid is
        /// ≈ 59 m from the dome, so a kora about it would circle empty hillside.</summary>
        private static void Curate(Zone z, CuratedDb db, ulong osmRef, TileData t, double x0, double z0)
        {
            ulong anchor = 0;
            if (db != null)
            {
                HeritageRecord hit = null;
                foreach (HeritageRecord r in db.Heritage)
                {
                    if (r.AnchorWayRelationRef == osmRef || r.CompoundRef == osmRef || HeritageRecord.WayRelationRef(r.Compound) == osmRef)
                    {
                        hit = r;
                        break;
                    }
                }
                // A curated stupa kora anchored on a node with no compound (Kathesimbhu) matches the compound its
                // anchor point stands in.
                if (hit == null && z.Info.Kind == SacredZoneKind.Compound)
                {
                    foreach (HeritageRecord r in db.Heritage)
                    {
                        if (r.Kind != HeritageKind.Stupa || r.Kora == KoraDirection.None || r.CompoundRef != 0) continue;
                        if (double.IsNaN(r.X) || double.IsNaN(r.Z) || !Contains(z, r.X, r.Z)) continue;
                        hit = r;
                        break;
                    }
                }
                if (hit != null)
                {
                    z.Info.HeroId = hit.Id;
                    z.Info.Rule = hit.Entry;
                    if (hit.Kora != KoraDirection.None)
                    {
                        z.Info.Kora = true;
                        z.Info.KoraDir = hit.Kora;
                        if (hit.Kind == HeritageKind.Stupa)
                        {
                            z.Info.Kind = SacredZoneKind.StupaKora;
                            anchor = hit.AnchorWayRelationRef;
                            // The curated anchor point centres the kora until the anchor footprint (below) refines it.
                            if (!double.IsNaN(hit.X) && !double.IsNaN(hit.Z) && Contains(z, hit.X, hit.Z))
                            {
                                z.Info.CX = hit.X;
                                z.Info.CZ = hit.Z;
                            }
                        }
                    }
                }
            }
            bool curatedKora = z.Info.Kind == SacredZoneKind.StupaKora;
            if (z.Info.Kind != SacredZoneKind.Compound && !curatedKora) return;
            // A stupa inside the compound makes it a kora (Boudha, Swayambhu, Kathesimbhu); a curated stupa kora is
            // centred on its anchor building when the tile has it.
            double best = 0;
            foreach (BuildingRecord b in t.Buildings)
            {
                bool isAnchor = anchor != 0 && b.OsmRef == anchor;
                if (!isAnchor && b.Archetype != BuildingArchetype.Stupa && b.Archetype != BuildingArchetype.Chorten) continue;
                if (b.Rings == null || b.Rings.Length == 0 || b.Rings[0].Length < 6) continue;
                int[] ring = b.Rings[0];
                double minx = double.MaxValue, maxx = double.MinValue, sx = 0, sz = 0;
                int n = ring.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    double x = x0 + ring[2 * i] / 100.0;
                    if (x < minx) minx = x;
                    if (x > maxx) maxx = x;
                    sx += x;
                    sz += z0 + ring[2 * i + 1] / 100.0;
                }
                double span = maxx - minx;
                double cx = sx / n, cz = sz / n;
                if (isAnchor)
                {
                    z.Info.CX = cx;
                    z.Info.CZ = cz;
                    return;
                }
                if (span < 15 || span <= best || !Contains(z, cx, cz)) continue;
                best = span;
                if (!curatedKora)
                {
                    z.Info.Kora = true;
                    z.Info.KoraDir = KoraDirection.Clockwise;
                    z.Info.Kind = SacredZoneKind.StupaKora;
                }
                z.Info.CX = cx;
                z.Info.CZ = cz;
            }
        }

        private static bool Contains(Zone z, double x, double zz)
        {
            if (x < z.MinX || x > z.MaxX || zz < z.MinZ || zz > z.MaxZ) return false;
            for (int t = 0; t < z.TriCount; t++)
                if (InTri(z.Tris, t, x, zz)) return true;
            return false;
        }

        private static bool InTri(double[] tr, int t, double px, double pz)
        {
            int o = 6 * t;
            double ax = tr[o], az = tr[o + 1], bx = tr[o + 2], bz = tr[o + 3], cx = tr[o + 4], cz = tr[o + 5];
            double d1 = (bx - ax) * (pz - az) - (bz - az) * (px - ax);
            double d2 = (cx - bx) * (pz - bz) - (cz - bz) * (px - bx);
            double d3 = (ax - cx) * (pz - cz) - (az - cz) * (px - cx);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>The smallest zone containing (x, z) (a courtyard inside a compound wins).</summary>
        public bool TryGetZone(double x, double z, out SacredZone zone)
        {
            zone = default(SacredZone);
            long key = Key((long)Math.Floor(x / CellM), (long)Math.Floor(z / CellM));
            lock (_lock)
            {
                List<Zone> l;
                if (!_grid.TryGetValue(key, out l)) return false;
                Zone best = null;
                foreach (Zone c in l)
                    if ((best == null || c.Info.AreaM2 < best.Info.AreaM2) && Contains(c, x, z)) best = c;
                if (best == null) return false;
                zone = best.Info;
                return true;
            }
        }

        public bool Contains(double x, double z)
        {
            SacredZone s;
            return TryGetZone(x, z, out s);
        }

        /// <summary>True when the segment touches any zone (an end inside, or crossing a zone triangle).</summary>
        public bool IntersectsSegment(double x0, double z0, double x1, double z1)
        {
            double minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1), minZ = Math.Min(z0, z1), maxZ = Math.Max(z0, z1);
            long cx0 = (long)Math.Floor(minX / CellM), cx1 = (long)Math.Floor(maxX / CellM);
            long cz0 = (long)Math.Floor(minZ / CellM), cz1 = (long)Math.Floor(maxZ / CellM);
            lock (_lock)
            {
                for (long cz = cz0; cz <= cz1; cz++)
                for (long cx = cx0; cx <= cx1; cx++)
                {
                    List<Zone> l;
                    if (!_grid.TryGetValue(Key(cx, cz), out l)) continue;
                    foreach (Zone zn in l)
                    {
                        if (maxX < zn.MinX || minX > zn.MaxX || maxZ < zn.MinZ || minZ > zn.MaxZ) continue;
                        for (int t = 0; t < zn.TriCount; t++)
                            if (SegmentHitsTri(zn.Tris, t, x0, z0, x1, z1)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The first point where the segment (x0, z0)→(x1, z1) touches a zone whose kind is not in
        /// <paramref name="ignoreKinds"/> (bit <c>1 &lt;&lt; (int)kind</c>): <paramref name="t"/> in [0, 1] along the
        /// segment (0 when the start is inside). False when it touches none.</summary>
        public bool TryFirstEntry(double x0, double z0, double x1, double z1, uint ignoreKinds, out double t)
        {
            t = double.PositiveInfinity;
            double minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1), minZ = Math.Min(z0, z1), maxZ = Math.Max(z0, z1);
            long cx0 = (long)Math.Floor(minX / CellM), cx1 = (long)Math.Floor(maxX / CellM);
            long cz0 = (long)Math.Floor(minZ / CellM), cz1 = (long)Math.Floor(maxZ / CellM);
            lock (_lock)
            {
                for (long cz = cz0; cz <= cz1; cz++)
                for (long cx = cx0; cx <= cx1; cx++)
                {
                    List<Zone> l;
                    if (!_grid.TryGetValue(Key(cx, cz), out l)) continue;
                    foreach (Zone zn in l)
                    {
                        if ((ignoreKinds & (1u << (int)zn.Info.Kind)) != 0) continue;
                        if (maxX < zn.MinX || minX > zn.MaxX || maxZ < zn.MinZ || minZ > zn.MaxZ) continue;
                        for (int k = 0; k < zn.TriCount && t > 0; k++)
                        {
                            double h = SegmentEntryT(zn.Tris, k, x0, z0, x1, z1);
                            if (h < t) t = h;
                        }
                    }
                }
            }
            return t <= 1.0;
        }

        /// <summary>The smallest segment parameter at which it touches triangle <paramref name="k"/> (∞ if never).</summary>
        private static double SegmentEntryT(double[] tr, int k, double x0, double z0, double x1, double z1)
        {
            if (InTri(tr, k, x0, z0)) return 0.0;
            double best = double.PositiveInfinity;
            int o = 6 * k;
            double dx = x1 - x0, dz = z1 - z0;
            for (int e = 0; e < 3; e++)
            {
                int a = o + 2 * e, b = o + 2 * ((e + 1) % 3);
                double ex = tr[b] - tr[a], ez = tr[b + 1] - tr[a + 1];
                double den = dx * ez - dz * ex;
                if (Math.Abs(den) < 1e-12) continue;
                double qx = tr[a] - x0, qz = tr[a + 1] - z0;
                double s = (qx * ez - qz * ex) / den, u = (qx * dz - qz * dx) / den;
                if (s >= 0 && s <= 1 && u >= 0 && u <= 1 && s < best) best = s;
            }
            return best;
        }

        private static bool SegmentHitsTri(double[] tr, int t, double x0, double z0, double x1, double z1)
        {
            if (InTri(tr, t, x0, z0) || InTri(tr, t, x1, z1)) return true;
            int o = 6 * t;
            for (int e = 0; e < 3; e++)
            {
                int a = o + 2 * e, b = o + 2 * ((e + 1) % 3);
                if (SegmentsCross(x0, z0, x1, z1, tr[a], tr[a + 1], tr[b], tr[b + 1])) return true;
            }
            return false;
        }

        private static bool SegmentsCross(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            double d1 = Orient(cx, cz, dx, dz, ax, az), d2 = Orient(cx, cz, dx, dz, bx, bz);
            double d3 = Orient(ax, az, bx, bz, cx, cz), d4 = Orient(ax, az, bx, bz, dx, dz);
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        private static double Orient(double ax, double az, double bx, double bz, double cx, double cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }
    }
}
