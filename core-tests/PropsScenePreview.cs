using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual check of the street furniture in place (package props): street scenes of the sample pack as the streamer
    /// builds them (TileBuild.Execute on High), with every placed prop drawn from the kit at its yaw and scale. Set
    /// GHUMANTE_PREVIEW_DIR and PROPS_SHOTS="name,lon,lat,bearingDeg,distM,eyeH,lookH;..." (the eye stands distM from
    /// the target towards the bearing), then render each folder with tools/mesh-preview/render.py --combine.
    /// </summary>
    public class PropsScenePreview
    {
        [Test]
        public void DumpScenes()
        {
            string spec = Environment.GetEnvironmentVariable("PROPS_SHOTS");
            if (!ObjDump.Enabled || string.IsNullOrWhiteSpace(spec)) Assert.Pass("set " + ObjDump.EnvVar + " and PROPS_SHOTS");
            var c = CultureInfo.InvariantCulture;
            var settings = new MeshingSettings();
            var ground = new TileGroundQuery(settings.Roads);
            var cache = new Dictionary<ulong, TileBuild>();
            var index = new StringBuilder();
            foreach (string line in spec.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split(',');
                string name = p[0].Trim();
                double lon = D(p[1]), lat = D(p[2]), bearing = D(p[3]) * Math.PI / 180, dist = D(p[4]);
                float eyeH = (float)D(p[5]), lookH = (float)D(p[6]);
                double gx, gz;
                WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
                TileId id = TileId.At(10, gx, gz);
                TileBuild b;
                if (!cache.TryGetValue(id.Key, out b))
                {
                    b = new TileBuild { Node = new SelectedNode(id, id) };
                    TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(2), settings, ground);
                    cache[id.Key] = b;
                }
                Assert.That(b.Error, Is.Null);
                float tx = (float)(gx - id.X0), tz = (float)(gz - id.Z0);
                float ex = tx + (float)(dist * Math.Sin(bearing)), ez = tz + (float)(dist * Math.Cos(bearing));
                float ey = Ground(b, ex, ez) + eyeH, ly = Ground(b, tx, tz) + lookH;
                string eye = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez), look = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", tx, ly, -tz);
                var directives = new[] { "props scene " + name + " tile " + id, "meshpreview: grid=0 sun=160,62 eye=" + eye + " look=" + look };
                const float r = 260f;
                float x0 = Math.Min(ex, tx) - r, z0 = Math.Min(ez, tz) - r, x1 = Math.Max(ex, tx) + r, z1 = Math.Max(ez, tz) + r;
                foreach (int layer in new[] { TileLayers.Terrain, TileLayers.Roads, TileLayers.RoadDecals, TileLayers.Areas, TileLayers.Buildings })
                {
                    MeshData m = ObjDump.Crop(b.Layers[layer], x0, z0, x1, z1, true);
                    if (m.TriangleCount > 0) ObjDump.Write(m, name + "/layer" + layer + ".obj", directives);
                }
                // The bridges at their nearest level (the streamer stores every level of every span in one layer).
                var bridges = new MeshData();
                BridgeMesher.Build(b.Source, b.Sampler, new BridgeOptions(), bridges);
                MeshData bc = ObjDump.Crop(bridges, x0, z0, x1, z1, true);
                if (bc.TriangleCount > 0) ObjDump.Write(bc, name + "/bridges.obj", directives);
                var props = new MeshData();
                var unit = new MeshData();
                foreach (StreetProp sp in b.Instances.Props)
                {
                    float dx = sp.X - ex, dz = sp.Z - ez;
                    if (dx * dx + dz * dz > r * r) continue;
                    unit.Clear();
                    StreetPropMesher.Build(sp.Kind, 0, unit);
                    float sc = sp.HeightM > 0 ? sp.HeightM / StreetPropMesher.DefaultHeight(sp.Kind) : 1f;
                    FloraObj.Append(props, unit, sp.X, sp.Y, sp.Z, sp.YawDeg, sc, sc, sc);
                }
                if (props.TriangleCount > 0) ObjDump.Write(props, name + "/props.obj", directives);
                index.Append(name).Append(": tile ").Append(id).Append(" eye ").Append(eye).Append(" look ").Append(look).Append('\n');
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "props_scenes.txt"), index.ToString());
            TestContext.WriteLine(index.ToString());
        }

        private static double D(string s)
        {
            return double.Parse(s.Trim(), CultureInfo.InvariantCulture);
        }

        private static float Ground(TileBuild b, double x, double z)
        {
            double gx = b.Node.Area.X0 + x, gz = b.Node.Area.Z0 + z;
            float y;
            b.Sampler.TryHeightClamped(gx, gz, out y);
            RoadSurfaceHit hit;
            if (RoadSurfaceQuery.For(b.Source, b.Sampler, new RoadOptions()).TrySample(gx, gz, y + 1f, out hit)) return hit.Y;
            return y;
        }
    }
}
