using Ghumante.Core.Synth.Look;
using Ghumante.Core.Synth.Textures;
using Ghumante.Platform;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ghumante.World.Rendering
{
    /// <summary>
    /// The runtime side of the cartoon look (World/README.md "Look"), self-starting and free of per-frame allocations:
    /// <list type="bullet">
    /// <item>bakes and binds the procedural material textures (<see cref="ToonTextureBank"/>) and the per-channel shading
    /// table (<see cref="MaterialLooks"/>) as globals of <c>Ghumante/ToonLit</c>; a tier with another texture size re-bakes
    /// them in the background and swaps when the new array is uploaded;</item>
    /// <item>applies the device tier (<see cref="ToonLookTier"/>): triplanar quality, texture reach, glints, and the outline
    /// (the shader's maximum LOD selects the SubShader with or without the outline pass; the tier's look roles decide
    /// which world materials draw it, <see cref="WorldMaterialDefaults.RoleOf"/>, applied to the active world's materials
    /// whenever the tier or the world's material set changes);</item>
    /// <item>sets the occluder-fade capsule (<see cref="OccluderCapsule"/>) from the world camera to the player before the
    /// world camera renders: the player end is <see cref="SetOccluderTarget"/> when a controller set it this frame, else the
    /// world's focus (<see cref="WorldRoot.Focus"/>, the explorer's position) raised by the target height; the camera-end
    /// radius covers the camera's view frustum just in front of the lens. Off while the debug free-fly camera drives the
    /// world and for every other camera.</item>
    /// <item>applies the adaptive governor's dynamic resolution (<see cref="DynamicRenderScale"/>, App's quality director)
    /// to the active URP asset, which changes with the quality level: the asset renders at the lower of its authored render
    /// scale and the dynamic one, and gets its authored value back when another level takes over, on
    /// <see cref="Shutdown"/> and when the application quits (in the editor the change would otherwise be saved into the
    /// asset).</item>
    /// </list>
    /// It hooks <see cref="RenderPipelineManager.beginCameraRendering"/>, so it needs no scene object and no wiring.
    /// A tier picked in Settings (or stepped by the governor) reaches the look through the quality level and
    /// <see cref="OverrideTier"/>, which the world's <see cref="WorldRoot.Tier"/> also sets.
    /// </summary>
    public static class ToonLook
    {
        public static readonly int ChannelAId = Shader.PropertyToID("_GhChannelA");
        public static readonly int ChannelBId = Shader.PropertyToID("_GhChannelB");
        public static readonly int LookParamsId = Shader.PropertyToID("_GhLookParams");
        public static readonly int OutlineParamsId = Shader.PropertyToID("_GhOutlineParams");
        public static readonly int OccluderAId = Shader.PropertyToID("_GhOccluderA");
        public static readonly int OccluderBId = Shader.PropertyToID("_GhOccluderB");
        public static readonly int OccluderCId = Shader.PropertyToID("_GhOccluderC");
        public static readonly int OccluderDId = Shader.PropertyToID("_GhOccluderD");

        private static bool _initialized;
        private static ToonTextureBank _bank;
        private static ToonTextureBank _pendingBank;
        private static int _appliedTier = -1;
        private static int _tierOverride = -1;
        private static ToonLookTier _tier = ToonLookTier.For(DeviceTier.Mid);
        private static OccluderFadeSettings _occluder = OccluderFadeSettings.Default;
        private static bool _occluderEnabled = true;
        private static bool _outlinesEnabled = true;
        private static Vector3 _target;
        private static int _targetFrame = -1;
        private static WorldMaterialSet _rolesSet;
        private static int _rolesVersion = -1;
        private static int _tierVersion;
        private static float _dynamicScale = 1f;
        private static UniversalRenderPipelineAsset _scaledAsset;
        private static float _authoredScale = 1f;
        private static bool _quitHooked;
        private static readonly Vector4[] ChannelA = new Vector4[MaterialLooks.ArraySize];
        private static readonly Vector4[] ChannelB = new Vector4[MaterialLooks.ArraySize];

        /// <summary>The tier settings in use.</summary>
        public static ToonLookTier Tier
        {
            get { return _tier; }
        }

        /// <summary>The bound texture bank (null before the first camera renders).</summary>
        public static ToonTextureBank Bank
        {
            get { return _bank; }
        }

        /// <summary>A bank being baked at the current tier's texture size while the old one stays bound (null when none).</summary>
        public static ToonTextureBank PendingBank
        {
            get { return _pendingBank; }
        }

        /// <summary>Occluder fade on (default) or off (a settings toggle, cutscenes).</summary>
        public static bool OccluderFadeEnabled
        {
            get { return _occluderEnabled; }
            set { _occluderEnabled = value; }
        }

        /// <summary>Outlines on (default, where the tier draws them) or off (a settings toggle).</summary>
        public static bool OutlinesEnabled
        {
            get { return _outlinesEnabled; }
            set
            {
                if (_outlinesEnabled == value) return;
                _outlinesEnabled = value;
                _appliedTier = -1;
            }
        }

        /// <summary>The capsule settings (tune at runtime; applied from the next camera).</summary>
        public static OccluderFadeSettings Occluder
        {
            get { return _occluder; }
            set { _occluder = value; }
        }

        /// <summary>
        /// Dynamic resolution from the adaptive quality governor, in [0.5, 1]: the active URP asset renders at the lower of
        /// its authored render scale and this (Low is authored at 0.7, so it has no range). Applied before the next camera
        /// renders. Main thread.
        /// </summary>
        public static float DynamicRenderScale
        {
            get { return _dynamicScale; }
            set { _dynamicScale = Mathf.Clamp(float.IsNaN(value) ? 1f : value, 0.5f, 1f); }
        }

        /// <summary>The render scale the active URP asset was authored with (1 before a camera rendered).</summary>
        public static float AuthoredRenderScale
        {
            get { return _authoredScale; }
        }

        /// <summary>The render scale in use (after the last camera's <see cref="Refresh"/>).</summary>
        public static float AppliedRenderScale { get; private set; } = 1f;

        /// <summary>The authored render scale of a quality level's URP asset (1 when it has none or is not URP), read before
        /// any dynamic change: the governor's maximum for that tier.</summary>
        public static float AuthoredRenderScaleOf(int qualityLevel)
        {
            if (qualityLevel < 0 || qualityLevel >= QualitySettings.names.Length) return 1f;
            var asset = QualitySettings.GetRenderPipelineAssetAt(qualityLevel) as UniversalRenderPipelineAsset;
            if (asset == null) asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return 1f;
            return asset == _scaledAsset ? _authoredScale : asset.renderScale;
        }

        /// <summary>Forces a tier (a settings menu); null follows the active quality level, as the world does.</summary>
        public static void OverrideTier(DeviceTier? tier)
        {
            _tierOverride = tier.HasValue ? (int)tier.Value : -1;
            _appliedTier = -1;
        }

        /// <summary>
        /// The player end of the occluder capsule for this frame, in scene coordinates (a camera rig or ride controller
        /// that knows a better point than the world focus, e.g. a bus's driver seat). Call every frame before rendering;
        /// without a call this frame the world focus is used.
        /// </summary>
        public static void SetOccluderTarget(Vector3 scenePosition)
        {
            _target = scenePosition;
            _targetFrame = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            EnsureInitialized();
        }

        /// <summary>Hooks the render callbacks and binds the defaults (idempotent). Editor tools call it for edit mode.</summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            var a = new float[MaterialLooks.ArraySize * 4];
            var b = new float[MaterialLooks.ArraySize * 4];
            MaterialLooks.FillShaderArrays(a, b);
            for (int i = 0; i < MaterialLooks.ArraySize; i++)
            {
                ChannelA[i] = new Vector4(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]);
                ChannelB[i] = new Vector4(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
            }
            Shader.SetGlobalVectorArray(ChannelAId, ChannelA);
            Shader.SetGlobalVectorArray(ChannelBId, ChannelB);
            Shader.SetGlobalVector(OccluderBId, Vector4.zero);
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            if (!_quitHooked)
            {
                _quitHooked = true;
                Application.quitting += RestoreRenderScale;
            }
        }

        /// <summary>Unhooks, frees the textures and gives the URP asset its authored render scale back (editor domain
        /// reloads, tests).</summary>
        public static void Shutdown()
        {
            RestoreRenderScale();
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (_pendingBank != null) _pendingBank.Dispose();
            if (_bank != null) _bank.Dispose();
            _pendingBank = null;
            _bank = null;
            _initialized = false;
            _appliedTier = -1;
            _rolesSet = null;
            _rolesVersion = -1;
        }

        /// <summary>The tier the look follows: the override, else the active quality level (Low, Mid, High).</summary>
        public static DeviceTier CurrentTier()
        {
            if (_tierOverride >= 0) return (DeviceTier)_tierOverride;
            int level = QualitySettings.GetQualityLevel();
            return (DeviceTier)Mathf.Clamp(level, (int)DeviceTier.Low, (int)DeviceTier.High);
        }

        /// <summary>
        /// What every camera does before it renders, minus the occluder capsule: follows a tier change, applies the tier's
        /// look roles to the active world's materials, and drives the texture bank (start, upload, re-bake at a new size).
        /// Editor tools and tests call it directly. Main thread.
        /// </summary>
        public static void Refresh()
        {
            DeviceTier tier = CurrentTier();
            if ((int)tier != _appliedTier) ApplyTier(tier);
            ApplyWorldRoles();
            UpdateBank();
            ApplyRenderScale();
        }

        // Dynamic resolution on the active URP asset (it changes with the quality level): remember its authored scale the
        // first time, give the previous asset its own back, and write only when the value changes (no per-frame work).
        private static void ApplyRenderScale()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != _scaledAsset)
            {
                RestoreRenderScale();
                _scaledAsset = asset;
                _authoredScale = asset != null ? asset.renderScale : 1f;
            }
            if (asset == null)
            {
                AppliedRenderScale = 1f;
                return;
            }
            float want = Mathf.Min(_authoredScale, _dynamicScale);
            if (Mathf.Abs(asset.renderScale - want) > 0.001f) asset.renderScale = want;
            AppliedRenderScale = want;
        }

        private static void RestoreRenderScale()
        {
            if (_scaledAsset != null && Mathf.Abs(_scaledAsset.renderScale - _authoredScale) > 0.0001f) _scaledAsset.renderScale = _authoredScale;
            _scaledAsset = null;
            _authoredScale = 1f;
            AppliedRenderScale = 1f;
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Refresh();
            SetOccluder(camera);
        }

        /// <summary>Applies a tier's look settings (shader LOD, globals) now; a different texture size re-bakes the
        /// textures in the background (<see cref="Refresh"/> swaps them when ready).</summary>
        public static void ApplyTier(DeviceTier tier)
        {
            _tier = ToonLookTier.For(tier);
            _tierVersion++;
            bool outlines = _outlinesEnabled && _tier.Outlines;
            Shader toon = Shader.Find(WorldShaders.ToonLit);
            // Until a ToonLit material has loaded the shader (the menu), try again next camera.
            _appliedTier = toon != null ? (int)tier : -1;
            if (toon != null) toon.maximumLOD = outlines ? ToonLitLayout.LodWithOutline : ToonLitLayout.LodWithoutOutline;
            Shader.SetGlobalVector(LookParamsId, new Vector4(_tier.Triplanar ? 1f : 0f, _tier.Macro, _tier.Glints ? 1f : 0f, _tier.DetailFadeM));
            Shader.SetGlobalVector(OutlineParamsId,
                                   new Vector4(outlines ? _tier.OutlineWidthPx : 0f, _tier.OutlineFadeStartM, _tier.OutlineFadeEndM, _tier.OutlineDarkness));
            if (_bank != null) _bank.SetAniso(_tier.AnisoLevel);
        }

        // The tier's outline roles on the materials the active world draws with (the streamer's band clones are made from
        // them when the world opens, after WorldMaterialSet.Load applied the same roles).
        private static void ApplyWorldRoles()
        {
            WorldRoot world = WorldRoot.Active;
            WorldMaterialSet set = world != null ? world.Materials : null;
            if (set == null || (set == _rolesSet && _rolesVersion == _tierVersion)) return;
            WorldMaterialDefaults.ApplyLook(set, false, _tier);
            _rolesSet = set;
            _rolesVersion = _tierVersion;
        }

        // Starts the first bake, uploads it, and re-bakes when the tier asks for another texture size: the old array stays
        // bound until the new one is uploaded (a bank that never uploaded is simply replaced).
        private static void UpdateBank()
        {
            int size = _tier.TextureSize;
            if (_bank == null || (!_bank.IsUploaded && _bank.Size != size))
            {
                if (_bank != null) _bank.Dispose();
                _bank = new ToonTextureBank(size);
                _bank.Start(true);
            }
            if (!_bank.IsUploaded) _bank.TryUpload(_tier.AnisoLevel);
            if (_pendingBank != null && _pendingBank.Size != size)
            {
                _pendingBank.Dispose();
                _pendingBank = null;
            }
            if (_bank.IsUploaded && _bank.Size != size && _pendingBank == null)
            {
                _pendingBank = new ToonTextureBank(size);
                _pendingBank.Start(false);
            }
            if (_pendingBank != null && _pendingBank.TryUpload(_tier.AnisoLevel))
            {
                _bank.Dispose();
                _bank = _pendingBank;
                _pendingBank = null;
            }
        }

        private static void SetOccluder(Camera camera)
        {
            WorldRoot world = WorldRoot.Active;
            bool on = _occluderEnabled && camera != null && camera.cameraType == CameraType.Game && world != null && world.IsOpen &&
                      world.FocusOverride == null && camera == world.ViewCamera;
            if (!on)
            {
                Shader.SetGlobalVector(OccluderBId, Vector4.zero);
                return;
            }
            Vector3 target = _targetFrame == Time.frameCount
                ? _target
                : world.ToScene(world.Focus) + new Vector3(0f, _occluder.TargetHeightM, 0f);
            Vector3 eye = camera.transform.position;
            float tanHalfFov = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
            OccluderCapsule c = OccluderCapsule.From(_occluder, eye.x, eye.y, eye.z, target.x, target.y, target.z, tanHalfFov, camera.aspect);
            Shader.SetGlobalVector(OccluderAId, new Vector4(c.Ax, c.Ay, c.Az, c.CameraRadius));
            Shader.SetGlobalVector(OccluderBId, new Vector4(c.Bx, c.By, c.Bz, 1f));
            Shader.SetGlobalVector(OccluderCId, new Vector4(c.Soft, c.SolidRadius, c.MinOpacity, c.PlayerRadius));
            Shader.SetGlobalVector(OccluderDId, new Vector4(c.KeepY, c.OverheadY, 0f, 0f));
        }
    }
}
