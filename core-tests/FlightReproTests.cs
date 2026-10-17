using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.World.Instancing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #11 (docs/W2_PLAYTEST_1.md): "Add ... airplane flying ... Allow the user to grab and start riding the
    /// parked vehicles around, including planes." Reproductions written against the base build (before the flight
    /// package): what the explorer finds at TIA's domestic apron, and whether the ambient air traffic can be kept off a
    /// runway the player is using. Kept as regression guards once the flight package lands (they then pass through the
    /// new API).
    /// </summary>
    public class FlightReproTests
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

        /// <summary>At the domestic apron the explorer must find an aircraft to board (before: the world's parked
        /// vehicles there are motorbikes and cars, and the vehicle catalogue has no aircraft at all).</summary>
        [Test]
        public void TheDomesticApronOffersAnAircraftToBoard()
        {
            AviationConfig c = Config();
            double ax = c.DomesticApronX, az = c.DomesticApronZ;
            // What the base game offers: the tiles' parked vehicles (community fleet included) near the apron.
            var parked = new List<string>();
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                if (ax < t.Tile.X0 - 200 || ax > t.Tile.X0 + t.Tile.Size + 200 || az < t.Tile.Z0 - 200 || az > t.Tile.Z0 + t.Tile.Size + 200) continue;
                var list = new List<ParkedVehicle>();
                ParkedPlacement.Place(t, new Ghumante.Core.Meshing.TileHeightSampler(t), list);
                foreach (ParkedVehicle p in list)
                {
                    double dx = t.Tile.X0 + p.X - ax, dz = t.Tile.Z0 + p.Z - az;
                    if (dx * dx + dz * dz < 200.0 * 200.0) parked.Add(VehicleCatalog.At(p.Variant).AssetId);
                }
            }
            string[] shapes = Enum.GetNames(typeof(BodyShape));
            bool catalogueHasAircraft = shapes.Any(n => n.IndexOf("Air", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       n.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       n.IndexOf("Heli", StringComparison.OrdinalIgnoreCase) >= 0);
            TestContext.WriteLine("parked within 200 m of the domestic apron: " + parked.Count + " (" + string.Join(", ", parked.Distinct()) + ")");
            TestContext.WriteLine("vehicle catalogue body shapes: " + string.Join(", ", shapes));

            // The flight package: TIA's stands hold aircraft, and one is within reach of an explorer on the apron.
            FlightStands stands = FlightStands.For(c);
            TestContext.WriteLine("flight stands at TIA: " + stands.Count);
            Assert.That(catalogueHasAircraft || stands.Count > 0, Is.True,
                        "nothing at TIA can be flown: no parked aircraft near the domestic apron and no aircraft in the vehicle catalogue");
            FlightStand nearest = stands.All.OrderBy(s => (s.X - ax) * (s.X - ax) + (s.Z - az) * (s.Z - az)).First();
            FlightSpec spec = FlightSpec.For(nearest.Craft);
            double ex, ez;
            FlightStands.ExitPoint(spec, nearest.X, nearest.Z, nearest.HeadingRad, out ex, out ez);
            FlightStand offered;
            float d;
            Assert.That(stands.TryBoardable(ex, ez, out offered, out d), Is.True, "standing at the door of " + nearest.Ref + " offers boarding");
            Assert.That(offered.Index, Is.EqualTo(nearest.Index));
        }

        /// <summary>While the player taxis and takes off, the ambient traffic must keep off the runway (before: the
        /// schedule rolls a departure or lands an arrival every couple of minutes whatever the player does).</summary>
        [Test]
        public void AmbientTrafficKeepsOffThePlayersRunway()
        {
            AviationConfig c = Config();
            var sim = new AirTrafficSim(c, 7);
            var states = new AircraftState[64];
            // 07:00 to 08:00 at the morning peak, then the player boards and taxis out for ten minutes.
            for (int t = 0; t < 3600; t++) sim.Step(1.0, 7f + t / 3600f, 10, WeatherKind.Clear);
            FlightReproSupport.HoldRunway(sim, true);
            int onRunway = 0;
            var phases = new HashSet<FlightPhase>();
            for (int t = 0; t < 600; t++)
            {
                sim.Step(1.0, 8f + t / 3600f, 10, WeatherKind.Clear);
                int n = sim.CopyStates(states);
                // The first four minutes let aircraft already rolling or about to touch down finish.
                if (t < 240) continue;
                for (int i = 0; i < n; i++)
                {
                    FlightPhase p = states[i].Phase;
                    bool runway = p == FlightPhase.Backtrack || p == FlightPhase.LineUp || p == FlightPhase.TakeoffRoll || p == FlightPhase.Rollout;
                    if (!runway) continue;
                    onRunway++;
                    phases.Add(p);
                }
            }
            TestContext.WriteLine("aircraft-seconds on the runway while the player uses it: " + onRunway + " (" + string.Join(", ", phases) + ")");
            Assert.That(onRunway, Is.EqualTo(0), "ambient aircraft used the runway while the player was on it");
        }
    }
}
