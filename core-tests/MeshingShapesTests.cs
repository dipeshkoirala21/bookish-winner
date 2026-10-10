using System;
using System.Collections.Generic;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The rounded-geometry library (Core/Meshing/Shapes): well-formed output, Unity winding against the
    /// normals, outward normals, watertight closed solids, LOD scaling, determinism and the helpers.</summary>
    public class MeshingShapesTests
    {
        internal delegate int Build(MeshData m, ShapeLod lod);

        internal static readonly ShapeBrush Paint = new ShapeBrush(MeshColor.FromHex(0xC0602A), MaterialChannel.Paint, 0.9f);

        private static double[] LX = { 0, 2, 2, 1, 1, 0 };
        private static double[] LZ = { 0, 0, 1, 1, 2, 2 };

        /// <summary>Every closed (watertight) primitive the library makes, with LOD0 recipes.</summary>
        internal static IEnumerable<KeyValuePair<string, Build>> ClosedShapes()
        {
            var id = Affine3.Identity;
            yield return P("RoundedBox", (m, l) => Shapes.RoundedBox(m, id, Paint, 1.2, 0.8, 2.0, 0.15, 8, l));
            yield return P("RoundedBoxRadii", (m, l) => Shapes.RoundedBox(m, id, Paint, 1.6, 1.0, 2.4, new BoxRadii(0.3, 0.3, 0.02, 0.45, 0.2, 0.6), 8, l));
            yield return P("Superellipsoid", (m, l) => Shapes.Superellipsoid(m, id, Paint, 1.0, 0.6, 0.8, 0.3, 0.5, 32, l));
            yield return P("SuperDiamond", (m, l) => Shapes.Superellipsoid(m, id, Paint, 0.8, 1.0, 0.8, 1.6, 1.4, 32, l));
            yield return P("CubeSphere", (m, l) => Shapes.CubeSphere(m, id, Paint, 1.0, 32, l));
            yield return P("SphereUV", (m, l) => Shapes.Sphere(m, id, Paint, 1.0, 32, l));
            yield return P("EllipsoidUV", (m, l) => Shapes.Ellipsoid(m, id, Paint, 1.2, 0.5, 0.8, 32, false, l));
            yield return P("Capsule", (m, l) => Shapes.Capsule(m, id, Paint, 0.3, 1.5, 32, l));
            yield return P("Cylinder", (m, l) => Shapes.Cylinder(m, id, Paint, 0.5, 1.0, 32, 0.1, 4, true, true, l));
            yield return P("Cone", (m, l) => Shapes.Cone(m, id, Paint, 0.5, 1.2, 32, 0.08, 4, true, l));
            yield return P("Frustum", (m, l) => Shapes.Frustum(m, id, Paint, 0.6, 0.3, 0.9, 32, 0.06, 4, true, true, l));
            yield return P("Torus", (m, l) => Shapes.Torus(m, id, Paint, 1.0, 0.25, 32, 16, l));
            yield return P("TorusPartial", (m, l) => Shapes.Torus(m, id, Paint, 1.0, 0.25, 32, 16, l, 0, 200, true));
            yield return P("LatheVaseCut", (m, l) => Shapes.Lathe(m, id, Paint, Vase(), 32, l, 30, 270, true));
            yield return P("LatheSmooth", (m, l) => Shapes.Lathe(m, id, Paint, Vase().Smooth(l.Path(6)), 32, l));
            yield return P("Dome", (m, l) => Shapes.Dome(m, id, Paint, 1.0, 0.8, 32, true, l));
            yield return P("BevelRound", (m, l) => Shapes.BevelExtrude(m, id, Paint, LX, LZ, 6, 0.5, 0.1, 4, BevelStyle.Round, true, true, 0.05, 30, l));
            yield return P("BevelChamfer", (m, l) => Shapes.BevelExtrude(m, id, Paint, LX, LZ, 6, 0.5, 0.1, 4, BevelStyle.Chamfer, true, true, 0.1, 30, l));
            yield return P("RoundedSlab", (m, l) => Shapes.RoundedSlab(m, id, Paint, 2.0, 1.2, 0.3, 0.3, 0.08, 4, l));
            yield return P("TubeCurve", (m, l) => Shapes.Tube(m, id, Paint, Spline(l), 0.08, 16, true, false, l));
            yield return P("TubeTaper", (m, l) => Shapes.Tube(m, id, Paint, Spline(l), 0.12, 16, true, false, l, 0.03));
            yield return P("TubeLoop", (m, l) => Shapes.Tube(m, id, Paint, Ring(l), 0.1, 16, false, true, l));
            yield return P("SweepRailMitred", (m, l) => Shapes.Sweep(m, id, Paint, Zigzag(), RailProfile(l), false, true, SweepFrames.Upright));
            yield return P("Loft", (m, l) => Shapes.Loft(m, id, Paint, new Profile2().SetRect(1, 1).FilletCorners(0.1, 2, false),
                                                           Affine3.RotationX(-Math.PI / 2),
                                                           new Profile2().SetCircle(0.4, 12), Affine3.Translation(0, 1.5, 0) * Affine3.RotationX(-Math.PI / 2),
                                                           6, true, true, l));
            yield return P("Bar", (m, l) => Shapes.Bar(m, id, Paint, 0, 0, 0, 1, 1.5, 0.5, 0.05, 12, l));
        }

        private static KeyValuePair<string, Build> P(string n, Build b)
        {
            return new KeyValuePair<string, Build>(n, b);
        }

        internal static Profile2 Vase()
        {
            var p = new Profile2();
            p.Add(0, 0, true).Add(0.35, 0, true).Add(0.5, 0.3).Add(0.45, 0.6).Add(0.22, 0.9).Add(0.2, 1.05).Add(0.3, 1.15, true).Add(0, 1.15, true);
            return p;
        }

        internal static Path3 Spline(ShapeLod lod)
        {
            var c = new Path3();
            c.Add(0, 0, 0).Add(0.5, 0.6, 0.2).Add(1.0, 0.4, 0.8).Add(1.6, 1.0, 1.0).Add(2.0, 0.8, 0.2);
            return Curves.CatmullRom(c, new Path3(), lod.Path(8));
        }

        internal static Path3 Ring(ShapeLod lod)
        {
            var c = new Path3();
            Curves.ArcXZ(c, 0, 0.5, 0, 0.8, 0, 360, lod.Path(24));
            return c; // last point repeats the first: the sweep drops it on a closed path
        }

        internal static Path3 Zigzag()
        {
            return new Path3().Add(0, 0, 0).Add(1, 0, 0).Add(1.5, 0.2, 0.8).Add(2.5, 0.2, 0.8);
        }

        internal static Profile2 RailProfile(ShapeLod lod)
        {
            return new Profile2().SetRoundedRect(0.12, 0.08, 0.03, lod.Bevel(3));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Weld positions (0.1 mm grid) and check every edge is used exactly twice, once in each direction
        /// (closed, consistently oriented 2-manifold). Returns the welded vertex count.</summary>
        internal static int AssertWatertight(MeshData m, string what)
        {
            var weld = new Dictionary<(long, long, long), int>();
            var id = new int[m.VertexCount];
            for (int v = 0; v < m.VertexCount; v++)
            {
                var k = (Q(m.Positions[3 * v]), Q(m.Positions[3 * v + 1]), Q(m.Positions[3 * v + 2]));
                int w;
                if (!weld.TryGetValue(k, out w))
                {
                    w = weld.Count;
                    weld.Add(k, w);
                }
                id[v] = w;
            }
            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t < m.TriangleCount; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = id[m.Indices[3 * t + k]], b = id[m.Indices[3 * t + (k + 1) % 3]];
                    if (a == b) Assert.Fail(what + ": triangle " + t + " has a collapsed edge");
                    edges.TryGetValue((a, b), out int c);
                    edges[(a, b)] = c + 1;
                }
            }
            foreach (var kv in edges)
            {
                if (kv.Value != 1) Assert.Fail(what + ": directed edge " + kv.Key + " used " + kv.Value + " times");
                if (!edges.ContainsKey((kv.Key.Item2, kv.Key.Item1))) Assert.Fail(what + ": edge " + kv.Key + " is a boundary (hole)");
            }
            return weld.Count;
        }

        private static long Q(float f)
        {
            return (long)Math.Round(f * 1e4);
        }

        /// <summary>Signed volume by the divergence theorem (positive when fronts face outward).</summary>
        internal static double Volume(MeshData m)
        {
            double v = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
                double ax = m.Positions[3 * a], ay = m.Positions[3 * a + 1], az = m.Positions[3 * a + 2];
                double bx = m.Positions[3 * b], by = m.Positions[3 * b + 1], bz = m.Positions[3 * b + 2];
                double cx = m.Positions[3 * c], cy = m.Positions[3 * c + 1], cz = m.Positions[3 * c + 2];
                v += ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
            }
            return v / 6;
        }

        private static MeshData Make(Build b, ShapeLod lod)
        {
            var m = new MeshData();
            b(m, lod);
            return m;
        }

        // ------------------------------------------------------------------ closed solids

        [Test]
        public void ClosedShapesAreWellFormedWatertightAndOutward()
        {
            foreach (var kv in ClosedShapes())
            {
                for (int level = 0; level <= 2; level++)
                {
                    string what = kv.Key + " LOD" + level;
                    MeshData m = Make(kv.Value, new ShapeLod(level));
                    Assert.That(m.TriangleCount, Is.GreaterThan(8), what);
                    MeshingChecks.AssertWellFormed(m, what);
                    MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.0, what);
                    AssertWatertight(m, what);
                    Assert.That(Volume(m), Is.GreaterThan(0), what + ": volume (fronts must face out)");
                    Assert.That(m.HasUv0, what);
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        Assert.That(m.Uv0[2 * v], Is.EqualTo((float)MaterialChannel.Paint), what);
                        Assert.That(m.Uv0[2 * v + 1], Is.EqualTo(0.9f), what);
                    }
                }
            }
        }

        [Test]
        public void ConvexShapesHaveOutwardNormals()
        {
            var id = Affine3.Identity;
            Build[] convex =
            {
                (m, l) => Shapes.RoundedBox(m, id, Paint, 1.2, 0.8, 2.0, 0.15, 8, l),
                (m, l) => Shapes.Superellipsoid(m, id, Paint, 1.0, 0.6, 0.8, 0.3, 0.5, 32, l),
                (m, l) => Shapes.Sphere(m, id, Paint, 1.0, 24, l),
                (m, l) => Shapes.Capsule(m, Affine3.Translation(0, -0.75, 0), Paint, 0.3, 1.5, 24, l),
                (m, l) => Shapes.Cylinder(m, Affine3.Translation(0, -0.5, 0), Paint, 0.5, 1.0, 24, 0.1, 3, true, true, l),
            };
            for (int i = 0; i < convex.Length; i++)
            {
                MeshData m = Make(convex[i], ShapeLod.Lod0);
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double d = m.Positions[3 * v] * m.Normals[3 * v] + m.Positions[3 * v + 1] * m.Normals[3 * v + 1] +
                               m.Positions[3 * v + 2] * m.Normals[3 * v + 2];
                    Assert.That(d, Is.GreaterThan(0), "shape " + i + " vertex " + v);
                }
            }
        }

        [Test]
        public void VolumesMatchTheAnalyticSolids()
        {
            var id = Affine3.Identity;
            double sphere = 4.0 / 3.0 * Math.PI;
            Assert.That(Volume(Make((m, l) => Shapes.CubeSphere(m, id, Paint, 1, 48, l), ShapeLod.Lod0)), Is.EqualTo(sphere).Within(0.02 * sphere));
            Assert.That(Volume(Make((m, l) => Shapes.Sphere(m, id, Paint, 1, 48, l), ShapeLod.Lod0)), Is.EqualTo(sphere).Within(0.03 * sphere));
            double cyl = Math.PI * 0.25 * 2;
            Assert.That(Volume(Make((m, l) => Shapes.Cylinder(m, id, Paint, 0.5, 2, 64, 0, 0, true, true, l), ShapeLod.Lod0)), Is.EqualTo(cyl).Within(0.01 * cyl));
            double torus = 2 * Math.PI * Math.PI * 1.0 * 0.25 * 0.25;
            Assert.That(Volume(Make((m, l) => Shapes.Torus(m, id, Paint, 1.0, 0.25, 64, 24, l), ShapeLod.Lod0)), Is.EqualTo(torus).Within(0.03 * torus));
            // A rounded box loses volume only at its edges and corners.
            double box = Volume(Make((m, l) => Shapes.RoundedBox(m, id, Paint, 2, 1, 3, 0.2, 8, l), ShapeLod.Lod0));
            double loss = (4 - Math.PI) * 0.04 * (2 + 1 + 3);
            Assert.That(box, Is.LessThan(6).And.GreaterThan(6 - loss - 0.01));
            // Extruded L: area 3, height 0.5, small bevel loss.
            double l3 = Volume(Make((m, l) => Shapes.BevelExtrude(m, id, Paint, LX, LZ, 6, 0.5, 0.05, 4, BevelStyle.Round, true, true, 0, 30, l), ShapeLod.Lod0));
            Assert.That(l3, Is.LessThan(1.5).And.GreaterThan(1.45));
            // Superellipsoid with e = 1 is the ellipsoid.
            double ell = 4.0 / 3.0 * Math.PI * 1.0 * 0.6 * 0.8;
            Assert.That(Volume(Make((m, l) => Shapes.Superellipsoid(m, id, Paint, 1, 0.6, 0.8, 1, 1, 48, l), ShapeLod.Lod0)), Is.EqualTo(ell).Within(0.02 * ell));
        }

        [Test]
        public void LodLevelsReduceTriangles()
        {
            foreach (var kv in ClosedShapes())
            {
                int t0 = Make(kv.Value, ShapeLod.Lod0).TriangleCount;
                int t1 = Make(kv.Value, ShapeLod.Lod1).TriangleCount;
                int t2 = Make(kv.Value, ShapeLod.Lod2).TriangleCount;
                if (kv.Key == "BevelChamfer") Assert.That(t1, Is.LessThanOrEqualTo(t0), kv.Key); // no segments to drop
                else Assert.That(t1, Is.LessThan(t0), kv.Key + " LOD1 vs LOD0");
                Assert.That(t2, Is.LessThanOrEqualTo(t1), kv.Key + " LOD2 vs LOD1");
                if (kv.Key != "BevelChamfer") Assert.That(t2, Is.LessThan(t0 / 2 + 8), kv.Key + " LOD2 is much cheaper");
            }
        }

        [Test]
        public void ExactCountsForSimpleRecipes()
        {
            var id = Affine3.Identity;
            // Capped cylinder, hard rims: per radial segment one bottom fan triangle, a side quad, one top fan triangle.
            MeshData c = Make((m, l) => Shapes.Cylinder(m, id, Paint, 0.5, 1, 16, 0, 0, true, true, l), ShapeLod.Lod0);
            Assert.That(c.TriangleCount, Is.EqualTo(16 * 4));
            Assert.That(Make((m, l) => Shapes.Cylinder(m, id, Paint, 0.5, 1, 16, 0, 0, true, true, l), ShapeLod.Lod1).TriangleCount, Is.EqualTo(8 * 4));
            // Rounded box: (2n + 1)^2 quads per face with n = ceil(segments / 2).
            Assert.That(Make((m, l) => Shapes.RoundedBox(m, id, Paint, 1, 1, 1, 0.1, 4, l), ShapeLod.Lod0).TriangleCount, Is.EqualTo(6 * 25 * 2));
            // Cube sphere: 6 faces of (segments / 4)^2 quads.
            Assert.That(Make((m, l) => Shapes.CubeSphere(m, id, Paint, 1, 32, l), ShapeLod.Lod0).TriangleCount, Is.EqualTo(6 * 64 * 2));
            Assert.That(ShapeLod.Lod1.Radial(32), Is.EqualTo(16));
            Assert.That(ShapeLod.Lod2.Radial(32), Is.EqualTo(8));
            Assert.That(ShapeLod.Lod2.Radial(12), Is.EqualTo(6));
            Assert.That(ShapeLod.Lod2.Bevel(3), Is.EqualTo(1));
            Assert.That(default(ShapeLod).Level, Is.EqualTo(0));
        }

        [Test]
        public void OpenShapesAreWellFormed()
        {
            var id = Affine3.Identity;
            Build[] open =
            {
                (m, l) => Shapes.Cylinder(m, id, Paint, 0.5, 1, 16, 0, 0, false, false, l),
                (m, l) => Shapes.Wire(m, id, Paint, 0, 6, 0, 20, 7, 3, 0.8, 0.02, 24, 4, l),
                (m, l) => Shapes.Sweep(m, id, Paint, Zigzag(), new Profile2().Clear(false).Add(-0.1, 0, true).Add(0, 0.05).Add(0.1, 0, true), false, false),
                (m, l) => Shapes.Lathe(m, id, Paint, new Profile2().Clear(false).Add(0.5, 0).Add(0.6, 0.5).Add(0.4, 1.0), 24, l),
            };
            for (int i = 0; i < open.Length; i++)
            {
                MeshData m = Make(open[i], ShapeLod.Lod0);
                Assert.That(m.TriangleCount, Is.GreaterThan(0), "open " + i);
                MeshingChecks.AssertWellFormed(m, "open " + i);
                MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.0, "open " + i);
            }
        }

        [Test]
        public void MirroredAndKitFrameTransformsKeepTheWinding()
        {
            Affine3[] xfs =
            {
                Affine3.Scaling(-1, 1, 1),
                Affine3.FromKitFrame(KitFrame.FromYaw(10, 2, -4, 37)),
                Affine3.Translation(3, 1, 2) * Affine3.RotationAxis(1, 2, 3, 0.7) * Affine3.Scaling(1.5, 0.5, 2),
            };
            foreach (Affine3 xf in xfs)
            {
                Affine3 x = xf;
                Build[] builds =
                {
                    (m, l) => Shapes.RoundedBox(m, x, Paint, 1, 0.5, 2, 0.1, 4, l),
                    (m, l) => Shapes.Torus(m, x, Paint, 1, 0.2, 24, 12, l, 0, 180, true),
                    (m, l) => Shapes.BevelExtrude(m, x, Paint, LX, LZ, 6, 0.5, 0.1, 3, BevelStyle.Round, true, true, 0, 30, l),
                    (m, l) => Shapes.Tube(m, x, Paint, Spline(l), 0.08, 12, true, false, l),
                };
                foreach (Build b in builds)
                {
                    MeshData m = Make(b, ShapeLod.Lod0);
                    MeshingChecks.AssertWellFormed(m, "xf");
                    MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.0, "xf");
                    AssertWatertight(m, "xf");
                    Assert.That(Volume(m), Is.GreaterThan(0));
                }
            }
        }

        [Test]
        public void OutputIsDeterministic()
        {
            foreach (var kv in ClosedShapes())
            {
                MeshData a = Make(kv.Value, ShapeLod.Lod1), b = Make(kv.Value, ShapeLod.Lod1);
                Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount), kv.Key);
                for (int i = 0; i < a.VertexCount * 3; i++)
                {
                    if (a.Positions[i] != b.Positions[i] || a.Normals[i] != b.Normals[i]) Assert.Fail(kv.Key + " differs at " + i);
                }
                for (int i = 0; i < a.IndexCount; i++) Assert.That(a.Indices[i], Is.EqualTo(b.Indices[i]), kv.Key);
            }
        }

        [Test]
        public void RepeatCallsDoNotAllocate()
        {
            var m = new MeshData(200000, 600000);
            var vase = Vase();
            var path = Spline(ShapeLod.Lod0);
            var rail = RailProfile(ShapeLod.Lod0);
            var id = Affine3.Identity;
            Action run = () =>
            {
                m.Clear();
                Shapes.RoundedBox(m, id, Paint, 1, 1, 1, 0.1, 4);
                Shapes.Superellipsoid(m, id, Paint, 1, 1, 1, 0.4, 0.4, 24);
                Shapes.Lathe(m, id, Paint, vase, 24);
                Shapes.Cylinder(m, id, Paint, 0.5, 1, 24, 0.05, 3);
                Shapes.Torus(m, id, Paint, 1, 0.2, 24, 12, default, 0, 120, true);
                Shapes.BevelExtrude(m, id, Paint, LX, LZ, 6, 0.5, 0.1, 3);
                Shapes.Tube(m, id, Paint, path, 0.05, 12);
                Shapes.Sweep(m, id, Paint, path, rail, false, true, SweepFrames.Upright);
                Shapes.CopyTransformed(m, m, Affine3.Translation(2, 0, 0), 0xFF8080FFu, 0, 100, 0, 0);
                ShapeAo.Bake(m, 0, -1, 0, -1, ShapeAo.Defaults());
            };
            for (int i = 0; i < 3; i++) run();
            long least = long.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                run();
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            Assert.That(least, Is.EqualTo(0), "bytes allocated by a warm repeat");
        }

        // ------------------------------------------------------------------ transforms and curves

        [Test]
        public void Affine3Basics()
        {
            double x, y, z;
            Affine3.Yaw(90).Point(0, 0, 1, out x, out y, out z);
            Assert.That(x, Is.EqualTo(1).Within(1e-12));
            Assert.That(z, Is.EqualTo(0).Within(1e-12));
            Affine3 a = Affine3.Translation(1, 2, 3) * Affine3.RotationAxis(1, 1, 0, 0.8) * Affine3.Scaling(2, 3, 0.5);
            (a.Inverse() * a).Point(0.3, -0.7, 1.1, out x, out y, out z);
            Assert.That(x, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(y, Is.EqualTo(-0.7).Within(1e-9));
            Assert.That(z, Is.EqualTo(1.1).Within(1e-9));
            // Then = reversed product.
            Affine3 t = Affine3.Scaling(2).Then(Affine3.Translation(1, 0, 0));
            t.Point(1, 0, 0, out x, out y, out z);
            Assert.That(x, Is.EqualTo(3).Within(1e-12));
            // Normals stay perpendicular to transformed tangents under non-uniform scale.
            Affine3 s = Affine3.Scaling(3, 1, 1);
            double nx, ny, nz, tx, ty, tz;
            s.Normal(1, 1, 0, out nx, out ny, out nz);
            s.Vector(1, -1, 0, out tx, out ty, out tz);
            Assert.That(nx * tx + ny * ty + nz * tz, Is.EqualTo(0).Within(1e-12));
            Assert.That(Affine3.Scaling(-1, 1, 1).Mirrors, Is.True);
            double len;
            Affine3 al = Affine3.Along(1, 1, 1, 2, 3, 1, out len);
            al.Point(0, len, 0, out x, out y, out z);
            Assert.That(x, Is.EqualTo(2).Within(1e-9));
            Assert.That(y, Is.EqualTo(3).Within(1e-9));
            Assert.That(z, Is.EqualTo(1).Within(1e-9));
            Assert.That(al.Determinant, Is.EqualTo(1).Within(1e-9));
            // KitFrame mapping.
            KitFrame f = KitFrame.FromYaw(5, 1, 7, 30);
            double kx, ky, kz;
            f.ToWorld(0.4, 1.2, -0.6, out kx, out ky, out kz);
            Affine3.FromKitFrame(f).Point(0.4, 1.2, -0.6, out x, out y, out z);
            Assert.That(x, Is.EqualTo(kx).Within(1e-12));
            Assert.That(y, Is.EqualTo(ky).Within(1e-12));
            Assert.That(z, Is.EqualTo(kz).Within(1e-12));
        }

        [Test]
        public void CurvesBehave()
        {
            var c = new Path3().Add(0, 0, 0).Add(1, 0, 0).Add(1, 1, 0).Add(2, 1, 1);
            Path3 cr = Curves.CatmullRom(c, new Path3(), 6);
            Assert.That(cr.Count, Is.EqualTo(3 * 6 + 1));
            for (int i = 0; i < c.Count; i++)
            {
                int k = i * 6;
                Assert.That(cr.X[k], Is.EqualTo(c.X[i]).Within(1e-9));
                Assert.That(cr.Y[k], Is.EqualTo(c.Y[i]).Within(1e-9));
                Assert.That(cr.Z[k], Is.EqualTo(c.Z[i]).Within(1e-9));
            }
            Path3 closed = Curves.CatmullRom(c, new Path3(), 4, true);
            Assert.That(closed.Count, Is.EqualTo(4 * 4));
            Path3 bz = Curves.Bezier(new Path3(), 0, 0, 0, 1, 2, 0, 2, 2, 0, 3, 0, 0, 10);
            Assert.That(bz.Count, Is.EqualTo(11));
            Assert.That(bz.X[10], Is.EqualTo(3));
            Assert.That(bz.Y[5], Is.EqualTo(1.5).Within(1e-9));
            // Fillet: corner (1,0,0) replaced by an arc that stays within the corner's hull.
            Path3 fl = Curves.Fillet(new Path3().Add(0, 0, 0).Add(1, 0, 0).Add(1, 0, 1), new Path3(), 0.3, 4);
            Assert.That(fl.Count, Is.EqualTo(2 + 5));
            for (int i = 1; i < fl.Count - 1; i++)
            {
                double dx = fl.X[i] - 0.7, dz = fl.Z[i] - 0.3;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.EqualTo(0.3).Within(1e-9), "on the arc");
            }
            // Catenary: exact ends, sag below the chord at mid-span.
            Path3 w = Curves.Catenary(new Path3(), 0, 10, 0, 20, 12, 0, 1.5, 20);
            Assert.That(w.Count, Is.EqualTo(21));
            Assert.That(w.Y[0], Is.EqualTo(10).Within(1e-9));
            Assert.That(w.Y[20], Is.EqualTo(12).Within(1e-9));
            Assert.That(w.Y[10], Is.EqualTo(11 - 1.5).Within(1e-6));
            for (int i = 1; i < 20; i++) Assert.That(w.Y[i], Is.LessThan(10 + 2.0 * i / 20), "below the chord");
            // Resample: equal spacing.
            Path3 rs = Curves.Resample(new Path3().Add(0, 0, 0).Add(10, 0, 0), new Path3(), 1);
            Assert.That(rs.Count, Is.EqualTo(11));
            Assert.That(rs.X[3], Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void ProfileBuilders()
        {
            var p = new Profile2().SetRoundedRect(2, 1, 0.2, 3);
            Assert.That(p.Count, Is.EqualTo(4 * 4));
            Assert.That(p.SignedArea(), Is.GreaterThan(0));
            double area = 2 * 1 - (4 - Math.PI) * 0.04;
            Assert.That(p.SignedArea(), Is.EqualTo(area).Within(0.02));
            for (int i = 0; i < p.Count; i++)
            {
                Assert.That(Math.Abs(p.X[i]), Is.LessThanOrEqualTo(1 + 1e-9));
                Assert.That(Math.Abs(p.Y[i]), Is.LessThanOrEqualTo(0.5 + 1e-9));
                // Explicit normals point out of the rectangle's centre.
                Assert.That(p.NX[i] * p.X[i] + p.NY[i] * p.Y[i], Is.GreaterThan(0));
            }
            // Smooth: passes through every control point, keeps creases sharp, adds points between.
            Profile2 v = Vase();
            int before = v.Count;
            v.Smooth(4);
            Assert.That(v.Count, Is.GreaterThan(2 * before));
            Profile2 raw = Vase();
            for (int i = 0; i < raw.Count; i++)
            {
                bool found = false;
                for (int k = 0; k < v.Count; k++) found |= Math.Abs(v.X[k] - raw.X[i]) < 1e-9 && Math.Abs(v.Y[k] - raw.Y[i]) < 1e-9 && v.Crease[k] == raw.Crease[i];
                Assert.That(found, "control point " + i);
            }
            Profile2 blob = new Profile2().SetRegular(1, 5).Smooth(3);
            Assert.That(blob.Count, Is.EqualTo(5), "all creased: unchanged");
            Profile2 loop = new Profile2().Clear(true).Add(1, 0).Add(0, 1).Add(-1, 0).Add(0, -1).Smooth(4);
            Assert.That(loop.Count, Is.EqualTo(16));
            Assert.That(loop.SignedArea(), Is.GreaterThan(2.0));
            var cw = new Profile2().SetRect(1, 1).Reverse().FilletCorners(0.2, 2);
            Assert.That(cw.SignedArea(), Is.LessThan(0));
            for (int i = 0; i < cw.Count; i++) Assert.That(cw.NX[i] * cw.X[i] + cw.NY[i] * cw.Y[i], Is.GreaterThan(0));
        }

        // ------------------------------------------------------------------ instancing, colour, AO, noise

        [Test]
        public void CopyTransformedInstancesInPlace()
        {
            var m = new MeshData();
            int f = Shapes.Cylinder(m, Affine3.Identity, Paint, 0.05, 1, 8, 0, 0, true, true);
            int nv = m.VertexCount, ni = m.IndexCount;
            for (int i = 1; i <= 4; i++) Shapes.CopyTransformed(m, m, Affine3.Translation(i * 0.5, 0, 0), 0x808080FFu, f, nv, 0, ni);
            Shapes.CopyTransformed(m, m, Affine3.Scaling(-1, 1, 1) * Affine3.Translation(1, 0, 0), 0xFFFFFFFFu, f, nv, 0, ni);
            Assert.That(m.VertexCount, Is.EqualTo(6 * nv));
            Assert.That(m.IndexCount, Is.EqualTo(6 * ni));
            MeshingChecks.AssertWellFormed(m, "copies");
            MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.0, "copies");
            Assert.That(m.Positions[3 * nv], Is.EqualTo(m.Positions[0] + 0.5f).Within(1e-5));
            Assert.That(m.Colors[4 * nv], Is.EqualTo(m.Colors[0] * 128 / 255));
            Assert.That(m.Uv0[2 * nv + 1], Is.EqualTo(0.9f));
            Assert.That(Volume(m), Is.GreaterThan(0));
        }

        [Test]
        public void ColourHelpers()
        {
            var m = new MeshData();
            Shapes.Cylinder(m, Affine3.Identity, Paint, 0.5, 2, 16, 0, 0, true, true);
            ShapeColor.VerticalGradient(m, 0, m.VertexCount, 0, 2, 0x000000FFu, 0xFFFFFFFFu);
            for (int v = 0; v < m.VertexCount; v++)
            {
                float y = m.Positions[3 * v + 1];
                Assert.That(m.Colors[4 * v], Is.EqualTo((int)(255 * y / 2 + 0.5f)).Within(1), "gradient");
                Assert.That(m.Colors[4 * v + 3], Is.EqualTo(255));
            }
            var a = new MeshData();
            var b = new MeshData();
            Shapes.CubeSphere(a, Affine3.Identity, Paint, 1, 16);
            Shapes.CubeSphere(b, Affine3.Identity, Paint, 1, 16);
            ShapeColor.JitterByPosition(a, 0, a.VertexCount, 0.2f, 7, 0.3);
            ShapeColor.JitterByPosition(b, 0, b.VertexCount, 0.2f, 7, 0.3);
            Assert.That(a.Colors, Is.EqualTo(b.Colors), "deterministic");
            bool varied = false;
            for (int v = 1; v < a.VertexCount; v++) varied |= a.Colors[4 * v] != a.Colors[0];
            Assert.That(varied);
            // Coincident seam vertices agree.
            for (int v = 0; v < a.VertexCount; v++)
            {
                for (int w = v + 1; w < a.VertexCount; w++)
                {
                    if (a.Positions[3 * v] == a.Positions[3 * w] && a.Positions[3 * v + 1] == a.Positions[3 * w + 1] &&
                        a.Positions[3 * v + 2] == a.Positions[3 * w + 2]) Assert.That(a.Colors[4 * v], Is.EqualTo(a.Colors[4 * w]));
                }
            }
            var box = new MeshData();
            MeshKit.Box(box, KitFrame.FromYaw(0, 0, 0, 0), -1, 1, 0, 1, -1, 1, MeshColor.FromHex(0x808080));
            ShapeColor.JitterFaces(box, 0, box.IndexCount, 0.3f, 3);
            int distinct = 0;
            for (int face = 1; face < 6; face++) distinct += box.Colors[16 * face] != box.Colors[0] ? 1 : 0;
            Assert.That(distinct, Is.GreaterThan(2));
            // A quad's two triangles share their vertices' colour (applied once per vertex).
            Assert.That(box.Colors[4], Is.EqualTo(box.Colors[8]));
        }

        [Test]
        public void AoDarkensGroundUndersidesAndCreases()
        {
            var m = new MeshData();
            Shapes.RoundedBox(m, Affine3.Translation(0, 0.5, 0), new ShapeBrush(MeshColor.FromHex(0xFFFFFF)), 1, 1, 1, 0.1, 4);
            ShapeAo.Bake(m, 0, -1, 0, -1, ShapeAo.Defaults());
            double top = 1, bottom = 1;
            for (int v = 0; v < m.VertexCount; v++)
            {
                float ao = m.Uv0[2 * v + 1];
                Assert.That(ao, Is.InRange(0f, 1f));
                if (m.Positions[3 * v + 1] > 0.99f) top = Math.Min(top, ao);
                if (m.Positions[3 * v + 1] < 0.01f) bottom = Math.Min(bottom, ao);
            }
            Assert.That(top, Is.GreaterThan(0.9));
            Assert.That(bottom, Is.LessThan(0.6));
            // A slab hovering over a block shadows the block's top with the ray term.
            var r = new MeshData();
            Shapes.RoundedBox(r, Affine3.Translation(0, 0.25, 0), new ShapeBrush(0xFFFFFFFFu), 1, 0.5, 1, 0.05, 2);
            int blockVerts = r.VertexCount;
            Shapes.RoundedBox(r, Affine3.Translation(0.5, 0.75, 0), new ShapeBrush(0xFFFFFFFFu), 1, 0.1, 1.4, 0.03, 2);
            AoSettings s = ShapeAo.Defaults();
            s.GroundFade = 0;
            s.Concavity = 0;
            s.Rays = 12;
            s.RayDistance = 1;
            ShapeAo.Bake(r, 0, -1, 0, -1, s);
            double covered = 0, open = 0;
            int nc = 0, no = 0;
            for (int v = 0; v < blockVerts; v++)
            {
                if (r.Positions[3 * v + 1] < 0.49f || r.Normals[3 * v + 1] < 0.99f) continue;
                if (r.Positions[3 * v] > 0.2f)
                {
                    covered += r.Uv0[2 * v + 1];
                    nc++;
                }
                else if (r.Positions[3 * v] < -0.2f)
                {
                    open += r.Uv0[2 * v + 1];
                    no++;
                }
            }
            Assert.That(nc, Is.GreaterThan(0));
            Assert.That(no, Is.GreaterThan(0));
            Assert.That(covered / nc, Is.LessThan(open / no - 0.1));
            // Bake on a mesh without UVs turns them on as Plain / open first.
            var plain = new MeshData();
            MeshKit.Box(plain, KitFrame.FromYaw(0, 0, 0, 0), -1, 1, 0, 1, -1, 1, 0xFFFFFFFFu);
            ShapeAo.Bake(plain, 0, -1, 0, -1, ShapeAo.Defaults());
            Assert.That(plain.HasUv0);
            Assert.That(plain.Uv0[0], Is.EqualTo(0f));
        }

        [Test]
        public void NoiseIsDeterministicBoundedAndSmooth()
        {
            double maxStep = 0;
            for (int i = 0; i < 2000; i++)
            {
                double x = i * 0.137, y = i * 0.071 - 3, z = i * 0.013 + 1;
                double v = ShapeNoise.Value3(x, y, z, 11), g = ShapeNoise.Gradient3(x, y, z, 11), f = ShapeNoise.Fbm3(x, y, z, 11, 4);
                Assert.That(v, Is.InRange(-1.0, 1.0));
                Assert.That(g, Is.InRange(-1.0, 1.0));
                Assert.That(f, Is.InRange(-1.0, 1.0));
                Assert.That(ShapeNoise.Gradient3(x, y, z, 11), Is.EqualTo(g));
                maxStep = Math.Max(maxStep, Math.Abs(ShapeNoise.Gradient3(x + 1e-3, y, z, 11) - g));
            }
            Assert.That(maxStep, Is.LessThan(0.01), "continuous");
            Assert.That(ShapeNoise.Value3(0.5, 0.5, 0.5, 1), Is.Not.EqualTo(ShapeNoise.Value3(0.5, 0.5, 0.5, 2)));
            Assert.That(ShapeNoise.Hash(1, 2, 3, 4), Is.EqualTo(ShapeNoise.Hash(1, 2, 3, 4)));
            Assert.That(ShapeNoise.Hash(1, 2, 3, 4), Is.Not.EqualTo(ShapeNoise.Hash(1, 2, 4, 4)));
        }

        [Test]
        public void DisplacedRockStaysClosed()
        {
            var m = new MeshData();
            Shapes.CubeSphere(m, Affine3.Scaling(1.2, 0.8, 1.0), Paint, 1, 32);
            ShapeNoise.Displace(m, 0, m.VertexCount, 0.18, 1.7, 99, 3);
            ShapeNoise.RecomputeNormals(m, 0, m.VertexCount, 0, m.IndexCount);
            MeshingChecks.AssertWellFormed(m, "rock");
            MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, -0.2, "rock");
            AssertWatertight(m, "rock");
            Assert.That(Volume(m), Is.GreaterThan(0));
        }
    }
}
