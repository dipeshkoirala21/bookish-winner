using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check of the solids (docs/W2_DETAIL_CONTRACT.md §6): writes OBJ scenes of the collision world, the
    /// drawn roads under footprint walls, point-object cylinders and railings, for tools/mesh-preview/render.py. Writes
    /// only when GHUMANTE_PREVIEW_DIR names a folder (ordinary runs write nothing).
    /// </summary>
    public class DrivingColliderPreview
    {
        private sealed class Obj
        {
            private readonly StringBuilder _v = new StringBuilder(), _f = new StringBuilder();
            private int _n;
            private readonly double _ox, _oy, _oz;

            public Obj(double ox, double oy, double oz)
            {
                _ox = ox;
                _oy = oy;
                _oz = oz;
            }

            private int V(double x, double y, double z, uint rgb)
            {
                // OBJ is right-handed: game Z (north) is negated, as ObjDump does.
                _v.AppendFormat(CultureInfo.InvariantCulture, "v {0:F3} {1:F3} {2:F3} {3:F3} {4:F3} {5:F3}\n", x - _ox, y - _oy, -(z - _oz),
                                ((rgb >> 16) & 255) / 255.0, ((rgb >> 8) & 255) / 255.0, (rgb & 255) / 255.0);
                return ++_n;
            }

            /// <summary>A triangle given counter-clockwise seen from its front in game axes (reversed for OBJ).</summary>
            public void Tri(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz, uint rgb)
            {
                int a = V(ax, ay, az, rgb), b = V(bx, by, bz, rgb), c = V(cx, cy, cz, rgb);
                _f.Append("f ").Append(a).Append(' ').Append(c).Append(' ').Append(b).Append('\n');
                _f.Append("f ").Append(a).Append(' ').Append(b).Append(' ').Append(c).Append('\n'); // both sides
            }

            public void Quad(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz,
                             double dx, double dy, double dz, uint rgb)
            {
                Tri(ax, ay, az, bx, by, bz, cx, cy, cz, rgb);
                Tri(ax, ay, az, cx, cy, cz, dx, dy, dz, rgb);
            }

            /// <summary>A capsule prism of a solid (walls as slabs with their thickness, cylinders as 10-gons).</summary>
            public void Prim(in SolidPrim p, float floor, float cap, uint rgb)
            {
                float y0 = Math.Max(p.Bottom, floor), y1 = Math.Min(p.Top, cap);
                if (!(y1 > y0)) return;
                double ex = p.Bx - p.Ax, ez = p.Bz - p.Az, l = Math.Sqrt(ex * ex + ez * ez);
                if (l < 1e-6)
                {
                    const int n = 10;
                    for (int k = 0; k < n; k++)
                    {
                        double a0 = 2 * Math.PI * k / n, a1 = 2 * Math.PI * (k + 1) / n;
                        double x0 = p.Ax + p.Radius * Math.Cos(a0), z0 = p.Az + p.Radius * Math.Sin(a0);
                        double x1 = p.Ax + p.Radius * Math.Cos(a1), z1 = p.Az + p.Radius * Math.Sin(a1);
                        Quad(x0, y0, z0, x1, y0, z1, x1, y1, z1, x0, y1, z0, rgb);
                        Tri(p.Ax, y1, p.Az, x0, y1, z0, x1, y1, z1, rgb);
                    }
                    return;
                }
                double nx = -ez / l * Math.Max(p.Radius, 0.04f), nz = ex / l * Math.Max(p.Radius, 0.04f);
                Quad(p.Ax + nx, y0, p.Az + nz, p.Bx + nx, y0, p.Bz + nz, p.Bx + nx, y1, p.Bz + nz, p.Ax + nx, y1, p.Az + nz, rgb);
                Quad(p.Ax - nx, y0, p.Az - nz, p.Bx - nx, y0, p.Bz - nz, p.Bx - nx, y1, p.Bz - nz, p.Ax - nx, y1, p.Az - nz, rgb);
                Quad(p.Ax + nx, y1, p.Az + nz, p.Bx + nx, y1, p.Bz + nz, p.Bx - nx, y1, p.Bz - nz, p.Ax - nx, y1, p.Az - nz, Shade(rgb, 1.15));
            }

            /// <summary>A tile-local mesh (positions relative to the tile's south-west corner (x0, z0)).</summary>
            public void Mesh(MeshData m, double x0, double z0, Func<double, double, bool> keep)
            {
                for (int t = 0; t + 2 < m.IndexCount; t += 3)
                {
                    int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                    if (!keep(x0 + m.Positions[3 * a], z0 + m.Positions[3 * a + 2])) continue;
                    uint rgb = (uint)(m.Colors[4 * a] << 16 | m.Colors[4 * a + 1] << 8 | m.Colors[4 * a + 2]);
                    Tri(x0 + m.Positions[3 * a], m.Positions[3 * a + 1], z0 + m.Positions[3 * a + 2], x0 + m.Positions[3 * b], m.Positions[3 * b + 1],
                        z0 + m.Positions[3 * b + 2], x0 + m.Positions[3 * c], m.Positions[3 * c + 1], z0 + m.Positions[3 * c + 2], rgb);
                }
            }

            public void Save(string path, string header)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "# " + header + "\n" + _v + _f);
            }
        }

        private static uint Shade(uint rgb, double f)
        {
            uint r = (uint)Math.Min(255, ((rgb >> 16) & 255) * f), g = (uint)Math.Min(255, ((rgb >> 8) & 255) * f), b = (uint)Math.Min(255, (rgb & 255) * f);
            return r << 16 | g << 8 | b;
        }

        private static string Dir()
        {
            string d = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_DIR");
            return string.IsNullOrEmpty(d) ? null : d;
        }

        [Test]
        public void WritesColliderPreviews()
        {
            string dir = Dir();
            if (dir == null) Assert.Ignore("set GHUMANTE_PREVIEW_DIR to write the collider previews");

            // 1. Thamel: a 180 m window of the real tile, roads as drawn, footprint walls, trunks and poles.
            double cx, cz;
            DrivingData.ThamelMarg(out cx, out cz);
            TileData t = DrivingData.SampleTiles()[TileId.At(10, cx, cz)];
            var g = new TileGroundQuery();
            g.Add(t);
            var prims = new List<SolidPrim>();
            g.SolidsOf(t.Tile, prims);
            GroundSample s;
            g.TrySample(cx, cz, out s);
            var obj = new Obj(cx, s.Height, cz);
            const double half = 90;
            Func<double, double, bool> inWindow = (x, z) => Math.Abs(x - cx) < half && Math.Abs(z - cz) < half;
            var roads = new MeshData(65536, 196608);
            RoadMesher.Build(t, new TileHeightSampler(t, 1), new RoadOptions(), roads);
            obj.Mesh(roads, t.Tile.X0, t.Tile.Z0, inWindow);
            int walls = 0, posts = 0;
            foreach (SolidPrim p in prims)
            {
                if (!inWindow(p.Ax, p.Az)) continue;
                bool cyl = p.Ax == p.Bx && p.Az == p.Bz;
                if (cyl) posts++;
                else walls++;
                obj.Prim(p, s.Height - 1f, s.Height + 9f, cyl ? 0x2E7D32u : p.Group >= 0 ? 0xE07B39u : 0xC62828u);
            }
            obj.Save(Path.Combine(dir, "collide", "thamel_colliders.obj"), "Thamel solids: " + walls + " walls, " + posts + " cylinders");

            // 2. The synthetic flyover: deck strip, railings, and the road below.
            TileData f = DrivingLayeredGroundTests.Flyover();
            var fg = new TileGroundQuery();
            fg.Add(f);
            var fp = new List<SolidPrim>();
            fg.SolidsOf(f.Tile, fp);
            var fo = new Obj(f.Tile.X0 + 500, 1300, f.Tile.Z0 + 500);
            var froads = new MeshData(65536, 196608);
            RoadMesher.Build(f, new TileHeightSampler(f, 1), new RoadOptions(), froads);
            fo.Mesh(froads, f.Tile.X0, f.Tile.Z0, (x, z) => Math.Abs(x - f.Tile.X0 - 500) < 420 && Math.Abs(z - f.Tile.Z0 - 500) < 420);
            RoadStructureRecord st = f.RoadStructures[1];
            int[] pts = f.Roads[1].Points;
            for (int k = 0; k + 1 < f.Roads[1].PointCount; k++)
            {
                double ax, az, bx, bz;
                f.LocalToGame(pts[2 * k], pts[2 * k + 1], out ax, out az);
                f.LocalToGame(pts[2 * k + 2], pts[2 * k + 3], out bx, out bz);
                const double w = 5.0;
                fo.Quad(ax - w, st.DeckY[k], az, ax + w, st.DeckY[k], az, bx + w, st.DeckY[k + 1], bz, bx - w, st.DeckY[k + 1], bz, 0x9E9E9Eu);
                fo.Quad(ax - w, st.DeckY[k] - 1.2, az, ax + w, st.DeckY[k] - 1.2, az, bx + w, st.DeckY[k + 1] - 1.2, bz, bx - w, st.DeckY[k + 1] - 1.2, bz, 0x616161u);
            }
            foreach (SolidPrim p in fp) fo.Prim(p, 1290f, 1320f, 0xC62828u);
            fo.Save(Path.Combine(dir, "collide", "flyover_colliders.obj"), "flyover deck, railings: " + fp.Count);
            Assert.That(walls, Is.GreaterThan(100));
            Assert.That(fp.Count, Is.GreaterThan(4));
        }
    }
}
