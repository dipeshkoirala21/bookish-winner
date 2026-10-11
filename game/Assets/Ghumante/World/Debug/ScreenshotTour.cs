#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Platform.Regions;
using Ghumante.World.Life;
using UnityEngine;

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// What the screenshot tour needs from the game layer above World (the App's Explore session): a playable open
    /// world, the explorer to place in each shot, and gameplay frozen while the tour owns the camera. The editor's
    /// <c>Ghumante &gt; Capture Screenshot Tour</c> implements it over <c>Bootstrap</c> and <c>ExploreSession</c>.
    /// </summary>
    public interface IScreenshotTourHost
    {
        /// <summary>Called every frame until it returns true: bring up a world the explorer plays in (open Explore when
        /// the menu is showing) and return it once the ride has started.</summary>
        bool TryGetWorld(out WorldRoot world);

        /// <summary>The tour takes the main camera and the explorer from here on: stop gameplay from driving them.</summary>
        void BeginTour(WorldRoot world);

        /// <summary>Stand the explorer at (<paramref name="x"/>, <paramref name="z"/>) (game metres) on
        /// <paramref name="groundY"/>, facing <paramref name="headingRad"/> (clockwise from north). The world was just
        /// teleported there (the floating origin moved).</summary>
        void PlacePlayer(WorldRoot world, double x, double z, float headingRad, float groundY);

        /// <summary>Give gameplay back (the explorer where it was before the tour).</summary>
        void EndTour(WorldRoot world);
    }

    /// <summary>Settings of one tour run.</summary>
    public sealed class ScreenshotTourOptions
    {
        /// <summary>Where the JPEGs and README.md go (default <see cref="ScreenshotTourCapture.DefaultFolder"/>).</summary>
        public string OutputFolder;

        /// <summary>The places to shoot, in order.</summary>
        public IReadOnlyList<ScreenshotAnchor> Anchors = ScreenshotTourAnchors.All;

        /// <summary>Render at 1, 2 or 4 times the output size and filter down (crisper edges and foliage).</summary>
        public int SuperSample = 2;

        public int JpegQuality = ScreenshotTourCapture.DefaultJpegQuality;

        /// <summary>Write <c>README.md</c> listing the shots when done.</summary>
        public bool WriteReadme = true;

        /// <summary>Give up waiting for the world to open after this long.</summary>
        public float OpenTimeoutS = 180f;

        /// <summary>Streaming may take this long per place before the shot is taken anyway (with a warning).</summary>
        public float SettleTimeoutS = 75f;

        /// <summary>At least this long at a place before the traffic, people and animals count as spawned.</summary>
        public float LifeMinS = 5f;

        /// <summary>The life counts must not grow for this long ...</summary>
        public float LifeStableS = 2f;

        /// <summary>... or this much time passes.</summary>
        public float LifeMaxS = 20f;

        /// <summary>Frames rendered into the target before reading it (LOD, culling and bands catch up with the
        /// orientation's frustum).</summary>
        public int CaptureWarmFrames = 4;
    }

    /// <summary>
    /// The screenshot tour (W2 detail pass; docs/images/README.md): for each <see cref="ScreenshotAnchor"/> it streams the
    /// world around the place (teleport plus <see cref="WorldRoot.FocusOverride"/>, which also turns the occluder fade
    /// off), confirms the place with the region's search, waits until streaming is settled and traffic, people and
    /// animals have spawned, sets the anchor's time of day (clock paused), places the explorer, frames the main camera
    /// (<see cref="ScreenshotFraming"/>) and renders it into a target of the exact output size, landscape 1920 x 1080
    /// then portrait 1080 x 1920, so no UI is in the picture. JPEGs go to the output folder as
    /// <c>&lt;anchor&gt;_&lt;orientation&gt;.jpg</c>, with a README.md listing them. Places the open region does not cover
    /// (Patan and Bhaktapur need the full valley) are skipped and listed. Gameplay, camera, clock and focus are restored
    /// afterwards. Editor and development builds only; one tour at a time.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ScreenshotTour : MonoBehaviour
    {
        private IScreenshotTourHost _host;
        private ScreenshotTourOptions _options;
        private WorldRoot _world;
        private Camera _camera;
        private bool _begun, _restored;
        private string _status = "";
        private string _error;
        private int _shots;
        private readonly List<ScreenshotReadme.Entry> _results = new List<ScreenshotReadme.Entry>();

        // What the tour changes and gives back.
        private Vector3 _camPosition;
        private Quaternion _camRotation;
        private float _camFov;
        private RenderTexture _camTarget;
        private float _hours;
        private bool _paused;
        private bool _runInBackground;
        private RenderTexture _target;
        private GUIStyle _style;
        private float _beat;
        private bool _finished;

        /// <summary>A coroutine step that throws ends the coroutine silently: without a heartbeat for this long the tour
        /// gives up and restores gameplay.</summary>
        public const float StallS = 30f;

        /// <summary>The tour in progress, or null.</summary>
        public static ScreenshotTour Running { get; private set; }

        /// <summary>Raised once when the tour ends (finished, failed or cancelled), after everything was restored.</summary>
        public event Action<ScreenshotTour> Completed;

        /// <summary>What the tour is doing now (for progress displays).</summary>
        public string Status
        {
            get { return _status; }
        }

        /// <summary>Why the tour stopped early, or null.</summary>
        public string Error
        {
            get { return _error; }
        }

        /// <summary>JPEG files written so far.</summary>
        public int ShotsWritten
        {
            get { return _shots; }
        }

        /// <summary>Per-anchor outcome so far.</summary>
        public IReadOnlyList<ScreenshotReadme.Entry> Results
        {
            get { return _results; }
        }

        public ScreenshotTourOptions Options
        {
            get { return _options; }
        }

        /// <summary>Path of the README the tour wrote, or null.</summary>
        public string ReadmePath { get; private set; }

        /// <summary>
        /// Starts a tour on a new hidden object (it survives scene loads and destroys itself when done). Returns the
        /// running tour instead when one is already in progress.
        /// </summary>
        public static ScreenshotTour Begin(IScreenshotTourHost host, ScreenshotTourOptions options)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (Running != null) return Running;
            var go = new GameObject("[Ghumante Screenshot Tour]");
            DontDestroyOnLoad(go);
            ScreenshotTour tour = go.AddComponent<ScreenshotTour>();
            tour._host = host;
            tour._options = options ?? new ScreenshotTourOptions();
            if (string.IsNullOrEmpty(tour._options.OutputFolder)) tour._options.OutputFolder = ScreenshotTourCapture.DefaultFolder();
            Running = tour;
            tour.StartCoroutine(tour.Run());
            return tour;
        }

        /// <summary>Stops the tour now and restores gameplay (files already written stay).</summary>
        public void Cancel()
        {
            if (_error == null) _error = "cancelled";
            Finish();
        }

        private void Update()
        {
            if (!_finished && _beat > 0f && Time.realtimeSinceStartup - _beat > StallS) Fail("stalled (see the errors above).");
        }

        private void OnDestroy()
        {
            Restore();
            if (Running == this) Running = null;
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
                _style.normal.textColor = Color.white;
            }
            var r = new Rect(16f, 16f, Mathf.Min(900f, Screen.width - 32f), 64f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f), _status, _style);
        }

        private IEnumerator Run()
        {
            _beat = Time.realtimeSinceStartup;
            _status = "Screenshot tour: opening Explore...";
            float start = Time.realtimeSinceStartup;
            WorldRoot world;
            while (!_host.TryGetWorld(out world))
            {
                _beat = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - start > _options.OpenTimeoutS)
                {
                    Fail("Explore did not open within " + _options.OpenTimeoutS + " s (is a region installed? Ghumante > Project Setup).");
                    yield break;
                }
                yield return null;
            }
            _world = world;
            _camera = world.ViewCamera;
            if (_camera == null)
            {
                Fail("no camera draws the world.");
                yield break;
            }
            FreeFlyCamera.CloseOverlay(world);
            _camera = world.ViewCamera;
            SaveState();
            _begun = true;
            _host.BeginTour(world);
            Debug.Log("ScreenshotTour: " + _options.Anchors.Count + " places in region " + world.RegionId + " -> " + _options.OutputFolder);

            for (int i = 0; i < _options.Anchors.Count; i++)
            {
                if (_world == null || !_world.IsOpen)
                {
                    Fail("the world closed during the tour.");
                    yield break;
                }
                ScreenshotAnchor a = _options.Anchors[i];
                _status = "Screenshot tour " + (i + 1) + "/" + _options.Anchors.Count + ": " + a.Title;
                var entry = new ScreenshotReadme.Entry { Anchor = a };
                string skip = SkipReason(a);
                if (skip != null)
                {
                    entry.Skipped = skip;
                    _results.Add(entry);
                    Debug.Log("ScreenshotTour: skipped " + a.Id + ": " + skip);
                    continue;
                }
                IEnumerator shoot = Shoot(a, i);
                while (shoot.MoveNext()) yield return shoot.Current;
                _results.Add(_last);
            }
            if (_options.WriteReadme) WriteReadme();
            _status = "Screenshot tour done: " + _shots + " images in " + _options.OutputFolder;
            Debug.Log("ScreenshotTour: " + _shots + " images in " + _options.OutputFolder);
            Finish();
        }

        private ScreenshotReadme.Entry _last;

        /// <summary>Why an anchor cannot be shot in the open region (null: it can).</summary>
        private string SkipReason(ScreenshotAnchor a)
        {
            RegionManifest m = _world.Manifest;
            if (m == null || !m.HasBounds) return null;
            if (ScreenshotTourAnchors.Covers(m.MinX, m.MinZ, m.MaxX, m.MaxZ, m.LeafLevel, a.X, a.Z)) return null;
            string needs = string.IsNullOrEmpty(a.Region) ? "a region that covers it" : "the " + a.Region + " region (Ghumante > Import Region Pack...)";
            return "outside region " + _world.RegionId + "; needs " + needs;
        }

        private IEnumerator Shoot(ScreenshotAnchor a, int index)
        {
            _last = new ScreenshotReadme.Entry { Anchor = a };
            double distance;
            SearchEntry hit = ScreenshotTourAnchors.Confirm(_world.Search, a, out distance);
            if (!string.IsNullOrEmpty(a.Query))
            {
                Debug.Log(hit != null
                    ? "ScreenshotTour: " + a.Id + ": search \"" + a.Query + "\" found " + hit.Name.Display(false) + " " + Math.Round(distance) + " m away"
                    : "ScreenshotTour: " + a.Id + ": search \"" + a.Query + "\" found nothing near; using the anchor's coordinates");
            }

            // Stream around the place: rebase there now and hold the focus (also turns the occluder fade off).
            var focus = new WorldPos(a.X, _world.Focus.Y, a.Z);
            _world.Teleport(focus);
            _world.FocusOverride = focus;
            _world.TimeOfDayHours = a.Hours;
            if (_world.Sky != null) _world.Sky.Paused = true;

            double ex, ez, lx, lz, px, pz;
            float heading;
            ScreenshotFraming.EyeXZ(a, out ex, out ez);
            ScreenshotFraming.LookXZ(a, out lx, out lz);
            bool showPlayer = ScreenshotFraming.PlayerSpot(a, out px, out pz, out heading);

            // Streaming: the camera already looks from the shot's eye so the view-dependent bands load for it.
            float t0 = Time.realtimeSinceStartup;
            float ge, gl, gp;
            while (true)
            {
                bool ground = Ground(ex, ez, out ge) & Ground(lx, lz, out gl) & Ground(px, pz, out gp);
                Pose(a, ex, ez, lx, lz, ge, gl, gp);
                float waited = Time.realtimeSinceStartup - t0;
                if (ground && _world.IsSettled && waited > 1.5f) break;
                if (waited > _options.SettleTimeoutS)
                {
                    Debug.LogWarning("ScreenshotTour: " + a.Id + ": streaming not settled after " + _options.SettleTimeoutS + " s; shooting anyway.");
                    break;
                }
                yield return null;
            }

            // The explorer: in front of the camera (Fixed) or on its street (Chase); out of sight otherwise.
            _host.PlacePlayer(_world, px, pz, heading, gp);
            if (!showPlayer) Debug.Log("ScreenshotTour: " + a.Id + ": no explorer in this shot.");

            // Life: traffic, people and animals spawn around the focus; wait until the counts stop growing.
            float lifeStart = Time.realtimeSinceStartup, stableSince = lifeStart;
            int last = -1;
            while (true)
            {
                float now = Time.realtimeSinceStartup;
                LifeHost life = _world.Life;
                int count = life != null ? life.VehicleCount + life.PeopleCount + life.AnimalCount : 0;
                if (count > last) stableSince = now;
                last = Math.Max(last, count);
                Ground(ex, ez, out ge);
                Ground(lx, lz, out gl);
                Ground(px, pz, out gp);
                Pose(a, ex, ez, lx, lz, ge, gl, gp);
                bool lived = now - lifeStart >= _options.LifeMinS && now - stableSince >= _options.LifeStableS;
                if (lived && _world.IsSettled || now - lifeStart >= _options.LifeMaxS) break;
                yield return null;
            }
            Debug.Log("ScreenshotTour: " + a.Id + ": " + last + " vehicles, people and animals around; " + a.Hours.ToString("0.0") + " h.");

            for (int o = 0; o < 2; o++)
            {
                var orientation = (ShotOrientation)o;
                IEnumerator capture = Capture(a, orientation, ex, ez, lx, lz, ge, gl, gp);
                while (capture.MoveNext()) yield return capture.Current;
            }
        }

        private IEnumerator Capture(ScreenshotAnchor a, ShotOrientation o, double ex, double ez, double lx, double lz, float ge, float gl, float gp)
        {
            int w, h;
            ScreenshotFraming.Size(o, out w, out h);
            ReleaseTarget();
            _target = ScreenshotTourCapture.CreateTarget(w, h, _options.SuperSample);
            _camera.targetTexture = _target;
            _camera.aspect = (float)w / h;
            _camera.fieldOfView = ScreenshotFraming.VerticalFovDeg(a, o);
            float tilt = ScreenshotFraming.TiltDeg(a, o), yaw = ScreenshotFraming.YawDeg(a, o);
            for (int f = 0; f < Math.Max(1, _options.CaptureWarmFrames); f++)
            {
                Pose(a, ex, ez, lx, lz, ge, gl, gp, tilt, yaw);
                yield return null;
            }
            // The target holds the frame rendered after the last yield (rendering follows LateUpdate).
            Texture2D tex = ScreenshotTourCapture.ReadBack(_target, w, h);
            _camera.targetTexture = _camTarget;
            _camera.ResetAspect();
            ReleaseTarget();
            try
            {
                string path = ScreenshotTourCapture.WriteJpeg(tex, _options.OutputFolder, ScreenshotFraming.FileName(a, o), _options.JpegQuality);
                _shots++;
                if (o == ShotOrientation.Landscape) _last.Landscape = true;
                else _last.Portrait = true;
                Debug.Log("ScreenshotTour: wrote " + path);
            }
            catch (Exception e)
            {
                _last.Skipped = "could not write: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                Destroy(tex);
            }
        }

        /// <summary>Puts the camera on the anchor's eye looking at its look point (scene coordinates of this frame), turned
        /// clockwise by <paramref name="yawDeg"/> and pitched up by <paramref name="tiltDeg"/>.</summary>
        private void Pose(ScreenshotAnchor a, double ex, double ez, double lx, double lz, float ge, float gl, float gp, float tiltDeg = 0f, float yawDeg = 0f)
        {
            float ey, ly;
            ScreenshotFraming.Heights(a, ge, gl, gp, out ey, out ly);
            _beat = Time.realtimeSinceStartup;
            Vector3 eye = _world.ToScene(ex, ey, ez), look = _world.ToScene(lx, ly, lz);
            Vector3 dir = look - eye;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            _camera.transform.SetPositionAndRotation(eye,
                Quaternion.Euler(0f, yawDeg, 0f) * Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-tiltDeg, 0f, 0f));
            if (_camera.targetTexture == null) _camera.fieldOfView = a.FovDeg;
        }

        /// <summary>Height of what is drawn at (x, z); false (and the focus height) while nothing is loaded there.</summary>
        private bool Ground(double x, double z, out float height)
        {
            GroundSample s;
            IGroundQuery g = _world.Ground;
            if (g != null && g.TrySample(x, z, out s))
            {
                height = s.Height;
                return true;
            }
            height = _world.Focus.Y;
            return false;
        }

        private void WriteReadme()
        {
            try
            {
                Directory.CreateDirectory(_options.OutputFolder);
                string path = Path.Combine(_options.OutputFolder, "README.md");
                File.WriteAllText(path, ScreenshotReadme.Build(_results, _world != null ? _world.RegionId : null, DateTime.UtcNow));
                ReadmePath = path;
                Debug.Log("ScreenshotTour: wrote " + path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void SaveState()
        {
            Transform t = _camera.transform;
            _camPosition = t.position;
            _camRotation = t.rotation;
            _camFov = _camera.fieldOfView;
            _camTarget = _camera.targetTexture;
            _hours = _world.TimeOfDayHours;
            _paused = _world.Sky != null && _world.Sky.Paused;
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _world.OriginShifted += OnOriginShifted;
        }

        private void OnOriginShifted(WorldPos delta)
        {
            _camPosition -= new Vector3((float)delta.X, delta.Y, (float)delta.Z);
        }

        /// <summary>Gives back the camera, clock, focus and gameplay (idempotent).</summary>
        private void Restore()
        {
            if (_restored || !_begun) return;
            _restored = true;
            ReleaseTarget();
            Application.runInBackground = _runInBackground;
            if (_world != null)
            {
                _world.OriginShifted -= OnOriginShifted;
                _world.FocusOverride = null;
                if (_world.IsOpen)
                {
                    _world.TimeOfDayHours = _hours;
                    if (_world.Sky != null) _world.Sky.Paused = _paused;
                }
            }
            if (_camera != null)
            {
                _camera.targetTexture = _camTarget;
                _camera.ResetAspect();
                _camera.fieldOfView = _camFov;
                _camera.transform.SetPositionAndRotation(_camPosition, _camRotation);
            }
            try
            {
                if (_world != null && _world.IsOpen) _host.EndTour(_world);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ReleaseTarget()
        {
            if (_target == null) return;
            if (_camera != null && _camera.targetTexture == _target) _camera.targetTexture = _camTarget;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        private void Fail(string message)
        {
            _error = message;
            _status = "Screenshot tour stopped: " + message;
            Debug.LogError("ScreenshotTour: " + message);
            Finish();
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            StopAllCoroutines();
            Restore();
            if (Running == this) Running = null;
            Action<ScreenshotTour> handler = Completed;
            Completed = null;
            if (handler != null) handler(this);
            Destroy(gameObject, 2f);
        }
    }
}
#endif
