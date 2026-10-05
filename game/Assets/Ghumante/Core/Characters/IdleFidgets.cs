using System;

namespace Ghumante.Core.Characters
{
    /// <summary>Idle personality clips (P §4.2). Append-only.</summary>
    public enum Fidget : byte
    {
        None = 0,
        LookAround = 1,
        Stretch = 2,
        CheckPhone = 3,
        ShiftWeight = 4,
        Yawn = 5,
        AdjustTopi = 6,
        FlickHair = 7,
        ToeTap = 8,

        /// <summary>The only "comic-free" rest inside sacred areas (calm mode replaces every other fidget).</summary>
        HandsTogetherRest = 9,
    }

    /// <summary>Social and sacred emotes (P §4.4). Append-only.</summary>
    public enum Emote : byte
    {
        None = 0,

        /// <summary>Palms pressed together at the sternum, fingers up, head bow 15° + spine 8°, 40 frames.</summary>
        Namaste = 1,

        /// <summary>The namaste for elders: hands 0.05 m higher and a 22° bow.</summary>
        Namaskar = 2,

        /// <summary>Right hand up to head height, forearm swinging ±25° at 2.5 Hz, 3 swings, 30 frames.</summary>
        Wave = 3,

        /// <summary>Both arms up, a little hop (not in sacred areas).</summary>
        Cheer = 4,

        /// <summary>Right hand strikes or pulls a shrine bell once, 30 frames.</summary>
        RingBell = 5,
    }

    /// <summary>Where the player is, for the fidget table (P §4.2).</summary>
    public struct FidgetContext
    {
        /// <summary>Inside a SACRED area (calm mode): only look-around, shift-weight and hands-together rest.</summary>
        public bool Sacred;

        public bool HasTopi;
        public bool LongHair;

        /// <summary>Urban streets (check_phone).</summary>
        public bool Urban;

        /// <summary>Game hour of day (yawn after 20:00).</summary>
        public float Hour;

        /// <summary>Bells or music nearby (toe_tap).</summary>
        public bool NearMusic;
    }

    /// <summary>
    /// Picks idle fidgets (P §4.2): after <see cref="IdleDelayS"/> without input, a weighted pick from the table, never
    /// one of the last three, then a 4–8 s rest before the next. Any input stops the current fidget at once. Inside
    /// sacred areas only <see cref="Fidget.LookAround"/>, <see cref="Fidget.ShiftWeight"/> and
    /// <see cref="Fidget.HandsTogetherRest"/> play (calm mode, W2_DESIGN 6.6). Deterministic from its seed.
    /// </summary>
    public sealed class IdleFidgets
    {
        public const float IdleDelayS = 6f, RestMinS = 4f, RestMaxS = 8f;

        private readonly Fidget[] _recent = new Fidget[3];
        private uint _seed;
        private uint _draws;
        private float _idle;
        private float _rest;

        public IdleFidgets(uint seed)
        {
            _seed = seed;
        }

        public Fidget Current { get; private set; }

        /// <summary>Seconds into the current fidget.</summary>
        public float Time { get; private set; }

        /// <summary>Seconds of the current fidget.</summary>
        public float Duration { get; private set; }

        /// <summary>Clip length in seconds (frames at 30 fps from the table).</summary>
        public static float DurationOf(Fidget f)
        {
            switch (f)
            {
                case Fidget.LookAround: return 4f;
                case Fidget.Stretch: return 3f;
                case Fidget.CheckPhone: return 4f;
                case Fidget.ShiftWeight: return 3f;
                case Fidget.Yawn: return 3f;
                case Fidget.AdjustTopi: return 1.5f;
                case Fidget.FlickHair: return 1.33f;
                case Fidget.ToeTap: return 4f;
                case Fidget.HandsTogetherRest: return 3f;
                default: return 0f;
            }
        }

        /// <summary>Weight of a fidget in a context (0 = not allowed there).</summary>
        public static int WeightOf(Fidget f, in FidgetContext c)
        {
            if (c.Sacred)
            {
                switch (f)
                {
                    case Fidget.LookAround:
                    case Fidget.ShiftWeight:
                    case Fidget.HandsTogetherRest:
                        return 3;
                    default:
                        return 0;
                }
            }
            switch (f)
            {
                case Fidget.LookAround: return 3;
                case Fidget.Stretch: return 2;
                case Fidget.CheckPhone: return c.Urban ? 2 : 0;
                case Fidget.ShiftWeight: return 3;
                case Fidget.Yawn: return c.Hour >= 20f || c.Hour < 4f ? 1 : 0;
                case Fidget.AdjustTopi: return c.HasTopi ? 2 : 0;
                case Fidget.FlickHair: return c.LongHair ? 1 : 0;
                case Fidget.ToeTap: return c.NearMusic ? 1 : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// Advances by <paramref name="dt"/>. <paramref name="input"/> is true while the player moves, rides or emotes
        /// (it resets the idle timer and stops a fidget). Returns the fidget playing now.
        /// </summary>
        public Fidget Update(float dt, bool input, in FidgetContext context)
        {
            if (!(dt > 0f)) return Current;
            if (input)
            {
                _idle = 0f;
                _rest = 0f;
                Current = Fidget.None;
                Time = 0f;
                return Current;
            }
            _idle += dt;
            if (Current != Fidget.None)
            {
                Time += dt;
                // Leaving or entering a sacred area mid-clip ends a fidget that is not allowed there.
                if (Time >= Duration || WeightOf(Current, context) == 0)
                {
                    Current = Fidget.None;
                    Time = 0f;
                    _rest = RestMinS + (RestMaxS - RestMinS) * CharMath.Unit(Next());
                }
                return Current;
            }
            if (_idle < IdleDelayS) return Current;
            if (_rest > 0f)
            {
                _rest -= dt;
                return Current;
            }
            Fidget pick = Pick(context);
            if (pick == Fidget.None) return Current;
            Current = pick;
            Time = 0f;
            Duration = DurationOf(pick);
            _recent[2] = _recent[1];
            _recent[1] = _recent[0];
            _recent[0] = pick;
            return Current;
        }

        private Fidget Pick(in FidgetContext c)
        {
            int total = 0;
            for (int f = 1; f <= 9; f++) total += Allowed((Fidget)f, c);
            if (total == 0)
            {
                // Everything allowed was played recently: allow repeats rather than stand still forever.
                Array.Clear(_recent, 0, _recent.Length);
                for (int f = 1; f <= 9; f++) total += Allowed((Fidget)f, c);
                if (total == 0) return Fidget.None;
            }
            int roll = (int)(Next() % (uint)total);
            for (int f = 1; f <= 9; f++)
            {
                int w = Allowed((Fidget)f, c);
                if (roll < w) return (Fidget)f;
                roll -= w;
            }
            return Fidget.None;
        }

        private int Allowed(Fidget f, in FidgetContext c)
        {
            for (int i = 0; i < _recent.Length; i++)
                if (_recent[i] == f) return 0;
            return WeightOf(f, c);
        }

        private uint Next()
        {
            _draws++;
            return CharMath.Hash(_seed, _draws, 0x46494447u);
        }
    }
}
