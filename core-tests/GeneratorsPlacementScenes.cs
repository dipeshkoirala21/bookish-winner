using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check scenes of the nature package (docs/W2_DETAIL_CONTRACT.md §6): one level-10 tile around a
    /// real place, with the terrain, areas, roads and B1 buildings and every placed plant within the radius as the
    /// game draws it (LOD0 near the eye, LOD1 mid, the family volume far), dumped as OBJ files with an eye point.
    /// Environment: GHUMANTE_PREVIEW_DIR (required), GHUMANTE_NATURE_SCENE ("name:lon,lat[:eyeAzDeg:eyeDistM:eyeHeightM:month[:park]]",
    /// "park" focusing on the centre of the nearest park area;
    /// several separated by ';'), GHUMANTE_NATURE_PACK (a .ghpk; default the kathmandu_core sample).
    /// </summary>
    public class GeneratorsPlacementScenes
    {
        [Test]
        public void DumpNatureScenes()
        {
            if (FloraObj.Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            string spec = Environment.GetEnvironmentVariable("GHUMANTE_NATURE_SCENE");
            if (string.IsNullOrWhiteSpace(spec)) spec = "ratnapark:85.3155,27.7046;swayambhu:85.2904,27.7149";
            string packPath = Environment.GetEnvironmentVariable("GHUMANTE_NATURE_PACK");
            PackReader pack = string.IsNullOrWhiteSpace(packPath) ? StreamingSampleRegion.Pack : new PackReader(File.ReadAllBytes(packPath));
            CultureInfo ci = CultureInfo.InvariantCulture;
            foreach (string one in spec.Split(';'))
            {
                string[] parts = one.Split(':');
                string name = parts[0];
                string[] ll = parts[1].Split(',');
                double lon = double.Parse(ll[0], ci), lat = double.Parse(ll[1], ci);
                float az = parts.Length > 2 ? float.Parse(parts[2], ci) : 200f, dist = parts.Length > 3 ? float.Parse(parts[3], ci) : 90f;
                float eyeH = parts.Length > 4 ? float.Parse(parts[4], ci) : 30f;
                int month = parts.Length > 5 ? int.Parse(parts[5], ci) : 10;
                bool park = parts.Length > 6 && parts[6] == "park";
                Scene(pack, name, lon, lat, az, dist, eyeH, month, park);
            }
        }

        private static void Scene(PackReader pack, string name, double lon, double lat, float azDeg, float dist, float eyeH, int month, bool park)
        {
            double gx, gz;
            WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
            TileId id = TileId.At(10, gx, gz);
            TileData t = pack.ReadTile(id);
            Assert.That(t, Is.Not.Null, name + ": tile " + id + " not in the pack");
            double fx = gx - id.X0, fz = gz - id.Z0;
            if (park)
            {
                // Focus on the centre of the park area nearest to the given point.
                double best = double.MaxValue, bx = fx, bz = fz;
                foreach (AreaRecord pa in t.Areas)
                {
                    if (pa.Kind != AreaKind.Park || pa.Vertices == null || pa.Vertices.Length < 6) continue;
                    double cx = 0, cz = 0;
                    int n = pa.Vertices.Length / 2;
                    for (int k = 0; k < n; k++)
                    {
                        cx += pa.Vertices[2 * k] / 100.0 / n;
                        cz += pa.Vertices[2 * k + 1] / 100.0 / n;
                    }
                    double d = (cx - fx) * (cx - fx) + (cz - fz) * (cz - fz);
                    if (d < best)
                    {
                        best = d;
                        bx = cx;
                        bz = cz;
                    }
                }
                fx = bx;
                fz = bz;
            }
            var sampler = TileHeightSampler.ForArea(t, id, 1);
            var terrain = new MeshData();
            TerrainMesher.Build(t, id, new TerrainOptions { Step = 1, Season = SeasonOf(month) }, terrain);
            var areas = new MeshData();
            AreaMesher.Build(t, sampler, new AreaOptions(), areas);
            var roads = new MeshData();
            RoadMesher.Build(t, sampler, new RoadOptions(), roads);
            var buildings = new MeshData();
            BuildingMesher.Build(t, sampler, new BuildingOptions { Band = BuildingBand.B1Styled }, buildings);
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, sampler, new TreePlacementOptions(), trees);

            float fy;
            if (!sampler.TryHeight(id.X0 + fx, id.Z0 + fz, out fy)) fy = 1300f;
            double a = azDeg * Math.PI / 180;
            float ex = (float)(fx + Math.Sin(a) * dist), ez = (float)(fz + Math.Cos(a) * dist), ey;
            if (!sampler.TryHeight(id.X0 + ex, id.Z0 + ez, out ey)) ey = fy;
            ey += eyeH;
            var flora = new MeshData();
            var cache = new Dictionary<int, MeshData>();
            int[] perLod = new int[4];
            double radius = Math.Max(250, dist * 2.5);
            foreach (TreeInstance tr in trees)
            {
                if (!FloraCatalog.InSeason(tr.Species, month)) continue;
                double dx = tr.X - ex, dz = tr.Z - ez, d = Math.Sqrt(dx * dx + dz * dz);
                double dfx = tr.X - fx, dfz = tr.Z - fz;
                if (Math.Sqrt(dfx * dfx + dfz * dfz) > radius) continue;
                bool tree = FloraCatalog.IsTree(tr.Species);
                int lod = d < 70 ? 0 : d < 220 || !tree ? 1 : 2;
                if (!tree && d > 120) continue;
                perLod[lod]++;
                int key = (int)tr.Species * 4 + lod;
                MeshData unit;
                if (!cache.TryGetValue(key, out unit))
                {
                    unit = new MeshData();
                    FloraMesher.Build(tr.Species, lod, month, unit);
                    if (lod >= 2) Tint(unit, FloraCatalog.FoliageColour(tr.Species, month));
                    cache[key] = unit;
                }
                FloraObj.Append(flora, unit, tr.X, tr.Y - 0.1f, tr.Z, tr.YawDeg, tr.CrownM, tr.HeightM, tr.CrownM);
                if (tr.Chautari)
                {
                    var plat = new MeshData();
                    FloraMesher.Chautari(0, plat);
                    float w = Math.Max(3f, Math.Min(9f, tr.CrownM * 0.45f));
                    FloraObj.Append(flora, plat, tr.X, tr.Y, tr.Z, tr.YawDeg, w, 0.7f, w);
                }
            }
            string dir = "scenes/" + name;
            string eye = string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#},{2:0.#}", ex, ey, -ez);
            string look = string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#},{2:0.#}", fx, fy + 4, -fz);
            string directive = "grid=0 sun=160,55 eye=" + eye + " look=" + look;
            FloraObj.Write(terrain, dir + "/terrain.obj", directive);
            FloraObj.Write(areas, dir + "/areas.obj", directive);
            FloraObj.Write(roads, dir + "/roads.obj", directive);
            FloraObj.Write(ObjCrop(buildings, fx, fz, radius + 150), dir + "/buildings.obj", directive);
            FloraObj.Write(flora, dir + "/flora.obj", directive);
            var counts = trees.GroupBy(tr => tr.Species).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count());
            var sb = new StringBuilder();
            sb.Append(name).Append(" tile ").Append(id).Append(" month ").Append(month).Append(": ").Append(trees.Count).Append(" instances; drawn LOD0 ")
              .Append(perLod[0]).Append(", LOD1 ").Append(perLod[1]).Append(", far ").Append(perLod[2]).Append("; flora ").Append(flora.TriangleCount)
              .Append(" tris, terrain ").Append(terrain.TriangleCount).Append(", areas ").Append(areas.TriangleCount).Append("\n").Append(string.Join(", ", counts));
            sb.Append("\nareas: ").Append(string.Join(", ", t.Areas.GroupBy(x => x.Kind).Select(g => g.Key + " " + g.Count() + " (" + g.Sum(x => ParkArea(x)).ToString("0") + " m2)")));
            if (t.Biomes != null)
                sb.Append("\nbiomes: ").Append(string.Join(", ", t.Biomes.GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())));
            File.WriteAllText(Path.Combine(FloraObj.Dir, dir, "summary.txt"), sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }

        private static double ParkArea(AreaRecord a)
        {
            double twice = 0;
            for (int k = 0; k + 2 < a.Indices.Length; k += 3)
            {
                int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                double x0 = a.Vertices[2 * i0], z0 = a.Vertices[2 * i0 + 1];
                twice += Math.Abs((a.Vertices[2 * i1] - x0) * (a.Vertices[2 * i2 + 1] - z0) - (a.Vertices[2 * i1 + 1] - z0) * (a.Vertices[2 * i2] - x0));
            }
            return twice * 0.5 / 10000.0;
        }

        private static Season SeasonOf(int month)
        {
            return month >= 3 && month <= 5 ? Season.Spring : month >= 6 && month <= 9 ? Season.Monsoon : month == 10 || month == 11 ? Season.Autumn : Season.Winter;
        }

        /// <summary>Multiply the tinted (alpha 255) vertex colours by an sRGB colour (what the instance tint does).</summary>
        private static void Tint(MeshData m, uint rgb)
        {
            float r = (rgb >> 16 & 0xFF) / 255f, g = (rgb >> 8 & 0xFF) / 255f, b = (rgb & 0xFF) / 255f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (m.Colors[4 * v + 3] != 255) continue;
                m.Colors[4 * v] = (byte)(m.Colors[4 * v] * r);
                m.Colors[4 * v + 1] = (byte)(m.Colors[4 * v + 1] * g);
                m.Colors[4 * v + 2] = (byte)(m.Colors[4 * v + 2] * b);
            }
        }

        /// <summary>The triangles whose centroid lies within <paramref name="r"/> of (x, z).</summary>
        private static MeshData ObjCrop(MeshData m, double x, double z, double r)
        {
            var o = new MeshData();
            var map = new Dictionary<int, int>();
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double cx = 0, cz = 0;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * t + k];
                    cx += m.Positions[3 * v] / 3.0;
                    cz += m.Positions[3 * v + 2] / 3.0;
                }
                if ((cx - x) * (cx - x) + (cz - z) * (cz - z) > r * r) continue;
                int[] tri = new int[3];
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * t + k], nv;
                    if (!map.TryGetValue(v, out nv))
                    {
                        uint rgba = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                        nv = m.HasUv0
                            ? o.AddVertex(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2], rgba,
                                          m.Uv0[2 * v], m.Uv0[2 * v + 1])
                            : o.AddVertex(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2], rgba);
                        map[v] = nv;
                    }
                    tri[k] = nv;
                }
                o.AddTriangle(tri[0], tri[1], tri[2]);
            }
            return o;
        }
    }
}
