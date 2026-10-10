using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>One curated roundabout or chowk with a real centrepiece (docs/research/w2/roundabouts.md §2-3,
    /// ref_ornaments.md).</summary>
    public sealed class RoundaboutEntry
    {
        public string Id;
        public string NameEn;

        /// <summary>OSM node of the matching JNCT record (0 = match by position and name only).</summary>
        public long OsmNodeId;

        /// <summary>Centre (WGS84 degrees) of the ring, chowk node or island.</summary>
        public double Lon, Lat;

        /// <summary>Centre in game metres (from <see cref="WorldFrame.LonLatToGame"/>).</summary>
        public double GameX, GameZ;

        /// <summary>No JNCT record: the island is drawn at the entry's own position (a mapped park or traffic island
        /// the roads already go round).</summary>
        public bool Standalone;

        /// <summary>Island radius when the JNCT record (or a standalone site) has none, 0 = from the record.</summary>
        public float IslandRadiusM;

        /// <summary>Stadium islands: half length across the facing direction.</summary>
        public float HalfLengthM;

        public IslandShape Shape;

        /// <summary>Lower-case words that must appear in the record's name for a position match (null = any).</summary>
        public string NameKey;

        public RoundaboutDesign Design;
    }

    /// <summary>
    /// The real roundabouts and chowks of the Kathmandu Valley whose centrepieces are modelled (Maitighar Mandala,
    /// Shahid Gate, the royal statues at Tripureshwor, Durbar Marg, New Road, Jawalakhel and Singha Durbar, Narayan
    /// Gopal Chowk, the airport approach tower), matched to JNCT records by OSM node, else by distance and name; and
    /// the deterministic generic design for every other island, which never invents a statue or a shrine
    /// (W2_DESIGN 4.7, O2). Immutable after the static constructor: safe on worker threads.
    /// </summary>
    public static class RoundaboutCatalog
    {
        /// <summary>Id of generic designs.</summary>
        public const string GenericId = "generic";

        /// <summary>Distance within which a JNCT record matches an entry by position.</summary>
        public const double MatchRadiusM = 45.0;

        private static readonly RoundaboutEntry[] s_entries = Build();

        /// <summary>Every curated entry.</summary>
        public static IReadOnlyList<RoundaboutEntry> Entries
        {
            get { return s_entries; }
        }

        public static bool TryGet(string id, out RoundaboutEntry e)
        {
            foreach (RoundaboutEntry x in s_entries)
            {
                if (x.Id == id)
                {
                    e = x;
                    return true;
                }
            }
            e = null;
            return false;
        }

        /// <summary>
        /// The curated entry for a JNCT record: the entry with the same OSM node, else (for island-capable records:
        /// roundabouts, rings, synthetic islands and police chowks) the nearest entry within <see cref="MatchRadiusM"/>
        /// whose name key (if any) appears in the record's name, provided no other island-capable record of the tile
        /// is nearer to that entry or carries its node (so an entry dresses one island only).
        /// </summary>
        public static bool TryMatch(in JunctionRecord j, TileData t, out RoundaboutEntry e)
        {
            e = null;
            foreach (RoundaboutEntry x in s_entries)
            {
                if (!x.Standalone && x.OsmNodeId != 0 && x.OsmNodeId == j.OsmNodeId)
                {
                    e = x;
                    return true;
                }
            }
            if (t == null || !CanHoldIsland(j.Kind)) return false;
            double gx, gz;
            t.LocalToGame(j.XCm, j.ZCm, out gx, out gz);
            NameRecord name = j.NameRef > 0 && j.NameRef <= t.Names.Count ? t.Name(j.NameRef) : null;
            double best = MatchRadiusM * MatchRadiusM;
            foreach (RoundaboutEntry x in s_entries)
            {
                if (x.Standalone) continue;
                double dx = x.GameX - gx, dz = x.GameZ - gz, d2 = dx * dx + dz * dz;
                if (d2 > best) continue;
                if (x.NameKey != null && name != null && !Contains(name, x.NameKey)) continue;
                if (OtherRecordOwns(x, j, t, d2)) continue;
                best = d2;
                e = x;
            }
            return e != null;
        }

        /// <summary>Junction kinds that can carry a decorated island.</summary>
        public static bool CanHoldIsland(JunctionKind k)
        {
            return k == JunctionKind.Roundabout || k == JunctionKind.Circular || k == JunctionKind.SyntheticIsland || k == JunctionKind.Police;
        }

        private static bool OtherRecordOwns(RoundaboutEntry x, in JunctionRecord j, TileData t, double d2)
        {
            foreach (JunctionRecord o in t.Junctions)
            {
                if (o.OsmNodeId == j.OsmNodeId && o.XCm == j.XCm && o.ZCm == j.ZCm) continue;
                if (x.OsmNodeId != 0 && o.OsmNodeId == x.OsmNodeId) return true;
                if (!CanHoldIsland(o.Kind)) continue;
                double ox, oz;
                t.LocalToGame(o.XCm, o.ZCm, out ox, out oz);
                double dx = x.GameX - ox, dz = x.GameZ - oz;
                if (dx * dx + dz * dz < d2) return true;
            }
            return false;
        }

        private static bool Contains(NameRecord n, string key)
        {
            return n.En.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 || n.Default.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Standalone entries (no JNCT record) whose centre lies inside the tile square, appended to
        /// <paramref name="dst"/>.</summary>
        public static void StandaloneIn(TileData t, List<RoundaboutEntry> dst)
        {
            if (t == null) return;
            double x0 = t.Tile.X0, z0 = t.Tile.Z0, s = t.Tile.Size;
            foreach (RoundaboutEntry x in s_entries)
                if (x.Standalone && x.GameX >= x0 && x.GameX < x0 + s && x.GameZ >= z0 && x.GameZ < z0 + s) dst.Add(x);
        }

        // ------------------------------------------------------------------ generic designs

        /// <summary>
        /// The generic design of an island of radius <paramref name="radiusM"/> (roundabouts.md §4): small islands get
        /// a lawn and the police post, middle ones a marigold or rose garden with a railing, big ones a fountain, flag
        /// pole or clock tower now and then, lamps, trees and paths. Deterministic from the junction's OSM node.
        /// Never a statue or a shrine.
        /// </summary>
        public static RoundaboutDesign Generic(in JunctionRecord j, float radiusM)
        {
            uint seed = OrnamentSeed.Of(j.OsmNodeId, 0x0A11u);
            bool police = j.Has(JunctionFlags.HasPolice) || j.Kind == JunctionKind.Police || j.Kind == JunctionKind.SyntheticIsland;
            var d = new RoundaboutDesign
            {
                Id = GenericId,
                NameEn = null,
                Centre = Centrepiece.Garden,
                Garden = GardenStyle.Lawn,
                Kerb = OrnamentSeed.Unit(seed, 1) < 0.6f ? KerbPaint.BlackWhite : KerbPaint.YellowBlack,
                Railing = RailingStyle.None,
                FacingDeg = float.NaN,
                Seed = seed,
                Police = police ? (OrnamentSeed.Unit(seed, 2) < 0.7f ? PoliceStyle.Drum : PoliceStyle.Umbrella) : PoliceStyle.None,
            };
            if (radiusM <= 0.5f)
            {
                d.Centre = Centrepiece.PolicePodium;
                return d;
            }
            if (radiusM < 3f)
            {
                d.Garden = GardenStyle.Lawn;
                return d;
            }
            d.Garden = OrnamentSeed.Unit(seed, 3) < 0.6f ? GardenStyle.Marigold : GardenStyle.Roses;
            float rr = OrnamentSeed.Unit(seed, 4);
            d.Railing = rr < 0.5f ? RailingStyle.WhiteArches : rr < 0.75f ? RailingStyle.BlackIron : RailingStyle.None;
            d.Signboard = radiusM >= 4f && OrnamentSeed.Unit(seed, 5) < 0.6f;
            if (radiusM >= 6f)
            {
                float c = OrnamentSeed.Unit(seed, 6);
                d.Centre = c < 0.3f ? Centrepiece.Fountain : c < 0.45f ? Centrepiece.FlagPole : c < 0.55f ? Centrepiece.ClockTower : Centrepiece.Garden;
                if (OrnamentSeed.Unit(seed, 7) < 0.4f) d.Garden = GardenStyle.Mixed;
            }
            if (radiusM >= 7f) d.LampPosts = 4;
            if (radiusM >= 12.5f)
            {
                d.Trees = (byte)Math.Min(6, (int)(radiusM / 3.5f));
                d.Paths = 4;
            }
            return d;
        }

        // ------------------------------------------------------------------ curated entries

        private static RoundaboutEntry[] Build()
        {
            var list = new List<RoundaboutEntry>();

            // Maitighar Mandala (JNCT Circular n425306923, ring 68.3 m, island 54.9 m).
            list.Add(Entry("maitighar", "Maitighar Mandala", 425306923, 85.32043, 27.69454, "maitighar", new RoundaboutDesign
            {
                Centre = Centrepiece.Mandala, Garden = GardenStyle.White, Kerb = KerbPaint.YellowBlack, FlagPoleM = 26f,
                Railing = RailingStyle.TealPosts, LampPosts = 6, Police = PoliceStyle.Drum, Trees = 6, Paths = 4, Signboard = true,
            }));

            // Tripureshwor: King Tribhuvan in white marble (JNCT Circular n103589515, island 12.8 m).
            list.Add(Entry("tripureshwor", "Tripureshwor (King Tribhuvan statue)", 103589515, 85.31412, 27.69380, "tripureshwor", new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Garden = GardenStyle.Lawn, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.None, LampPosts = 2,
                Police = PoliceStyle.Drum,
                Statue = new StatueSpec
                {
                    Subject = "King Tribhuvan", Pose = StatuePose.CloakHands, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.WhiteMarble,
                    FigureM = 3.0f, Cloak = true, Garland = true, PlumeM = 0.16f, Plinth = PlinthShape.Square, PlinthM = 2.0f, PlinthW = 1.5f,
                    PlinthColour = OrnamentPalette.PlinthWhite, Platform = PlatformShape.Square, Steps = 3, PlatformW = 6.4f,
                    PlatformColour = OrnamentPalette.StepGrey, Pots = true,
                },
            }));

            // Durbar Marg south (Jamal): King Mahendra (JNCT Roundabout n313918587, island 15.8 m).
            list.Add(Entry("durbar_marg", "Durbar Marg (King Mahendra statue)", 313918587, 85.31732, 27.70997, null, new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Garden = GardenStyle.Roses, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.BlackIron, LampPosts = 4,
                Statue = new StatueSpec
                {
                    Subject = "King Mahendra", Pose = StatuePose.HandOnHilt, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.Bronze,
                    FigureM = 3.0f, Cloak = true, Glasses = true, Plinth = PlinthShape.Tapered, PlinthM = 4.6f, PlinthW = 2.7f,
                    PlinthColour = OrnamentPalette.PlinthPink, Platform = PlatformShape.Square, Steps = 3, PlatformW = 6.2f,
                    PlatformColour = OrnamentPalette.PlinthWhite,
                },
            }, 7.9f));

            // New Road west (Basantapur): Juddha Shumsher, standing (JNCT Roundabout n1273136899, ring 20.2 m, plinth island 8.9 m).
            list.Add(Entry("new_road", "New Road (Juddha Shumsher statue)", 1273136899, 85.30914, 27.70365, null, new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Garden = GardenStyle.Paved, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.None, LampPosts = 0,
                Statue = new StatueSpec
                {
                    Subject = "Juddha Shumsher", Pose = StatuePose.HandOnHilt, Attire = StatueAttire.RanaPlumed, Finish = StatueFinish.Bronze,
                    FigureM = 2.8f, Cloak = true, Plinth = PlinthShape.Square, PlinthM = 3.6f, PlinthW = 1.5f, PlinthColour = OrnamentPalette.PlinthGrey,
                    Platform = PlatformShape.Round, Steps = 4, PlatformW = 8.4f, PlatformColour = OrnamentPalette.StepRound, Pots = true, Medallion = true,
                },
            }, 4.45f));

            // Jawalakhel: King Birendra on the stone spire in the fountain basin (JNCT Roundabout n425307049, island 35.9 m).
            list.Add(Entry("jawalakhel", "Jawalakhel (King Birendra statue)", 425307049, 85.31358, 27.67290, "jawalakhel", new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Fountain = true, Garden = GardenStyle.Mixed, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.WhiteArches,
                LampPosts = 6, Police = PoliceStyle.Drum, Trees = 4, Paths = 0, Signboard = true,
                Statue = new StatueSpec
                {
                    Subject = "King Birendra", Pose = StatuePose.Sceptre, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.Verdigris,
                    FigureM = 2.4f, Cloak = true, Glasses = true, Plinth = PlinthShape.Spire, PlinthM = 7.4f, PlinthW = 5.4f,
                    PlinthColour = OrnamentPalette.SpireStone, Steps = 0,
                },
            }));

            // Narayan Gopal Chowk (JNCT SyntheticIsland n1945933074; statue n4113142690 at the node). Pose and plinth [E].
            list.Add(Entry("narayan_gopal", "Narayan Gopal Chowk", 1945933074, 85.33712, 27.74000, "narayan", new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Garden = GardenStyle.Marigold, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.WhiteArches, LampPosts = 2,
                Police = PoliceStyle.Drum,
                Statue = new StatueSpec
                {
                    Subject = "Narayan Gopal", Pose = StatuePose.HandOnChest, Attire = StatueAttire.CoatTopi, Finish = StatueFinish.Bronze,
                    FigureM = 2.4f, Glasses = true, Plinth = PlinthShape.Round, PlinthM = 1.9f, PlinthW = 1.4f, PlinthColour = OrnamentPalette.PlinthWhite,
                    Platform = PlatformShape.Round, Steps = 2, PlatformW = 3.4f, PlatformColour = OrnamentPalette.StepRound,
                },
            }, 6.1f));

            // TIA approach roundabout (JNCT Roundabout n1008825374, island 39.7 m, an 8 m man_made=tower inside).
            list.Add(Entry("airport", "Airport approach roundabout", 1008825374, 85.35647, 27.70054, null, new RoundaboutDesign
            {
                Centre = Centrepiece.ClockTower, Garden = GardenStyle.Mixed, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.WhiteArches, LampPosts = 6,
                Trees = 10, Paths = 4, Signboard = true,
            }));

            // Shahid Gate (park island w193705373, no JNCT record): the memorial arch, facing south down to Sundhara.
            RoundaboutEntry sg = Entry("shahid_gate", "Shahid Gate (Martyrs' Memorial)", 0, 85.31497, 27.69959, null, new RoundaboutDesign
            {
                Centre = Centrepiece.MemorialArch, Garden = GardenStyle.Marigold, Kerb = KerbPaint.Stone, Railing = RailingStyle.BlackIron, LampPosts = 4,
                FacingDeg = 180f,
            }, 8.5f);
            sg.Standalone = true;
            sg.Shape = IslandShape.Stadium;
            sg.HalfLengthM = 16.5f;
            list.Add(sg);

            // Singha Durbar main gate: Prithvi Narayan Shah (statue n2088245265 on traffic island w691998242, 6.2 m).
            RoundaboutEntry pn = Entry("pn_shah", "Singha Durbar gate (Prithvi Narayan Shah statue)", 0, 85.32160, 27.69846, null, new RoundaboutDesign
            {
                Centre = Centrepiece.Statue, Garden = GardenStyle.Paved, Kerb = KerbPaint.BlackWhite, Railing = RailingStyle.BlackIron, LampPosts = 0,
                FacingDeg = 270f,
                Statue = new StatueSpec
                {
                    Subject = "Prithvi Narayan Shah", Pose = StatuePose.PointUp, Attire = StatueAttire.RoyalPlumed, Finish = StatueFinish.Bronze,
                    FigureM = 2.8f, Cloak = true, Plinth = PlinthShape.Square, PlinthM = 2.4f, PlinthW = 1.4f, PlinthColour = OrnamentPalette.PlinthCream,
                    Platform = PlatformShape.Square, Steps = 2, PlatformW = 3.6f,
                },
            }, 3.1f);
            pn.Standalone = true;
            list.Add(pn);

            foreach (RoundaboutEntry e in list)
            {
                WorldFrame.LonLatToGame(e.Lon, e.Lat, out e.GameX, out e.GameZ);
                e.Design.Id = e.Id;
                e.Design.NameEn = e.NameEn;
                if (e.Design.FacingDeg == 0f) e.Design.FacingDeg = float.NaN;
                e.Design.Seed = OrnamentSeed.Of(e.OsmNodeId != 0 ? e.OsmNodeId : (long)Data.Hashes.Fnv1a64(Ascii(e.Id), 0, e.Id.Length), 0x0C47u);
            }
            return list.ToArray();
        }

        private static byte[] Ascii(string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        private static RoundaboutEntry Entry(string id, string name, long node, double lon, double lat, string nameKey, RoundaboutDesign d, float radius = 0f)
        {
            return new RoundaboutEntry
            {
                Id = id, NameEn = name, OsmNodeId = node, Lon = lon, Lat = lat, NameKey = nameKey, Design = d, IslandRadiusM = radius,
            };
        }
    }
}
