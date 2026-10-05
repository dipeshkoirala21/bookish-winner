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
        }

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
            a.Heading = hd + (a.Clip == (byte)AnimalClip.Walk && Math.Cos((_time + a.Phase) * 0.08) < 0 ? (float)Math.PI : 0f);
            a.ClipTime = (_time + a.Phase) % 4f;
            _animals.Add(a);
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
                        ClipTime = a.ClipTime, AgentId = a.Id,
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
