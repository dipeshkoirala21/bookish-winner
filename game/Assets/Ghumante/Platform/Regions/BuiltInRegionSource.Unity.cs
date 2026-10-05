using UnityEngine;

namespace Ghumante.Platform.Regions
{
    // The Unity half of BuiltInRegionSource: the app's default roots and fetcher. The rest of the class is engine-free.
    public sealed partial class BuiltInRegionSource
    {
        /// <summary>
        /// The app's source: <c>Application.streamingAssetsPath</c>, unpacked to <c>Application.persistentDataPath</c>
        /// where StreamingAssets is not a folder (Android). Call on the main thread (Unity paths are main-thread only).
        /// </summary>
        public static BuiltInRegionSource CreateDefault()
        {
            string packaged = Application.streamingAssetsPath;
            bool unpack = RegionFiles.IsUrl(packaged);
            IRegionFileFetcher fetcher = unpack ? (IRegionFileFetcher)new UnityWebRequestFetcher() : new LocalFileFetcher();
            return new BuiltInRegionSource(packaged, Application.persistentDataPath, unpack, fetcher);
        }
    }
}
