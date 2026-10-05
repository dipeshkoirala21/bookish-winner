using System;
using Ghumante.App.Explore;
using Ghumante.Core.Data;
using Ghumante.Core.Search;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// "Ride there" (M1 track D): progress along a route, remaining distance and ETA, the steering direction, re-routing
    /// when off the route, and arrival within about 40 m; plus a real ride from Thamel to Boudhanath on the sample region.
    /// Engine-free.
    /// </summary>
    public class ExploreRouteTests
    {
        private static PlannedRoute Route(double destX, double destZ, params double[] xz)
        {
            return new PlannedRoute { Polyline = xz, LengthM = 0, TimeS = 60, DestinationX = destX, DestinationZ = destZ };
        }

        [Test]
        public void ProgressRemainingAndEtaFollowTheExplorer()
        {
            var guide = new RouteGuide(Route(100, 100, 0, 0, 0, 100, 100, 100));
            Assert.AreEqual(200, guide.TotalM, 1e-9);
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(0, 0, 0.1f));
            Assert.AreEqual(200, guide.RemainingM, 1e-9);
            Assert.AreEqual(60, guide.EtaSeconds, 1e-9);

            guide.Update(2, 50, 0.1f);
            Assert.AreEqual(50, guide.ProgressM, 1e-9);
            Assert.AreEqual(150, guide.RemainingM, 1e-9);
            Assert.AreEqual(45, guide.EtaSeconds, 1e-9, "the route's own time, scaled by what is left");
            Assert.AreEqual(2, guide.DistanceToRouteM, 1e-9);
        }

        [Test]
        public void TheArrowPointsAtTheRouteAhead()
        {
            var guide = new RouteGuide(Route(100, 100, 0, 0, 0, 100, 100, 100));
            guide.Update(0, 50, 0.1f);
            Assert.AreEqual(0f, guide.BearingRad, 1e-5f, "straight on, north");
            Assert.AreEqual(0f, guide.RelativeBearing(0f), 1e-5f);
            guide.Update(0, 90, 0.1f);
            Assert.AreEqual((float)Math.Atan2(20, 10), guide.BearingRad, 1e-5f, "cutting towards the turn ahead");
            Assert.Greater(guide.RelativeBearing(0f), 0f, "to the right of a camera looking north");
            Assert.Less(guide.RelativeBearing((float)(Math.PI / 2)), 0f, "to the left of a camera looking east");
            Assert.AreEqual((float)(Math.Atan2(20, 10) + Math.PI), guide.RelativeBearing((float)Math.PI) + 2f * (float)Math.PI, 1e-4f,
                            "wrapped into [-pi, pi)");
        }

        [Test]
        public void ArrivesWithinFortyMetresOfTheEnd()
        {
            var guide = new RouteGuide(Route(100, 100, 0, 0, 0, 100, 100, 100));
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(55, 100, 0.1f), "45 m to go");
            Assert.IsFalse(guide.Arrived);
            Assert.AreEqual(RouteStatus.Arrived, guide.Update(61, 100, 0.1f), "39 m to go");
            Assert.IsTrue(guide.Arrived);
            Assert.AreEqual(0, guide.RemainingM, 1e-9);
            Assert.AreEqual(0, guide.EtaSeconds, 1e-9);
            Assert.AreEqual(RouteStatus.Arrived, guide.Update(0, 0, 0.1f), "arrival is final");
        }

        [Test]
        public void ArrivesAtTheDestinationEvenOffTheRoadsEnd()
        {
            // The road ends 60 m short of a stupa in a pedestrian square: standing at the end of the road counts.
            var atEnd = new RouteGuide(Route(100, 160, 0, 0, 0, 100, 100, 100));
            Assert.AreEqual(RouteStatus.Arrived, atEnd.Update(100, 100, 0.1f));
            // Walking across the square to the stupa itself counts too.
            var walked = new RouteGuide(Route(100, 160, 0, 0, 0, 100, 100, 100));
            walked.Update(0, 10, 0.1f);
            Assert.AreEqual(RouteStatus.Arrived, walked.Update(100, 150, 0.1f));
        }

        [Test]
        public void OffTheRouteForThreeSecondsAsksForANewWay()
        {
            var guide = new RouteGuide(Route(100, 100, 0, 0, 0, 100, 100, 100));
            guide.Update(0, 0, 0.1f);
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(60, 40, 1f), "60 m off, for a second");
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(60, 40, 1.5f));
            Assert.AreEqual(RouteStatus.OffRoute, guide.Update(60, 40, 1f));
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(1, 40, 0.1f), "back on it");
        }

        [Test]
        public void AnOutAndBackRouteDoesNotJumpAhead()
        {
            // North along x = 0, back south along x = 10 past the start and on: a point between the legs belongs to the
            // leg being ridden, even when it is a little nearer the other one.
            var guide = new RouteGuide(Route(10, -300, 0, 0, 0, 100, 10, 100, 10, -300));
            guide.Update(0, 5, 0.1f);
            guide.Update(5.5, 20, 0.1f);
            Assert.AreEqual(20, guide.ProgressM, 1e-9, "nearer the return leg, but riding the first one");
            for (double z = 20; z <= 100; z += 5) guide.Update(0.5, z, 0.1f);
            guide.Update(10, 100, 0.1f);
            guide.Update(9.5, 60, 0.1f);
            Assert.AreEqual(150, guide.ProgressM, 1e-6, "now on the way back");
            Assert.IsFalse(guide.Arrived);
        }

        [Test]
        public void PassingNearTheEndEarlyIsNotArriving()
        {
            // Out 200 m and back to 30 m beside the start: riding out passes the end of the route.
            var guide = new RouteGuide(Route(1000, 1000, 0, 0, 0, 200, 30, 200, 30, 10));
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(0, 5, 0.1f), "the end is 30 m away, but 400 m of route are left");
            Assert.AreEqual(RouteStatus.OnRoute, guide.Update(0, 100, 0.1f));
            guide.Update(0, 200, 0.1f);
            guide.Update(30, 150, 0.1f);
            Assert.AreEqual(RouteStatus.Arrived, guide.Update(30, 45, 0.1f), "35 m from the end, on the way in");
        }

        [Test]
        public void PointAtWalksAlongTheRoute()
        {
            var guide = new RouteGuide(Route(100, 100, 0, 0, 0, 100, 100, 100));
            double x, z;
            guide.PointAt(150, out x, out z);
            Assert.AreEqual(50, x, 1e-9);
            Assert.AreEqual(100, z, 1e-9);
            guide.PointAt(-5, out x, out z);
            Assert.AreEqual(0, z, 1e-9);
            guide.PointAt(1e9, out x, out z);
            Assert.AreEqual(100, x, 1e-9);
        }

        [Test]
        public void RideFromThamelToBoudhanathOnTheSample()
        {
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            SearchEntry boudha = ExploreSpawn.Best(ExploreSample.Search, "Boudhanath");
            Assert.IsNotNull(thamel);
            Assert.IsNotNull(boudha);
            StringAssert.StartsWith("Boudh", boudha.DisplayName);

            PlannedRoute route = ExploreSample.Planner.Plan(thamel.X, thamel.Z, boudha.X, boudha.Z);
            Assert.IsNotNull(route, "the motorbike network joins Thamel and Boudha");
            Assert.That(route.LengthM, Is.InRange(5000.0, 10000.0), "about 7.5 km of real roads");
            Assert.Greater(route.TimeS, 0.0);
            Assert.AreEqual(boudha.X, route.DestinationX);

            // Ride the polyline in 10 m steps: always on the route, the distance left only shrinks, and it arrives.
            var guide = new RouteGuide(route);
            double[] xz = route.Polyline;
            double previous = double.PositiveInfinity;
            RouteStatus status = RouteStatus.OnRoute;
            int points = xz.Length / 2;
            for (int i = 0; i + 1 < points && status != RouteStatus.Arrived; i++)
            {
                double ax = xz[2 * i], az = xz[2 * i + 1], bx = xz[2 * i + 2], bz = xz[2 * i + 3];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                int steps = Math.Max(1, (int)Math.Ceiling(len / 10.0));
                for (int k = 0; k < steps && status != RouteStatus.Arrived; k++)
                {
                    double t = (double)k / steps;
                    status = guide.Update(ax + (bx - ax) * t, az + (bz - az) * t, 0.5f);
                    Assert.AreNotEqual(RouteStatus.OffRoute, status);
                    Assert.LessOrEqual(guide.RemainingM, previous + 1e-6, "remaining never grows while following the route");
                    previous = guide.RemainingM;
                }
            }
            Assert.AreEqual(RouteStatus.Arrived, status, "arrives near Boudhanath");
        }

        [Test]
        public void PlanningToTheSameSpotGivesAnArrivableRoute()
        {
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            PlannedRoute route = ExploreSample.Planner.Plan(thamel.X, thamel.Z, thamel.X, thamel.Z);
            Assert.IsNotNull(route);
            Assert.GreaterOrEqual(route.Polyline.Length, 4);
            var guide = new RouteGuide(route);
            Assert.AreEqual(RouteStatus.Arrived, guide.Update(thamel.X, thamel.Z, 0.1f));
        }
    }
}
