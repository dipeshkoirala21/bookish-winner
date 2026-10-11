#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// Development shortcut for single screenshots (docs/images/README.md), attached to every world when it becomes ready
    /// (editor and development builds only): <b>F12</b> renders the main camera as the player sees it, without any UI, at
    /// 1920 x 1080 (or 1080 x 1920 when the screen is portrait); <b>Shift+F12</b> captures the screen with the HUD (the
    /// debug overlays hidden). Files go to <c>&lt;output&gt;/shots/shot_&lt;time&gt;_&lt;orientation&gt;.jpg</c> under
    /// <see cref="ScreenshotTourCapture.DefaultFolder"/> (docs/images/game in the editor). Ignored while a text field has
    /// focus or a screenshot tour runs.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ScreenshotTourHotkey : MonoBehaviour
    {
        private WorldRoot _world;
        private bool _busy;
        private readonly List<VisualElement> _hidden = new List<VisualElement>();

        /// <summary>Where the last shot was written, or null.</summary>
        public string LastPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            WorldRoot.AnyReady -= Attach;
            WorldRoot.AnyReady += Attach;
        }

        private static void Attach(WorldRoot world)
        {
            if (world != null && world.GetComponent<ScreenshotTourHotkey>() == null) world.gameObject.AddComponent<ScreenshotTourHotkey>();
        }

        private void Awake()
        {
            _world = GetComponent<WorldRoot>();
        }

        private void Update()
        {
            if (_busy || _world == null || !_world.IsOpen || ScreenshotTour.Running != null) return;
#if GHUMANTE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.f12Key.wasPressedThisFrame || TextFieldFocused()) return;
            bool hud = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            StartCoroutine(hud ? ShootScreen() : ShootCamera());
#endif
        }

        /// <summary>The camera as the player sees it, rendered into a target of the output size (no UI).</summary>
        private IEnumerator ShootCamera()
        {
            Camera cam = _world.ViewCamera;
            if (cam == null) yield break;
            _busy = true;
            ShotOrientation o = ScreenshotTourCapture.ScreenOrientation();
            int w, h;
            ScreenshotFraming.Size(o, out w, out h);
            RenderTexture previous = cam.targetTexture;
            RenderTexture target = ScreenshotTourCapture.CreateTarget(w, h, 2);
            cam.targetTexture = target;
            cam.aspect = (float)w / h;
            yield return null;
            yield return null;
            Texture2D tex = null;
            try
            {
                tex = ScreenshotTourCapture.ReadBack(target, w, h);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (cam != null)
                {
                    cam.targetTexture = previous;
                    cam.ResetAspect();
                }
                target.Release();
                Destroy(target);
            }
            Write(tex, o, false);
            _busy = false;
        }

        /// <summary>The screen with the HUD, debug overlays hidden for the frame.</summary>
        private IEnumerator ShootScreen()
        {
            _busy = true;
            ScreenshotTourCapture.HideDebugUi(_hidden);
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                ScreenshotTourCapture.ShowDebugUi(_hidden);
            }
            Write(tex, ScreenshotTourCapture.ScreenOrientation(), true);
            _busy = false;
        }

        private void Write(Texture2D tex, ShotOrientation o, bool hud)
        {
            if (tex == null) return;
            try
            {
                string folder = System.IO.Path.Combine(ScreenshotTourCapture.DefaultFolder(), ScreenshotTourCapture.ExtraFolderName);
                LastPath = ScreenshotTourCapture.WriteJpeg(tex, folder, ScreenshotFraming.SingleShotName(DateTime.Now, o, hud),
                                                           ScreenshotTourCapture.DefaultJpegQuality);
                Debug.Log("Screenshot: " + LastPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Destroy(tex);
            }
        }

        /// <summary>True when a UI Toolkit text field has keyboard focus (typing in search must not take screenshots).</summary>
        private static bool TextFieldFocused()
        {
            UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                VisualElement root = docs[i].rootVisualElement;
                IPanel panel = root != null ? root.panel : null;
                if (panel == null || panel.focusController == null) continue;
                var focused = panel.focusController.focusedElement as VisualElement;
                if (focused != null && focused.GetFirstOfType<TextField>() != null) return true;
            }
            return false;
        }
    }
}
#endif
