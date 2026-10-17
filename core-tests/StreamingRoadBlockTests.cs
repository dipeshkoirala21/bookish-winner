using System;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The per-frame road-block rule the streamer applies to every detail view (<see cref="RoadBlocks.UpdateMasks"/>,
    /// <see cref="RoadBlocks.BlockIn"/>, <see cref="RoadBlocks.PartShown"/>, the same calls <c>WorldStreamer.UpdateBands</c>
    /// makes): a block's full kit within the B1 radius, its cheap copy out to the far radius (B2; 250 m on Low), never both, and each switch
    /// held by <see cref="RoadBlocks.HysteresisM"/> so a camera on the line never makes a block flicker.
    /// </summary>
    public class StreamingRoadBlockTests
    {
        private const double TileM = 1024;

        [Test]
        public void ABlockSwitchesCopiesAtTheRadiiWithHysteresisAndNeverShowsBoth([Values(0, 1, 2)] int tier)
        {
            float nearM, farM;
            RoadBlocks.Radii(BandConfig.ForTier(tier), StreamingConfig.ForTier(tier), out nearM, out farM);
            // Block 0 is [0, 128) x [0, 128); the camera walks out east along z = 64 and back.
            const int block = 0;
            const double edgeX = RoadBlocks.BlockM;
            ulong near = 0, far = 0;
            int state = -1, switches = 0;
            double lastSwitchOut = 0, lastSwitchIn = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i <= 2000; i++)
                {
                    double d = pass == 0 ? i * 0.5 : (2000 - i) * 0.5; // metres beyond the block's east edge
                    double cx = edgeX + d, cz = 64;
                    RoadBlocks.UpdateMasks(TileM, cx, cz, nearM, farM, ref near, ref far);
                    bool full = RoadBlocks.PartShown(block, near, far, true, TileM, cx, cz, nearM, farM);
                    bool cheap = RoadBlocks.PartShown(RoadBlocks.FarPart + block, near, far, true, TileM, cx, cz, nearM, farM);
                    bool decals = RoadBlocks.BlockIn(near, block, TileM, cx, cz, nearM);
                    Assert.That(full && cheap, Is.False, "never both copies, at " + d + " m");
                    Assert.That(decals, Is.EqualTo(full), "the markings go with the full kit, at " + d + " m");
                    int s = full ? 2 : cheap ? 1 : 0;
                    if (d <= nearM) Assert.That(s, Is.EqualTo(2), "full kit within the B1 radius, at " + d + " m");
                    else if (d > nearM + RoadBlocks.HysteresisM && d <= farM) Assert.That(s, Is.EqualTo(1), "cheap copy out to the far radius, at " + d + " m");
                    else if (d > farM + RoadBlocks.HysteresisM) Assert.That(s, Is.EqualTo(0), "no road beyond, at " + d + " m");
                    if (state >= 0 && s != state)
                    {
                        switches++;
                        if (pass == 0) lastSwitchOut = d;
                        else lastSwitchIn = d;
                        if (pass == 0) // going out, a copy is let go only past its radius plus the hysteresis
                            Assert.That(d, Is.GreaterThan((state == 2 ? nearM : farM) + RoadBlocks.HysteresisM - 1e-9));
                        else // coming in, a copy appears at its radius
                            Assert.That(d, Is.LessThanOrEqualTo(s == 2 ? nearM : farM));
                    }
                    state = s;
                }
            }
            Assert.That(switches, Is.EqualTo(4), "out: full -> cheap -> none; in: none -> cheap -> full");
            Assert.That(lastSwitchOut, Is.GreaterThan(farM));
            Assert.That(lastSwitchIn, Is.LessThanOrEqualTo(nearM));
        }

        [Test]
        public void AViewWithoutTheCheapCopyDrawsTheFullKitOutToTheFarRadius()
        {
            float nearM, farM;
            RoadBlocks.Radii(BandConfig.ForTier(1), StreamingConfig.ForTier(1), out nearM, out farM);
            ulong near = 0, far = 0;
            double cx = RoadBlocks.BlockM + (nearM + farM) / 2, cz = 64;
            RoadBlocks.UpdateMasks(TileM, cx, cz, nearM, farM, ref near, ref far);
            Assert.That(RoadBlocks.PartShown(0, near, far, false, TileM, cx, cz, nearM, farM), Is.True);
            Assert.That(RoadBlocks.BlockIn(near, 0, TileM, cx, cz, nearM), Is.False, "but not its markings");
            cx = RoadBlocks.BlockM + farM + 2 * RoadBlocks.HysteresisM;
            RoadBlocks.UpdateMasks(TileM, cx, cz, nearM, farM, ref near, ref far);
            Assert.That(RoadBlocks.PartShown(0, near, far, false, TileM, cx, cz, nearM, farM), Is.False);
        }

        [Test]
        public void EveryBlockOfATileIsTrackedAndUpdatingAllocatesNothing()
        {
            float nearM, farM;
            RoadBlocks.Radii(BandConfig.ForTier(2), StreamingConfig.ForTier(2), out nearM, out farM);
            int per = MeshParts.BlocksPerSide(TileM, RoadBlocks.BlockM);
            Assert.That(per * per, Is.LessThanOrEqualTo(RoadBlocks.MaxTrackedBlocks), "a 1 km tile's 8 x 8 blocks fit the masks");
            ulong near = 0, far = 0;
            RoadBlocks.UpdateMasks(TileM, 512, 512, nearM, farM, ref near, ref far);
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) RoadBlocks.UpdateMasks(TileM, 512 + i * 0.3, 512, nearM, farM, ref near, ref far);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - a0, Is.EqualTo(0));
            RoadBlocks.UpdateMasks(TileM, 512, 512, nearM, farM, ref near, ref far); // back at the tile's centre
            Assert.That(far, Is.EqualTo(ulong.MaxValue), "from the centre, High's B2 radius (" + farM + " m) reaches every block of the tile");
            Assert.That(near, Is.Not.EqualTo(0UL));
            Assert.That(near, Is.Not.EqualTo(ulong.MaxValue), "the B1 radius only the blocks around the camera");
        }
    }
}
