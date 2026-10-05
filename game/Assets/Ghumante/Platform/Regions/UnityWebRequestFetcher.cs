using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Ghumante.Platform.Regions
{
    /// <summary>
    /// <see cref="IRegionFileFetcher"/> over UnityWebRequest, the only way to read Android's StreamingAssets (they live
    /// inside the APK). Files are streamed to disk by <see cref="DownloadHandlerFile"/>, so a large pack never sits in
    /// managed memory. Main thread only: the awaits resume through Unity's synchronization context.
    /// </summary>
    public sealed class UnityWebRequestFetcher : IRegionFileFetcher
    {
        public async Task<string> ReadTextAsync(string location, CancellationToken cancellationToken)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(location))
            {
                await SendAsync(request, null, cancellationToken);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new FileNotFoundException(location + ": " + request.error, location);
                return request.downloadHandler.text;
            }
        }

        public async Task CopyToFileAsync(string location, string destinationPath, IProgress<long> progress, CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(location, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath) { removeFileOnAbort = true };
                await SendAsync(request, progress, cancellationToken);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException("copying " + location + " failed: " + request.error);
                if (progress != null) progress.Report((long)request.downloadedBytes);
            }
        }

        private static async Task SendAsync(UnityWebRequest request, IProgress<long> progress, CancellationToken cancellationToken)
        {
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (progress != null) progress.Report((long)request.downloadedBytes);
                // On the main thread this resumes on the next player-loop tick (UnitySynchronizationContext).
                await Task.Yield();
            }
        }
    }
}
