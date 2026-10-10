using Ghumante.World.Rendering;
using UnityEditor;

namespace Ghumante.World.EditorTools
{
    /// <summary>
    /// Edit-mode hook of the cartoon look: <see cref="ToonLook"/> starts on every editor domain load, so the Scene view
    /// shows the procedural textures and the tier's look exactly like Play mode (the bake runs on worker threads; the
    /// texture array left over from the previous domain is destroyed first, see <see cref="ToonTextureBank"/>).
    /// </summary>
    public static class ToonLookEditor
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            ToonLook.EnsureInitialized();
        }

        /// <summary>Re-bakes the material textures and re-applies the tier (after a texture or look change).</summary>
        [MenuItem("Ghumante/Look/Refresh Material Textures", false, 40)]
        public static void Refresh()
        {
            ToonLook.Shutdown();
            ToonLook.EnsureInitialized();
        }
    }
}
