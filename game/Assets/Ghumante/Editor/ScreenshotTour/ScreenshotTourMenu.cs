using System.IO;
using Ghumante.App;
using Ghumante.App.Explore;
using Ghumante.Characters;
using Ghumante.Core.Geo;
using Ghumante.World;
using Ghumante.World.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// <b>Ghumante &gt; Capture Screenshot Tour</b> (docs/images/README.md): one click from edit mode opens the Bootstrap
    /// scene, enters Play mode, opens Explore and runs the <see cref="ScreenshotTour"/> over every
    /// <see cref="ScreenshotTourAnchors"/> place, writing landscape and portrait JPEGs plus a README.md to
    /// <c>&lt;repo&gt;/docs/images/game</c>; it reveals the folder when done. In Play mode it starts the tour at once.
    /// The pending request survives the domain reload of entering Play mode in <see cref="SessionState"/>. Editor only;
    /// release builds never contain it (and the runtime part is compiled for the editor and development builds only).
    /// </summary>
    public static class ScreenshotTourMenu
    {
        public const string MenuPath = "Ghumante/Capture Screenshot Tour";

        /// <summary>SessionState key: start the tour once Play mode has been entered.</summary>
        public const string PendingKey = "Ghumante.ScreenshotTour.Pending";

        [MenuItem(MenuPath, priority = 30)]
        public static void Capture()
        {
            if (EditorApplication.isPlaying)
            {
                StartTour();
                return;
            }
            if (!File.Exists(ProjectSetup.BootstrapScenePath))
            {
                EditorUtility.DisplayDialog("Capture Screenshot Tour",
                    "There is no Bootstrap scene yet. Run Ghumante > Project Setup first, then try again.", "OK");
                return;
            }
            if (SceneManager.GetActiveScene().path != ProjectSetup.BootstrapScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ProjectSetup.BootstrapScenePath);
            }
            SessionState.SetBool(PendingKey, true);
            Debug.Log("ScreenshotTour: entering Play mode; the tour starts once Explore is open (about two minutes for every place).");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>The folder the tour writes to: <c>&lt;repo&gt;/docs/images/game</c>.</summary>
        public static string OutputFolder
        {
            get { return ScreenshotTourCapture.DefaultFolder(); }
        }

        /// <summary>Starts the tour in Play mode (or returns the one running).</summary>
        public static ScreenshotTour StartTour()
        {
            if (ScreenshotTour.Running != null)
            {
                Debug.Log("ScreenshotTour: already running.");
                return ScreenshotTour.Running;
            }
            ScreenshotTour tour = ScreenshotTour.Begin(new ExploreTourHost(), new ScreenshotTourOptions { OutputFolder = OutputFolder });
            tour.Completed += OnCompleted;
            return tour;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PendingKey, false);
                return;
            }
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            StartTour();
        }

        private static void OnCompleted(ScreenshotTour tour)
        {
            if (tour.Error != null) Debug.LogWarning("ScreenshotTour: stopped (" + tour.Error + ") after " + tour.ShotsWritten + " images.");
            else Debug.Log("ScreenshotTour: " + tour.ShotsWritten + " images in " + tour.Options.OutputFolder + ". Commit them with: git add docs/images/game");
            if (tour.ShotsWritten > 0) EditorUtility.RevealInFinder(tour.ReadmePath ?? tour.Options.OutputFolder);
        }
    }

    /// <summary>
    /// The tour's view of the game (<see cref="IScreenshotTourHost"/>): opens Explore from the menu through
    /// <see cref="Bootstrap"/>, freezes the <see cref="ExploreSession"/> (its camera rig and the explorer) while the tour
    /// owns the camera, stands the explorer in each shot and puts it back where it was afterwards.
    /// </summary>
    internal sealed class ExploreTourHost : IScreenshotTourHost
    {
        /// <summary>Frames to wait after Play mode starts before asking for Explore, so Bootstrap's own first screen
        /// (Start) has been shown and does not replace it.</summary>
        public const int StartupFrames = 10;

        private int _firstFrame = -1;
        private bool _requested;
        private ExploreSession _session;
        private bool _saved;
        private WorldPos _position;
        private float _heading;

        public bool TryGetWorld(out WorldRoot world)
        {
            world = null;
            Bootstrap bootstrap = Bootstrap.Instance;
            if (bootstrap == null) return false;
            ExploreSession session = bootstrap.CurrentExplore;
            if (session == null)
            {
                if (_firstFrame < 0) _firstFrame = Time.frameCount;
                if (!_requested && Time.frameCount - _firstFrame >= StartupFrames)
                {
                    _requested = true;
                    bootstrap.Show(Bootstrap.StartScreen.Explore);
                }
                return false;
            }
            if (!session.Playing || session.World == null || !session.World.IsOpen || session.Explorer == null) return false;
            _session = session;
            world = session.World;
            return true;
        }

        public void BeginTour(WorldRoot world)
        {
            if (_session == null) return;
            ExplorerController explorer = _session.Explorer;
            if (explorer != null)
            {
                _position = explorer.Position;
                _heading = explorer.Pose.HeadingRad;
                _saved = true;
            }
            // The session drives the camera rig, the explorer and the streaming focus every frame: the tour owns them now.
            _session.enabled = false;
        }

        public void PlacePlayer(WorldRoot world, double x, double z, float headingRad, float groundY)
        {
            if (_session == null || _session.Explorer == null) return;
            _session.Explorer.Teleport(x, z, headingRad, groundY);
        }

        public void EndTour(WorldRoot world)
        {
            if (_session == null) return;
            if (_saved && _session.Explorer != null)
            {
                world.Teleport(_position);
                _session.Explorer.Teleport(_position.X, _position.Z, _heading, _position.Y);
            }
            _session.enabled = true;
            _session = null;
        }
    }
}
