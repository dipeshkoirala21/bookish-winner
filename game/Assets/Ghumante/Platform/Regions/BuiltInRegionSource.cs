using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Core.Services;

namespace Ghumante.Platform.Regions
{
    /// <summary>
    /// <see cref="IRegionPackSource"/> for the regions shipped inside the app (ARCHITECTURE.md section 9,
    /// "BuiltInSource"): <c>StreamingAssets/Regions/&lt;id&gt;/</c>, filled by Project Setup (the sample region) and
    /// <b>Ghumante &gt; Import Region Pack</b>.
    /// <list type="bullet">
    /// <item>Where StreamingAssets is a folder (editor, iOS, desktop) files are opened in place, and
    /// <see cref="RequestAsync"/> only checks them.</item>
    /// <item>Where it is a URL (Android: inside the compressed APK, not seekable) <see cref="RequestAsync"/> copies the
    /// region once to <c>persistentDataPath/Regions/&lt;id&gt;/</c>, verifying every file's size and SHA-256 against the
    /// manifest; the manifest is written last, so its presence marks a complete install, and an interrupted copy
    /// resumes with the files already verified. An app update that ships a different manifest triggers a fresh copy.
    /// <see cref="IsAvailableAsync"/> is true once the installed manifest equals the packaged one and the files have
    /// the listed sizes.</item>
    /// </list>
    /// Use it from the main thread (the Android fetcher is UnityWebRequest). Progress may be reported from a worker
    /// thread: pass a <see cref="Progress{T}"/> to get it on the main thread. <see cref="CreateDefault"/> builds the
    /// app's instance; the constructor takes explicit roots and a fetcher, for tests.
    /// </summary>
    public sealed partial class BuiltInRegionSource : IRegionPackSource
    {
        private readonly string _packagedRoot;
        private readonly string _installRoot;
        private readonly bool _unpack;
        private readonly IRegionFileFetcher _fetcher;
        private readonly object _gate = new object();
        private readonly Dictionary<string, Task<bool>> _requests = new Dictionary<string, Task<bool>>(StringComparer.Ordinal);
        private readonly HashSet<string> _installed = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="packagedRoot">The root holding <c>Regions/</c> as shipped (StreamingAssets): a folder or a URL.</param>
        /// <param name="installRoot">Writable root for unpacked regions (persistent data); unused unless
        /// <paramref name="unpack"/>.</param>
        /// <param name="unpack">Copy regions to <paramref name="installRoot"/> before opening them.</param>
        /// <param name="fetcher">Reads packaged files (<see cref="LocalFileFetcher"/> for folders).</param>
        public BuiltInRegionSource(string packagedRoot, string installRoot, bool unpack, IRegionFileFetcher fetcher)
        {
            if (string.IsNullOrEmpty(packagedRoot)) throw new ArgumentException("packaged root required", nameof(packagedRoot));
            if (unpack && string.IsNullOrEmpty(installRoot)) throw new ArgumentException("install root required to unpack", nameof(installRoot));
            if (unpack && RegionFiles.IsUrl(installRoot)) throw new ArgumentException("install root must be a folder", nameof(installRoot));
            if (!unpack && RegionFiles.IsUrl(packagedRoot))
                throw new ArgumentException("a URL root (" + packagedRoot + ") cannot be opened in place; unpack it", nameof(unpack));
            _packagedRoot = packagedRoot;
            _installRoot = installRoot;
            _unpack = unpack;
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        }

        /// <summary>Where the shipped regions are (StreamingAssets).</summary>
        public string PackagedRoot
        {
            get { return _packagedRoot; }
        }

        /// <summary>Where regions are unpacked to (persistent data), when <see cref="Unpacks"/>.</summary>
        public string InstallRoot
        {
            get { return _installRoot; }
        }

        /// <summary>True when regions are copied to <see cref="InstallRoot"/> before use (Android).</summary>
        public bool Unpacks
        {
            get { return _unpack; }
        }

        /// <summary>The root files are opened from.</summary>
        public string ReadRoot
        {
            get { return _unpack ? _installRoot : _packagedRoot; }
        }

        /// <summary>Also hash every file in <see cref="IsAvailableAsync"/> (sizes are always checked; hashes after each
        /// copy). Off by default: hashing a 60 MB pack takes a noticeable fraction of a second on a phone.</summary>
        public bool VerifyHashesOnCheck { get; set; }

        /// <summary>Why the last failed <see cref="RequestAsync"/> or check failed, or null.</summary>
        public string LastError { get; private set; }

        public async Task<IReadOnlyList<string>> ListRegionsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var ids = new List<string>();
            if (!RegionFiles.IsUrl(_packagedRoot))
            {
                string folder = RegionFiles.RegionsFolder(_packagedRoot);
                if (Directory.Exists(folder))
                {
                    foreach (string dir in Directory.GetDirectories(folder))
                    {
                        string id = Path.GetFileName(dir);
                        if (RegionFiles.IsValidRegionId(id) && File.Exists(RegionFiles.FilePath(_packagedRoot, id, RegionFileKind.Manifest)))
                            ids.Add(id);
                    }
                }
            }
            else
            {
                try
                {
                    string index = await _fetcher.ReadTextAsync(RegionFiles.IndexPath(_packagedRoot), cancellationToken);
                    ids.AddRange(RegionFiles.ParseIndex(index));
                }
                catch (FileNotFoundException)
                {
                    LastError = "no " + RegionFiles.IndexFileName + " in " + RegionFiles.RegionsFolder(_packagedRoot);
                }
                catch (FormatException e)
                {
                    LastError = RegionFiles.IndexFileName + ": " + e.Message;
                }
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        public async Task<bool> IsAvailableAsync(string regionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!RegionFiles.IsValidRegionId(regionId)) return false;
            if (!_unpack) return await Task.Run(() => CheckFolder(_packagedRoot, regionId, null, VerifyHashesOnCheck, cancellationToken), cancellationToken);
            lock (_gate)
            {
                if (_installed.Contains(regionId)) return true;
            }
            string packaged;
            try
            {
                packaged = await _fetcher.ReadTextAsync(RegionFiles.FilePath(_packagedRoot, regionId, RegionFileKind.Manifest), cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            bool ok = await Task.Run(() => CheckFolder(_installRoot, regionId, packaged, VerifyHashesOnCheck, cancellationToken), cancellationToken);
            if (ok)
            {
                lock (_gate) _installed.Add(regionId);
            }
            return ok;
        }

        public async Task<bool> RequestAsync(string regionId, IProgress<float> progress = null,
                                             CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!RegionFiles.IsValidRegionId(regionId))
            {
                LastError = "invalid region id '" + regionId + "'";
                return false;
            }
            if (!_unpack)
            {
                bool ok = await IsAvailableAsync(regionId, cancellationToken);
                if (ok && progress != null) progress.Report(1f);
                return ok;
            }
            Task<bool> task;
            bool owner = false;
            lock (_gate)
            {
                if (!_requests.TryGetValue(regionId, out task))
                {
                    task = UnpackAsync(regionId, progress, cancellationToken);
                    _requests[regionId] = task;
                    owner = true;
                }
            }
            try
            {
                return await task;
            }
            finally
            {
                if (owner)
                {
                    lock (_gate) _requests.Remove(regionId);
                }
            }
        }

        public Task<Stream> OpenAsync(string regionId, RegionFileKind kind, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = RegionFiles.FilePath(ReadRoot, regionId, kind);
                if (!File.Exists(path))
                {
                    string hint = _unpack ? " (call RequestAsync first to unpack it)" : "";
                    return Task.FromException<Stream>(new FileNotFoundException("region file " + path + " not found" + hint, path));
                }
                // The pack is read with random access (one tile blob per read); the others are read once, start to end.
                Stream s = kind == RegionFileKind.Pack
                    ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess)
                    : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
                return Task.FromResult(s);
            }
            catch (Exception e)
            {
                return Task.FromException<Stream>(e);
            }
        }

        // ---------------------------------------------------------------------------------------------------------

        private async Task<bool> UnpackAsync(string regionId, IProgress<float> progress, CancellationToken cancellationToken)
        {
            try
            {
                string text = await _fetcher.ReadTextAsync(RegionFiles.FilePath(_packagedRoot, regionId, RegionFileKind.Manifest), cancellationToken);
                RegionManifest manifest = RegionManifest.Parse(text);
                if (manifest.RegionId != regionId)
                    throw new InvalidDataException("manifest of '" + regionId + "' describes region '" + manifest.RegionId + "'");

                string folder = RegionFiles.RegionFolder(_installRoot, regionId);
                string manifestPath = RegionFiles.FilePath(_installRoot, regionId, RegionFileKind.Manifest);
                bool installed = await Task.Run(() => CheckFolder(_installRoot, regionId, text, VerifyHashesOnCheck, cancellationToken), cancellationToken);
                if (!installed)
                {
                    Directory.CreateDirectory(folder);
                    if (File.Exists(manifestPath)) File.Delete(manifestPath); // written last: marks a complete install
                    long total = Math.Max(1L, manifest.TotalBytes), done = 0;
                    foreach (RegionFileEntry entry in manifest.Files)
                    {
                        string destination = RegionFiles.Join(folder, entry.Path);
                        // An interrupted install keeps the files it finished; keep any that still verify.
                        string problem = await Task.Run(() => Verify(destination, entry, cancellationToken), cancellationToken);
                        if (problem != null)
                        {
                            string part = destination + ".part";
                            string source = RegionFiles.Join(RegionFiles.RegionFolder(_packagedRoot, regionId), entry.Path);
                            var fileProgress = progress == null ? null : new ByteProgress(progress, done, total);
                            await _fetcher.CopyToFileAsync(source, part, fileProgress, cancellationToken);
                            problem = await Task.Run(() => Verify(part, entry, cancellationToken), cancellationToken);
                            if (problem != null)
                            {
                                TryDelete(part);
                                throw new InvalidDataException("copied " + problem);
                            }
                            if (File.Exists(destination)) File.Delete(destination);
                            File.Move(part, destination);
                        }
                        done += entry.Bytes;
                        if (progress != null) progress.Report((float)((double)done / total));
                    }
                    string tmp = manifestPath + ".part";
                    File.WriteAllText(tmp, text, new UTF8Encoding(false));
                    File.Move(tmp, manifestPath);
                }
                lock (_gate) _installed.Add(regionId);
                LastError = null;
                if (progress != null) progress.Report(1f);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                LastError = "region " + regionId + ": " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// True when <paramref name="root"/>/Regions/<paramref name="regionId"/> holds a readable manifest (equal to
        /// <paramref name="expectedManifest"/> when given) and every file it lists with the right size (and hash).
        /// </summary>
        private bool CheckFolder(string root, string regionId, string expectedManifest, bool hash, CancellationToken cancellationToken)
        {
            string manifestPath = RegionFiles.FilePath(root, regionId, RegionFileKind.Manifest);
            if (!File.Exists(manifestPath)) return false;
            try
            {
                string text = File.ReadAllText(manifestPath, Encoding.UTF8);
                if (expectedManifest != null && !string.Equals(text, expectedManifest, StringComparison.Ordinal)) return false;
                RegionManifest manifest = RegionManifest.Parse(text);
                if (manifest.RegionId != regionId) return false;
                string folder = RegionFiles.RegionFolder(root, regionId);
                foreach (RegionFileEntry entry in manifest.Files)
                {
                    string problem;
                    if (!RegionFileVerifier.Check(RegionFiles.Join(folder, entry.Path), entry, hash, out problem, cancellationToken))
                    {
                        LastError = "region " + regionId + ": " + problem;
                        return false;
                    }
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                LastError = "region " + regionId + ": " + e.Message;
                return false;
            }
        }

        private static string Verify(string path, RegionFileEntry entry, CancellationToken cancellationToken)
        {
            string problem;
            return RegionFileVerifier.Check(path, entry, true, out problem, cancellationToken) ? null : problem;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Maps the bytes of one file onto the whole region's [0, 1] progress.</summary>
        private sealed class ByteProgress : IProgress<long>
        {
            private readonly IProgress<float> _outer;
            private readonly long _before, _total;

            public ByteProgress(IProgress<float> outer, long before, long total)
            {
                _outer = outer;
                _before = before;
                _total = total;
            }

            public void Report(long value)
            {
                _outer.Report((float)Math.Min(1.0, (double)(_before + value) / _total));
            }
        }
    }
}
