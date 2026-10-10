using Ghumante.Core.Synth.Textures;
using Ghumante.Platform;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Rendering
{
    /// <summary>
    /// The runtime side of the cartoon look (World/README.md "Look"), self-starting and free of per-frame allocations:
    /// <list type="bullet">
    /// <item>bakes and binds the procedural material textures (<see cref="ToonTextureBank"/>) and the per-channel shading
    /// table (<see cref="MaterialLooks"/>) as globals of <c>Ghumante/ToonLit</c>;</item>
    /// <item>applies the device tier (<see cref="ToonLookTier"/>): triplanar quality, texture reach, glints, and the outline
    /// (the shader's maximum LOD selects the SubShader with or without the outline pass);</item>
    /// <item>sets the occluder-fade capsule from the world camera to the player before the world camera renders: the
    /// player end is <see cref="SetOccluderTarget"/> when a controller set it this frame, else the world's focus
    /// (<see cref="WorldRoot.Focus"/>, the explorer's position) raised by the target height. Off while the debug free-fly
    /// camera drives the world and for every other camera.</item>
    /// </list>
    /// It hooks <see cref="RenderPipelineManager.beginCameraRendering"/>, so it needs no scene object and no wiring.
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

        private static bool _initialized;
        private static ToonTextureBank _bank;
        private static int _appliedTier = -1;
        private static int _tierOverride = -1;
        private static ToonLookTier _tier;
        private static OccluderFadeSettings _occluder = OccluderFadeSettings.Default;
        private static bool _occluderEnabled = true;
        private static bool _outlinesEnabled = true;
        private static Vector3 _target;
        private static int _targetFrame = -1;
        private static readonly Vector4[] ChannelA = new Vector4[MaterialLooks.ArraySize];
        private static readonly Vector4[] ChannelB = new Vector4[MaterialLooks.ArraySize];

        /// <summary>The tier settings in use.</summary>
        public static ToonLookTier Tier
        {
            get { return _tier; }
        }

        /// <summary>The texture bank (null before the first camera renders).</summary>
        public static ToonTextureBank Bank
        {
            get { return _bank; }
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
        }

        /// <summary>Unhooks and frees the textures (editor domain reloads, tests).</summary>
        public static void Shutdown()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (_bank != null) _bank.Dispose();
            _bank = null;
            _initialized = false;
            _appliedTier = -1;
        }

        /// <summary>The tier the look follows: the override, else the active quality level (Low, Mid, High).</summary>
        public static DeviceTier CurrentTier()
        {
            if (_tierOverride >= 0) return (DeviceTier)_tierOverride;
            int level = QualitySettings.GetQualityLevel();
            return (DeviceTier)Mathf.Clamp(level, (int)DeviceTier.Low, (int)DeviceTier.High);
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            DeviceTier tier = CurrentTier();
            if ((int)tier != _appliedTier) ApplyTier(tier);
            if (_bank == null)
            {
                _bank = new ToonTextureBank(_tier.TextureSize);
                _bank.Start();
            }
            else if (!_bank.IsUploaded)
            {
                _bank.TryUpload(_tier.AnisoLevel);
            }
            SetOccluder(camera);
        }

        /// <summary>Applies a tier's look settings (shader LOD, globals) now.</summary>
        public static void ApplyTier(DeviceTier tier)
        {
            _tier = ToonLookTier.For(tier);
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
            OccluderFadeSettings s = _occluder;
            Shader.SetGlobalVector(OccluderAId, new Vector4(eye.x, eye.y, eye.z, s.CameraRadiusM));
            Shader.SetGlobalVector(OccluderBId, new Vector4(target.x, target.y, target.z, 1f));
            Shader.SetGlobalVector(OccluderCId, new Vector4(s.SoftM, s.SolidBeforePlayerM, s.MinOpacity, s.PlayerRadiusM));
        }
    }
}
