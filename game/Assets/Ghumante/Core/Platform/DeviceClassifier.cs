using System;

namespace Ghumante.Core.Platform
{
    /// <summary>What kind of machine runs the game (the classifier's rules differ per kind).</summary>
    public enum DeviceKind
    {
        Unknown = 0,
        Android = 1,
        Ios = 2,

        /// <summary>The Unity editor on a Mac (Apple silicon or Intel).</summary>
        MacEditor = 3,

        /// <summary>The Unity editor on Windows or Linux.</summary>
        Editor = 4,

        /// <summary>A desktop player build.</summary>
        Desktop = 5,
    }

    /// <summary>The hardware facts the classifier reads (plain data, so tests can describe any phone).</summary>
    public struct DeviceProfile
    {
        public DeviceKind Kind;

        /// <summary>RAM as the OS reports it (a little below the marketing figure: 4 GB phones report about 3.6-3.8 GB).</summary>
        public int SystemMemoryMB;

        public int ProcessorCount;

        /// <summary>Model identifier ("iPhone13,2", "samsung SM-A566B").</summary>
        public string DeviceModel;

        /// <summary>Renderer string (<c>SystemInfo.graphicsDeviceName</c>: "Adreno (TM) 619", "Mali-G57 MC2").</summary>
        public string GraphicsDeviceName;
    }

    /// <summary>The classifier's answer, with what it was based on (logged once at startup).</summary>
    public struct TierDecision
    {
        /// <summary>The tier Auto uses.</summary>
        public QualityTier Tier;

        /// <summary>What RAM and CPU count alone allow.</summary>
        public QualityTier RamTier;

        /// <summary>The GPU as the table read it.</summary>
        public GpuInfo Gpu;

        /// <summary>Which rule decided: "iphone-model", "apple-gpu", "gpu-ceiling", "ram-cpu", "ram-unknown-gpu",
        /// "mac-editor", "software-gpu", "unknown-ram".</summary>
        public string Rule;

        /// <summary>One line for the startup log (allocates).</summary>
        public string Describe(in DeviceProfile p)
        {
            return Tier + " by " + Rule + " (" + p.Kind + ", " + (p.DeviceModel ?? "?") + ", " + p.SystemMemoryMB + " MB RAM, " +
                   p.ProcessorCount + " cores, RAM/CPU allow " + RamTier + ", " + Gpu.Describe() + ")";
        }
    }

    /// <summary>
    /// First-launch tier heuristic (ARCHITECTURE.md section 10, docs/research/w2/ref_quality.md), engine-free so the
    /// table of real phones is unit-tested:
    /// <list type="bullet">
    /// <item><b>Android</b>: RAM and cores give a ceiling (under about 5 GB reported or fewer than 6 cores: Low; 6 GB
    /// phones: Mid; 8 GB and more: High), and the GPU family table another (<see cref="GpuTierTable"/>); the tier is the
    /// lower of the two. Without a recognised GPU the older RAM-only proxy applies (High only from about 10 GB), so an
    /// unknown phone is never rated above what its memory suggests.</item>
    /// <item><b>iOS</b>: the iPhone model identifier names the SoC (iPhone13,x is the A14 iPhone 12: High); iPads and
    /// unknown identifiers go by the Apple GPU name, else by RAM.</item>
    /// <item><b>Mac editor</b>: Apple silicon (M1/M2/M3/M4) is High with 16 GB or more, Mid otherwise; Intel graphics
    /// Mid.</item>
    /// <item><b>Other editors and desktops</b>: RAM, capped by the GPU table (Intel integrated Mid, software Low).</item>
    /// </list>
    /// The player can always pick a tier in Settings, and the adaptive governor (<see cref="QualityGovernor"/>) lowers an
    /// Auto tier that turns out too heavy.
    /// </summary>
    public static class DeviceClassifier
    {
        /// <summary>Android RAM below this (MB, as reported) is a 4 GB-or-less phone: Low.</summary>
        public const int LowRamBelowMB = 5000;

        /// <summary>Android RAM below this with a recognised GPU is a 6 GB phone: at most Mid. 8 GB phones report about
        /// 7.3-7.6 GB.</summary>
        public const int MidRamBelowMB = 7000;

        /// <summary>Android RAM below this with an unrecognised GPU: at most Mid (the pre-table proxy).</summary>
        public const int UnknownGpuMidRamBelowMB = 9500;

        /// <summary>Phones with fewer cores are old enough to be Low whatever else they report.</summary>
        public const int MinCoresAboveLow = 6;

        /// <summary>Apple silicon Macs with at least this much RAM (MB) run the editor at High.</summary>
        public const int MacEditorHighRamMB = 16000;

        /// <summary>Major number of the first iPhone with an A14 (iPhone13,1 to iPhone13,4 are the iPhone 12 family).</summary>
        public const int FirstA14IphoneMajor = 13;

        /// <summary>Classifies a device (pure).</summary>
        public static TierDecision Classify(in DeviceProfile p)
        {
            var d = new TierDecision { Gpu = GpuTierTable.Parse(p.GraphicsDeviceName) };
            int ram = p.SystemMemoryMB;
            switch (p.Kind)
            {
                case DeviceKind.Ios:
                    return Ios(ref d, p);
                case DeviceKind.MacEditor:
                    return MacEditor(ref d, p);
                case DeviceKind.Android:
                    return Android(ref d, p);
            }

            // Other editors, desktops and unknown kinds: RAM, capped by the GPU.
            d.RamTier = ram <= 0 ? QualityTier.Mid : ram < LowRamBelowMB ? QualityTier.Low : ram < UnknownGpuMidRamBelowMB ? QualityTier.Mid : QualityTier.High;
            d.Tier = d.Gpu.Known ? QualityTiers.Min(d.RamTier, d.Gpu.Ceiling) : d.RamTier;
            d.Rule = d.Gpu.Family == GpuFamily.Software ? "software-gpu" : ram <= 0 ? "unknown-ram" : d.Gpu.Known && d.Gpu.Ceiling < d.RamTier ? "gpu-ceiling" : "ram-cpu";
            return d;
        }

        private static TierDecision Android(ref TierDecision d, in DeviceProfile p)
        {
            int ram = p.SystemMemoryMB;
            bool fewCores = p.ProcessorCount > 0 && p.ProcessorCount < MinCoresAboveLow;
            if (d.Gpu.Family == GpuFamily.Software)
            {
                d.RamTier = QualityTier.Low;
                d.Tier = QualityTier.Low;
                d.Rule = "software-gpu";
                return d;
            }
            if (ram <= 0)
            {
                // Unknown memory (some emulators): be conservative, but never above what the GPU allows.
                d.RamTier = fewCores ? QualityTier.Low : QualityTier.Mid;
                d.Tier = d.Gpu.Known ? QualityTiers.Min(d.RamTier, d.Gpu.Ceiling) : d.RamTier;
                d.Rule = "unknown-ram";
                return d;
            }
            if (fewCores || ram < LowRamBelowMB)
            {
                d.RamTier = QualityTier.Low;
                d.Tier = QualityTier.Low;
                d.Rule = "ram-cpu";
                return d;
            }
            if (!d.Gpu.Known)
            {
                d.RamTier = ram < UnknownGpuMidRamBelowMB ? QualityTier.Mid : QualityTier.High;
                d.Tier = d.RamTier;
                d.Rule = "ram-unknown-gpu";
                return d;
            }
            d.RamTier = ram < MidRamBelowMB ? QualityTier.Mid : QualityTier.High;
            d.Tier = QualityTiers.Min(d.RamTier, d.Gpu.Ceiling);
            d.Rule = d.Gpu.Ceiling < d.RamTier ? "gpu-ceiling" : "ram-cpu";
            return d;
        }

        private static TierDecision Ios(ref TierDecision d, in DeviceProfile p)
        {
            int ram = p.SystemMemoryMB;
            // iPhone model identifiers name the SoC generation: iPhone13,x is the iPhone 12 (A14), iPhone12,x the
            // iPhone 11 (A13). A14 and newer are High (section 10) whatever their RAM.
            int iphoneMajor = IphoneModelMajor(p.DeviceModel);
            if (iphoneMajor > 0)
            {
                d.RamTier = ram >= 3500 ? QualityTier.Mid : QualityTier.Low;
                d.Tier = iphoneMajor >= FirstA14IphoneMajor ? QualityTier.High : d.RamTier;
                d.Rule = "iphone-model";
                return d;
            }
            // iPad, or an unknown identifier: iOS reports e.g. about 3.7 GB on a 4 GB device and 5.6 GB on a 6 GB one;
            // 8 GB (M-series iPads) is High.
            d.RamTier = ram >= 7200 ? QualityTier.High : ram >= 3500 ? QualityTier.Mid : QualityTier.Low;
            if (d.Gpu.Family == GpuFamily.AppleA || d.Gpu.Family == GpuFamily.AppleM)
            {
                // An A14+ or M-series GPU is High; older A-series follow RAM, capped by the GPU.
                d.Tier = d.Gpu.Ceiling == QualityTier.High ? QualityTier.High : QualityTiers.Min(d.RamTier, d.Gpu.Ceiling);
                d.Rule = "apple-gpu";
                return d;
            }
            d.Tier = d.RamTier;
            d.Rule = "ram-cpu";
            return d;
        }

        private static TierDecision MacEditor(ref TierDecision d, in DeviceProfile p)
        {
            int ram = p.SystemMemoryMB;
            d.Rule = "mac-editor";
            if (d.Gpu.Family == GpuFamily.AppleM)
            {
                d.RamTier = ram >= MacEditorHighRamMB ? QualityTier.High : QualityTier.Mid;
                d.Tier = d.RamTier;
                return d;
            }
            d.RamTier = ram <= 0 ? QualityTier.Mid : ram < LowRamBelowMB ? QualityTier.Low : ram < MacEditorHighRamMB ? QualityTier.Mid : QualityTier.High;
            d.Tier = d.Gpu.Known ? QualityTiers.Min(d.RamTier, d.Gpu.Ceiling) : d.RamTier;
            return d;
        }

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
