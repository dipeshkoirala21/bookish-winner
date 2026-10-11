using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class ZzProbeB0
    {
        [Test]
        public void CopyCost()
        {
            var b = new MeshingBuildingB0CellTests.Bench(2);
            double x, z;
            WorldFrame.LonLatToGame(85.3122, 27.7074, out x, out z);
            b.AddTilesAround(x, z);
            b.Settle(x, z);
            MethodInfo copy = typeof(B0CellSet).GetMethod("CopyHouse", BindingFlags.NonPublic | BindingFlags.Static);
            int houses = 0; long tris = 0;
            var sw = new Stopwatch();
            foreach (B0Tile t in b.Tiles.Values)
                foreach (B0CellSet.Cell c in t.Cells.Values)
                {
                    B0CellSet.Version v = c.Live;
                    if (v == null || v.Data == null) continue;
                    for (int rep = 0; rep < 5; rep++)
                    {
                        var dst = new B0CellSet.Version { Data = new MeshData(4096, 12288), Colliders = new StructureColliders() };
                        sw.Start();
                        for (int k = 0; k < v.Positions.Length; k++) copy.Invoke(null, new object[] { v, v.Data, k, dst });
                        sw.Stop();
                        houses += v.Positions.Length;
                        tris += dst.Data.TriangleCount;
                    }
                }
            TestContext.WriteLine("copied {0} houses, {1:0} tris/house: {2:0.000} ms/house", houses, tris / (double)houses, sw.Elapsed.TotalMilliseconds / houses);
        }
    }
}
