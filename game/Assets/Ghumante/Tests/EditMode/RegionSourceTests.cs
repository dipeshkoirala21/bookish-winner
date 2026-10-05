using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ghumante.Core.Data;
using Ghumante.Core.Services;
using Ghumante.Platform.Regions;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Region provisioning: file layout, manifest validation, SHA-256 checks and BuiltInRegionSource in both modes
    /// (open in place, and unpack to persistent storage as on Android, here with the file-system fetcher). Engine-free.
    /// </summary>
    public class RegionSourceTests
    {
        private SynchronizationContext _savedContext;
        private readonly List<string> _temp = new List<string>();

        [SetUp]
        public void NoSynchronizationContext()
        {
            // The source's awaits would post to Unity's context, which a blocking test never pumps.
            _savedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void Cleanup()
        {
            SynchronizationContext.SetSynchronizationContext(_savedContext);
            foreach (string dir in _temp)
            {
                try
                {
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
            _temp.Clear();
        }

        private string TempRoot()
        {
            string dir = Path.Combine(Path.GetTempPath(), "gh-regions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            _temp.Add(dir);
            return dir;
        }

        /// <summary>A root holding Regions/kathmandu_core/ with the four sample files.</summary>
        private string RootWithSample()
        {
            string root = TempRoot();
            string folder = Path.Combine(root, RegionFiles.FolderName, SampleRegion.Id);
            Directory.CreateDirectory(folder);
            foreach (RegionFileKind kind in new[] { RegionFileKind.Manifest, RegionFileKind.Pack, RegionFileKind.SearchIndex, RegionFileKind.RouteGraph })
            {
                string name = RegionFiles.FileName(SampleRegion.Id, kind);
                File.Copy(Path.Combine(SampleRegion.Folder, name), Path.Combine(folder, name));
            }
            return root;
        }

        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void FileNamesAndPathsFollowTheRegionLayout()
        {
            Assert.AreEqual("kathmandu_core.manifest.json", RegionFiles.FileName("kathmandu_core", RegionFileKind.Manifest));
            Assert.AreEqual("kathmandu_core.ghpk", RegionFiles.FileName("kathmandu_core", RegionFileKind.Pack));
            Assert.AreEqual("kathmandu_core.search.ghsi", RegionFiles.FileName("kathmandu_core", RegionFileKind.SearchIndex));
            Assert.AreEqual("kathmandu_core.route.ghrg", RegionFiles.FileName("kathmandu_core", RegionFileKind.RouteGraph));

            const string apk = "jar:file:///data/app/com.ghumante.game/base.apk!/assets";
            Assert.IsTrue(RegionFiles.IsUrl(apk));
            Assert.IsTrue(RegionFiles.IsUrl("file:///Users/me/StreamingAssets"));
            Assert.IsFalse(RegionFiles.IsUrl("/Users/me/game/Assets/StreamingAssets"));
            Assert.IsFalse(RegionFiles.IsUrl(@"C:\game\Assets\StreamingAssets"));
            Assert.AreEqual(apk + "/Regions/kathmandu_core/kathmandu_core.ghpk", RegionFiles.FilePath(apk + "/", "kathmandu_core", RegionFileKind.Pack));
            Assert.AreEqual("/data/x/Regions/regions.json", RegionFiles.IndexPath("/data/x"));
        }

        [Test]
        public void RegionIdsCannotEscapeTheirFolder()
        {
            Assert.IsTrue(RegionFiles.IsValidRegionId("kathmandu_valley"));
            Assert.IsTrue(RegionFiles.IsValidRegionId("pokhara-2"));
            foreach (string bad in new[] { null, "", "../x", "a/b", "A", "_x", "-x", "x.y", "x y", new string('a', 65) })
                Assert.IsFalse(RegionFiles.IsValidRegionId(bad), "'" + bad + "' must be rejected");
            Assert.Throws<ArgumentException>(() => RegionFiles.FileName("../evil", RegionFileKind.Pack));
        }

        [Test]
        public void IndexRoundTripsSortedValidIds()
        {
            string json = RegionFiles.FormatIndex(new[] { "pokhara", "kathmandu_core", "pokhara", "Bad Id" });
            CollectionAssert.AreEqual(new[] { "kathmandu_core", "pokhara" }, RegionFiles.ParseIndex(json));
            CollectionAssert.AreEqual(new[] { "a", "b" }, RegionFiles.ParseIndex("[\"b\", \"a\"]"));
            CollectionAssert.IsEmpty(RegionFiles.ParseIndex(RegionFiles.FormatIndex(new string[0])));
            Assert.Throws<FormatException>(() => RegionFiles.ParseIndex("{\"nope\": 1}"));
        }

        [Test]
        public void SampleManifestParsesAndMatchesItsFiles()
        {
            RegionManifest m = RegionManifest.Parse(File.ReadAllText(SampleRegion.FilePath(".manifest.json")));
            Assert.AreEqual(SampleRegion.Id, m.RegionId);
            Assert.AreEqual(10, m.LeafLevel);
            CollectionAssert.AreEqual(new[] { 8, 9, 10 }, m.DetailLevels);
            CollectionAssert.AreEqual(new[] { 5, 6 }, m.HorizonLevels);
            Assert.IsTrue(m.HasBounds);
            Assert.That(SampleRegion.ThamelX, Is.InRange(m.MinX, m.MaxX));
            Assert.That(SampleRegion.ThamelZ, Is.InRange(m.MinZ, m.MaxZ));
            Assert.AreEqual(3, m.Files.Count);
            Assert.IsNotEmpty(m.Attribution);
            foreach (RegionFileKind kind in RegionFiles.DataKinds)
            {
                RegionFileEntry e = m.FileFor(kind);
                Assert.IsNotNull(e, kind.ToString());
                Assert.AreEqual(new FileInfo(Path.Combine(SampleRegion.Folder, e.Path)).Length, e.Bytes);
            }
            RegionFileEntry search = m.FileFor(RegionFileKind.SearchIndex);
            Assert.AreEqual(search.Sha256, RegionFileVerifier.Sha256Hex(Path.Combine(SampleRegion.Folder, search.Path)));
            string problem;
            Assert.IsTrue(RegionFileVerifier.Check(Path.Combine(SampleRegion.Folder, search.Path), search, true, out problem), problem);
        }

        [Test]
        public void ManifestRejectsUnsafeOrBrokenEntries()
        {
            string good = File.ReadAllText(SampleRegion.FilePath(".manifest.json"));
            Assert.Throws<FormatException>(() => RegionManifest.Parse(good.Replace("\"kathmandu_core.ghpk\"", "\"../kathmandu_core.ghpk\"")));
            Assert.Throws<FormatException>(() => RegionManifest.Parse(good.Replace("ghumante-region-manifest", "something-else")));
            Assert.Throws<FormatException>(() => RegionManifest.Parse(good.Replace("\"region\": \"kathmandu_core\"", "\"region\": \"../x\"")));
            Assert.Throws<FormatException>(() => RegionManifest.Parse("[]"));
            Assert.Throws<FormatException>(() => RegionManifest.Parse("{\"format\": \"ghumante-region-manifest\", \"region\": \"x\", \"files\": []}"));
            Assert.IsFalse(RegionManifest.IsPlainFileName("a/b"));
            Assert.IsFalse(RegionManifest.IsPlainFileName(".."));
            Assert.IsTrue(RegionManifest.IsSha256Hex(new string('a', 64)));
            Assert.IsFalse(RegionManifest.IsSha256Hex(new string('A', 64)));
        }

        [Test]
        public void InPlaceSourceListsChecksAndOpensRegions()
        {
            string root = RootWithSample();
            var source = new BuiltInRegionSource(root, null, false, new LocalFileFetcher());
            CollectionAssert.AreEqual(new[] { SampleRegion.Id }, source.ListRegionsAsync().Result);
            Assert.IsTrue(source.IsAvailableAsync(SampleRegion.Id).Result);
            Assert.IsFalse(source.IsAvailableAsync("pokhara").Result);
            Assert.IsFalse(source.IsAvailableAsync("../x").Result);
            float last = -1f;
            Assert.IsTrue(source.RequestAsync(SampleRegion.Id, new SyncProgress(p => last = p)).Result);
            Assert.AreEqual(1f, last);

            using (Stream s = source.OpenAsync(SampleRegion.Id, RegionFileKind.Pack).Result)
            {
                Assert.IsTrue(s.CanSeek);
                using (var pack = new PackReader(s, false)) Assert.Greater(pack.TileCount, 200);
            }
            Assert.Throws<AggregateException>(() => source.OpenAsync("pokhara", RegionFileKind.Pack).Wait());

            // A truncated file makes the region unavailable.
            string search = RegionFiles.FilePath(root, SampleRegion.Id, RegionFileKind.SearchIndex);
            using (var fs = new FileStream(search, FileMode.Open)) fs.SetLength(fs.Length - 1);
            Assert.IsFalse(source.IsAvailableAsync(SampleRegion.Id).Result);
            StringAssert.Contains("search.ghsi", source.LastError);
        }

        [Test]
        public void UnpackingSourceCopiesVerifiesAndReusesTheInstall()
        {
            string packaged = RootWithSample();
            string install = TempRoot();
            var source = new BuiltInRegionSource(packaged, install, true, new LocalFileFetcher());
            Assert.IsFalse(source.IsAvailableAsync(SampleRegion.Id).Result, "nothing unpacked yet");
            Assert.Throws<AggregateException>(() => source.OpenAsync(SampleRegion.Id, RegionFileKind.Pack).Wait());

            var reports = new List<float>();
            Assert.IsTrue(source.RequestAsync(SampleRegion.Id, new SyncProgress(reports.Add)).Result, source.LastError);
            Assert.IsNotEmpty(reports);
            Assert.AreEqual(1f, reports[reports.Count - 1]);
            for (int i = 1; i < reports.Count; i++) Assert.GreaterOrEqual(reports[i], reports[i - 1], "progress never goes back");
            string installedPack = RegionFiles.FilePath(install, SampleRegion.Id, RegionFileKind.Pack);
            Assert.IsTrue(File.Exists(installedPack));
            Assert.IsTrue(File.Exists(RegionFiles.FilePath(install, SampleRegion.Id, RegionFileKind.Manifest)));
            Assert.IsTrue(source.IsAvailableAsync(SampleRegion.Id).Result);
            using (Stream s = source.OpenAsync(SampleRegion.Id, RegionFileKind.Pack).Result)
                Assert.AreEqual(new FileInfo(installedPack).Length, s.Length);

            // A second app session finds the install and does not copy again.
            DateTime written = File.GetLastWriteTimeUtc(installedPack);
            var next = new BuiltInRegionSource(packaged, install, true, new LocalFileFetcher());
            Assert.IsTrue(next.IsAvailableAsync(SampleRegion.Id).Result);
            Assert.IsTrue(next.RequestAsync(SampleRegion.Id).Result);
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(installedPack));

            // Corruption that keeps the size is caught by the hash check and repaired by a new request.
            using (var fs = new FileStream(installedPack, FileMode.Open))
            {
                fs.Position = 1000;
                int b = fs.ReadByte();
                fs.Position = 1000;
                fs.WriteByte((byte)(b ^ 0xFF));
            }
            var paranoid = new BuiltInRegionSource(packaged, install, true, new LocalFileFetcher()) { VerifyHashesOnCheck = true };
            Assert.IsFalse(paranoid.IsAvailableAsync(SampleRegion.Id).Result);
            Assert.IsTrue(paranoid.RequestAsync(SampleRegion.Id).Result, paranoid.LastError);
            Assert.IsTrue(paranoid.IsAvailableAsync(SampleRegion.Id).Result);

            // An app update that ships a different manifest invalidates the install.
            string manifestPath = RegionFiles.FilePath(packaged, SampleRegion.Id, RegionFileKind.Manifest);
            File.AppendAllText(manifestPath, "\n");
            var updated = new BuiltInRegionSource(packaged, install, true, new LocalFileFetcher());
            Assert.IsFalse(updated.IsAvailableAsync(SampleRegion.Id).Result);
            Assert.IsTrue(updated.RequestAsync(SampleRegion.Id).Result, updated.LastError);
            Assert.AreEqual(File.ReadAllText(manifestPath), File.ReadAllText(RegionFiles.FilePath(install, SampleRegion.Id, RegionFileKind.Manifest)));
        }

        [Test]
        public void UnpackingFailsCleanlyWhenThePackagedFileIsWrong()
        {
            string packaged = RootWithSample();
            string install = TempRoot();
            string pack = RegionFiles.FilePath(packaged, SampleRegion.Id, RegionFileKind.Pack);
            using (var fs = new FileStream(pack, FileMode.Open))
            {
                fs.Position = 5000;
                fs.WriteByte(0x42);
            }
            var source = new BuiltInRegionSource(packaged, install, true, new LocalFileFetcher());
            Assert.IsFalse(source.RequestAsync(SampleRegion.Id).Result);
            StringAssert.Contains("SHA-256", source.LastError);
            Assert.IsFalse(File.Exists(RegionFiles.FilePath(install, SampleRegion.Id, RegionFileKind.Manifest)), "no manifest: not installed");
            Assert.IsFalse(File.Exists(RegionFiles.FilePath(install, SampleRegion.Id, RegionFileKind.Pack) + ".part"), "no partial file left");
            Assert.IsFalse(source.IsAvailableAsync(SampleRegion.Id).Result);
        }

        /// <summary>IProgress that reports on the calling thread (System.Progress posts asynchronously).</summary>
        private sealed class SyncProgress : IProgress<float>
        {
            private readonly Action<float> _report;

            public SyncProgress(Action<float> report)
            {
                _report = report;
            }

            public void Report(float value)
            {
                lock (this) _report(value);
            }
        }
    }
}
