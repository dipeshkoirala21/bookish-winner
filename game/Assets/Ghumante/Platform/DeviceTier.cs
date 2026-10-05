using System;
using UnityEngine;

namespace Ghumante.Platform
{
    /// <summary>
    /// Device performance tier (ARCHITECTURE.md section 10). The integer value is also the index of the
    /// matching quality level that <c>Ghumante.EditorTools.ProjectSetup</c> creates (Low, Medium, High).
    /// </summary>
    public enum DeviceTier
    {
        Low = 0,
        Mid = 1,
        High = 2,
    }

    /// <summary>The hardware facts the tier heuristic looks at. Plain data so tests can feed any device.</summary>
    [Serializable]
    public struct DeviceFacts
    {
        public RuntimePlatform platform;
        public int systemMemoryMB;
        public int graphicsMemoryMB;
        public int processorCount;
        public string deviceModel;

        /// <summary>Reads the facts of the device the player is running on.</summary>
        public static DeviceFacts Current()
        {
            return new DeviceFacts
            {
                platform = Application.platform,
                systemMemoryMB = SystemInfo.systemMemorySize,
                graphicsMemoryMB = SystemInfo.graphicsMemorySize,
                processorCount = SystemInfo.processorCount,
                deviceModel = SystemInfo.deviceModel,
            };
        }
    }

    /// <summary>
    /// First-launch tier heuristic from RAM and CPU count. M1 adds the GPU family table and the 3-second
    /// benchmark scene described in ARCHITECTURE.md section 10; the player can always override the tier.
    /// </summary>
    public static class DeviceTierDetector
    {
        /// <summary>
        /// Default target frame rate per tier (ARCHITECTURE.md section 10 and P2): Low 30 fps, Mid 30 fps by
        /// default (60 fps is a player option, see <see cref="MaxFrameRate"/>), High 60 fps.
        /// </summary>
        public static int TargetFrameRate(DeviceTier tier)
        {
            return tier == DeviceTier.High ? 60 : 30;
        }

        /// <summary>Highest frame rate the player may opt into on a tier (Mid offers a 60 fps option).</summary>
        public static int MaxFrameRate(DeviceTier tier)
        {
            return tier == DeviceTier.Low ? 30 : 60;
        }

        /// <summary>Name of the quality level that matches a tier, as created by ProjectSetup.</summary>
        public static string QualityLevelName(DeviceTier tier)
        {
            switch (tier)
            {
                case DeviceTier.Low: return "Low";
                case DeviceTier.Mid: return "Medium";
                default: return "High";
            }
        }

        /// <summary>Classifies the current device.</summary>
        public static DeviceTier Detect()
        {
            return Classify(DeviceFacts.Current());
        }

        /// <summary>
        /// Pure classification, so it can be unit-tested. Thresholds follow the tier table in ARCHITECTURE.md
        /// section 10: 3-4 GB Android phones are Low, 6-8 GB Android (Snapdragon 7 Gen 1 / 7s Gen 2 class) is
        /// Mid, and iPhone 12 / A14 or newer is High. RAM reported by the OS is always a little below the
        /// marketing figure, hence the odd thresholds.
        /// </summary>
        public static DeviceTier Classify(DeviceFacts facts)
        {
            int ramMB = facts.systemMemoryMB;
            bool isIOS = facts.platform == RuntimePlatform.IPhonePlayer;

            if (isIOS)
            {
                // iPhone model identifiers name the SoC generation: iPhone13,x is the iPhone 12 (A14),
                // iPhone12,x the iPhone 11 (A13). A14 and newer are High (section 10) whatever their RAM.
                int iphoneMajor = IphoneModelMajor(facts.deviceModel);
                if (iphoneMajor >= FirstA14IphoneMajor) return DeviceTier.High;
                if (iphoneMajor > 0) return ramMB >= 3500 ? DeviceTier.Mid : DeviceTier.Low;

                // iPad, or an unknown identifier: go by RAM. iOS reports e.g. ~3.7 GB on a 4 GB device and
                // ~5.6 GB on a 6 GB one; 8 GB (M-series iPads) is High.
                if (ramMB >= 7200) return DeviceTier.High;
                if (ramMB >= 3500) return DeviceTier.Mid;
                return DeviceTier.Low;
            }

            if (ramMB <= 0)
            {
                // Unknown (some emulators and the editor on exotic hosts): be conservative.
                return DeviceTier.Mid;
            }

            if (ramMB < 5000 || facts.processorCount < 6) return DeviceTier.Low;
            if (ramMB < 9500) return DeviceTier.Mid;
            return DeviceTier.High;
        }

        /// <summary>Major number of the first iPhone with an A14 (iPhone13,1 to iPhone13,4 are the iPhone 12 family).</summary>
        public const int FirstA14IphoneMajor = 13;

        /// <summary>
        /// The major number of an iPhone model identifier ("iPhone13,2" gives 13), or 0 when
        /// <paramref name="deviceModel"/> is not an iPhone identifier (iPad, simulator, null).
        /// </summary>
        public static int IphoneModelMajor(string deviceModel)
        {
            const string prefix = "iPhone";
            if (string.IsNullOrEmpty(deviceModel) || !deviceModel.StartsWith(prefix, StringComparison.Ordinal))
            {
                return 0;
            }
            int major = 0;
            int i = prefix.Length;
            for (; i < deviceModel.Length && deviceModel[i] >= '0' && deviceModel[i] <= '9'; i++)
            {
                if (major > 1000) return 0;
                major = major * 10 + (deviceModel[i] - '0');
            }
            if (i == prefix.Length || i >= deviceModel.Length || deviceModel[i] != ',') return 0;
            return major;
        }
    }
}
