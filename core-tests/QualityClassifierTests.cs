using Ghumante.Core.Platform;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The device tier table (docs/research/w2/ref_quality.md): real phones sold in Nepal from 2023 to 2026 with the
    /// renderer string Unity reports, the RAM Android reports (4 GB phones about 3.7 GB, 6 GB about 5.6 GB, 8 GB about
    /// 7.5 GB, 12 GB about 11.4 GB) and their core count, and the tier they must get.
    /// </summary>
    public class QualityClassifierTests
    {
        private const int Gb4 = 3700, Gb6 = 5600, Gb8 = 7500, Gb12 = 11400, Gb16 = 15200;

        private static QualityTier Android(string gpu, int ramMB, int cores = 8)
        {
            var p = new DeviceProfile { Kind = DeviceKind.Android, SystemMemoryMB = ramMB, ProcessorCount = cores, GraphicsDeviceName = gpu };
            return DeviceClassifier.Classify(p).Tier;
        }

        private static QualityTier Ios(string model, int ramMB, string gpu = null)
        {
            var p = new DeviceProfile { Kind = DeviceKind.Ios, SystemMemoryMB = ramMB, ProcessorCount = 6, DeviceModel = model, GraphicsDeviceName = gpu };
            return DeviceClassifier.Classify(p).Tier;
        }

        private static QualityTier Editor(DeviceKind kind, string gpu, int ramMB)
        {
            var p = new DeviceProfile { Kind = kind, SystemMemoryMB = ramMB, ProcessorCount = 10, GraphicsDeviceName = gpu };
            return DeviceClassifier.Classify(p).Tier;
        }

        // ----- Samsung Galaxy -----------------------------------------------------------------------------------

        [TestCase("Mali-G52 MC2", Gb4, QualityTier.Low, TestName = "Galaxy A05/A06 (Helio G85, 4 GB)")]
        [TestCase("Adreno (TM) 610", Gb4, QualityTier.Low, TestName = "Galaxy A05s (Snapdragon 680, 4 GB)")]
        [TestCase("Mali-G57 MC2", Gb6, QualityTier.Low, TestName = "Galaxy A07/A08 (Helio G99, 6 GB)")]
        [TestCase("Mali-G57 MC2", Gb8, QualityTier.Low, TestName = "Galaxy A15 (Helio G99, 8 GB): RAM is not enough")]
        [TestCase("Mali-G68 MP2", Gb8, QualityTier.Low, TestName = "Galaxy A16/A17 5G (Exynos 1330, MP2 reported)")]
        [TestCase("Mali-G68", Gb6, QualityTier.Mid, TestName = "Galaxy A17 5G (Exynos 1330, no core count): Mid, governor decides")]
        [TestCase("Mali-G68 MP4", Gb6, QualityTier.Mid, TestName = "Galaxy A25 (Exynos 1280, 6 GB)")]
        [TestCase("Mali-G68 MP5", Gb8, QualityTier.Mid, TestName = "Galaxy A35/A26/M36 (Exynos 1380, 8 GB)")]
        [TestCase("Adreno (TM) 710", Gb8, QualityTier.Mid, TestName = "Galaxy A27/A36 (Snapdragon 6 Gen 3, 8 GB)")]
        [TestCase("Samsung Xclipse 530", Gb8, QualityTier.Mid, TestName = "Galaxy A55/A37 (Exynos 1480, 8 GB)")]
        [TestCase("Samsung Xclipse 540", Gb8, QualityTier.High, TestName = "Galaxy A56 (Exynos 1580, 8 GB)")]
        [TestCase("Samsung Xclipse 540", Gb6, QualityTier.Mid, TestName = "Galaxy A56 6 GB: memory caps it at Mid")]
        [TestCase("Samsung Xclipse 550", Gb8, QualityTier.High, TestName = "Galaxy A57 (Exynos 1680, 8 GB)")]
        [TestCase("Samsung Xclipse 940", Gb8, QualityTier.High, TestName = "Galaxy S24/S25 FE (Exynos 2400, 8 GB)")]
        [TestCase("Adreno (TM) 740", Gb8, QualityTier.High, TestName = "Galaxy S23 (Snapdragon 8 Gen 2, 8 GB)")]
        [TestCase("Adreno (TM) 830", Gb12, QualityTier.High, TestName = "Galaxy S25 Ultra (Snapdragon 8 Elite, 12 GB)")]
        public void SamsungGalaxy(string gpu, int ram, QualityTier expected)
        {
            Assert.That(Android(gpu, ram), Is.EqualTo(expected));
        }

        // ----- Xiaomi / Redmi -----------------------------------------------------------------------------------

        [TestCase("Mali-G57 MP1", Gb4, QualityTier.Low, TestName = "Redmi A5/A7 (Unisoc T7250, 4 GB)")]
        [TestCase("Mali-G52 MC2", Gb8, QualityTier.Low, TestName = "Redmi 14C/15C (Helio G81-Ultra, 8 GB)")]
        [TestCase("Adreno (TM) 610", Gb8, QualityTier.Low, TestName = "Redmi 15 / Note 13 4G (Snapdragon 685, 8 GB)")]
        [TestCase("Adreno (TM) 613", Gb8, QualityTier.Low, TestName = "Redmi Note 17 5G (Snapdragon 4 Gen 4, 8 GB)")]
        [TestCase("Mali-G57 MC2", Gb12, QualityTier.Low, TestName = "Redmi Note 14 5G (Dimensity 7025, 12 GB)")]
        [TestCase("Adreno (TM) 710", Gb8, QualityTier.Mid, TestName = "Redmi Note 13 Pro 5G (Snapdragon 7s Gen 2) / Note 17 Pro (6s Gen 4)")]
        [TestCase("Mali-G615 MC2", Gb8, QualityTier.Mid, TestName = "Redmi Note 14 Pro 5G (Dimensity 7300, 8 GB)")]
        [TestCase("Adreno (TM) 810", Gb12, QualityTier.Mid, TestName = "Redmi Note 14 Pro+ (Snapdragon 7s Gen 3, 12 GB)")]
        [TestCase("Mali-G615 MC6", Gb12, QualityTier.High, TestName = "Xiaomi 14T (Dimensity 8300, 12 GB)")]
        [TestCase("Mali-G720 MC8", Gb12, QualityTier.High, TestName = "Xiaomi 17T (Dimensity 8500, 12 GB)")]
        [TestCase("Adreno (TM) 830", Gb16, QualityTier.High, TestName = "Xiaomi 15 Ultra (Snapdragon 8 Elite, 16 GB)")]
        public void XiaomiRedmi(string gpu, int ram, QualityTier expected)
        {
            Assert.That(Android(gpu, ram), Is.EqualTo(expected));
        }

        // ----- Realme, Vivo, Oppo -------------------------------------------------------------------------------

        [TestCase("Mali-G57 MP1", Gb6, QualityTier.Low, TestName = "realme C61/C63, Narzo N61, vivo Y19s (Unisoc T612)")]
        [TestCase("Mali-G57 MC2", Gb8, QualityTier.Low, TestName = "realme C67 5G / 12x (Dimensity 6100+, 8 GB)")]
        [TestCase("Mali-G57 MC2", Gb12, QualityTier.Low, TestName = "realme 15T (Dimensity 6400, 12 GB): plenty of RAM, weak GPU")]
        [TestCase("Mali-G68 MC4", Gb8, QualityTier.Mid, TestName = "realme Narzo 70 Pro, OPPO Reno 10/11F (Dimensity 7050)")]
        [TestCase("Adreno (TM) 710", Gb12, QualityTier.Mid, TestName = "realme 12 Pro+ (7s Gen 2), OPPO Reno 14F/15F (6 Gen 1)")]
        [TestCase("Adreno (TM) 732", Gb12, QualityTier.High, TestName = "realme GT 6T (Snapdragon 7+ Gen 3, 12 GB)")]
        [TestCase("Mali-G52 MC2", Gb8, QualityTier.Low, TestName = "vivo Y28, OPPO A18/A38/A58 (Helio G85)")]
        [TestCase("Adreno (TM) 610", Gb8, QualityTier.Low, TestName = "vivo Y29 / V50 Lite 4G, OPPO A60/A78 (Snapdragon 680/685)")]
        [TestCase("Mali-G57 MC2", Gb12, QualityTier.Low, TestName = "vivo V50 Lite 5G (Dimensity 6300, 12 GB)")]
        [TestCase("Adreno (TM) 720", Gb12, QualityTier.High, TestName = "vivo V30/V40/V50 (Snapdragon 7 Gen 3, 12 GB)")]
        [TestCase("Adreno (TM) 722", Gb12, QualityTier.High, TestName = "vivo V60 (Snapdragon 7 Gen 4, 12 GB)")]
        [TestCase("Immortalis-G925 MC12", Gb16, QualityTier.High, TestName = "vivo X200 Pro (Dimensity 9400, 16 GB)")]
        [TestCase("Mali-G1-Ultra MC12", Gb16, QualityTier.High, TestName = "vivo X300 Pro (Dimensity 9500, 16 GB)")]
        [TestCase("Adreno (TM) 610", Gb4, QualityTier.Low, TestName = "OPPO A3x/A5x (Snapdragon 6s 4G Gen 1, 4 GB)")]
        [TestCase("PowerVR Rogue GE8320", Gb4, QualityTier.Low, TestName = "OPPO A17/A57/A77 (Helio G35)")]
        [TestCase("Mali-G615 MC2", Gb12, QualityTier.Mid, TestName = "OPPO Reno 12/15 (Dimensity 7300/7400, 12 GB)")]
        public void RealmeVivoOppo(string gpu, int ram, QualityTier expected)
        {
            Assert.That(Android(gpu, ram), Is.EqualTo(expected));
        }

        [Test]
        public void MemoryAndCoresStillCap()
        {
            Assert.That(Android("Adreno (TM) 740", Gb4), Is.EqualTo(QualityTier.Low), "a flagship GPU with 4 GB is Low");
            Assert.That(Android("Adreno (TM) 740", Gb6), Is.EqualTo(QualityTier.Mid), "6 GB: at most Mid");
            Assert.That(Android("Adreno (TM) 740", Gb12, 4), Is.EqualTo(QualityTier.Low), "fewer than 6 cores: Low");
        }

        [Test]
        public void UnknownGpuFallsBackToTheRamProxy()
        {
            // The pre-table heuristic (ProjectTests.TierHeuristicMatchesArchitectureTable) still holds without a GPU name.
            Assert.That(Android(null, 3800), Is.EqualTo(QualityTier.Low));
            Assert.That(Android(null, 7600, 4), Is.EqualTo(QualityTier.Low));
            Assert.That(Android(null, 7600), Is.EqualTo(QualityTier.Mid));
            Assert.That(Android(null, 11800), Is.EqualTo(QualityTier.High));
            Assert.That(Android("Vivante GC7000UL", 11800), Is.EqualTo(QualityTier.High), "an unknown family sets no ceiling");
            Assert.That(Android(null, 0), Is.EqualTo(QualityTier.Mid), "unknown memory: conservative");
            Assert.That(Android("Adreno (TM) 610", 0), Is.EqualTo(QualityTier.Low), "unknown memory never beats the GPU");
            Assert.That(Android("Google SwiftShader", Gb8), Is.EqualTo(QualityTier.Low), "emulator software renderer");
        }

        // ----- Apple --------------------------------------------------------------------------------------------

        [TestCase("iPhone11,8", 2900, QualityTier.Low, TestName = "iPhone XR (A12, 3 GB)")]
        [TestCase("iPhone12,8", 2900, QualityTier.Low, TestName = "iPhone SE 2 (A13, 3 GB)")]
        [TestCase("iPhone12,1", 3700, QualityTier.Mid, TestName = "iPhone 11 (A13, 4 GB)")]
        [TestCase("iPhone13,2", 3700, QualityTier.High, TestName = "iPhone 12 (A14)")]
        [TestCase("iPhone14,5", 3700, QualityTier.High, TestName = "iPhone 13 (A15)")]
        [TestCase("iPhone14,6", 3700, QualityTier.High, TestName = "iPhone SE 3 (A15)")]
        [TestCase("iPhone15,4", 5600, QualityTier.High, TestName = "iPhone 15 (A16)")]
        [TestCase("iPhone17,3", 7500, QualityTier.High, TestName = "iPhone 16 (A18)")]
        [TestCase("iPhone18,1", 11400, QualityTier.High, TestName = "iPhone 17 Pro (A19 Pro)")]
        public void IPhones(string model, int ram, QualityTier expected)
        {
            Assert.That(Ios(model, ram), Is.EqualTo(expected));
        }

        [Test]
        public void IPadsGoByAppleGpuThenRam()
        {
            Assert.That(Ios("iPad13,1", 3700, "Apple A14 GPU"), Is.EqualTo(QualityTier.High), "iPad Air 4 (A14)");
            Assert.That(Ios("iPad11,6", 2900, "Apple A12 GPU"), Is.EqualTo(QualityTier.Low), "iPad 8 (A12, 3 GB)");
            Assert.That(Ios("iPad13,4", 7600, "Apple M1 GPU"), Is.EqualTo(QualityTier.High), "M1 iPad Pro");
            Assert.That(Ios("iPad13,4", 7600), Is.EqualTo(QualityTier.High), "no GPU name: RAM");
            Assert.That(Ios(null, 3700), Is.EqualTo(QualityTier.Mid));
            Assert.That(Ios("", 2900), Is.EqualTo(QualityTier.Low));
        }

        [Test]
        public void MacEditorAppleSiliconNeeds16Gb()
        {
            Assert.That(Editor(DeviceKind.MacEditor, "Apple M1", 8192), Is.EqualTo(QualityTier.Mid));
            Assert.That(Editor(DeviceKind.MacEditor, "Apple M2", 16384), Is.EqualTo(QualityTier.High));
            Assert.That(Editor(DeviceKind.MacEditor, "Apple M3 Pro", 18432), Is.EqualTo(QualityTier.High));
            Assert.That(Editor(DeviceKind.MacEditor, "Apple M3 Max", 36864), Is.EqualTo(QualityTier.High));
            Assert.That(Editor(DeviceKind.MacEditor, "Intel(R) Iris(TM) Plus Graphics", 16384), Is.EqualTo(QualityTier.Mid));
            Assert.That(Editor(DeviceKind.MacEditor, "AMD Radeon Pro 5500M", 32768), Is.EqualTo(QualityTier.High));
        }

        [Test]
        public void OtherEditorsAndDesktops()
        {
            Assert.That(Editor(DeviceKind.Editor, "NVIDIA GeForce RTX 3070", 32768), Is.EqualTo(QualityTier.High));
            Assert.That(Editor(DeviceKind.Editor, "Intel(R) UHD Graphics 620", 16384), Is.EqualTo(QualityTier.Mid));
            Assert.That(Editor(DeviceKind.Editor, "llvmpipe (LLVM 15.0.7, 256 bits)", 16384), Is.EqualTo(QualityTier.Low));
            Assert.That(Editor(DeviceKind.Desktop, null, 8192), Is.EqualTo(QualityTier.Mid));
        }

        [Test]
        public void DecisionExplainsItself()
        {
            var p = new DeviceProfile
            {
                Kind = DeviceKind.Android, SystemMemoryMB = Gb12, ProcessorCount = 8, DeviceModel = "vivo V2427",
                GraphicsDeviceName = "Mali-G57 MC2",
            };
            TierDecision d = DeviceClassifier.Classify(p);
            Assert.That(d.Tier, Is.EqualTo(QualityTier.Low));
            Assert.That(d.RamTier, Is.EqualTo(QualityTier.High));
            Assert.That(d.Rule, Is.EqualTo("gpu-ceiling"));
            string line = d.Describe(p);
            StringAssert.Contains("Low by gpu-ceiling", line);
            StringAssert.Contains("Mali 57", line);
            StringAssert.Contains("vivo V2427", line);
        }

        // ----- The GPU table on its own -------------------------------------------------------------------------

        [Test]
        public void ParsesRendererStrings()
        {
            GpuInfo g = GpuTierTable.Parse("Adreno (TM) 642L");
            Assert.That((g.Family, g.Model, g.Ceiling, g.Known), Is.EqualTo((GpuFamily.Adreno, 642, QualityTier.Mid, true)));
            g = GpuTierTable.Parse("Mali-G57 MC2");
            Assert.That((g.Family, g.Model, g.Cores, g.Ceiling), Is.EqualTo((GpuFamily.Mali, 57, 2, QualityTier.Low)));
            g = GpuTierTable.Parse("Mali-G610 MC6");
            Assert.That((g.Model, g.Cores, g.Ceiling), Is.EqualTo((610, 6, QualityTier.High)));
            g = GpuTierTable.Parse("Mali-G610");
            Assert.That((g.Model, g.Cores, g.Ceiling), Is.EqualTo((610, 0, QualityTier.Mid)), "no core count: the lower answer");
            g = GpuTierTable.Parse("Mali-G1-Pro MC6");
            Assert.That((g.Model, g.Variant, g.Ceiling), Is.EqualTo((1, "pro", QualityTier.Mid)));
            g = GpuTierTable.Parse("Mali-T830 MP2");
            Assert.That((g.Family, g.Ceiling), Is.EqualTo((GpuFamily.Mali, QualityTier.Low)));
            g = GpuTierTable.Parse("Immortalis-G715 MC11");
            Assert.That((g.Family, g.Model, g.Cores, g.Ceiling), Is.EqualTo((GpuFamily.Immortalis, 715, 11, QualityTier.High)));
            g = GpuTierTable.Parse("Xclipse 920");
            Assert.That((g.Family, g.Model, g.Ceiling), Is.EqualTo((GpuFamily.Xclipse, 920, QualityTier.High)));
            g = GpuTierTable.Parse("PowerVR B-Series BXM-8-256");
            Assert.That((g.Family, g.Variant, g.Ceiling), Is.EqualTo((GpuFamily.PowerVR, "b", QualityTier.Low)));
            g = GpuTierTable.Parse("PowerVR D-Series DXT-48-1536");
            Assert.That((g.Family, g.Variant, g.Ceiling), Is.EqualTo((GpuFamily.PowerVR, "d", QualityTier.High)));
            g = GpuTierTable.Parse("Apple A15 GPU");
            Assert.That((g.Family, g.Model, g.Ceiling), Is.EqualTo((GpuFamily.AppleA, 15, QualityTier.High)));
            g = GpuTierTable.Parse("Apple M2 Max");
            Assert.That((g.Family, g.Model), Is.EqualTo((GpuFamily.AppleM, 2)));
            g = GpuTierTable.Parse("Maleoon 910");
            Assert.That((g.Family, g.Ceiling), Is.EqualTo((GpuFamily.Maleoon, QualityTier.Mid)));
            Assert.That(GpuTierTable.Parse("").Known, Is.False);
            Assert.That(GpuTierTable.Parse(null).Known, Is.False);
            Assert.That(GpuTierTable.Parse("Adreno X1-85").Known, Is.False, "Windows on Arm: no ceiling");
        }

        [Test]
        public void AdrenoRanksWithinEachGeneration()
        {
            Assert.That(GpuTierTable.AdrenoCeiling(540), Is.EqualTo(QualityTier.Low));
            Assert.That(GpuTierTable.AdrenoCeiling(619), Is.EqualTo(QualityTier.Low));
            Assert.That(GpuTierTable.AdrenoCeiling(620), Is.EqualTo(QualityTier.Low));
            Assert.That(GpuTierTable.AdrenoCeiling(630), Is.EqualTo(QualityTier.Mid));
            Assert.That(GpuTierTable.AdrenoCeiling(644), Is.EqualTo(QualityTier.Mid));
            Assert.That(GpuTierTable.AdrenoCeiling(650), Is.EqualTo(QualityTier.High));
            Assert.That(GpuTierTable.AdrenoCeiling(702), Is.EqualTo(QualityTier.Low));
            Assert.That(GpuTierTable.AdrenoCeiling(710), Is.EqualTo(QualityTier.Mid));
            Assert.That(GpuTierTable.AdrenoCeiling(720), Is.EqualTo(QualityTier.High));
            Assert.That(GpuTierTable.AdrenoCeiling(812), Is.EqualTo(QualityTier.Mid));
            Assert.That(GpuTierTable.AdrenoCeiling(840), Is.EqualTo(QualityTier.High));
            Assert.That(GpuTierTable.AdrenoCeiling(910), Is.EqualTo(QualityTier.High), "future generations");
        }
    }
}
