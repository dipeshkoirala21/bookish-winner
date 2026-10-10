using System;

namespace Ghumante.Core.Synth.Look
{
    /// <summary>
    /// Tuning of the occluder fade (W2 detail pass, "houses block the view"): every surface of a material with
    /// <c>_OCCLUDER_FADE</c> (buildings, temples, walls, trees, props, people, and bridge parts on the road layer) that lies
    /// between the camera and the player dissolves into a screen-door dither, so the player is always visible, even backing
    /// a motorbike down a narrow galli with the camera squeezed against a house. Engine-free; World/Rendering's
    /// <c>ToonLook</c> turns it into an <see cref="OccluderCapsule"/> every frame and the shader evaluates it per fragment.
    /// </summary>
    public struct OccluderFadeSettings
    {
        /// <summary>Smallest fully faded radius at the camera end (m). Everything this close to the lens dissolves.</summary>
        public float CameraRadiusM;

        /// <summary>The camera-end radius also covers the whole view frustum this far in front of the lens (m), so a wall
        /// filling the screen just in front of the camera fades at the screen corners too (wide and landscape views).</summary>
        public float NearCoverM;

        /// <summary>Upper bound of the camera-end radius (m).</summary>
        public float MaxCameraRadiusM;

        /// <summary>Fully faded radius at the player end (m): the player and a scooter's length around them.</summary>
        public float PlayerRadiusM;

        /// <summary>Soft edge outside the fully faded core = this × the capsule length, clamped to
        /// [<see cref="SoftMinM"/>, <see cref="SoftMaxM"/>] (a short capsule gets a short edge).</summary>
        public float SoftFraction, SoftMinM, SoftMaxM;

        /// <summary>A sphere of this radius around the player end stays solid (the player's body: a person standing next
        /// to them, the doorway they stand in), soft over half the radius outside it; at most a quarter of the capsule.</summary>
        public float SolidRadiusM;

        /// <summary>Opacity left deep inside the capsule (0 = fully gone, 1 = no fade).</summary>
        public float MinOpacity;

        /// <summary>Height of the capsule's player end above the player's feet (m): the chest of someone walking, about
        /// the seat of a scooter.</summary>
        public float TargetHeightM;

        /// <summary>Ground layers (the road material, which also draws bridge railings, piers, decks and flyovers) fade
        /// only above the player's feet + this (m): road surfaces, kerbs and footpaths never dissolve.</summary>
        public float GroundKeepM;

        /// <summary>Upward-facing ground surfaces fade only this far above the player end (m): a flyover deck overhead, never
        /// the road ahead climbing a hill.</summary>
        public float OverheadM;

        /// <summary>The defaults: at least 1 m at the camera (more for wide views), 1.3 m at the player, soft edges of 12%
        /// of the capsule (0.15 to 0.6 m), a 0.5 m solid sphere at the player, 18% left (enough to read the wall that was
        /// there), the player end 1 m above the feet, ground structures from the knees up, decks from 0.8 m above the
        /// player end.</summary>
        public static OccluderFadeSettings Default
        {
            get
            {
                return new OccluderFadeSettings
                {
                    CameraRadiusM = 1.0f, NearCoverM = 0.8f, MaxCameraRadiusM = 2.0f, PlayerRadiusM = 1.3f, SoftFraction = 0.12f,
                    SoftMinM = 0.15f, SoftMaxM = 0.6f, SolidRadiusM = 0.5f, MinOpacity = 0.18f, TargetHeightM = 1.0f,
                    GroundKeepM = 0.5f, OverheadM = 0.8f,
                };
            }
        }
    }

    /// <summary>
    /// One frame's occluder capsule from the camera to the player, exactly what <c>Ghumante/ToonLit</c> receives as
    /// <c>_GhOccluderA..D</c>, with the engine-free twin of the shader's <c>GhOccluderOpacity</c> (for tests and tools).
    /// The fade is decided per fragment: no per-object CPU work.
    /// <list type="bullet">
    /// <item>Fully faded within a radius growing from <see cref="CameraRadius"/> at the camera to
    /// <see cref="PlayerRadius"/> at the player, soft over <see cref="Soft"/> outside it. There is no fade-in at the camera
    /// end: a wall right in front of the lens is gone.</item>
    /// <item>Nothing at or beyond the plane through the player (perpendicular to the view line) fades: the background
    /// stays.</item>
    /// <item>A small sphere around the player end stays solid (<see cref="SolidRadius"/>).</item>
    /// <item>Ground layers fade only above <see cref="KeepY"/>, and their upward-facing surfaces only above
    /// <see cref="OverheadY"/>.</item>
    /// </list>
    /// </summary>
    public struct OccluderCapsule
    {
        /// <summary>Width (m) of the ground layers' fade-in above <see cref="KeepY"/> and <see cref="OverheadY"/>.</summary>
        public const float GroundRampM = 0.3f;

        /// <summary>Camera end (scene metres) and its fully faded radius.</summary>
        public float Ax, Ay, Az, CameraRadius;

        /// <summary>Player end (scene metres).</summary>
        public float Bx, By, Bz;

        /// <summary>Soft edge (m), solid sphere radius (m), opacity left inside, fully faded radius at the player end.</summary>
        public float Soft, SolidRadius, MinOpacity, PlayerRadius;

        /// <summary>World heights for the ground layers: fade only above <see cref="KeepY"/> (the player's knees), and
        /// upward-facing surfaces only above <see cref="OverheadY"/>.</summary>
        public float KeepY, OverheadY;

        /// <summary>Length of the capsule (m).</summary>
        public float Length
        {
            get
            {
                float x = Bx - Ax, y = By - Ay, z = Bz - Az;
                return (float)Math.Sqrt(x * x + y * y + z * z);
            }
        }

        /// <summary>
        /// The capsule for a camera at (<paramref name="camX"/>, ...) looking at a player end (<paramref name="targetX"/>,
        /// ...), with the camera's vertical field of view as tan(fov / 2) and its aspect (width / height).
        /// </summary>
        public static OccluderCapsule From(in OccluderFadeSettings s, float camX, float camY, float camZ, float targetX, float targetY,
                                           float targetZ, float tanHalfFovY, float aspect)
        {
            float dx = targetX - camX, dy = targetY - camY, dz = targetZ - camZ;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float halfDiagonal = s.NearCoverM * Math.Max(tanHalfFovY, 0f) * (float)Math.Sqrt(1.0 + (double)aspect * aspect);
            float feet = targetY - s.TargetHeightM;
            return new OccluderCapsule
            {
                Ax = camX, Ay = camY, Az = camZ,
                CameraRadius = Clamp(halfDiagonal, s.CameraRadiusM, Math.Max(s.MaxCameraRadiusM, s.CameraRadiusM)),
                Bx = targetX, By = targetY, Bz = targetZ,
                Soft = Clamp(s.SoftFraction * length, s.SoftMinM, s.SoftMaxM),
                SolidRadius = Math.Min(s.SolidRadiusM, 0.25f * length),
                MinOpacity = s.MinOpacity,
                PlayerRadius = s.PlayerRadiusM,
                KeepY = feet + s.GroundKeepM,
                OverheadY = targetY + s.OverheadM,
            };
        }

        /// <summary>
        /// Opacity (<see cref="MinOpacity"/>..1) of a world point with the world normal's y component
        /// <paramref name="normalY"/> (unit normal); <paramref name="ground"/> for the ground layers. The shader discards the
        /// fragment where the opacity is below its 4 × 4 Bayer threshold. Mirrors <c>GhOccluderOpacity</c>.
        /// </summary>
        public float Opacity(float px, float py, float pz, float normalY, bool ground)
        {
            float abx = Bx - Ax, aby = By - Ay, abz = Bz - Az;
            float len2 = Math.Max(abx * abx + aby * aby + abz * abz, 1e-4f);
            float len = (float)Math.Sqrt(len2);
            float t = ((px - Ax) * abx + (py - Ay) * aby + (pz - Az) * abz) / len2;
            float tc = Saturate(t);
            float soft = Math.Max(Soft, 1e-3f);
            float radius = CameraRadius + (PlayerRadius - CameraRadius) * tc;
            float side = Saturate((radius + soft - Distance(px, py, pz, Ax + abx * tc, Ay + aby * tc, Az + abz * tc)) / soft);
            float cap = Saturate((1f - t) * len / (0.5f * soft));
            float away = Saturate((Distance(px, py, pz, Bx, By, Bz) - SolidRadius) / Math.Max(0.5f * SolidRadius, 1e-3f));
            float fade = side * cap * away;
            if (ground)
            {
                float above = Saturate((py - KeepY) / GroundRampM);
                float upright = Saturate((0.7f - normalY) / 0.2f);
                float overhead = Saturate((py - OverheadY) / GroundRampM);
                fade *= above * Math.Max(upright, overhead);
            }
            return 1f - fade * (1f - MinOpacity);
        }

        private static float Distance(float ax, float ay, float az, float bx, float by, float bz)
        {
            float x = ax - bx, y = ay - by, z = az - bz;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        private static float Saturate(float x)
        {
            return x < 0f ? 0f : x > 1f ? 1f : x;
        }

        private static float Clamp(float x, float lo, float hi)
        {
            return x < lo ? lo : x > hi ? hi : x;
        }
    }
}
