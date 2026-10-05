using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ghumante.Platform.Regions
{
    /// <summary>
    /// Reads packaged region files for <see cref="BuiltInRegionSource"/>: plain file IO for folders
    /// (<see cref="LocalFileFetcher"/>), UnityWebRequest for URLs such as Android's APK StreamingAssets
    /// (<c>UnityWebRequestFetcher</c>, main thread only).
    /// </summary>
    public interface IRegionFileFetcher
    {
        /// <summary>The UTF-8 text of a small file (manifest, index); fails with <see cref="FileNotFoundException"/>
        /// when it does not exist.</summary>
        Task<string> ReadTextAsync(string location, CancellationToken cancellationToken);

        /// <summary>Copies a file to <paramref name="destinationPath"/> (overwriting it), reporting the bytes copied so
        /// far, possibly from a worker thread.</summary>
        Task CopyToFileAsync(string location, string destinationPath, IProgress<long> progress, CancellationToken cancellationToken);
    }

    /// <summary>File-system <see cref="IRegionFileFetcher"/> (editor, iOS, desktop, tests). Copies run on a worker
    /// thread; engine-free.</summary>
    public sealed class LocalFileFetcher : IRegionFileFetcher
    {
        private const int BufferSize = 1 << 16;

        public Task<string> ReadTextAsync(string location, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(location)) return Task.FromException<string>(new FileNotFoundException(location + " not found", location));
                return Task.FromResult(File.ReadAllText(location, Encoding.UTF8));
            }
            catch (Exception e)
            {
                return Task.FromException<string>(e);
            }
        }

        public Task CopyToFileAsync(string location, string destinationPath, IProgress<long> progress, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(location)) throw new FileNotFoundException(location + " not found", location);
                using (var src = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan))
                using (var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    var buffer = new byte[BufferSize];
                    long copied = 0;
                    int n;
                    while ((n = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        dst.Write(buffer, 0, n);
                        copied += n;
                        if (progress != null) progress.Report(copied);
                    }
                }
            }, cancellationToken);
        }
    }
}
