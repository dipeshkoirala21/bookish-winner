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
        /// <summary>Per-tier target frame rate (ARCHITECTURE.md section 10: Low 30 fps, Mid and High 60 fps).</summary>
        public static int TargetFrameRate(DeviceTier tier)
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
        /// Pure classification, so it can be unit-tested. Thresholds follow the budgets in ARCHITECTURE.md
        /// section 10: 3-4 GB Android phones are Low, iPhone 12 class (4 GB) and 6-8 GB Android are Mid.
        /// RAM reported by the OS is always a little below the marketing figure, hence the odd thresholds.
        /// </summary>
        public static DeviceTier Classify(DeviceFacts facts)
        {
            int ramMB = facts.systemMemoryMB;
            bool isIOS = facts.platform == RuntimePlatform.IPhonePlayer;

            if (isIOS)
            {
                // iOS reports e.g. ~3.7 GB on a 4 GB iPhone 12 and ~5.6 GB on a 6 GB iPhone 13 Pro.
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
    }
}
