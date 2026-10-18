using System;

namespace Ghumante.Characters
{
    /// <summary>
    /// Sprint lock on foot (owner, first playtest #10: "Add locking mechanism while user is sprinting, so that the player
    /// do not have to keep pushing it forward"). Once locked the explorer keeps sprinting forward (away from the camera,
    /// steered by turning the camera or by the stick's sideways push) with no key or stick held, until the player stops it.
    /// <list type="bullet">
    /// <item><b>Lock</b>: double-tap sprint (Shift, gamepad X: two presses within <see cref="DoubleTapS"/>); or, on touch,
    /// push the stick up past its rim (<see cref="ControlFrame.SprintLockDrag"/>, the "drag into the lock" of mobile
    /// shooters) and hold it there <see cref="DragHoldS"/>.</item>
    /// <item><b>Unlock</b>: tap sprint again, pull the stick or the keys back (<see cref="BackOffY"/>), press Action, or
    /// run into a wall for <see cref="StuckS"/>; the owner also drops it on getting into a vehicle, on an emote, in a calm
    /// zone and while paused (<see cref="Release"/>). Jumping keeps it.</item>
    /// </list>
    /// Engine-free and allocation-free; one instance per explorer, stepped once per frame on foot.
    /// </summary>
    public sealed class SprintLock
    {
        /// <summary>Two sprint presses closer than this lock (or unlock) the sprint.</summary>
        public const float DoubleTapS = 0.35f;

        /// <summary>The touch stick held past its rim this long locks the sprint.</summary>
        public const float DragHoldS = 0.25f;

        /// <summary>Pulling the stick or the keys back past this (towards the camera) unlocks.</summary>
        public const float BackOffY = -0.35f;

        /// <summary>Locked but barely moving (a wall, a steep bank) for this long unlocks.</summary>
        public const float StuckS = 0.8f;

        /// <summary>Below this speed the locked explorer counts as stuck.</summary>
        public const float StuckMps = 0.4f;

        private bool _boostWas;
        private float _sincePress = float.PositiveInfinity;
        private float _dragS, _stuckS;

        /// <summary>True while the sprint is locked.</summary>
        public bool Locked { get; private set; }

        /// <summary>The lock changed on the last <see cref="Step"/> (the HUD shows a toast).</summary>
        public bool Changed { get; private set; }

        /// <summary>
        /// Steps the lock with this frame's controls and the explorer's speed, and rewrites <paramref name="c"/>: while
        /// locked it pushes forward at full stick (keeping any sideways push for steering) with sprint on. Returns
        /// <see cref="Locked"/>.
        /// </summary>
        public bool Step(float dt, ref ControlFrame c, float speedMps)
        {
            Changed = false;
            if (!(dt > 0f)) dt = 0f;
            bool press = c.Boost && !_boostWas;
            _boostWas = c.Boost;
            _sincePress += dt;
            if (press)
            {
                if (_sincePress <= DoubleTapS) Set(!Locked);
                else if (Locked) Set(false); // a single tap while locked stops the run
                _sincePress = Changed ? float.PositiveInfinity : 0f;
            }
            if (c.SprintLockDrag)
            {
                _dragS += dt;
                if (!Locked && _dragS >= DragHoldS) Set(true);
            }
            else _dragS = 0f;
            if (Locked)
            {
                if (c.MoveY <= BackOffY || c.ToggleMode) Set(false);
                else
                {
                    _stuckS = Math.Abs(speedMps) < StuckMps ? _stuckS + dt : 0f;
                    // The first moments of a lock start from standing: only a run that stalls counts.
                    if (_stuckS >= StuckS) Set(false);
                }
            }
            if (Locked)
            {
                c.MoveY = 1f;
                c.Boost = true;
            }
            return Locked;
        }

        /// <summary>Drops the lock (getting into a vehicle, a calm zone, an emote, pause).</summary>
        public void Release()
        {
            if (Locked) Set(false);
            _dragS = 0f;
        }

        private void Set(bool locked)
        {
            if (Locked == locked) return;
            Locked = locked;
            Changed = true;
            _stuckS = locked ? -0.6f : 0f; // a grace while the run builds up
        }
    }
}
