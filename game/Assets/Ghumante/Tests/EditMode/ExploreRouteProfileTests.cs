using System;
using System.Collections.Generic;
using Ghumante.App.Explore;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Routes per vehicle (W2 detail pass; owner: "If I am on a car, avoid the roads that have narrower streets through
    /// which cars cannot pass"): the profile for each rig, a car keeping off a galli a scooter takes, the next turn the
    /// HUD chip shows, and a car route across the sample region. Engine-free (no UnityEngine).
    /// </summary>
    public class ExploreRouteProfileTests
    {
        private const Travel Everyone = Travel.Foot | Travel.Bicycle | Travel.Motorbike | Travel.Car | Travel.Jeep | Travel.Bus;
        private const Travel TwoWheelsAndFeet = Travel.Foot | Travel.Bicycle | Travel.Motorbike;

        [Test]
        public void EachRigRoutesWithItsOwnProfile()
        {
            Assert.AreEqual(Travel.Foot, RoutePlanner.ProfileFor(RigClass.Walk));
            Assert.AreEqual(Travel.Bicycle, RoutePlanner.ProfileFor(RigClass.Bicycle));
            Assert.AreEqual(Travel.Motorbike, RoutePlanner.ProfileFor(RigClass.TwoWheeler));
            foreach (RigClass four in new[] { RigClass.Car, RigClass.Van, RigClass.Bus, RigClass.Truck, RigClass.Tractor })
                Assert.AreEqual(Travel.Car, RoutePlanner.ProfileFor(four), four + " uses the GHRG car profile");
            Assert.AreEqual(Travel.Car, RoutePlanner.ProfileFor(RigClass.Passenger, RigClass.Bus), "riding a bus");
            Assert.AreEqual(Travel.Car, RoutePlanner.ProfileFor(RigClass.Passenger, RigClass.Car), "riding a taxi");
            Assert.AreEqual(Travel.Bicycle, RoutePlanner.ProfileFor(RigClass.Passenger, RigClass.Passenger), "a cycle rickshaw");
            Assert.AreEqual(Travel.Car, RoutePlanner.ProfileFor(RigClass.Passenger, RigClass.Walk));
        }

        /// <summary>
        /// S (0, 0) to T (0, 200): straight north through a galli (S, A, T) that only feet and two wheels may use, or round
        /// by a road (S, B, C, T) everyone may use.
        /// </summary>
        private static RouteGraph GalliOrRoad()
        {
            double[] nodes = { 0, 0, 0, 100, 0, 200, 150, 0, 150, 200 }; // S, A, T, B, C
            return Build(nodes, new[]
            {
                (0, 1, TwoWheelsAndFeet, RoadClass.Residential), (1, 2, TwoWheelsAndFeet, RoadClass.Residential),
                (0, 3, Everyone, RoadClass.Residential), (3, 4, Everyone, RoadClass.Residential), (4, 2, Everyone, RoadClass.Residential),
            });
        }

        [Test]
        public void ACarGoesRoundTheGalliAScooterTakes()
        {
            var planner = new RoutePlanner(GalliOrRoad());
            PlannedRoute bike = planner.Plan(0, 0, 0, 200, Travel.Motorbike);
            PlannedRoute car = planner.Plan(0, 0, 0, 200, Travel.Car);
            PlannedRoute walk = planner.Plan(0, 0, 0, 200, Travel.Foot);
            Assert.IsNotNull(bike);
            Assert.IsNotNull(car);
            Assert.IsNotNull(walk);
            Assert.AreEqual(200.0, bike.LengthM, 0.5, "the scooter squeezes through the galli");
            Assert.AreEqual(500.0, car.LengthM, 0.5, "the car goes round by the road");
            Assert.AreEqual(Travel.Car, car.Profile);
            Assert.AreEqual(Travel.Motorbike, bike.Profile);
            for (int i = 0; i < car.Polyline.Length; i += 2)
            {
                double x = car.Polyline[i], z = car.Polyline[i + 1];
                Assert.IsFalse(Math.Abs(x) < 1 && z > 1 && z < 199, "the car route never enters the galli");
            }
            Assert.Greater(walk.TimeS, bike.TimeS, "walking takes longer");
            Assert.AreEqual(Travel.Motorbike, planner.Plan(0, 0, 0, 200).Profile, "the default profile stays the motorbike");
            Assert.IsNotNull(planner.StartsFor(Travel.Car));
            Assert.Throws<ArgumentException>(() => planner.Plan(0, 0, 0, 200, Travel.Car | Travel.Bus));
        }

        [Test]
        public void ACarStartingInAGalliSnapsToTheRoad()
        {
            var planner = new RoutePlanner(GalliOrRoad());
            PlannedRoute car = planner.Plan(0, 100, 150, 200, Travel.Car); // starts at A, inside the galli
            Assert.IsNotNull(car);
            Assert.AreNotEqual(1, car.FromNode, "A has no edge a car may use");
        }

        // ----- The next turn (the HUD chip's arrow) -------------------------------------------------------------------

        private static RouteGuide Guide(params double[] xz)
        {
            return new RouteGuide(new PlannedRoute
            {
                Polyline = xz, TimeS = 120, DestinationX = xz[xz.Length - 2], DestinationZ = xz[xz.Length - 1],
            });
        }

        [Test]
        public void AStraightRouteHasNoTurn()
        {
            RouteGuide g = Guide(0, 0, 0, 1000);
            g.Update(0, 0, 0.1f);
            float turn;
            double d;
            Assert.IsFalse(g.NextTurn(out turn, out d));
            Assert.AreEqual(0f, turn);
            Assert.AreEqual(1000.0, d, 1e-6, "the distance to the end");
        }

        [Test]
        public void RightAndLeftTurnsAreFoundWithTheirDistance()
        {
            RouteGuide right = Guide(0, 0, 0, 200, 300, 200);
            right.Update(0, 0, 0.1f);
            float turn;
            double d;
            Assert.IsTrue(right.NextTurn(out turn, out d));
            Assert.AreEqual(Math.PI / 2, turn, 0.05, "a right turn");
            Assert.AreEqual(200.0, d, 1.0);
            right.Update(0, 120, 0.1f);
            Assert.IsTrue(right.NextTurn(out turn, out d));
            Assert.AreEqual(80.0, d, 1.0, "closer as the explorer rides on");

            RouteGuide left = Guide(0, 0, 0, 200, -300, 200);
            left.Update(0, 0, 0.1f);
            Assert.IsTrue(left.NextTurn(out turn, out d));
            Assert.AreEqual(-Math.PI / 2, turn, 0.05, "a left turn");

            // Past the turn the road runs straight to the end.
            right.Update(60, 200, 0.1f);
            Assert.IsFalse(right.NextTurn(out turn, out d));
            Assert.AreEqual(240.0, d, 1.0);
        }

        [Test]
        public void HairpinsCurvesAndRoundaboutsReadRight()
        {
            float turn;
            double d;
            RouteGuide hairpin = Guide(0, 0, 0, 200, 10, 200, 10, -200);
            hairpin.Update(0, 0, 0.1f);
            Assert.IsTrue(hairpin.NextTurn(out turn, out d));
            Assert.Greater(Math.Abs(turn), 2.6, "a U-turn");

            // A long gentle bend (20 degrees over 100 m) is not a turn.
            var bend = new List<double> { 0, 0 };
            for (int i = 1; i <= 20; i++)
            {
                double a = i * (20.0 / 20.0) * Math.PI / 180.0;
                bend.Add(300 * (1 - Math.Cos(a)));
                bend.Add(100 + 300 * Math.Sin(a));
            }
            bend.Add(bend[bend.Count - 2] + 400 * Math.Sin(20 * Math.PI / 180));
            bend.Add(bend[bend.Count - 2] + 400 * Math.Cos(20 * Math.PI / 180));
            RouteGuide gentle = Guide(bend.ToArray());
            gentle.Update(0, 0, 0.1f);
            Assert.IsFalse(gentle.NextTurn(out turn, out d));

            // A quarter turn round a 20 m roundabout in 3 m steps: one turn to the right, its apex mid-way.
            var round = new List<double> { 0, 0, 0, 100 };
            for (int i = 1; i <= 10; i++)
            {
                double a = i * (Math.PI / 2) / 10;
                round.Add(20 - 20 * Math.Cos(a));
                round.Add(100 + 20 * Math.Sin(a));
            }
            round.Add(220);
            round.Add(120);
            RouteGuide ring = Guide(round.ToArray());
            ring.Update(0, 0, 0.1f);
            Assert.IsTrue(ring.NextTurn(out turn, out d));
            Assert.AreEqual(Math.PI / 2, turn, 0.25, "a right turn");
            Assert.That(d, Is.InRange(100.0, 135.0), "at the roundabout");
        }

        [Test]
        public void ACarCrossesTheSampleRegionOnCarStreetsOnly()
        {
            SearchEntry thamel = ExploreSpawn.Best(ExploreSample.Search, "Thamel");
            SearchEntry boudha = ExploreSpawn.Best(ExploreSample.Search, "Boudhanath");
            PlannedRoute car = ExploreSample.Planner.Plan(thamel.X, thamel.Z, boudha.X, boudha.Z, RoutePlanner.ProfileFor(RigClass.Car));
            Assert.IsNotNull(car, "cars reach Boudha from Thamel");
            Assert.AreEqual(Travel.Car, car.Profile);
            RouteGraph g = ExploreSample.Graph;
            Route r = ExploreSample.Planner.AStar.FindRoute(car.FromNode, car.ToNode, Travel.Car);
            Assert.IsNotNull(r);
            foreach (int e in r.Edges)
            {
                Assert.AreNotEqual(Travel.None, g.EdgeAccess[e] & Travel.Car, "edge " + e + " is not open to cars");
                Assert.IsFalse(TravelProfiles.IsTrail((int)g.EdgeClass[e]), "no footways, paths or steps");
            }
            PlannedRoute walk = ExploreSample.Planner.Plan(thamel.X, thamel.Z, boudha.X, boudha.Z, Travel.Foot);
            Assert.IsNotNull(walk);
            Assert.Greater(walk.TimeS, car.TimeS * 2, "walking there takes much longer");
        }

        // ----- A tiny graph ----------------------------------------------------------------------------------------------

        /// <summary>A GHRG graph from node positions (x, z metres) and two-way ways, with straight edge geometry.</summary>
        private static RouteGraph Build(double[] nodes, (int a, int b, Travel access, RoadClass cls)[] ways)
        {
            var edges = new List<(int s, int t, Travel access, RoadClass cls)>();
            foreach (var w in ways)
            {
                edges.Add((w.a, w.b, w.access, w.cls));
                edges.Add((w.b, w.a, w.access, w.cls));
            }
            edges.Sort((p, q) => p.s != q.s ? p.s.CompareTo(q.s) : p.t.CompareTo(q.t));
            int n = nodes.Length / 2, m = edges.Count;
            var g = new RouteGraph
            {
                NodeXDm = new int[n], NodeZDm = new int[n], NodeElev = new short[n], Offsets = new int[n + 1], EdgeTarget = new int[m],
                EdgeLengthDm = new uint[m], EdgeClass = new RoadClass[m], EdgeSurface = new Surface[m], EdgeAccess = new Travel[m], EdgeFlags = new byte[m],
                EdgeSac = new SacScale[m], EdgeClimb = new short[m], EdgeGeomOffset = new uint[m], EdgeName = new ushort[m], EdgeGeomCount = new ushort[m],
                Names = new NameRecord[0],
            };
            for (int v = 0; v < n; v++)
            {
                g.NodeXDm[v] = (int)Math.Round(nodes[2 * v] * 10);
                g.NodeZDm[v] = (int)Math.Round(nodes[2 * v + 1] * 10);
            }
            var geometry = new List<byte>();
            for (int i = 0; i < m; i++)
            {
                var e = edges[i];
                g.Offsets[e.s + 1]++;
                g.EdgeTarget[i] = e.t;
                long dx = g.NodeXDm[e.t] - g.NodeXDm[e.s], dz = g.NodeZDm[e.t] - g.NodeZDm[e.s];
                g.EdgeLengthDm[i] = (uint)Math.Round(Math.Sqrt((double)dx * dx + (double)dz * dz));
                g.EdgeClass[i] = e.cls;
                g.EdgeSurface[i] = Surface.Asphalt;
                g.EdgeAccess[i] = e.access;
                g.EdgeGeomOffset[i] = (uint)geometry.Count;
                g.EdgeGeomCount[i] = 2;
                Svarint(geometry, 0);
                Svarint(geometry, 0);
                Svarint(geometry, dx);
                Svarint(geometry, dz);
            }
            for (int v = 0; v < n; v++) g.Offsets[v + 1] += g.Offsets[v];
            g.Geometry = geometry.ToArray();
            return g;
        }

        private static void Svarint(List<byte> o, long v)
        {
            ulong z = unchecked((ulong)((v << 1) ^ (v >> 63)));
            while (z >= 0x80)
            {
                o.Add((byte)(z | 0x80));
                z >>= 7;
            }
            o.Add((byte)z);
        }
    }
}
