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
    /// the target towards the bearing) or "name,sig,lon,lat,k,distM" (a rider's view of the k-th signal nearest the
    /// point: in the lane under its head, distM upstream, looking at it), then render each folder with tools/mesh-preview/render.py --combine.
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
                // "name,sig,lon,lat,k,distM": the driver's view of the k-th signal nearest (lon, lat): the eye in the lane
                // under its head, distM upstream, at rider height, looking at the head.
                bool sig = p[1].Trim() == "sig", deck = p[1].Trim() == "deck";
                int pi = sig || deck ? 2 : 1;
                double lon = D(p[pi]), lat = D(p[pi + 1]), bearing = sig || deck ? 0 : D(p[3]) * Math.PI / 180, dist = sig || deck ? D(p[5]) : D(p[4]);
                float eyeH = sig || deck ? 1.7f : (float)D(p[5]), lookH = sig || deck ? 0f : (float)D(p[6]);
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
                float ex, ez, ey, ly;
                if (sig)
                {
                    StreetProp s = NearestSignal(b.Instances.Props, tx, tz, (int)D(p[4]));
                    StreetPropMesher.SignalHead head;
                    StreetPropMesher.TryHangingHead(s.Kind, out head);
                    double yaw = s.YawDeg * Math.PI / 180, fa = yaw + head.FacingDeg * Math.PI / 180;
                    tx = s.X + (float)(Math.Sin(yaw) * head.ForwardM);
                    tz = s.Z + (float)(Math.Cos(yaw) * head.ForwardM);
                    ex = tx + (float)(dist * Math.Sin(fa));
                    ez = tz + (float)(dist * Math.Cos(fa));
                    ey = Ground(b, ex, ez) + eyeH;
                    ly = s.Y + (float)(0.5 * (head.BottomM + head.TopM)) - 1.5f;
                }
                else if (deck)
                {
                    // "name,deck,lon,lat,k,distM": the k-th lamp standing on a bridge deck nearest the point, seen from the
                    // deck's carriageway distM along the road, at rider height.
                    StreetProp s = NearestDeckLamp(b, tx, tz, (int)D(p[4]));
                    double yaw = s.YawDeg * Math.PI / 180, fx = Math.Sin(yaw), fz = Math.Cos(yaw);
                    tx = s.X;
                    tz = s.Z;
                    ex = s.X + (float)(fx * 3.5 + fz * dist);
                    ez = s.Z + (float)(fz * 3.5 - fx * dist);
                    ey = s.Y + eyeH;
                    ly = s.Y + 3f;
                }
                else
                {
                    ex = tx + (float)(dist * Math.Sin(bearing));
                    ez = tz + (float)(dist * Math.Cos(bearing));
                    ey = Ground(b, ex, ez) + eyeH;
                    ly = Ground(b, tx, tz) + lookH;
                }
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

        /// <summary>The <paramref name="k"/>-th nearest lamp post (0 = nearest) to tile-local (x, z) whose base is on a
        /// bridge deck.</summary>
        private static StreetProp NearestDeckLamp(TileBuild b, float x, float z, int k)
        {
            IBridgeDeckQuery decks = BridgeDeckIndex.ForTile(b.Source);
            var list = new List<StreetProp>();
            foreach (StreetProp p in b.Instances.Props)
            {
                float dy, nx, ny, nz;
                if (!PropPlacement.IsLampPost(p.Kind)) continue;
                if (decks.TryDeck(b.Node.Area.X0 + p.X, b.Node.Area.Z0 + p.Z, p.Y + 0.5f, out dy, out nx, out ny, out nz) && Math.Abs(dy - p.Y) < 0.05f) list.Add(p);
            }
            Assert.That(list.Count, Is.GreaterThan(k), "lamps on decks on the tile");
            list.Sort((a, c) => ((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z)).CompareTo((c.X - x) * (c.X - x) + (c.Z - z) * (c.Z - z)));
            return list[k];
        }

        /// <summary>The <paramref name="k"/>-th nearest signal (0 = nearest) to tile-local (x, z).</summary>
        private static StreetProp NearestSignal(List<StreetProp> props, float x, float z, int k)
        {
            var list = new List<StreetProp>();
            foreach (StreetProp p in props)
                if (StreetPropMesher.IsSignal(p.Kind)) list.Add(p);
            Assert.That(list.Count, Is.GreaterThan(k), "signals on the tile");
            list.Sort((a, b) => ((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z)).CompareTo((b.X - x) * (b.X - x) + (b.Z - z) * (b.Z - z)));
            return list[k];
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
