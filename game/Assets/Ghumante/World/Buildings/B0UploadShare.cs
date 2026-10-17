using System;

namespace Ghumante.World.Buildings
{
    /// <summary>
    /// How the streamer shares its per-frame upload cap between tile chunks and B0 cell chunks (WorldStreamer.Tick),
    /// engine-free so the stability tests ride the very policy the game runs (core-tests MeshingBuildingB0StabilityTests):
    /// <list type="bullet">
    /// <item>B0 goes <b>first</b>, up to half the cap (one chunk at least), when a build the band needs now is waiting
    /// (<see cref="B0CellSet.UrgentUploadWaiting"/>: a house within the lead of the band edge, or one a B1 block stands in
    /// for), so a house riding brings in is up before it reaches the band even while a new tile streams in; and when B0
    /// found no room for <see cref="StarvedTicks"/> frames in a row, so level and pruning builds never starve.</item>
    /// <item>Otherwise B0 goes <b>after</b> the tiles, with what they left of the cap and of the frame's upload time.</item>
    /// </list>
    /// Main thread; no allocation.
    /// </summary>
    public sealed class B0UploadShare
    {
        /// <summary>Frames B0 waits behind tile uploads before it uploads first.</summary>
        public const int StarvedTicks = 3;

        /// <summary>The upload time (ms) that must be left after the tiles for B0 to upload after them.</summary>
        public const double MinAfterMs = 0.25;

        private int _starved;

        /// <summary>Frames in a row B0 had something waiting and uploaded nothing.</summary>
        public int Starved
        {
            get { return _starved; }
        }

        /// <summary>Bytes B0 uploads before the tiles this frame (with at least one chunk), 0 to wait for what the tiles
        /// leave.</summary>
        public long First(long capBytes, bool waiting, bool urgent)
        {
            return waiting && (urgent || _starved >= StarvedTicks) ? Math.Max(1, capBytes / 2) : 0;
        }

        /// <summary>Bytes B0 may upload after the tiles (no chunk forced): what they left of the cap, when it did not go
        /// first and enough upload time is left.</summary>
        public long After(long capBytes, long usedBytes, bool waiting, bool wentFirst, double msLeft)
        {
            return waiting && !wentFirst && msLeft > MinAfterMs ? Math.Max(0, capBytes - usedBytes) : 0;
        }

        /// <summary>Ends the frame: B0 had something <paramref name="waiting"/> and uploaded <paramref name="b0Bytes"/>.</summary>
        public void EndFrame(bool waiting, long b0Bytes)
        {
            _starved = waiting && b0Bytes == 0 ? _starved + 1 : 0;
        }
    }
}
