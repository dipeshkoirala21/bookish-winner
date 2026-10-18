using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check (docs/W2_DETAIL_CONTRACT.md §6) of the bridges package's foot structures: a structure's drawn
    /// pieces at LOD0 (<see cref="BridgeMesher.BuildSpan"/>) with its solids (<see cref="BridgeColliders"/>, red) and a
    /// ground plate, as OBJ under GHUMANTE_PREVIEW_DIR. Real scenes of the sample: <see cref="BridgeCrossingTests.DumpScene"/>.
    /// </summary>
    public class BridgePreviewDumps
    {
        /// <summary>The Kanti Path style overbridge as OSM maps it (stairs along the pavements, mapped to the deck's centre
        /// line at its ends), with and without a branch deck.</summary>
        [Test, Explicit]
        public void DumpMappedOverbridge()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set GHUMANTE_PREVIEW_DIR");
            Dump(BridgeScenes.MappedOverbridge(false), "bridges2/mapped_overbridge", 500, 500, 22);
            Dump(BridgeScenes.MappedOverbridge(true), "bridges2/mapped_overbridge_branch", 510, 500, 28);
        }

        private static void Dump(TileData t, string name, double cx, double cz, double half)
        {
            BridgeLayout l = BridgeLayout.For(t);
            var h = new TileHeightSampler(t, 1);
            BridgeOptions o = StreamingScene.Settings(2, 10).BridgeLod(0);
            var m = new MeshData { HasUv0 = true };
            for (int si = 0; si < l.Spans.Count; si++) BridgeMesher.BuildSpan(l, si, h, o, m);
            var shifted = new MeshData { HasUv0 = true };
            ObjDump.Append(shifted, m, (float)-cx, 0f, (float)-cz);
            // A ground plate at the terrain under the centre.
            float g;
            h.TryHeight(t.Tile.X0 + cx, t.Tile.Z0 + cz, out g);
            var plate = new MeshData();
            BridgeWorld.Slab(plate, -half, 0, half, 0, (float)half, g - 0.05f, g, 0xC8C0B0FFu);
            var solids = new MeshData();
            BridgeColliders c = BridgeColliders.For(l);
            foreach (BridgeColliders.Wall w in c.Walls)
                BridgeWorld.Slab(solids, w.X0 - cx, w.Z0 - cz, w.X1 - cx, w.Z1 - cz, Math.Max(0.03f, w.HalfThickness), w.Bottom, w.Top, 0xD03030FFu);
            foreach (BridgeColliders.Column k in c.Columns)
                BridgeWorld.Slab(solids, k.CX - cx, k.CZ - cz, k.CX - cx, k.CZ - cz, k.Radius, k.Bottom, k.Top, 0xD03030FFu);
            ObjDump.Write(shifted, name + "/bridges.obj");
            ObjDump.Write(plate, name + "/ground.obj");
            ObjDump.Write(solids, name + "/solids.obj");
        }
    }
}
