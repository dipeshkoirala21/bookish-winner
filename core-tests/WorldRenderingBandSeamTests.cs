using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The dithered cross-fade between two building bands drawn as different meshes (owner playtest #4: houses
    /// flickering): at a band edge the inner band fades out and the outer band fades in with the complementary threshold of
    /// the same 4 × 4 screen door, so for one surface every pixel shows exactly one of them. The two meshes of a house are
    /// not the same surface though (the near ring has recessed windows and shop fronts, the lite ring and B1 lay them on
    /// the wall), so at one pixel the inner band's fragment and the outer band's fragment lie at different distances from
    /// the camera and can both be discarded: a hole into the house, sparkling in a 4 m ring that moves with the camera.
    /// Measured on the real houses at the owner's spots from the chase camera of the rides, at the near/lite edge and the
    /// B0/B1 edge on High.
    /// </summary>
    public class WorldRenderingBandSeamTests
    {
        /// <summary>The share of the fade the incoming band draws beyond its complement (GH_BAND_OVERLAP in
        /// ToonLitInput.hlsl, ToonLitLayout.BandOverlap).</summary>
        public const float BandOverlap = 0.12f;

        [Test]
        public void BandCrossFadesLeaveNoHolesInTheHouses()
        {
            var report = new StringBuilder();
            double worst = 0;
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                BandConfig bands = BandConfig.ForTier(2);
                var overlaps = new[] { 0f, BandOverlap };
                BandSeam[] nearLite = scene.MeasureSeam(scene.Houses, scene.LiteHouses, bands.B0NearOuterM, overlaps);
                BandSeam[] b0b1 = scene.MeasureSeam(scene.LiteHouses, scene.B1Houses, bands.B0OuterM, overlaps);
                for (int o = 0; o < overlaps.Length; o++)
                {
                    string label = overlaps[o] > 0f ? "shader" : "no overlap (before)";
                    report.Append(spot.Name).Append(" near/lite at ").Append(bands.B0NearOuterM).Append(" m, ").Append(label).Append(": ").Append(nearLite[o]).Append('\n');
                    report.Append(spot.Name).Append(" B0/B1 at ").Append(bands.B0OuterM).Append(" m, ").Append(label).Append(": ").Append(b0b1[o]).Append('\n');
                    if (overlaps[o] > 0f) worst = Math.Max(worst, Math.Max(nearLite[o].HoleShare, b0b1[o].HoleShare));
                }
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("band_seams.txt", report.ToString());
            Assert.That(worst, Is.LessThan(0.002), "share of fade-ring wall pixels where neither band draws (see-through):\n" + report);
        }

        [Test]
        public void TheShaderAndItsTwinAgreeOnTheOverlap()
        {
            string root = GoldenFiles.RepoRoot;
            string hlsl = File.ReadAllText(Path.Combine(root, "game", "Assets", "Ghumante", "World", "Shaders", "ToonLitInput.hlsl"));
            string layout = File.ReadAllText(Path.Combine(root, "game", "Assets", "Ghumante", "World", "Rendering", "ToonLitLayout.cs"));
            Assert.That(Number(hlsl, @"#define GH_BAND_OVERLAP ([\d.]+)"), Is.EqualTo(BandOverlap).Within(1e-6));
            Assert.That(Number(layout, @"BandOverlap = ([\d.]+)f"), Is.EqualTo(BandOverlap).Within(1e-6));
            Assert.That(Number(hlsl, @"#define GH_BAND_PUSH ([\d.]+)"), Is.EqualTo(Number(layout, @"BandPush = ([\d.]+)f")).Within(1e-9));
            // The overlap only ever widens the incoming band, and the push covers every place both bands may draw: at
            // 80 m it is 8 mm, many depth steps even on a 24-bit buffer (d² / (0.4 m · 2^24) ≈ 1 mm).
            Assert.That(hlsl, Does.Contain("+ GH_BAND_OVERLAP);"));
            double push80 = Number(hlsl, @"#define GH_BAND_PUSH ([\d.]+)") * 80.0;
            Assert.That(push80, Is.GreaterThan(4.0 * 80.0 * 80.0 / (0.4 * 16777216.0)));
        }

        private static double Number(string text, string pattern)
        {
            Match m = Regex.Match(text, pattern);
            Assert.That(m.Success, pattern);
            return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>What a band edge's cross-fade shows on the inner band's wall patches in its 4 m fade ring.</summary>
    internal struct BandSeam
    {
        public int Cameras, Patches;
        public double AreaM2;

        /// <summary>Area-weighted share of pixels where neither band draws (the screen door discards both fragments).</summary>
        public double HoleShare;

        /// <summary>Area-weighted share where both draw at the same depth (within 3 mm: they would z-fight).</summary>
        public double CoplanarDoubleShare;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} cameras, {1} wall patches ({2:0.0} m²) in the fade ring: holes {3:0.000%}, coplanar double draws {4:0.000%}",
                                 Cameras, Patches, AreaM2, HoleShare, CoplanarDoubleShare);
        }
    }
}
