using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Meshing;
using Ghumante.World.Buildings;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual evidence for owner playtest #4 and #12 (GHUMANTE_PREVIEW_DIR, rendered with tools/mesh-preview): the houses
    /// at the owner's spots with every visibly different coplanar overlap (z-fighting) marked magenta, and the band
    /// cross-fade ray-traced from the rides' chase camera under the old complementary dithers and the stencil hand-off,
    /// with every pixel where no band draws (a hole into the house) marked magenta.
    /// </summary>
    public class WorldRenderingStabilityPreviews
    {
        /// <summary>
        /// Per spot, the house with the most z-fighting area (or the building index named in
        /// GHUMANTE_ZFIGHT_HOUSES as spot=index,...) as OBJ: the house, and the fighting triangles lifted 1 cm in magenta.
        /// </summary>
        [Test]
        public void DumpZFightHouses()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the previews");
            string tag = Environment.GetEnvironmentVariable("GHUMANTE_ZFIGHT_TAG") ?? "now";
            var pick = new Dictionary<string, int>();
            string env = Environment.GetEnvironmentVariable("GHUMANTE_ZFIGHT_HOUSES");
            if (!string.IsNullOrEmpty(env))
                foreach (string kv in env.Split(','))
                {
                    string[] p = kv.Split('=');
                    pick[p[0]] = int.Parse(p[1], CultureInfo.InvariantCulture);
                }
            var index = new StringBuilder();
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                int best = -1;
                double bestArea = -1;
                MeshData bestMesh = null;
                for (int i = 0; i < scene.Tile.Buildings.Count; i++)
                {
                    if (pick.ContainsKey(spot.Name) && pick[spot.Name] != i) continue;
                    if (!scene.NearRides(scene.Tile.Buildings[i], 45f)) continue;
                    MeshData m = scene.House(i);
                    CoplanarScan s = CoplanarScan.Run(m, WorldRenderingZFightTests.FightM, WorldRenderingZFightTests.MinOverlapM2, 0);
                    if (s.AreaM2 > bestArea)
                    {
                        bestArea = s.AreaM2;
                        best = i;
                        bestMesh = m;
                    }
                }
                if (bestMesh == null) continue;
                var fights = new MeshData();
                var seen = new HashSet<int>();
                CoplanarScan scan = CoplanarScan.Run(bestMesh, WorldRenderingZFightTests.FightM, WorldRenderingZFightTests.MinOverlapM2, 0, (a, b, area, gap) =>
                {
                    foreach (int t in new[] { a, b })
                    {
                        if (!seen.Add(t)) continue;
                        int[] v = { bestMesh.Indices[3 * t], bestMesh.Indices[3 * t + 1], bestMesh.Indices[3 * t + 2] };
                        int f0 = fights.VertexCount;
                        foreach (int k in v)
                            fights.AddVertex(bestMesh.Positions[3 * k] + 0.01f * bestMesh.Normals[3 * k], bestMesh.Positions[3 * k + 1] + 0.01f * bestMesh.Normals[3 * k + 1],
                                             bestMesh.Positions[3 * k + 2] + 0.01f * bestMesh.Normals[3 * k + 2], bestMesh.Normals[3 * k], bestMesh.Normals[3 * k + 1],
                                             bestMesh.Normals[3 * k + 2], 0xFF00FFFFu);
                        fights.AddTriangle(f0, f0 + 1, f0 + 2);
                    }
                });
                string dir = "stable/zfight/" + spot.Name + "_b" + best;
                ObjDump.Write(bestMesh, dir + "/house_" + tag + ".obj", new[] { "meshpreview: grid=0 sun=150,55" });
                if (fights.TriangleCount > 0) ObjDump.Write(fights, dir + "/fights_" + tag + ".obj", new[] { "meshpreview: grid=0 sun=150,55" });
                index.Append(spot.Name).Append(" b").Append(best).Append(' ').Append(tag).Append(": ").Append(scan).Append('\n');
            }
            OccluderSpotScene.WriteReport("zfight_previews_" + tag + ".txt", index.ToString());
            TestContext.WriteLine(index.ToString());
        }

        /// <summary>
        /// The near/lite band edge (30 m on High) ray-traced from the chase camera of the rides, at the frame where the most
        /// house wall lies in the fade ring, zoomed 4x on that wall: what each pixel shows under the old complementary dithers
        /// (seam_before.ppm) and the stencil hand-off (seam_after.ppm). Near-ring fragments are drawn warm, lite-ring ones
        /// cool, sky grey-blue, and pixels where no band draws magenta.
        /// </summary>
        [Test]
        public void DumpBandSeamImages()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the previews");
            var index = new StringBuilder();
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                float edge = BandConfig.ForTier(2).B0NearOuterM;
                OccluderSpotScene.CameraFrame frame = scene.FrameWithMostRing(scene.Houses, edge);
                int holesBefore, holesAfter;
                string dir = Path.Combine(ObjDump.Dir, "stable", "seam");
                Directory.CreateDirectory(dir);
                // Zoomed 4x on the wall in the ring (the 4 m ring is a narrow band of the full view).
                scene.TraceSeam(frame, scene.Houses, scene.LiteHouses, edge, 0, Path.Combine(dir, spot.Name + "_before.ppm"), out holesBefore, 4f);
                scene.TraceSeam(frame, scene.Houses, scene.LiteHouses, edge, 2, Path.Combine(dir, spot.Name + "_after.ppm"), out holesAfter, 4f);
                index.Append(spot.Name).Append(": hole pixels before ").Append(holesBefore).Append(", after ").Append(holesAfter).Append('\n');
            }
            OccluderSpotScene.WriteReport("seam_previews.txt", index.ToString());
            TestContext.WriteLine(index.ToString());
        }
    }
}
