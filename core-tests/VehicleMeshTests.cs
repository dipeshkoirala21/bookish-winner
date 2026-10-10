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
                foreach (WheelSocket w in VehicleMesher.Wheels(VehicleCatalog.At(v)))
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
                            foreach (WheelSocket w in VehicleMesher.Wheels(e))
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
                                foreach (WheelSocket s in VehicleMesher.Wheels(e))
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
                                foreach (WheelSocket s in VehicleMesher.Wheels(e))
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
