using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The building LOD band table of the detail pass (W2_DESIGN 2.4 re-budgeted for the rounded, detailed B0 of
    /// docs/W2_DETAIL_CONTRACT.md decision 6), the one source of truth for radii, caps and the building slice. Per device
    /// tier (0 Low, 1 Mid, 2 High), measured horizontally from the camera:
    /// <list type="bullet">
    /// <item>B0 keeps the W2 radii (35 / 60 / 80 m) and is split in two rings: the <b>near</b> ring
    /// (<see cref="NearOuterM"/>: 10 / 22 / 36 m) draws the full grammar held to <see cref="B0CapFor"/> triangles per
    /// house (per plot of a row), from the tier's <see cref="B0BaseDropFor"/>; the <b>lite</b> ring beyond draws every
    /// house at <see cref="LiteDropFor"/>: on Mid and High the lite level (openings cut with their reveals and plain
    /// frames, coarse lattice, the sanjhya as a bay, eaves, shutters and boards, a tank on the roof; about a quarter of
    /// the full cost), on Low the flat level (the same house with its openings laid on the wall; about a seventh). Every
    /// level keeps the same structure (Core HouseBuilder forks its random draws per element), so a house never changes
    /// between the rings. No house inside B0 is ever the bare B1 box.</item>
    /// <item>B1 (the styled extrusion with its front detail: paint, floor band, window rows), B2 prisms and B3 blocks
    /// beyond, as in W2_DESIGN 2.4.</item>
    /// </list>
    /// The budget check (core-tests MeshingBuildingGrammarTests) measures the bands at Asan, the densest chowk, on the
    /// 3 × 3 tiles around it as the game draws them (B0 per house by distance, B1-B3 per triangle with the band
    /// cross-fade), 40% in the frustum: B0 stays within <see cref="B0ShareTris"/> and the total within
    /// <see cref="SliceTris"/> × <see cref="SliceHeadroom"/>. Engine-free; World/Buildings/BandConfig reads it.
    /// </summary>
    public static class BuildingBandTable
    {
        /// <summary>B0 triangle cap per house in whole-tile builds and previews (the NEWAR cap of ASSET_MANIFEST §4); the
        /// tiers use <see cref="B0CapFor"/>.</summary>
        public const int B0CapTris = 5000;

        /// <summary>The drop level of the lite B0 ring on Mid and High (Core HouseBuilder.LiteDrop).</summary>
        public const int LiteDrop = 5;

        /// <summary>The drop level of the lite B0 ring on Low (Core HouseBuilder.FlatDrop): openings laid on the wall.</summary>
        public const int FlatDrop = 6;

        /// <summary>Near-ring B0 cells split each 64 m detail cell this many times per side (32 m cells): the rich houses
        /// drawn around the camera are only those of the few small cells the near ring touches.</summary>
        public const int NearSubdivision = 2;

        /// <summary>Headroom on <see cref="SliceTris"/> for the band-table estimate (40% of the buildings of each ring in
        /// the frustum), as in W2_DESIGN 2.4 / V3.</summary>
        public const double SliceHeadroom = 1.15;

        private static readonly int[] Caps = { 2200, 3600, 4500 };
        private static readonly int[] BaseDrops = { 1, 0, 0 };
        private static readonly float[] NearRadii = { 10f, 22f, 36f };

        /// <summary>The B0 share of the building slice per tier at Asan (40% in view): the measured near and lite rings
        /// stay under it (Low 5.7 k, Mid 38 k, High 85 k when this table was set), and the far bands B1-B3 measured the
        /// same way take the rest (Low 19 k, Mid 40 k, High 62 k).</summary>
        private static readonly int[] B0Shares = { 5800, 41000, 90000 };

        /// <summary>Outer radius (m) of B0, B1, B2 and B3 per tier (W2_DESIGN 2.4).</summary>
        private static readonly float[][] Radii =
        {
            new[] { 35f, 120f, 350f, 750f },
            new[] { 60f, 200f, 500f, 1250f },
            new[] { 80f, 250f, 700f, 1750f },
        };

        private static int Tier(int tier)
        {
            return tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
        }

        /// <summary>The triangles B0 (both rings) may take of the building slice on a tier, at Asan with 40% in view.</summary>
        public static int B0ShareTris(int tier)
        {
            return B0Shares[Tier(tier)];
        }

        /// <summary>The near-ring B0 cap per house (per plot of a row) on a tier.</summary>
        public static int B0CapFor(int tier)
        {
            return Caps[Tier(tier)];
        }

        /// <summary>The richest near-ring B0 drop level on a tier (Low starts without the smallest relief).</summary>
        public static int B0BaseDropFor(int tier)
        {
            return BaseDrops[Tier(tier)];
        }

        /// <summary>Outer radius (m) of the near B0 ring on a tier; the lite ring runs from it to <c>OuterM(tier, 0)</c>.</summary>
        public static float NearOuterM(int tier)
        {
            return NearRadii[Tier(tier)];
        }

        /// <summary>The outer radius of <paramref name="band"/> (0..3) on <paramref name="tier"/> (0 Low, 1 Mid, 2 High).</summary>
        public static float OuterM(int tier, int band)
        {
            band = band <= 0 ? 0 : band >= 3 ? 3 : band;
            return Radii[Tier(tier)][band];
        }

        /// <summary>The W2 building slice per tier (W2_DESIGN 10.4: 22 k / 72 k / 135 k triangles in view, generic sacred
        /// buildings included).</summary>
        public static int SliceTris(int tier)
        {
            int k = Tier(tier);
            return k == 0 ? 22000 : k == 1 ? 72000 : 135000;
        }

        /// <summary>The options of the near B0 ring on a tier (<paramref name="o"/> copied: corridors, hide set, sink).</summary>
        public static BuildingOptions NearOptions(int tier, BuildingOptions o = null)
        {
            BuildingOptions r = Copy(o, BuildingBand.B0KitLite);
            r.B0CapTris = B0CapFor(tier);
            r.B0BaseDrop = B0BaseDropFor(tier);
            return r;
        }

        /// <summary>The drop level of the lite B0 ring on a tier: <see cref="FlatDrop"/> on Low, <see cref="LiteDrop"/>
        /// on Mid and High.</summary>
        public static int LiteDropFor(int tier)
        {
            return Tier(tier) == 0 ? FlatDrop : LiteDrop;
        }

        /// <summary>The options of the lite B0 ring at the lite level (Mid and High; <paramref name="o"/> copied).</summary>
        public static BuildingOptions LiteOptions(BuildingOptions o = null)
        {
            return LiteOptions(1, o);
        }

        /// <summary>The options of the lite B0 ring on a tier (<see cref="LiteDropFor"/>; <paramref name="o"/> copied).</summary>
        public static BuildingOptions LiteOptions(int tier, BuildingOptions o = null)
        {
            BuildingOptions r = Copy(o, BuildingBand.B0KitLite);
            r.B0CapTris = int.MaxValue;
            r.B0BaseDrop = LiteDropFor(tier);
            return r;
        }

        /// <summary>The options of the B1 layer: the styled extrusion with its street-front detail (front paint, floor
        /// band, window rows; W2_DESIGN 2.4), which the budget counts. The streamer's B1 layer should be built with these
        /// (World/Streaming TileBuild builds it from MeshingSettings.Buildings, default options: an open issue for the
        /// integration package).</summary>
        public static BuildingOptions B1Options(BuildingOptions o = null)
        {
            BuildingOptions r = Copy(o, BuildingBand.B1Styled);
            r.Styled = true;
            r.FrontDetail = true;
            return r;
        }

        /// <summary>A copy of <paramref name="o"/> (or the defaults) for <paramref name="band"/>.</summary>
        public static BuildingOptions Copy(BuildingOptions o, BuildingBand band)
        {
            if (o == null) o = new BuildingOptions();
            return new BuildingOptions
            {
                SinkM = o.SinkM, SkipLandmarks = o.SkipLandmarks, MinAreaM2 = o.MinAreaM2, Parapets = o.Parapets, Band = band, Styled = o.Styled,
                FrontDetail = o.FrontDetail, HiddenRefs = o.HiddenRefs, B0CapTris = o.B0CapTris, B0BaseDrop = o.B0BaseDrop, RoadGuard = o.RoadGuard,
                Corridors = o.Corridors,
            };
        }

        /// <summary>
        /// Channel and AO for a far-band building appended from <paramref name="v0"/> (B1 extrusions, B2 prisms, B3
        /// blocks): up-facing faces are roofs (jhingati <see cref="MaterialChannel.RoofTile"/> on tile roofs, else
        /// concrete or CGI metal), walls are brick on Newar, hybrid and raw-brick walls, plaster on Rana, paint
        /// elsewhere; AO darkens the bottom 2.5 m of the walls (the lowest vertex sits <paramref name="sinkM"/> under
        /// the ground) and downward faces.
        /// </summary>
        public static void PaintFar(MeshData m, int v0, in HousePlan plan, float sinkM)
        {
            if (v0 >= m.VertexCount) return;
            KitPaint.Begin(m);
            float minY = float.MaxValue;
            for (int v = v0; v < m.VertexCount; v++) minY = Math.Min(minY, m.Positions[3 * v + 1]);
            double ground = minY + (plan.MinHeightM > 0 ? 0 : sinkM);
            MaterialChannel wallCh, frontCh, roofCh;
            switch (plan.Archetype)
            {
                case BuildingArchetype.Newar:
                case BuildingArchetype.NewarHybrid:
                    wallCh = MaterialChannel.Brick;
                    frontCh = plan.Front == plan.Wall ? MaterialChannel.Brick : MaterialChannel.Paint;
                    break;
                case BuildingArchetype.RanaPalace:
                    wallCh = frontCh = MaterialChannel.Plaster;
                    break;
                case BuildingArchetype.ModernUrban:
                case BuildingArchetype.Generic:
                    wallCh = plan.Wall == BuildingGrammar.RawBrick ? MaterialChannel.Brick : MaterialChannel.Paint;
                    frontCh = plan.Front == BuildingGrammar.RawBrick ? MaterialChannel.Brick : MaterialChannel.Paint;
                    break;
                default:
                    wallCh = frontCh = MaterialChannel.Plaster;
                    break;
            }
            roofCh = plan.TileRoof || plan.Roof == PlanRoof.Gable ? MaterialChannel.RoofTile : plan.Roof == PlanRoof.Skillion ? MaterialChannel.Metal : MaterialChannel.Concrete;
            uint front = plan.Front, window = MeshColor.Scale(plan.Front, 0.42f) | 0xFF, band = MeshColor.Scale(plan.Front, 0.75f) | 0xFF,
                 shop = MeshColor.FromHex(0x8C949C);
            float[] p = m.Positions, n = m.Normals, uv = m.Uv0;
            byte[] c = m.Colors;
            for (int v = v0; v < m.VertexCount; v++)
            {
                double ny = n[3 * v + 1], y = p[3 * v + 1];
                MaterialChannel ch;
                if (ny > 0.35) ch = roofCh;
                else
                {
                    uint col = (uint)(c[4 * v] << 24 | c[4 * v + 1] << 16 | c[4 * v + 2] << 8 | 0xFF);
                    ch = col == (front | 0xFF) ? frontCh : col == window ? MaterialChannel.Glass : col == shop ? MaterialChannel.Metal :
                         col == band ? frontCh : wallCh;
                }
                double t = (y - ground) / 2.5;
                double ao = t <= 0 ? 0.55 : t >= 1 ? 1.0 : 0.55 + 0.45 * t;
                if (ny < 0) ao *= 1.0 + 0.3 * ny;
                uv[2 * v] = (float)ch;
                uv[2 * v + 1] = (float)ao;
            }
        }
    }
}
