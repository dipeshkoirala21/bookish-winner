using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// A deterministic group of ground animals living round a home point (W2_DESIGN 5.5): a macaque troop on a
    /// temple terrace, goats on a field bund, hens in a yard, ducks at a pond, buffalo by a shed. Each animal picks an
    /// activity every few seconds (graze, walk to a new spot, stand, lie, sit; macaques also groom in pairs and carry
    /// babies) inside the home disc, keeps a little space from its neighbours and steps aside from the player
    /// (macaques keep 2 m and bound off, hens scatter with a flutter; never towards the player, never aggressive).
    /// Positions are relative to home. Fixed 10 Hz steps with a seeded stream: deterministic and allocation-free.
    /// </summary>
    public sealed class HerdSim
    {
        public const float StepS = 0.1f;

        public const int MaxAnimals = 64;

        public readonly FaunaSpecies Species;
        public readonly int Count;
        public readonly double HomeX, HomeZ;
        public float HomeY;
        public readonly float RadiusM;
        public readonly uint Seed;

        public readonly float[] X, Z, Heading, Speed, ClipTime;
        public readonly FaunaClip[] Clip;

        /// <summary>Instance tint index of each animal (coat colour; see <see cref="FaunaPalette.CoatFor"/>).</summary>
        public readonly int[] Tint;

        /// <summary>For macaques: index of the animal this one carries on its back (−1 none), or that carries it.</summary>
        public readonly int[] Carrier;

        private readonly float[] _timer, _gx, _gz;
        private FaunaRng _rng;
        private float _acc;

        public HerdSim(FaunaSpecies species, int count, double homeX, double homeZ, float homeY, float radiusM, uint seed)
        {
            Species = species;
            Count = Math.Max(1, Math.Min(MaxAnimals, count));
            HomeX = homeX;
            HomeZ = homeZ;
            HomeY = homeY;
            RadiusM = Math.Max(1f, radiusM);
            Seed = seed;
            int n = Count;
            X = new float[n];
            Z = new float[n];
            Heading = new float[n];
            Speed = new float[n];
            ClipTime = new float[n];
            Clip = new FaunaClip[n];
            Tint = new int[n];
            Carrier = new int[n];
            _timer = new float[n];
            _gx = new float[n];
            _gz = new float[n];
            _rng = new FaunaRng(FaunaRng.Hash(seed, (uint)species, 0x4E8Du));
            for (int i = 0; i < n; i++)
            {
                Pick(i);
                X[i] = _gx[i];
                Z[i] = _gz[i];
                Heading[i] = _rng.Range(-FMath.Pi, FMath.Pi);
                Tint[i] = (int)(_rng.NextU32() & 0xFFFF);
                Carrier[i] = -1;
                Choose(i);
                _timer[i] = _rng.Range(0f, 6f);
            }
            if (species == FaunaSpecies.Macaque)
            {
                // About one adult in ten carries a baby (street_life §7.3): the last animals are the babies.
                int babies = Math.Max(0, n / 10);
                for (int k = 0; k < babies; k++)
                {
                    int mother = k, baby = n - 1 - k;
                    if (baby <= mother) break;
                    Carrier[mother] = baby;
                    Carrier[baby] = mother;
                }
            }
        }

        /// <summary>True when animal <paramref name="i"/> is a baby riding (macaques).</summary>
        public bool IsRider(int i)
        {
            return Species == FaunaSpecies.Macaque && Carrier[i] >= 0 && i >= Count - Math.Max(0, Count / 10);
        }

        /// <summary>The species an animal is drawn as (macaque babies are <see cref="FaunaSpecies.MacaqueBaby"/>; one
        /// hen in five of a yard is a rooster).</summary>
        public FaunaSpecies SpeciesOf(int i)
        {
            if (IsRider(i)) return FaunaSpecies.MacaqueBaby;
            if (Species == FaunaSpecies.Hen && (Tint[i] % 5) == 0) return FaunaSpecies.Rooster;
            return Species;
        }

        /// <summary>Advances by <paramref name="dt"/>; the player at (<paramref name="px"/>, <paramref name="pz"/>)
        /// relative to home (when <paramref name="hasPlayer"/>) makes animals step aside.</summary>
        public void Step(float dt, float px, float pz, bool hasPlayer)
        {
            if (!(dt > 0f)) return;
            _acc = Math.Min(_acc + dt, 6 * StepS);
            while (_acc >= StepS)
            {
                _acc -= StepS;
                Fixed(StepS, px, pz, hasPlayer);
            }
        }

        private void Fixed(float h, float px, float pz, bool hasPlayer)
        {
            FaunaSpeciesInfo info = FaunaCatalog.Info(Species);
            float keep = Species == FaunaSpecies.Macaque ? 2f : Species == FaunaSpecies.Hen || Species == FaunaSpecies.Duck ? 1.6f : 1.2f;
            for (int i = 0; i < Count; i++)
            {
                ClipTime[i] += h;
                if (IsRider(i))
                {
                    // Riding: follows the carrier; the presenter places it on the carrier's back.
                    int m = Carrier[i];
                    X[i] = X[m];
                    Z[i] = Z[m];
                    Heading[i] = Heading[m];
                    Clip[i] = FaunaClip.Sit;
                    continue;
                }
                _timer[i] -= h;
                float vx = 0f, vz = 0f;
                if (hasPlayer)
                {
                    float dx = X[i] - px, dz = Z[i] - pz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < keep * keep && d2 > 1e-4f)
                    {
                        // Step (or bound, or scatter) away, then carry on.
                        float d = (float)Math.Sqrt(d2);
                        float sp = info.TrotMps * 1.2f;
                        vx = dx / d * sp;
                        vz = dz / d * sp;
                        FaunaClip flee = Species == FaunaSpecies.Macaque || Species == FaunaSpecies.Hen ? FaunaClip.Run : FaunaClip.Trot;
                        if (Clip[i] != flee) ClipTime[i] = 0f;
                        Clip[i] = flee;
                        _timer[i] = 1.5f;
                        _gx[i] = X[i] + dx / d * 2.5f;
                        _gz[i] = Z[i] + dz / d * 2.5f;
                    }
                }
                if (vx == 0f && vz == 0f)
                {
                    if (_timer[i] <= 0f) Choose(i);
                    if (Clip[i] == FaunaClip.Walk || Clip[i] == FaunaClip.Trot || Clip[i] == FaunaClip.Run || Clip[i] == FaunaClip.Swim)
                    {
                        float dx = _gx[i] - X[i], dz = _gz[i] - Z[i];
                        float d = (float)Math.Sqrt(dx * dx + dz * dz);
                        float sp = Clip[i] == FaunaClip.Walk || Clip[i] == FaunaClip.Swim ? info.WalkMps : info.TrotMps;
                        if (d < 0.1f)
                        {
                            Clip[i] = Species == FaunaSpecies.Macaque ? FaunaClip.Sit : FaunaClip.Stand;
                            ClipTime[i] = 0f;
                        }
                        else
                        {
                            vx = dx / d * sp;
                            vz = dz / d * sp;
                        }
                    }
                }
                // Keep a little space.
                for (int j = 0; j < Count; j++)
                {
                    if (j == i || IsRider(j)) continue;
                    float dx = X[i] - X[j], dz = Z[i] - Z[j];
                    float d2 = dx * dx + dz * dz;
                    float r = 0.5f * info.LengthM;
                    if (d2 < r * r && d2 > 1e-6f)
                    {
                        float d = (float)Math.Sqrt(d2);
                        vx += dx / d * (r - d) * 2f;
                        vz += dz / d * (r - d) * 2f;
                    }
                }
                X[i] += vx * h;
                Z[i] += vz * h;
                // Stay near home.
                float hr = (float)Math.Sqrt(X[i] * X[i] + Z[i] * Z[i]);
                if (hr > RadiusM * 1.3f)
                {
                    X[i] *= RadiusM * 1.3f / hr;
                    Z[i] *= RadiusM * 1.3f / hr;
                }
                float v = (float)Math.Sqrt(vx * vx + vz * vz);
                Speed[i] = v;
                if (v > 0.05f)
                {
                    float target = (float)Math.Atan2(vx, vz);
                    float dh = FMath.WrapPi(target - Heading[i]);
                    float m = 4f * h;
                    Heading[i] = FMath.WrapPi(Heading[i] + FMath.Clamp(dh, -m, m));
                }
            }
        }

        /// <summary>Picks the next activity of an animal (by species).</summary>
        private void Choose(int i)
        {
            float u = _rng.NextFloat();
            FaunaClip c;
            switch (Species)
            {
                case FaunaSpecies.Macaque:
                    c = u < 0.5f ? FaunaClip.Sit : u < 0.72f ? FaunaClip.Groom : u < 0.92f ? FaunaClip.Walk : FaunaClip.Run;
                    break;
                case FaunaSpecies.Goat:
                case FaunaSpecies.Buffalo:
                case FaunaSpecies.Cow:
                    c = u < 0.5f ? FaunaClip.Graze : u < 0.7f ? FaunaClip.Walk : u < 0.85f ? FaunaClip.Lie : FaunaClip.Stand;
                    break;
                case FaunaSpecies.Duck:
                    c = u < 0.4f ? FaunaClip.Swim : u < 0.7f ? FaunaClip.Walk : FaunaClip.Peck;
                    break;
                case FaunaSpecies.Hen:
                    c = u < 0.55f ? FaunaClip.Peck : u < 0.85f ? FaunaClip.Walk : FaunaClip.Stand;
                    break;
                default:
                    c = u < 0.5f ? FaunaClip.Stand : FaunaClip.Walk;
                    break;
            }
            if (c == FaunaClip.Walk || c == FaunaClip.Run || c == FaunaClip.Swim) Pick(i);
            Clip[i] = c;
            ClipTime[i] = 0f;
            _timer[i] = c == FaunaClip.Lie || c == FaunaClip.Sit || c == FaunaClip.Groom ? _rng.Range(8f, 25f) : _rng.Range(3f, 9f);
        }

        private void Pick(int i)
        {
            float r = RadiusM * (float)Math.Sqrt(_rng.NextFloat());
            float a = _rng.Range(0f, FMath.TwoPi);
            _gx[i] = r * FMath.Cos(a);
            _gz[i] = r * FMath.Sin(a);
        }
    }
}
