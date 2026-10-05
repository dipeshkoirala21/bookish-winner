using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.World.Instancing;

namespace Ghumante.World.Life
{
    /// <summary>Where a traffic police officer stands (tile-local metres).</summary>
    public struct OfficerPost
    {
        public double X, Z;

        /// <summary>On the podium (raised 0.35 m, under the umbrella) or standing on the carriageway at a corner.</summary>
        public bool OnPodium;

        /// <summary>Seeds the signal phases and the facing.</summary>
        public uint Seed;

        /// <summary>The junction's OSM node (0 for synthetic islands without a record).</summary>
        public long JunctionNode;
    }

    /// <summary>
    /// The traffic officers of a tile's police-controlled chowks (W2_DESIGN 4.7 curated list via JNCT; 5.1 POLICE
    /// controller): one officer on the podium of every chowk with police (the podium Track A's JunctionMesher draws on
    /// the island or at the junction centre), and 2-4 at the big chowks flagged OFFICERS_2_4 (Kalanki, Koteshwor,
    /// Chabahil, ...), the extra ones standing 6 m out on the diagonals. Never more than placed by the data: no officer
    /// is invented at a plain junction. Deterministic. Engine-free.
    /// </summary>
    public static class OfficerPosts
    {
        private const uint PurposeCount = 0x4F464643;

        public static List<OfficerPost> For(TileData t)
        {
            var posts = new List<OfficerPost>();
            if (t == null || t.Junctions.Count == 0 && t.Roads.Count == 0) return posts;
            RoadLayout layout = RoadLayout.For(t);
            var used = new HashSet<long>();
            foreach (RoadIsland isl in layout.Islands)
            {
                if (!isl.PolicePodium) continue;
                JunctionRecord j;
                bool hasRecord = Nearest(t, isl.X, isl.Z, 40.0, out j);
                if (hasRecord && !used.Add(j.OsmNodeId)) continue;
                Add(posts, isl.X, isl.Z, hasRecord ? j : default(JunctionRecord), hasRecord, isl.RadiusM);
            }
            foreach (JunctionCap cap in layout.Caps)
            {
                bool police = cap.Kind == JunctionKind.Police || (cap.Flags & (byte)JunctionFlags.HasPolice) != 0;
                if (!police || NearIsland(layout, cap.X, cap.Z)) continue;
                JunctionRecord j;
                bool hasRecord = Nearest(t, cap.X, cap.Z, 30.0, out j);
                if (hasRecord && !used.Add(j.OsmNodeId)) continue;
                Add(posts, cap.X, cap.Z, hasRecord ? j : default(JunctionRecord), hasRecord, 0f);
            }
            return posts;
        }

        private static void Add(List<OfficerPost> posts, double x, double z, JunctionRecord j, bool hasRecord, float islandRadius)
        {
            ulong key = hasRecord ? (ulong)j.OsmNodeId : (ulong)(long)(x * 100) << 32 ^ (ulong)(long)(z * 100);
            uint seed = WorldHash.Fnv1a(key, PurposeCount);
            posts.Add(new OfficerPost { X = x, Z = z, OnPodium = true, Seed = seed, JunctionNode = hasRecord ? j.OsmNodeId : 0 });
            if (!hasRecord || !j.Has(JunctionFlags.Officers24)) return;
            int extra = 1 + (int)(seed % 3); // 2-4 officers in all
            double r = Math.Max(6.0, islandRadius + 4.0);
            for (int k = 0; k < extra; k++)
            {
                double a = Math.PI * 0.25 + k * Math.PI * 0.5 + (seed & 0xFF) / 255.0 * 0.3;
                posts.Add(new OfficerPost
                {
                    X = x + r * Math.Cos(a), Z = z + r * Math.Sin(a), OnPodium = false, Seed = WorldHash.Fnv1a(key, PurposeCount + (uint)k + 1),
                    JunctionNode = j.OsmNodeId,
                });
            }
        }

        private static bool NearIsland(RoadLayout layout, double x, double z)
        {
            foreach (RoadIsland i in layout.Islands)
                if ((i.X - x) * (i.X - x) + (i.Z - z) * (i.Z - z) < (i.RadiusM + 10) * (i.RadiusM + 10)) return true;
            return false;
        }

        private static bool Nearest(TileData t, double x, double z, double maxM, out JunctionRecord best)
        {
            best = default(JunctionRecord);
            double bd = maxM * maxM;
            bool found = false;
            foreach (JunctionRecord j in t.Junctions)
            {
                double dx = j.XCm / 100.0 - x, dz = j.ZCm / 100.0 - z, d = dx * dx + dz * dz;
                if (d > bd) continue;
                bd = d;
                best = j;
                found = true;
            }
            return found;
        }

        /// <summary>The officer's hand signal at <paramref name="timeS"/>: phases of 25-60 s (W2_DESIGN 5.1 POLICE),
        /// seeded per post.</summary>
        public static int SignalAt(uint seed, double timeS, out double phaseStartS)
        {
            double len = 25.0 + (seed % 36);
            double t = timeS + (seed >> 8) % 60;
            long phase = (long)Math.Floor(t / len);
            phaseStartS = phase * len - (seed >> 8) % 60;
            return (int)((phase + (seed >> 16)) % 5);
        }
    }
}
