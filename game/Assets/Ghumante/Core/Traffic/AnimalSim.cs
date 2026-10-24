using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Traffic
{
    /// <summary>Per-tier animal settings (W2_DESIGN 5.5 render caps are applied by the presenter).</summary>
    public sealed class AnimalSettings
    {
        /// <summary>Simulated animals at most (nearest first).</summary>
        public int MaxAnimals = 40;

        /// <summary>Animals exist within this distance of the focus.</summary>
        public float RadiusM = 250f;

        public static AnimalSettings Low()
        {
            return new AnimalSettings { MaxAnimals = 16, RadiusM = 150f };
        }

        public static AnimalSettings Mid()
        {
            return new AnimalSettings { MaxAnimals = 40, RadiusM = 250f };
        }

        public static AnimalSettings High()
        {
            return new AnimalSettings { MaxAnimals = 80, RadiusM = 400f };
        }
    }

    /// <summary>
    /// Street animals of stage 1 (W2_DESIGN 5.5): cows (humped zebu) and roaming dogs, placed deterministically in 25 m
    /// slots along the roads so the same cow lies at the same place whenever the player comes back. Densities per km of
    /// road: cows 2 on the Ring Road and trunks, 0.6 on arterials, 0.3 urban, 0.15 at old-core edges, 2 (tethered) in
    /// peri-urban villages; dogs 14.2 per km of street (old core 15, urban 14, peri-urban 11, villages 7). Cows lie
    /// chewing 60% of the time, stand 30%, walk slowly 10%; dogs sleep 60–70% of the day and are 70% awake at night.
    /// Animals on or beside the carriageway are pushed into the <see cref="TrafficSim"/> as obstacles, so every vehicle
    /// slows, steers round them or stops (no honking at animals). Goats, chickens, buffalo, ducks, macaques and the bird
    /// flocks arrive in stage 2 (<see cref="CopyFlocks"/> returns none yet). Deterministic per seed.
    /// </summary>
    public sealed class AnimalSim
    {
        public const float SlotM = 25f;
        public const float CowRadiusM = 1.2f, DogRadiusM = 0.5f;

        /// <summary>Obstacle ids handed to the traffic sim start here (keeps them apart from pedestrians and the player).</summary>
        public const int ObstacleIdBase = 0x20000000;

        private readonly LaneGraph _g;
        private readonly AnimalSettings _s;
        private readonly ulong _seed;
        private double _fx, _fz;
        private bool _hasFocus;
        private float _time;
        private readonly object _inputLock = new object();
        private double _pfx, _pfz;
        private bool _pHas;

        private struct Animal
        {
            public int Id;
            public AnimalKind Kind;
            public int Lane;
            public float S0, Lat;
            public double X, Z;
            public float Y, Heading, Speed;
            public byte Tint, Clip;
            public float ClipTime, Phase;
            public bool OnRoad;
            public double Dist;

            /// <summary>The lane point the animal is placed beside and the lateral direction there (for
            /// <see cref="AnimalSim.Ground"/>).</summary>
            public double BX, BZ, RX, RZ;

            public float BY, LaneW;

            /// <summary>Pose on the ground (degrees, Unity Euler x and z; see <see cref="AnimalPose.PitchDeg"/>).</summary>
            public float Pitch, Roll;
        }

        /// <summary>
        /// Optional (cut/fill package, owner playtest: "the dog looks like it is sleeping in the air"): the surface the
        /// animals stand on. With it each animal stands exactly on the drawn road surface under it (the carriageway, a
        /// footpath top, a shoulder, a junction cap: the same at every tier), and one placed beside the kerb where no road
        /// surface is drawn (a cutting, an embankment, a verge) is moved back toward its lane, 0.5 m at a time, until it lies
        /// on the road's edge. Without it every animal takes its lane's height.
        /// </summary>
        public Meshing.IStandSurface Ground;

        /// <summary>Most the ground under an animal may differ from its lane's surface (a kerb, a shoulder's fall).</summary>
        public const float SettleMaxM = 0.6f;

        private struct Settled
        {
            public int Lane;
            public float S, Lat, OutLat, Y;

            /// <summary>Rise of the ground per metre along the lane and to its right under the body.</summary>
            public float Along, Right;

            /// <summary>A drawn road surface carries the whole body somewhere across its lane.</summary>
            public bool Ok;
        }

        private readonly Dictionary<int, Settled> _settled = new Dictionary<int, Settled>();

        private readonly List<Animal> _animals = new List<Animal>();
        private readonly Dictionary<int, Animal> _byId = new Dictionary<int, Animal>();
        private readonly HashSet<int> _pushed = new HashSet<int>();
        private readonly HashSet<int> _keep = new HashSet<int>();
        private readonly List<int> _near = new List<int>();
        private readonly object _snapLock = new object();
        private AnimalPose[] _front = new AnimalPose[0];
        private int _frontCount;

        /// <summary>Optional: obstacles for the animals on or beside the carriageway go here.</summary>
        public TrafficSim Traffic;

        public AnimalSim(LaneGraph g, AnimalSettings s, ulong seed)
        {
            _g = g ?? throw new ArgumentNullException(nameof(g));
            _s = s ?? new AnimalSettings();
            _seed = seed;
        }

        public void SetFocus(double x, double z, float radiusM)
        {
            lock (_inputLock)
            {
                _pfx = x;
                _pfz = z;
                _pHas = true;
                if (radiusM > 0f) _s.RadiusM = radiusM;
            }
        }

        public int Count
        {
            get
            {
                lock (_snapLock) return _frontCount;
            }
        }

        /// <summary>Cows per km of road by class and area.</summary>
        public static float CowsPerKm(RoadClass c, AreaType a)
        {
            if (a == AreaType.OldCore) return 0.15f;
            if (a == AreaType.PeriUrban || a == AreaType.Rural) return c <= RoadClass.Tertiary ? 1.0f : 2.0f;
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                    return 2.0f;
                case RoadClass.Primary:
                case RoadClass.Secondary:
                    return 0.6f;
                default:
                    return 0.3f;
            }
        }

        /// <summary>Roaming dogs per km of street by area (ICAM 14.2 per km on average).</summary>
        public static float DogsPerKm(AreaType a)
        {
            switch (a)
            {
                case AreaType.OldCore: return 15f;
                case AreaType.Urban: return 14f;
                case AreaType.PeriUrban: return 11f;
                case AreaType.Rural:
                case AreaType.Hill:
                    return 7f;
                default: return 14.2f;
            }
        }

        public void Step(float dt, float gameHour, float wetness01)
        {
            if (!(dt > 0f)) return;
            _time += dt;
            lock (_inputLock)
            {
                if (_pHas)
                {
                    _fx = _pfx;
                    _fz = _pfz;
                    _hasFocus = true;
                }
            }
            if (!_hasFocus) return;
            float h = ((gameHour % 24f) + 24f) % 24f;
            bool night = h >= 20f || h < 6f;
            _animals.Clear();
            lock (_g.SyncRoot)
            {
                _near.Clear();
                _g.LanesNear(_fx, _fz, _s.RadiusM, _near);
                _near.Sort();
                foreach (int id in _near)
                {
                    LaneGraph.Lane l = _g.Lanes[id];
                    // One placement per road: the forward (or shared forward) kerb lane.
                    if (l.Kind != LaneKind.Road || l.Index != 0 || !l.Forward && l.TwoWay) continue;
                    if (l.Length < SlotM) continue;
                    int slots = (int)(l.Length / SlotM);
                    float cowP = CowsPerKm(l.Class, l.Area) * SlotM / 1000f;
                    float dogP = DogsPerKm(l.Area) * SlotM / 1000f;
                    for (int k = 0; k < slots; k++)
                    {
                        float s = (k + 0.5f) * SlotM;
                        double x, z;
                        float y, hd;
                        LaneGraph.PointAt(l, s, out x, out z, out y, out hd);
                        double dx = x - _fx, dz = z - _fz;
                        double d = Math.Sqrt(dx * dx + dz * dz);
                        if (d > _s.RadiusM) continue;
                        // Stable slot key: the way and the slot point on a 5 m grid.
                        ulong key = SimRng.Mix((ulong)l.WayId, (ulong)(long)Math.Round(x / 5.0) * 1000003UL ^ (ulong)(long)Math.Round(z / 5.0));
                        var r = new SimRng(SimRng.Mix(_seed, key));
                        if (r.Chance(cowP)) Add(AnimalKind.Cow, l, s, d, ref r, night);
                        if (r.Chance(dogP)) Add(AnimalKind.Dog, l, Math.Min(l.Length - 1f, s + r.Range(-10f, 10f)), d, ref r, night);
                    }
                }
            }
            // Nearest first, capped.
            _animals.Sort((a, b) => a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Id.CompareTo(b.Id));
            if (_animals.Count > _s.MaxAnimals) _animals.RemoveRange(_s.MaxAnimals, _animals.Count - _s.MaxAnimals);
            if (Ground != null) SettleAll();
            PushObstacles();
            Publish();
        }

        private void Add(AnimalKind kind, LaneGraph.Lane l, float s, double d, ref SimRng r, bool night)
        {
            var a = new Animal { Kind = kind, Lane = l.Id, S0 = s, Dist = d };
            a.Id = (int)(r.NextU32() & 0x0FFFFFFF);
            // The kerb side of the kerb lane is its left (negative lateral, left-hand traffic).
            float edge = -0.5f * l.Width;
            if (kind == AnimalKind.Cow)
            {
                // On the carriageway near the kerb (70%) or just off it (30%).
                a.OnRoad = r.Chance(0.7f);
                a.Lat = a.OnRoad ? edge + 0.9f + r.Range(0f, 0.6f) : edge - 1.2f - r.Range(0f, 1.0f);
                a.Tint = (byte)r.Next(5);
                a.Phase = r.Range(0f, 600f);
            }
            else
            {
                a.OnRoad = r.Chance(0.2f);
                a.Lat = a.OnRoad ? edge + 0.5f : edge - 0.8f - r.Range(0f, 1.0f);
                a.Tint = (byte)r.Next(5);
                a.Phase = r.Range(0f, 600f);
            }
            // Behaviour from the time bucket (minutes): deterministic per animal.
            int bucket = (int)((_time + a.Phase) / 60f);
            var b = new SimRng(SimRng.Mix((ulong)a.Id, (ulong)bucket));
            float u = b.NextFloat();
            float walkOff = 0f;
            if (kind == AnimalKind.Cow)
            {
                if (u < 0.6f) a.Clip = (byte)AnimalClip.Lie;
                else if (u < 0.9f) a.Clip = (byte)AnimalClip.Stand;
                else
                {
                    a.Clip = (byte)AnimalClip.Walk;
                    a.Speed = 0.4f;
                    walkOff = 5f * (float)Math.Sin((_time + a.Phase) * 0.08);
                }
            }
            else
            {
                float asleep = night ? 0.3f : 0.65f;
                if (u < asleep) a.Clip = (byte)AnimalClip.Sleep;
                else if (u < asleep + 0.2f) a.Clip = (byte)AnimalClip.Sit;
                else
                {
                    a.Clip = (byte)AnimalClip.Walk;
                    a.Speed = 0.8f;
                    walkOff = 8f * (float)Math.Sin((_time + a.Phase) * 0.1);
                }
            }
            float s1 = Math.Max(0.5f, Math.Min(l.Length - 0.5f, s + walkOff));
            double x, z;
            float y, hd;
            LaneGraph.PointAt(l, s1, out x, out z, out y, out hd);
            double rx = Math.Cos(hd), rz = -Math.Sin(hd);
            a.X = x + rx * a.Lat;
            a.Z = z + rz * a.Lat;
            a.Y = y;
            a.BX = x;
            a.BZ = z;
            a.BY = y;
            a.RX = rx;
            a.RZ = rz;
            a.S0 = s1;
            a.LaneW = l.Width;
            a.Heading = hd + (a.Clip == (byte)AnimalClip.Walk && Math.Cos((_time + a.Phase) * 0.08) < 0 ? (float)Math.PI : 0f);
            a.ClipTime = (_time + a.Phase) % 4f;
            _animals.Add(a);
        }

        /// <summary>Stands every kept animal on <see cref="Ground"/> (cached per animal while its lane point and lateral
        /// do not change: lying and sitting animals cost one lookup).</summary>
        private void SettleAll()
        {
            if (_settled.Count > 8 * Math.Max(16, _s.MaxAnimals)) _settled.Clear();
            int kept = 0;
            for (int i = 0; i < _animals.Count; i++)
            {
                Animal a = _animals[i];
                Settled c;
                if (!_settled.TryGetValue(a.Id, out c) || c.Lane != a.Lane || c.S != a.S0 || c.Lat != a.Lat)
                {
                    c = new Settled { Lane = a.Lane, S = a.S0, Lat = a.Lat };
                    c.Ok = Settle(a, out c.OutLat, out c.Y, out c.Along, out c.Right);
                    _settled[a.Id] = c;
                }
                // Nowhere across its lane does a drawn road surface carry the whole body (the lane leaves the drawn road
                // here, over a cutting or an embankment): the animal is not there rather than hanging over an edge.
                if (!c.Ok) continue;
                a.X = a.BX + a.RX * c.OutLat;
                a.Z = a.BZ + a.RZ * c.OutLat;
                a.Y = c.Y;
                // Lay the body on the ground: the lane runs along (-RZ, RX); an animal facing the other way sees both
                // slopes reversed.
                double hx = Math.Sin(a.Heading), hz = Math.Cos(a.Heading);
                float dir = -a.RZ * hx + a.RX * hz >= 0 ? 1f : -1f;
                a.Pitch = (float)(-Math.Atan(dir * c.Along) * 180.0 / Math.PI);
                a.Roll = (float)(Math.Atan(dir * c.Right) * 180.0 / Math.PI);
                _animals[kept++] = a;
            }
            if (kept < _animals.Count) _animals.RemoveRange(kept, _animals.Count - kept);
        }

        /// <summary>Step of the lateral search for a place to stand.</summary>
        private const float SettleStepM = 0.25f;

        /// <summary>
        /// The lateral an animal ends up at and the height of the surface under it: its own lateral when a drawn road
        /// surface carries its whole body there (<see cref="StandsLevel"/>) within <see cref="SettleMaxM"/> of its lane's
        /// surface, else the first such lateral stepping in toward its lane and across it (<see cref="SettleStepM"/> at a
        /// time) up to the lane's far edge. False when there is none (the animal is then not placed).
        /// </summary>
        private bool Settle(in Animal a, out float lat, out float y, out float along, out float right)
        {
            lat = a.Lat;
            y = a.BY;
            along = right = 0f;
            // The body's footprint (half length along the lane, half width across it), lying or standing.
            bool cow = a.Kind == AnimalKind.Cow;
            float hl = cow ? 0.85f : 0.45f, hw = cow ? 0.4f : 0.2f;
            float far = 0.5f * a.LaneW - hw;
            double fx = -a.RZ, fz = a.RX; // along the lane (the lateral is its right)
            for (float t = a.Lat; t <= Math.Max(a.Lat, far) + 1e-4f; t += SettleStepM)
            {
                float gy;
                float sa, sr;
                if (!StandsLevel(a.BX + a.RX * t, a.BZ + a.RZ * t, a.BY, fx, fz, a.RX, a.RZ, hl, hw, out gy, out sa, out sr)) continue;
                lat = t;
                y = gy;
                along = sa;
                right = sr;
                return true;
            }
            return false;
        }

        /// <summary>Most a body's footprint may depart from one plane (a kerb, a cap edge or a drop under it does more).</summary>
        private const float FootprintFlatM = 0.03f;

        /// <summary>
        /// True when a drawn road surface lies under the centre and the four corners of a body's footprint at (x, z), on one
        /// plane within <see cref="FootprintFlatM"/> (no kerb, cap edge or drop under it; a street's grade and camber are
        /// fine: the body lies on them), falling at most 30 % along and 15 % across, within <see cref="SettleMaxM"/> of
        /// <paramref name="nearY"/>. <paramref name="y"/> is the surface under the centre, <paramref name="along"/> and
        /// <paramref name="right"/> its rise per metre along (fx, fz) and toward (rx, rz).
        /// </summary>
        private bool StandsLevel(double x, double z, float nearY, double fx, double fz, double rx, double rz, float hl, float hw, out float y, out float along,
                                 out float right)
        {
            along = right = 0f;
            bool road;
            if (!Ground.TryStand(x, z, nearY, out y, out road) || !road || Math.Abs(y - nearY) > SettleMaxM) return false;
            // Corners: (-,-), (+,-), (-,+), (+,+) in (along, right).
            float h0 = 0f, h1 = 0f, h2 = 0f, h3 = 0f;
            for (int c = 0; c < 4; c++)
            {
                double a = (c & 1) != 0 ? hl : -hl, b = (c & 2) != 0 ? hw : -hw;
                float cy;
                if (!Ground.TryStand(x + fx * a + rx * b, z + fz * a + rz * b, y, out cy, out road) || !road || Math.Abs(cy - y) > 0.5f) return false;
                if (c == 0) h0 = cy;
                else if (c == 1) h1 = cy;
                else if (c == 2) h2 = cy;
                else h3 = cy;
            }
            float twist = 0.25f * (h3 - h2 - h1 + h0), mid = 0.25f * (h0 + h1 + h2 + h3);
            along = (h1 + h3 - h0 - h2) / (4f * hl);
            right = (h2 + h3 - h0 - h1) / (4f * hw);
            if (Math.Abs(twist) > FootprintFlatM || Math.Abs(mid - y) > FootprintFlatM || Math.Abs(along) > 0.3f || Math.Abs(right) > 0.15f) return false;
            return true;
        }

        private void PushObstacles()
        {
            TrafficSim t = Traffic;
            if (t == null) return;
            HashSet<int> keep = _keep;
            keep.Clear();
            foreach (Animal a in _animals)
            {
                int id = ObstacleIdBase + (a.Id & 0x0FFFFFFF);
                keep.Add(id);
                t.AddObstacle(id, a.X, a.Z, a.Kind == AnimalKind.Cow ? CowRadiusM : DogRadiusM, a.Kind == AnimalKind.Cow ? ObstacleKind.Cow : ObstacleKind.Dog);
            }
            foreach (int id in _pushed)
                if (!keep.Contains(id))
                    t.RemoveObstacle(id);
            _pushed.Clear();
            foreach (int id in keep) _pushed.Add(id);
        }

        private void Publish()
        {
            lock (_snapLock)
            {
                if (_front.Length < _animals.Count) _front = new AnimalPose[_animals.Count * 2];
                for (int i = 0; i < _animals.Count; i++)
                {
                    Animal a = _animals[i];
                    _front[i] = new AnimalPose
                    {
                        X = a.X, Z = a.Z, Y = a.Y, HeadingRad = a.Heading, SpeedMps = a.Speed, Kind = a.Kind, Tint = a.Tint, ClipId = a.Clip,
                        ClipTime = a.ClipTime, AgentId = a.Id, PitchDeg = a.Pitch, RollDeg = a.Roll,
                    };
                }
                _frontCount = _animals.Count;
            }
        }

        /// <summary>Copies the animals of the last step (nearest first) and returns the count. Thread-safe.</summary>
        public int CopyPoses(AnimalPose[] dst)
        {
            lock (_snapLock)
            {
                int n = Math.Min(dst.Length, _frontCount);
                Array.Copy(_front, dst, n);
                return n;
            }
        }

        /// <summary>Bird flocks (stage 2): none yet.</summary>
        public int CopyFlocks(FlockState[] dst)
        {
            return 0;
        }

        /// <summary>Distance from (x, z) to the nearest cow body (centre distance minus its radius), for the player's
        /// cushion (<see cref="ContactRules.PlayerSpeedCapMps"/>).</summary>
        public bool TryNearestCow(double x, double z, out float distanceM)
        {
            distanceM = float.PositiveInfinity;
            lock (_snapLock)
            {
                for (int i = 0; i < _frontCount; i++)
                {
                    if (_front[i].Kind != AnimalKind.Cow) continue;
                    double dx = _front[i].X - x, dz = _front[i].Z - z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz) - CowRadiusM;
                    if (d < distanceM) distanceM = d;
                }
            }
            return !float.IsInfinity(distanceM);
        }
    }
}
