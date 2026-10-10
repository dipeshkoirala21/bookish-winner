using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// How far a plant's geometry reaches sideways below a height, measured on its own near meshes (docs/W2_DETAIL_CONTRACT.md
    /// §1.1-1.2: nothing may stand in, or hang below <see cref="RoadClearance.MinOverheadClearanceM"/> over, a road
    /// corridor). For every tree kind the LOD0 and LOD1 unit models (October) are scanned once into a profile: the
    /// largest distance from the trunk axis of any vertex below each twentieth of the height. So a bar's prop roots, a
    /// pipal's buttresses, an oak's low crooked limbs or a bottlebrush's low crown all count, while a crown that starts
    /// above 4.5 m may overhang the road. Built on first use (about forty mesh builds, once per process; thread-safe);
    /// lookups are allocation-free.
    /// </summary>
    public static class FloraReach
    {
        /// <summary>Profile samples: heights 0, 1/20, ..., 1 of the unit model.</summary>
        public const int Samples = 21;

        private static readonly float[][] Profiles = Build();

        private static float[][] Build()
        {
            var table = new float[FloraCatalog.Count][];
            var m = new MeshData(4096, 12288);
            for (int s = 0; s < FloraCatalog.Count; s++)
            {
                var sp = (TreeSpecies)s;
                var prof = new float[Samples];
                for (int lod = 0; lod < 2; lod++)
                {
                    m.Clear();
                    FloraMesher.Build(sp, lod, 10, m);
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                        float r = (float)Math.Sqrt(x * x + z * z);
                        int k = (int)Math.Ceiling(Math.Max(0f, y) * (Samples - 1) - 1e-4);
                        if (k < 0) k = 0;
                        if (k >= Samples) continue;
                        if (r > prof[k]) prof[k] = r;
                    }
                }
                // Cumulative: everything below a height.
                for (int k = 1; k < Samples; k++) prof[k] = Math.Max(prof[k], prof[k - 1]);
                table[s] = prof;
            }
            return table;
        }

        /// <summary>The largest horizontal distance (unit crown widths) from the axis of any part of a kind's near
        /// models below the unit height <paramref name="unitY"/> (rounded up to the next profile sample: conservative).</summary>
        public static float UnitReachBelow(TreeSpecies s, float unitY)
        {
            int i = (int)s;
            float[] p = i >= 0 && i < Profiles.Length ? Profiles[i] : Profiles[0];
            if (unitY >= 1f) return p[Samples - 1];
            if (unitY <= 0f) return p[0];
            int k = (int)Math.Ceiling(unitY * (Samples - 1));
            return p[Math.Min(Samples - 1, k)];
        }

        /// <summary>
        /// The reach (metres from the trunk axis) of a plant of height <paramref name="heightM"/> and crown
        /// <paramref name="crownM"/> below <paramref name="belowM"/> (default the overhead clearance of 4.5 m): what
        /// must keep out of a road corridor.
        /// </summary>
        public static float LowReachM(TreeSpecies s, float heightM, float crownM, float belowM = RoadClearance.MinOverheadClearanceM)
        {
            if (heightM <= 0f || crownM <= 0f) return 0f;
            return UnitReachBelow(s, belowM / heightM) * crownM;
        }
    }
}
