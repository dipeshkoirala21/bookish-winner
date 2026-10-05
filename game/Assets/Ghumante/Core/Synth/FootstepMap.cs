namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Footstep set and variant choice (W2_DESIGN 7.3 footsteps, 10.3 FootSurface; A §2.1): the surface comes
    /// from <c>GroundSample.Foot</c>; wetness ≥ 0.5 turns Dirt into Mud (as the vehicle grip table does);
    /// barefoot only inside curated shoes-off zones [V]. Variants never repeat immediately; pitch varies ±4%
    /// walking and ±6% running, volume ±1.5 dB. A mutable struct per walker (seeded), no allocation.
    /// </summary>
    public struct FootstepPicker
    {
        private Noise _rng;
        private int _last;
        private bool _init;

        public FootstepPicker(uint seed)
        {
            _rng = new Noise(Dsp.Mix(seed, 0x464F4F54u));
            _last = -1;
            _init = true;
        }

        /// <summary>The surface that sounds for a foot surface byte, wetness and the shoes-off rule.</summary>
        public static FootstepSurface Resolve(byte footSurface, float wetness01, bool barefoot)
        {
            if (barefoot) return FootstepSurface.Barefoot;
            var s = footSurface <= (byte)FootstepSurface.Water ? (FootstepSurface)footSurface : FootstepSurface.Concrete;
            if (s == FootstepSurface.Dirt && wetness01 >= 0.5f) s = FootstepSurface.Mud;
            return s;
        }

        /// <summary>Picks the bank sound, variant, pitch and gain (dB) for one footstep.</summary>
        public void Next(FootstepSurface surface, FootstepGait gait, out BankSound sound, out int variant, out float pitch, out float gainDb)
        {
            if (!_init) this = new FootstepPicker(0x5EED);
            sound = ProceduralBank.Footstep(surface);
            if (gait == FootstepGait.Walk || gait == FootstepGait.Run)
            {
                int baseV = ProceduralBank.FootstepVariant(gait, 0);
                bool lastHere = _last >= baseV && _last < baseV + 6;
                // With the previous variant in this gait: five choices that skip it (no immediate repeat).
                int v = baseV + _rng.Index(lastHere ? 5 : 6);
                if (lastHere && v >= _last) v++;
                variant = v;
            }
            else
            {
                variant = ProceduralBank.FootstepVariant(gait, 0);
            }
            _last = variant;
            float spread = gait == FootstepGait.Run ? 0.06f : 0.04f;
            pitch = 1f + _rng.Range(-spread, spread);
            gainDb = _rng.Range(-1.5f, 1.5f) + (gait == FootstepGait.Run ? 2f : gait == FootstepGait.Land ? 3f : 0f);
        }
    }
}
