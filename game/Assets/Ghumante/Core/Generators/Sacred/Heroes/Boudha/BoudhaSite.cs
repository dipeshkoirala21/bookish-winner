using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// Where the Boudhanath site stands in a tile: the centroid of the lowest terrace (OSM w56688295, the hero anchor),
    /// the stupa axis (the centre of the upper terraces, 1.85 m north of it on the site axis), the site axes turned
    /// <see cref="BoudhaSite.AxisBearingDeg"/> clockwise from north (faces and stairs at 9°, 99°, 189° and 279°) and the
    /// kora level (the plaza is one flat level; boudha.md 2.3). <see cref="Site"/> maps site-local (u east', y up,
    /// v north') at the stupa axis on the kora to tile-local metres with absolute heights.
    /// </summary>
    internal struct BoudhaFrame
    {
        /// <summary>Tile-local position of the T1 centroid.</summary>
        public double OX, OZ;

        /// <summary>Tile-local position of the stupa axis.</summary>
        public double AX, AZ;

        /// <summary>Absolute kora level (m).</summary>
        public double Level;

        /// <summary>Site-local (u, y, v) at the stupa axis on the kora → tile-local (x, absolute y, z).</summary>
        public Affine3 Site;

        /// <summary>The compound wall's centre line in the site frame (counter-clockwise), fitted to the tile's drawn
        /// roads (<see cref="BoudhaWall.Fit"/>); null before fitting (then the OSM line is used).</summary>
        public double[] WallU, WallV;

        /// <summary>Points of <see cref="WallU"/>.</summary>
        public int WallN;

        /// <summary>The tile the frame was located in (its drawn roads keep the site's solids off the kora walks).</summary>
        public TileData Tile;

        /// <summary>Site-local (u, v) of a point given in true (east, north) metres from the T1 centroid.</summary>
        public static void FromTrue(double e, double n, out double u, out double v)
        {
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            double lu = e * c - n * s, lv = e * s + n * c;
            u = lu - BoudhaSite.AxisOffsetU;
            v = lv - BoudhaSite.AxisOffsetV;
        }

        /// <summary>Tile-local (x, z) of a site-local (u, v).</summary>
        public void ToTile(double u, double v, out double x, out double z)
        {
            double y;
            Site.Point(u, 0, v, out x, out y, out z);
        }

        /// <summary>Site-local (u, v) of a tile-local (x, z).</summary>
        public void ToSite(double x, double z, out double u, out double v)
        {
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            double dx = x - AX, dz = z - AZ;
            u = dx * c - dz * s;
            v = dx * s + dz * c;
        }
    }

    /// <summary>The structure groups of the bespoke Boudhanath site, each built as a hero of its own (its own LODs in the
    /// hero allocator, picked by its own screen height): the Great Stupa on its terraces, the four quarters of the kora
    /// (compound wall, gates and houses, ring of houses and gompas, plaza) and the main gate on Boudha Main Road.</summary>
    public enum BoudhaPart : byte
    {
        Stupa = 0,
        North = 1,
        East = 2,
        South = 3,
        West = 4,
        Gate = 5,
    }

    /// <summary>
    /// The bespoke Boudhanath heritage site (W2 monuments round; docs/research/heritage/boudha.md): every structure
    /// modelled to its measured plan, section and colours instead of the parametric stupa template. The curated hero
    /// (<c>her.ktm.boudhanath</c>) builds the Great Stupa (S1: terraces, chortens, statues, stairs, ring, dome, harmika,
    /// spire, crown and the prayer flags); five parts (<see cref="BoudhaPart"/>, <see cref="HeroRecipe.SiteOf"/>) build
    /// the rest by quarter of the kora (45° either side of the four site axes): the compound wall with the 147
    /// prayer-wheel niches (S2), the north entrance with the Ajima shrine, its gate pavilions and the NE compound house
    /// (S3, S4), the Guru Lhakhang and the Big Bell (S5), the ring of houses round the kora (S7), the Jamchen Lhakhang
    /// (S8), Old Dhabzang (S9), the compound's lamp houses, kilns, flagpoles and trees and the paved plaza; and the main
    /// gate on Boudha Main Road with the paved lane (S6). The front-row houses a quarter rebuilds are hidden from the house
    /// bands (<see cref="HideRefs"/> through <see cref="HeroRecipe.Hide"/>). Four LODs per part: LOD0 the full detail,
    /// LOD1 about 30 %, LOD2 about 8 % (both keep the silhouette), LOD3 the far shape. The whole site stays within about
    /// 150,000 triangles at LOD0.
    /// </summary>
    public static class BoudhaSite
    {
        /// <summary>The curated hero id (the Great Stupa).</summary>
        public const string HeroId = "her.ktm.boudhanath";

        /// <summary>The site axes: faces and stairs at 9°, 99°, 189° and 279° (all three terraces in OSM: −9.0 to −9.1°).</summary>
        public const double AxisBearingDeg = 9.0;

        /// <summary>The stupa axis (the centre of the T2 and T3 bodies, OSM w235084185 and w421288398) from the T1 centroid
        /// along the site axes (m): T1's north arm sticks out 8.8 m and its south arm 4.7 m, so its centroid lies 1.85 m
        /// south of the axis.</summary>
        public const double AxisOffsetU = -0.17, AxisOffsetV = 1.85;

        /// <summary>The centroid of the lowest terrace (OSM w56688295) when the tile has no footprint.</summary>
        public const double Lat = 27.721445, Lon = 85.362004;

        /// <summary>The T1 footprint way (OSM).</summary>
        public const ulong T1Way = 56688295;

        /// <summary>The plaza is flat out to this radius from the T1 centroid (m), and blends back into the terrain by
        /// <see cref="PlazaBlendM"/> (boudha.md 2.3: flatten r ≤ 75 m and the south lane).</summary>
        public const double PlazaRadiusM = 86.0, PlazaBlendM = 16.0;

        /// <summary>Total height of the stupa above the kora (finial tip; boudha.md 3.1.1, photo-measured).</summary>
        public const float HeightM = 44.8f;

        /// <summary>LOD triangle ceilings of the Great Stupa (the curated hero). LOD0 stays under the High tier's
        /// 60,000-triangle hero slice with the nearer quarters stepped down beside it, so aerial and rooftop views keep the
        /// whole site; LOD0 carries the 64 prayer-flag lines, LOD1 (seen from the rooftops beside a LOD0 quarter) their 32
        /// continuous ribbons of colour.</summary>
        public const int BudgetLod0 = 51000, BudgetLod1 = 17000, BudgetLod2 = 4000, BudgetLod3 = 240;

        /// <summary>The whole site's LOD0 ceiling on the High tier: the parts' LOD0 budgets stay within it.</summary>
        public const int SiteBudgetLod0 = 150000;

        // LOD0..3 ceilings of the other parts: LOD1 is 30 % and LOD2 8 % of LOD0. The north quarter carries the Ajima
        // entrance and the Guru Lhakhang, the west the Jamchen and the Dhabzang, so they get the larger shares. LOD3 is the
        // far quarter the hero slice steps the smaller parts down to from the rooftops and the air (the wall with its niche
        // band, the paving, the house bodies with their front bands and roofs), so it keeps up to 800.
        private static readonly int[][] PartBudget =
        {
            null,
            new[] { 24500, 7350, 1960, 800 },
            new[] { 16500, 4950, 1460, 800 },
            new[] { 19000, 5700, 1520, 800 },
            new[] { 24500, 7350, 1960, 800 },
            new[] { 12500, 3750, 1000, 240 },
        };

        private static readonly string[] Suffix = { "", ".north", ".east", ".south", ".west", ".gate" };

        private static readonly string[] Names =
        {
            "Boudhanath", "Boudhanath north kora (Ajima, Guru Lhakhang)", "Boudhanath east kora", "Boudhanath south kora",
            "Boudhanath west kora (Jamchen Lhakhang)", "Boudha Gate",
        };

        /// <summary>The hero id of a part.</summary>
        public static string IdOf(BoudhaPart p)
        {
            return HeroId + Suffix[(int)p];
        }

        /// <summary>The part a hero id names (false for any other hero).</summary>
        public static bool TryPart(string id, out BoudhaPart p)
        {
            p = BoudhaPart.Stupa;
            if (id == null || !id.StartsWith(HeroId, StringComparison.Ordinal)) return false;
            for (int i = 0; i < Suffix.Length; i++)
            {
                if (id.Length != HeroId.Length + Suffix[i].Length) continue;
                if (string.CompareOrdinal(id, HeroId.Length, Suffix[i], 0, Suffix[i].Length) != 0) continue;
                p = (BoudhaPart)i;
                return true;
            }
            return false;
        }

        /// <summary>True for the heroes this site builds (the curated hero and its parts).</summary>
        public static bool Handles(string id)
        {
            BoudhaPart p;
            return TryPart(id, out p);
        }

        /// <summary>Per-LOD triangle ceilings of a part (LOD0..3).</summary>
        public static int[] BudgetOf(BoudhaPart p)
        {
            if (p == BoudhaPart.Stupa) return new[] { BudgetLod0, BudgetLod1, BudgetLod2, BudgetLod3 };
            return (int[])PartBudget[(int)p].Clone();
        }

        /// <summary>The height of a part's tallest structure above the kora (m): the stupa's finial; a quarter's tallest
        /// house, gompa roof ornament or flagpole; the main gate's crest finial.</summary>
        public static float TopOf(BoudhaPart p)
        {
            switch (p)
            {
                case BoudhaPart.Stupa: return HeightM;
                case BoudhaPart.Gate: return (float)Math.Max(BoudhaGate.Top, BoudhaRing.TopOf(BoudhaPart.Gate));
                default: return (float)BoudhaRing.TopOf(p);
            }
        }

        /// <summary>The front-row buildings round the kora a quarter rebuilds (OSM refs "w123"; boudha.md 3.7), with the
        /// gompas it models (Guru Lhakhang w520755992, Old Dhabzang w209312611, Jamchen w209530023).</summary>
        public static string[] HideRefs(BoudhaPart p)
        {
            var l = new List<string>();
            for (int i = 0; i < BoudhaRing.Houses.Length; i++)
                if (BoudhaRing.Houses[i].Part == p) l.Add("w" + BoudhaRing.Houses[i].Way.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return l.ToArray();
        }

        /// <summary>Every OSM way the site rebuilds (all quarters).</summary>
        public static IEnumerable<ulong> HiddenWays
        {
            get
            {
                for (int i = 0; i < BoudhaRing.Houses.Length; i++) yield return BoudhaRing.Houses[i].Way;
            }
        }

        /// <summary>The catalogue recipes of the five parts after the curated hero (added to the region's heroes with it by
        /// World/Sacred/HeroSet).</summary>
        public static IEnumerable<HeroRecipe> Parts()
        {
            for (int i = 1; i < Suffix.Length; i++)
            {
                var p = (BoudhaPart)i;
                yield return new HeroRecipe
                {
                    Id = IdOf(p), Name = Names[i], Osm = "w" + T1Way, Form = HeroForm.Bespoke, Kind = p == BoudhaPart.Gate ? HeritageKind.Gate : HeritageKind.Gompa,
                    SiteOf = HeroId, Lat = Lat, Lon = Lon, HeightM = TopOf(p), PlanW = 30f, PlanD = 30f, Measured = true, BudgetTris = BudgetOf(p),
                    Hide = HideRefs(p),
                };
            }
        }

        /// <summary>The quarter of the kora a site-local point lies in (by its bearing from the stupa axis, 45° either side
        /// of the four site axes).</summary>
        public static BoudhaPart QuarterOf(double u, double v)
        {
            double a = Math.Atan2(u, v) * 180.0 / Math.PI;
            if (a < 0) a += 360;
            if (a >= 315 || a < 45) return BoudhaPart.North;
            if (a < 135) return BoudhaPart.East;
            if (a < 225) return BoudhaPart.South;
            return BoudhaPart.West;
        }

        /// <summary>Locate the site in a tile (false when its anchor is not in it).</summary>
        internal static bool TryFrame(HeritageRecord r, TileData tile, out BoudhaFrame f)
        {
            f = default(BoudhaFrame);
            double ox = double.NaN, oz = double.NaN;
            ulong t1 = 2 * T1Way;
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                if (tile.Buildings[i].OsmRef != t1) continue;
                int[] ring = tile.Buildings[i].Rings[0];
                Centroid(ring, out ox, out oz);
                break;
            }
            if (double.IsNaN(ox))
            {
                double x, z;
                int b;
                if (!HeroBuilder.TryLocate(r, HeroBuilder.Merge(r), tile, out x, out z, out b)) return false;
                ox = x;
                oz = z;
            }
            f.OX = ox;
            f.OZ = oz;
            double a = AxisBearingDeg * Math.PI / 180.0;
            // Axis offset (site u east', v north') in true coordinates.
            f.AX = ox + AxisOffsetU * Math.Cos(a) + AxisOffsetV * Math.Sin(a);
            f.AZ = oz - AxisOffsetU * Math.Sin(a) + AxisOffsetV * Math.Cos(a);
            f.Level = BoudhaGround.KoraLevel(tile, ox, oz);
            f.Site = Affine3.Translation(f.AX, f.Level, f.AZ) * Affine3.Yaw(AxisBearingDeg);
            f.Tile = tile;
            BoudhaWall.Fit(tile, ref f);
            return true;
        }

        /// <summary>Area centroid of a ring of tile-local centimetres (metres).</summary>
        internal static void Centroid(int[] ring, out double cx, out double cz)
        {
            int n = ring.Length / 2;
            double a = 0, sx = 0, sz = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i] / 100.0, zi = ring[2 * i + 1] / 100.0, xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                double cr = xj * zi - xi * zj;
                a += cr;
                sx += (xi + xj) * cr;
                sz += (zi + zj) * cr;
            }
            if (Math.Abs(a) < 1e-9)
            {
                cx = ring[0] / 100.0;
                cz = ring[1] / 100.0;
                return;
            }
            cx = sx / (3 * a);
            cz = sz / (3 * a);
        }

        /// <summary>
        /// Build one part of the site (the hero <paramref name="r"/> names it) at one LOD into <paramref name="m"/>
        /// (tile-local, absolute Y) with its colliders at LOD0; returns the triangles added, 0 when the anchor is not in
        /// <paramref name="tile"/>.
        /// </summary>
        public static int Build(HeritageRecord r, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            BoudhaPart part;
            if (!TryPart(r.Id, out part)) return 0;
            BoudhaFrame f;
            if (!TryFrame(r, tile, out f)) return 0;
            int t0 = m.TriangleCount;
            m.HasUv0 = true;
            lod = Math.Max(0, Math.Min(3, lod));
            GenColliders cc = lod == 0 ? c : null;
            int niches = 0;
            switch (part)
            {
                case BoudhaPart.Stupa:
                    if (lod >= 3)
                    {
                        int v0 = m.VertexCount, i0 = m.IndexCount;
                        BoudhaStupa.Far(m, f);
                        SacredDraw.Bake(m, v0, i0, f.Level);
                    }
                    else
                    {
                        BoudhaStupa.Build(m, cc, f, lod, stats);
                        BoudhaTerraces.Build(m, cc, f, lod);
                    }
                    break;
                case BoudhaPart.Gate:
                    BoudhaGate.Build(m, cc, tile, f, lod);
                    {
                        // The brick house east of the gate on the road frontage (the gate's own street front).
                        int gv = m.VertexCount, gi = m.IndexCount;
                        BoudhaRing.Build(m, cc, tile, f, BoudhaPart.Gate, lod);
                        SacredDraw.Bake(m, gv, gi, f.Level);
                    }
                    break;
                default:
                    niches = BoudhaQuarter.Build(m, cc, tile, f, part, lod);
                    break;
            }
            if (stats != null)
            {
                stats.TopM = TopOf(part);
                // The prayer-wheel niches as drawn: each quarter part its stretch of the compound wall's 147.
                stats.Niches = niches;
                if (part == BoudhaPart.Stupa)
                {
                    stats.PlinthTopM = (float)BoudhaStupa.T3Top;
                    stats.Terraces = 3;
                    stats.Rings = 13;
                    stats.DoorYawDeg = (float)(AxisBearingDeg + 180.0);
                }
            }
            return m.TriangleCount - t0;
        }
    }
}
