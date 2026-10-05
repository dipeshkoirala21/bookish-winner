using System;
using System.Collections.Generic;
using Ghumante.App.Explore;
using Ghumante.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Search;
using Ghumante.Vehicles;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The M1 acceptance ride without Unity (M1 track D): spawn in Thamel as Explore does, plan "Ride there" to Boudhanath,
    /// and let a simple autopilot ride Core's arcade scooter along the route at a fixed step over the real ground of the
    /// sample region (the level-10 tiles along the way, as the streamer would load them) until the route guide says it
    /// arrived. Engine-free.
    /// </summary>
    public class ExploreRideTests
    {
        [Test]
        public void TheScooterRidesFromThamelToBoudhanath()
        {
            SearchEngine search = ExploreSample.Search;
            RoutePlanner planner = ExploreSample.Planner;
            SpawnPoint spawn = ExploreSpawn.Find(search, planner.Graph, planner.Starts, ExploreSpawn.DefaultPlace, 0, 0);
            SearchEntry boudha = ExploreSpawn.Best(search, "Boudhanath");
            PlannedRoute route = planner.Plan(spawn.X, spawn.Z, boudha.X, boudha.Z);
            Assert.IsNotNull(route);

            // The ground along the route: every level-10 tile within 300 m of it.
            var ground = new TileGroundQuery();
            var loaded = new HashSet<TileId>();
            double[] xz = route.Polyline;
            for (int i = 0; i < xz.Length; i += 2)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        TileId t = TileId.At(10, xz[i] + dx * 300.0, xz[i + 1] + dz * 300.0);
                        if (!loaded.Add(t) || !SampleRegion.Pack.Contains(t)) continue;
                        ground.Add(SampleRegion.Pack.ReadTile(t));
                    }
                }
            }

            var bike = new FixedStepDriver(new ArcadeVehicle(VehicleTuning.Motorbike()));
            bike.Teleport(spawn.X, spawn.Z, spawn.HeadingRad, ground);
            Assert.IsTrue(bike.Current.HasGround, "the spawn stands on loaded ground");
            Assert.IsTrue(bike.Vehicle.OnRoad, "on the road");

            var guide = new RouteGuide(route);
            const float frame = 1f / 30f;
            float time = 0f;
            int recoveries = 0;
            RouteStatus status = RouteStatus.OnRoute;
            while (time < 1500f && status != RouteStatus.Arrived)
            {
                ArcadeVehicle v = bike.Vehicle;
                status = guide.Update(v.X, v.Z, frame);
                if (status == RouteStatus.Arrived) break;
                double px, pz;
                guide.PointAt(guide.ProgressM + 12.0, out px, out pz);
                float target = (float)Math.Atan2(px - v.X, pz - v.Z);
                float error = ArcadeVehicle.WrapAngle(target - v.HeadingRad);
                var controls = new ControlFrame
                {
                    MoveX = Math.Max(-1f, Math.Min(1f, error / 0.35f)),
                    Throttle = Math.Abs(error) > 0.6f ? 0.35f : 0.75f,
                    Brake = Math.Abs(error) > 1.2f && v.SpeedMps > 6f ? 1f : 0f,
                };
                StepEvents events = bike.Advance(frame, ControlMapper.Ride(controls), ground, 0f);
                if ((events & StepEvents.StuckRecovered) != 0) recoveries++;
                time += frame;
            }
            Assert.AreEqual(RouteStatus.Arrived, status,
                            "arrived at Boudhanath (" + guide.RemainingM.ToString("0") + " m left after " + time.ToString("0") + " s)");
            Assert.Less(time, 1200f, "about 7.5 km at city speeds");
            Assert.LessOrEqual(recoveries, 3, "the route is ridable, not a string of stuck recoveries");
            TestContext.WriteLine("Thamel -> Boudhanath: " + route.LengthM.ToString("0") + " m in " + time.ToString("0") +
                                  " s of riding, " + recoveries + " recoveries, " + loaded.Count + " tiles");
        }
    }
}
