using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// A showcase sheet of every Shapes primitive plus a few composed props (cartoon car, tree, railing, rock),
    /// for the visual self-check (docs/W2_DETAIL_CONTRACT.md §6). Always built and checked; written as OBJ only
    /// when GHUMANTE_SHAPES_OBJ names an output directory (AO is multiplied into the colours so the bake shows).
    /// </summary>
    public class MeshingShapesShowcase
    {
        private static ShapeBrush Br(uint hex, MaterialChannel ch = MaterialChannel.Paint)
        {
            return ShapeBrush.Hex(hex, ch);
        }

        internal static MeshData Sheet(ShapeLod lod)
        {
            var m = new MeshData(65536, 196608);
            double sp = 3.2;
            int col = 0, row = 0;
            Func<Affine3> next = () =>
            {
                Affine3 a = Affine3.Translation(col * sp, 0, -row * sp);
                if (++col == 6)
                {
                    col = 0;
                    row++;
                }
                return a;
            };
            var id = Affine3.Identity;
            // Row 0: boxes and superquadrics.
            Shapes.RoundedBox(m, next() * Affine3.Translation(0, 0.6, 0), Br(0xE0533D), 1.6, 1.2, 2.0, 0.25, 6, lod);
            Shapes.RoundedBox(m, next() * Affine3.Translation(0, 0.5, 0), Br(0xF2A541), 1.8, 1.0, 2.4, new BoxRadii(0.35, 0.35, 0.03, 0.5, 0.25, 0.7), 6, lod);
            Shapes.Superellipsoid(m, next() * Affine3.Translation(0, 0.8, 0), Br(0x3D9BE0), 1.2, 0.8, 0.9, 0.35, 0.45, 32, lod);
            Shapes.Superellipsoid(m, next() * Affine3.Translation(0, 1.0, 0), Br(0x8E5BD9), 0.9, 1.0, 0.9, 1.5, 1.3, 32, lod);
            Shapes.CubeSphere(m, next() * Affine3.Translation(0, 1.0, 0), Br(0x4CB86B), 1.0, 32, lod);
            Shapes.Sphere(m, next() * Affine3.Translation(0, 1.0, 0), Br(0x2FB5A8), 1.0, 32, lod);
            // Row 1: lathes.
            Shapes.Ellipsoid(m, next() * Affine3.Translation(0, 0.6, 0), Br(0xD94F8A), 1.3, 0.6, 0.9, 32, true, lod);
            Shapes.Capsule(m, next() * Affine3.RotationZ(0.35), Br(0xE07B39), 0.4, 2.0, 32, lod);
            Shapes.Cylinder(m, next(), Br(0x5E7CE0), 0.8, 1.2, 32, 0.15, 4, true, true, lod);
            Shapes.Cone(m, next(), Br(0xC9B037), 0.9, 1.8, 32, 0.12, 4, true, lod);
            Shapes.Frustum(m, next(), Br(0x9A6B4F, MaterialChannel.Wood), 1.0, 0.55, 1.0, 32, 0.12, 4, true, true, lod);
            Shapes.Dome(m, next(), Br(0xF4EDE0, MaterialChannel.Plaster), 1.1, 1.0, 32, true, lod);
            // Row 2: tori, lathe, extrudes.
            Shapes.Torus(m, next() * Affine3.Translation(0, 0.35, 0), Br(0x30343B, MaterialChannel.Rubber), 0.9, 0.32, 32, 16, lod);
            Shapes.Torus(m, next() * Affine3.Translation(0, 0.3, 0), Br(0xB33A3A), 1.0, 0.25, 32, 16, lod, -100, 200, true);
            Shapes.Lathe(m, next(), Br(0xB5651D, MaterialChannel.Dirt), MeshingShapesTests.Vase().Smooth(lod.Path(6)).Transform(1.6, 1.6, 0, 0), 32, lod, 20, 300, true);
            double[] lx = { -1, 1, 1, 0, 0, -1 }, lz = { -1, -1, 0, 0, 1, 1 };
            Shapes.BevelExtrude(m, next(), Br(0xA8A29A, MaterialChannel.Stone), lx, lz, 6, 0.6, 0.12, 4, BevelStyle.Round, true, true, 0.05, 30, lod);
            Shapes.BevelExtrude(m, next(), Br(0x7D8C99, MaterialChannel.Concrete), lx, lz, 6, 0.6, 0.12, 4, BevelStyle.Chamfer, true, true, 0.12, 30, lod);
            Shapes.RoundedSlab(m, next(), Br(0x6B4A2E, MaterialChannel.Wood), 2.2, 1.4, 0.35, 0.35, 0.1, 4, lod);
            // Row 3: sweeps, tubes, loft, wire.
            {
                Affine3 a = next();
                var ctrl = new Path3().Add(-1, 0.2, -0.8).Add(-0.3, 1.2, 0.2).Add(0.4, 0.6, 0.9).Add(1.0, 1.6, -0.5);
                Path3 sp1 = Curves.CatmullRom(ctrl, new Path3(), lod.Path(10));
                Shapes.Tube(m, a, Br(0x2E86AB, MaterialChannel.Metal), sp1, 0.12, 16, true, false, lod, 0.04);
            }
            {
                Affine3 a = next();
                var loop = Curves.ArcXZ(new Path3(), 0, 0.15, 0, 0.9, 0, 360, lod.Path(32));
                Shapes.Sweep(m, a, Br(0xE8C547, MaterialChannel.Gilt), loop, new Profile2().SetRoundedRect(0.3, 0.3, 0.1, lod.Bevel(3)), true, false,
                             SweepFrames.Upright);
            }
            {
                // Railing: mitred handrail along a bent line, posts instanced with CopyTransformed.
                Affine3 a = next();
                var rail = new Path3().Add(-1.3, 1.0, 0.8).Add(0, 1.0, 0.8).Add(1.0, 1.0, -0.6);
                Path3 r2 = Curves.Fillet(rail, new Path3(), 0.3, lod.Bevel(4));
                Shapes.Sweep(m, a, Br(0x3B3F46, MaterialChannel.Metal), r2, new Profile2().SetRoundedRect(0.12, 0.08, 0.03, lod.Bevel(3)), false, true,
                             SweepFrames.Upright);
                int f = m.VertexCount, fi = m.IndexCount;
                Shapes.Cylinder(m, a * Affine3.Translation(-1.3, 0, 0.8), Br(0x3B3F46, MaterialChannel.Metal), 0.04, 0.96, 10, 0.02, 2, true, true, lod);
                int nv = m.VertexCount - f, ni = m.IndexCount - fi;
                for (int i = 1; i < 5; i++)
                    Shapes.CopyTransformed(m, m, Affine3.Translation(0.4 * i - (i > 3 ? 0.45 * (i - 3) : 0), 0, i > 3 ? -0.55 * (i - 3) : 0), 0xFFFFFFFFu, f, nv, fi, ni);
            }
            Shapes.Loft(m, next(), Br(0x7A9E7E, MaterialChannel.Stone), new Profile2().SetRect(1.4, 1.4).FilletCorners(0.15, lod.Bevel(2)),
                        Affine3.RotationX(-Math.PI / 2), new Profile2().SetCircle(0.45, lod.Radial(16)),
                        Affine3.Translation(0, 1.8, 0) * Affine3.RotationX(-Math.PI / 2), 6, true, true, lod);
            {
                Affine3 a = next();
                Shapes.Cylinder(m, a * Affine3.Translation(-1.2, 0, 0), Br(0x6B4A2E, MaterialChannel.Wood), 0.08, 2.2, 8, 0, 0, true, true, lod);
                Shapes.Cylinder(m, a * Affine3.Translation(1.2, 0, 0), Br(0x6B4A2E, MaterialChannel.Wood), 0.08, 2.0, 8, 0, 0, true, true, lod);
                Shapes.Wire(m, a, Br(0x222222, MaterialChannel.Metal), -1.2, 2.1, 0, 1.2, 1.9, 0, 0.45, 0.025, 24, 5, lod);
            }
            {
                // Rock: displaced cube sphere.
                Affine3 a = next() * Affine3.Translation(0, 0.5, 0) * Affine3.Scaling(1.1, 0.7, 0.9);
                int f = m.VertexCount, fi = m.IndexCount;
                Shapes.CubeSphere(m, a, Br(0x8A8378, MaterialChannel.Stone), 1.0, 32, lod);
                ShapeNoise.Displace(m, f, m.VertexCount - f, 0.22, 1.3, 7, 3);
                ShapeNoise.RecomputeNormals(m, f, m.VertexCount - f, fi, m.IndexCount - fi);
                ShapeColor.JitterByPosition(m, f, m.VertexCount - f, 0.12f, 5, 0.3);
            }
            // Row 4: composed props.
            Car(m, next(), lod);
            Tree(m, next(), lod);
            Bike(m, next(), lod);
            Topi(m, next(), lod);
            ShapeAo.Bake(m, 0, -1, 0, -1, ShapeAo.Defaults(0));
            return m;
        }

        private static void Car(MeshData m, Affine3 a, ShapeLod lod)
        {
            var body = Br(0xD7263D);
            Shapes.Superellipsoid(m, a * Affine3.Translation(0, 0.62, 0), body, 0.85, 0.32, 1.6, 0.25, 0.3, 40, lod);
            Shapes.Superellipsoid(m, a * Affine3.Translation(0, 1.0, -0.15), Br(0xBFE3F2, MaterialChannel.Glass), 0.72, 0.3, 0.95, 0.35, 0.35, 32, lod);
            Shapes.RoundedBox(m, a * Affine3.Translation(0, 1.3, -0.15), body, 1.3, 0.08, 1.4, new BoxRadii(0.1, 0.1, 0.02, 0.04, 0.2, 0.2), 4, lod);
            for (int i = 0; i < 4; i++)
            {
                double x = i % 2 == 0 ? -0.78 : 0.78, z = i < 2 ? -1.0 : 1.0;
                Affine3 w = a * Affine3.Translation(x, 0.34, z) * Affine3.RotationZ(Math.PI / 2);
                Shapes.Torus(m, w, Br(0x23262B, MaterialChannel.Rubber), 0.24, 0.11, 24, 10, lod);
                Shapes.Cylinder(m, w * Affine3.Translation(0, -0.06, 0), Br(0xC8CDD2, MaterialChannel.Metal), 0.2, 0.12, 16, 0.03, 2, true, true, lod);
            }
            for (int i = 0; i < 2; i++)
                Shapes.Sphere(m, a * Affine3.Translation(i == 0 ? -0.5 : 0.5, 0.7, 1.55) * Affine3.Scaling(1, 1, 0.5), Br(0xFFF4C2, MaterialChannel.Glass), 0.13, 16, lod);
        }

        private static void Tree(MeshData m, Affine3 a, ShapeLod lod)
        {
            var trunk = new Path3().Add(0, 0, 0).Add(0.05, 0.8, 0.02).Add(-0.08, 1.6, 0.05).Add(0.02, 2.2, 0);
            Shapes.Tube(m, a, Br(0x7A5230, MaterialChannel.Bark), Curves.CatmullRom(trunk, new Path3(), lod.Path(4)), 0.22, 12, true, false, lod, 0.1);
            var branch = new Path3().Add(0, 1.4, 0).Add(0.4, 1.8, 0.1).Add(0.7, 2.0, 0.1);
            Shapes.Tube(m, a, Br(0x7A5230, MaterialChannel.Bark), Curves.CatmullRom(branch, new Path3(), lod.Path(4)), 0.08, 8, true, false, lod, 0.03);
            double[] cx = { 0, 0.55, -0.45, 0.1 }, cy = { 2.6, 2.3, 2.25, 3.05 }, cz = { 0, 0.15, -0.1, 0.2 }, cr = { 0.85, 0.6, 0.6, 0.55 };
            for (int i = 0; i < 4; i++)
            {
                int f = m.VertexCount, fi = m.IndexCount;
                Shapes.CubeSphere(m, a * Affine3.Translation(cx[i], cy[i], cz[i]), Br(0x3E8E41, MaterialChannel.Foliage), cr[i], 24, lod);
                ShapeNoise.Displace(m, f, m.VertexCount - f, 0.12 * cr[i], 2.2, (uint)(11 + i), 2);
                ShapeNoise.RecomputeNormals(m, f, m.VertexCount - f, fi, m.IndexCount - fi);
                ShapeColor.VerticalShade(m, f, m.VertexCount - f, cy[i] - cr[i], cy[i] + cr[i], 0.75f, 1.15f);
            }
        }

        private static void Bike(MeshData m, Affine3 a, ShapeLod lod)
        {
            var metal = Br(0x2B59C3, MaterialChannel.Paint);
            for (int i = 0; i < 2; i++)
            {
                Affine3 w = a * Affine3.Translation(0, 0.36, i == 0 ? -0.7 : 0.7) * Affine3.RotationZ(Math.PI / 2);
                Shapes.Torus(m, w, Br(0x1E1F22, MaterialChannel.Rubber), 0.3, 0.06, 24, 8, lod);
                Shapes.Cylinder(m, w * Affine3.Translation(0, -0.04, 0), Br(0xBFC5CC, MaterialChannel.Metal), 0.09, 0.08, 12, 0.02, 1, true, true, lod);
            }
            var frame = new Path3().Add(0, 0.36, -0.7).Add(0, 0.75, -0.2).Add(0, 0.8, 0.4).Add(0, 0.36, 0.7);
            Shapes.Tube(m, a, metal, Curves.Fillet(frame, new Path3(), 0.15, lod.Bevel(4)), 0.045, 10, true, false, lod);
            Shapes.Superellipsoid(m, a * Affine3.Translation(0, 0.9, -0.1), Br(0x2A2A2A, MaterialChannel.Leather), 0.16, 0.06, 0.3, 0.4, 0.6, 16, lod);
            var bar = new Path3().Add(-0.35, 1.05, 0.5).Add(-0.15, 1.0, 0.45).Add(0.15, 1.0, 0.45).Add(0.35, 1.05, 0.5);
            Shapes.Tube(m, a, Br(0xBFC5CC, MaterialChannel.Metal), Curves.CatmullRom(bar, new Path3(), lod.Path(4)), 0.025, 8, true, false, lod);
            Shapes.Bar(m, a, Br(0xBFC5CC, MaterialChannel.Metal), 0, 0.36, 0.7, 0, 1.0, 0.47, 0.03, 8, lod);
            for (int i = 0; i < 2; i++)
                Shapes.Capsule(m, a * Affine3.Translation(i == 0 ? -0.4 : 0.32, 1.06, 0.5) * Affine3.RotationZ(-Math.PI / 2), Br(0x111111, MaterialChannel.Rubber),
                               0.035, 0.12, 10, lod);
        }

        private static void Topi(MeshData m, Affine3 a, ShapeLod lod)
        {
            // Dhaka topi: a soft, slanted cap (front lower than back) built as a loft from an oval brim to a smaller,
            // tilted top, with a crown dome and a gold pin.
            var brim = new Profile2().SetEllipse(0.55, 0.45, lod.Radial(24));
            var top = new Profile2().SetEllipse(0.5, 0.38, lod.Radial(24));
            Affine3 fa = a * Affine3.Translation(0, 0.6, 0) * Affine3.RotationX(-Math.PI / 2);
            Affine3 fb = a * Affine3.Translation(0, 1.3, 0.05) * Affine3.RotationX(-Math.PI / 2 - 0.35);
            int f = m.VertexCount;
            Shapes.Loft(m, Affine3.Identity, Br(0xB23A48, MaterialChannel.Fabric), brim, fa, top, fb, 6, true, true, lod);
            ShapeColor.JitterByPosition(m, f, m.VertexCount - f, 0.18f, 3, 0.12);
            Shapes.Torus(m, a * Affine3.Translation(0, 0.62, 0) * Affine3.Scaling(1, 1, 0.82), Br(0x2B2B2B, MaterialChannel.Fabric), 0.56, 0.05, 32, 8, lod);
            Shapes.Sphere(m, a * Affine3.Translation(0.3, 1.1, -0.3), Br(0xE8C547, MaterialChannel.Gilt), 0.07, 12, lod);
        }

        [Test]
        public void ShowcaseIsWellFormedAndWritesObj()
        {
            for (int level = 0; level <= 2; level++)
            {
                MeshData m = Sheet(new ShapeLod(level));
                MeshingChecks.AssertWellFormed(m, "sheet LOD" + level);
                MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, -0.35, "sheet LOD" + level);
                string dir = Environment.GetEnvironmentVariable("GHUMANTE_SHAPES_OBJ");
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                    WriteObj(m, Path.Combine(dir, "shapes_lod" + level + ".obj"));
                    TestContext.Progress.WriteLine("LOD" + level + ": " + m.VertexCount + " vertices, " + m.TriangleCount + " triangles");
                }
            }
        }

        /// <summary>Minimal OBJ dump in the preview tool's convention (Unity z north → OBJ -z), AO baked into the
        /// vertex colour.</summary>
        internal static void WriteObj(MeshData m, string path)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.Append("# Ghumante shapes showcase\n");
            for (int v = 0; v < m.VertexCount; v++)
            {
                float ao = m.HasUv0 ? m.Uv0[2 * v + 1] : 1f;
                sb.AppendFormat(ci, "v {0:R} {1:R} {2:R} {3:0.####} {4:0.####} {5:0.####}\n", m.Positions[3 * v], m.Positions[3 * v + 1], -m.Positions[3 * v + 2],
                                m.Colors[4 * v] / 255f * ao, m.Colors[4 * v + 1] / 255f * ao, m.Colors[4 * v + 2] / 255f * ao);
            }
            for (int v = 0; v < m.VertexCount; v++)
                sb.AppendFormat(ci, "vn {0:0.#####} {1:0.#####} {2:0.#####}\n", m.Normals[3 * v], m.Normals[3 * v + 1], -m.Normals[3 * v + 2]);
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[3 * t] + 1, b = m.Indices[3 * t + 1] + 1, c = m.Indices[3 * t + 2] + 1;
                sb.Append("f ").Append(a).Append("//").Append(a).Append(' ').Append(b).Append("//").Append(b).Append(' ').Append(c).Append("//").Append(c).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}
