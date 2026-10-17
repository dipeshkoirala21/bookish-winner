using System;

namespace Ghumante.Core.Horizon
{
    /// <summary>
    /// Per-tier settings of the distant-mountains layer (the skyline ring, ARCHITECTURE 8 "Skyline", ROADMAP 1.9):
    /// how finely the panorama is sampled, how its silhouettes are layered and simplified, how many triangles it may
    /// cost (W2_DESIGN 10.4: sky and water get 4 k / 10 k / 20 k triangles), where it takes over from the streamed
    /// terrain and how often it is rebuilt as the camera moves. <see cref="ForTier"/> returns fresh instances.
    /// </summary>
    public sealed class HorizonSettings
    {
        public const int TierLow = 0;
        public const int TierMid = 1;
        public const int TierHigh = 2;

        /// <summary>Azimuth step of the panorama rays in degrees (360 / step rays).</summary>
        public double AzimuthStepDeg = 0.25;

        /// <summary>Distance bands in metres, nearest first: band b covers [BandEdgesM[b], BandEdgesM[b + 1]). Each band
        /// keeps its own silhouette, so a nearer range hides the foot of a farther one as it does in the photos.</summary>
        public double[] BandEdgesM = { 4000, 30000, 60000, 100000, 250000 };

        /// <summary>Ray-march step: max(<see cref="StepMinM"/>, distance × <see cref="StepRelative"/>).</summary>
        public double StepMinM = 40.0;

        /// <summary>Relative ray-march step (0.004 = 260 m at 65 km).</summary>
        public double StepRelative = 0.004;

        /// <summary>Largest error of the simplified silhouette, degrees (elevation and azimuth alike).</summary>
        public double SimplifyToleranceDeg = 0.025;

        /// <summary>Most triangles the ring may have; the tolerance grows until it fits.</summary>
        public int TriangleCap = 9000;

        /// <summary>Spacing of the coarse height grid built from the horizon tiles, metres (128 = the level-6 samples;
        /// 256 keeps every summit by taking the highest of four).</summary>
        public double GridSpacingM = 128.0;

        /// <summary>
        /// Slant range from the camera where the ring takes over from the streamed terrain: the ring is drawn at this
        /// distance along each view ray (after the opaque terrain), so streamed terrain nearer than this stays in front
        /// and the coarse, flattened horizon terrain farther than this is covered by the true silhouettes. Kept inside
        /// the camera's far clip (WorldRoot.ConfigureCamera: 67.5 / 105 / 155 km). 30 km on every tier: beyond it the
        /// valley haze has left about a tenth of the streamed terrain's colour, and the high ground 30-60 km out (the
        /// Langtang and Jugal foothills, 3 500-5 000 m) is drawn in the ring's own altitude colours (blue-grey rock and
        /// snow streaks over a hazy foot, as in the photos) instead of the streamed alpine and bare-ground colours, which
        /// read khaki from the valley.
        /// </summary>
        public double HandoverM = 30000.0;

        /// <summary>Rebuild the ring once the camera moved this far horizontally from where it was built.</summary>
        public double RebuildDistanceM = 200.0;

        /// <summary>... or this far vertically (flying).</summary>
        public double RebuildHeightM = 60.0;

        /// <summary>The curtains hang from each silhouette down to this many degrees below the eye's horizon.</summary>
        public double BottomDepressionDeg = 6.0;

        /// <summary>Settings for tier 0 (Low), 1 (Mid) or 2 (High).</summary>
        public static HorizonSettings ForTier(int tier)
        {
            switch (tier)
            {
                case TierLow:
                    return new HorizonSettings
                    {
                        AzimuthStepDeg = 0.4, BandEdgesM = new double[] { 4000, 30000, 75000, 250000 }, StepRelative = 0.005,
                        SimplifyToleranceDeg = 0.05, TriangleCap = 2500, GridSpacingM = 256.0, HandoverM = 30000.0, RebuildDistanceM = 400.0,
                        RebuildHeightM = 100.0,
                    };
                case TierMid:
                    return new HorizonSettings
                    {
                        AzimuthStepDeg = 0.25, BandEdgesM = new double[] { 4000, 30000, 60000, 100000, 250000 }, StepRelative = 0.004,
                        SimplifyToleranceDeg = 0.035, TriangleCap = 5000, GridSpacingM = 128.0, HandoverM = 30000.0, RebuildDistanceM = 300.0,
                        RebuildHeightM = 80.0,
                    };
                case TierHigh:
                    return new HorizonSettings();
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), "tier must be 0 (Low), 1 (Mid) or 2 (High)");
            }
        }

        /// <summary>Number of distance bands.</summary>
        public int Bands
        {
            get { return BandEdgesM.Length - 1; }
        }

        /// <summary>Number of panorama rays.</summary>
        public int Azimuths
        {
            get { return Math.Max(8, (int)Math.Round(360.0 / AzimuthStepDeg)); }
        }

        /// <summary>Farthest distance marched.</summary>
        public double MaxDistanceM
        {
            get { return BandEdgesM[BandEdgesM.Length - 1]; }
        }

        /// <summary>Throws <see cref="ArgumentException"/> unless the settings are usable.</summary>
        public void Validate()
        {
            if (!(AzimuthStepDeg > 0.01) || AzimuthStepDeg > 10) throw new ArgumentException("AzimuthStepDeg must lie in (0.01, 10]");
            if (BandEdgesM == null || BandEdgesM.Length < 2) throw new ArgumentException("at least one distance band");
            for (int i = 0; i < BandEdgesM.Length; i++)
            {
                if (!(BandEdgesM[i] > 0) || double.IsInfinity(BandEdgesM[i])) throw new ArgumentException("band edges must be positive and finite");
                if (i > 0 && BandEdgesM[i] <= BandEdgesM[i - 1]) throw new ArgumentException("band edges must grow");
            }
            if (!(StepMinM > 0) || !(StepRelative > 0)) throw new ArgumentException("steps must be positive");
            if (!(SimplifyToleranceDeg >= 0)) throw new ArgumentException("SimplifyToleranceDeg must not be negative");
            if (TriangleCap < 8) throw new ArgumentException("TriangleCap too small");
            if (!(GridSpacingM >= 16)) throw new ArgumentException("GridSpacingM must be at least 16 m");
            if (!(HandoverM > 0)) throw new ArgumentException("HandoverM must be positive");
        }
    }
}
