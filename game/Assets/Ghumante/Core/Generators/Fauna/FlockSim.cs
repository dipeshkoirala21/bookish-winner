using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>The bird groups of the valley (docs/research/w2/street_life.md §8, W2_DESIGN 5.6). Append only.</summary>
    public enum FlockKind : byte
    {
        /// <summary>Rock pigeons on a square: peck in a disc, burst up together, circle, re-land.</summary>
        Pigeons = 0,

        /// <summary>Black kites soaring in thermals over the city and the rivers.</summary>
        Kites = 1,

        /// <summary>House crows hopping on the ground and the roofs.</summary>
        Crows = 2,

        /// <summary>House sparrows at eaves and shopfronts.</summary>
        Sparrows = 3,

        /// <summary>Common mynas strutting on lawns and verges.</summary>
        Mynas = 4,

        /// <summary>Barn swallows swooping low over fields and rivers (March to October).</summary>
        Swallows = 5,

        /// <summary>Cattle egrets walking in the paddy.</summary>
        Egrets = 6,
    }

    /// <summary>Behaviour numbers of a flock kind (street_life §8; [E] values from the design).</summary>
    public readonly struct FlockParams
    {
        public readonly FaunaSpecies Species;

        /// <summary>Ground disc radius the birds peck and walk in (m).</summary>
        public readonly float DiscRadiusM;

        /// <summary>The flock bursts up when a threat comes this close to any bird (m).</summary>
        public readonly float FlushRadiusM;

        /// <summary>Radius and height of the circuit flown after a burst (m), and the cruise speed (m/s).</summary>
        public readonly float CircuitRadiusM, CircuitHeightM, SpeedMps;

        /// <summary>Boids radii: separation, alignment, cohesion (m).</summary>
        public readonly float SeparationM, AlignmentM, CohesionM;

        /// <summary>Laps of the circuit before landing (min, max).</summary>
        public readonly int LapsMin, LapsMax;

        public FlockParams(FaunaSpecies sp, float disc, float flush, float circuitR, float circuitH, float speed, float sep, float align, float coh, int lapsMin,
                           int lapsMax)
        {
            Species = sp;
            DiscRadiusM = disc;
            FlushRadiusM = flush;
            CircuitRadiusM = circuitR;
            CircuitHeightM = circuitH;
            SpeedMps = speed;
            SeparationM = sep;
            AlignmentM = align;
            CohesionM = coh;
            LapsMin = lapsMin;
            LapsMax = lapsMax;
        }

        /// <summary>The numbers of a kind.</summary>
        public static FlockParams Of(FlockKind k)
        {
            switch (k)
            {
                case FlockKind.Pigeons: return new FlockParams(FaunaSpecies.Pigeon, 7f, 3.5f, 42f, 14f, 12f, 0.6f, 3f, 8f, 1, 3);
                case FlockKind.Kites: return new FlockParams(FaunaSpecies.BlackKite, 0f, 0f, 35f, 120f, 10f, 0f, 0f, 0f, 1, 1);
                case FlockKind.Crows: return new FlockParams(FaunaSpecies.Crow, 5f, 5f, 16f, 10f, 9f, 1.0f, 4f, 8f, 1, 2);
                case FlockKind.Sparrows: return new FlockParams(FaunaSpecies.Sparrow, 3f, 4f, 8f, 4.5f, 7f, 0.3f, 2f, 4f, 1, 1);
                case FlockKind.Mynas: return new FlockParams(FaunaSpecies.Myna, 4f, 4f, 10f, 5f, 8f, 0.5f, 2.5f, 5f, 1, 1);
                case FlockKind.Swallows: return new FlockParams(FaunaSpecies.Swallow, 0f, 0f, 30f, 3f, 12f, 0f, 0f, 0f, 1, 1);
                default: return new FlockParams(FaunaSpecies.Egret, 8f, 6f, 18f, 6f, 6f, 1.0f, 4f, 10f, 1, 1);
            }
        }
    }

    /// <summary>
    /// A deterministic bird flock (W2_DESIGN 5.6): pigeons, crows, sparrows, mynas and egrets peck and walk in a disc
    /// round their home, burst up together when the player or a vehicle comes close (staggered over 0.4 s), fly 1–3
    /// laps of a circuit as boids (separation, alignment and cohesion radii from street_life §8) and re-land in their
    /// disc 20–40 s later; kites soar in banked circles 20–50 m across, high over the city; swallows swoop low along
    /// looping paths. Bird positions are relative to the flock's home (metres, y up from the home's ground height).
    /// Fixed 20 Hz steps with a seeded random stream: the same seed and the same calls give the same birds on every
    /// device. Allocation-free after construction (a small spatial hash finds neighbours).
    /// </summary>
    public sealed class FlockSim
    {
        /// <summary>Simulation step (s).</summary>
        public const float StepS = 0.05f;

        /// <summary>Birds per flock at most.</summary>
        public const int MaxBirds = 256;

        private enum Mode : byte
        {
            Ground = 0,
            Air = 1,
            Return = 2,
        }

        private const byte StGround = 0, StTakeOff = 1, StFly = 2, StLand = 3, StSoar = 4;

        public readonly FlockKind Kind;
        public readonly FlockParams P;
        public readonly int Count;
        public readonly uint Seed;

        /// <summary>Radius of the ground disc the birds peck in (m): the curated site's, else the kind's.</summary>
        public readonly float DiscRadiusM;

        /// <summary>The world's ground, or null: ground spots are only picked where birds can stand (not in a
        /// building or a monument, not on a carriageway, not in water; egrets also in wet fields).</summary>
        public readonly IFaunaHabitat Habitat;

        /// <summary>Home of the flock in game metres, and its ground height.</summary>
        public readonly double HomeX, HomeZ;

        public float HomeY;

        /// <summary>Bird positions relative to home (m).</summary>
        public readonly float[] X, Y, Z;

        /// <summary>Bird velocities (m/s).</summary>
        public readonly float[] VX, VY, VZ;

        /// <summary>Heading (rad, clockwise from +Z), pitch (rad, nose up positive) and bank (rad, right wing down positive).</summary>
        public readonly float[] Heading, Pitch, Bank;

        /// <summary>Clip each bird plays and its clip time (s).</summary>
        public readonly FaunaClip[] Clip;

        public readonly float[] ClipTime;

        private readonly byte[] _state;
        private readonly float[] _timer, _gx, _gz, _phase;
        private readonly int[] _cellHead = new int[256];
        private readonly int[] _next;

        // Feeding spots (grain thrown by the square's vendors and visitors): birds crowd round them.
        private readonly float[] _spotX = new float[3], _spotZ = new float[3];
        private FaunaRng _rng;
        private Mode _mode;
        private float _modeTime, _lapAngle, _lapTarget, _circuitDir, _acc, _time;

        /// <summary>Seconds since the last burst (for the presenters' wing sounds); large when the flock is calm.</summary>
        public float SinceBurstS { get; private set; }

        /// <summary>True while any bird is in the air because of a burst.</summary>
        public bool Airborne
        {
            get { return _mode != Mode.Ground; }
        }

        public FlockSim(FlockKind kind, int count, double homeX, double homeZ, float homeY, uint seed)
            : this(kind, count, homeX, homeZ, homeY, seed, 0f, null)
        {
        }

        /// <summary>A flock at home (x, z) on ground height <paramref name="homeY"/>, pecking in a disc of
        /// <paramref name="discRadiusM"/> (≤ 0: the kind's) on the ground <paramref name="habitat"/> allows.</summary>
        public FlockSim(FlockKind kind, int count, double homeX, double homeZ, float homeY, uint seed, float discRadiusM, IFaunaHabitat habitat)
        {
            Kind = kind;
            P = FlockParams.Of(kind);
            DiscRadiusM = discRadiusM > 0f ? discRadiusM : P.DiscRadiusM;
            Habitat = habitat;
            Count = Math.Max(1, Math.Min(MaxBirds, count));
            HomeX = homeX;
            HomeZ = homeZ;
            HomeY = homeY;
            Seed = seed;
            int n = Count;
            X = new float[n];
            Y = new float[n];
            Z = new float[n];
            VX = new float[n];
            VY = new float[n];
            VZ = new float[n];
            Heading = new float[n];
            Pitch = new float[n];
            Bank = new float[n];
            Clip = new FaunaClip[n];
            ClipTime = new float[n];
            _state = new byte[n];
            _timer = new float[n];
            _gx = new float[n];
            _gz = new float[n];
            _phase = new float[n];
            _next = new int[n];
            _rng = new FaunaRng(FaunaRng.Hash(seed, (uint)kind, 0xF10Cu));
            SinceBurstS = 1e4f;
            for (int k = 0; k < _spotX.Length; k++)
            {
                _spotX[k] = _spotZ[k] = 0f;
                for (int t = 0; t < 5; t++)
                {
                    float r = 0.55f * DiscRadiusM * (float)Math.Sqrt(_rng.NextFloat());
                    float a = _rng.Range(0f, FMath.TwoPi);
                    if (!SpotOk(r * FMath.Cos(a), r * FMath.Sin(a))) continue;
                    _spotX[k] = r * FMath.Cos(a);
                    _spotZ[k] = r * FMath.Sin(a);
                    break;
                }
            }
            for (int i = 0; i < n; i++)
            {
                _phase[i] = _rng.Range(0f, 100f);
                Heading[i] = _rng.Range(-FMath.Pi, FMath.Pi);
                if (kind == FlockKind.Kites || kind == FlockKind.Swallows)
                {
                    _state[i] = StSoar;
                    Clip[i] = kind == FlockKind.Kites ? FaunaClip.Soar : FaunaClip.Glide;
                    _gx[i] = _rng.Range(-150f, 150f);
                    _gz[i] = _rng.Range(-150f, 150f);
                    if (kind == FlockKind.Swallows)
                    {
                        _gx[i] *= 0.15f;
                        _gz[i] *= 0.15f;
                    }
                    Kinematic(i, 0f);
                }
                else
                {
                    PickGroundSpot(i);
                    X[i] = _gx[i];
                    Z[i] = _gz[i];
                    _state[i] = StGround;
                    Clip[i] = FaunaClip.Peck;
                    _timer[i] = _rng.Range(0f, 3f);
                }
            }
        }

        /// <summary>
        /// Advances the flock by <paramref name="dt"/> seconds (in fixed 50 ms steps, at most 8 per call). A threat
        /// (the player, a vehicle) at (<paramref name="threatX"/>, <paramref name="threatZ"/>) relative to home
        /// flushes ground birds within the flush radius; <paramref name="hasThreat"/> false means none.
        /// </summary>
        public void Step(float dt, float threatX, float threatZ, bool hasThreat)
        {
            if (!(dt > 0f)) return;
            _acc = Math.Min(_acc + dt, 8 * StepS);
            while (_acc >= StepS)
            {
                _acc -= StepS;
                FixedStep(StepS, threatX, threatZ, hasThreat);
            }
        }

        /// <summary>Forces a burst (a scooter horn, a child running in): every ground bird takes off.</summary>
        public void Startle()
        {
            if (_mode == Mode.Ground) Burst();
        }

        private void FixedStep(float h, float tx, float tz, bool threat)
        {
            _time += h;
            _modeTime += h;
            SinceBurstS += h;
            if (Kind == FlockKind.Kites || Kind == FlockKind.Swallows)
            {
                for (int i = 0; i < Count; i++)
                {
                    Kinematic(i, _time);
                    ClipTime[i] += h;
                }
                return;
            }
            // Flush check.
            if (_mode == Mode.Ground && threat)
            {
                float r2 = P.FlushRadiusM * P.FlushRadiusM;
                for (int i = 0; i < Count; i++)
                {
                    float dx = X[i] - tx, dz = Z[i] - tz;
                    if (dx * dx + dz * dz < r2)
                    {
                        Burst();
                        break;
                    }
                }
            }
            if (_mode != Mode.Ground) Circuit(h);
            BuildGrid();
            int landed = 0;
            for (int i = 0; i < Count; i++)
            {
                switch (_state[i])
                {
                    case StGround:
                        Ground(i, h, tx, tz, threat);
                        landed++;
                        break;
                    case StTakeOff:
                        TakeOff(i, h);
                        break;
                    case StFly:
                        Fly(i, h);
                        break;
                    default:
                        Land(i, h);
                        break;
                }
                ClipTime[i] += h;
            }
            if (_mode == Mode.Return && landed == Count)
            {
                _mode = Mode.Ground;
                _modeTime = 0f;
            }
        }

        private void Burst()
        {
            _mode = Mode.Air;
            _modeTime = 0f;
            SinceBurstS = 0f;
            _lapAngle = 0f;
            int laps = P.LapsMin + _rng.Next(P.LapsMax - P.LapsMin + 1);
            _lapTarget = laps * FMath.TwoPi;
            _circuitDir = _rng.Chance(0.5f) ? 1f : -1f;
            for (int i = 0; i < Count; i++)
            {
                if (_state[i] != StGround) continue;
                _state[i] = StTakeOff;
                // Staggered over 0.4 s, nearest-to-threat first would need sorting; a hash spread looks the same.
                _timer[i] = -0.4f * FaunaRng.Unit(FaunaRng.Hash(Seed, (uint)i, (uint)(_time * 10f)));
                Clip[i] = FaunaClip.TakeOff;
                ClipTime[i] = 0f;
            }
        }

        /// <summary>Advances the circuit target and switches to the return when the laps are flown (20–40 s up).</summary>
        private void Circuit(float h)
        {
            float w = P.SpeedMps / Math.Max(1f, P.CircuitRadiusM);
            _lapAngle += w * h;
            if (_mode == Mode.Air && (_lapAngle >= _lapTarget && _modeTime > 20f || _modeTime > 40f))
            {
                _mode = Mode.Return;
                for (int i = 0; i < Count; i++) PickGroundSpot(i);
            }
        }

        private void CircuitTarget(int i, out float x, out float y, out float z)
        {
            float a = _circuitDir * _lapAngle + 0.03f * (i % 17);
            float r = P.CircuitRadiusM;
            // The circuit runs through the home: centred one radius away, so the flock passes back over the square.
            x = r * (float)Math.Cos(a) - r;
            z = r * (float)Math.Sin(a);
            y = P.CircuitHeightM * (0.85f + 0.3f * FMath.Sin(a * 2f + 0.5f));
        }

        private void Ground(int i, float h, float tx, float tz, bool threat)
        {
            _timer[i] -= h;
            if (_timer[i] <= 0f)
            {
                // New activity: peck (most), walk to a new spot, stand and look.
                float u = _rng.NextFloat();
                if (u < 0.55f)
                {
                    Clip[i] = FaunaClip.Peck;
                    _timer[i] = _rng.Range(1.5f, 4f);
                }
                else if (u < 0.85f)
                {
                    Clip[i] = Kind == FlockKind.Sparrows ? FaunaClip.Hop : FaunaClip.Walk;
                    PickGroundSpot(i);
                    _timer[i] = _rng.Range(2f, 5f);
                }
                else
                {
                    Clip[i] = FaunaClip.Stand;
                    _timer[i] = _rng.Range(1f, 3f);
                }
                ClipTime[i] = 0f;
            }
            float vx = 0f, vz = 0f;
            if (Clip[i] == FaunaClip.Walk || Clip[i] == FaunaClip.Hop)
            {
                float dx = _gx[i] - X[i], dz = _gz[i] - Z[i];
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                FaunaSpeciesInfo info = FaunaCatalog.Info(P.Species);
                float sp = info.WalkMps * (Clip[i] == FaunaClip.Hop ? 1.2f : 1f);
                if (d > 0.05f)
                {
                    vx = dx / d * sp;
                    vz = dz / d * sp;
                    TurnTo(i, (float)Math.Atan2(vx, vz), 6f, h);
                }
                else
                {
                    Clip[i] = FaunaClip.Peck;
                }
            }
            // Keep a little space from neighbours on the ground too.
            Separate(i, 0.6f * Math.Max(0.15f, P.SeparationM), ref vx, ref vz);
            X[i] += vx * h;
            Z[i] += vz * h;
            Y[i] = 0f;
            VX[i] = vx;
            VY[i] = 0f;
            VZ[i] = vz;
            Pitch[i] = 0f;
            Bank[i] = 0f;
        }

        private void TakeOff(int i, float h)
        {
            _timer[i] += h;
            if (_timer[i] < 0f) return; // waiting its turn
            // Leap up and away from the threat-side, climbing steeply with fast strokes.
            float climb = 4.5f, fwd = 3f + 4f * _timer[i];
            float s = FMath.Sin(Heading[i]), c = FMath.Cos(Heading[i]);
            VX[i] = s * fwd;
            VZ[i] = c * fwd;
            VY[i] = climb;
            Integrate(i, h);
            Pitch[i] = 0.6f;
            if (_timer[i] > 0.6f)
            {
                _state[i] = StFly;
                Clip[i] = FaunaClip.Flap;
            }
        }

        private void Fly(int i, float h)
        {
            float tx, ty, tz;
            if (_mode == Mode.Return)
            {
                tx = _gx[i];
                tz = _gz[i];
                float dxz = (float)Math.Sqrt(Sq(tx - X[i]) + Sq(tz - Z[i]));
                ty = Math.Min(P.CircuitHeightM, dxz * 0.35f);
                if (dxz < 4f && Y[i] < 2.5f)
                {
                    _state[i] = StLand;
                    Clip[i] = FaunaClip.Land;
                    ClipTime[i] = 0f;
                    _timer[i] = 0f;
                    return;
                }
            }
            else
            {
                CircuitTarget(i, out tx, out ty, out tz);
            }
            // Boids.
            float sx = 0f, sy = 0f, sz = 0f, ax = 0f, ay = 0f, az = 0f, cx = 0f, cy = 0f, cz = 0f;
            int na = 0, nc = 0;
            float sep2 = P.SeparationM * P.SeparationM, al2 = P.AlignmentM * P.AlignmentM, co2 = P.CohesionM * P.CohesionM;
            int cxi = Cell(X[i]), cyi = Cell(Y[i]), czi = Cell(Z[i]);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int j = _cellHead[CellKey(cxi + ox, cyi + oy, czi + oz)];
                while (j >= 0)
                {
                    if (j != i && _state[j] == StFly)
                    {
                        float dx = X[j] - X[i], dy = Y[j] - Y[i], dz = Z[j] - Z[i];
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 < sep2 && d2 > 1e-6f)
                        {
                            float k = 1f / d2;
                            sx -= dx * k;
                            sy -= dy * k;
                            sz -= dz * k;
                        }
                        if (d2 < al2)
                        {
                            ax += VX[j];
                            ay += VY[j];
                            az += VZ[j];
                            na++;
                        }
                        if (d2 < co2)
                        {
                            cx += dx;
                            cy += dy;
                            cz += dz;
                            nc++;
                        }
                    }
                    j = _next[j];
                }
            }
            float gx = tx - X[i], gy = ty - Y[i], gz = tz - Z[i];
            float gl = (float)Math.Sqrt(gx * gx + gy * gy + gz * gz) + 1e-4f;
            float speed = P.SpeedMps * (0.9f + 0.2f * FMath.Frac(_phase[i] * 0.37f));
            // Desired velocity: towards the target plus the boids terms.
            float dvx = gx / gl * speed, dvy = gy / gl * speed, dvz = gz / gl * speed;
            if (na > 0)
            {
                dvx += 0.35f * (ax / na - VX[i]);
                dvy += 0.35f * (ay / na - VY[i]);
                dvz += 0.35f * (az / na - VZ[i]);
            }
            if (nc > 0)
            {
                dvx += 0.25f * cx / nc;
                dvy += 0.25f * cy / nc;
                dvz += 0.25f * cz / nc;
            }
            dvx += 1.2f * sx;
            dvy += 1.2f * sy;
            dvz += 1.2f * sz;
            // Steer with limited acceleration.
            float maxA = 14f * h;
            float ex = dvx - VX[i], ey = dvy - VY[i], ez = dvz - VZ[i];
            float el = (float)Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (el > maxA)
            {
                ex *= maxA / el;
                ey *= maxA / el;
                ez *= maxA / el;
            }
            float oldHeading = Heading[i];
            VX[i] += ex;
            VY[i] += ey;
            VZ[i] += ez;
            float v = (float)Math.Sqrt(VX[i] * VX[i] + VY[i] * VY[i] + VZ[i] * VZ[i]);
            float vmax = P.SpeedMps * 1.3f;
            if (v > vmax)
            {
                VX[i] *= vmax / v;
                VY[i] *= vmax / v;
                VZ[i] *= vmax / v;
            }
            Integrate(i, h);
            if (Y[i] < 0.3f && _mode != Mode.Return)
            {
                Y[i] = 0.3f;
                VY[i] = Math.Abs(VY[i]);
            }
            float turn = FMath.WrapPi(Heading[i] - oldHeading) / h;
            Bank[i] = FMath.Clamp(FMath.Lerp(Bank[i], turn * 0.35f, 0.2f), -0.7f, 0.7f);
            // Flap while climbing or slow, glide otherwise.
            bool glide = VY[i] < 0.5f && v > P.SpeedMps * 0.85f && FMath.Frac(_phase[i] + _time * 0.15f) > 0.55f;
            FaunaClip want = glide ? FaunaClip.Glide : FaunaClip.Flap;
            if (want != Clip[i])
            {
                Clip[i] = want;
                ClipTime[i] = 0f;
            }
        }

        private void Land(int i, float h)
        {
            _timer[i] += h;
            float dx = _gx[i] - X[i], dz = _gz[i] - Z[i];
            float k = Math.Min(1f, 3f * h);
            VX[i] = FMath.Lerp(VX[i], dx * 2f, k);
            VZ[i] = FMath.Lerp(VZ[i], dz * 2f, k);
            VY[i] = FMath.Lerp(VY[i], -Y[i] * 3f - 0.3f, k);
            Integrate(i, h);
            Pitch[i] = -0.5f;
            Bank[i] *= 0.8f;
            if (Y[i] <= 0.02f || _timer[i] > 2.5f)
            {
                Y[i] = 0f;
                VX[i] = VY[i] = VZ[i] = 0f;
                _state[i] = StGround;
                Clip[i] = FaunaClip.Stand;
                ClipTime[i] = 0f;
                _timer[i] = _rng.Range(0.5f, 2f);
                Pitch[i] = 0f;
                Bank[i] = 0f;
            }
        }

        /// <summary>Kites and swallows: banked circles (kites) and looping low swoops (swallows), closed form.</summary>
        private void Kinematic(int i, float t)
        {
            float ph = _phase[i];
            if (Kind == FlockKind.Kites)
            {
                float r = 20f + 30f * FMath.Frac(ph * 0.618f);
                float v = 8f + 4f * FMath.Frac(ph * 0.31f);
                float dir = FMath.Frac(ph * 0.77f) < 0.5f ? 1f : -1f;
                float w = v / r * dir;
                float a = ph + w * t;
                // Thermal centres drift slowly with the wind.
                float cx = _gx[i] + 12f * FMath.Sin(t * 0.013f + ph), cz = _gz[i] + 12f * FMath.Cos(t * 0.011f + ph);
                X[i] = cx + r * FMath.Cos(a);
                Z[i] = cz + r * FMath.Sin(a);
                Y[i] = 60f + 140f * FMath.Frac(ph * 0.43f) + 6f * FMath.Sin(t * 0.05f + ph);
                VX[i] = -r * w * FMath.Sin(a);
                VZ[i] = r * w * FMath.Cos(a);
                VY[i] = 0.3f * FMath.Cos(t * 0.05f + ph);
                Heading[i] = (float)Math.Atan2(VX[i], VZ[i]);
                float bank = (float)Math.Atan(v * v / (9.81f * r));
                Bank[i] = FMath.Clamp(bank, 15f * FMath.Deg, 30f * FMath.Deg) * dir;
                Pitch[i] = 0f;
                // A few wing beats now and then.
                Clip[i] = FMath.Frac(t / 23f + ph) < 0.08f ? FaunaClip.Flap : FaunaClip.Soar;
            }
            else
            {
                // Swallow: a looping figure over the field, 1–5 m up.
                float s = 0.35f + 0.1f * FMath.Frac(ph * 0.71f);
                float a = t * s + ph;
                float rx = 18f + 10f * FMath.Frac(ph * 0.29f), rz = 12f + 8f * FMath.Frac(ph * 0.53f);
                X[i] = _gx[i] + rx * FMath.Sin(a);
                Z[i] = _gz[i] + rz * FMath.Sin(2f * a + ph);
                Y[i] = 2.5f + 1.8f * FMath.Sin(3f * a + 1.3f * ph);
                VX[i] = rx * s * FMath.Cos(a);
                VZ[i] = 2f * rz * s * FMath.Cos(2f * a + ph);
                VY[i] = 5.4f * s * FMath.Cos(3f * a + 1.3f * ph);
                float old = Heading[i];
                Heading[i] = (float)Math.Atan2(VX[i], VZ[i]);
                Bank[i] = FMath.Clamp(FMath.WrapPi(Heading[i] - old) * 8f, -0.8f, 0.8f);
                Pitch[i] = FMath.Clamp(VY[i] * 0.08f, -0.4f, 0.4f);
                Clip[i] = FMath.Frac(a * 0.5f) < 0.3f ? FaunaClip.Flap : FaunaClip.Glide;
            }
        }

        private void Integrate(int i, float h)
        {
            X[i] += VX[i] * h;
            Y[i] += VY[i] * h;
            Z[i] += VZ[i] * h;
            float hz = VX[i] * VX[i] + VZ[i] * VZ[i];
            if (hz > 0.04f) Heading[i] = (float)Math.Atan2(VX[i], VZ[i]);
            Pitch[i] = FMath.Clamp((float)Math.Atan2(VY[i], Math.Sqrt(hz) + 0.1f), -0.6f, 0.6f);
        }

        private void TurnTo(int i, float target, float rate, float h)
        {
            float d = FMath.WrapPi(target - Heading[i]);
            float m = rate * h;
            Heading[i] = FMath.WrapPi(Heading[i] + FMath.Clamp(d, -m, m));
        }

        private void Separate(int i, float radius, ref float vx, ref float vz)
        {
            if (radius <= 0f) return;
            float r2 = radius * radius;
            int cxi = Cell(X[i]), cyi = Cell(Y[i]), czi = Cell(Z[i]);
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int j = _cellHead[CellKey(cxi + ox, cyi, czi + oz)];
                while (j >= 0)
                {
                    if (j != i && _state[j] == StGround)
                    {
                        float dx = X[i] - X[j], dz = Z[i] - Z[j];
                        float d2 = dx * dx + dz * dz;
                        if (d2 < r2 && d2 > 1e-6f)
                        {
                            float k = (radius - (float)Math.Sqrt(d2)) * 1.5f / (float)Math.Sqrt(d2);
                            vx += dx * k;
                            vz += dz * k;
                        }
                    }
                    j = _next[j];
                }
            }
        }

        private void PickGroundSpot(int i)
        {
            // Two thirds crowd round a feeding spot, the rest wander the whole disc; only where a bird can stand.
            for (int t = 0; t < 5; t++)
            {
                float u = _rng.NextFloat();
                float r, a = _rng.Range(0f, FMath.TwoPi), x, z;
                if (u < 0.67f)
                {
                    int k = _rng.Next(_spotX.Length);
                    r = 0.35f * DiscRadiusM * (float)Math.Sqrt(_rng.NextFloat());
                    x = _spotX[k] + r * FMath.Cos(a);
                    z = _spotZ[k] + r * FMath.Sin(a);
                }
                else
                {
                    r = DiscRadiusM * (float)Math.Sqrt(_rng.NextFloat());
                    x = r * FMath.Cos(a);
                    z = r * FMath.Sin(a);
                }
                if (!SpotOk(x, z)) continue;
                _gx[i] = x;
                _gz[i] = z;
                return;
            }
            // Nothing suitable nearby: the feeding spot by home (the planner checked home).
            int s = i % _spotX.Length;
            _gx[i] = _spotX[s];
            _gz[i] = _spotZ[s];
        }

        /// <summary>True when a bird may stand at (x, z) relative to home (unknown ground counts as fine).</summary>
        private bool SpotOk(float x, float z)
        {
            if (Habitat == null) return true;
            FaunaGround g = Habitat.GroundAt(HomeX + x, HomeZ + z);
            if (g == FaunaGround.None) return true;
            if ((g & (FaunaGround.Building | FaunaGround.Road)) != 0) return false;
            return Kind == FlockKind.Egrets || (g & FaunaGround.Water) == 0;
        }

        private float CellSize
        {
            get { return Math.Max(2f, P.CohesionM); }
        }

        private int Cell(float v)
        {
            return (int)Math.Floor(v / CellSize);
        }

        private static int CellKey(int x, int y, int z)
        {
            unchecked
            {
                return (int)((uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791) & 255u);
            }
        }

        private void BuildGrid()
        {
            for (int k = 0; k < _cellHead.Length; k++) _cellHead[k] = -1;
            for (int i = 0; i < Count; i++)
            {
                int key = CellKey(Cell(X[i]), Cell(Y[i]), Cell(Z[i]));
                _next[i] = _cellHead[key];
                _cellHead[key] = i;
            }
        }

        private static float Sq(float v)
        {
            return v * v;
        }
    }
}
