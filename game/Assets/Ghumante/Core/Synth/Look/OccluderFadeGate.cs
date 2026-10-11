using System;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Synth.Look
{
    /// <summary>
    /// When the occluder fade may dissolve anything at all (owner playtest #4 and #12: "buildings constantly flicker and
    /// stutter... make them stable and solid"; the fade used to dissolve every house surface near the camera-to-player
    /// line on every frame, so walls beside the camera and beside the explorer flickered between solid and dithered as
    /// the camera moved, although the explorer was in plain view). The gate fades only what <b>really hides the explorer
    /// for a sustained moment</b>:
    /// <list type="bullet">
    /// <item><b>Sight test</b> (<see cref="IsHidden"/>): thin sphere casts from the camera to the explorer's chest and
    /// head against the world's solid geometry (<see cref="IViewObstacleQuery"/>: house solids, structures, statues,
    /// decks and the terrain, the same geometry the chase camera's boom avoids). The explorer is hidden only when both
    /// are blocked; whatever stands within <see cref="OccluderFadeSettings.EndMarginM"/> of the body (the wall the
    /// explorer leans on, the doorway they stand in) does not count.</item>
    /// <item><b>Hysteresis</b>: the fade engages after <see cref="OccluderFadeSettings.EngageS"/> of continuous hiding
    /// and releases after <see cref="OccluderFadeSettings.ReleaseS"/> of continuous sight, so a lamp post, a corner or a
    /// balcony passing the line for a moment never flickers anything.</item>
    /// <item><b>Smooth strength</b>: in over <see cref="OccluderFadeSettings.FadeInS"/>, out over
    /// <see cref="OccluderFadeSettings.FadeOutS"/> (smoothstep): the screen door thins and thickens through the 16 Bayer
    /// levels instead of popping.</item>
    /// </list>
    /// A jump of the camera or the explorer (teleport, camera cut) resets it. Engine-free, no allocation; World/Rendering's
    /// <c>ToonLook</c> runs one per frame for the world camera and hands the shader the capsule with
    /// <see cref="OccluderCapsule.Strength"/>. When in doubt the camera moves (the chase camera's boom keeps the line of
    /// sight clear), never the houses.
    /// </summary>
    public sealed class OccluderFadeGate
    {
        private OccluderFadeSettings _settings = OccluderFadeSettings.Default;
        private float _level;
        private float _hiddenFor, _clearFor;
        private bool _engaged, _hidden, _primed;
        private float _lastCx, _lastCy, _lastCz, _lastTx, _lastTy, _lastTz;

        /// <summary>The tuning (applied from the next update).</summary>
        public OccluderFadeSettings Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }

        /// <summary>The last sight test found the explorer hidden.</summary>
        public bool Hidden
        {
            get { return _hidden; }
        }

        /// <summary>The fade is wanted (engaged after sustained hiding, until sustained sight).</summary>
        public bool Engaged
        {
            get { return _engaged; }
        }

        /// <summary>The linear fade level, 0..1.</summary>
        public float Level
        {
            get { return _level; }
        }

        /// <summary>The fade strength the shader gets (smoothstep of <see cref="Level"/>): 0 = nothing dissolves.</summary>
        public float Strength
        {
            get { return _level * _level * (3f - 2f * _level); }
        }

        /// <summary>Seconds the explorer has been hidden without a break (0 while in sight).</summary>
        public float HiddenForS
        {
            get { return _hiddenFor; }
        }

        /// <summary>Seconds the explorer has been in sight without a break (0 while hidden).</summary>
        public float ClearForS
        {
            get { return _clearFor; }
        }

        /// <summary>Back to solid at once, timers cleared (teleport, camera cut, a world opened or closed).</summary>
        public void Reset()
        {
            _level = 0f;
            _hiddenFor = 0f;
            _clearFor = 0f;
            _engaged = false;
            _hidden = false;
            _primed = false;
        }

        /// <summary>
        /// Advances the hysteresis by <paramref name="dt"/> seconds with this frame's sight test and returns
        /// <see cref="Strength"/>. A paused frame (dt = 0) changes nothing.
        /// </summary>
        public float Step(bool hidden, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return Strength;
            dt = Math.Min(dt, 0.25f); // a hitch never skips the hysteresis
            _hidden = hidden;
            if (hidden)
            {
                _hiddenFor += dt;
                _clearFor = 0f;
                if (!_engaged && _hiddenFor >= _settings.EngageS) _engaged = true;
            }
            else
            {
                _clearFor += dt;
                _hiddenFor = 0f;
                if (_engaged && _clearFor >= _settings.ReleaseS) _engaged = false;
            }
            float rate = _engaged ? 1f / Math.Max(_settings.FadeInS, 1e-3f) : -1f / Math.Max(_settings.FadeOutS, 1e-3f);
            _level = Math.Max(0f, Math.Min(1f, _level + rate * dt));
            return Strength;
        }

        /// <summary>
        /// One frame for a camera at (<paramref name="camX"/>, ...) looking at the explorer's capsule end
        /// (<paramref name="targetX"/>, ...: the chest, <see cref="OccluderFadeSettings.TargetHeightM"/> above the feet),
        /// both in scene metres; (<paramref name="originX"/>, ...) is the floating origin in game metres, so the sight
        /// test runs against <paramref name="query"/> in game metres (null: never hidden). Returns the capsule with its
        /// <see cref="OccluderCapsule.Strength"/>.
        /// </summary>
        public OccluderCapsule Update(IViewObstacleQuery query, double originX, double originY, double originZ, float camX, float camY,
                                      float camZ, float targetX, float targetY, float targetZ, float dt, float tanHalfFovY, float aspect)
        {
            OccluderFadeSettings s = _settings;
            if (_primed && (Dist(camX, camY, camZ, _lastCx, _lastCy, _lastCz) > s.JumpM ||
                            Dist(targetX, targetY, targetZ, _lastTx, _lastTy, _lastTz) > s.JumpM))
                Reset();
            _primed = true;
            _lastCx = camX;
            _lastCy = camY;
            _lastCz = camZ;
            _lastTx = targetX;
            _lastTy = targetY;
            _lastTz = targetZ;
            bool hidden = query != null && IsHidden(query, s, originX + camX, originY + camY, originZ + camZ, originX + targetX,
                                                    originY + targetY, originZ + targetZ);
            float strength = Step(hidden, dt);
            OccluderCapsule c = OccluderCapsule.From(s, camX, camY, camZ, targetX, targetY, targetZ, tanHalfFovY, aspect);
            c.Strength = strength;
            return c;
        }

        /// <summary>
        /// True when solid geometry hides the explorer from the camera: sphere casts of
        /// <see cref="OccluderFadeSettings.ProbeRadiusM"/> from the camera to the chest (<paramref name="tx"/>, ...) and to
        /// the head (<see cref="OccluderFadeSettings.HeadM"/> higher) both meet something before the last
        /// <see cref="OccluderFadeSettings.EndMarginM"/>. Game metres. Allocation free.
        /// </summary>
        public static bool IsHidden(IViewObstacleQuery query, in OccluderFadeSettings s, double cx, double cy, double cz, double tx, double ty,
                                    double tz)
        {
            if (query == null) return false;
            return Blocked(query, s, cx, cy, cz, tx, ty, tz) && Blocked(query, s, cx, cy, cz, tx, ty + s.HeadM, tz);
        }

        private static bool Blocked(IViewObstacleQuery query, in OccluderFadeSettings s, double cx, double cy, double cz, double tx, double ty,
                                    double tz)
        {
            double dx = tx - cx, dy = ty - cy, dz = tz - cz;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double reach = len - Math.Max(0f, s.EndMarginM);
            if (!(reach > 0.05) || double.IsNaN(len)) return false;
            double hit;
            return query.SphereCast(cx, cy, cz, dx / len, dy / len, dz / len, Math.Max(0f, s.ProbeRadiusM), reach, out hit);
        }

        private static float Dist(float ax, float ay, float az, float bx, float by, float bz)
        {
            float x = ax - bx, y = ay - by, z = az - bz;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
