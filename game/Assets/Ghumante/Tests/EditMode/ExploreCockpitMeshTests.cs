using System;
using Ghumante.Characters.Rides;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The stand-in cockpit for the driver's view inside closed bodies (W2 detail pass review: "Interior, Driver's seat and
    /// Your seat views look out from inside a back-face-culled shell with no cabin"): every closed vehicle the player can
    /// drive gets a cabin within budget, wound and shaded right, with UV0 channels, inside its body and around the eye;
    /// open vehicles get none; the steering wheel turns like the hands. Engine-free (no UnityEngine).
    /// </summary>
    public class ExploreCockpitMeshTests
    {
        [Test]
        public void EveryClosedCabinBuildsWithinBudgetAroundTheEye()
        {
            int built = 0;
            for (int i = 0; i < VehicleCatalog.Count; i++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(i);
                CockpitSpec s = CockpitSpec.For(e, 0);
                if (s.Kind == CockpitKind.None) continue;
                built++;
                string what = e.AssetId + " (" + e.Shape + ")";
                var cabin = new MeshData();
                CockpitMesher.Build(s, cabin);
                var wheel = new MeshData();
                CockpitMesher.BuildWheel(s, wheel);
                Assert.Greater(cabin.TriangleCount, 2000, what + ": a detailed cabin");
                Assert.LessOrEqual(cabin.TriangleCount + wheel.TriangleCount, CockpitMesher.MaxTriangles, what + ": within budget");
                Assert.Greater(wheel.TriangleCount, 300, what + ": a round steering wheel");
                Assert.IsTrue(cabin.HasUv0 && wheel.HasUv0, what + ": material channels and AO in UV0");
                Assert.Less(Disagreeing(cabin), 0.01, what + ": triangles face the way their normals do");
                Assert.Less(Disagreeing(wheel), 0.01, what + ": the wheel too");

                float ex, ey, ez;
                CockpitMesher.Eye(s, out ex, out ey, out ez);
                Assert.IsTrue(CockpitMesher.Inside(s, ex, ey, ez), what + ": the driver's eye is inside the body");
                Assert.IsFalse(CockpitMesher.Inside(s, ex, ey, s.NoseZ + 1f), what + ": in front of the nose is outside");
                Assert.IsFalse(CockpitMesher.Inside(s, s.HalfWidth + 0.3f, ey, ez), what + ": beside the body is outside");

                // Inside the body (side mirrors stand out by design; the lining may sit above a low cartoon roof).
                for (int v = 0; v < cabin.VertexCount; v++)
                {
                    float x = cabin.Positions[3 * v], y = cabin.Positions[3 * v + 1], z = cabin.Positions[3 * v + 2];
                    Assert.LessOrEqual(Math.Abs(x), s.HalfWidth + 0.4f, what + ": x " + x);
                    Assert.That(y, Is.InRange(0.2f, Math.Max(s.Height, ey + 0.45f)), what + ": y");
                    Assert.That(z, Is.InRange(s.TailZ - 0.05f, s.NoseZ + 0.15f), what + ": z");
                }
                // Nothing of the cabin hangs within 0.12 m of the eye (the near plane is 0.1 m).
                Assert.Greater(NearestTo(cabin, ex, ey, ez), 0.12f, what + ": clear of the eye");
            }
            Assert.GreaterOrEqual(built, 8, "cars, SUVs, pickups, vans, the tempo, buses and trucks");
        }

        [Test]
        public void OpenVehiclesGetNone()
        {
            foreach (BodyShape shape in new[] { BodyShape.Scooter, BodyShape.Motorbike, BodyShape.Bicycle, BodyShape.Tractor, BodyShape.Rickshaw })
                Assert.AreEqual(CockpitKind.None, CockpitMesher.KindOf(shape), shape.ToString());
            var m = new MeshData();
            CockpitMesher.Build(new CockpitSpec { Kind = CockpitKind.None, HalfWidth = 0.4f, Height = 1.1f, NoseZ = 1.5f, TailZ = -0.3f }, m);
            Assert.AreEqual(0, m.VertexCount);
        }

        [Test]
        public void TheBusHasItsSaloonAndTheTruckOnlyItsCab()
        {
            CockpitSpec bus = CockpitSpec.For(VehicleCatalog.At(Find(BodyShape.Bus)), 0);
            CockpitSpec truck = CockpitSpec.For(VehicleCatalog.At(Find(BodyShape.Truck)), 0);
            var b = new MeshData();
            var t = new MeshData();
            CockpitMesher.Build(bus, b);
            CockpitMesher.Build(truck, t);
            Assert.Less(MinZ(b), bus.TailZ + 0.5f, "the bus's seats and lining reach its back");
            Assert.Greater(MinZ(t), truck.SeatZ - 0.75f, "the truck's load bed is not inside the cab");
        }

        [Test]
        public void TheWheelTurnsLikeTheHands()
        {
            CockpitSpec car = CockpitSpec.For(VehicleCatalog.At(Find(BodyShape.Hatchback)), 0);
            CockpitSpec bus = CockpitSpec.For(VehicleCatalog.At(Find(BodyShape.Bus)), 0);
            Assert.AreEqual(0f, CockpitMesher.WheelAngleDeg(car, 0f));
            Assert.AreEqual(0.1f * 35f * 15f, CockpitMesher.WheelAngleDeg(car, 0.1f), 1e-3f, "15:1 on a car");
            Assert.AreEqual(0.1f * 35f * 20f, CockpitMesher.WheelAngleDeg(bus, 0.1f), 1e-3f, "20:1 on a bus");
            Assert.AreEqual(100f, CockpitMesher.WheelAngleDeg(car, 1f), "hand over hand beyond 100°");
            Assert.AreEqual(-100f, CockpitMesher.WheelAngleDeg(car, -3f));
            Assert.AreEqual(0f, CockpitMesher.WheelAngleDeg(car, float.NaN));
            float x, y, z, tilt, r;
            CockpitMesher.WheelPlacement(car, out x, out y, out z, out tilt, out r);
            Assert.AreEqual(car.SeatX, x, 1e-5f, "in front of the driver");
            Assert.AreEqual(car.SeatY + 0.34f, y, 1e-5f, "where CharacterPoser puts the hands");
            Assert.AreEqual(25f, tilt);
            CockpitMesher.WheelPlacement(bus, out x, out y, out z, out tilt, out r);
            Assert.AreEqual(60f, tilt, "a bus wheel lies flatter");
            Assert.Greater(r, 0.2f);
        }

        private static int Find(BodyShape shape)
        {
            for (int i = 0; i < VehicleCatalog.Count; i++)
                if (VehicleCatalog.At(i).Shape == shape) return i;
            Assert.Fail("no " + shape);
            return -1;
        }

        private static float MinZ(MeshData m)
        {
            float z = float.PositiveInfinity;
            for (int v = 0; v < m.VertexCount; v++) z = Math.Min(z, m.Positions[3 * v + 2]);
            return z;
        }

        private static float NearestTo(MeshData m, float x, float y, float z)
        {
            float best = float.PositiveInfinity;
            for (int v = 0; v < m.VertexCount; v++)
            {
                float dx = m.Positions[3 * v] - x, dy = m.Positions[3 * v + 1] - y, dz = m.Positions[3 * v + 2] - z;
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            return best;
        }

        /// <summary>Share of triangles whose winding (cross(b − a, c − a), Unity's front) opposes their vertex normals.</summary>
        private static double Disagreeing(MeshData m)
        {
            int bad = 0, n = 0;
            for (int k = 0; k < m.IndexCount; k += 3)
            {
                int a = m.Indices[k], b = m.Indices[k + 1], c = m.Indices[k + 2];
                float[] q = m.Positions;
                float ux = q[3 * b] - q[3 * a], uy = q[3 * b + 1] - q[3 * a + 1], uz = q[3 * b + 2] - q[3 * a + 2];
                float vx = q[3 * c] - q[3 * a], vy = q[3 * c + 1] - q[3 * a + 1], vz = q[3 * c + 2] - q[3 * a + 2];
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                if (nx * nx + ny * ny + nz * nz < 1e-14f) continue;
                n++;
                float sx = m.Normals[3 * a] + m.Normals[3 * b] + m.Normals[3 * c];
                float sy = m.Normals[3 * a + 1] + m.Normals[3 * b + 1] + m.Normals[3 * c + 1];
                float sz = m.Normals[3 * a + 2] + m.Normals[3 * b + 2] + m.Normals[3 * c + 2];
                if (nx * sx + ny * sy + nz * sz < 0f) bad++;
            }
            return n > 0 ? (double)bad / n : 1.0;
        }
    }
}
