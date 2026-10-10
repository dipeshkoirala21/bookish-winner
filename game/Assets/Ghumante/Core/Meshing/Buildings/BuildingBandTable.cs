using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The building LOD band table of the detail pass (W2_DESIGN 2.4 as re-budgeted for the rounded, detailed B0 of
    /// docs/W2_DETAIL_CONTRACT.md decision 6): the band radii per device tier (0 Low, 1 Mid, 2 High) measured
    /// horizontally from the camera, the B0 cap per house, and the channel and AO paint of the far bands. B0 houses
    /// now cost about three times the stage-1 grammar, so B0 ends nearer the camera and the cheap styled band B1 takes
    /// over sooner: the tile totals at Asan stay inside the W2 slice. Engine-free; World/Buildings/BandConfig reads it.
    /// </summary>
    public static class BuildingBandTable
    {
        /// <summary>B0 triangle cap for one house on the High tier and in whole-tile builds (the NEWAR cap of
        /// ASSET_MANIFEST §4 is 5,000; a merged footprint holding several plots counts as one house).</summary>
        public const int B0CapTris = 5000;

        private static readonly int[] Caps = { 2600, 4000, 5000 };

        /// <summary>Outer radius (m) of B0, B1, B2 and B3 per tier.</summary>
        private static readonly float[][] Radii =
        {
            new[] { 22f, 120f, 350f, 750f },
            new[] { 32f, 200f, 500f, 1250f },
            new[] { 42f, 250f, 700f, 1750f },
        };

        /// <summary>The B0 cap per house on a tier (Low houses drop detail sooner).</summary>
        public static int B0CapFor(int tier)
        {
            return Caps[tier <= 0 ? 0 : tier >= 2 ? 2 : 1];
        }

        /// <summary>The outer radius of <paramref name="band"/> (0..3) on <paramref name="tier"/> (0 Low, 1 Mid, 2 High).</summary>
        public static float OuterM(int tier, int band)
        {
            tier = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            band = band <= 0 ? 0 : band >= 3 ? 3 : band;
            return Radii[tier][band];
        }

        /// <summary>W2 building slice per tier (W2_DESIGN 2.4 / 10.4 budget-check totals, triangles in view).</summary>
        public static int SliceTris(int tier)
        {
            return tier <= 0 ? 20000 : tier >= 2 ? 115000 : 63000;
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
