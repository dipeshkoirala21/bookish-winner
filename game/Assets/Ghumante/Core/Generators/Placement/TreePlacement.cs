using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>Options of <see cref="TreePlacement"/>.</summary>
    public sealed class TreePlacementOptions
    {
        /// <summary>Share of URBAN arterial ways (trunk, primary, secondary) that carry an avenue.</summary>
        public float AvenueShare = 0.30f;

        public float AvenueSpacingMin = 8f, AvenueSpacingMax = 15f;

        /// <summary>Step of the forest canopy lattice (metres): about 90 canopy trees per hectare, crowns about 1.45
        /// steps wide.</summary>
        public float ForestSpacingM = 10.5f;

        /// <summary>Cap of forest trees per tile: beyond it the lattice step widens evenly (crowns with it).</summary>
        public int MaxForestTrees = 10000;

        /// <summary>Cap of plants (everything that is not a tree: flowers, shrubs, hedges, pots, ground cover, rocks,
        /// straw stacks) per tile; placement adds the important kinds first (parks, gardens, understorey, then wild
        /// ground cover), so the cap trims the wild cover first.</summary>
        public int MaxPlants = 9000;

        /// <summary>Forest understorey (ferns, shrubs, grass, rocks round the clumps).</summary>
        public bool Understorey = true;

        /// <summary>House gardens (pots and tulsi by the door, banana, bamboo and poinsettia by village houses,
        /// bougainvillea at urban compounds).</summary>
        public bool Gardens = true;

        /// <summary>Park planting (flower beds, hedges, ornamental trees, shrubs).</summary>
        public bool Parks = true;

        /// <summary>Wild ground cover (grass tufts and rocks on hills and field edges, boulders on steep slopes and river
        /// banks) and field straw stacks.</summary>
        public bool GroundCover = true;

        /// <summary>
        /// The road corridors of a tile (tile-local metres), e.g. <c>RoadCorridorIndex.ForTile</c> from the roads
        /// package: generated plants keep out of them (docs/W2_DETAIL_CONTRACT.md §1.1) and OSM trees standing in one
        /// are moved to its edge. Null: the corridor is taken from the road layout widths (the mask), never narrower
        /// than <see cref="RoadClearance.MinCorridorM"/>.
        /// </summary>
        public Func<TileData, IRoadCorridorQuery> Corridors;
    }

    /// <summary>
    /// Nature placement for a tile (W2_DESIGN 5.8; research street_life.md 9-12; docs/research/w2/ref_nature.md), in
    /// order of importance: (1) every OSM tree from PROP at its position (pipal and bar from the tree class, a chautari
    /// platform where flagged, its porter ledge toward the road; unnamed trees get a species from the area and the
    /// elevation band; a tree whose low parts stand in a road corridor is moved out of it, or dropped when deep
    /// inside); (2) avenue rows on URBAN trunk, primary and
    /// secondary roads: 30% of ways, 8-15 m apart on both sides on the verge outside the corridor, with the
    /// north/central or other district mix, never in old cores; (3) park planting (<see cref="ParkPlacement"/>);
    /// (4) house gardens (<see cref="GardenPlacement"/>); (5) a closed forest canopy inside AREA FOREST and the
    /// forest biomes, species by elevation band and aspect (Schima-Castanopsis, chir pine on south faces, oak-laurel
    /// with rhododendron, brown oak; sal in the Terai and Chure), with an understorey; (6) wild ground cover and field
    /// straw stacks (<see cref="GroundCoverPlacement"/>). Generated plants never stand on a road corridor, building or
    /// water. Seeded by the tile seed (D10; the forest lattice by world position, so it continues across tile edges),
    /// so everything stays put between builds. Returns the instances added.
    /// </summary>
    public static class TreePlacement
    {
        private const uint PurposeAvenue = 0x41564E55, PurposeForest = 0x46525354, PurposeOsm = 0x4F534D54;

        public static int Place(TileData t, IHeightSampler h, TreePlacementOptions o, List<TreeInstance> output)
        {
            if (o == null) o = new TreePlacementOptions();
            return Place(t, h, o, o.Corridors != null && t != null ? o.Corridors(t) : null, output);
        }

        /// <summary>Placement with an explicit corridor query (tile-local metres; null for the layout fallback).</summary>
        public static int Place(TileData t, IHeightSampler h, TreePlacementOptions o, IRoadCorridorQuery corridor, List<TreeInstance> output)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (o == null) o = new TreePlacementOptions();
            int before = output.Count;
            var c = new PlacementContext(t, h, o, corridor, output);
            Osm(c);
            Avenues(c);
            if (o.Parks) ParkPlacement.Place(c);
            if (o.Gardens) GardenPlacement.Place(c);
            Forest(c);
            if (o.GroundCover) GroundCoverPlacement.Place(c);
            return output.Count - before;
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void Osm(PlacementContext c)
        {
            TileData t = c.T;
            foreach (PropRecord p in t.Props)
            {
                if (p.Kind != ObjectKind.Tree) continue;
                var rng = new FloraRng((uint)(p.OsmRef ^ (p.OsmRef >> 32)), PurposeOsm);
                double x = p.XCm / 100.0, z = p.ZCm / 100.0;
                TreeSpecies sp;
                switch (p.Tree)
                {
                    case TreeClass.Pipal: sp = TreeSpecies.Pipal; break;
                    case TreeClass.Bar: sp = TreeSpecies.Bar; break;
                    case TreeClass.Conifer: sp = TreeSpecies.ChirPine; break;
                    case TreeClass.Palm: sp = TreeSpecies.Palm; break;
                    default: sp = GuessSpecies(c, x, z, ref rng); break;
                }
                FloraInfo info = FloraCatalog.Info(sp);
                float hgt = p.HeightDm > 0 ? p.HeightDm / 10f : rng.Range(info.MinHeightM, info.MaxHeightM);
                float crown = hgt * info.Aspect * rng.Range(0.9f, 1.1f);
                float yaw = rng.Range(0f, 360f);
                bool chautari = p.Has(PropFlags.Chautari);
                float platform = chautari ? DefaultPlatformM(crown) : 0f;
                if (c.Corridor != null)
                {
                    // Rideability beats exact positions (contract §1.1): everything below 4.5 m (trunk, buttresses,
                    // prop roots, a low crown) out of the corridor, and a chautari's whole platform with its ledge.
                    double margin = PlacementContext.RoadMarginFor(sp, hgt, crown);
                    // Moved at most 6 m beyond what a bare trunk would need (a bar's prop roots reach 6-8 m out).
                    double maxMove = 6.0 + Math.Max(0.0, margin - 1.2);
                    double x0 = x, z0 = z;
                    if (chautari && !FitChautari(c, ref x, ref z, ref yaw, ref platform, margin, maxMove))
                    {
                        chautari = false; // no platform fits beside this road: the tree stands without one
                        platform = 0f;
                    }
                    if (!chautari && c.Corridor.SignedDistance(x, z) < margin && !Nudge(c, ref x, ref z, margin, maxMove))
                    {
                        c.OsmDropped++;
                        continue;
                    }
                    if (x != x0 || z != z0) c.OsmMoved++;
                }
                c.Add(sp, x, z, hgt, crown, yaw, TreeOrigin.Osm, 0, p.OsmRef, chautari, -1f, platform);
            }
        }

        /// <summary>The chautari platform side the renderer draws by default: 0.45 of the crown, 3-9 m.</summary>
        public static float DefaultPlatformM(float crownM)
        {
            return Math.Max(3f, Math.Min(9f, crownM * 0.45f));
        }

        /// <summary>
        /// Outline points of the chautari platform in its unit frame (side 1, centre at the origin; FloraMesher.Chautari):
        /// the slab corners and edge middles (slab overhang included), the porter ledge's outer corners on −Z and the
        /// step's.
        /// </summary>
        internal static readonly float[] PlatformOutline =
        {
            0.514f, 0.514f, -0.514f, 0.514f, 0.514f, -0.514f, -0.514f, -0.514f, 0.514f, 0f, -0.514f, 0f, 0f, 0.514f,
            0.4f, -0.618f, -0.4f, -0.618f, 0.15f, -0.672f, -0.15f, -0.672f, 0f, -0.672f,
        };

        /// <summary>Least corridor distance of a chautari platform of side <paramref name="w"/> turned by
        /// <paramref name="yawDeg"/> at (x, z).</summary>
        internal static double PlatformClearance(IRoadCorridorQuery q, double x, double z, float yawDeg, float w)
        {
            double a = yawDeg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a), best = double.MaxValue;
            for (int i = 0; i + 1 < PlatformOutline.Length; i += 2)
            {
                double ux = PlatformOutline[i] * w, uz = PlatformOutline[i + 1] * w;
                // Clockwise from north (Unity yaw): x' = x cos + z sin, z' = -x sin + z cos.
                best = Math.Min(best, q.SignedDistance(x + ux * ca + uz * sa, z - ux * sa + uz * ca));
            }
            return best;
        }

        /// <summary>
        /// Place a chautari beside the roads: its porter ledge turned toward the nearest road (porters back their loads
        /// onto it from the trail), the whole platform at least 0.3 m and the tree's low reach outside every corridor,
        /// moved at most <paramref name="maxMove"/>; when the default platform does not fit, smaller ones (down to 3 m)
        /// are tried. False when none fits.
        /// </summary>
        private static bool FitChautari(PlacementContext c, ref double x, ref double z, ref float yaw, ref float platform, double margin, double maxMove)
        {
            for (float w = platform; w >= 3f - 1e-3f; w = w > 3f ? Math.Max(3f, w - 1.5f) : 0f)
            {
                double px = x, pz = z;
                float yw = yaw;
                for (int step = 0; step < 32; step++)
                {
                    double gx, gz;
                    bool hasDir = Gradient(c, px, pz, out gx, out gz);
                    // Local −Z (the ledge) along −gradient, toward the road: yaw = atan2(gx, gz).
                    if (hasDir) yw = (float)(Math.Atan2(gx, gz) * 180 / Math.PI);
                    // Measured where the instance will stand (its float position).
                    double fx = (float)px, fz = (float)pz;
                    double need = Math.Min(PlatformClearance(c.Corridor, fx, fz, yw, w) - 0.3, c.Corridor.SignedDistance(fx, fz) - margin);
                    if (need >= 0)
                    {
                        if ((px - x) * (px - x) + (pz - z) * (pz - z) > maxMove * maxMove) break;
                        x = px;
                        z = pz;
                        yaw = yw < 0 ? yw + 360f : yw;
                        platform = w;
                        return true;
                    }
                    if (!hasDir) break;
                    double move = Math.Max(0.25, 0.01 - need);
                    px += gx * move;
                    pz += gz * move;
                }
                if (w <= 3f) break;
            }
            return false;
        }

        /// <summary>The unit gradient of the corridor distance at (x, z) (pointing away from the nearest road).</summary>
        private static bool Gradient(PlacementContext c, double x, double z, out double gx, out double gz)
        {
            const double e = 0.25;
            gx = c.Corridor.SignedDistance(x + e, z) - c.Corridor.SignedDistance(x - e, z);
            gz = c.Corridor.SignedDistance(x, z + e) - c.Corridor.SignedDistance(x, z - e);
            double gl = Math.Sqrt(gx * gx + gz * gz);
            if (gl < 1e-9) return false;
            gx /= gl;
            gz /= gl;
            return true;
        }

        /// <summary>Move (x, z) up the corridor distance gradient until it is <paramref name="margin"/> outside every
        /// corridor, at most <paramref name="maxMove"/> metres; false when that is not enough.</summary>
        private static bool Nudge(PlacementContext c, ref double x, ref double z, double margin, double maxMove)
        {
            double x0 = x, z0 = z, px = x, pz = z;
            for (int step = 0; step < 24; step++)
            {
                // Measured where the instance will stand (its float position).
                double d = c.Corridor.SignedDistance((float)px, (float)pz);
                if (d >= margin)
                {
                    if ((px - x0) * (px - x0) + (pz - z0) * (pz - z0) > maxMove * maxMove) return false;
                    x = px;
                    z = pz;
                    return true;
                }
                double gx, gz;
                if (!Gradient(c, px, pz, out gx, out gz)) return false;
                double move = Math.Max(0.25, margin - d + 0.01);
                px += gx * move;
                pz += gz * move;
            }
            return false;
        }

        private static readonly float[] UrbanMix = { 30, 20, 15, 15, 10, 10 };
        private static readonly float[] VillageMix = { 25, 15, 10, 10, 10, 10, 10, 10 };

        /// <summary>A species for an unnamed OSM broadleaf: the street mix in the city, the village mix (with Schima,
        /// Castanopsis, utis and eucalyptus) outside it, the forest band above 1,800 m, sal below 1,000 m.</summary>
        private static TreeSpecies GuessSpecies(PlacementContext c, double x, double z, ref FloraRng rng)
        {
            float y = c.Height(x, z);
            if (y >= 1800f) return ForestSpecies(y, false, ref rng);
            if (y < 1000f) return rng.Chance(0.5f) ? TreeSpecies.Sal : TreeSpecies.Broadleaf;
            AreaType a = c.AreaAt(x, z);
            if (a == AreaType.OldCore || a == AreaType.Urban || a == AreaType.Unknown)
            {
                switch (rng.Pick(UrbanMix))
                {
                    case 0: return TreeSpecies.Broadleaf;
                    case 1: return TreeSpecies.Camphor;
                    case 2: return TreeSpecies.Bottlebrush;
                    case 3: return TreeSpecies.Jacaranda;
                    case 4: return TreeSpecies.SilkyOak;
                    default: return TreeSpecies.Eucalyptus;
                }
            }
            switch (rng.Pick(VillageMix))
            {
                case 0: return TreeSpecies.Broadleaf;
                case 1: return TreeSpecies.Schima;
                case 2: return TreeSpecies.Castanopsis;
                case 3: return TreeSpecies.Alnus;
                case 4: return TreeSpecies.Camphor;
                case 5: return TreeSpecies.SilkyOak;
                case 6: return TreeSpecies.Eucalyptus;
                default: return TreeSpecies.Bottlebrush;
            }
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void Avenues(PlacementContext c)
        {
            TileData t = c.T;
            if (t.Roads.Count == 0) return;
            double lon, lat;
            WorldFrame.GameToLonLat(t.Tile.X0 + t.Tile.Size * 0.5, t.Tile.Z0 + t.Tile.Size * 0.5, out lon, out lat);
            bool central = lat >= 27.695; // north and central districts
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (r.RoadClass != RoadClass.Trunk && r.RoadClass != RoadClass.Primary && r.RoadClass != RoadClass.Secondary) continue;
                if ((r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) continue;
                int[] p = r.Points;
                int n = p.Length / 2;
                if (n < 2) continue;
                // Never in old cores (lanes too narrow): the area type at the middle of the way.
                int mid = n / 2;
                if (c.AreaAt(p[2 * mid] / 100.0, p[2 * mid + 1] / 100.0) != AreaType.Urban) continue;
                var pick = new FloraRng(FloraRng.Mix((uint)r.OsmWayId, (uint)(r.OsmWayId >> 32)), PurposeAvenue);
                if (!pick.Chance(c.O.AvenueShare)) continue;
                double spacing = pick.Range(c.O.AvenueSpacingMin, c.O.AvenueSpacingMax);
                double along = 0.5 * spacing;
                for (int k = 0; k + 1 < n; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (len < 1e-6) continue;
                    double ux = (bx - ax) / len, uz = (bz - az) / len;
                    for (; along < len; along += spacing)
                    {
                        double cx = ax + ux * along, cz = az + uz * along;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            var rng = new FloraRng(FloraRng.Mix(c.Seed, (uint)(cx * 10) ^ (uint)(cz * 10) << 16 ^ (uint)(side + 1)), PurposeAvenue);
                            TreeSpecies sp = AvenueSpecies(central, ref rng);
                            float hgt, crown;
                            PlacementContext.Size01(sp, ref rng, out hgt, out crown);
                            double margin = PlacementContext.RoadMarginFor(sp, hgt, crown);
                            // Step out from the centreline onto the verge: the first clear spot within 14 m.
                            for (double off = 3.0; off <= 14.0; off += 0.5)
                            {
                                double x = cx - uz * side * off, z = cz + ux * side * off;
                                if ((c.Mask.At(x, z) & PlacementMask.OsmTree) != 0) break;
                                if (!c.Clear(x, z, 3.0, margin)) continue;
                                c.Add(sp, x, z, hgt, crown, rng.Range(0f, 360f), TreeOrigin.Avenue);
                                break;
                            }
                        }
                    }
                    along -= len;
                }
            }
        }

        private static readonly float[] CentralMix = { 45, 20, 15, 20 };
        private static readonly float[] OtherMix = { 20, 25, 15, 15, 25 };

        /// <summary>Avenue mix (W2_DESIGN 5.8): north and central districts jacaranda 45, silky oak 20, bottlebrush 15,
        /// other 20; elsewhere jacaranda 20, bottlebrush 25, silky oak 15, ficus 15, other 25.</summary>
        private static TreeSpecies AvenueSpecies(bool central, ref FloraRng rng)
        {
            if (central)
            {
                switch (rng.Pick(CentralMix))
                {
                    case 0: return TreeSpecies.Jacaranda;
                    case 1: return TreeSpecies.SilkyOak;
                    case 2: return TreeSpecies.Bottlebrush;
                    default: return rng.Chance(0.5f) ? TreeSpecies.Camphor : TreeSpecies.Broadleaf;
                }
            }
            switch (rng.Pick(OtherMix))
            {
                case 0: return TreeSpecies.Jacaranda;
                case 1: return TreeSpecies.Bottlebrush;
                case 2: return TreeSpecies.SilkyOak;
                case 3: return TreeSpecies.Pipal;
                default: return rng.Chance(0.5f) ? TreeSpecies.Camphor : TreeSpecies.Broadleaf;
            }
        }

        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Forest canopy (W2_DESIGN 5.8; ref_nature.md 4): trees on a world-fixed jittered lattice
        /// (<see cref="TreePlacementOptions.ForestSpacingM"/>, widened evenly when a tile would pass
        /// <see cref="TreePlacementOptions.MaxForestTrees"/>) over the forest cells, each crown about 1.45 lattice steps
        /// wide so neighbouring crowns overlap into the continuous dark canopy the rim forests show from the valley,
        /// heights following the crowns through each species' proportions. Species come by elevation band and aspect
        /// (<see cref="ForestSpecies(float, bool, float, float)"/>) in patches about 70 m across (stands of pine,
        /// Schima-Castanopsis, oak), with a few understorey plants under one tree in three. Trees keep their low reach
        /// out of the road corridors (<see cref="PlacementContext.RoadMarginFor"/>) and off OSM trees, buildings and
        /// water; the lattice continues across tile edges.
        /// </summary>
        private static void Forest(PlacementContext c)
        {
            PlacementMask mask = c.Mask;
            TreePlacementOptions o = c.O;
            int n = mask.N;
            double cell = mask.CellM;
            int forestCells = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                if ((mask.At((i + 0.5) * cell, (j + 0.5) * cell) & PlacementMask.Forest) != 0) forestCells++;
            if (forestCells == 0 || o.MaxForestTrees <= 0) return;
            double s = Math.Max(4.0, o.ForestSpacingM);
            double expected = forestCells * cell * cell / (s * s);
            if (expected > o.MaxForestTrees) s *= Math.Sqrt(expected / o.MaxForestTrees);
            double x0w = c.T.Tile.X0, z0w = c.T.Tile.Z0;
            long i0 = (long)Math.Floor(x0w / s) - 1, i1 = (long)Math.Floor((x0w + c.Size) / s) + 1;
            long j0 = (long)Math.Floor(z0w / s) - 1, j1 = (long)Math.Floor((z0w + c.Size) / s) + 1;
            int trees = 0;
            for (long j = j0; j <= j1 && trees < o.MaxForestTrees; j++)
            for (long i = i0; i <= i1 && trees < o.MaxForestTrees; i++)
            {
                // World-fixed seeding: the same lattice point gets the same tree from every tile.
                var rng = new FloraRng(FloraRng.Mix((uint)i ^ (uint)(i >> 32) * 0x9E3779B9u, (uint)j ^ (uint)(j >> 32) * 0x85EBCA6Bu), PurposeForest);
                double wx = (i + 0.5 + rng.Jitter(0.34f)) * s, wz = (j + 0.5 + rng.Jitter(0.34f)) * s;
                double x = wx - x0w, z = wz - z0w;
                if (x < 0 || z < 0 || x >= c.Size || z >= c.Size) continue;
                byte m = mask.At(x, z);
                if ((m & PlacementMask.Forest) == 0 || (m & PlacementMask.OsmTree) != 0) continue;
                float y = c.Height(x, z), slope, aspect;
                c.Slope(x, z, out slope, out aspect);
                bool south = slope > 0.08f && aspect >= 135f && aspect <= 225f;
                Biome biome = c.BiomeAt(x, z);
                // Stands: the species draw follows a ~70 m noise patch more than the single tree.
                float patch = 0.5f + 0.5f * FloraNoise.Value(wx / 70.0, wz / 70.0, 0x5354414Eu);
                float patch2 = 0.5f + 0.5f * FloraNoise.Value(wx / 90.0 + 17.3, wz / 90.0 - 4.1, 0x50494E45u);
                float r = Math.Max(0f, Math.Min(0.999f, 0.62f * patch + 0.38f * rng.Next()));
                float r2 = Math.Max(0f, Math.Min(0.999f, 0.62f * patch2 + 0.38f * rng.Next()));
                TreeSpecies sp = biome == Biome.TeraiSalForest || biome == Biome.ChureForest || y < 1000f
                    ? (r < 0.8f ? TreeSpecies.Sal : TreeSpecies.Broadleaf)
                    : ForestSpecies(y, south, r, r2);
                float hgt, crown;
                ForestSize(sp, s, ref rng, out hgt, out crown);
                if (!c.Clear(x, z, 1.2, PlacementContext.RoadMarginFor(sp, hgt, crown))) continue;
                int clump = 1 + (int)(((i >> 1) & 0x3FF) | ((j >> 1) & 0x3FF) << 10);
                c.Add(sp, x, z, hgt, crown, rng.Range(0f, 360f), TreeOrigin.Forest, clump, 0, false, 1.1f);
                trees++;
                if (o.Understorey && !c.PlantsFull && rng.Chance(0.3f)) Understorey(c, x, z, y, south, ref rng);
            }
        }

        /// <summary>
        /// A forest tree's size on a lattice of step <paramref name="spacing"/>: the crown about 1.45 steps wide (±12 %),
        /// so neighbours overlap, and the height from the species' proportions (crown over height = its model aspect
        /// × 1.1, a little wider than in the open), kept in the species' height range.
        /// </summary>
        internal static void ForestSize(TreeSpecies sp, double spacing, ref FloraRng rng, out float heightM, out float crownM)
        {
            FloraInfo info = FloraCatalog.Info(sp);
            float ratio = info.Aspect * 1.1f * rng.Range(0.96f, 1.04f);
            crownM = (float)(spacing * 1.45) * rng.Range(0.88f, 1.12f);
            heightM = crownM / ratio;
            float lo = info.MinHeightM, hi = info.MaxHeightM;
            if (heightM < lo || heightM > hi)
            {
                heightM = Math.Max(lo, Math.Min(hi, heightM));
                crownM = heightM * ratio;
            }
        }

        /// <summary>One or two plants under a forest tree: ferns and shrubs in the broadleaf bands, grass and rocks on
        /// the open pine floor, ferns and rocks in the oak and rhododendron forest.</summary>
        private static void Understorey(PlacementContext c, double x, double z, float y, bool south, ref FloraRng rng)
        {
            int count = rng.Int(1, 2);
            for (int q = 0; q < count; q++)
            {
                double a = rng.Range(0f, 6.2832f), r = rng.Range(1.8f, 5f);
                double px = x + r * Math.Cos(a), pz = z + r * Math.Sin(a);
                float u = rng.Next();
                TreeSpecies sp;
                if (south && y < 2000f) sp = u < 0.5f ? TreeSpecies.GrassTuft : u < 0.8f ? TreeSpecies.Rock : TreeSpecies.Shrub; // open pine floor
                else if (y >= 1800f) sp = u < 0.5f ? TreeSpecies.Fern : u < 0.8f ? TreeSpecies.Shrub : TreeSpecies.Rock;
                else sp = u < 0.4f ? TreeSpecies.Fern : u < 0.7f ? TreeSpecies.Shrub : u < 0.9f ? TreeSpecies.GrassTuft : TreeSpecies.Rock;
                float hgt, crown;
                PlacementContext.Size01(sp, ref rng, out hgt, out crown);
                if (!c.Clear(px, pz, 0.4 * crown, PlacementContext.RoadMarginFor(sp, hgt, crown))) continue;
                if (!c.Add(sp, px, pz, hgt, crown, rng.Range(0f, 360f), TreeOrigin.Forest)) return;
            }
        }

        /// <summary>Forest bands by elevation (W2_DESIGN 5.8; S 11): fringe 1,300-1,400 m, Schima-Castanopsis to
        /// 1,800 m (60% of the broadleaf weight swaps to chir pine on south-facing slopes), oak-laurel with rhododendron
        /// to 2,400 m, brown oak above.</summary>
        internal static TreeSpecies ForestSpecies(float elevation, bool southFacing, ref FloraRng rng)
        {
            float r = rng.Next();
            return ForestSpecies(elevation, southFacing, r, rng.Next());
        }

        /// <summary>The band species for draws <paramref name="r"/> (the mix) and <paramref name="pine"/> (below 0.6 a
        /// south face at 1,400-1,800 m is chir pine), both in [0, 1).</summary>
        internal static TreeSpecies ForestSpecies(float elevation, bool southFacing, float r, float pine)
        {
            if (elevation >= 2400) return r < 0.8f ? TreeSpecies.BrownOak : TreeSpecies.Rhododendron;
            if (elevation >= 1800)
            {
                if (r < 0.6f) return TreeSpecies.Oak;
                if (r < 0.8f) return TreeSpecies.Rhododendron;
                return r < 0.9f ? TreeSpecies.Bamboo : TreeSpecies.Broadleaf;
            }
            if (southFacing && elevation >= 1400 && pine < 0.6f) return TreeSpecies.ChirPine;
            if (elevation < 1400) return r < 0.4f ? TreeSpecies.Schima : r < 0.7f ? TreeSpecies.Castanopsis : r < 0.85f ? TreeSpecies.Alnus : TreeSpecies.Broadleaf;
            if (r < 0.4f) return TreeSpecies.Schima;
            if (r < 0.7f) return TreeSpecies.Castanopsis;
            return r < 0.8f ? TreeSpecies.Alnus : TreeSpecies.Broadleaf;
        }
    }
}
