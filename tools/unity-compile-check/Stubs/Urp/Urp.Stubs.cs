// Declaration stubs for the part of URP 17.3 (com.unity.render-pipelines.universal, Unity 6.3) that
// game/Assets/Ghumante uses. Same rules as Stubs/UnityEditor: exact signatures, used members only,
// verified against the Unity Graphics repository (branch 6000.3/staging) by the API audit.
#pragma warning disable 1591
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public enum RenderingMode
    {
        Forward = 0,
        Deferred = 1,
        ForwardPlus = 2,
        DeferredPlus = 3,
    }

    public class PostProcessData : ScriptableObject
    {
    }

    public abstract class ScriptableRendererData : ScriptableObject
    {
    }

    public class UniversalRendererData : ScriptableRendererData
    {
        public PostProcessData postProcessData;
        public RenderingMode renderingMode { get { throw null; } set { } }
    }

    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) { throw null; }
        public float renderScale { get { throw null; } set { } }
        public int msaaSampleCount { get { throw null; } set { } }
        public bool supportsHDR { get { throw null; } set { } }
        public float shadowDistance { get { throw null; } set { } }
        public int shadowCascadeCount { get { throw null; } set { } }
        public int mainLightShadowmapResolution { get { throw null; } set { } }
        public int maxAdditionalLightsCount { get { throw null; } set { } }
        public bool supportsCameraDepthTexture { get { throw null; } set { } }
        public bool supportsCameraOpaqueTexture { get { throw null; } set { } }
        public bool useSRPBatcher { get { throw null; } set { } }
        public bool useAdaptivePerformance { get { throw null; } set { } }

        // Unity 6.3 declares this protected; the 2021.3 reference assembly we compile against exposes the
        // base member as public, so the override must match that. Our code never calls it.
        public override RenderPipeline CreatePipeline() { throw null; }
    }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
    }

    public class UniversalAdditionalLightData : MonoBehaviour
    {
    }
}
