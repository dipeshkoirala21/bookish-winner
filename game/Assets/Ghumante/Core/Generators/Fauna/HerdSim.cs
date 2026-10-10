using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// A deterministic group of ground animals living round a home point (W2_DESIGN 5.5): a macaque troop on a
    /// temple terrace, goats on a field bund, hens in a yard, ducks at a pond, buffalo by a shed. Each animal picks an
    /// activity every few seconds (graze, walk to a new spot, stand, lie, sit; macaques also groom in pairs, carry
    /// babies and climb up onto walls, plinths and chaitya terraces to sit there; ducks swim on the pond and walk the
    /// bank) inside the home disc, keeps a little space from its neighbours and steps aside from the player and from
    /// passing vehicles (macaques keep 2 m and bound off, hens scatter with a flutter; never towards anyone, never
    /// aggressive). With a habitat (<see cref="IFaunaHabitat"/>) spots are only picked on open dry ground (never in a
    /// building or a monument, never on a carriageway; ducks swim only on water) and nobody walks into a building.
    /// Positions are relative to home. Fixed 10 Hz steps with a seeded stream: deterministic and allocation-free.
    /// </summary>
    public sealed class HerdSim
    {
        public const float StepS = 0.1f;

        public const int MaxAnimals = 64;

        /// <summary>Perches a troop can use at most.</summary>
        public const int MaxPerches = 16;

        /// <summary>Climbing speed up a wall (m/s) and the drop time back down (s).</summary>
        public const float ClimbMps = 0.7f, DropS = 0.45f;

        /// <summary>Animals step aside from a moving vehicle that comes this close (m).</summary>
        public const float VehicleKeepM = 3.2f;

        public readonly FaunaSpecies Species;
        public readonly int Count;
        public readonly double HomeX, HomeZ;
        public float HomeY;
        public readonly float RadiusM;
        public readonly uint Seed;

        /// <summary>The world's ground, or null (then every spot in the disc is fine and nobody swims).</summary>
        public readonly IFaunaHabitat Habitat;

        public readonly float[] X, Z, Heading, Speed, ClipTime;
        public readonly FaunaClip[] Clip;

        /// <summary>Height above <see cref="HomeY"/> of an animal that is up on a perch or climbing
        /// (<see cref="Elevated"/>); otherwise the presenter puts it on the ground under its position.</summary>
        public readonly float[] Y;

        /// <summary>True while an animal climbs, sits on a perch or drops back down.</summary>
        public readonly bool[] Elevated;

        /// <summary>Instance tint index of each animal (coat colour; see <see cref="FaunaPalette.CoatFor"/>).</summary>
        public readonly int[] Tint;

        /// <summary>For macaques: index of the animal this one carries on its back (−1 none), or that carries it.</summary>
        public readonly int[] Carrier;

        private const byte PerchNone = 0, PerchGo = 1, PerchClimb = 2, PerchTop = 3, PerchDrop = 4;

        private readonly float[] _timer, _gx, _gz;
        private readonly bool[] _swim;
        private readonly byte[] _perchState;
        private readonly sbyte[] _perch;
        private readonly float[] _perchX = new float[MaxPerches], _perchZ = new float[MaxPerches];
        private readonly float[] _perchAx = new float[MaxPerches], _perchAz = new float[MaxPerches];
        private readonly float[] _perchBase = new float[MaxPerches], _perchTop = new float[MaxPerches];
        private int _perches;
        private FaunaRng _rng;
        private float _acc;

        public HerdSim(FaunaSpecies species, int count, double homeX, double homeZ, float homeY, float radiusM, uint seed)
            : this(species, count, homeX, homeZ, homeY, radiusM, seed, null)
        {
        }

        public HerdSim(FaunaSpecies species, int count, double homeX, double homeZ, float homeY, float radiusM, uint seed, IFaunaHabitat habitat)
        {
            Species = species;
            Count = Math.Max(1, Math.Min(MaxAnimals, count));
            HomeX = homeX;
            HomeZ = homeZ;
            HomeY = homeY;
            RadiusM = Math.Max(1f, radiusM);
            Seed = seed;
            Habitat = habitat;
            int n = Count;
            X = new float[n];
            Z = new float[n];
            Y = new float[n];
            Elevated = new bool[n];
            Heading = new float[n];
            Speed = new float[n];
            ClipTime = new float[n];
            Clip = new FaunaClip[n];
            Tint = new int[n];
            Carrier = new int[n];
            _timer = new float[n];
            _gx = new float[n];
            _gz = new float[n];
            _perchState = new byte[n];
            _perch = new sbyte[n];
            _swim = new bool[n];
            _rng = new FaunaRng(FaunaRng.Hash(seed, (uint)species, 0x4E8Du));
            for (int i = 0; i < n; i++)
            {
                Pick(i, false);
                X[i] = _gx[i];
                Z[i] = _gz[i];
                Heading[i] = _rng.Range(-FMath.Pi, FMath.Pi);
                Tint[i] = (int)(_rng.NextU32() & 0xFFFF);
                Carrier[i] = -1;
                _perch[i] = -1;
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

        /// <summary>Number of perches set.</summary>
        public int PerchCount
        {
            get { return _perches; }
        }

        /// <summary>
        /// Raised spots the troop can climb onto and sit on (walls, plinths, chaitya terraces, rails): the sitting spot
        /// on top (<paramref name="x"/>, <paramref name="z"/>) relative to home, and the ground (<paramref name="baseY"/>)
        /// and top (<paramref name="topY"/>) heights relative to <see cref="HomeY"/>; the climb starts right under the
        /// spot. Macaques only.
        /// </summary>
        public void SetPerches(float[] x, float[] z, float[] baseY, float[] topY, int n)
        {
            SetPerches(x, z, x, z, baseY, topY, n);
        }

        /// <summary>
        /// Raised spots the troop can climb onto and sit on (walls, plinths, chaitya terraces, rails), found by the
        /// presenter from the structure tops round the troop: the sitting spot on top (<paramref name="x"/>,
        /// <paramref name="z"/>, just inside the edge), the foot of the face below it where the climb starts
        /// (<paramref name="ax"/>, <paramref name="az"/>, on the ground just outside the edge), all relative to home, and
        /// the ground (<paramref name="baseY"/>) and top (<paramref name="topY"/>) heights relative to
        /// <see cref="HomeY"/>. A macaque walks to the foot, climbs the face looking at it, steps onto the top and sits
        /// there, and later leaps back down to the foot. Macaques only.
        /// </summary>
        public void SetPerches(float[] x, float[] z, float[] ax, float[] az, float[] baseY, float[] topY, int n)
        {
            _perches = 0;
            if (Species != FaunaSpecies.Macaque || x == null || ax == null) return;
            for (int k = 0; k < n && _perches < MaxPerches; k++)
            {
                if (!(topY[k] - baseY[k] > 0.3f)) continue;
                _perchX[_perches] = x[k];
                _perchZ[_perches] = z[k];
                _perchAx[_perches] = ax[k];
                _perchAz[_perches] = az[k];
                _perchBase[_perches] = baseY[k];
                _perchTop[_perches] = topY[k];
                _perches++;
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
            Step(dt, px, pz, hasPlayer, 0f, 0f, false);
        }

        /// <summary>Advances by <paramref name="dt"/>; animals step aside from the player and from a moving vehicle at
        /// (<paramref name="vx"/>, <paramref name="vz"/>) relative to home (when <paramref name="hasVehicle"/>).</summary>
        public void Step(float dt, float px, float pz, bool hasPlayer, float vx, float vz, bool hasVehicle)
        {
            if (!(dt > 0f)) return;
            _acc = Math.Min(_acc + dt, 6 * StepS);
            while (_acc >= StepS)
            {
                _acc -= StepS;
                Fixed(StepS, px, pz, hasPlayer, vx, vz, hasVehicle);
            }
        }

        private void Fixed(float h, float px, float pz, bool hasPlayer, float tvx, float tvz, bool hasVehicle)
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
                    Y[i] = Y[m];
                    Elevated[i] = Elevated[m];
                    Heading[i] = Heading[m];
                    Clip[i] = FaunaClip.Sit;
                    continue;
                }
                _timer[i] -= h;
                if (_perchState[i] >= PerchClimb)
                {
                    Perched(i, h);
                    continue;
                }
                float vx = 0f, vz = 0f;
                bool fled = Flee(i, px, pz, hasPlayer, keep, info, ref vx, ref vz);
                if (!fled) fled = Flee(i, tvx, tvz, hasVehicle, VehicleKeepM, info, ref vx, ref vz);
                if (!fled)
                {
                    if (_timer[i] <= 0f) Choose(i);
                    if (Clip[i] == FaunaClip.Walk || Clip[i] == FaunaClip.Trot || Clip[i] == FaunaClip.Run || Clip[i] == FaunaClip.Swim)
                    {
                        float dx = _gx[i] - X[i], dz = _gz[i] - Z[i];
                        float d = (float)Math.Sqrt(dx * dx + dz * dz);
                        float sp = Clip[i] == FaunaClip.Walk || Clip[i] == FaunaClip.Swim ? info.WalkMps : info.TrotMps;
                        if (Species == FaunaSpecies.Duck && Habitat != null)
                        {
                            // A duck walks the bank and swims once it is on the water.
                            FaunaClip want = OnWater(X[i], Z[i]) ? FaunaClip.Swim : FaunaClip.Walk;
                            if (want != Clip[i]) ClipTime[i] = 0f;
                            Clip[i] = want;
                        }
                        if (d < 0.1f)
                        {
                            Arrive(i);
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
                    if (j == i || IsRider(j) || Elevated[j]) continue;
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
                float nx = X[i] + vx * h, nz = Z[i] + vz * h;
                // Stay near home.
                float hr = (float)Math.Sqrt(nx * nx + nz * nz);
                if (hr > RadiusM * 1.3f)
                {
                    nx *= RadiusM * 1.3f / hr;
                    nz *= RadiusM * 1.3f / hr;
                }
                if ((vx != 0f || vz != 0f) && !CanStep(X[i], Z[i], nx, nz))
                {
                    vx = vz = 0f;
                    nx = X[i];
                    nz = Z[i];
                    if (_perchState[i] == PerchGo && !fled)
                    {
                        // The wall of the perch itself: climb it from here.
                        Speed[i] = 0f;
                        Arrive(i);
                        continue;
                    }
                    // A wall, a house or a carriageway ahead: stop and choose again.
                    if (!fled && _timer[i] > 0.3f) _timer[i] = 0.3f;
                    if (_perchState[i] == PerchGo) _perchState[i] = PerchNone;
                }
                X[i] = nx;
                Z[i] = nz;
                float v = (float)Math.Sqrt(vx * vx + vz * vz);
                Speed[i] = v;
                if (Species == FaunaSpecies.Duck && Habitat != null && (Clip[i] == FaunaClip.Swim || Clip[i] == FaunaClip.Walk))
                {
                    // Paddle on the water, walk on the bank.
                    bool wet = OnWater(X[i], Z[i]);
                    FaunaClip want = wet ? FaunaClip.Swim : v > 0.05f ? FaunaClip.Walk : FaunaClip.Stand;
                    if (want != Clip[i])
                    {
                        Clip[i] = want;
                        ClipTime[i] = 0f;
                    }
                }
                if (v > 0.05f)
                {
                    float target = (float)Math.Atan2(vx, vz);
                    float dh = FMath.WrapPi(target - Heading[i]);
                    float m = 4f * h;
                    Heading[i] = FMath.WrapPi(Heading[i] + FMath.Clamp(dh, -m, m));
                }
            }
        }

        /// <summary>Steps (or bounds, or scatters) away from a threat within <paramref name="keep"/>.</summary>
        private bool Flee(int i, float tx, float tz, bool has, float keep, FaunaSpeciesInfo info, ref float vx, ref float vz)
        {
            if (!has) return false;
            float dx = X[i] - tx, dz = Z[i] - tz;
            float d2 = dx * dx + dz * dz;
            if (!(d2 < keep * keep) || d2 <= 1e-4f) return false;
            float d = (float)Math.Sqrt(d2);
            float sp = info.TrotMps * 1.2f;
            vx = dx / d * sp;
            vz = dz / d * sp;
            FaunaClip flee = Clip[i] == FaunaClip.Swim ? FaunaClip.Swim :
                             Species == FaunaSpecies.Macaque || Species == FaunaSpecies.Hen || Species == FaunaSpecies.Duck ? FaunaClip.Run : FaunaClip.Trot;
            if (Clip[i] != flee) ClipTime[i] = 0f;
            Clip[i] = flee;
            _timer[i] = 1.5f;
            _gx[i] = X[i] + dx / d * 2.5f;
            _gz[i] = Z[i] + dz / d * 2.5f;
            if (_perchState[i] == PerchGo) _perchState[i] = PerchNone;
            return true;
        }

        /// <summary>Reached the goal: a macaque at its perch starts climbing; a swimming duck floats on.</summary>
        private void Arrive(int i)
        {
            if (_perchState[i] == PerchGo && _perch[i] >= 0)
            {
                int p = _perch[i];
                _perchState[i] = PerchClimb;
                Elevated[i] = true;
                Y[i] = _perchBase[p];
                Clip[i] = FaunaClip.Climb;
                ClipTime[i] = 0f;
                // Face the wall.
                float fx = _perchX[p] - X[i], fz = _perchZ[p] - Z[i];
                if (fx * fx + fz * fz > 1e-4f) Heading[i] = (float)Math.Atan2(fx, fz);
                return;
            }
            if (Species == FaunaSpecies.Duck && OnWater(X[i], Z[i]))
            {
                Clip[i] = FaunaClip.Swim; // floating idly
                return;
            }
            _swim[i] = false;
            Clip[i] = Species == FaunaSpecies.Macaque ? FaunaClip.Sit : FaunaClip.Stand;
            ClipTime[i] = 0f;
        }

        /// <summary>Climbing up, sitting on top (sitting, grooming, looking about), dropping back down.</summary>
        private void Perched(int i, float h)
        {
            int p = _perch[i];
            Speed[i] = 0f;
            switch (_perchState[i])
            {
                case PerchClimb:
                    Y[i] += ClimbMps * h;
                    if (Y[i] >= _perchTop[p])
                    {
                        Y[i] = _perchTop[p];
                        _perchState[i] = PerchTop;
                        Clip[i] = FaunaClip.Sit;
                        ClipTime[i] = 0f;
                        _timer[i] = _rng.Range(15f, 45f);
                        // Step onto the top and sit there, turned to look out over the edge.
                        X[i] = _perchX[p];
                        Z[i] = _perchZ[p];
                        Heading[i] = FMath.WrapPi(Heading[i] + FMath.Pi + _rng.Range(-0.8f, 0.8f));
                    }
                    break;
                case PerchTop:
                    if (_timer[i] <= 0f)
                    {
                        if (_rng.Chance(0.35f))
                        {
                            _perchState[i] = PerchDrop;
                            Clip[i] = FaunaClip.Run;
                            ClipTime[i] = 0f;
                            _timer[i] = DropS;
                        }
                        else
                        {
                            Clip[i] = _rng.Chance(0.4f) ? FaunaClip.Groom : FaunaClip.Sit;
                            ClipTime[i] = 0f;
                            _timer[i] = _rng.Range(10f, 30f);
                        }
                    }
                    break;
                default:
                {
                    // Leap down: a short fall from the top to the foot of the face.
                    float k = FMath.Clamp01(1f - _timer[i] / DropS);
                    Y[i] = FMath.Lerp(_perchTop[p], _perchBase[p], k * k);
                    X[i] = FMath.Lerp(_perchX[p], _perchAx[p], k);
                    Z[i] = FMath.Lerp(_perchZ[p], _perchAz[p], k);
                    if (_timer[i] <= 0f)
                    {
                        Elevated[i] = false;
                        Y[i] = 0f;
                        _perchState[i] = PerchNone;
                        _perch[i] = -1;
                        Pick(i, false);
                        Clip[i] = FaunaClip.Walk;
                        ClipTime[i] = 0f;
                        _timer[i] = _rng.Range(3f, 8f);
                    }
                    break;
                }
            }
        }

        /// <summary>Picks the next activity of an animal (by species).</summary>
        private void Choose(int i)
        {
            float u = _rng.NextFloat();
            FaunaClip c;
            bool water = false;
            switch (Species)
            {
                case FaunaSpecies.Macaque:
                    if (_perches > 0 && u < 0.28f)
                    {
                        // Off to a wall or a chaitya terrace to sit up there.
                        int p = _rng.Next(_perches);
                        _perch[i] = (sbyte)p;
                        _perchState[i] = PerchGo;
                        _gx[i] = _perchAx[p];
                        _gz[i] = _perchAz[p];
                        Clip[i] = FaunaClip.Walk;
                        ClipTime[i] = 0f;
                        _timer[i] = 25f;
                        return;
                    }
                    if (_perches > 0) u = (u - 0.28f) / 0.72f;
                    c = u < 0.5f ? FaunaClip.Sit : u < 0.72f ? FaunaClip.Groom : u < 0.92f ? FaunaClip.Walk : FaunaClip.Run;
                    break;
                case FaunaSpecies.Goat:
                case FaunaSpecies.Buffalo:
                case FaunaSpecies.Cow:
                    c = u < 0.5f ? FaunaClip.Graze : u < 0.7f ? FaunaClip.Walk : u < 0.85f ? FaunaClip.Lie : FaunaClip.Stand;
                    break;
                case FaunaSpecies.Duck:
                    // Swim on the pond (only where there is water), walk the bank, dabble at its edge.
                    if (u < 0.45f && Habitat != null)
                    {
                        c = FaunaClip.Swim;
                        water = true;
                    }
                    else
                    {
                        c = u < 0.75f ? FaunaClip.Walk : FaunaClip.Peck;
                    }
                    break;
                case FaunaSpecies.Hen:
                    c = u < 0.55f ? FaunaClip.Peck : u < 0.85f ? FaunaClip.Walk : FaunaClip.Stand;
                    break;
                default:
                    c = u < 0.5f ? FaunaClip.Stand : FaunaClip.Walk;
                    break;
            }
            _swim[i] = false;
            if (c == FaunaClip.Walk || c == FaunaClip.Run || c == FaunaClip.Swim)
            {
                // No pond in reach: the duck walks instead.
                if (!Pick(i, water) && c == FaunaClip.Swim) c = FaunaClip.Walk;
                if (c == FaunaClip.Swim)
                {
                    _swim[i] = true;
                    if (!OnWater(X[i], Z[i])) c = FaunaClip.Walk;
                }
            }
            else if (Species == FaunaSpecies.Duck && OnWater(X[i], Z[i]))
            {
                // Dabbling while afloat.
                c = FaunaClip.Swim;
                _swim[i] = true;
            }
            Clip[i] = c;
            ClipTime[i] = 0f;
            _timer[i] = c == FaunaClip.Lie || c == FaunaClip.Sit || c == FaunaClip.Groom ? _rng.Range(8f, 25f) : _rng.Range(3f, 9f);
        }

        /// <summary>Picks a goal in the home disc where the animal may be (on water when <paramref name="water"/>);
        /// false when none was found in a few tries (the goal is then the current position).</summary>
        private bool Pick(int i, bool water)
        {
            for (int t = 0; t < 6; t++)
            {
                float r = RadiusM * (float)Math.Sqrt(_rng.NextFloat());
                float a = _rng.Range(0f, FMath.TwoPi);
                float x = r * FMath.Cos(a), z = r * FMath.Sin(a);
                if (!SpotOk(x, z, water)) continue;
                _gx[i] = x;
                _gz[i] = z;
                return true;
            }
            _gx[i] = X[i];
            _gz[i] = Z[i];
            return false;
        }

        /// <summary>True when the animal may stand (or swim, with <paramref name="water"/>) at (x, z) relative to
        /// home; unknown ground counts as fine on land.</summary>
        private bool SpotOk(float x, float z, bool water)
        {
            if (Habitat == null) return !water;
            FaunaGround g = Habitat.GroundAt(HomeX + x, HomeZ + z);
            if (g == FaunaGround.None) return !water;
            if ((g & (FaunaGround.Building | FaunaGround.Road)) != 0) return false;
            if (water) return (g & FaunaGround.Water) != 0;
            return (g & FaunaGround.Water) == 0 || Species == FaunaSpecies.Buffalo;
        }

        /// <summary>True when (x, z) relative to home is on water (never without a habitat).</summary>
        public bool OnWater(float x, float z)
        {
            return Habitat != null && (Habitat.GroundAt(HomeX + x, HomeZ + z) & FaunaGround.Water) != 0;
        }

        /// <summary>False when the step would walk into a building or onto a carriageway (leaving one is allowed);
        /// ducks may walk from the bank onto the pond and back.</summary>
        private bool CanStep(float x0, float z0, float x1, float z1)
        {
            if (Habitat == null) return true;
            FaunaGround g = Habitat.GroundAt(HomeX + x1, HomeZ + z1);
            if (g == FaunaGround.None) return true;
            if ((g & FaunaGround.Building) != 0) return false;
            if ((g & FaunaGround.Road) != 0) return (Habitat.GroundAt(HomeX + x0, HomeZ + z0) & FaunaGround.Road) != 0;
            return true;
        }
    }
}
