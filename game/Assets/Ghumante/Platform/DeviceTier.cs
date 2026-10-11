using System;
using Ghumante.Core.Platform;
using UnityEngine;

namespace Ghumante.Platform
{
    /// <summary>
    /// Device performance tier (ARCHITECTURE.md section 10). The integer value is also the index of the
    /// matching quality level that <c>Ghumante.EditorTools.ProjectSetup</c> creates (Low, Medium, High), and equals the
    /// engine-free <see cref="QualityTier"/> (a cast converts).
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

        /// <summary>The renderer string ("Adreno (TM) 619", "Mali-G57 MC2", "Apple M2"): the GPU family table's input.</summary>
        public string graphicsDeviceName;

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
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
            };
        }

        /// <summary>The engine-free profile the classifier reads.</summary>
        public DeviceProfile ToProfile()
        {
            return new DeviceProfile
            {
                Kind = DeviceTierDetector.KindOf(platform),
                SystemMemoryMB = systemMemoryMB,
                ProcessorCount = processorCount,
                DeviceModel = deviceModel,
                GraphicsDeviceName = graphicsDeviceName,
            };
        }
    }

    /// <summary>
    /// First-launch tier detection: RAM, CPU count, the iPhone model and the GPU family table
    /// (<see cref="DeviceClassifier"/> in Core, docs/research/w2/ref_quality.md). The player can always override the
    /// tier in Settings > Graphics, and on Auto the adaptive governor (<see cref="QualityGovernor"/>) lowers a tier the
    /// phone cannot hold.
    /// </summary>
    public static class DeviceTierDetector
    {
        /// <summary>
        /// Default target frame rate per tier (ARCHITECTURE.md section 10 and P2): Low 30 fps, Mid 30 fps by
        /// default (60 fps is a player option, see <see cref="MaxFrameRate"/>), High 60 fps.
        /// </summary>
        public static int TargetFrameRate(DeviceTier tier)
        {
            return QualityTiers.DefaultFrameRate((QualityTier)tier);
        }

        /// <summary>Highest frame rate the player may opt into on a tier (Mid offers a 60 fps option).</summary>
        public static int MaxFrameRate(DeviceTier tier)
        {
            return QualityTiers.MaxFrameRate((QualityTier)tier);
        }

        /// <summary>Name of the quality level that matches a tier, as created by ProjectSetup.</summary>
        public static string QualityLevelName(DeviceTier tier)
        {
            return QualityTiers.QualityLevelName((QualityTier)tier);
        }

        /// <summary>Classifies the current device.</summary>
        public static DeviceTier Detect()
        {
            return Classify(DeviceFacts.Current());
        }

        /// <summary>
        /// Pure classification, so it can be unit-tested: the tier of <see cref="Decide"/>. Thresholds follow the tier
        /// table in ARCHITECTURE.md section 10: 3-4 GB Android phones are Low, 6-8 GB Android with a Snapdragon 7 Gen 1 /
        /// 7s Gen 2 class GPU is Mid, iPhone 12 / A14 or newer is High, and a weak GPU caps any amount of RAM.
        /// </summary>
        public static DeviceTier Classify(DeviceFacts facts)
        {
            return (DeviceTier)Decide(facts).Tier;
        }

        /// <summary>The full decision (tier, what RAM allowed, the GPU as the table read it, the rule) for the log.</summary>
        public static TierDecision Decide(DeviceFacts facts)
        {
            DeviceProfile profile = facts.ToProfile();
            return DeviceClassifier.Classify(profile);
        }

        /// <summary>The classifier's device kind of a Unity platform.</summary>
        public static DeviceKind KindOf(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android: return DeviceKind.Android;
                case RuntimePlatform.IPhonePlayer: return DeviceKind.Ios;
                case RuntimePlatform.OSXEditor: return DeviceKind.MacEditor;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor: return DeviceKind.Editor;
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.LinuxPlayer: return DeviceKind.Desktop;
                default: return DeviceKind.Unknown;
            }
        }

        /// <summary>Major number of the first iPhone with an A14 (iPhone13,1 to iPhone13,4 are the iPhone 12 family).</summary>
        public const int FirstA14IphoneMajor = DeviceClassifier.FirstA14IphoneMajor;

        /// <summary>
        /// The major number of an iPhone model identifier ("iPhone13,2" gives 13), or 0 when
        /// <paramref name="deviceModel"/> is not an iPhone identifier (iPad, simulator, null).
        /// </summary>
        public static int IphoneModelMajor(string deviceModel)
        {
            return DeviceClassifier.IphoneModelMajor(deviceModel);
        }
    }
}
