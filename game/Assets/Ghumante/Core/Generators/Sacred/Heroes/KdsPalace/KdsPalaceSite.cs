using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The structures of the bespoke Hanuman Dhoka palace site (Kathmandu Durbar Square), each built as its own hero so the
    /// hero LOD allocator gives the one the player stands at LOD0 while the far courts step down. The first five keep the
    /// curated ids they had as parametric templates; the others are catalogue parts anchored in the same tile
    /// (<see cref="HeroRecipe.SiteOf"/> = <see cref="KdsPalaceSite.GateId"/>).
    /// </summary>
    public enum KdsPalacePart : byte
    {
        /// <summary>S1 Hanuman Dhoka gate, S2 Hanuman under his parasol, the lions, the Narasimha niche, the Pratap Malla
        /// inscription, the white west front of the gate block and S5 Agamchhen on it.</summary>
        Gate = 0,

        /// <summary>S10 Taleju on its 12-stage plinth with the 16 mini shrines, the precinct wall, Singha Dhoka and the north
        /// shop row.</summary>
        Taleju = 1,

        /// <summary>S11 the nine-storey Basantapur tower (Nautale Durbar), as rebuilt after 2015.</summary>
        BasantapurTower = 2,

        /// <summary>S17 Gaddi Baithak with its west portico and south colonnade, and the S18 Hnulunche west range.</summary>
        GaddiBaithak = 3,

        /// <summary>S19 Kumari Ghar with its carved north facade, lions, pinnacle cluster and inner court.</summary>
        KumariGhar = 4,

        /// <summary>S3 the white Dakha range on the forecourt and the Tribhuvan Museum block round Dakha and Lam Chowk.</summary>
        Dakha = 5,

        /// <summary>S16 the white south-west wing and the white range between it and Gaddi Baithak on Basantapur.</summary>
        SouthWing = 6,

        /// <summary>S6 Mohan and Sundari Chowk, the closed courts north of Nasal Chowk, with the Sun Dhara pit.</summary>
        MohanChowk = 7,

        /// <summary>S4 Degu Taleju on its brick tower and the brick range of the Seto Bhairav screen.</summary>
        DeguTaleju = 8,

        /// <summary>S7 Nasal Chowk: the paved court, the coronation platform, the white north range with its arcade and
        /// green shutters, the red-post east veranda and the white shrine.</summary>
        NasalChowk = 9,

        /// <summary>S8 Panch Mukhi Hanuman with its five round roofs.</summary>
        PanchMukhi = 10,

        /// <summary>S9 Mul Chowk, Taleju's closed court, and the range on the east lane.</summary>
        MulChowk = 11,

        /// <summary>S12 Lohan Chowk and its ranges with the S16 brick south facade, railing, lions and the east shop range.</summary>
        LohanChowk = 12,

        /// <summary>S13 the Kirtipur tower with its curved copper roof.</summary>
        KirtipurTower = 13,

        /// <summary>S14 the octagonal Bhaktapur tower.</summary>
        BhaktapurTower = 14,

        /// <summary>S15 the Lalitpur tower and its lion.</summary>
        LalitpurTower = 15,
    }

    /// <summary>
    /// The bespoke Hanuman Dhoka palace (W2 monuments round; docs/research/heritage/kds-palace.md): every structure of the
    /// old royal palace of Kathmandu Durbar Square modelled to its measured plan, section and colours, at its real place
    /// (the brief's site frame, OSM outlines and 2026 imagery), replacing the parametric templates that put a small gate
    /// box in the middle of Nasal Chowk and the nine-storey tower in Lohan Chowk. Sixteen heroes
    /// (<see cref="KdsPalacePart"/>), four LODs each: LOD0 the carved detail, LOD1 about 30 %, LOD2 about 8 % (both keep
    /// the silhouette and the colours), LOD3 a few boxes (≤ 200 triangles). The site stands on one level
    /// (<see cref="KpGround"/>); plinths, steps and courts are walkable; the generic house boxes inside the palace are
    /// hidden (<see cref="HideRefs"/>).
    /// </summary>
    public static class KdsPalaceSite
    {
        /// <summary>The brief's origin (§2.1) and its metres per degree.</summary>
        public const double OriginLat = 27.7045, OriginLon = 85.3075, MetresPerDegLat = 110574.0, MetresPerDegLon = 98573.0;

        /// <summary>The square's level (absolute metres): the open ground of the forecourt, the gate and Nasal Chowk in the
        /// region pack's terrain (1300.7-1301.2 m; the surface model climbs to 1306 m over the houses round Basantapur), on
        /// the height grid's 0.15 m quantum. <see cref="KpGround"/> flattens the palace's zones to it.</summary>
        public const double LevelM = 1301.0;

        public const string GateId = "her.ktm.hanuman_dhoka";
        public const string TalejuId = "her.ktm.taleju";
        public const string TowerId = "her.ktm.basantapur_tower";
        public const string GaddiId = "her.ktm.gaddi_baithak";
        public const string KumariId = "her.ktm.kumari_ghar";

        /// <summary>Number of parts.</summary>
        public const int PartCount = 16;

        /// <summary>The whole palace at LOD0 (all sixteen parts over some 250 × 200 m, about 274k as built). The hero
        /// allocator shows a part at LOD0 only while it fills a third of the screen, so a handful at most at once (standing in
        /// Basantapur: the tower, Lohan, Kumari Ghar and the white wing, about 79k; on the forecourt the gate, the Dakha
        /// fronts and Degu Taleju, about 64k, Taleju at LOD1). Above the monuments round's ~150k guide for a single-temple
        /// site: this site is sixteen structures, one of them Taleju, a major temple of its own (about 40k of its 60k
        /// allowance), and the judged fixes added the close-set struts (0.8 m on the temples, 1.0 m on the ranges, about
        /// 44k in all), the 45° lattice in every Newar window (about 23k), the four white ranges west of the forecourt
        /// (which replace four generic houses) and the forecourt flags.</summary>
        public const int SiteBudgetLod0 = 285000;

        private static readonly string[] Ids =
        {
            GateId, TalejuId, TowerId, GaddiId, KumariId, GateId + ".dakha", GateId + ".south_wing", GateId + ".mohan_chowk", GateId + ".degu_taleju",
            GateId + ".nasal_chowk", GateId + ".panch_mukhi", GateId + ".mul_chowk", GateId + ".lohan_chowk", GateId + ".kirtipur_tower",
            GateId + ".bhaktapur_tower", GateId + ".lalitpur_tower",
        };

        private static readonly string[] Names =
        {
            "Hanuman Dhoka", "Taleju (Kathmandu)", "Basantapur tower", "Gaddi Baithak", "Kumari Ghar", "Dakha Chowk range", "Basantapur white wing",
            "Mohan Chowk", "Degu Taleju", "Nasal Chowk", "Panch Mukhi Hanuman", "Mul Chowk", "Lohan Chowk", "Kirtipur tower", "Bhaktapur tower",
            "Lalitpur tower",
        };

        /// <summary>Highest point of each part above the square (m), as built.</summary>
        private static readonly float[] Tops = { 23.2f, 36.0f, 30.0f, 21.4f, 15.9f, 18.3f, 17.9f, 16.9f, 26.2f, 17.0f, 25.2f, 15.0f, 14.8f, 23.7f, 19.6f, 21.0f };

        /// <summary>Bearing of each part's main facade or door.</summary>
        private static readonly float[] Doors = { 282f, 283f, 192f, 281f, 10f, 12f, 192f, 282f, 282f, 12f, 192f, 12f, 192f, 282f, 102f, 192f };

        /// <summary>Centre of each part (local metres) and its plan (w × d m), for the catalogue (anchor, fauna keep-outs).</summary>
        private static readonly double[] Centres =
        {
            -9.5, -1.5, 45.1, 44.4, 1.6, -66.7, -70.0, -55.0, -102.3, -84.0, -25.9, -32.1, -27.3, -63.6, 6.3, 18.9, -0.1, 18.5, 4.0, -34.0, 19.5, -8.0,
            34.0, -23.0, 27.8, -58.4, 16.5, -42.5, 40.0, -47.5, 39.5, -71.0,
        };

        private static readonly float[] Plans =
        {
            16f, 10f, 66f, 75f, 14f, 14f, 46f, 36f, 21f, 25f, 38f, 52f, 40f, 14f, 27f, 41f, 14f, 16f, 20f, 47f, 9f, 9f, 34f, 34f, 38f, 38f, 9f, 9f, 9f, 9f, 11f,
            11f,
        };

        /// <summary>Per-part LOD ceilings (LOD0..3): each part as built with about 10 % headroom, Taleju (the one major temple)
        /// well under the round's 60,000; the whole site is held to <see cref="SiteBudgetLod0"/>. LOD1 is about a third of
        /// LOD0 (the enclosed courts drop a level), LOD2 under an eighth.</summary>
        private static readonly int[][] Budgets =
        {
            new[] { 24500, 6200, 900, 200 }, // gate
            new[] { 43000, 14800, 2050, 200 }, // Taleju
            new[] { 27000, 6600, 800, 200 }, // Basantapur tower
            new[] { 18500, 6200, 2000, 200 }, // Gaddi Baithak
            new[] { 25000, 8700, 1150, 200 }, // Kumari Ghar
            new[] { 27000, 10300, 1700, 200 }, // Dakha
            new[] { 12000, 3900, 450, 200 }, // south wing
            new[] { 13000, 5800, 1500, 200 }, // Mohan
            new[] { 18200, 5200, 900, 200 }, // Degu Taleju
            new[] { 22000, 7400, 1200, 200 }, // Nasal
            new[] { 8000, 2400, 750, 200 }, // Panch Mukhi
            new[] { 7000, 2300, 750, 200 }, // Mul
            new[] { 21500, 4400, 900, 200 }, // Lohan
            new[] { 13600, 4000, 650, 200 }, // Kirtipur (the big Bangla hood and the cusped-arch drum, judged round)
            new[] { 5400, 950, 300, 200 }, // Bhaktapur
            new[] { 14600, 3500, 600, 200 }, // Lalitpur
        };

        /// <summary>The hero id of a part.</summary>
        public static string IdOf(KdsPalacePart part)
        {
            return Ids[(int)part];
        }

        /// <summary>True for the site's hero ids.</summary>
        public static bool Handles(string id)
        {
            KdsPalacePart p;
            return TryPart(id, out p);
        }

        public static bool TryPart(string id, out KdsPalacePart part)
        {
            part = KdsPalacePart.Gate;
            if (id == null) return false;
            for (int i = 0; i < Ids.Length; i++)
            {
                if (!string.Equals(id, Ids[i], StringComparison.Ordinal)) continue;
                part = (KdsPalacePart)i;
                return true;
            }
            return false;
        }

        /// <summary>The part's highest point above the square (its catalogue height).</summary>
        public static float TopOf(KdsPalacePart part)
        {
            return Tops[(int)part];
        }

        /// <summary>The part's main facade bearing.</summary>
        public static float DoorOf(KdsPalacePart part)
        {
            return Doors[(int)part];
        }

        /// <summary>Per-LOD triangle ceilings of a part (a copy).</summary>
        public static int[] BudgetOf(KdsPalacePart part)
        {
            return (int[])Budgets[(int)part].Clone();
        }

        /// <summary>OSM buildings (and building parts) a part rebuilds: hidden from the house bands
        /// (<see cref="HeroRecipe.Hide"/>). Most palace ranges are mapped only as rough relations; every generic box inside
        /// the palace outline goes.</summary>
        public static string[] HideRefs(KdsPalacePart part)
        {
            switch (part)
            {
                case KdsPalacePart.Gate: return new[] { "w1374620542" };
                case KdsPalacePart.Taleju:
                    return new[]
                    {
                        "w1414600808", "w420797418", "w1414588339", "w1414024864", "w1413718110", "w1413440904", "w1413440905", "w1413440906", "w1413440907",
                        "w1413440908", "w1414610434", "w1414610435", "w1414610436", "w1414610437", "w1414610438", "w1414610439", "w1414610440", "w1414610441",
                        "w1414610442", "w1414610443", "w1414610444", "w1414610445", "w1415152802", "w1415152803", "w1415152804", "w1415152805", "w1374620543",
                    };
                case KdsPalacePart.BasantapurTower: return new[] { "w906985354", "w341127950" };
                case KdsPalacePart.GaddiBaithak:
                    return new[]
                    {
                        "w247501389", "w430743883", "w420797416", "w420797430", "w420797433", "w420797438", "w420797447", "w420797448", "w420797453", "w420797472",
                        "w420797473", "w420797474", "w420797475", "w420797478", "w420797479", "w420797480", "w420797481", "w420797483", "w420797492", "w420797500",
                        "w1029965549",
                    };
                case KdsPalacePart.KumariGhar:
                    return new[]
                    {
                        "r2688997", "w111984923", "w429890326", "w429890329", "w429890381", "w429890382", "w429890383", "w429890384", "w429890386", "w429890387",
                        "w429890388",
                    };
                // The Dakha range, the block west of Lam Chowk and the white ranges continuing the front west along the
                // forecourt (round r8: before, the house grammar built these four as modern apartment boxes).
                case KdsPalacePart.Dakha: return new[] { "w420797415", "w910121099", "w907080042", "w1415152801", "w1029965548", "w1117943799" };
                case KdsPalacePart.SouthWing: return new[] { "w910121098", "w906935471" };
                case KdsPalacePart.MohanChowk: return new[] { "r12339739" };
                case KdsPalacePart.DeguTaleju: return new[] { "w1415152806" };
                case KdsPalacePart.PanchMukhi: return new[] { "w1414600809" };
                case KdsPalacePart.MulChowk: return new[] { "r12339740", "w1414849434" };
                case KdsPalacePart.LohanChowk: return new[] { "r12314010", "w906985353" };
                default: return null;
            }
        }

        /// <summary>
        /// The catalogue side of the site, applied at the end of <see cref="HeroCatalog"/>'s list: the five curated heroes
        /// it already had keep their ids but are measured now (height, door bearing, plan, the brief's true positions and
        /// per-part budgets win over the curated DB's template numbers), and the eleven other structures are added as parts
        /// of the gate's site.
        /// </summary>
        public static void Patch(List<HeroRecipe> list)
        {
            if (list == null) return;
            for (int i = 0; i < PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                HeroRecipe r = null;
                foreach (HeroRecipe x in list)
                    if (string.Equals(x.Id, Ids[i], StringComparison.Ordinal))
                        r = x;
                if (r == null)
                {
                    r = new HeroRecipe { Id = Ids[i], Name = Names[i], Form = HeroForm.Bespoke, Kind = KindOf(part), SiteOf = GateId };
                    list.Add(r);
                }
                double lat, lon;
                LatLon(Centres[2 * i], Centres[2 * i + 1], out lat, out lon);
                r.Lat = lat;
                r.Lon = lon;
                r.HeightM = Tops[i];
                r.YawDeg = Doors[i];
                r.PlanW = Plans[2 * i];
                r.PlanD = Plans[2 * i + 1];
                // Built by this site's code (the generic form tests and builders leave it alone).
                r.Form = HeroForm.Bespoke;
                r.Measured = true;
                r.BudgetTris = BudgetOf(part);
                r.Hide = HideRefs(part);
                if (part == KdsPalacePart.Taleju)
                {
                    r.Tiers = 3;
                    r.PlinthLevels = 12;
                }
                else if (part == KdsPalacePart.BasantapurTower)
                {
                    r.Tiers = KpTowers.BasantapurRoofs;
                    r.Storeys = 9;
                }
                else
                {
                    r.Tiers = 0;
                    r.PlinthLevels = 0;
                }
            }
        }

        private static HeritageKind KindOf(KdsPalacePart part)
        {
            switch (part)
            {
                case KdsPalacePart.DeguTaleju:
                case KdsPalacePart.PanchMukhi: return HeritageKind.Pagoda;
                case KdsPalacePart.KirtipurTower:
                case KdsPalacePart.BhaktapurTower:
                case KdsPalacePart.LalitpurTower: return HeritageKind.Tower;
                default: return HeritageKind.Palace;
            }
        }

        /// <summary>Latitude and longitude of local (x, y).</summary>
        public static void LatLon(double x, double y, out double lat, out double lon)
        {
            lat = OriginLat + y / MetresPerDegLat;
            lon = OriginLon + x / MetresPerDegLon;
        }

        /// <summary>True when the site origin lies in the tile (every part is built in the origin's level-10 tile).</summary>
        public static bool InTile(TileData tile)
        {
            if (tile == null) return false;
            double gx, gz;
            WorldFrame.LonLatToGame(OriginLon, OriginLat, out gx, out gz);
            double x = gx - tile.Tile.X0, z = gz - tile.Tile.Z0;
            return x >= 0 && z >= 0 && x < tile.Tile.Size && z < tile.Tile.Size;
        }

        /// <summary>Build one part from its hero record (see the other overload); 0 when the site is not in the tile.</summary>
        public static int Build(HeritageRecord r, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            KdsPalacePart part;
            if (!TryPart(r.Id, out part) || !InTile(tile)) return 0;
            return Build(part, tile, lod, m, c, stats);
        }

        /// <summary>
        /// Build one part at a LOD into <paramref name="m"/> (tile-local, absolute heights) with its LOD0 colliders; returns
        /// its triangles.
        /// </summary>
        public static int Build(KdsPalacePart part, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            lod = Math.Max(0, Math.Min(3, lod));
            stats?.Clear();
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            ShapeAo.EnsureUv(m);
            m.HasUv0 = true;
            KpFrame f = KpFrame.For(tile, lod, lod == 0 ? c : null);
            switch (part)
            {
                case KdsPalacePart.Gate: KpGate.Build(ref f, m); break;
                case KdsPalacePart.Taleju: KpTaleju.Build(ref f, m); break;
                case KdsPalacePart.BasantapurTower: KpTowers.Basantapur(ref f, m); break;
                case KdsPalacePart.GaddiBaithak: KpGaddi.Build(ref f, m); break;
                case KdsPalacePart.KumariGhar: KpKumari.Build(ref f, m); break;
                case KdsPalacePart.Dakha: KpRanges.Dakha(ref f, m); break;
                case KdsPalacePart.SouthWing: KpRanges.SouthWing(ref f, m); break;
                case KdsPalacePart.MohanChowk: KpCourts.Mohan(ref f, m); break;
                case KdsPalacePart.DeguTaleju: KpTemples.DeguTaleju(ref f, m); break;
                case KdsPalacePart.NasalChowk: KpCourts.Nasal(ref f, m); break;
                case KdsPalacePart.PanchMukhi: KpTemples.PanchMukhi(ref f, m); break;
                case KdsPalacePart.MulChowk: KpCourts.Mul(ref f, m); break;
                case KdsPalacePart.LohanChowk: KpCourts.Lohan(ref f, m); break;
                case KdsPalacePart.KirtipurTower: KpTowers.Kirtipur(ref f, m); break;
                case KdsPalacePart.BhaktapurTower: KpTowers.Bhaktapur(ref f, m); break;
                default: KpTowers.Lalitpur(ref f, m); break;
            }
            if (lod < 3) SacredDraw.Bake(m, v0, i0, f.Level);
            if (stats != null)
            {
                stats.TopM = TopOf(part);
                stats.DoorYawDeg = DoorOf(part);
                if (part == KdsPalacePart.Taleju)
                {
                    stats.Tiers = 3;
                    stats.PlinthLevels = 12;
                    stats.PlinthTopM = (float)KpTaleju.PlinthTopM;
                }
                else if (part == KdsPalacePart.BasantapurTower)
                {
                    stats.Tiers = KpTowers.BasantapurRoofs;
                }
            }
            return m.TriangleCount - t0;
        }
    }
}
