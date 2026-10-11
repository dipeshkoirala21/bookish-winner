using System;

namespace Ghumante.Core.Platform
{
    /// <summary>GPU families the tier table knows (from the renderer string, <c>SystemInfo.graphicsDeviceName</c>).</summary>
    public enum GpuFamily
    {
        /// <summary>Not recognised: the GPU sets no ceiling and the device is classified by RAM and CPU alone.</summary>
        Unknown = 0,

        /// <summary>Qualcomm Adreno ("Adreno (TM) 619").</summary>
        Adreno = 1,

        /// <summary>Arm Mali, Midgard to 5th generation ("Mali-G57 MC2", "Mali-G68", "Mali-G1-Ultra MC12", "Mali-T830").</summary>
        Mali = 2,

        /// <summary>Arm Immortalis, the ray-tracing flagships ("Immortalis-G715", "Immortalis-G925").</summary>
        Immortalis = 3,

        /// <summary>Samsung Xclipse, AMD RDNA based ("Samsung Xclipse 530", "Xclipse 940").</summary>
        Xclipse = 4,

        /// <summary>Imagination PowerVR ("PowerVR Rogue GE8320", "PowerVR B-Series BXM-8-256", "PowerVR D-Series DXT-48-1536").</summary>
        PowerVR = 5,

        /// <summary>Huawei Maleoon (Kirin 9000S and later).</summary>
        Maleoon = 6,

        /// <summary>Apple A-series ("Apple A15 GPU").</summary>
        AppleA = 7,

        /// <summary>Apple M-series ("Apple M2 Pro"): Macs (the editor) and iPads.</summary>
        AppleM = 8,

        /// <summary>Intel integrated graphics (UHD, Iris): Intel Macs and laptops running the editor.</summary>
        IntelIntegrated = 9,

        /// <summary>Discrete desktop GPUs (NVIDIA, AMD Radeon, Intel Arc).</summary>
        Desktop = 10,

        /// <summary>Software rasterisers (SwiftShader, llvmpipe): emulators and CI.</summary>
        Software = 11,
    }

    /// <summary>What the tier table read from a renderer string.</summary>
    public struct GpuInfo
    {
        /// <summary>The renderer string as reported (may be null).</summary>
        public string Name;

        public GpuFamily Family;

        /// <summary>Model number within the family (Adreno 619 gives 619, Mali-G57 gives 57, Xclipse 530 gives 530,
        /// Apple A15 gives 15); 0 when the string has none.</summary>
        public int Model;

        /// <summary>Shader core count when the string carries one ("MC2", "MP5"); 0 when unknown. Mali drivers often omit
        /// it, so every rule has a conservative answer without it.</summary>
        public int Cores;

        /// <summary>Sub-series where it matters: "ultra", "premium" or "pro" for Mali-G1, the PowerVR series letter
        /// ("b", "c", "d", "rogue"); null otherwise.</summary>
        public string Variant;

        /// <summary>True when the family and model were recognised and <see cref="Ceiling"/> applies.</summary>
        public bool Known;

        /// <summary>Highest tier this GPU should run (High when unknown, i.e. no ceiling).</summary>
        public QualityTier Ceiling;

        /// <summary>Short description for the startup log ("Adreno 619 -> Low").</summary>
        public string Describe()
        {
            if (!Known) return "GPU '" + (Name ?? "") + "' not in the table";
            string cores = Cores > 0 ? " x" + Cores : "";
            string variant = Variant != null ? "-" + Variant : "";
            return Family + " " + Model + variant + cores + " -> " + Ceiling;
        }
    }

    /// <summary>
    /// GPU family table (docs/research/w2/ref_quality.md): maps the renderer string of the phones sold in Nepal from 2023
    /// to 2026 (Samsung Galaxy A/M/S, Redmi and Xiaomi, Realme, Vivo, Oppo, iPhones) to the highest tier the GPU runs
    /// well, so a phone with plenty of RAM but a weak GPU is not High. The cut-offs follow GFXBench Manhattan 3.1
    /// offscreen (notebookcheck): below about 40 fps is Low (Adreno 610-619, Mali-G52/G57, Mali-G68 MP2, PowerVR
    /// Rogue), 40 to 80 is Mid (Adreno 642L/644/710/810, Mali-G68 MP4-5, G610 MP4, G615 MP2, Xclipse 530), above is
    /// High (Adreno 720+, Mali-G610/G615 MP6, G710+, Immortalis, Xclipse 540+). Rules work on the model number, so
    /// newer GPUs of a known family land sensibly: Adreno's middle digit ranks within a generation (6x0-6x9: below
    /// 630 Low, 630-649 Mid, 650+ High; 7xx: 702 Low, 710-719 Mid, 720+ High; 8xx: 810-819 Mid, 820+ High). Pure.
    /// </summary>
    public static class GpuTierTable
    {
        /// <summary>Reads a renderer string (<c>SystemInfo.graphicsDeviceName</c>). Allocates a little (once at startup).</summary>
        public static GpuInfo Parse(string graphicsDeviceName)
        {
            var info = new GpuInfo { Name = graphicsDeviceName, Ceiling = QualityTier.High };
            if (string.IsNullOrEmpty(graphicsDeviceName)) return info;
            string s = graphicsDeviceName.ToLowerInvariant();

            if (s.Contains("swiftshader") || s.Contains("llvmpipe") || s.Contains("softpipe") || s.Contains("basic render"))
                return Set(ref info, GpuFamily.Software, 0, QualityTier.Low);

            int at;
            if ((at = s.IndexOf("adreno", StringComparison.Ordinal)) >= 0)
            {
                int model = NumberAfter(s, at + 6, 6);
                if (model < 100) return info; // "Adreno X1-85" (Windows on Arm) and other unnumbered parts: no ceiling
                return Set(ref info, GpuFamily.Adreno, model, AdrenoCeiling(model));
            }
            if ((at = s.IndexOf("immortalis", StringComparison.Ordinal)) >= 0)
            {
                info.Cores = CoresAfter(s, at);
                return Set(ref info, GpuFamily.Immortalis, NumberAfter(s, at + 10, 4), QualityTier.High);
            }
            if ((at = s.IndexOf("mali", StringComparison.Ordinal)) >= 0) return Mali(ref info, s, at + 4);
            if ((at = s.IndexOf("xclipse", StringComparison.Ordinal)) >= 0)
            {
                int model = NumberAfter(s, at + 7, 4);
                if (model <= 0) return info;
                // 530 (Exynos 1480) is Mid; 540 (1580), 550 (1680) and the 9xx flagships are High.
                return Set(ref info, GpuFamily.Xclipse, model, model >= 540 ? QualityTier.High : QualityTier.Mid);
            }
            if (s.Contains("powervr") || s.StartsWith("img ", StringComparison.Ordinal) || s.Contains("bxm-") || s.Contains("dxt-"))
                return PowerVr(ref info, s);
            if ((at = s.IndexOf("maleoon", StringComparison.Ordinal)) >= 0)
                return Set(ref info, GpuFamily.Maleoon, NumberAfter(s, at + 7, 4), QualityTier.Mid);
            if ((at = s.IndexOf("apple", StringComparison.Ordinal)) >= 0)
            {
                int i = SkipSpaces(s, at + 5);
                if (i < s.Length && (s[i] == 'a' || s[i] == 'm') && i + 1 < s.Length && IsDigit(s[i + 1]))
                {
                    int model = NumberAfter(s, i + 1, 1);
                    if (s[i] == 'm') return Set(ref info, GpuFamily.AppleM, model, QualityTier.High);
                    QualityTier t = model >= 14 ? QualityTier.High : model >= 12 ? QualityTier.Mid : QualityTier.Low;
                    return Set(ref info, GpuFamily.AppleA, model, t);
                }
                return info;
            }
            if (s.Contains("intel"))
            {
                if (s.Contains(" arc")) return Set(ref info, GpuFamily.Desktop, 0, QualityTier.High);
                return Set(ref info, GpuFamily.IntelIntegrated, 0, QualityTier.Mid);
            }
            if (s.Contains("nvidia") || s.Contains("geforce") || s.Contains("quadro") || s.Contains("radeon") || s.StartsWith("amd", StringComparison.Ordinal))
                return Set(ref info, GpuFamily.Desktop, 0, QualityTier.High);
            return info;
        }

        /// <summary>
        /// Adreno by model number: the hundreds are the generation, the tens the rank within it. Generation 5 and older:
        /// Low. 6xx: 610-620 Low (Snapdragon 680/685/695/4 Gen 1-2), 630-644 Mid (845, 855, 778G, 7 Gen 1), 650+ High
        /// (865, 888). 7xx: 702 Low, 710 Mid (6 Gen 1/3, 7s Gen 2, 6s Gen 4), 720+ High (7 Gen 3, 7 Gen 4, 7+, 8-series).
        /// 8xx: 810-819 Mid (7s Gen 3, 6 Gen 5), 820+ High (8 Elite and later). 9xx and later: High.
        /// </summary>
        public static QualityTier AdrenoCeiling(int model)
        {
            int generation = model / 100, rank = model % 100;
            if (generation <= 5) return QualityTier.Low;
            if (generation == 6) return rank < 30 ? QualityTier.Low : rank < 50 ? QualityTier.Mid : QualityTier.High;
            if (generation == 7) return rank < 10 ? QualityTier.Low : rank < 20 ? QualityTier.Mid : QualityTier.High;
            if (generation == 8) return rank < 20 ? QualityTier.Mid : QualityTier.High;
            return QualityTier.High;
        }

        /// <summary>
        /// Mali by model and (when reported) core count. G31/G51/G52/G57 and the old Midgard T-series and Utgard
        /// 400-series: Low. G68: MP4 and more Mid, MP2 Low, unknown Mid (MP4/MP5 is the common build). G71/G72 Low, G76
        /// MP10+ Mid else Low, G77 Mid, G78 MP14+ High else Mid. G6xx (610, 615, 625): MP6+ High, else Mid. G7xx (710,
        /// 715, 720, 725) and G9xx: High. 5th generation G1: Pro Mid, Premium and Ultra High.
        /// </summary>
        public static QualityTier MaliCeiling(int model, int cores, string variant)
        {
            if (model == 1) return variant == "pro" ? QualityTier.Mid : QualityTier.High;
            if (model < 60) return QualityTier.Low;
            if (model == 68) return cores == 0 || cores >= 4 ? QualityTier.Mid : QualityTier.Low;
            if (model < 76) return QualityTier.Low;
            if (model == 76) return cores >= 10 ? QualityTier.Mid : QualityTier.Low;
            if (model == 77) return QualityTier.Mid;
            if (model == 78) return cores >= 14 ? QualityTier.High : QualityTier.Mid;
            if (model >= 600 && model < 700) return cores >= 6 ? QualityTier.High : QualityTier.Mid;
            if (model >= 700) return QualityTier.High;
            return QualityTier.Mid;
        }

        private static GpuInfo Mali(ref GpuInfo info, string s, int i)
        {
            i = SkipSeparators(s, i);
            if (i >= s.Length) return info;
            char c = s[i];
            if (c == 't' || IsDigit(c))
            {
                // Midgard (T-series) and Utgard (Mali-400/450/470): far below the floor.
                info.Cores = CoresAfter(s, i);
                return Set(ref info, GpuFamily.Mali, NumberAfter(s, i, 4), QualityTier.Low);
            }
            if (c != 'g') return info;
            int model = NumberAfter(s, i + 1, 1);
            if (model <= 0) return info;
            if (model == 1)
            {
                info.Variant = s.Contains("ultra") ? "ultra" : s.Contains("premium") ? "premium" : s.Contains("pro") ? "pro" : null;
            }
            info.Cores = CoresAfter(s, i);
            return Set(ref info, GpuFamily.Mali, model, MaliCeiling(model, info.Cores, info.Variant));
        }

        private static GpuInfo PowerVr(ref GpuInfo info, string s)
        {
            // D-Series (DXT, DXD) is the 2024+ flagship line (Tensor G5); C-Series (CXT, CXM) the mid range; Rogue
            // (GE8320/GE8322 in Helio G35/G37/P35) and B-Series (BXM, BXE in Dimensity 930/Unisoc) are entry level.
            if (s.Contains("dxt") || s.Contains("dxd") || s.Contains("d-series"))
            {
                info.Variant = "d";
                return Set(ref info, GpuFamily.PowerVR, 0, QualityTier.High);
            }
            if (s.Contains("cxt") || s.Contains("cxm") || s.Contains("c-series"))
            {
                info.Variant = "c";
                return Set(ref info, GpuFamily.PowerVR, 0, QualityTier.Mid);
            }
            info.Variant = s.Contains("bx") || s.Contains("b-series") ? "b" : "rogue";
            int at = s.IndexOf("ge8", StringComparison.Ordinal);
            int model = at >= 0 ? NumberAfter(s, at + 2, 1) : 0;
            return Set(ref info, GpuFamily.PowerVR, model, QualityTier.Low);
        }

        private static GpuInfo Set(ref GpuInfo info, GpuFamily family, int model, QualityTier ceiling)
        {
            info.Family = family;
            info.Model = model;
            info.Ceiling = ceiling;
            info.Known = true;
            return info;
        }

        /// <summary>The first run of digits at or after <paramref name="from"/> within <paramref name="window"/>
        /// characters of it (skipping "(tm)", spaces and dashes); 0 when none.</summary>
        private static int NumberAfter(string s, int from, int window)
        {
            int i = from, limit = Math.Min(s.Length, from + window + 1);
            while (i < s.Length && !IsDigit(s[i]))
            {
                if (i >= limit) return 0;
                i++;
            }
            int n = 0, digits = 0;
            for (; i < s.Length && IsDigit(s[i]) && digits < 6; i++, digits++) n = n * 10 + (s[i] - '0');
            return n;
        }

        /// <summary>Core count from "mc2", "mp5" or "mc12" after <paramref name="from"/>; 0 when absent.</summary>
        private static int CoresAfter(string s, int from)
        {
            for (int i = Math.Max(0, from); i + 2 < s.Length; i++)
            {
                if (s[i] != 'm' || (s[i + 1] != 'c' && s[i + 1] != 'p') || !IsDigit(s[i + 2])) continue;
                if (i > 0 && char.IsLetter(s[i - 1])) continue; // "...amp4": not a core count
                return NumberAfter(s, i + 2, 0);
            }
            return 0;
        }

        private static int SkipSpaces(string s, int i)
        {
            while (i < s.Length && s[i] == ' ') i++;
            return i;
        }

        private static int SkipSeparators(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '-' || s[i] == '_' || s[i] == '(' || s[i] == ')')) i++;
            if (i + 2 < s.Length && s[i] == 't' && s[i + 1] == 'm' && s[i + 2] == ')') i = SkipSeparators(s, i + 3);
            return i;
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
