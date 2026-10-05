using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Before every player build: make sure the sample region is in StreamingAssets and that
    /// <c>Regions/regions.json</c> lists every installed region (Android reads the list; it cannot enumerate the APK).
    /// No asset-database refresh here: StreamingAssets are copied into the build as raw files.
    /// </summary>
    public sealed class RegionImportBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RegionImport.EnsureSampleRegion(refresh: false);
            RegionImport.RefreshIndex();
            if (RegionImport.InstalledRegions().Count == 0)
                Debug.LogWarning("RegionImportBuildHook: no region in " + RegionImport.RegionsAssetFolder + "; Explore will have nothing to load.");
        }
    }
}
