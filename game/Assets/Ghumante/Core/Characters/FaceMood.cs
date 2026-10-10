using System;

namespace Ghumante.Core.Characters
{
    /// <summary>A moment the player's face reacts to (<see cref="FaceMood.Cue"/>), strongest last.</summary>
    public enum MoodCue : byte
    {
        /// <summary>Ran into a wall, a tree or a parked vehicle: a short wince-laugh.</summary>
        Bump = 0,

        /// <summary>Landed a jump or a hop off a kerb: a quick grin.</summary>
        Landing = 1,

        /// <summary>A named place seen for the first time this session (the HUD's place banner changed to it).</summary>
        Discovery = 2,

        /// <summary>Reached the end of a planned route (the HUD celebrates).</summary>
        Arrival = 3,
    }

    /// <summary>
    /// The player's face between blinks (W2 detail pass, characters open issue): calm inside sacred zones (temple
    /// compounds, stupa koras, the ghats), the friendly smile elsewhere, and a short reaction on a cue: joy on a
    /// discovery or an arrival, a quick grin on landing, a wince-laugh on a bump. A cue holds for its time
    /// (<see cref="HoldS"/>); a weaker cue never cuts a stronger one short, and inside a sacred zone a bump stays calm (no
    /// laughing in a temple). Out of breath (running more than 4 s) is the avatar's own. Engine-free, deterministic,
    /// allocation-free.
    /// </summary>
    public sealed class FaceMood
    {
        private FaceExpression _cue = FaceExpression.Smile;
        private MoodCue _cueKind;
        private float _left;

        /// <summary>The face shown now.</summary>
        public FaceExpression Current { get; private set; } = FaceExpression.Smile;

        /// <summary>True while a cue's reaction is showing.</summary>
        public bool Reacting
        {
            get { return _left > 0f; }
        }

        /// <summary>Seconds a cue's reaction holds.</summary>
        public static float HoldS(MoodCue cue)
        {
            switch (cue)
            {
                case MoodCue.Arrival: return 3.5f;
                case MoodCue.Discovery: return 2.5f;
                case MoodCue.Landing: return 0.9f;
                default: return 0.8f;
            }
        }

        /// <summary>The face a cue shows (<paramref name="sacred"/>: inside a sacred zone).</summary>
        public static FaceExpression FaceOf(MoodCue cue, bool sacred)
        {
            switch (cue)
            {
                case MoodCue.Arrival:
                case MoodCue.Discovery:
                case MoodCue.Landing: return FaceExpression.Joy;
                default: return sacred ? FaceExpression.Calm : FaceExpression.WinceLaugh;
            }
        }

        /// <summary>The resting face: calm inside a sacred zone, the smile elsewhere.</summary>
        public static FaceExpression RestingFace(bool sacred)
        {
            return sacred ? FaceExpression.Calm : FaceExpression.Smile;
        }

        /// <summary>Starts a reaction (ignored while a stronger one still shows).</summary>
        public void Cue(MoodCue cue, bool sacred)
        {
            if (_left > 0f && cue < _cueKind) return;
            _cueKind = cue;
            _cue = FaceOf(cue, sacred);
            _left = HoldS(cue);
            Current = _cue;
        }

        /// <summary>Advances the reaction by <paramref name="dt"/> seconds and returns the face to show.</summary>
        public FaceExpression Update(float dt, bool sacred)
        {
            if (_left > 0f) _left = Math.Max(0f, _left - Math.Max(0f, dt));
            Current = _left > 0f ? _cue : RestingFace(sacred);
            return Current;
        }

        /// <summary>Drops any reaction (a respawn or teleport).</summary>
        public void Reset()
        {
            _left = 0f;
            _cue = FaceExpression.Smile;
            Current = FaceExpression.Smile;
        }
    }
}
