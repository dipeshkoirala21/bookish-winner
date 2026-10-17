using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The flyable aircraft models (<see cref="FlightMesher"/>): budgets per LOD, real outer dimensions, wheels on the
    /// ground, material channels and AO, determinism, and the OBJ previews (written only with GHUMANTE_PREVIEW_DIR set;
    /// rendered with tools/mesh-preview/render.py into /home/user/wt/previews/flight/).
    /// </summary>
    public class FlightMeshTests
    {
        [Test]
        public void EveryAircraftFitsItsBudgetAndItsRealSize()
        {
            foreach (FlightCraft c in FlightSpec.All)
            {
                FlightSpec s = FlightSpec.For(c);
                for (int lod = 0; lod < 3; lod++)
                {
                    var m = new MeshData();
                    int tris = FlightMesher.BuildParked(c, 0, lod, m);
                    TestContext.WriteLine(c + " LOD" + lod + ": " + tris + " triangles");
                    Assert.That(tris, Is.GreaterThan(lod == 2 ? 100 : 500), c + " LOD" + lod + " has a shape");
                    Assert.That(tris, Is.LessThanOrEqualTo(FlightMesher.Budget[lod]), c + " LOD" + lod + " within budget");
                    Assert.That(m.HasUv0, Is.True, "material channels written");
                    float x0, y0, z0, x1, y1, z1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    for (int i = 0; i < m.VertexCount * 3; i++) Assert.That(float.IsNaN(m.Positions[i]), Is.False);
                    if (lod > 0) continue;
                    float span = x1 - x0, length = z1 - z0, height = y1 - y0;
                    TestContext.WriteLine("  span " + span.ToString("0.00") + " length " + length.ToString("0.00") + " height " + height.ToString("0.00"));
                    if (c == FlightCraft.Helicopter)
                    {
                        // The span of a helicopter is its rotor's diameter; three blades parked never reach across it.
                        var rotor = new MeshData();
                        FlightMesher.Build(c, FlightPart.Rotor, 0, 0, rotor);
                        double r = 0;
                        for (int i = 0; i < rotor.VertexCount; i++)
                            r = Math.Max(r, Math.Sqrt(rotor.Positions[i * 3] * rotor.Positions[i * 3] + rotor.Positions[i * 3 + 2] * rotor.Positions[i * 3 + 2]));
                        TestContext.WriteLine("  rotor diameter " + (2 * r).ToString("0.00"));
                        Assert.That(2 * r, Is.EqualTo(s.SpanM).Within(s.SpanM * 0.06), c + " rotor diameter");
                    }
                    else
                    {
                        Assert.That(span, Is.EqualTo(s.SpanM).Within(s.SpanM * 0.06), c + " span");
                    }
                    Assert.That(length, Is.EqualTo(s.LengthM).Within(s.LengthM * 0.06), c + " length");
                    Assert.That(height, Is.EqualTo(s.HeightM).Within(s.HeightM * 0.1), c + " height");
                    Assert.That(y0, Is.InRange(-0.02f, 0.05f), c + " stands on its wheels or skids");
                }
            }
        }

        [Test]
        public void TheMovingPartsAndTheCockpitAreThere()
        {
            foreach (FlightCraft c in FlightSpec.All)
            {
                var m = new MeshData();
                Assert.That(FlightMesher.Build(c, FlightPart.Cockpit, 0, 0, m), Is.GreaterThan(100), c + " cockpit");
                m.Clear();
                if (c == FlightCraft.Helicopter)
                {
                    Assert.That(FlightMesher.Build(c, FlightPart.Rotor, 0, 0, m), Is.GreaterThan(50));
                    m.Clear();
                    Assert.That(FlightMesher.Build(c, FlightPart.TailRotor, 0, 0, m), Is.GreaterThan(20));
                    Assert.That(FlightMesher.PropCount(c), Is.EqualTo(0));
                    continue;
                }
                Assert.That(FlightMesher.PropCount(c), Is.EqualTo(2));
                Assert.That(FlightMesher.Build(c, FlightPart.Prop, 0, 0, m), Is.GreaterThan(100), c + " propeller");
                float x0, y0, z0, x1, y1, z1;
                m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                float px, py, pz;
                bool cw;
                FlightMesher.PropMount(c, 1, out px, out py, out pz, out cw);
                Assert.That(py - 0.5f * (y1 - y0), Is.GreaterThan(0.25f), c + " propeller clears the ground");
            }
        }

        [Test]
        public void PartBreakdown()
        {
            foreach (FlightCraft c in FlightSpec.All)
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    string line = c + " LOD" + lod + ":";
                    foreach (FlightPart p in new[] { FlightPart.Body, FlightPart.Frame, FlightPart.Gear, FlightPart.Prop, FlightPart.Rotor, FlightPart.TailRotor, FlightPart.Cockpit })
                    {
                        var m = new MeshData();
                        line += " " + p + " " + FlightMesher.Build(c, p, 0, lod, m);
                    }
                    TestContext.WriteLine(line);
                }
            }
        }

        [Test]
        public void ModelsAreDeterministic()
        {
            var a = new MeshData();
            var b = new MeshData();
            FlightMesher.BuildParked(FlightCraft.Turboprop, 2, 0, a);
            FlightMesher.BuildParked(FlightCraft.Turboprop, 2, 0, b);
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
        }

        [Test]
        public void DumpPreviews()
        {
            if (!ObjDump.Enabled) Assert.Pass("GHUMANTE_PREVIEW_DIR not set");
            foreach (FlightCraft c in FlightSpec.All)
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    var m = new MeshData();
                    FlightMesher.BuildParked(c, c == FlightCraft.Helicopter ? 0 : (int)c, lod, m);
                    ObjDump.Write(m, "flight/" + c + "_lod" + lod + ".obj");
                }
                var cp = new MeshData();
                FlightMesher.Build(c, FlightPart.Cockpit, 0, 0, cp);
                var frame = new MeshData();
                FlightMesher.Build(c, FlightPart.Frame, 0, 0, frame);
                ObjDump.Write("flight/" + c + "_cockpit.obj", new ObjPart("cockpit", cp), new ObjPart("frame", frame));
            }
        }
    }
}
