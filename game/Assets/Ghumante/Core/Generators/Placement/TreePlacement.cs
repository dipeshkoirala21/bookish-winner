using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>Tree species and classes of the cartoon tree kit (W2_DESIGN 5.8).</summary>
    public enum TreeSpecies : byte
    {
        Broadleaf = 0,
        Pipal = 1,
        Bar = 2,
        Jacaranda = 3,
        SilkyOak = 4,
        Bottlebrush = 5,
        Camphor = 6,
        Eucalyptus = 7,
        Bamboo = 8,
        Palm = 9,
        Schima = 10,
        Castanopsis = 11,
        Alnus = 12,
        ChirPine = 13,
        Oak = 14,
        Rhododendron = 15,
        BrownOak = 16,
    }

    /// <summary>The three shape families × four sizes of the tree kit.</summary>
    public enum TreeShape : byte
    {
        Round = 0,
        Cone = 1,
        Umbrella = 2,
    }

    /// <summary>Where a tree came from.</summary>
    public enum TreeOrigin : byte
    {
        /// <summary>An OSM tree at its real position (PROP).</summary>
        Osm = 0,

        /// <summary>A generated avenue tree along an URBAN arterial.</summary>
        Avenue = 1,

        /// <summary>Generated forest scatter inside real forest.</summary>
        Forest = 2,
    }

    /// <summary>One placed tree (tile-local metres, absolute Y). Trees of one forest clump share a
    /// <see cref="ClumpId"/> (> 0) so they can be merged into one mesh.</summary>
    public struct TreeInstance
    {
        public float X, Y, Z;
        public float HeightM, CrownM, YawDeg;
        public TreeSpecies Species;
        public TreeShape Shape;
        public byte SizeClass;
        public TreeOrigin Origin;
        public bool Chautari;
        public int ClumpId;
        public ulong OsmRef;
    }

    /// <summary>Options of <see cref="TreePlacement"/>.</summary>
    public sealed class TreePlacementOptions
    {
        /// <summary>Share of URBAN arterial ways (trunk, primary, secondary) that carry an avenue.</summary>
        public float AvenueShare = 0.30f;

        public float AvenueSpacingMin = 8f, AvenueSpacingMax = 15f;

        /// <summary>Forest clumps per hectare (3-7 trees each): about 150 trees per hectare.</summary>
        public float ClumpsPerHa = 30f;

        /// <summary>Hard cap of forest trees per tile (thinned uniformly, deterministically, beyond it).</summary>
        public int MaxForestTrees = 8000;
    }

    /// <summary>
    /// Tree placement (W2_DESIGN 5.8), in its order: (1) every OSM tree from PROP at its position (pipal and bar from
    /// the tree class, a chautari platform where flagged); (2) avenue rows on URBAN trunk, primary and secondary roads
    /// without OSM trees nearby: 30% of ways, 8-15 m apart on both sides beyond the footpath, with the north/central or
    /// other district mix, never in old cores; (3) forest clumps of 3-7 trees inside AREA FOREST and the forest biomes,
    /// species by elevation band (Schima-Castanopsis, chir pine on south-facing slopes, oak-laurel with rhododendron,
    /// brown oak). Generated trees never stand on a road, footpath, building or water (<see cref="PlacementMask"/>).
    /// Seeded by the tile seed (D10), so trees stay put between builds. Returns the trees added.
    /// </summary>
    public static class TreePlacement
    {
        private const uint PurposeAvenue = 0x41564E55, PurposeForest = 0x46525354, PurposeOsm = 0x4F534D54;

        public static int Place(TileData t, IHeightSampler h, TreePlacementOptions o, List<TreeInstance> output)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (o == null) o = new TreePlacementOptions();
            int before = output.Count;
            var g = new RoadSurface(t, h);
            uint seed = (uint)(t.TileSeed ^ (t.TileSeed >> 32));
            if (!t.HasSeed) seed = (uint)(t.Tile.Key ^ (t.Tile.Key >> 32));
            Osm(t, ref g, seed, output);
            var mask = new PlacementMask(t);
            Avenues(t, ref g, mask, seed, o, output);
            Forest(t, ref g, mask, seed, o, output);
            return output.Count - before;
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void Osm(TileData t, ref RoadSurface g, uint seed, List<TreeInstance> output)
        {
            foreach (PropRecord p in t.Props)
            {
                if (p.Kind != ObjectKind.Tree) continue;
                var rng = new GrammarRng((uint)(p.OsmRef ^ (p.OsmRef >> 32)), PurposeOsm);
                TreeSpecies sp;
                switch (p.Tree)
                {
                    case TreeClass.Pipal: sp = TreeSpecies.Pipal; break;
                    case TreeClass.Bar: sp = TreeSpecies.Bar; break;
                    case TreeClass.Conifer: sp = TreeSpecies.ChirPine; break;
                    case TreeClass.Palm: sp = TreeSpecies.Palm; break;
                    default: sp = TreeSpecies.Broadleaf; break;
                }
                float hgt = p.HeightDm > 0 ? p.HeightDm / 10f : DefaultHeight(sp, ref rng);
                double x = p.XCm / 100.0, z = p.ZCm / 100.0;
                output.Add(Tree(x, g.Height(x, z), z, sp, hgt, ref rng, TreeOrigin.Osm, p.Has(PropFlags.Chautari), 0, p.OsmRef));
            }
        }

        private static float DefaultHeight(TreeSpecies sp, ref GrammarRng rng)
        {
            switch (sp)
            {
                case TreeSpecies.Pipal: return rng.Range(15f, 25f);
                case TreeSpecies.Bar: return rng.Range(15f, 20f);
                case TreeSpecies.Jacaranda: return rng.Range(8f, 15f);
                case TreeSpecies.SilkyOak: return rng.Range(18f, 30f);
                case TreeSpecies.Bottlebrush: return rng.Range(4f, 8f);
                case TreeSpecies.Eucalyptus: return rng.Range(15f, 35f);
                case TreeSpecies.Camphor: return rng.Range(10f, 20f);
                case TreeSpecies.Bamboo: return rng.Range(8f, 15f);
                case TreeSpecies.Palm: return rng.Range(6f, 12f);
                case TreeSpecies.ChirPine: return rng.Range(12f, 25f);
                case TreeSpecies.Rhododendron: return rng.Range(6f, 12f);
                default: return rng.Range(8f, 18f);
            }
        }

        private static TreeShape ShapeOf(TreeSpecies sp)
        {
            switch (sp)
            {
                case TreeSpecies.ChirPine:
                case TreeSpecies.SilkyOak:
                case TreeSpecies.Eucalyptus: return TreeShape.Cone;
                case TreeSpecies.Jacaranda:
                case TreeSpecies.Pipal:
                case TreeSpecies.Bar: return TreeShape.Umbrella;
                default: return TreeShape.Round;
            }
        }

        private static TreeInstance Tree(double x, float y, double z, TreeSpecies sp, float h, ref GrammarRng rng, TreeOrigin origin, bool chautari, int clump, ulong osmRef)
        {
            float crown = sp == TreeSpecies.Pipal || sp == TreeSpecies.Bar ? h * rng.Range(0.9f, 1.1f)
                : ShapeOf(sp) == TreeShape.Cone ? h * 0.35f : h * rng.Range(0.5f, 0.75f);
            return new TreeInstance
            {
                X = (float)x, Y = y, Z = (float)z, HeightM = h, CrownM = crown, YawDeg = rng.Range(0f, 360f), Species = sp, Shape = ShapeOf(sp),
                SizeClass = (byte)(h < 6 ? 0 : h < 12 ? 1 : h < 20 ? 2 : 3), Origin = origin, Chautari = chautari, ClumpId = clump, OsmRef = osmRef,
            };
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void Avenues(TileData t, ref RoadSurface g, PlacementMask mask, uint seed, TreePlacementOptions o, List<TreeInstance> output)
        {
            if (t.Roads.Count == 0) return;
            RoadLayout layout = RoadLayout.For(t);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (r.RoadClass != RoadClass.Trunk && r.RoadClass != RoadClass.Primary && r.RoadClass != RoadClass.Secondary) continue;
                if (RoadWidthModel.AreaOf(layout.Attrs[ri]) != AreaType.Urban) continue; // never in old cores
                if ((r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) continue;
                var pick = new GrammarRng(GrammarRng.Mix((uint)r.OsmWayId, (uint)(r.OsmWayId >> 32)), PurposeAvenue);
                if (!pick.Chance(o.AvenueShare)) continue;
                double spacing = pick.Range(o.AvenueSpacingMin, o.AvenueSpacingMax);
                double length = layout.Profiles[ri].LengthM;
                double lon, lat;
                WorldFrame.GameToLonLat(t.Tile.X0 + t.Tile.Size * 0.5, t.Tile.Z0 + t.Tile.Size * 0.5, out lon, out lat);
                bool central = lat >= 27.695; // north and central districts
                for (double s = 0.5 * spacing; s < length; s += spacing)
                {
                    if (layout.InGap(ri, s)) continue;
                    RoadCut c = layout.CutAt(t, ri, s);
                    RoadProfile p = layout.ProfileAt(ri, s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double foot = side > 0 ? p.FootpathLeftM : p.FootpathRightM;
                        double off = c.Shift + side * (c.Half + foot + 1.2);
                        double x = c.CX + c.UX * off, z = c.CZ + c.UZ * off;
                        if (!mask.Free(x, z) || (mask.At(x, z) & PlacementMask.OsmTree) != 0) continue;
                        var rng = new GrammarRng(GrammarRng.Mix(seed, (uint)(x * 10) ^ (uint)(z * 10) << 16), PurposeAvenue);
                        TreeSpecies sp = AvenueSpecies(central, ref rng);
                        output.Add(Tree(x, g.Height(x, z), z, sp, DefaultHeight(sp, ref rng), ref rng, TreeOrigin.Avenue, false, 0, 0));
                    }
                }
            }
        }

        private static readonly float[] CentralMix = { 45, 20, 15, 20 };
        private static readonly float[] OtherMix = { 20, 25, 15, 15, 25 };

        /// <summary>Avenue mix (W2_DESIGN 5.8): north and central districts jacaranda 45, silky oak 20, bottlebrush 15,
        /// other 20; elsewhere jacaranda 20, bottlebrush 25, silky oak 15, ficus 15, other 25.</summary>
        private static TreeSpecies AvenueSpecies(bool central, ref GrammarRng rng)
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

        private static void Forest(TileData t, ref RoadSurface g, PlacementMask mask, uint seed, TreePlacementOptions o, List<TreeInstance> output)
        {
            int n = mask.N;
            double cell = mask.CellM;
            // Count forest cells first so the density can be thinned to the cap deterministically.
            int forestCells = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                if ((mask.At((i + 0.5) * cell, (j + 0.5) * cell) & PlacementMask.Forest) != 0) forestCells++;
            if (forestCells == 0) return;
            double haPerCell = cell * cell / 10000.0;
            double clumps = forestCells * haPerCell * o.ClumpsPerHa;
            double keep = Math.Min(1.0, o.MaxForestTrees / Math.Max(1.0, clumps * 5.0));
            double pPerCell = o.ClumpsPerHa * haPerCell * keep;
            int clumpId = 1;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                double cx = (i + 0.5) * cell, cz = (j + 0.5) * cell;
                if ((mask.At(cx, cz) & PlacementMask.Forest) == 0) continue;
                var rng = new GrammarRng(GrammarRng.Mix(seed, (uint)(j * n + i)), PurposeForest);
                if (rng.Next() >= pPerCell) continue;
                double x = cx + rng.Range(-0.5f, 0.5f) * (float)cell, z = cz + rng.Range(-0.5f, 0.5f) * (float)cell;
                if (!mask.Free(x, z)) continue;
                float y = g.Height(x, z);
                // Aspect: the downhill direction from the height gradient (south-facing slopes take chir pine).
                float yE = g.Height(x + 6, z), yN = g.Height(x, z + 6);
                double gx = (yE - y) / 6.0, gz = (yN - y) / 6.0, slope = Math.Sqrt(gx * gx + gz * gz);
                double aspect = Math.Atan2(-gx, -gz) * 180 / Math.PI;
                if (aspect < 0) aspect += 360;
                bool south = slope > 0.08 && aspect >= 135 && aspect <= 225;
                int count = rng.Int(3, 7);
                for (int q = 0; q < count; q++)
                {
                    double a = rng.Range(0f, 6.2832f), r = rng.Range(1.5f, 5f);
                    double tx = x + r * Math.Cos(a), tz = z + r * Math.Sin(a);
                    if (!mask.Free(tx, tz)) continue;
                    float ty = g.Height(tx, tz);
                    TreeSpecies sp = ForestSpecies(ty, south, ref rng);
                    output.Add(Tree(tx, ty, tz, sp, DefaultHeight(sp, ref rng), ref rng, TreeOrigin.Forest, false, clumpId, 0));
                }
                clumpId++;
            }
        }

        /// <summary>Forest bands by elevation (W2_DESIGN 5.8; S 11): fringe 1,300-1,400 m, Schima-Castanopsis to
        /// 1,800 m (60% of the broadleaf weight swaps to chir pine on south-facing slopes), oak-laurel with rhododendron
        /// to 2,400 m, brown oak above.</summary>
        internal static TreeSpecies ForestSpecies(float elevation, bool southFacing, ref GrammarRng rng)
        {
            float r = rng.Next();
            if (elevation >= 2400) return r < 0.8f ? TreeSpecies.BrownOak : TreeSpecies.Rhododendron;
            if (elevation >= 1800)
            {
                if (r < 0.6f) return TreeSpecies.Oak;
                if (r < 0.8f) return TreeSpecies.Rhododendron;
                return r < 0.9f ? TreeSpecies.Bamboo : TreeSpecies.Broadleaf;
            }
            if (southFacing && elevation >= 1400 && rng.Chance(0.6f)) return TreeSpecies.ChirPine;
            if (elevation < 1400) return r < 0.4f ? TreeSpecies.Schima : r < 0.7f ? TreeSpecies.Castanopsis : r < 0.85f ? TreeSpecies.Alnus : TreeSpecies.Broadleaf;
            if (r < 0.4f) return TreeSpecies.Schima;
            if (r < 0.7f) return TreeSpecies.Castanopsis;
            return r < 0.8f ? TreeSpecies.Alnus : TreeSpecies.Broadleaf;
        }
    }
}
