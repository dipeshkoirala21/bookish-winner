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
    /// flickering). The two meshes of a house are not the same surface (the near ring has recessed windows and shop
    /// fronts, the lite ring and B1 lay them on the wall), so at one pixel the inner band's fragment and the outer band's
    /// lie at different distances from the camera. With complementary dithers evaluated at those two distances both could
    /// be discarded (a hole into the house) or both drawn on one wall (z-fighting), sparkling in a 4 m ring that moves with
    /// the camera. The shader now hands over by stencil (ToonLitInput.hlsl GhBandClip, WorldMaterialDefaults.SetBandOrder):
    /// measured on the real houses at the owner's spots from the rides' chase camera, at the near/lite edge and the
    /// B0/B1 edge on High.
    /// </summary>
    public class WorldRenderingBandSeamTests
    {

        [Test]
        public void BandCrossFadesShowExactlyOneBandPerPixel()
        {
            var report = new StringBuilder();
            double worstHoles = 0, worstDoubles = 0, beforeHoles = 0;
            var tooMany = new StringBuilder();
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                BandConfig bands = BandConfig.ForTier(2);
                BandSeam nearLite = scene.MeasureSeam(scene.Houses, scene.LiteHouses, bands.B0NearOuterM);
                BandSeam b0b1 = scene.MeasureSeam(scene.LiteHouses, scene.B1Houses, bands.B0OuterM);
                report.Append(spot.Name).Append(" near/lite at ").Append(bands.B0NearOuterM).Append(" m: ").Append(nearLite).Append('\n');
                report.Append(spot.Name).Append(" B0/B1 at ").Append(bands.B0OuterM).Append(" m: ").Append(b0b1).Append('\n');
                foreach (BandSeam seam in new[] { nearLite, b0b1 })
                {
                    worstHoles = Math.Max(worstHoles, seam.HoleShare(2));
                    worstDoubles = Math.Max(worstDoubles, seam.DoubleShare(2));
                    beforeHoles = Math.Max(beforeHoles, seam.HoleShare(0));
                    // What is left are places where the two meshes disagree by more than the 4 m ring (open issue).
                    if (seam.HoleShare(2) > 0.4 * seam.HoleShare(0) + 0.0005) tooMany.Append(spot.Name).Append(' ');
                }
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("band_seams.txt", report.ToString());
            Assert.That(beforeHoles, Is.GreaterThan(0.005), "the repro: the old complementary dithers left holes\n" + report);
            Assert.That(tooMany.ToString(), Is.Empty, "the hand-off must remove most holes at every edge:\n" + report);
            Assert.That(worstHoles, Is.LessThan(0.005), "share of fade-ring wall pixels where no band draws (see-through):\n" + report);
            Assert.That(worstDoubles, Is.EqualTo(0.0), "share where both bands draw one wall (z-fighting):\n" + report);
        }

        [Test]
        public void TheShaderHandsOverByStencil()
        {
            string root = GoldenFiles.RepoRoot;
            string dir = Path.Combine(root, "game", "Assets", "Ghumante", "World");
            string hlsl = File.ReadAllText(Path.Combine(dir, "Shaders", "ToonLitInput.hlsl"));
            string shader = File.ReadAllText(Path.Combine(dir, "Shaders", "ToonLit.shader"));
            string materials = File.ReadAllText(Path.Combine(dir, "Rendering", "WorldMaterials.cs"));
            string cells = File.ReadAllText(Path.Combine(dir, "Buildings", "DetailCells.cs"));
            string streamer = File.ReadAllText(Path.Combine(dir, "Streaming", "WorldStreamer.cs"));
            // The incoming band draws its whole fade-in (no dither), the outgoing one dithers out.
            Assert.That(hlsl, Does.Contain("float fadeIn = _BandRange.x <= 0.0 ? 1.0 : d - (_BandRange.x - h);"));
            Assert.That(hlsl, Does.Contain("clip(min(fadeIn, fadeOut - GhBayer4(pixel)));"));
            Assert.That(hlsl, Does.Not.Contain("GH_BAND_OVERLAP"));
            // Shadow casters overlap over the whole shared fade.
            Assert.That(hlsl, Does.Contain("float inner = _BandRange.x <= 0.0 ? -1.0 : _BandRange.x - h;"));
            Assert.That(hlsl, Does.Contain("clip(min(d - inner, (_BandRange.y + h) - d));"));
            // Both lit passes test and mark, the outline pass only tests.
            Assert.That(Regex.Matches(shader, @"Pass \[_BandStencilPass\]").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(shader, @"WriteMask 0\s+Comp \[_BandStencilComp\]\s+Pass Keep").Count, Is.EqualTo(1));
            // Every band material is put in the order, the B0 rings by the detail cells.
            Assert.That(materials, Does.Contain("SetBandOrder(bands[i], orders[i]);"));
            Assert.That(streamer, Does.Contain("WorldMaterialDefaults.SetBandOrder(m, band == BuildingBandLayer.B0 ? ToonLitLayout.BandOrderNear : (int)band + 1);"));
            // While the B0 rings draw with their own material copies (one per ring), each copy takes its ring's order; a
            // single B0 material (houses switched per house) keeps the streamer's B0 order.
            if (cells.Contains("Material RingMaterial("))
                Assert.That(cells, Does.Contain("WorldMaterialDefaults.SetBandOrder(m, layer == NearLayer ? ToonLitLayout.BandOrderNear : ToonLitLayout.BandOrderLite);"));
        }
    }

    /// <summary>What a band edge's cross-fade shows on the inner band's wall patches in its 4 m fade ring, per scheme
    /// (0 complementary, 1 with the 12% overlap, 2 stencil hand-off).</summary>
    internal sealed class BandSeam
    {
        public int Cameras, Patches;
        public double AreaM2;

        /// <summary>Area-weighted pixels where no band draws (the screen door discards every fragment).</summary>
        public readonly double[] Holes = new double[3];

        /// <summary>Area-weighted pixels where both draw at the same depth (within 3 mm: they z-fight).</summary>
        public readonly double[] Doubles = new double[3];

        public double HoleShare(int scheme)
        {
            return AreaM2 > 0 ? Holes[scheme] / AreaM2 : 0;
        }

        public double DoubleShare(int scheme)
        {
            return AreaM2 > 0 ? Doubles[scheme] / AreaM2 : 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} cameras, {1} wall patches ({2:0.0} m²) in the fade ring", Cameras, Patches, AreaM2);
            string[] names = { "complementary (before)", "+12% overlap", "stencil hand-off (now)" };
            for (int i = 0; i < 3; i++)
                sb.AppendFormat(CultureInfo.InvariantCulture, "; {0}: holes {1:0.000%}, coplanar double draws {2:0.000%}", names[i], HoleShare(i), DoubleShare(i));
            return sb.ToString();
        }
    }
}
