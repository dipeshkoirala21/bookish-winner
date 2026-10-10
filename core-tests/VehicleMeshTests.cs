using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The detailed procedural vehicles (docs/research/w2/ref_vehicles.md, docs/W2_DETAIL_CONTRACT.md): every catalogue
    /// entry × model type × livery × level stays inside its family budget and the catalogue box, writes UV0 with valid
    /// material channels and AO, is deterministic, and picks its model types by the street weights. With
    /// <c>GHUMANTE_PREVIEW_DIR</c> set, <see cref="DumpVehiclePreviews"/> writes OBJs of every model for the preview tool.
    /// </summary>
    [TestFixture]
    public class VehicleMeshTests
    {
        private static readonly VehicleLod[] Moving = { VehicleLod.Lod0, VehicleLod.Lod1, VehicleLod.Lod2 };

        private static int Assemble(int v, byte livery, byte model, VehicleLod lod, MeshData m, uint seed, bool rider)
        {
            int tris = VehicleMesher.Build(v, livery, model, lod, m, seed, rider);
            if (lod <= VehicleLod.Lod1)
                foreach (WheelSocket w in VehicleMesher.Wheels(VehicleCatalog.At(v), model))
                    tris += VehicleMesher.BuildWheel(w, lod, new MeshData());
            return tris;
        }

        [Test]
        public void EveryModelStaysWithinItsFamilyBudget()
        {
            var m = new MeshData(8192, 24576);
            var report = new List<string>();
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    foreach (VehicleLod lod in Moving)
                    {
                        int worst = 0;
                        for (int l = 0; l < e.LiveryCount; l++)
                        {
                            m.Clear();
                            int tris = Assemble(v, (byte)l, (byte)k, lod, m, 77u, true);
                            worst = Math.Max(worst, tris);
                            Assert.That(tris, Is.GreaterThan(0), e.AssetId);
                            Assert.That(tris, Is.LessThanOrEqualTo(VehicleMesher.Budget(lod, e.Shape)),
                                        e.AssetId + " " + VehicleMesher.ModelName(v, k) + " " + lod + " livery " + l);
                            Assert.That(tris, Is.LessThanOrEqualTo(VehicleMesher.Budget(lod)));
                        }
                        report.Add(e.AssetId + "/" + VehicleMesher.ModelName(v, k) + " " + lod + ": " + worst);
                    }
                }
                foreach (VehicleLod lod in new[] { VehicleLod.Block, VehicleLod.Box })
                {
                    m.Clear();
                    int tris = VehicleMesher.Build(v, 0, lod, m, 5u);
                    Assert.That(tris, Is.InRange(1, VehicleMesher.Budget(lod)), e.AssetId + " " + lod);
                }
            }
            TestContext.WriteLine(string.Join("\n", report));
        }

        [Test]
        public void ModelsFitTheCatalogueBoxAndStandOnTheGround()
        {
            var m = new MeshData(8192, 24576);
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    m.Clear();
                    VehicleMesher.Build(v, 0, (byte)k, VehicleLod.Lod0, m, 5u);
                    float minX, minY, minZ, maxX, maxY, maxZ;
                    m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                    string id = e.AssetId + "/" + VehicleMesher.ModelName(v, k);
                    Assert.That(maxZ - minZ, Is.EqualTo(e.LengthM).Within(e.LengthM * 0.12f + 0.15f), id + " length");
                    Assert.That(maxX - minX, Is.LessThanOrEqualTo(e.WidthM * 1.12f + 0.05f), id + " width");
                    Assert.That(maxY, Is.LessThanOrEqualTo(e.HeightM * 1.15f + 0.05f), id + " height");
                    Assert.That(minY, Is.GreaterThanOrEqualTo(-0.01f), id + " above the ground");
                }
            }
        }

        [Test]
        public void MeshesCarryMaterialChannelsAndAo()
        {
            var m = new MeshData(8192, 24576);
            int channels = Enum.GetValues(typeof(MaterialChannel)).Length;
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                foreach (VehicleLod lod in new[] { VehicleLod.Lod0, VehicleLod.Lod2, VehicleLod.Block })
                {
                    m.Clear();
                    VehicleMesher.Build(v, 0, 0, lod, m, 9u, true);
                    Assert.That(m.HasUv0, Is.True, VehicleCatalog.At(v).AssetId + " " + lod);
                    var seen = new HashSet<int>();
                    for (int i = 0; i < m.VertexCount; i++)
                    {
                        float u = m.Uv0[2 * i], ao = m.Uv0[2 * i + 1];
                        Assert.That(u, Is.EqualTo((float)Math.Round(u)).And.InRange(0f, channels - 1f));
                        Assert.That(ao, Is.InRange(0f, 1f));
                        seen.Add((int)u);
                    }
                    if (lod == VehicleLod.Lod0)
                    {
                        Assert.That(seen.Count, Is.GreaterThanOrEqualTo(4), VehicleCatalog.At(v).AssetId + " uses several materials");
                        Assert.That(seen.Contains((int)MaterialChannel.Paint), Is.True);
                    }
                }
                foreach (WheelSocket w in VehicleMesher.Wheels(VehicleCatalog.At(v)))
                {
                    m.Clear();
                    VehicleMesher.BuildWheel(w, VehicleLod.Lod0, m);
                    Assert.That(m.HasUv0, Is.True);
                    float minX, minY, minZ, maxX, maxY, maxZ;
                    m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                    Assert.That(maxY, Is.EqualTo(w.Radius).Within(0.06f), "wheel radius");
                    // Hubs, drums and discs of two-wheelers stand proud of their slim tyres.
                    Assert.That(maxX - minX, Is.LessThanOrEqualTo(Math.Max(w.Width + 0.08f, 0.22f)), "wheel width");
                }
            }
        }

        [Test]
        public void BuildsAreDeterministic()
        {
            var a = new MeshData(8192, 24576);
            var b = new MeshData(8192, 24576);
            for (int v = 0; v < VehicleCatalog.Count; v++)
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    a.Clear();
                    b.Clear();
                    VehicleMesher.Build(v, 1, (byte)k, VehicleLod.Lod0, a, 1234u, true);
                    VehicleMesher.Build(v, 1, (byte)k, VehicleLod.Lod0, b, 1234u, true);
                    Assert.That(b.VertexCount, Is.EqualTo(a.VertexCount));
                    Assert.That(b.IndexCount, Is.EqualTo(a.IndexCount));
                    for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(b.Positions[i], Is.EqualTo(a.Positions[i]));
                    for (int i = 0; i < a.IndexCount; i++) Assert.That(b.Indices[i], Is.EqualTo(a.Indices[i]));
                }
        }

        [Test]
        public void ModelTypesFollowTheStreetWeights()
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                int n = VehicleMesher.ModelCount(v);
                Assert.That(n, Is.GreaterThanOrEqualTo(1));
                var counts = new int[n];
                for (uint s = 0; s < 4000; s++)
                {
                    byte k = VehicleMesher.ModelFor(v, s * 2654435761u);
                    Assert.That(k, Is.LessThan(n));
                    counts[k]++;
                }
                for (int k = 0; k < n; k++) Assert.That(counts[k], Is.GreaterThan(4000 / n / 3), VehicleCatalog.At(v).AssetId + " model " + k + " is drawn");
                Assert.That(VehicleMesher.ModelFor(v, 42u), Is.EqualTo(VehicleMesher.ModelFor(v, 42u)));
            }
            // The commuter motorbike comes in its three street types.
            Assert.That(VehicleMesher.ModelCount(VehicleCatalog.MotorbikeCommuter), Is.EqualTo(3));
            Assert.That(VehicleMesher.ModelName(VehicleCatalog.MotorbikeCommuter, 0), Is.EqualTo("pulsar"));
        }

        [Test]
        public void TwoWheelersHaveSpinnableWheelsAndPlatesWithDevanagariNumbers()
        {
            var m = new MeshData(8192, 24576);
            WheelSocket[] w = VehicleMesher.Wheels(VehicleCatalog.At(VehicleCatalog.MotorbikeCommuter));
            Assert.That(w.Length, Is.EqualTo(2));
            Assert.That(w[1].Steers, Is.True);
            Assert.That(w[0].Style, Is.EqualTo(WheelStyle.MotoAlloy));
            Assert.That(VehicleMesher.StyleFor(w[0].Radius, w[0].Width), Is.EqualTo(WheelStyle.MotoAlloy));
            // LOD0 numbers the plate (more triangles than the blank LOD1 plate).
            m.Clear();
            int lod0 = VehicleMesher.Plate(m, VehiclePlates.For(VehicleCatalog.At(VehicleCatalog.MotorbikeCommuter), 3u), -0.4f, 0.6f, false, 0.2f, 0.13f,
                                           true, true);
            m.Clear();
            int blank = VehicleMesher.Plate(m, VehiclePlates.For(VehicleCatalog.At(VehicleCatalog.MotorbikeCommuter), 3u), -0.4f, 0.6f, false, 0.2f, 0.13f,
                                            true, false);
            Assert.That(lod0, Is.GreaterThan(blank + 20));
        }

        /// <summary>
        /// A wheel's style reads back from its size alone (<see cref="VehicleMesher.StyleFor"/>, used by callers that only
        /// know the radius and width): over every entry and model type no two sockets of one size disagree, so a borrowed
        /// EV keeps its alloys and the police jeep its steel wheels.
        /// </summary>
        [Test]
        public void WheelStyleReadsBackFromEverySocketSize()
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                    foreach (WheelSocket s in VehicleMesher.Wheels(e, k))
                        Assert.That(VehicleMesher.StyleFor(s.Radius, s.Width), Is.EqualTo(s.Style),
                                    e.AssetId + "/" + VehicleMesher.ModelName(v, k) + " r " + s.Radius + " w " + s.Width);
            }
            // The overload without a model type is the first model type's.
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                WheelSocket[] a = VehicleMesher.Wheels(VehicleCatalog.At(v)), b = VehicleMesher.Wheels(VehicleCatalog.At(v), 0);
                Assert.That(a.Length, Is.EqualTo(b.Length));
                for (int i = 0; i < a.Length; i++) Assert.That(a[i].X, Is.EqualTo(b[i].X));
            }
        }

        /// <summary>
        /// Car wheels sit under their own model type's body: the tyre's outer face is within 3 cm of the body side (no
        /// tyre poking out of the narrow Alto type, none sunk deep inside the wide Prado type).
        /// </summary>
        [Test]
        public void CarWheelsSitFlushWithTheirModelBody()
        {
            var m = new MeshData(8192, 24576);
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                if (e.Shape != BodyShape.Hatchback && e.Shape != BodyShape.Suv && e.Shape != BodyShape.Pickup) continue;
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    m.Clear();
                    VehicleMesher.Build(v, 0, (byte)k, VehicleLod.Lod1, m, 5u);
                    // Body half width at the wheel centre height over the front axle, mirrors excluded (below the belt).
                    WheelSocket[] ws = VehicleMesher.Wheels(e, k);
                    WheelSocket f = ws[ws.Length - 1];
                    float half = 0;
                    for (int i = 0; i < m.VertexCount; i++)
                    {
                        float y = m.Positions[3 * i + 1], z = m.Positions[3 * i + 2];
                        if (y > f.Radius + 0.35f || Math.Abs(z - f.Z) > 0.9f) continue;
                        half = Math.Max(half, Math.Abs(m.Positions[3 * i]));
                    }
                    float outer = Math.Abs(f.X) + 0.5f * f.Width;
                    string id = e.AssetId + "/" + VehicleMesher.ModelName(v, k);
                    Assert.That(outer, Is.LessThanOrEqualTo(half + 0.03f), id + " tyre inside the body");
                    Assert.That(outer, Is.GreaterThanOrEqualTo(half - 0.08f), id + " tyre near the body side");
                }
            }
        }

        /// <summary>Highest point of the mesh on the vertical line through (x, z), or NaN when the line misses it.</summary>
        internal static float TopAt(MeshData m, float x, float z)
        {
            float best = float.NaN;
            float[] p = m.Positions;
            for (int t = 0; t < m.IndexCount; t += 3)
            {
                int a = m.Indices[t] * 3, b = m.Indices[t + 1] * 3, c = m.Indices[t + 2] * 3;
                float ax = p[a], az = p[a + 2], bx = p[b], bz = p[b + 2], cx = p[c], cz = p[c + 2];
                float d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (Math.Abs(d) < 1e-9f) continue;
                float u = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / d, v = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / d, w = 1 - u - v;
                if (u < -1e-4f || v < -1e-4f || w < -1e-4f) continue;
                float y = u * p[a + 1] + v * p[b + 1] + w * p[c + 1];
                if (float.IsNaN(best) || y > best) best = y;
            }
            return best;
        }

        /// <summary>
        /// LOD2 keeps the car's shape: the far hull may not drop the stations that make the glasshouse (the old
        /// every-other-station rule turned every car into a wedge coupé). Along two lines over the roof (x = ±0.4 m, clear
        /// of signs, bars and rails), the roof span (top within 8 cm of the highest point) starts and ends within 12 cm of
        /// LOD0's headers, and over LOD0's roof span the LOD2 roof is within 5 cm.
        /// </summary>
        [Test]
        public void FarCarsKeepTheirRoofline()
        {
            var m0 = new MeshData(8192, 24576);
            var m2 = new MeshData(2048, 6144);
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                if (e.Shape != BodyShape.Hatchback && e.Shape != BodyShape.Suv && e.Shape != BodyShape.Pickup) continue;
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    string id = e.AssetId + "/" + VehicleMesher.ModelName(v, k);
                    m0.Clear();
                    m2.Clear();
                    VehicleMesher.Build(v, 0, (byte)k, VehicleLod.Lod0, m0, 5u);
                    VehicleMesher.Build(v, 0, (byte)k, VehicleLod.Lod2, m2, 5u);
                    float minX, minY, minZ, maxX, maxY, maxZ;
                    m0.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                    int n = (int)((maxZ - minZ) / 0.04f);
                    var h0 = new float[n];
                    var h2 = new float[n];
                    float top0 = 0, top2 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float z = minZ + 0.02f + 0.04f * i;
                        h0[i] = Math.Max(Nz(TopAt(m0, 0.4f, z)), Nz(TopAt(m0, -0.4f, z)));
                        h2[i] = Math.Max(Nz(TopAt(m2, 0.4f, z)), Nz(TopAt(m2, -0.4f, z)));
                        top0 = Math.Max(top0, h0[i]);
                        top2 = Math.Max(top2, h2[i]);
                    }
                    Assert.That(top2, Is.EqualTo(top0).Within(0.05f), id + " roof height");
                    int a0 = -1, b0 = -1, a2 = -1, b2 = -1;
                    for (int i = 0; i < n; i++)
                    {
                        if (h0[i] >= top0 - 0.08f)
                        {
                            if (a0 < 0) a0 = i;
                            b0 = i;
                        }
                        if (h2[i] >= top2 - 0.08f)
                        {
                            if (a2 < 0) a2 = i;
                            b2 = i;
                        }
                    }
                    Assert.That(Math.Abs(a2 - a0) * 0.04f, Is.LessThanOrEqualTo(0.12f), id + " rear roof header");
                    Assert.That(Math.Abs(b2 - b0) * 0.04f, Is.LessThanOrEqualTo(0.12f), id + " windscreen header");
                    for (int i = a0; i <= b0; i++) Assert.That(h2[i], Is.EqualTo(h0[i]).Within(0.05f), id + " roof at z " + (minZ + 0.02f + 0.04f * i));
                }
            }
        }

        /// <summary>
        /// A far two-wheeler (LOD2, wheels and rider left out) is one piece: voxelised at 2.5 cm, every part touches the
        /// rest (no seat, tank or tail floating with daylight round it), and the piece runs from behind the seat to the
        /// headstock (the frame, side covers and tail are there, not just a tank and an engine).
        /// </summary>
        [Test]
        public void FarTwoWheelersAreOnePiece()
        {
            var m = new MeshData(2048, 6144);
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                if (e.Shape != BodyShape.Scooter && e.Shape != BodyShape.Motorbike && e.Shape != BodyShape.Cruiser && e.Shape != BodyShape.Bicycle) continue;
                VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    string id = e.AssetId + "/" + VehicleMesher.ModelName(v, k);
                    m.Clear();
                    VehicleMesher.Build(v, 0, (byte)k, VehicleLod.Lod2, m, 5u, false, false);
                    float z0, z1;
                    int pieces = Pieces(m, 0.025f, out z0, out z1);
                    Assert.That(pieces, Is.EqualTo(1), id + " LOD2 body pieces");
                    Assert.That(z0, Is.LessThanOrEqualTo(0.42f * d.Wheelbase - 0.15f), id + " reaches behind the seat");
                    Assert.That(z1, Is.GreaterThanOrEqualTo(d.Wheelbase - 0.40f), id + " reaches the headstock");
                }
            }
        }

        /// <summary>Connected pieces of a mesh voxelised at <paramref name="cell"/> (26-neighbourhood over the cells its
        /// triangles touch) and the z extent of the largest piece.</summary>
        internal static int Pieces(MeshData m, float cell, out float zMin, out float zMax)
        {
            var cells = new HashSet<long>();
            float[] p = m.Positions;
            for (int t = 0; t < m.IndexCount; t += 3)
            {
                int a = m.Indices[t] * 3, b = m.Indices[t + 1] * 3, c = m.Indices[t + 2] * 3;
                float e = Math.Max(Dist(p, a, b), Math.Max(Dist(p, b, c), Dist(p, c, a)));
                int n = Math.Max(1, (int)Math.Ceiling(e / (0.4f * cell)));
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n - i; j++)
                    {
                        float u = (float)i / n, w = (float)j / n;
                        float x = p[a] + (p[b] - p[a]) * u + (p[c] - p[a]) * w, y = p[a + 1] + (p[b + 1] - p[a + 1]) * u + (p[c + 1] - p[a + 1]) * w,
                              z = p[a + 2] + (p[b + 2] - p[a + 2]) * u + (p[c + 2] - p[a + 2]) * w;
                        cells.Add(Key((int)Math.Floor(x / cell), (int)Math.Floor(y / cell), (int)Math.Floor(z / cell)));
                    }
            }
            var seen = new HashSet<long>();
            var queue = new Queue<long>();
            int pieces = 0, best = 0;
            zMin = zMax = 0;
            foreach (long start in cells)
            {
                if (!seen.Add(start)) continue;
                pieces++;
                queue.Enqueue(start);
                int count = 0, lo = int.MaxValue, hi = int.MinValue;
                while (queue.Count > 0)
                {
                    long k = queue.Dequeue();
                    count++;
                    int cx = (int)((k >> 42) & 0x1FFFFF) - (1 << 20), cy = (int)((k >> 21) & 0x1FFFFF) - (1 << 20), cz = (int)(k & 0x1FFFFF) - (1 << 20);
                    lo = Math.Min(lo, cz);
                    hi = Math.Max(hi, cz);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                long nk = Key(cx + dx, cy + dy, cz + dz);
                                if (cells.Contains(nk) && seen.Add(nk)) queue.Enqueue(nk);
                            }
                }
                if (count > best)
                {
                    best = count;
                    zMin = lo * cell;
                    zMax = (hi + 1) * cell;
                }
            }
            return pieces;
        }

        private static long Key(int x, int y, int z)
        {
            return ((long)(x + (1 << 20)) << 42) | ((long)(y + (1 << 20)) << 21) | (long)(z + (1 << 20));
        }

        private static float Dist(float[] p, int a, int b)
        {
            float dx = p[a] - p[b], dy = p[a + 1] - p[b + 1], dz = p[a + 2] - p[b + 2];
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float Nz(float f)
        {
            return float.IsNaN(f) ? 0f : f;
        }

        /// <summary>Overall dimensions of every catalogue entry (run explicitly).</summary>
        [Test, Explicit]
        public void DimsReport()
        {
            var lines = new List<string>();
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
                lines.Add(v + " " + e.AssetId + " L " + d.Length.ToString("0.00") + " W " + d.Width + " H " + d.Height + " wb " + d.Wheelbase + " R " + d.WheelR.ToString("0.000") +
                          " rear " + d.Rear.ToString("0.00") + " front " + d.Front.ToString("0.00") + " seats " + e.Seats);
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        /// <summary>Triangle report per entry, model and level: body, each wheel, rider (run explicitly).</summary>
        [Test, Explicit]
        public void TriangleReport()
        {
            var lines = new List<string>();
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                    foreach (VehicleLod lod in Moving)
                    {
                        int body = VehicleMesher.Build(v, 0, (byte)k, lod, new MeshData(), 3u, false);
                        int withRider = VehicleMesher.Build(v, 0, (byte)k, lod, new MeshData(), 3u, true);
                        string wheels = "";
                        int wsum = 0;
                        if (lod <= VehicleLod.Lod1)
                            foreach (WheelSocket w in VehicleMesher.Wheels(e, k))
                            {
                                int t = VehicleMesher.BuildWheel(w, lod, new MeshData());
                                wsum += t;
                                wheels += " " + t;
                            }
                        lines.Add(e.AssetId + "/" + VehicleMesher.ModelName(v, k) + " " + lod + ": body " + body + " rider +" + (withRider - body) + " wheels" + wheels +
                                  " = " + (withRider + wsum) + " / " + VehicleMesher.Budget(lod, e.Shape));
                    }
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        /// <summary>OBJs of every model type (LOD0 with wheels and a rider on two-wheelers, plus LOD1 and LOD2) for
        /// tools/mesh-preview/render.py. Writes only with GHUMANTE_PREVIEW_DIR set.</summary>
        [Test]
        public void DumpVehiclePreviews()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the vehicle previews");
            string only = Environment.GetEnvironmentVariable("GHUMANTE_VEHICLE_ONLY");
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                if (!string.IsNullOrEmpty(only) && e.AssetId.IndexOf(only, StringComparison.Ordinal) < 0) continue;
                bool two = e.Shape == BodyShape.Scooter || e.Shape == BodyShape.Motorbike || e.Shape == BodyShape.Cruiser || e.Shape == BodyShape.Bicycle;
                for (int k = 0; k < VehicleMesher.ModelCount(v); k++)
                {
                    string name = e.AssetId.Replace("ghm_veh_", "") + (string.IsNullOrEmpty(e.Variant) ? "" : "_" + e.Variant) + "_" + VehicleMesher.ModelName(v, k);
                    for (int l = 0; l < Math.Min(2, (int)e.LiveryCount); l++)
                    {
                        foreach (VehicleLod lod in Moving)
                        {
                            if (l > 0 && lod != VehicleLod.Lod0) continue;
                            var m = new MeshData();
                            int tris = VehicleMesher.Build(v, (byte)l, (byte)k, lod, m, 1234u + (uint)l, false);
                            if (lod <= VehicleLod.Lod1)
                                foreach (WheelSocket s in VehicleMesher.Wheels(e, k))
                                {
                                    var wheel = new MeshData();
                                    tris += VehicleMesher.BuildWheel(s, lod, wheel);
                                    ObjDump.Append(m, wheel, s.X, s.Y, s.Z);
                                }
                            string suffix = (l > 0 ? "_l" + l : "") + (lod == VehicleLod.Lod0 ? "" : "_" + lod.ToString().ToLowerInvariant());
                            ObjDump.Write(m, "vehicles/" + name + suffix + ".obj", new[] { "meshpreview: views=4", "tris " + tris });
                            if (two && lod == VehicleLod.Lod0 && l == 0)
                            {
                                var rm = new MeshData();
                                VehicleMesher.Build(v, (byte)l, (byte)k, lod, rm, 1234u, true);
                                foreach (WheelSocket s in VehicleMesher.Wheels(e, k))
                                {
                                    var wheel = new MeshData();
                                    VehicleMesher.BuildWheel(s, lod, wheel);
                                    ObjDump.Append(rm, wheel, s.X, s.Y, s.Z);
                                }
                                ObjDump.Write(rm, "vehicles/" + name + "_rider.obj", new[] { "meshpreview: views=4" });
                            }
                        }
                    }
                }
            }
        }
    }
}
