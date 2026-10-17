using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The flight package (owner playtest #11): headless take-off, circuit and landing on TIA's runway 02 on the real
    /// kathmandu_core sample, the soft bounce and reset on terrain, water and buildings, boarding and taxiing from the
    /// apron, the stands, the helicopter, the stall and the region's edge. Everything is flown through
    /// <see cref="FlightInput"/> by <see cref="FlightTestPilot"/>.
    /// </summary>
    public class FlightTests
    {
        private const float Deg = (float)(Math.PI / 180.0);
        private static AviationConfig _config;
        private static TileGroundQuery _ground;

        internal static AviationConfig Config()
        {
            if (_config != null) return _config;
            var c = AviationConfig.Parse(File.ReadAllText(DrivingData.SamplePath(".aviation.json")));
            c.AddApronsFrom(DrivingData.SampleTiles().Values);
            _config = c;
            return c;
        }

        internal static TileGroundQuery Ground()
        {
            return _ground ?? (_ground = DrivingData.SampleGround(true));
        }

        internal static FlightWorld World()
        {
            return new FlightWorld(Config(), Ground(), null);
        }

        private static FlightBounds RegionBounds()
        {
            // kathmandu_core's manifest bbox (game metres).
            return FlightBounds.ForRegion(526461.086, 163521.145, 535585.833, 168604.449, Config());
        }

        private static FlightModel LinedUpOn02(FlightCraft craft, out FlightTestPilot pilot)
        {
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(craft), w) { Bounds = RegionBounds() };
            double rx, rz;
            float ry, rh;
            Assert.That(w.TryRunwayStart(Config().Threshold02.X, Config().Threshold02.Z, out rx, out rz, out ry, out rh), Is.True);
            m.PlaceOnGround(rx, rz, rh);
            m.EngineRunning = true;
            pilot = new FlightTestPilot(m, w);
            return m;
        }

        [Test]
        public void TheSpecsMatchTheRealAircraft()
        {
            // Real outer dimensions (ref_flight.md §2) and stall speeds that come out of the derived lift coefficients.
            FlightSpec atr = FlightSpec.For(FlightCraft.Turboprop);
            Assert.That(atr.LengthM, Is.EqualTo(27.17f).Within(0.01f));
            Assert.That(atr.SpanM, Is.EqualTo(27.05f).Within(0.01f));
            Assert.That(atr.StallSpeed(FlightSpec.ReferenceAltitudeM, 0), Is.EqualTo(atr.StallCleanMps).Within(0.05f));
            Assert.That(atr.StallSpeed(FlightSpec.ReferenceAltitudeM, 2), Is.EqualTo(atr.StallFullMps).Within(0.05f));
            foreach (FlightCraft c in FlightSpec.All)
            {
                FlightSpec s = FlightSpec.For(c);
                Assert.That(s.LengthM, Is.GreaterThan(10f), c.ToString());
                if (s.Rotorcraft) continue;
                Assert.That(s.StallAlpha(0), Is.InRange(10f * Deg, 20f * Deg), c + " stalls at a believable angle");
                Assert.That(s.StallAlpha(2), Is.InRange(10f * Deg, 20f * Deg), c + " with flaps");
                Assert.That(s.PowerEta, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void TakeoffFromRunway02ClimbsAway()
        {
            FlightTestPilot p;
            FlightModel m = LinedUpOn02(FlightCraft.Turboprop, out p);
            double s0, l0;
            p.ToRunway(m.X, m.Z, out s0, out l0);
            double liftS = double.NaN;
            FlightEvents all = FlightEvents.None;
            bool gearUp = false;
            for (int k = 0; k < 60 * 120; k++)
            {
                FlightInput i;
                if (m.OnGround)
                {
                    double s, l;
                    p.ToRunway(m.X, m.Z, out s, out l);
                    float hErr = ArcadeVehicle.WrapAngle(p.RunwayHeading - m.Heading);
                    i = new FlightInput(m.Airspeed > m.Spec.RotateMps ? 0.7f : 0f, 0f, Clamp(hErr * 3f - (float)l * 0.05f, -1f, 1f), 1f);
                }
                else
                {
                    i = p.Hold(6f * Deg, 1.35f * m.Spec.StallCleanMps, p.RunwayHeading);
                    i.Throttle = 1f;
                    if (m.Agl > 120f && m.Flaps > 0) i.FlapsUp = true;
                }
                FlightEvents ev = m.Advance(FlightModel.StepS, i);
                all |= ev;
                if ((ev & FlightEvents.LiftOff) != 0)
                {
                    double s, l;
                    p.ToRunway(m.X, m.Z, out s, out l);
                    liftS = s;
                }
                if (m.Gear <= 0f) gearUp = true;
                if (m.Agl > 300f) break;
            }
            TestContext.WriteLine("lift-off " + (liftS - s0).ToString("0") + " m down the runway; at 300 m AGL after; events " + all);
            Assert.That(double.IsNaN(liftS), Is.False, "lifted off");
            Assert.That(liftS - s0, Is.InRange(500.0, 1600.0), "a believable take-off roll at 1,340 m");
            Assert.That(m.Agl, Is.GreaterThan(300f), "climbed to 300 m");
            Assert.That(gearUp, Is.True, "the gear came up");
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetAir | FlightEvents.ResetRunway), Is.EqualTo(FlightEvents.None));
        }

        [TestCase(FlightCraft.Turboprop)]
        [TestCase(FlightCraft.TurbopropStretch)]
        [TestCase(FlightCraft.Commuter)]
        [TestCase(FlightCraft.Stol)]
        public void CircuitAndLandingOnRunway02(FlightCraft craft)
        {
            FlightTestPilot p;
            FlightModel m = LinedUpOn02(craft, out p);
            FlightSpec spec = m.Spec;
            double len = p.RunwayLength;
            float circuitAlt = Config().ElevationM + 300f;
            float cruise = 1.45f * spec.StallCleanMps, vref = 1.3f * spec.StallFullMps;
            // Right-hand circuit east of the runway (the domestic visual circuit side, aviation research §5.2 A5).
            double[,] legs =
            {
                { 0, 0, len + 600, 0 }, // climb out
                { len + 600, 0, len + 1500, 1800 }, // crosswind
                { len + 1500, 1800, -2400, 1800 }, // downwind
                { -2400, 1800, -3600, 0 }, // base
            };
            int leg = 0;
            bool onFinal = false, landed = false, stopped = false;
            double tdS = double.NaN, tdL = double.NaN;
            float tdSink = float.NaN, maxAgl = 0f;
            FlightEvents all = FlightEvents.None;
            int steps = 0;
            for (; steps < 60 * 60 * 15 && !stopped; steps++)
            {
                FlightInput i;
                double s, l;
                p.ToRunway(m.X, m.Z, out s, out l);
                if (!onFinal && m.OnGround && leg == 0)
                {
                    float hErr = ArcadeVehicle.WrapAngle(p.RunwayHeading - m.Heading);
                    i = new FlightInput(m.Airspeed > spec.RotateMps ? 0.7f : 0f, 0f, Clamp(hErr * 3f - (float)l * 0.05f, -1f, 1f), 1f);
                }
                else if (!onFinal)
                {
                    double ax, az, bx, bz;
                    p.FromRunway(legs[leg, 0], legs[leg, 1], out ax, out az);
                    p.FromRunway(legs[leg, 2], legs[leg, 3], out bx, out bz);
                    float alt = leg == 3 ? circuitAlt - 90f : circuitAlt;
                    i = p.HoldAltitude(alt, leg == 0 && m.Agl < 150f ? 1.3f * spec.StallCleanMps : cruise, p.LegHeading(ax, az, bx, bz));
                    if (leg == 0 && m.Agl < 100f) i.Throttle = 1f;
                    if (leg == 0 && m.Agl > 150f && m.Flaps > 0) i.FlapsUp = true;
                    if (leg == 2 && m.Flaps < 1 && s < len) i.FlapsDown = true;
                    // Turn onto the next leg a turn radius early (onto final a little earlier still).
                    if (p.LegRemaining(ax, az, bx, bz) < (leg == 3 ? 900 : 400)) leg++;
                    if (leg == 4)
                    {
                        onFinal = true;
                        leg = 3;
                    }
                }
                else
                {
                    i = p.Final(350, vref);
                    if (m.Flaps < 2) i.FlapsDown = true;
                }
                double vyBefore = m.Vy;
                float bankBefore = m.Bank, pitchBefore = m.Pitch, speedBefore = m.Airspeed;
                FlightEvents ev = m.Advance(FlightModel.StepS, i);
                all |= ev;
                maxAgl = Math.Max(maxAgl, m.Agl);
                if ((ev & FlightEvents.Bounce) != 0)
                {
                    double bs, bl;
                    p.ToRunway(m.X, m.Z, out bs, out bl);
                    TestContext.WriteLine("bounce (" + m.LastBounceCause + ") at s " + bs.ToString("0") + " l " + bl.ToString("0") + ", leg " + leg +
                                          (onFinal ? " final" : "") + ", vy " + vyBefore.ToString("0.0") + ", bank " + (bankBefore / Deg).ToString("0") +
                                          ", pitch " + (pitchBefore / Deg).ToString("0") + ", speed " + speedBefore.ToString("0") + ", surface " + m.Surface);
                }
                if ((ev & FlightEvents.Touchdown) != 0 && !landed)
                {
                    landed = true;
                    p.ToRunway(m.X, m.Z, out tdS, out tdL);
                    tdSink = m.LastTouchdownSinkMps;
                }
                if (landed && m.OnGround && m.Airspeed < 0.5f) stopped = true;
                if (onFinal && !landed && steps % 600 == 0)
                    TestContext.WriteLine("  final s " + s.ToString("0") + " l " + l.ToString("0.0") + " agl " + m.Agl.ToString("0") + " v " +
                                          m.Airspeed.ToString("0.0") + " vs " + m.Vy.ToString("0.0") + " bank " + (m.Bank / Deg).ToString("0") + " hdg-err " +
                                          (ArcadeVehicle.WrapAngle(m.Heading - p.RunwayHeading) / Deg).ToString("0.0"));
            }
            double sEnd, lEnd;
            p.ToRunway(m.X, m.Z, out sEnd, out lEnd);
            TestContext.WriteLine(craft + ": max " + maxAgl.ToString("0") + " m AGL; touchdown " + tdS.ToString("0") + " m past THR 02, " +
                                  tdL.ToString("0.0") + " m off the centreline, sink " + tdSink.ToString("0.00") + " m/s; stopped at " +
                                  sEnd.ToString("0") + " m after " + (steps / 60f).ToString("0") + " s; events " + all);
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetAir | FlightEvents.ResetRunway), Is.EqualTo(FlightEvents.None), "no bounce or reset");
            Assert.That(maxAgl, Is.InRange(220f, 420f), "flew the circuit at about 300 m");
            Assert.That(landed, Is.True, "touched down");
            Assert.That(tdS, Is.InRange(0.0, 1200.0), "in the touchdown zone of 02");
            Assert.That(Math.Abs(tdL), Is.LessThan(12.0), "on the centreline");
            Assert.That(tdSink, Is.LessThan(2.5f), "a gentle landing");
            Assert.That(stopped, Is.True, "stopped");
            Assert.That(sEnd, Is.LessThan(len), "on the runway");
            Assert.That(World().OnRunway(m.X, m.Z), Is.True);
        }

        [Test]
        public void FlyingIntoTheGroundBouncesSoftlyAndResetsToASafeAltitude()
        {
            // Over Tundikhel (4 km from the runway), nose down into the ground at cruise speed.
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w) { Bounds = RegionBounds() };
            double x, z;
            WorldFrame.LonLatToGame(85.3155, 27.7015, out x, out z);
            FlightGroundSample g;
            w.Ground(x, z, 1e6f, out g);
            m.PlaceInAir(x, g.Height + 120f, z, 180f * Deg, 75f);
            FlightEvents all = FlightEvents.None;
            bool bounced = false;
            for (int k = 0; k < 60 * 20; k++)
            {
                FlightEvents ev = m.Advance(FlightModel.StepS, new FlightInput(-1f, 0f, 0f, 0.8f));
                all |= ev;
                if ((ev & FlightEvents.Bounce) != 0) bounced = true;
                Assert.That(double.IsNaN(m.X + m.Y + m.Z), Is.False);
                if ((ev & (FlightEvents.ResetAir | FlightEvents.ResetRunway)) != 0) break;
            }
            TestContext.WriteLine("events " + all + "; cause " + m.LastBounceCause + "; after reset " + m.Agl.ToString("0") + " m AGL at " +
                                  m.Airspeed.ToString("0") + " m/s");
            Assert.That(bounced, Is.True, "the ground contact was a soft bounce");
            Assert.That(m.LastBounceCause, Is.Not.EqualTo(FlightBounceCause.None));
            Assert.That(all & FlightEvents.ResetAir, Is.EqualTo(FlightEvents.ResetAir), "back up at a safe altitude");
            Assert.That(m.Agl, Is.GreaterThan(250f));
            Assert.That(m.OnGround, Is.False);
            Assert.That(m.Airspeed, Is.GreaterThan(1.3f * m.Spec.StallCleanMps));
            // And it flies on: a minute later it is still airborne and well clear.
            for (int k = 0; k < 60 * 60; k++) m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, m.Throttle));
            Assert.That(m.Agl, Is.GreaterThan(150f), "keeps flying after the reset");
            Assert.That(m.ResetCount, Is.EqualTo(1));
        }

        [Test]
        public void AHardLandingNearTheRunwayResetsToTheRunwayLinedUp()
        {
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w) { Bounds = RegionBounds() };
            var p = new FlightTestPilot(m, w);
            double x, z;
            p.FromRunway(900, 0, out x, out z);
            FlightGroundSample g;
            w.Ground(x, z, 1e6f, out g);
            m.PlaceInAir(x, g.Height + 40f, z, p.RunwayHeading, 65f);
            FlightEvents all = FlightEvents.None;
            for (int k = 0; k < 60 * 15; k++)
            {
                all |= m.Advance(FlightModel.StepS, new FlightInput(-1f, 0f, 0f, 0f));
                if ((all & FlightEvents.ResetRunway) != 0) break;
            }
            TestContext.WriteLine("events " + all + "; cause " + m.LastBounceCause);
            Assert.That(all & FlightEvents.Bounce, Is.EqualTo(FlightEvents.Bounce));
            Assert.That(m.LastBounceCause, Is.EqualTo(FlightBounceCause.HardLanding));
            Assert.That(all & FlightEvents.ResetRunway, Is.EqualTo(FlightEvents.ResetRunway));
            Assert.That(m.OnGround, Is.True);
            Assert.That(w.OnRunway(m.X, m.Z), Is.True);
            Assert.That(Math.Abs(ArcadeVehicle.WrapAngle(m.Heading - p.RunwayHeading)), Is.LessThan(1f * Deg), "lined up for 02");
            Assert.That(m.Airspeed, Is.LessThan(0.1f));
        }

        [Test]
        public void FlyingIntoBuildingsBouncesSoftlyAndResets()
        {
            // Low over the old town (Asan to Indra Chowk): the houses stand taller than the aircraft flies.
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w) { Bounds = RegionBounds() };
            double x, z;
            WorldFrame.LonLatToGame(85.3117, 27.7095, out x, out z);
            FlightGroundSample g;
            w.Ground(x, z, 1e6f, out g);
            m.PlaceInAir(x, g.Height + 6f, z, 200f * Deg, 70f);
            FlightEvents all = FlightEvents.None;
            for (int k = 0; k < 60 * 20; k++)
            {
                all |= m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, 0.7f));
                if ((all & (FlightEvents.ResetAir | FlightEvents.ResetRunway)) != 0) break;
            }
            TestContext.WriteLine("events " + all + "; cause " + m.LastBounceCause + "; " + m.Agl.ToString("0") + " m AGL after");
            Assert.That(all & FlightEvents.Bounce, Is.EqualTo(FlightEvents.Bounce));
            Assert.That(m.LastBounceCause, Is.EqualTo(FlightBounceCause.Building).Or.EqualTo(FlightBounceCause.Terrain));
            Assert.That(all & FlightEvents.ResetAir, Is.EqualTo(FlightEvents.ResetAir));
            Assert.That(m.Agl, Is.GreaterThan(250f));
        }

        [Test]
        public void BoardingFromTheApronAndTaxiingOut()
        {
            AviationConfig c = Config();
            FlightStands stands = FlightStands.For(c);
            FlightWorld w = World();
            // Every stand at the domestic apron is offered from its door and stands on the apron's ground.
            int domestic = 0;
            foreach (FlightStand s in stands.All)
            {
                FlightSpec spec = FlightSpec.For(s.Craft);
                double ex, ez;
                FlightStands.ExitPoint(spec, s.X, s.Z, s.HeadingRad, out ex, out ez);
                FlightStand offered;
                float d;
                Assert.That(stands.TryBoardable(ex, ez, out offered, out d), Is.True, s.Ref + " offered at its door");
                Assert.That(offered.Index, Is.EqualTo(s.Index), s.Ref);
                if (s.Ref.StartsWith("D", StringComparison.Ordinal)) domestic++;
            }
            Assert.That(domestic, Is.GreaterThanOrEqualTo(7));

            // Board the ATR at D2 and taxi out towards the runway for 40 s at a walking-to-jogging pace.
            FlightStand d2 = stands.All.First(s => s.Ref == "D2");
            stands.Take(d2.Index);
            var m = new FlightModel(FlightSpec.For(d2.Craft), w) { Bounds = RegionBounds() };
            m.PlaceOnGround(d2.X, d2.Z, d2.HeadingRad);
            m.EngineRunning = true;
            GroundSample gs;
            Assert.That(Ground().TrySample(d2.X, d2.Z, out gs), Is.True, "D2 is on resident ground");
            Assert.That(m.Y, Is.EqualTo(gs.Height).Within(0.05), "the wheels stand on the apron");
            FlightEvents all = FlightEvents.None;
            for (int k = 0; k < 60 * 40; k++)
            {
                float throttle = m.Airspeed < 6f ? 0.35f : 0.05f;
                all |= m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, throttle));
            }
            double moved = Math.Sqrt((m.X - d2.X) * (m.X - d2.X) + (m.Z - d2.Z) * (m.Z - d2.Z));
            TestContext.WriteLine("taxied " + moved.ToString("0") + " m in 40 s; events " + all);
            Assert.That(moved, Is.GreaterThan(80.0), "taxied out");
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetRunway | FlightEvents.ResetAir), Is.EqualTo(FlightEvents.None));
            Assert.That(m.OnGround, Is.True);
            // Stop and park it there: the stand moves with it, and the explorer can board it again.
            for (int k = 0; k < 60 * 20 && !m.CanExit; k++) m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, 0f, 1f));
            Assert.That(m.CanExit, Is.True, "stopped: the pilot may get out");
            stands.Park(d2.Index, m.X, m.Z, m.Heading);
            double qx, qz;
            FlightStands.ExitPoint(m.Spec, m.X, m.Z, m.Heading, out qx, out qz);
            FlightStand again;
            float dd;
            Assert.That(stands.TryBoardable(qx, qz, out again, out dd), Is.True);
            Assert.That(again.Index, Is.EqualTo(d2.Index));
        }

        [Test]
        public void TheStandsAreClearOfEachOtherTheRunwayTheAmbientTrafficAndBuildings()
        {
            AviationConfig c = Config();
            FlightStands stands = FlightStands.For(c);
            FlightWorld w = World();
            TileGroundQuery ground = Ground();
            Procedure he = c.Procedures["H-E"];
            var ambient = new List<(double, double, string)>
            {
                (c.DomesticApronX, c.DomesticApronZ, "domestic apron parking"), (c.IntlApronX, c.IntlApronZ, "international apron parking"),
                (he.Points[0].X, he.Points[0].Z, "helicopter lift-off"),
            };
            foreach (var a in ambient)
            {
                double lo, la;
                WorldFrame.GameToLonLat(a.Item1, a.Item2, out lo, out la);
                TestContext.WriteLine(a.Item3 + " at " + lo.ToString("0.000000") + ", " + la.ToString("0.000000"));
            }
            var all = stands.All.ToList();
            foreach (FlightStand s in all)
            {
                FlightSpec spec = FlightSpec.For(s.Craft);
                double runway = w.DistanceToRunway(s.X, s.Z);
                double lon, lat;
                WorldFrame.GameToLonLat(s.X, s.Z, out lon, out lat);
                TestContext.WriteLine(s.Ref + " " + s.Craft + " at " + lon.ToString("0.000000") + ", " + lat.ToString("0.000000") + " heading " +
                                      (s.HeadingRad / Deg).ToString("0") + "; runway " + runway.ToString("0") + " m");
                Assert.That(runway, Is.GreaterThan(90.0), s.Ref + " stands off the runway strip");
                foreach (var a in ambient)
                {
                    double d = Math.Sqrt((s.X - a.Item1) * (s.X - a.Item1) + (s.Z - a.Item2) * (s.Z - a.Item2));
                    Assert.That(d, Is.GreaterThan(FlightStands.AmbientClearanceM), s.Ref + " clear of the " + a.Item3);
                }
                foreach (FlightStand o in all)
                {
                    if (o.Index <= s.Index) continue;
                    FlightSpec os = FlightSpec.For(o.Craft);
                    double d = Math.Sqrt((s.X - o.X) * (s.X - o.X) + (s.Z - o.Z) * (s.Z - o.Z));
                    Assert.That(d, Is.GreaterThan(0.5 * (spec.SpanM + os.SpanM) * 0.75), s.Ref + " and " + o.Ref + " do not overlap");
                }
                GroundSample g;
                if (!ground.TrySample(s.X, s.Z, out g))
                {
                    // Only the charter helipad lies outside the sample's tiles (south of 27.69°N).
                    Assert.That(s.Ref, Is.EqualTo("Helipad"), s.Ref + " has resident ground");
                    continue;
                }
                // No building, wall or structure where the fuselage stands.
                float t;
                FlightObstacle hit;
                double fx = Math.Sin(s.HeadingRad) * 0.5 * spec.LengthM, fz = Math.Cos(s.HeadingRad) * 0.5 * spec.LengthM;
                double cy = g.Height + spec.FuselageCentreM;
                bool blocked = w.Sweep(s.X - fx, cy, s.Z - fz, s.X + fx, cy, s.Z + fz, spec.FuselageRadiusM, out t, out hit) && hit == FlightObstacle.Structure;
                Assert.That(blocked, Is.False, s.Ref + " fuselage is clear of solids");
            }
        }

        [Test]
        public void TheHelicopterLiftsOffHoversFliesAndLands()
        {
            AviationConfig c = Config();
            FlightStands stands = FlightStands.For(c);
            FlightStand h = stands.All.First(s => s.Craft == FlightCraft.Helicopter && s.Ref.StartsWith("D", StringComparison.Ordinal));
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Helicopter), w) { Bounds = RegionBounds() };
            m.PlaceOnGround(h.X, h.Z, h.HeadingRad);
            m.EngineRunning = true;
            FlightEvents all = FlightEvents.None;
            // Rotor spools up, then collective up: lift-off and a hover at ~30 m.
            for (int k = 0; k < 60 * 8; k++) all |= m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, 0f));
            Assert.That(m.OnGround, Is.True, "stays down at idle collective");
            Assert.That(m.RotorRpm, Is.GreaterThan(0.99f));
            for (int k = 0; k < 60 * 12; k++)
            {
                float col = m.Agl < 30f ? 0.65f : FlightModel.HoverCollective - (float)m.Vy * 0.1f;
                all |= m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, col));
            }
            Assert.That(all & FlightEvents.LiftOff, Is.EqualTo(FlightEvents.LiftOff));
            Assert.That(m.Agl, Is.InRange(20f, 60f), "hovering");
            Assert.That(m.Airspeed, Is.LessThan(4f));
            // Nose down: it flies forward and speeds up.
            for (int k = 0; k < 60 * 15; k++)
            {
                float col = FlightModel.HoverCollective + (float)(80f - m.Agl) * 0.01f - (float)m.Vy * 0.08f;
                all |= m.Advance(FlightModel.StepS, new FlightInput(-1f, 0f, 0f, col));
            }
            Assert.That(m.Airspeed, Is.GreaterThan(30f), "forward flight");
            // Pull back to stop, then let it down gently.
            bool down = false;
            for (int k = 0; k < 60 * 90 && !down; k++)
            {
                float pitch = m.Airspeed > 2f ? 1f : 0f;
                float sinkWant = m.Agl > 15f ? -3f : -1f;
                float col = FlightModel.HoverCollective + (sinkWant - (float)m.Vy) * 0.08f;
                FlightEvents ev = m.Advance(FlightModel.StepS, new FlightInput(pitch, 0f, 0f, col));
                all |= ev;
                if ((ev & FlightEvents.Touchdown) != 0) down = true;
            }
            TestContext.WriteLine("events " + all + "; touchdown sink " + m.LastTouchdownSinkMps.ToString("0.00"));
            Assert.That(down, Is.True, "landed");
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetAir | FlightEvents.ResetRunway), Is.EqualTo(FlightEvents.None));
            for (int k = 0; k < 60 * 3; k++) m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, 0f));
            Assert.That(m.CanExit, Is.True);
        }

        [Test]
        public void TheStallWarnsAndRecoversGently()
        {
            FlightWorld w = World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w) { Bounds = RegionBounds() };
            var p = new FlightTestPilot(m, w);
            double x, z;
            p.FromRunway(1000, 3000, out x, out z);
            m.PlaceInAir(x, Config().ElevationM + 900f, z, p.RunwayHeading, 75f);
            FlightEvents all = FlightEvents.None;
            // Power off, stick fully back: the warning comes first, then the stall.
            for (int k = 0; k < 60 * 30; k++)
            {
                all |= m.Advance(FlightModel.StepS, new FlightInput(1f, 0f, 0f, 0f));
                if (m.Stalled) break;
            }
            Assert.That(all & FlightEvents.StallWarning, Is.EqualTo(FlightEvents.StallWarning), "warned");
            Assert.That(all & FlightEvents.Stall, Is.EqualTo(FlightEvents.Stall), "stalled");
            float lost = (float)m.Y;
            // Keep pulling: the nose drops by itself and it keeps flying (no spin, no crash), then power and release.
            for (int k = 0; k < 60 * 5; k++) all |= m.Advance(FlightModel.StepS, new FlightInput(1f, 0f, 0f, 0f));
            for (int k = 0; k < 60 * 20; k++) all |= m.Advance(FlightModel.StepS, new FlightInput(0f, 0f, 0f, 1f));
            TestContext.WriteLine("lost " + (lost - m.Y).ToString("0") + " m; " + m.Airspeed.ToString("0") + " m/s; events " + all);
            Assert.That(m.Stalled, Is.False, "recovered");
            Assert.That(m.Airspeed, Is.GreaterThan(1.1f * m.Spec.StallSpeed((float)m.Y, m.Flaps)));
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetAir), Is.EqualTo(FlightEvents.None), "recovered in the air");
            Assert.That(Math.Abs(m.Bank), Is.LessThan(15f * Deg));
        }

        [Test]
        public void TheRegionsEdgeTurnsTheAircraftBack()
        {
            FlightWorld w = World();
            FlightBounds b = RegionBounds();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w) { Bounds = b };
            // Westbound towards the western edge, stick neutral.
            m.PlaceInAir(b.MinX + b.MarginM + 1500, Config().ElevationM + 700f, b.CentreZ, 270f * Deg, 80f);
            FlightEvents all = FlightEvents.None;
            double minX = double.MaxValue;
            for (int k = 0; k < 60 * 180; k++)
            {
                FlightInput i = new FlightInput(0f, 0f, 0f, 0.6f);
                FlightEvents ev = m.Advance(FlightModel.StepS, i);
                all |= ev;
                minX = Math.Min(minX, m.X);
                if ((ev & FlightEvents.Bounce) != 0)
                    TestContext.WriteLine("bounce (" + m.LastBounceCause + ") at t " + (k / 60f).ToString("0") + " s, x-edge " + (m.X - b.MinX).ToString("0") + ", y " +
                                          m.Y.ToString("0") + ", surface " + m.Surface);
                if (k % 600 == 0)
                    TestContext.WriteLine("t " + (k / 60) + " s: x-edge " + (m.X - b.MinX).ToString("0") + " y " + m.Y.ToString("0") + " agl " + m.Agl.ToString("0") +
                                          " hdg " + (m.Heading / Deg).ToString("0") + " bank " + (m.Bank / Deg).ToString("0") + " v " + m.Airspeed.ToString("0"));
            }
            float back = ArcadeVehicle.WrapAngle(m.Heading - 90f * Deg);
            TestContext.WriteLine("closest to the west edge " + (minX - b.MinX).ToString("0") + " m; heading now " + (m.Heading / Deg).ToString("0") +
                                  "; events " + all);
            Assert.That(all & FlightEvents.TurnBack, Is.EqualTo(FlightEvents.TurnBack));
            Assert.That(minX, Is.GreaterThan(b.MinX), "never left the region");
            Assert.That(m.X, Is.GreaterThan(minX + 500), "flying back in");
            Assert.That(all & (FlightEvents.Bounce | FlightEvents.ResetAir), Is.EqualTo(FlightEvents.None));
        }

        [Test]
        public void FlightIsDeterministic()
        {
            double a = Fly(), b = Fly();
            Assert.That(a, Is.EqualTo(b));
        }

        private static double Fly()
        {
            FlightTestPilot p;
            FlightModel m = LinedUpOn02(FlightCraft.Stol, out p);
            for (int k = 0; k < 60 * 60; k++)
            {
                var i = new FlightInput((float)Math.Sin(k * 0.01), (float)Math.Sin(k * 0.013) * 0.5f, 0f, 1f);
                m.Advance(1f / 47f, i);
            }
            return m.X * 31 + m.Y * 17 + m.Z + m.Heading;
        }

        private static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }
    }
}
