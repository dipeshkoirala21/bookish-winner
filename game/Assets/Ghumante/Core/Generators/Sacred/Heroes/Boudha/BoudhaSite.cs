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
    /// hero allocator, picked by its own screen height): the Great Stupa's shell (the three terrace walls, the ring with
    /// its niches, the dome with its north stair, the harmika with the eyes, the spire and the crown), its dressing in four
    /// quadrants (the terrace floors, parapet and stairs with the terrace chortens and statues of that side), its prayer
    /// flags in four quadrants, sixteen sectors of the kora (the compound wall with its niches, the paving and the
    /// compound's furniture as the inner ring; the houses and gompas round the kora as the outer ring; see
    /// <see cref="BoudhaSite.Sectors"/>) and the main gate on Boudha Main Road.</summary>
    public enum BoudhaPart : byte
    {
        /// <summary>The Great Stupa's shell (the curated hero).</summary>
        Stupa = 0,

        /// <summary>The main gate on Boudha Main Road.</summary>
        Gate = 1,

        /// <summary>The stupa's dressing of the north quadrant (site bearings 315-45°): the terrace floors, the parapet and
        /// the stairs, the terrace chortens, the north elephants and the Dhyani Buddhas.</summary>
        DressNorth = 2,

        DressEast = 3,
        DressSouth = 4,
        DressWest = 5,

        /// <summary>Kora sector: the north entrance (the Ajima shrine and the gate pavilions in the wall) with its stretch of
        /// the wall and the house across the kora west of the Guru Lhakhang.</summary>
        NorthGate = 6,

        NorthNorthEast = 7,
        NorthEast = 8,
        East = 9,
        SouthEast = 10,
        SouthSouthEast = 11,

        /// <summary>Kora sector: the south kora with the lamp houses by the south stair and the houses beside the lane.</summary>
        South = 12,

        /// <summary>Kora sector: the south pavilion where the lane from the main gate meets the wall, with its forecourt and
        /// the brick house at the lane's west corner.</summary>
        SouthGate = 13,

        SouthSouthWest = 14,
        SouthWest = 15,
        WestSouthWest = 16,

        /// <summary>Kora sector: the Jamchen Lhakhang and the west kiln and benches.</summary>
        West = 17,

        WestNorthWest = 18,
        NorthWest = 19,
        NorthNorthWest = 20,

        /// <summary>The prayer-flag bundles of the north quadrant (its arm end, the body corner clockwise after it and its
        /// two poles, <see cref="BoudhaStupa.BundleQuadrant"/>): tall lines from the crown to the lowest terrace, a part of
        /// their own so they hold their detail while the terrace dressing beneath them steps down.</summary>
        FlagsNorth = 21,

        FlagsEast = 22,
        FlagsSouth = 23,
        FlagsWest = 24,

        /// <summary>Kora sector (outer ring only): the Guru Lhakhang with the Big Bell, across the kora from the north
        /// entrance.</summary>
        NorthGuru = 25,
    }

    /// <summary>One kora sector: the site bearings (degrees clockwise from the site's north axis, start ≤ bearing &lt; end,
    /// wrapping through 0) of its inner ring (wall, paving, compound) and of its outer ring (the houses and gompas, listed in
    /// <see cref="BoudhaRing.Houses"/>); NaN when it holds none of that ring.</summary>
    internal struct BoudhaSector
    {
        public BoudhaPart Part;
        public double In0, In1, Out0, Out1;
        public string Name;

        public BoudhaSector(BoudhaPart part, double in0, double in1, double out0, double out1, string name)
        {
            Part = part;
            In0 = in0;
            In1 = in1;
            Out0 = out0;
            Out1 = out1;
            Name = name;
        }

        public bool HasInner
        {
            get { return !double.IsNaN(In0); }
        }

        public bool HasOuter
        {
            get { return !double.IsNaN(Out0); }
        }

        /// <summary>True when a site bearing lies in [a0, a1) (wrapping through 0).</summary>
        public static bool In(double deg, double a0, double a1)
        {
            if (double.IsNaN(a0)) return false;
            deg %= 360.0;
            if (deg < 0) deg += 360.0;
            return a0 <= a1 ? deg >= a0 && deg < a1 : deg >= a0 || deg < a1;
        }
    }

    /// <summary>
    /// The bespoke Boudhanath heritage site (W2 monuments round; docs/research/heritage/boudha.md): every structure
    /// modelled to its measured plan, section and colours instead of the parametric stupa template. The curated hero
    /// (<c>her.ktm.boudhanath</c>) builds the Great Stupa's shell (S1: terraces, stairs, ring with its niches, dome with its
    /// north stair, harmika with the eyes, spire and crown); the other parts (<see cref="BoudhaPart"/>,
    /// <see cref="HeroRecipe.SiteOf"/>) build the rest: the stupa's dressing in four quadrants (the prayer-flag bundles, the
    /// terrace chortens and statues), sixteen kora sectors (<see cref="Sectors"/>: the compound wall with the 147
    /// prayer-wheel niches (S2), the north entrance with the Ajima shrine, its gate pavilions and the NE compound house
    /// (S3, S4), the Guru Lhakhang and the Big Bell (S5), the ring of houses round the kora (S7), the Jamchen Lhakhang
    /// (S8), Old Dhabzang (S9), the south pavilion with its forecourt, the compound's lamp houses, kilns, flagpoles and
    /// garden beds and the paved plaza) and the main gate on Boudha Main Road with the paved lane (S6). Small parts let
    /// the hero allocator (World/Sacred/HeroLodBudget, which steps the smallest on screen first) keep what stands beside
    /// the player at LOD0 or LOD1 while the far side of the kora steps down. The front-row houses the site rebuilds are
    /// hidden from the house bands (<see cref="HideRefs"/> through <see cref="HeroRecipe.Hide"/>). Four LODs per part:
    /// LOD0 the full detail, LOD1 about 30 %, LOD2 about 8 % (both keep the silhouette), LOD3 the far shape. The whole
    /// site stays within 150,000 triangles at LOD0.
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

        /// <summary>LOD triangle ceilings of the Great Stupa's shell (the curated hero): the three terrace walls, the ring with
        /// its niches, the dome with its north stair, the harmika with the eyes, the spire and the crown. Its dressing (the
        /// terrace floors, parapet and stairs with the chortens and statues) and its prayer flags are four parts each, so the
        /// shell keeps LOD0 or LOD1 from the kora while the far side's dressing steps down; LOD2 keeps the eyes, the white
        /// dome with its saffron arcade, the stepped spire and the banded crown (the Low tier's view from the kora).</summary>
        public const int BudgetLod0 = 16000, BudgetLod1 = 5500, BudgetLod2 = 2400, BudgetLod3 = 240;

        /// <summary>The whole site's LOD0 ceiling on the High tier: the parts' LOD0 budgets stay within it.</summary>
        public const int SiteBudgetLod0 = 150000;

        /// <summary>
        /// LOD0..3 ceilings of every part, by <see cref="BoudhaPart"/> (their LOD0 ceilings add up to
        /// <see cref="SiteBudgetLod0"/> at most). The main gate and every kora sector stay within 7,000 at LOD0 and 2,400 at
        /// LOD1 (most within 2,200) so the sectors beside the player keep LOD0 or LOD1 in the Mid tier's 40,000-triangle
        /// hero slice; a sector's LOD3 (the far side of the kora from the kora and the rooftops) keeps the wall's niche band,
        /// the house fronts with their shutters, awnings and windows, and the roofs.
        /// </summary>
        private static readonly int[][] PartBudgets =
        {
            new[] { BudgetLod0, BudgetLod1, BudgetLod2, BudgetLod3 }, // the shell
            new[] { 7000, 2400, 900, 260 }, // the main gate
            new[] { 8400, 2900, 600, 300 }, new[] { 4400, 1500, 600, 300 }, new[] { 7000, 2450, 600, 300 }, new[] { 4400, 1500, 600, 300 }, // dressing N E S W
            new[] { 5700, 2000, 750, 420 }, // north entrance
            new[] { 5400, 1900, 750, 420 }, // NNE
            new[] { 6000, 2000, 750, 420 }, // NE
            new[] { 7000, 2300, 750, 420 }, // E
            new[] { 5100, 1800, 750, 420 }, // SE
            new[] { 6300, 2000, 750, 420 }, // SSE
            new[] { 7000, 2200, 750, 420 }, // S
            new[] { 6500, 2200, 750, 420 }, // south pavilion
            new[] { 6300, 2100, 750, 420 }, // SSW
            new[] { 6200, 2100, 750, 420 }, // SW
            new[] { 4900, 1700, 750, 420 }, // WSW
            new[] { 6400, 2200, 750, 420 }, // W (Jamchen)
            new[] { 6200, 2100, 750, 420 }, // WNW
            new[] { 4600, 1800, 750, 420 }, // NW
            new[] { 2600, 1000, 750, 420 }, // NNW
            new[] { 2800, 900, 380, 200 }, new[] { 2800, 900, 380, 200 }, new[] { 2800, 900, 380, 200 }, new[] { 2800, 900, 380, 200 }, // flags N E S W
            new[] { 5400, 1900, 750, 420 }, // Guru Lhakhang
        };

        /// <summary>
        /// The sixteen kora sectors (site bearings in degrees clockwise from the site's north axis), each a wedge from the
        /// compound wall out to the house fronts so its bounds stand as tall as its houses and its centre stays within about
        /// 20 m of anyone on its stretch of the kora (the allocator sizes a part by its bounds): the inner ring (the wall with
        /// its niches, the paving and the compound's furniture, by where each stands) and the outer ring (the houses and
        /// gompas, each listed with its sector in <see cref="BoudhaRing.Houses"/>; the outer ranges document where their
        /// centroids lie) are cut so that each sector holds about 4,000-8,000 triangles at LOD0.
        /// </summary>
        internal static readonly BoudhaSector[] Sectors =
        {
            new BoudhaSector(BoudhaPart.NorthGate, 350, 10, 336, 350, "Boudhanath north entrance"),
            new BoudhaSector(BoudhaPart.NorthGuru, double.NaN, double.NaN, 350, 6, "Boudhanath Guru Lhakhang and the Big Bell"),
            new BoudhaSector(BoudhaPart.NorthNorthEast, 10, 30, 6, 30, "Boudhanath kora, north-north-east (Dhabzang)"),
            new BoudhaSector(BoudhaPart.NorthEast, 30, 58, 30, 58, "Boudhanath kora, north-east"),
            new BoudhaSector(BoudhaPart.East, 58, 95, 58, 95, "Boudhanath kora, east"),
            new BoudhaSector(BoudhaPart.SouthEast, 95, 128, 95, 128, "Boudhanath kora, south-east"),
            new BoudhaSector(BoudhaPart.SouthSouthEast, 128, 162, 128, 162, "Boudhanath kora, south-south-east"),
            new BoudhaSector(BoudhaPart.South, 162, 183, 162, 186, "Boudhanath kora, south"),
            new BoudhaSector(BoudhaPart.SouthGate, 183, 198, 186, 198, "Boudhanath south pavilion"),
            new BoudhaSector(BoudhaPart.SouthSouthWest, 198, 224, 198, 224, "Boudhanath kora, south-south-west"),
            new BoudhaSector(BoudhaPart.SouthWest, 224, 246, 224, 245.5, "Boudhanath kora, south-west"),
            new BoudhaSector(BoudhaPart.WestSouthWest, 246, 258, 245.5, 258, "Boudhanath kora, west-south-west"),
            new BoudhaSector(BoudhaPart.West, 258, 276, 258, 276, "Boudhanath Jamchen Lhakhang"),
            new BoudhaSector(BoudhaPart.WestNorthWest, 276, 300, 276, 300, "Boudhanath kora, west-north-west"),
            new BoudhaSector(BoudhaPart.NorthWest, 300, 325, 300, 325, "Boudhanath kora, north-west"),
            new BoudhaSector(BoudhaPart.NorthNorthWest, 325, 350, 325, 336, "Boudhanath kora, north-north-west"),
        };

        /// <summary>The number of parts (the shell, the gate, four dressing quadrants, the kora sectors and four flag
        /// quadrants).</summary>
        public const int PartCount = 26;

        private static readonly string[] Suffix =
        {
            "", ".gate", ".dress.n", ".dress.e", ".dress.s", ".dress.w", ".kora.n", ".kora.nne", ".kora.ne", ".kora.e", ".kora.se", ".kora.sse", ".kora.s",
            ".kora.spav", ".kora.ssw", ".kora.sw", ".kora.wsw", ".kora.w", ".kora.wnw", ".kora.nw", ".kora.nnw", ".flags.n", ".flags.e", ".flags.s", ".flags.w", ".kora.gl",
        };

        private static readonly string[] DressNames =
        {
            "Boudhanath terraces and chortens, north", "Boudhanath terraces and chortens, east", "Boudhanath terraces and chortens, south",
            "Boudhanath terraces and chortens, west",
        };

        private static readonly string[] FlagNames =
        {
            "Boudhanath prayer flags, north", "Boudhanath prayer flags, east", "Boudhanath prayer flags, south", "Boudhanath prayer flags, west",
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

        /// <summary>True for a kora sector.</summary>
        public static bool IsSector(BoudhaPart p)
        {
            return (p >= BoudhaPart.NorthGate && p <= BoudhaPart.NorthNorthWest) || p == BoudhaPart.NorthGuru;
        }

        /// <summary>True for a quadrant of the stupa's prayer flags.</summary>
        public static bool IsFlags(BoudhaPart p)
        {
            return p >= BoudhaPart.FlagsNorth && p <= BoudhaPart.FlagsWest;
        }

        /// <summary>The dressing quadrant a flag quadrant hangs over (and the dressing quadrant itself for one).</summary>
        public static BoudhaPart QuadrantOf(BoudhaPart p)
        {
            return IsFlags(p) ? (BoudhaPart)((int)BoudhaPart.DressNorth + (p - BoudhaPart.FlagsNorth)) : p;
        }

        /// <summary>True for a dressing quadrant of the stupa.</summary>
        public static bool IsDressing(BoudhaPart p)
        {
            return p >= BoudhaPart.DressNorth && p <= BoudhaPart.DressWest;
        }

        /// <summary>Every part, the shell first.</summary>
        public static IEnumerable<BoudhaPart> AllParts
        {
            get
            {
                for (int i = 0; i < PartCount; i++) yield return (BoudhaPart)i;
            }
        }

        /// <summary>The sector record of a kora sector.</summary>
        internal static BoudhaSector SectorOf(BoudhaPart p)
        {
            for (int i = 0; i < Sectors.Length; i++)
                if (Sectors[i].Part == p) return Sectors[i];
            return new BoudhaSector(p, double.NaN, double.NaN, double.NaN, double.NaN, null);
        }

        /// <summary>The kora sector whose inner ring holds site bearing <paramref name="deg"/>.</summary>
        public static BoudhaPart InnerAt(double deg)
        {
            for (int i = 0; i < Sectors.Length; i++)
                if (BoudhaSector.In(deg, Sectors[i].In0, Sectors[i].In1)) return Sectors[i].Part;
            return BoudhaPart.NorthGate;
        }

        /// <summary>The kora sector whose inner ring holds the site-local point (u, v).</summary>
        public static BoudhaPart InnerAt(double u, double v)
        {
            return InnerAt(BearingOf(u, v));
        }

        /// <summary>The site bearing of a site-local point (degrees clockwise from the site's north axis, 0..360).</summary>
        public static double BearingOf(double u, double v)
        {
            double a = Math.Atan2(u, v) * 180.0 / Math.PI;
            return a < 0 ? a + 360.0 : a;
        }

        /// <summary>The dressing quadrant of a site bearing (45° either side of the four site axes).</summary>
        public static BoudhaPart QuadrantAt(double deg)
        {
            deg %= 360.0;
            if (deg < 0) deg += 360.0;
            if (deg >= 315 || deg < 45) return BoudhaPart.DressNorth;
            if (deg < 135) return BoudhaPart.DressEast;
            if (deg < 225) return BoudhaPart.DressSouth;
            return BoudhaPart.DressWest;
        }

        /// <summary>Per-LOD triangle ceilings of a part (LOD0..3).</summary>
        public static int[] BudgetOf(BoudhaPart p)
        {
            return (int[])PartBudgets[(int)p].Clone();
        }

        /// <summary>The height of a part's tallest structure above the kora (m): the stupa's finial; a dressing quadrant's
        /// flag lines under the canopy; a sector's tallest house, gompa roof ornament or flagpole; the main gate's crest
        /// finial.</summary>
        public static float TopOf(BoudhaPart p)
        {
            switch (p)
            {
                case BoudhaPart.Stupa: return HeightM;
                case BoudhaPart.Gate: return (float)BoudhaGate.Top;
                default:
                    if (IsFlags(p)) return (float)BoudhaStupa.FlagTop;
                    if (IsDressing(p)) return (float)(BoudhaStupa.T1Top + BoudhaTerraces.MiniH);
                    return (float)BoudhaRing.TopOf(p);
            }
        }

        /// <summary>The front-row buildings round the kora a part rebuilds (OSM refs "w123"; boudha.md 3.7), with the
        /// gompas it models (Guru Lhakhang w520755992, Old Dhabzang w209312611, Jamchen w209530023).</summary>
        public static string[] HideRefs(BoudhaPart p)
        {
            var l = new List<string>();
            for (int i = 0; i < BoudhaRing.Houses.Length; i++)
                if (BoudhaRing.Houses[i].Part == p) l.Add("w" + BoudhaRing.Houses[i].Way.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return l.ToArray();
        }

        /// <summary>Every OSM way the site rebuilds (all sectors).</summary>
        public static IEnumerable<ulong> HiddenWays
        {
            get
            {
                for (int i = 0; i < BoudhaRing.Houses.Length; i++) yield return BoudhaRing.Houses[i].Way;
            }
        }

        /// <summary>The display name of a part.</summary>
        public static string NameOf(BoudhaPart p)
        {
            if (p == BoudhaPart.Stupa) return "Boudhanath";
            if (p == BoudhaPart.Gate) return "Boudha Gate";
            if (IsDressing(p)) return DressNames[p - BoudhaPart.DressNorth];
            if (IsFlags(p)) return FlagNames[p - BoudhaPart.FlagsNorth];
            return SectorOf(p).Name;
        }

        /// <summary>The catalogue recipes of the parts after the curated hero (added to the region's heroes with it by
        /// World/Sacred/HeroSet).</summary>
        public static IEnumerable<HeroRecipe> Parts()
        {
            for (int i = 1; i < PartCount; i++)
            {
                var p = (BoudhaPart)i;
                HeritageKind kind = p == BoudhaPart.Gate ? HeritageKind.Gate : IsDressing(p) || IsFlags(p) ? HeritageKind.Stupa : HeritageKind.Gompa;
                yield return new HeroRecipe
                {
                    Id = IdOf(p), Name = NameOf(p), Osm = "w" + T1Way, Form = HeroForm.Bespoke, Kind = kind, SiteOf = HeroId, Lat = Lat, Lon = Lon, HeightM = TopOf(p),
                    PlanW = 30f, PlanD = 30f, Measured = true, BudgetTris = BudgetOf(p), Hide = HideRefs(p),
                };
            }
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
        /// (tile-local, absolute Y) with its colliders; returns the triangles added, 0 when the anchor is not in
        /// <paramref name="tile"/>. Like the generic generators it makes the colliders at whichever LOD it is handed them:
        /// the streamer hands them to the first LOD it builds (TileBuild.BuildHeroes), LOD1 on Low, which never draws LOD0
        /// (BandConfig.HeroLod0AllowedFor). LOD1 makes exactly LOD0's set (the kora's walkable slabs follow LOD0's rays).
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
            GenColliders cc = c;
            int niches = 0;
            if (part == BoudhaPart.Stupa)
            {
                if (lod >= 3)
                {
                    int v0 = m.VertexCount, i0 = m.IndexCount;
                    BoudhaStupa.Far(m, f);
                    SacredDraw.Bake(m, v0, i0, f.Level);
                }
                else
                {
                    BoudhaStupa.Build(m, cc, f, lod, stats);
                }
            }
            else if (IsDressing(part))
            {
                // The quadrant's terrace floors, parapet and stairs, its chortens and statues.
                BoudhaStupa.Quadrant(m, f, part, lod);
                BoudhaTerraces.Build(m, cc, f, part, lod);
            }
            else if (IsFlags(part))
            {
                // The quadrant's four flag bundles.
                int v0 = m.VertexCount, i0 = m.IndexCount;
                BoudhaStupa.Flags(m, f, QuadrantOf(part), lod);
                SacredDraw.Bake(m, v0, i0, f.Level);
            }
            else if (part == BoudhaPart.Gate)
            {
                BoudhaGate.Build(m, cc, tile, f, lod);
            }
            else
            {
                niches = BoudhaQuarter.Build(m, cc, tile, f, part, lod);
            }
            if (stats != null)
            {
                stats.TopM = TopOf(part);
                // The prayer-wheel niches as drawn: each sector its stretch of the compound wall's 147.
                stats.Niches = niches;
                if (part == BoudhaPart.Stupa)
                {
                    stats.PlinthTopM = (float)BoudhaStupa.T3Top;
                    stats.Terraces = 3;
                    stats.Rings = 13;
                    stats.DoorYawDeg = (float)(AxisBearingDeg + 180.0);
                    // The site's prayer-flag lines (the four flag quadrants draw them, four a bundle at LOD0).
                    stats.FlagLines = BoudhaStupa.FlagLinesLod0;
                }
            }
            return m.TriangleCount - t0;
        }
    }
}
