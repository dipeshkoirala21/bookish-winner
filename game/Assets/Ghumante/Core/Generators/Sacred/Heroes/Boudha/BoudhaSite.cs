using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// Where the Boudhanath site stands in a tile: the centroid of the lowest terrace (OSM w56688295, the hero anchor),
    /// the stupa axis (the centre of the upper terraces, 0.8 m north of it on the site axis), the site axes turned
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
    }

    /// <summary>
    /// The bespoke Boudhanath heritage site (W2 monuments round; docs/research/heritage/boudha.md): every structure
    /// modelled to its measured plan, section and colours instead of the parametric stupa template. One hero
    /// (<c>her.ktm.boudhanath</c>, the curated DB's record) builds the whole site: the Great Stupa (S1), the compound wall
    /// with the 147 prayer-wheel niches (S2), the Ajima shrine and the north entrance (S3, S4), the Guru Lhakhang and the
    /// Big Bell (S5), the main gate on Boudha Main Road (S6), the ring of houses round the kora (S7), the Jamchen
    /// Lhakhang (S8), Old Dhabzang (S9), the peace park statue (S10) and the paved plaza with its props. The front-row
    /// houses it rebuilds are hidden from the house bands (<see cref="HiddenWays"/> through
    /// <see cref="HeroRecipe.Hide"/>). Four LODs: LOD0 the full site (≤ <see cref="BudgetLod0"/> triangles), LOD1 about
    /// 30 %, LOD2 about 8 % (both keep the silhouette), LOD3 the far shape (dome, tower, ring).
    /// </summary>
    public static class BoudhaSite
    {
        /// <summary>The curated hero id.</summary>
        public const string HeroId = "her.ktm.boudhanath";

        /// <summary>The site axes: faces and stairs at 9°, 99°, 189° and 279° (all three terraces in OSM: −9.0 to −9.1°).</summary>
        public const double AxisBearingDeg = 9.0;

        /// <summary>The stupa axis (the centre of T2 and T3) from the T1 centroid along the site axes (m): T1's body
        /// reaches 3 m further south than north (OSM).</summary>
        public const double AxisOffsetU = 0.0, AxisOffsetV = 0.8;

        /// <summary>The centroid of the lowest terrace (OSM w56688295) when the tile has no footprint.</summary>
        public const double Lat = 27.721445, Lon = 85.362004;

        /// <summary>The T1 footprint way (OSM).</summary>
        public const ulong T1Way = 56688295;

        /// <summary>The plaza is flat out to this radius from the T1 centroid (m), and blends back into the terrain by
        /// <see cref="PlazaBlendM"/> (boudha.md 2.3: flatten r ≤ 75 m and the south lane).</summary>
        public const double PlazaRadiusM = 80.0, PlazaBlendM = 16.0;

        /// <summary>LOD triangle ceilings of the whole site (W2 monuments round: about 150 k per site at LOD0 on High,
        /// LOD1 about 30 %, LOD2 about 8 %).</summary>
        public const int BudgetLod0 = 150000, BudgetLod1 = 45000, BudgetLod2 = 12000, BudgetLod3 = 400;

        /// <summary>Total height of the stupa above the kora (finial tip; boudha.md 3.1.1, photo-measured).</summary>
        public const float HeightM = 44.8f;

        /// <summary>The front-row buildings round the kora the site rebuilds (OSM way ids; boudha.md 3.7, every building
        /// with an unobstructed face toward the stupa within 78 m), plus the gompas it models (Guru Lhakhang w520755992,
        /// Old Dhabzang w209312611, Jamchen w209530023).</summary>
        public static readonly ulong[] HiddenWays =
        {
            520755992, 520756005, 517234573, 911869006, 911869007, 911869008, 209312611, 912264293, 912264291, 912264292, 912264454, 912264453,
            912264591, 912264590, 912264589, 912264741, 912269948, 912269941, 211905448, 912269936, 912270352, 912270787, 912276969, 912276521,
            912276520, 911869018, 517160028, 911869019, 912278963, 912278964, 912278965, 912280177, 912280176, 911869026, 911869027, 911869028,
            209530023, 911869029, 912280174, 912280388, 209312596, 517236636, 517236635, 912261800, 912261799, 912262363, 912262534,
        };

        /// <summary>The hide refs in the curated form ("w123"), for <see cref="HeroRecipe.Hide"/>.</summary>
        public static string[] HideRefs()
        {
            var s = new string[HiddenWays.Length];
            for (int i = 0; i < s.Length; i++) s[i] = "w" + HiddenWays[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            return s;
        }

        /// <summary>True for the hero this site builds.</summary>
        public static bool Handles(HeroRecipe r)
        {
            return r != null && string.Equals(r.Id, HeroId, StringComparison.Ordinal);
        }

        /// <summary>Locate the site in a tile (false when its anchor is not in it).</summary>
        internal static bool TryFrame(HeritageRecord r, HeroRecipe recipe, TileData tile, out BoudhaFrame f)
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
                if (!HeroBuilder.TryLocate(r, recipe, tile, out x, out z, out b)) return false;
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
        /// Build the whole site at one LOD into <paramref name="m"/> (tile-local, absolute Y) with its colliders at LOD0;
        /// returns the triangles added, 0 when the anchor is not in <paramref name="tile"/>.
        /// </summary>
        public static int Build(HeritageRecord r, HeroRecipe recipe, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            BoudhaFrame f;
            if (!TryFrame(r, recipe, tile, out f)) return 0;
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            m.HasUv0 = true;
            lod = Math.Max(0, Math.Min(3, lod));
            if (lod >= 3)
            {
                BoudhaStupa.Far(m, f);
                SacredDraw.Bake(m, v0, i0, f.Level);
            }
            else
            {
                GenColliders cc = lod == 0 ? c : null;
                BoudhaStupa.Build(m, cc, f, lod, stats);
                BoudhaWall.Build(m, cc, f, lod);
            }
            if (stats != null)
            {
                stats.TopM = (float)BoudhaStupa.Tip;
                stats.PlinthTopM = (float)BoudhaStupa.T3Top;
                stats.Terraces = 3;
                stats.Rings = 13;
                stats.DoorYawDeg = (float)(AxisBearingDeg + 180.0);
            }
            return m.TriangleCount - t0;
        }
    }
}
