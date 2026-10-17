using System;

namespace Ghumante.Core.Synth.Look
{
    /// <summary>
    /// Tuning of the occluder fade: surfaces of a material with <c>_OCCLUDER_FADE</c> (near building bands, temples,
    /// trees, props, people, bridge structures) that lie between the camera and the explorer dissolve into a screen-door
    /// dither, but only while the explorer is really hidden from the camera for a sustained moment
    /// (<see cref="OccluderFadeGate"/>, owner playtest #4 and #12: houses beside the camera or beside the explorer must
    /// never dither on and off as the camera moves). The cleared region is a narrow cone from the lens to the explorer
    /// (<see cref="OccluderCapsule"/>). Engine-free; World/Rendering's <c>ToonLook</c> runs the gate every frame and the
    /// shader evaluates the capsule per fragment.
    /// </summary>
    public struct OccluderFadeSettings
    {
        /// <summary>Fully faded radius at the camera end of the cone (m): just the line of sight, so walls beside the
        /// lens stay solid.</summary>
        public float CameraRadiusM;

        /// <summary>Optional: widen the camera end to cover the whole view frustum this far in front of the lens (m);
        /// 0 = off (the default since the playtest: a wall beside the lens is not in the way).</summary>
        public float NearCoverM;

        /// <summary>Upper bound of the camera-end radius (m).</summary>
        public float MaxCameraRadiusM;

        /// <summary>Fully faded radius at the explorer end (m): the explorer's body and the vehicle around it.</summary>
        public float PlayerRadiusM;

        /// <summary>Soft edge outside the fully faded core = this × the capsule length, clamped to
        /// [<see cref="SoftMinM"/>, <see cref="SoftMaxM"/>] (a short capsule gets a short edge).</summary>
        public float SoftFraction, SoftMinM, SoftMaxM;

        /// <summary>A sphere of this radius around the explorer end stays solid (the explorer's body: a person standing
        /// next to them, the doorway they stand in), soft over half the radius outside it; at most a quarter of the
        /// capsule.</summary>
        public float SolidRadiusM;

        /// <summary>Opacity left deep inside the capsule at full strength (0 = fully gone, 1 = no fade).</summary>
        public float MinOpacity;

        /// <summary>Height of the capsule's explorer end above the feet (m): the chest of someone walking, about the seat
        /// of a scooter.</summary>
        public float TargetHeightM;

        /// <summary>Ground layers (a material in ground mode) fade only above the explorer's feet + this (m): road
        /// surfaces, kerbs and footpaths never dissolve.</summary>
        public float GroundKeepM;

        /// <summary>Upward-facing ground surfaces fade only this far above the explorer end (m): a flyover deck
        /// overhead, never the road ahead climbing a hill.</summary>
        public float OverheadM;

        // ---- the sight gate (OccluderFadeGate) ----

        /// <summary>Radius of the sight casts from the camera to the explorer (m).</summary>
        public float ProbeRadiusM;

        /// <summary>The head probe is this far above the chest (the capsule's explorer end), m.</summary>
        public float HeadM;

        /// <summary>Solids within this of the explorer's body do not hide them (the wall they lean on), m.</summary>
        public float EndMarginM;

        /// <summary>The fade engages after this long of continuous hiding (s) ...</summary>
        public float EngageS;

        /// <summary>... and releases after this long of continuous sight (s).</summary>
        public float ReleaseS;

        /// <summary>The strength ramps in over this long (s) ...</summary>
        public float FadeInS;

        /// <summary>... and out over this long (s).</summary>
        public float FadeOutS;

        /// <summary>A camera or explorer move larger than this in one frame (m) is a cut: the gate starts over solid.</summary>
        public float JumpM;

        /// <summary>The defaults: a cone from 0.35 m at the lens to 0.8 m at the explorer (no frustum cover), soft edges of
        /// 12% of the capsule (0.15 to 0.45 m), a 0.5 m solid sphere at the explorer, 18% left at full strength, the
        /// explorer end 1 m above the feet, ground structures from the knees up, decks from 0.8 m above the explorer end;
        /// the gate: 0.12 m sight casts to the chest and 0.6 m higher, ignoring the last 0.45 m, engaging after 0.35 s
        /// hidden, releasing after 0.6 s in sight, fading in over 0.3 s and out over 0.45 s, reset by a 12 m jump.</summary>
        public static OccluderFadeSettings Default
        {
            get
            {
                return new OccluderFadeSettings
                {
                    CameraRadiusM = 0.35f, NearCoverM = 0f, MaxCameraRadiusM = 2.0f, PlayerRadiusM = 0.8f, SoftFraction = 0.12f,
                    SoftMinM = 0.15f, SoftMaxM = 0.45f, SolidRadiusM = 0.5f, MinOpacity = 0.18f, TargetHeightM = 1.0f,
                    GroundKeepM = 0.5f, OverheadM = 0.8f,
                    ProbeRadiusM = 0.12f, HeadM = 0.6f, EndMarginM = 0.45f, EngageS = 0.35f, ReleaseS = 0.6f, FadeInS = 0.3f,
                    FadeOutS = 0.45f, JumpM = 12f,
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
    /// <item>Everything scales with <see cref="Strength"/> (the <see cref="OccluderFadeGate"/>'s smooth output; 0 =
    /// nothing fades, the shader skips the clip).</item>
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

        /// <summary>How much of the fade applies, 0..1 (<c>_GhOccluderB.w</c>): 1 from <see cref="From"/>, the gate's
        /// smooth strength in the game.</summary>
        public float Strength;

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
            float halfDiagonal = Math.Max(s.NearCoverM, 0f) * Math.Max(tanHalfFovY, 0f) * (float)Math.Sqrt(1.0 + (double)aspect * aspect);
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
                Strength = 1f,
            };
        }

        /// <summary>
        /// Opacity (<see cref="MinOpacity"/>..1, scaled by <see cref="Strength"/>) of a world point with the world normal's y component
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
            float fade = side * cap * away * Saturate(Strength);
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
