using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Aviation;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V10: the golden schedule (one seeded day), runway separation and the path clearance rules.</summary>
    public class AviationTests
    {
        private static AviationConfig _config;

        private static AviationConfig Config()
        {
            if (_config != null) return _config;
            var c = AviationConfig.Parse(File.ReadAllText(DrivingData.SamplePath(".aviation.json")));
            c.AddApronsFrom(DrivingData.SampleTiles().Values);
            _config = c;
            return c;
        }

        [Test]
        public void TheConfigMatchesTheAirport()
        {
            AviationConfig c = Config();
            Assert.That(c.Icao, Is.EqualTo("VNKT"));
            Assert.That(c.Heading02Deg, Is.EqualTo(21.4f).Within(1.5f), "runway 02/20");
            foreach (string p in new[] { "A1", "D1", "D2", "H-E" }) Assert.That(c.Procedures.ContainsKey(p), Is.True, p);
            Assert.That(c.OpenFromHour, Is.EqualTo(6));
            Assert.That(c.OpenToHour, Is.EqualTo(24));
            // Both aprons resolved from the tiles' APRON areas, beside the runway.
            Assert.That(c.DistanceToRunway(c.DomesticApronX, c.DomesticApronZ), Is.InRange(50.0, 1500.0));
            Assert.That(c.DistanceToRunway(c.IntlApronX, c.IntlApronZ), Is.InRange(50.0, 1500.0));
            Assert.Throws<FormatException>(() => AviationConfig.Parse("{\"format\":\"something-else\"}"));
        }

        [Test]
        public void OneSeededDayMatchesTheScheduleAndSeparation()
        {
            AviationConfig c = Config();
            var sim = new AirTrafficSim(c, 99) { KeepLog = true };
            var states = new AircraftState[64];
            for (int t = 0; t < 86400; t++)
            {
                sim.Step(1.0, t / 3600f, 10, WeatherKind.Clear);
                sim.CopyStates(states);
            }
            // W2_DESIGN 8.2 hourly table summed over the day, × 1.10 for October.
            var expected = new Dictionary<ScheduleClass, double>
            {
                { ScheduleClass.DomesticTurboprop, 220 * 1.10 },
                { ScheduleClass.Helicopter, 50 * 1.10 },
                { ScheduleClass.InternationalNarrowbody, 87 * 1.10 },
                { ScheduleClass.InternationalWidebody, 8 * 1.10 },
            };
            foreach (KeyValuePair<ScheduleClass, double> kv in expected)
            {
                double tol = Math.Max(1.0, Math.Round(kv.Value * 0.05));
                Assert.That(sim.Total(kv.Key), Is.EqualTo(kv.Value).Within(tol), kv.Key.ToString());
            }
            var slots = new List<double>();
            sim.RunwaySlots(slots);
            Assert.That(slots.Count, Is.GreaterThan(300));
            for (int i = 1; i < slots.Count; i++)
                Assert.That(slots[i] - slots[i - 1], Is.GreaterThanOrEqualTo(100.0 - 1e-6), "runway separation at " + slots[i]);
            // Nothing scheduled outside opening hours.
            foreach (Movement m in sim.Log)
            {
                double hour = m.EmittedAt / 3600.0;
                Assert.That(hour, Is.InRange(c.OpenFromHour - 1.0, c.OpenToHour), "emitted at " + hour);
            }
        }

        [Test]
        public void PathsKeepTheirClearance()
        {
            AviationConfig c = Config();
            var sim = new AirTrafficSim(c, 1);
            var cases = new[] { ("A1", true, false), ("D1", false, false), ("D2", false, false), ("H-E", true, true), ("H-E", false, true) };
            foreach (var (name, arrival, heli) in cases)
            {
                FlightPath p = sim.PathOf(name, arrival, heli);
                Assert.That(p.Length, Is.GreaterThan(10000f), name);
                for (int i = 0; i < p.N; i++)
                {
                    float agl = p.Y[i] - p.Ground[i];
                    bool final = arrival && p.Length - p.S[i] < 2000f;
                    if (!final) Assert.That(agl, Is.GreaterThanOrEqualTo(0f), name + " AGL at " + p.S[i]);
                    if (c.DistanceToRunway(p.X[i], p.Z[i]) >= FlightPath.ClearanceOutsideM)
                        Assert.That(agl, Is.GreaterThanOrEqualTo(FlightPath.ClearanceM - 0.5f), name + " clearance at " + p.S[i]);
                }
            }
        }

        [Test]
        public void FogAndStormsThinTheSchedule()
        {
            AviationConfig c = Config();
            var sim = new AirTrafficSim(c, 5);
            Assert.That(sim.Factor(ScheduleClass.DomesticTurboprop, 8, 12, WeatherKind.Fog), Is.EqualTo(0f), "fog until 10:00");
            Assert.That(sim.Factor(ScheduleClass.DomesticTurboprop, 15, 7, WeatherKind.Storm), Is.EqualTo(0.85f * 0.7f).Within(1e-4f));
            Assert.That(sim.Factor(ScheduleClass.Helicopter, 15, 7, WeatherKind.Storm), Is.EqualTo(0.85f * 0.4f).Within(1e-4f));
            Assert.That(sim.Factor(ScheduleClass.InternationalNarrowbody, 9, 10, WeatherKind.Clear), Is.EqualTo(1.10f).Within(1e-4f));
        }
    }
}
