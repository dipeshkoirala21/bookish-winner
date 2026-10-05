using System;
using System.IO;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>"Vehicles rest outside" for the player's vehicle (W2_DESIGN 6.6, W2-O1): the swept path and the braking
    /// curve stop it at a sacred zone's edge at any speed, forwards and in reverse, and a road that turns away from a
    /// compound does not stop it. Plus the passenger camera per vehicle class (6.4) and the node-anchored curated kora.</summary>
    public class CharactersZoneGateTests
    {
        private const float Dt = 1f / 60f;

        private static VehicleCatalogEntry Entry(string assetId)
        {
            VehicleCatalogEntry e;
            Assert.That(VehicleCatalog.TryGet(assetId, out e), Is.True, assetId);
            return e;
        }

        /// <summary>The farthest point of zone <paramref name="z"/> from its centre along 32 rays (its outer radius).</summary>
        private static double OuterRadius(SacredZoneIndex zones, in SacredZone z)
        {
            double r = 0;
            for (int k = 0; k < 32; k++)
            {
                double a = k * Math.PI / 16;
                for (double d = 0; d < 600; d += 0.5)
                {
                    SacredZone at;
                    if (zones.TryGetZone(z.CX + Math.Sin(a) * d, z.CZ + Math.Cos(a) * d, out at) && at.AreaRef == z.AreaRef) r = Math.Max(r, d);
                }
            }
            return r;
        }

        private static bool FrontInside(SacredZoneIndex zones, uint allowed, in VehicleCatalogEntry e, ArcadeVehicle v, bool rear)
        {
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            float off = rear ? -d.Rear : d.Wheelbase + d.Front;
            double x = v.X + Math.Sin(v.HeadingRad) * off, z = v.Z + Math.Cos(v.HeadingRad) * off;
            SacredZone zz;
            return zones.TryGetZone(x, z, out zz) && (allowed & (1u << (int)zz.Kind)) == 0;
        }

        [TestCase("ghm_veh_taxi_small_a", false)]
        [TestCase("ghm_veh_motorbike_commuter_a", false)]
        [TestCase("ghm_veh_bus_city_a", false)]
        [TestCase("ghm_veh_taxi_small_a", true)]
        public void FullThrottleAtBoudhaStopsAtTheEdge(string asset, bool reverse)
        {
            SacredZoneIndex zones = TrafficData.Zones;
            double bx, bz;
            TrafficData.Game(TrafficData.Boudha, out bx, out bz);
            SacredZone boudha;
            Assert.That(zones.TryGetZone(bx, bz, out boudha), Is.True, "Boudha zone");
            VehicleCatalogEntry e = Entry(asset);
            uint allowed = VehicleZoneGate.AllowedKinds(e);
            // Start 260 m south of the kora's outer edge, facing the stupa (or backing towards it).
            double start = OuterRadius(zones, boudha) + (reverse ? 60 : 260);
            var g = new PlaneGround();
            var v = new ArcadeVehicle(e.Spec()) { RoadAssist = false };
            v.Teleport(boudha.CX, boudha.CZ - start, reverse ? (float)Math.PI : 0f, g);
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            float top = 0f;
            for (int i = 0; i < 60 * 90; i++)
            {
                float s = (float)Math.Sin(v.HeadingRad), c = (float)Math.Cos(v.HeadingRad), front = d.Wheelbase + d.Front;
                float look = VehicleZoneGate.LookAheadM(Math.Abs(v.SpeedMps), v.Spec.BrakeMps2);
                float frontM = VehicleZoneGate.DistanceToZone(zones, allowed, v.X + s * front, v.Z + c * front, v.HeadingRad, 0f, look);
                float rearM = VehicleZoneGate.DistanceToZone(zones, allowed, v.X - s * d.Rear, v.Z - c * d.Rear, v.HeadingRad + (float)Math.PI, 0f, look);
                var input = new DriveInput(reverse ? -1f : 1f, 0f, 0f);
                VehicleZoneGate.Apply(ref input, v.SpeedMps, frontM, rearM, v.Spec.BrakeMps2);
                v.Step(input, Dt, g, 0f);
                top = Math.Max(top, Math.Abs(v.SpeedMps));
                Assert.That(FrontInside(zones, allowed, e, v, reverse), Is.False, $"{asset} entered the zone at step {i}, {v.SpeedMps * 3.6f:F0} km/h");
            }
            if (!reverse) Assert.That(top, Is.GreaterThan(0.9f * e.Spec().MaxSpeedKmh / 3.6f), "reached top speed on the run-up");
            Assert.That(Math.Abs(v.SpeedMps), Is.LessThan(0.3f), "standing at the edge");
            // ... and standing close to it, not braking far short.
            float sNow = (float)Math.Sin(v.HeadingRad), cNow = (float)Math.Cos(v.HeadingRad);
            float off = reverse ? -d.Rear : d.Wheelbase + d.Front;
            float gap = VehicleZoneGate.DistanceToZone(zones, allowed, v.X + sNow * off, v.Z + cNow * off, v.HeadingRad + (reverse ? (float)Math.PI : 0f), 0f, 20f);
            Assert.That(gap, Is.InRange(0f, VehicleZoneGate.MarginM + 2.5f), "stops at the edge");
        }

        [Test]
        public void TheSweepFollowsTheSteeringArc()
        {
            // A road that bends away from Boudha: the straight line ahead crosses the kora, the arc the car is
            // steering does not, so the car is not stopped.
            SacredZoneIndex zones = TrafficData.Zones;
            double bx, bz;
            TrafficData.Game(TrafficData.Boudha, out bx, out bz);
            SacredZone boudha;
            Assert.That(zones.TryGetZone(bx, bz, out boudha), Is.True);
            uint allowed = VehicleZoneGate.AllowedKinds(Entry("ghm_veh_taxi_small_a"));
            double rb = OuterRadius(zones, boudha), r = rb + 6;
            // South of the stupa, heading east but tilted north so the straight line passes 0.4 rb from the centre.
            double alpha = Math.Acos(0.4 * rb / r);
            double px = boudha.CX, pz = boudha.CZ - r;
            float heading = (float)(Math.PI / 2 - alpha);
            float look = (float)(2 * r);
            Assert.That(VehicleZoneGate.DistanceToZone(zones, allowed, px, pz, heading, 0f, look), Is.LessThan(look), "straight probe hits");
            Assert.That(VehicleZoneGate.DistanceToZone(zones, allowed, px, pz, heading, 1f / 8f, 40f), Is.EqualTo(float.PositiveInfinity),
                        "turning away (8 m radius, right) stays clear");
        }

        [Test]
        public void FirstEntryLiesOnTheZoneEdge()
        {
            SacredZoneIndex zones = TrafficData.Zones;
            double bx, bz;
            TrafficData.Game(TrafficData.Boudha, out bx, out bz);
            double x0 = bx - 500, z0 = bz;
            double t;
            Assert.That(zones.TryFirstEntry(x0, z0, bx, bz, 0u, out t), Is.True);
            double ex = x0 + (bx - x0) * t, ez = z0 + (bz - z0) * t;
            double step = 0.05 / 500;
            Assert.That(zones.Contains(x0 + (bx - x0) * (t + step), ez), Is.True, "just past the entry is inside");
            Assert.That(zones.Contains(x0 + (bx - x0) * (t - step), ez), Is.False, "just before is outside");
            Assert.That(zones.TryFirstEntry(bx, bz, bx + 1, bz, 0u, out t) && t == 0, Is.True, "a start inside enters at 0");
            // Ignoring every kind sees nothing.
            Assert.That(zones.TryFirstEntry(x0, z0, bx, bz, ~0u, out t), Is.False);
            Assert.That(zones.IntersectsSegment(x0, z0, bx, bz), Is.True, "agrees with IntersectsSegment");
            _ = ex;
        }

        [Test]
        public void AllowedKindsFollowTheRoles()
        {
            uint bike = VehicleZoneGate.AllowedKinds(Entry("ghm_veh_bicycle_a"));
            uint car = VehicleZoneGate.AllowedKinds(Entry("ghm_veh_taxi_small_a"));
            Assert.That(bike & (1u << (int)SacredZoneKind.HeritageSquare), Is.Not.Zero, "a bicycle may cross a heritage square");
            Assert.That(bike & (1u << (int)SacredZoneKind.Compound), Is.Zero);
            Assert.That(car, Is.EqualTo(1u << (int)SacredZoneKind.None), "a car may enter no zone");
            Assert.That(VehicleZoneGate.SpeedCapMps(VehicleZoneGate.MarginM, 7f), Is.EqualTo(0f));
            Assert.That(VehicleZoneGate.SpeedCapMps(float.PositiveInfinity, 7f), Is.EqualTo(float.PositiveInfinity));
            // At the edge a standing car cannot set off towards the zone, but can back away from it.
            var go = new DriveInput(1f, 0f, 0f);
            Assert.That(VehicleZoneGate.Apply(ref go, 0f, 0.5f, float.PositiveInfinity, 7f), Is.True);
            Assert.That(go.Throttle, Is.EqualTo(0f));
            var back = new DriveInput(-1f, 0f, 0f);
            Assert.That(VehicleZoneGate.Apply(ref back, 0f, 0.5f, float.PositiveInfinity, 7f), Is.False);
        }

        [Test]
        public void PassengerCameraOrbitsTheRiddenVehiclesRig()
        {
            foreach (bool portrait in new[] { false, true })
            {
                RigParams bus = CameraRigTable.For(RigClass.Bus, portrait);
                RigParams ride = CameraRigTable.For(RigClass.Passenger, portrait, RigClass.Bus);
                Assert.That(ride.DistanceM, Is.EqualTo(bus.DistanceM * CameraRigTable.PassengerScale).Within(1e-4f), "1.2× the bus rig");
                Assert.That(ride.PivotM, Is.EqualTo(bus.PivotM), "the bus pivot");
                Assert.That(ride.LookAheadS, Is.EqualTo(0f));
                RigParams van = CameraRigTable.For(RigClass.Passenger, portrait, RigClass.Van);
                Assert.That(van.DistanceM, Is.EqualTo(CameraRigTable.For(RigClass.Van, portrait).DistanceM * 1.2f).Within(1e-4f));
                // Unknown or self-referential classes fall back to the car rig; the old two-argument call is unchanged.
                RigParams car = CameraRigTable.For(RigClass.Passenger, portrait);
                Assert.That(CameraRigTable.For(RigClass.Passenger, portrait, RigClass.Passenger).DistanceM, Is.EqualTo(car.DistanceM));
                Assert.That(car.DistanceM, Is.EqualTo(CameraRigTable.For(RigClass.Car, portrait).DistanceM * 1.2f).Within(1e-4f));
                // Other rigs ignore the ridden class.
                Assert.That(CameraRigTable.For(RigClass.Walk, portrait, RigClass.Bus).DistanceM, Is.EqualTo(CameraRigTable.For(RigClass.Walk, portrait).DistanceM));
            }
        }

        [Test]
        public void NodeAnchoredCuratedKoraIsCentredOnItsStupa()
        {
            // Kathesimbhu's curated record (Stupa, clockwise kora) is anchored on a node with no compound: it must still
            // make its compound a kora about the stupa.
            string path = DrivingData.SamplePath(".curated.ghcd");
            if (!File.Exists(path)) Assert.Ignore("no curated DB in the sample region");
            CuratedDb db = CuratedDb.Read(File.ReadAllBytes(path));
            HeritageRecord rec = null;
            foreach (HeritageRecord r in db.Heritage)
                if (r.Kind == HeritageKind.Stupa && r.Kora != KoraDirection.None && r.CompoundRef == 0 && !double.IsNaN(r.X)) rec = r;
            if (rec == null) Assert.Ignore("no node-anchored stupa kora in the curated DB");
            SacredZoneIndex zones = TrafficData.Zones;
            SacredZone z;
            if (!zones.TryGetZone(rec.X, rec.Z, out z)) Assert.Ignore($"{rec.Id}: no compound around the anchor in the sample");
            Assert.That(z.HeroId, Is.EqualTo(rec.Id));
            Assert.That(z.Kind, Is.EqualTo(SacredZoneKind.StupaKora));
            Assert.That(z.Kora, Is.True);
            Assert.That(z.KoraDir, Is.EqualTo(rec.Kora));
            Assert.That(Math.Sqrt((z.CX - rec.X) * (z.CX - rec.X) + (z.CZ - rec.Z) * (z.CZ - rec.Z)), Is.LessThan(8.0), "kora centre on the stupa");
        }
    }
}
