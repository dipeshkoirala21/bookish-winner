using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #12 ("make the buildings very solid"): house surfaces are drawn one-sided (back faces culled), so a
    /// wall seen from behind is not there at all: through a gap in a house's shell, an open box or a single-sided panel the
    /// viewer looks into the house and out the other side. Measured from the rides' chase camera at the owner's spots: rays
    /// over the view whose first hit on the near-ring houses is a back face.
    /// </summary>
    public class WorldRenderingSolidityTests
    {
        [Test]
        public void HouseShellsShowNoBackFacesFromTheStreet()
        {
            var report = new StringBuilder();
            double worst = 0;
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                SeeThrough st = scene.MeasureSeeThrough(scene.Houses, 30, 64, 36, 60f);
                report.Append(spot.Name).Append(": ").Append(st).Append('\n');
                worst = Math.Max(worst, st.FarShare);
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("see_through.txt", report.ToString());
            Assert.That(worst, Is.LessThan(SeeThrough.MaxFarShare), "share of house pixels showing a culled back face (see-through):\n" + report);
        }
    }

    /// <summary>Rays over the chase camera's view and what they first hit on a house mesh.</summary>
    internal sealed class SeeThrough
    {
        /// <summary>At most this share of the house pixels beyond 1.5 m of the lens may be culled back faces. After the
        /// pilasters were closed (0.1-0.5% at the three spots, from 0.16-1.12%; a two-sided panel's back quad no longer
        /// counts when its front quad lies in the same plane) what is left are shell gaps in the grammar: shop boxes deeper
        /// than the house behind them and side walls next to party walls that are not drawn (open issue for the buildings
        /// package, World/README.md). The bound keeps them from growing.</summary>
        public const double MaxFarShare = 0.006;

        public int Cameras, Rays, Hits, NearBack, FarBack;
        public readonly List<string> Examples = new List<string>();
        public readonly List<string> Channels = new List<string>();

        /// <summary>Share of the rays hitting a house that first hit a back face beyond 1.5 m of the lens.</summary>
        public double FarShare => Hits > 0 ? (double)FarBack / Hits : 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} cameras, {1} rays, {2} on houses: back faces beyond 1.5 m {3} ({4:0.000%}), within 1.5 m of the lens {5}",
                            Cameras, Rays, Hits, FarBack, FarShare, NearBack);
            if (Channels.Count > 0) sb.Append("; by channel: ").Append(string.Join(", ", Channels));
            foreach (string e in Examples) sb.Append("\n    ").Append(e);
            return sb.ToString();
        }
    }
}
