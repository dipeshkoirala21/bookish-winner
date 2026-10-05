using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Save;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Enter and exit (V8), vehicle roles, cameras, calm mode, garage and passenger rides (W2_DESIGN 6.2–6.6).</summary>
    public class CharactersPlayerTests
    {
        /// <summary>Flat ground with axis-aligned walls (boxes that block a standing body).</summary>
        private sealed class WalledGround : IGroundQuery, IFootBlocker
        {
            public readonly List<(double x0, double z0, double x1, double z1)> Walls = new List<(double, double, double, double)>();
            public float Height;

            public bool TrySample(double x, double z, out GroundSample s)
            {
                s = new GroundSample { Height = Height, Ny = 1f, Surface = SurfaceGroup.Paved };
                return true;
            }

            public bool IsBlocked(double x, double z, float feetY)
            {
                foreach (var w in Walls)
                    if (x > w.x0 - 0.25 && x < w.x1 + 0.25 && z > w.z0 - 0.25 && z < w.z1 + 0.25) return true;
                return false;
            }
        }

        [Test]
        public void EveryCatalogueEntryHasARigLayoutAndPose()
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                RigClass rig = VehicleRoles.RigOf(e);
                RigParams land = CameraRigTable.For(rig, false), port = CameraRigTable.For(rig, true);
                Assert.That(port.DistanceM, Is.GreaterThanOrEqualTo(land.DistanceM), e.AssetId);
                Assert.That(port.PitchDeg, Is.GreaterThan(land.PitchDeg), e.AssetId);
                SeatSocket[] seats = VehicleSeats.For(e);
                Assert.That(seats.Length, Is.GreaterThan(0));
                foreach (SeatSocket s in seats)
                {
                    SeatPose pose = VehicleRoles.PoseFor(e, s.Kind);
                    Assert.That(pose, Is.Not.EqualTo(SeatPose.None), e.AssetId + " seat " + s.Index);
                }
                if (VehicleRoles.IsTwoWheeler(e.Shape)) Assert.That(VehicleRoles.NeedsHelmet(e), Is.True);
            }
        }

        [Test]
        public void CamerasFollowTheDesignTable()
        {
            RigParams walkP = CameraRigTable.For(RigClass.Walk, true);
            Assert.That(walkP.DistanceM, Is.EqualTo(6.8f));
            Assert.That(walkP.PitchDeg, Is.EqualTo(20f));
            Assert.That(walkP.PivotM, Is.EqualTo(0.9f));
            RigParams walkL = CameraRigTable.For(RigClass.Walk, false);
            Assert.That(walkL.DistanceM, Is.EqualTo(8.4f));
            Assert.That(CameraRigTable.For(RigClass.Bus, false).DistanceM, Is.EqualTo(16.5f));
            Assert.That(CameraRigTable.For(RigClass.Bus, false).MinHFovDeg, Is.EqualTo(64f));
            Assert.That(CameraRigTable.For(RigClass.Car, true).DistanceM, Is.EqualTo(10.8f));
            // Landscape distance ≈ 6 + 1 × vehicle length.
            foreach (int v in new[] { VehicleCatalog.Taxi, VehicleCatalog.Suv, VehicleCatalog.BusCityGreen, VehicleCatalog.TruckPainted })
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                float d = CameraRigTable.For(VehicleRoles.RigOf(e), false).DistanceM;
                Assert.That(d, Is.EqualTo(6f + e.LengthM).Within(1.6f), e.AssetId);
            }
            RigParams pass = CameraRigTable.For(RigClass.Passenger, false);
            Assert.That(pass.DistanceM, Is.EqualTo(CameraRigTable.For(RigClass.Car, false).DistanceM * 1.2f).Within(1e-4f));
            Assert.That(CameraRigTable.FovKick(CameraRigTable.For(RigClass.TwoWheeler, false), 10f), Is.EqualTo(0f), "no kick below 60 km/h");
            Assert.That(CameraRigTable.FovKick(CameraRigTable.For(RigClass.TwoWheeler, false), 30f), Is.EqualTo(6f));
        }

        [Test]
        public void RightHandDriveSidesFollowNepalsRules()
        {
            Assert.That(VehicleSeats.For(VehicleCatalog.Taxi)[0].Side, Is.EqualTo(EntrySide.Right), "car driver: right door");
            Assert.That(VehicleSeats.For(VehicleCatalog.Taxi)[2].Side, Is.EqualTo(EntrySide.Left), "passengers: kerb side");
            Assert.That(VehicleSeats.For(VehicleCatalog.Scooter)[0].Side, Is.EqualTo(EntrySide.Left), "two-wheelers from the left");
            Assert.That(VehicleSeats.For(VehicleCatalog.BusCityGreen)[0].Side, Is.EqualTo(EntrySide.Left), "bus: front-left door");
            Assert.That(VehicleSeats.For(VehicleCatalog.TruckPainted)[0].Side, Is.EqualTo(EntrySide.Right), "truck: right cab door");
            Assert.That(VehicleSeats.For(VehicleCatalog.Tractor)[0].Side, Is.EqualTo(EntrySide.Left), "tractor: left");
        }

        [Test]
        public void AWallOnTheDefaultSideSendsThePlayerRoundAndTooFarFails()
        {
            VehicleSeats.For(VehicleCatalog.Scooter);
            SeatSocket s = VehicleSeats.For(VehicleCatalog.Scooter)[0];
            var v = new VehicleFrame(100, 100, 0f, 0f);
            var g = new WalledGround();
            MountPlan p;
            Assert.That(MountPlan.TryPlan(VehicleClass.TwoWheeler, s, v, 99.0, 100.5, g, out p), Is.True);
            Assert.That(p.Alternate, Is.False);
            Assert.That(p.Side, Is.EqualTo(EntrySide.Left));
            Assert.That(p.EnterS, Is.EqualTo(0.6f).Within(0.1f));
            // A wall 0.5 m left of the scooter.
            g.Walls.Add((98.6, 95, 99.2, 105));
            Assert.That(MountPlan.TryPlan(VehicleClass.TwoWheeler, s, v, 101.0, 100.5, g, out p), Is.True);
            Assert.That(p.Alternate, Is.True);
            Assert.That(p.EntryX, Is.GreaterThan(100.0));
            Assert.That(MountPlan.TryPlan(VehicleClass.TwoWheeler, s, v, 110.0, 100.5, g, out p), Is.False, "more than 4 m away");
        }

        [Test]
        public void ExitPlacementIsNeverInsideGeometryOn1000ParkedSpots()
        {
            // V8: random vehicles parked among random walls; every exit lands on clear ground outside the body, and every
            // class can be entered from somewhere.
            uint h = 12345u;
            int placed = 0, kept = 0;
            for (int i = 0; i < 1000; i++)
            {
                h = CharMath.Hash(h, (uint)i);
                int variant = (int)(h % (uint)VehicleCatalog.Count);
                VehicleCatalogEntry e = VehicleCatalog.At(variant);
                var g = new WalledGround { Height = 1300f };
                var v = new VehicleFrame(1000 + i * 30, 2000, 1300f, CharMath.Unit(h) * 6.283f);
                // 0–3 walls near the vehicle.
                int walls = (int)((h >> 4) % 4);
                for (int k = 0; k < walls; k++)
                {
                    uint hk = CharMath.Hash(h, (uint)k, 77u);
                    double wx = v.X - 4 + 8 * CharMath.Unit(hk), wz = v.Z - 4 + 8 * CharMath.Unit(hk >> 3);
                    g.Walls.Add((wx, wz, wx + 0.3 + 3 * CharMath.Unit(hk >> 6), wz + 0.3 + 3 * CharMath.Unit(hk >> 9)));
                }
                SeatSocket[] seats = VehicleSeats.For(e);
                SeatSocket seat = seats[(int)((h >> 12) % (uint)seats.Length)];
                double x, z;
                float y;
                if (!ExitPlacement.Find(v, e, seat, g, out x, out z, out y))
                {
                    kept++;
                    continue;
                }
                placed++;
                Assert.That(g.IsBlocked(x, z, y), Is.False, "exit inside a wall");
                float lx, lz;
                v.ToLocal(x, z, out lx, out lz);
                Assert.That(ExitPlacement.InsideBody(VehicleMesher.DimsOf(e), lx, lz), Is.False, "exit inside the vehicle");
                Assert.That(y, Is.EqualTo(1300f).Within(1f));
            }
            Assert.That(placed, Is.GreaterThan(950), "almost every spot has room (" + kept + " kept seated)");
        }

        [Test]
        public void TimeToControlMatchesTheDurations()
        {
            var expected = new Dictionary<int, float>
            {
                { VehicleCatalog.Bicycle, 0.5f }, { VehicleCatalog.Scooter, 0.6f }, { VehicleCatalog.MotorbikeCommuter, 0.7f },
                { VehicleCatalog.Taxi, 0.9f }, { VehicleCatalog.Microbus, 1.0f }, { VehicleCatalog.BusCityGreen, 1.2f },
                { VehicleCatalog.TruckPainted, 1.4f }, { VehicleCatalog.Tractor, 1.0f }, { VehicleCatalog.Rickshaw, 0.8f },
            };
            foreach (var kv in expected)
            {
                SeatSocket s = VehicleSeats.For(kv.Key)[0];
                Assert.That(s.EnterS, Is.EqualTo(kv.Value).Within(0.1f), VehicleCatalog.At(kv.Key).AssetId);
                MountPlan p;
                var v = new VehicleFrame(0, 0, 0f, 0f);
                double ex, ez;
                v.ToWorld(s.EntryX, s.EntryZ, out ex, out ez);
                Assert.That(MountPlan.TryPlan(VehicleSeats.ClassOf(kv.Key), s, v, ex, ez, new WalledGround(), out p), Is.True);
                Assert.That(p.TotalS, Is.EqualTo(kv.Value).Within(0.1f));
            }
        }

        [Test]
        public void TheDetectorScoresInsideTheConeWithHysteresis()
        {
            var d = new MountDetector();
            SeatSocket[] seats = VehicleSeats.For(VehicleCatalog.Scooter);
            var v = new VehicleFrame(0, 0, 0f, 0f);
            double ex, ez;
            v.ToWorld(seats[0].EntryX, seats[0].EntryZ, out ex, out ez);
            // Facing the entry point from 1.5 m away: offered.
            d.Begin(ex - 1.5, ez, (float)(Math.PI / 2));
            d.Consider(7, VehicleCatalog.Scooter, v, seats, true, false);
            Assert.That(d.Driver.Valid, Is.True);
            Assert.That(d.Driver.VehicleId, Is.EqualTo(7));
            // Facing away: not offered.
            var away = new MountDetector();
            away.Begin(ex - 1.5, ez, (float)(-Math.PI / 2));
            away.Consider(7, VehicleCatalog.Scooter, v, seats, true, false);
            Assert.That(away.Driver.Valid, Is.False);
            // 2.2 m: beyond 2.0 m but within the 0.3 m hysteresis of the current pick.
            d.Begin(ex - 2.2, ez, (float)(Math.PI / 2));
            d.Consider(7, VehicleCatalog.Scooter, v, seats, true, false);
            Assert.That(d.Driver.Valid, Is.True, "hysteresis keeps the prompt");
            var fresh = new MountDetector();
            fresh.Begin(ex - 2.2, ez, (float)(Math.PI / 2));
            fresh.Consider(7, VehicleCatalog.Scooter, v, seats, true, false);
            Assert.That(fresh.Driver.Valid, Is.False, "a new prompt needs 2.0 m");
            // Only a passenger seat on a taxi that is not ours.
            SeatSocket[] taxi = VehicleSeats.For(VehicleCatalog.Taxi);
            var t = new VehicleFrame(0, 0, 0f, 0f);
            v.ToWorld(taxi[1].EntryX, taxi[1].EntryZ, out ex, out ez);
            var p = new MountDetector();
            p.Begin(ex - 1.0, ez, (float)(Math.PI / 2));
            p.Consider(9, VehicleCatalog.Taxi, t, taxi, false, true);
            Assert.That(p.Driver.Valid, Is.False);
            Assert.That(p.Passenger.Valid, Is.True);
        }

        [Test]
        public void VehiclesRestOutsideSacredZones()
        {
            VehicleCatalogEntry bike = VehicleCatalog.At(VehicleCatalog.Bicycle), car = VehicleCatalog.At(VehicleCatalog.Hatchback);
            Assert.That(VehicleRoles.AllowedIn(car, SacredZoneKind.None), Is.True);
            foreach (SacredZoneKind k in new[] { SacredZoneKind.Compound, SacredZoneKind.Courtyard, SacredZoneKind.HeritageSquare, SacredZoneKind.StupaKora, SacredZoneKind.Ghat })
                Assert.That(VehicleRoles.AllowedIn(car, k), Is.False, "no motor vehicle in " + k);
            Assert.That(VehicleRoles.AllowedIn(bike, SacredZoneKind.HeritageSquare), Is.True, "walk-and-cycle squares");
            Assert.That(VehicleRoles.AllowedIn(bike, SacredZoneKind.Compound), Is.False, "no vehicles in compounds");
            Assert.That(VehicleRoles.AllowedIn(VehicleCatalog.At(VehicleCatalog.Scooter), SacredZoneKind.HeritageSquare), Is.False);
        }

        [Test]
        public void CalmModeLimitsBellsAndShowsTheEntryCard()
        {
            var calm = new CalmMode();
            var zone = new SacredZone { AreaRef = 42, Kind = SacredZoneKind.Compound, Rule = EntryRule.ShoesOff };
            Assert.That(calm.Update(0.1f, true, zone), Is.EqualTo(1));
            Assert.That(CalmMode.CardKey(calm.Rule), Is.EqualTo("sacred.rule.shoes_off"));
            Assert.That(CalmMode.CardKey(EntryRule.NoLeather), Is.EqualTo("sacred.rule.no_leather"));
            Assert.That(calm.AllowSprint, Is.False);
            int rung = 0;
            for (int i = 0; i < 200; i++)
            {
                if (calm.TryRingBell()) rung++;
                calm.Update(0.1f, true, zone);
            }
            Assert.That(rung, Is.EqualTo(CalmMode.BellsPerVisit), "three per visit");
            Assert.That(calm.Update(0.1f, false, zone), Is.EqualTo(-1));
            var other = new SacredZone { AreaRef = 43, Kind = SacredZoneKind.StupaKora };
            calm.Update(0.1f, true, other);
            for (int i = 0; i < 40; i++) calm.Update(0.1f, true, other);
            Assert.That(calm.TryRingBell(), Is.True, "a new visit");
            Assert.That(calm.TryRingBell(), Is.False, "3 s between bells");
        }

        [Test]
        public void GarageAndAppearanceSurviveTheSave()
        {
            var save = new SaveData();
            CharacterRecipe r = PlayerProfile.LoadAppearance(save, 99u);
            Assert.That(PlayerProfile.HasAppearance(save), Is.False);
            r.Hair = 7;
            PlayerProfile.StoreAppearance(save, r);
            Garage g = PlayerProfile.LoadGarage(save, 99u);
            Assert.That(g.Owns(VehicleCatalog.Scooter) && g.Owns(VehicleCatalog.Bicycle) && g.Owns(VehicleCatalog.Hatchback), Is.True);
            Assert.That(g.Add(new GarageVehicle(VehicleCatalog.SchoolBus, 0, 5u)), Is.False, "not player drivable");
            Assert.That(g.Add(new GarageVehicle(VehicleCatalog.Tempo, 0, 5u)), Is.True);
            g.Selected = 3;
            PlayerProfile.StoreGarage(save, g);
            string text = save.ToJson().ToString();
            SaveData back = SaveData.FromJson(SaveMigrator.Default().Migrate(Json.Parse(text).AsObject()));
            Assert.That(PlayerProfile.HasAppearance(back), Is.True);
            Assert.That(PlayerProfile.LoadAppearance(back, 1u), Is.EqualTo(r));
            Garage g2 = PlayerProfile.LoadGarage(back, 1u);
            Assert.That(g2.Vehicles.Count, Is.EqualTo(4));
            Assert.That(g2.Current.Variant, Is.EqualTo(VehicleCatalog.Tempo));
            Assert.That(g2.Vehicles[0].PlateSeed, Is.EqualTo(g.Vehicles[0].PlateSeed));
            Assert.That(text, Does.Contain("\"appearance\""));
            Assert.That(text, Does.Contain("\"garage\""));
        }

        [Test]
        public void TheCommunityFleetGoesHomeAfterTenMinutesOr150Metres()
        {
            var loan = new FleetLoan();
            Assert.That(loan.Update(1f, true, 0, 0, 500, 0), Is.False, "never while ridden");
            Assert.That(loan.Update(1f, false, 0, 0, 100, 0), Is.False);
            Assert.That(loan.Update(1f, false, 0, 0, 151, 0), Is.True, "left 150 m behind");
            var idle = new FleetLoan();
            for (int i = 0; i < 599; i++) Assert.That(idle.Update(1f, false, 0, 0, 5, 0), Is.False);
            Assert.That(idle.Update(1.5f, false, 0, 0, 5, 0), Is.True, "10 minutes unused");
        }

        [Test]
        public void PassengersBoardAtStopsRingTheBellAndPayTaxiFares()
        {
            var bus = new AgentPose { AgentId = 5, Class = VehicleClass.Bus, X = 0, Z = 0, SpeedMps = 0f, AnimState = (byte)AgentAnim.AtStop };
            Assert.That(PassengerRide.IsBoardable(bus), Is.True);
            var moving = bus;
            moving.SpeedMps = 5f;
            Assert.That(PassengerRide.IsBoardable(moving), Is.False);
            var notAtStop = bus;
            notAtStop.AnimState = 0;
            Assert.That(PassengerRide.IsBoardable(notAtStop), Is.False, "buses board at stops");
            Assert.That(PassengerRide.IsBoardable(new AgentPose { Class = VehicleClass.Car }), Is.False, "private cars are never ridden");

            var ride = new PassengerRide();
            ride.Board(bus, 3);
            var p = bus;
            for (int i = 0; i < 100; i++)
            {
                p.Z += 1;
                p.SpeedMps = 8f;
                p.AnimState = 0;
                ride.Update(0.1f, true, p, 0, 0);
            }
            ride.RequestStop();
            Assert.That(ride.State, Is.EqualTo(RideState.StopRequested));
            p.SpeedMps = 0.2f;
            ride.Update(0.1f, true, p, 0, 0);
            Assert.That(ride.State, Is.EqualTo(RideState.StopRequested), "a bus stops only at stops");
            p.AnimState = (byte)AgentAnim.AtStop;
            ride.Update(0.1f, true, p, 0, 0);
            Assert.That(ride.State, Is.EqualTo(RideState.Alight));
            Assert.That(ride.StopsPassed, Is.EqualTo(1));
            Assert.That(ride.Fare, Is.EqualTo(0));
            Assert.That(PassengerRide.FareFor(3000), Is.EqualTo(16));

            var poses = new[]
            {
                new AgentPose { AgentId = 1, Class = VehicleClass.Taxi, X = 200, Z = 0 },
                new AgentPose { AgentId = 2, Class = VehicleClass.Taxi, X = 90, Z = 0 },
                new AgentPose { AgentId = 3, Class = VehicleClass.Car, X = 10, Z = 0 },
            };
            Assert.That(PassengerRide.NearestTaxi(poses, 3, 0, 0), Is.EqualTo(1));
            Assert.That(PassengerRide.NearestTaxi(poses, 1, 0, 0), Is.EqualTo(-1), "beyond 150 m");
        }
    }
}
