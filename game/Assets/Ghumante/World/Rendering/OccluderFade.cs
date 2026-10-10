using System;

namespace Ghumante.World.Rendering
{
    /// <summary>
    /// The occluder fade (W2 detail pass, decision "houses never block the view"): every surface of a material with
    /// <c>_OCCLUDER_FADE</c> (buildings, temples, walls, trees, props, people) that lies inside a capsule from the camera to
    /// the player dissolves into a screen-door dither, so the player is always visible, even backing a motorbike down a
    /// narrow galli. The shader evaluates it per fragment from three global vectors set once per camera
    /// (<see cref="ToonLook"/>), so there is no per-object CPU work. This is the engine-free C# twin of
    /// <c>GhOccluderOpacity</c> in <c>Shaders/ToonLitInput.hlsl</c>, for tests and tools.
    /// </summary>
    public struct OccluderFadeSettings
    {
        /// <summary>Capsule radius at the camera end and at the player end (m).</summary>
        public float CameraRadiusM, PlayerRadiusM;

        /// <summary>Width of the soft edge at the side and at both ends (m).</summary>
        public float SoftM;

        /// <summary>The last metres before the player that stay solid (the ground and walls right at the player).</summary>
        public float SolidBeforePlayerM;

        /// <summary>Opacity left deep inside the capsule (0 = fully gone, 1 = no fade).</summary>
        public float MinOpacity;

        /// <summary>Height of the capsule's player end above the player's feet (m): the chest of someone walking, about
        /// the seat of a scooter.</summary>
        public float TargetHeightM;

        /// <summary>The defaults: 1 m at the camera widening to 2.2 m at the player (the player and a scooter's length
        /// around them), 0.9 m soft edges, the last 0.9 m solid, 18% left (enough to read the wall that was there).</summary>
        public static OccluderFadeSettings Default
        {
            get
            {
                return new OccluderFadeSettings
                {
                    CameraRadiusM = 1.0f, PlayerRadiusM = 2.2f, SoftM = 0.9f, SolidBeforePlayerM = 0.9f, MinOpacity = 0.18f,
                    TargetHeightM = 1.0f,
                };
            }
        }
    }

    /// <summary>Engine-free math of the occluder fade (see <see cref="OccluderFadeSettings"/>).</summary>
    public static class OccluderFade
    {
        /// <summary>
        /// Opacity (MinOpacity..1) of a world point against the capsule from <c>a</c> (camera) to <c>b</c> (player); the
        /// shader discards the fragment where the opacity is below its 4 × 4 Bayer threshold.
        /// </summary>
        public static float Opacity(float px, float py, float pz, float ax, float ay, float az, float bx, float by, float bz,
                                    in OccluderFadeSettings s)
        {
            float abx = bx - ax, aby = by - ay, abz = bz - az;
            float len2 = Math.Max(abx * abx + aby * aby + abz * abz, 1e-4f);
            float len = (float)Math.Sqrt(len2);
            float t = ((px - ax) * abx + (py - ay) * aby + (pz - az) * abz) / len2;
            float soft = Math.Max(s.SoftM, 1e-3f);
            float along = Saturate(t * len / soft) * Saturate((len - s.SolidBeforePlayerM - t * len) / soft);
            float tc = Saturate(t);
            float radius = s.CameraRadiusM + (s.PlayerRadiusM - s.CameraRadiusM) * tc;
            float dx = px - (ax + abx * tc), dy = py - (ay + aby * tc), dz = pz - (az + abz * tc);
            float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float inside = Saturate((radius - d) / soft);
            float fade = along * inside;
            return 1f - fade * (1f - s.MinOpacity);
        }

        private static float Saturate(float x)
        {
            return x < 0f ? 0f : x > 1f ? 1f : x;
        }
    }
}
