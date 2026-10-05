using System;
using System.Collections.Generic;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Aviation
{
    /// <summary>
    /// Air traffic at TIA (W2_DESIGN 8.2–8.4): the real-hour schedule (the game hour picks the table row, the rate applies
    /// per real second: λ = rate / 3600 × month × weather × busy), runway slots with 100 s separation (120 s after a
    /// wide-body) and 60 s between helicopters on the apron, and kinematic flights on the stage-1 procedures: arrivals on
    /// A1 straight in to runway 02 with rollout and taxi-in, departures backtracking ~1.2 km to the 02 threshold, rolling
    /// and climbing out on D1 (west) or D2 (east), helicopters lifting off the domestic apron along H-E (and arriving
    /// along it reversed). Runway 20, A2–A5, D3–D4, H-N, H-W, holds and go-arounds arrive in stage 2.
    /// <para>Counts are exact: each class's expected movements Λ(t) = ∫λ dt are drawn at the midpoints
    /// k − 0.5 (so a day gives round(Λ) movements), each then shifted by a seeded jitter of up to 40% of its interval so
    /// the stream does not look mechanical. Deterministic per region seed. Kinematic only: microseconds per aircraft.</para>
    /// </summary>
    public sealed class AirTrafficSim
    {
        public const float TaxiMps = 7f, BacktrackMps = 10f, KtToMps = 0.514444f;
        private const float Gravity = 9.81f;

        private readonly AviationConfig _c;
        private readonly ulong _seed;
        private double _clock;
        private readonly double[] _lambda = new double[4];
        private readonly long[] _emitted = new long[4];
        private double _fogBacklog;
        private int _nextId = 1;
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<KeyValuePair<double, float>> _runway = new List<KeyValuePair<double, float>>(); // slot, separation after
        private readonly List<double> _heliSlots = new List<double>();
        private readonly List<Movement> _log = new List<Movement>();
        private readonly int[] _totals = new int[4];
        private readonly Dictionary<string, FlightPath> _paths = new Dictionary<string, FlightPath>(StringComparer.Ordinal);
        private readonly object _snapLock = new object();
        private AircraftState[] _front = new AircraftState[0];
        private int _frontCount;

        /// <summary>Busy-airport setting: × 1 to × <see cref="AviationConfig.BusyMax"/>.</summary>
        public float BusyFactor = 1f;

        /// <summary>Keep the movement log (tests, debug); off in the game to avoid growth.</summary>
        public bool KeepLog;

        private struct Pending
        {
            public double SpawnAt;
            public Movement M;
        }

        private sealed class Flight
        {
            public int Id;
            public AircraftClass Class;
            public byte Livery;
            public bool Arrival, Heli;
            public FlightPhase Phase;
            public FlightPath Path;
            public float S, Speed, PhaseT;
            public double X, Z;
            public float Y, Heading, Pitch, Bank, Gear, Rpm, PrevHeading;
            public double GroundFromX, GroundFromZ, GroundToX, GroundToZ;
            public float GroundLen, GroundS;
            public float Vapp, Vrot, Roll, TakeoffRoll;
            public bool Done;
            public float AirborneT;
        }

        public AirTrafficSim(AviationConfig c, ulong regionSeed)
        {
            _c = c ?? throw new ArgumentNullException(nameof(c));
            _seed = regionSeed;
        }

        /// <summary>Real seconds simulated so far.</summary>
        public double Clock
        {
            get { return _clock; }
        }

        /// <summary>Movements drawn so far per schedule class.</summary>
        public int Total(ScheduleClass c)
        {
            return _totals[(int)c];
        }

        /// <summary>Expected movements so far per class (∫λ dt).</summary>
        public double Expected(ScheduleClass c)
        {
            return _lambda[(int)c];
        }

        /// <summary>The movement log (only with <see cref="KeepLog"/>).</summary>
        public IReadOnlyList<Movement> Log
        {
            get { return _log; }
        }

        /// <summary>Live flights (airborne, rolling, taxiing or parked briefly).</summary>
        public int FlightCount
        {
            get { return _flights.Count; }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Schedule
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The weather and month multiplier of a class at a game hour (W2_DESIGN 8.2).</summary>
        public float Factor(ScheduleClass c, int hour, int month, WeatherKind w)
        {
            float f = month >= 1 && month <= 12 ? _c.MonthFactor[month] : 1f;
            if (f <= 0f) f = 1f;
            bool domHeli = c == ScheduleClass.DomesticTurboprop || c == ScheduleClass.Helicopter;
            if (w == WeatherKind.Fog && domHeli && hour < _c.FogDomesticUntilHour) return 0f;
            if (w == WeatherKind.Storm && hour >= _c.StormFromHour && hour < _c.StormToHour && Array.IndexOf(_c.StormMonths, month) >= 0)
            {
                if (c == ScheduleClass.DomesticTurboprop) f *= _c.StormDomestic;
                else if (c == ScheduleClass.Helicopter) f *= _c.StormHeli;
            }
            return f * Math.Max(1f, Math.Min(_c.BusyMax, BusyFactor));
        }

        /// <summary>
        /// Advances by <paramref name="realSeconds"/> of real time at game hour <paramref name="gameHour"/> (its whole hour
        /// picks the schedule row), month 1–12 and weather. Long steps are split internally (≤ 1 s).
        /// </summary>
        public void Step(double realSeconds, float gameHour, int month, WeatherKind w)
        {
            if (!(realSeconds > 0)) return;
            int hour = (int)Math.Floor(((gameHour % 24f) + 24f) % 24f);
            while (realSeconds > 0)
            {
                double dt = Math.Min(1.0, realSeconds);
                realSeconds -= dt;
                _clock += dt;
                Schedule(dt, hour, month, w);
                SpawnDue();
                for (int i = 0; i < _flights.Count; i++) Fly(_flights[i], (float)dt);
                _flights.RemoveAll(f => f.Done);
            }
            Publish();
        }

        private void Schedule(double dt, int hour, int month, WeatherKind w)
        {
            for (int ci = 0; ci < 4; ci++)
            {
                var c = (ScheduleClass)ci;
                double rate = _c.RateAt(hour, c) / 3600.0;
                float f = Factor(c, hour, month, w);
                double add = rate * f * dt;
                if (f == 0f && rate > 0)
                {
                    // Fog: the domestic and helicopter movements wait and go once it lifts.
                    _fogBacklog += rate * (month >= 1 && month <= 12 ? _c.MonthFactor[month] : 1f) * dt;
                }
                else if (_fogBacklog > 0 && w == WeatherKind.Fog && hour < _c.FogBacklogUntilHour && ci == 0)
                {
                    double room = Math.Max(0.0, _c.FogMaxPerHour / 3600.0 - rate * f) * dt;
                    double take = Math.Min(room, _fogBacklog);
                    _fogBacklog -= take;
                    add += take;
                }
                _lambda[ci] += add;
                while (_lambda[ci] >= _emitted[ci] + 0.5)
                {
                    _emitted[ci]++;
                    Emit(c, _emitted[ci], rate * Math.Max(f, 0.05f), hour);
                }
            }
        }

        private void Emit(ScheduleClass c, long k, double lambdaPerS, int hour)
        {
            var r = new SimRng(SimRng.Mix(_seed, (ulong)c * 0x9E37UL + 1, (ulong)k));
            double interval = lambdaPerS > 1e-9 ? 1.0 / lambdaPerS : 600.0;
            double jitter = r.NextDouble() * 0.4 * Math.Min(interval, 600.0);
            var m = new Movement { Class = c, EmittedAt = _clock };
            switch (c)
            {
                case ScheduleClass.Helicopter:
                {
                    float p = hour < 10 ? _c.HeliArrivalBefore10 : hour < 12 ? _c.HeliArrival10To12 : _c.HeliArrivalAfter12;
                    m.Arrival = r.Chance(p);
                    m.Aircraft = AircraftClass.Helicopter;
                    m.Procedure = "H-E";
                    m.Runway = "apron";
                    break;
                }
                case ScheduleClass.DomesticTurboprop:
                {
                    m.Arrival = r.Chance(_c.ArrivalShareDomestic);
                    float u = r.NextFloat() * (_c.TurbopropShare + _c.StretchShare + _c.StolShare);
                    m.Aircraft = u < _c.TurbopropShare ? AircraftClass.Turboprop :
                        u < _c.TurbopropShare + _c.StretchShare ? AircraftClass.TurbopropStretch : AircraftClass.Stol;
                    m.Procedure = m.Arrival ? "A1" : r.Chance(_c.DomesticWestShare) ? "D1" : "D2";
                    m.Runway = "02";
                    break;
                }
                default:
                {
                    m.Arrival = r.Chance(_c.ArrivalShareIntl);
                    m.Aircraft = c == ScheduleClass.InternationalWidebody ? AircraftClass.Widebody : AircraftClass.Narrowbody;
                    m.Procedure = m.Arrival ? "A1" : r.Chance(_c.IntlWestShare) ? "D1" : "D2";
                    m.Runway = "02";
                    break;
                }
            }
            _totals[(int)c]++;
            // Runway (or apron) slot after the lead time; the aircraft appears so as to arrive exactly at it.
            float lead = LeadTime(m);
            double desired = _clock + jitter + lead;
            double slot = m.Aircraft == AircraftClass.Helicopter ? HeliSlot(desired) : RunwaySlot(desired, m.Aircraft);
            m.SlotAt = slot;
            m.AircraftId = _nextId++;
            _pending.Add(new Pending { SpawnAt = slot - lead, M = m });
            if (KeepLog) _log.Add(m);
        }

        /// <summary>Separation after a runway movement of this class.</summary>
        public float SeparationAfter(AircraftClass a)
        {
            return a == AircraftClass.Widebody ? _c.SepAfterWidebodyS : a == AircraftClass.Narrowbody ? _c.SepAfterNarrowbodyS : _c.SepAfterTurbopropS;
        }

        private double RunwaySlot(double desired, AircraftClass a)
        {
            _runway.RemoveAll(kv => kv.Key < _clock - 900.0);
            double t = desired;
            float mine = SeparationAfter(a);
            for (int guard = 0; guard < 64; guard++)
            {
                bool moved = false;
                foreach (var kv in _runway)
                {
                    if (kv.Key <= t && t - kv.Key < kv.Value)
                    {
                        t = kv.Key + kv.Value;
                        moved = true;
                    }
                    else if (kv.Key > t && kv.Key - t < mine)
                    {
                        t = kv.Key + kv.Value;
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            _runway.Add(new KeyValuePair<double, float>(t, mine));
            _runway.Sort((p, q) => p.Key.CompareTo(q.Key));
            return t;
        }

        private double HeliSlot(double desired)
        {
            _heliSlots.RemoveAll(s => s < _clock - 900.0);
            double t = desired;
            for (int guard = 0; guard < 64; guard++)
            {
                bool moved = false;
                foreach (double s in _heliSlots)
                    if (Math.Abs(s - t) < _c.SepHeliApronS)
                    {
                        t = s + _c.SepHeliApronS;
                        moved = true;
                    }
                if (!moved) break;
            }
            _heliSlots.Add(t);
            return t;
        }

        /// <summary>Runway slots reserved so far (start of take-off roll or touchdown), for the separation check.</summary>
        public void RunwaySlots(List<double> dst)
        {
            dst.Clear();
            foreach (Movement m in _log)
                if (m.Aircraft != AircraftClass.Helicopter)
                    dst.Add(m.SlotAt);
            dst.Sort();
        }

        // ---------------------------------------------------------------------------------------------------------
        // Flights
        // ---------------------------------------------------------------------------------------------------------

        private static float Vapp(AircraftClass a)
        {
            switch (a)
            {
                case AircraftClass.Narrowbody: return 140f * KtToMps;
                case AircraftClass.Widebody: return 145f * KtToMps;
                case AircraftClass.Stol: return 80f * KtToMps;
                case AircraftClass.Helicopter: return 60f * KtToMps;
                default: return 115f * KtToMps;
            }
        }

        private static bool Jet(AircraftClass a)
        {
            return a == AircraftClass.Narrowbody || a == AircraftClass.Widebody;
        }

        /// <summary>The path a procedure flies (cached): arrivals end at the touchdown zone, departures start at the lift-off
        /// point set to the runway elevation; helicopter arrivals fly the corridor reversed.</summary>
        public FlightPath PathOf(string procedure, bool arrival, bool heli)
        {
            string key = procedure + (arrival ? ">" : "<");
            FlightPath p;
            if (_paths.TryGetValue(key, out p)) return p;
            Procedure pr;
            if (!_c.Procedures.TryGetValue(procedure, out pr) || pr.Points.Length < 2) return null;
            ProcedurePoint[] pts = (ProcedurePoint[])pr.Points.Clone();
            if (heli && arrival) pts = FlightPath.Reversed(pts);
            if (!heli && !arrival)
            {
                // Lift-off on the runway surface.
                pts[0].AltM = _c.RunwayElevationAt(pts[0].X, pts[0].Z);
            }
            p = FlightPath.Build(pts, _c, arrival && !heli ? 2000f : 0f);
            _paths[key] = p;
            return p;
        }

        /// <summary>Seconds from appearing to the slot: the flight time of an arrival's approach (speed schedule), or the
        /// taxi, backtrack and line-up time of a departure; helicopters the reversed corridor or the spool-up.</summary>
        private float LeadTime(Movement m)
        {
            bool heli = m.Aircraft == AircraftClass.Helicopter;
            if (heli)
            {
                if (!m.Arrival) return 30f;
                FlightPath hp = PathOf(m.Procedure, true, true);
                return hp == null ? 60f : FlightTime(hp, Vapp(m.Aircraft), true, true) + 20f;
            }
            if (m.Arrival)
            {
                FlightPath ap = PathOf(m.Procedure, true, false);
                return ap == null ? 600f : FlightTime(ap, Vapp(m.Aircraft), true, false);
            }
            double ex = EntryX(), ez = EntryZ();
            double ax, az;
            Apron(m.Aircraft, out ax, out az);
            float taxi = (float)Math.Sqrt((ex - ax) * (ex - ax) + (ez - az) * (ez - az)) / TaxiMps;
            return 20f + taxi + _c.BacktrackM / BacktrackMps + 25f;
        }

        private float FlightTime(FlightPath p, float vapp, bool arrival, bool heli)
        {
            const float step = 50f;
            float t = 0f;
            int n = (int)Math.Ceiling(p.Length / step);
            for (int i = 0; i < n; i++) t += step / SpeedOnPath(p, i * step, vapp, arrival, heli);
            return t;
        }

        /// <summary>Speed schedule along a path by distance to its end (arrivals) or from its start (departures).</summary>
        private static float SpeedOnPath(FlightPath p, float s, float vapp, bool arrival, bool heli)
        {
            float cap = heli ? 55f : 128f;
            if (arrival)
            {
                float rem = p.Length - s;
                float k = rem > 30000f ? 1.8f : rem > 15000f ? 1.5f : rem > 8000f ? 1.2f : 1.0f;
                if (heli && rem < 800f) k = Math.Max(0.15f, rem / 800f);
                return Math.Min(cap, vapp * k);
            }
            float kd = s < 5000f ? 1.15f + 0.15f * s / 5000f : s < 15000f ? 1.3f + 0.5f * (s - 5000f) / 10000f : 1.8f;
            if (heli) kd = s < 600f ? 0.4f + 0.6f * s / 600f : 1.6f;
            return Math.Min(cap, vapp * kd);
        }

        private double EntryX()
        {
            return _c.Threshold02.X + (_c.Threshold20.X - _c.Threshold02.X) * RunwayFrac(_c.BacktrackM);
        }

        private double EntryZ()
        {
            return _c.Threshold02.Z + (_c.Threshold20.Z - _c.Threshold02.Z) * RunwayFrac(_c.BacktrackM);
        }

        private double RunwayFrac(float metresFrom02)
        {
            double l = Math.Sqrt(Sq(_c.Threshold20.X - _c.Threshold02.X) + Sq(_c.Threshold20.Z - _c.Threshold02.Z));
            return l > 0 ? metresFrom02 / l : 0;
        }

        private void Apron(AircraftClass a, out double x, out double z)
        {
            bool intl = Jet(a);
            x = intl ? _c.IntlApronX : _c.DomesticApronX;
            z = intl ? _c.IntlApronZ : _c.DomesticApronZ;
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private void SpawnDue()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                Pending p = _pending[i];
                if (p.SpawnAt > _clock) continue;
                _pending.RemoveAt(i);
                i--;
                Start(p.M);
            }
        }

        private void Start(Movement m)
        {
            var r = new SimRng(SimRng.Mix(_seed, (ulong)m.AircraftId, 0x464C59));
            var f = new Flight
            {
                Id = m.AircraftId, Class = m.Aircraft, Arrival = m.Arrival, Heli = m.Aircraft == AircraftClass.Helicopter,
                Vapp = Vapp(m.Aircraft),
            };
            int livs = f.Heli ? Math.Max(1, _c.HelicopterColours.Length) : Math.Max(1, _c.FixedWingLiveries.Length);
            f.Livery = (byte)r.Next(livs);
            f.Roll = Jet(f.Class) ? r.Range(1600f, 2000f) : r.Range(1000f, 1400f);
            f.TakeoffRoll = f.Class == AircraftClass.Widebody ? r.Range(2100f, 2400f) : f.Class == AircraftClass.Narrowbody ? r.Range(1800f, 2200f) :
                f.Class == AircraftClass.Stol ? r.Range(600f, 800f) : r.Range(1100f, 1300f);
            f.Vrot = f.Vapp * 1.1f;
            if (f.Arrival)
            {
                f.Path = PathOf(m.Procedure, true, f.Heli);
                if (f.Path == null) return;
                f.Phase = FlightPhase.Approach;
                f.S = 0f;
                f.Speed = SpeedOnPath(f.Path, 0f, f.Vapp, true, f.Heli);
                f.Gear = f.Heli ? 1f : 0f;
                f.Rpm = f.Heli ? 390f : 980f;
            }
            else if (f.Heli)
            {
                f.Path = PathOf(m.Procedure, false, true);
                if (f.Path == null) return;
                f.Phase = FlightPhase.Parked;
                f.X = f.Path.X[0];
                f.Z = f.Path.Z[0];
                f.Y = f.Path.Y[0];
                f.Gear = 1f;
                f.PhaseT = 0f;
            }
            else
            {
                f.Path = PathOf(m.Procedure, false, false);
                if (f.Path == null) return;
                double ax, az;
                Apron(f.Class, out ax, out az);
                f.Phase = FlightPhase.TaxiOut;
                SetGround(f, ax, az, EntryX(), EntryZ());
                f.Gear = 1f;
                f.Rpm = 600f;
            }
            f.PrevHeading = f.Heading;
            _flights.Add(f);
            Place(f, 0f);
        }

        private void SetGround(Flight f, double fx, double fz, double tx, double tz)
        {
            f.GroundFromX = fx;
            f.GroundFromZ = fz;
            f.GroundToX = tx;
            f.GroundToZ = tz;
            f.GroundLen = (float)Math.Sqrt(Sq(tx - fx) + Sq(tz - fz));
            f.GroundS = 0f;
        }

        private void Fly(Flight f, float dt)
        {
            f.PhaseT += dt;
            switch (f.Phase)
            {
                case FlightPhase.Approach:
                {
                    f.Speed = SpeedOnPath(f.Path, f.S, f.Vapp, true, f.Heli);
                    f.S += f.Speed * dt;
                    float rem = f.Path.Length - f.S;
                    if (!f.Heli && rem < 12000f) f.Gear = Math.Min(1f, f.Gear + dt / 8f);
                    if (f.S >= f.Path.Length)
                    {
                        if (f.Heli)
                        {
                            f.Phase = FlightPhase.Hover;
                            f.PhaseT = 0f;
                            f.Speed = 0f;
                        }
                        else
                        {
                            // Touchdown: roll out along the runway heading.
                            double hx = Math.Sin(_c.Heading02Deg * Math.PI / 180.0), hz = Math.Cos(_c.Heading02Deg * Math.PI / 180.0);
                            double px, pz;
                            float py, ph, pg;
                            f.Path.At(f.Path.Length, out px, out pz, out py, out ph, out pg);
                            f.Phase = FlightPhase.Rollout;
                            SetGround(f, px, pz, px + hx * f.Roll, pz + hz * f.Roll);
                            f.PhaseT = 0f;
                        }
                    }
                    break;
                }
                case FlightPhase.Rollout:
                {
                    float decel = f.Vapp * f.Vapp / (2f * Math.Max(200f, f.Roll));
                    f.Speed = Math.Max(TaxiMps, f.Speed - decel * dt);
                    f.GroundS += f.Speed * dt;
                    if (f.GroundS >= f.GroundLen)
                    {
                        double ax, az;
                        Apron(f.Class, out ax, out az);
                        f.Phase = FlightPhase.TaxiIn;
                        SetGround(f, f.GroundToX, f.GroundToZ, ax, az);
                        f.Speed = TaxiMps;
                        f.Rpm = 600f;
                    }
                    break;
                }
                case FlightPhase.TaxiIn:
                case FlightPhase.TaxiOut:
                case FlightPhase.Backtrack:
                {
                    f.Speed = f.Phase == FlightPhase.Backtrack ? BacktrackMps : TaxiMps;
                    f.GroundS += f.Speed * dt;
                    if (f.GroundS >= f.GroundLen)
                    {
                        if (f.Phase == FlightPhase.TaxiIn)
                        {
                            f.Phase = FlightPhase.Parked;
                            f.PhaseT = 0f;
                            f.Speed = 0f;
                        }
                        else if (f.Phase == FlightPhase.TaxiOut)
                        {
                            f.Phase = FlightPhase.Backtrack;
                            SetGround(f, f.GroundToX, f.GroundToZ, _c.Threshold02.X, _c.Threshold02.Z);
                        }
                        else
                        {
                            f.Phase = FlightPhase.LineUp;
                            f.PhaseT = 0f;
                            f.Speed = 0f;
                        }
                    }
                    break;
                }
                case FlightPhase.LineUp:
                {
                    if (f.PhaseT >= 25f)
                    {
                        double hx = Math.Sin(_c.Heading02Deg * Math.PI / 180.0), hz = Math.Cos(_c.Heading02Deg * Math.PI / 180.0);
                        f.Phase = FlightPhase.TakeoffRoll;
                        SetGround(f, _c.Threshold02.X, _c.Threshold02.Z, _c.Threshold02.X + hx * f.TakeoffRoll, _c.Threshold02.Z + hz * f.TakeoffRoll);
                        f.Rpm = 1200f;
                        f.PhaseT = 0f;
                    }
                    break;
                }
                case FlightPhase.TakeoffRoll:
                {
                    float acc = f.Vrot * f.Vrot / (2f * f.TakeoffRoll);
                    f.Speed += acc * dt;
                    f.GroundS += f.Speed * dt;
                    if (f.GroundS >= f.GroundLen)
                    {
                        f.Phase = FlightPhase.Climb;
                        // Join the departure path at the nearest point to the lift-off.
                        float best = 0f;
                        double bd = double.MaxValue;
                        for (int i = 0; i < f.Path.N; i++)
                        {
                            double d = Sq(f.Path.X[i] - f.GroundToX) + Sq(f.Path.Z[i] - f.GroundToZ);
                            if (d < bd)
                            {
                                bd = d;
                                best = f.Path.S[i];
                            }
                            if (f.Path.S[i] > 4000f) break;
                        }
                        f.S = best;
                        f.AirborneT = 0f;
                    }
                    break;
                }
                case FlightPhase.Climb:
                {
                    f.AirborneT += dt;
                    float target = SpeedOnPath(f.Path, f.S, f.Vapp, false, f.Heli);
                    f.Speed += Math.Max(-2f, Math.Min(2.5f, target - f.Speed)) * dt;
                    if (f.Heli) f.Speed = Math.Max(f.Speed, 5f);
                    f.S += f.Speed * dt;
                    if (!f.Heli && f.AirborneT > 5f) f.Gear = Math.Max(0f, f.Gear - dt / 8f);
                    if (f.S >= f.Path.Length) f.Done = true;
                    break;
                }
                case FlightPhase.Hover:
                {
                    // Helicopter arrival: settle over the apron and shut down.
                    if (f.PhaseT >= 20f)
                    {
                        f.Phase = FlightPhase.Parked;
                        f.PhaseT = 0f;
                    }
                    break;
                }
                case FlightPhase.Parked:
                {
                    if (f.Heli && !f.Arrival)
                    {
                        // Spool up, then lift off into a short hover climb and away along the corridor.
                        f.Rpm = Math.Min(390f, f.Rpm + 390f / 20f * dt);
                        if (f.PhaseT >= 30f)
                        {
                            f.Phase = FlightPhase.Climb;
                            f.S = 0f;
                            f.Speed = 3f;
                            f.AirborneT = 0f;
                        }
                    }
                    else
                    {
                        f.Rpm = Math.Max(0f, f.Rpm - 390f / 30f * dt);
                        if (f.PhaseT >= 90f) f.Done = true; // the parked fleet is static scenery (Track C)
                    }
                    break;
                }
            }
            Place(f, dt);
        }

        private void Place(Flight f, float dt)
        {
            float heading = f.Heading, pitch = 0f;
            bool ground = false;
            switch (f.Phase)
            {
                case FlightPhase.Approach:
                case FlightPhase.Climb:
                {
                    double x, z;
                    float y, h, g;
                    f.Path.At(f.S, out x, out z, out y, out h, out g);
                    f.X = x;
                    f.Z = z;
                    f.Y = y;
                    heading = h;
                    pitch = (float)Math.Atan(g);
                    if (f.Phase == FlightPhase.Approach && !f.Heli && f.Path.Length - f.S < 300f) pitch += 3f * (float)(Math.PI / 180.0); // flare
                    break;
                }
                case FlightPhase.Hover:
                    ground = f.PhaseT >= 15f;
                    break;
                case FlightPhase.Parked:
                    ground = true;
                    break;
                case FlightPhase.LineUp:
                {
                    // Turn round on the threshold (backtrack heading 202° → 022°).
                    float from = (_c.Heading02Deg + 180f) * (float)(Math.PI / 180.0), to = _c.Heading02Deg * (float)(Math.PI / 180.0);
                    float u = Math.Min(1f, f.PhaseT / 15f);
                    heading = from + Driving.ArcadeVehicle.WrapAngle(to - from) * u;
                    ground = true;
                    break;
                }
                default:
                {
                    float t = f.GroundLen > 0f ? Math.Min(1f, f.GroundS / f.GroundLen) : 1f;
                    f.X = f.GroundFromX + (f.GroundToX - f.GroundFromX) * t;
                    f.Z = f.GroundFromZ + (f.GroundToZ - f.GroundFromZ) * t;
                    f.Y = f.Phase == FlightPhase.TaxiIn || f.Phase == FlightPhase.TaxiOut ? _c.ElevationM : _c.RunwayElevationAt(f.X, f.Z);
                    if (f.GroundLen > 0.1f) heading = (float)Math.Atan2(f.GroundToX - f.GroundFromX, f.GroundToZ - f.GroundFromZ);
                    ground = true;
                    if (f.Phase == FlightPhase.TakeoffRoll && f.GroundLen - f.GroundS < f.Speed * 3f) pitch = 8f * (float)(Math.PI / 180.0);
                    break;
                }
            }
            float dh = Driving.ArcadeVehicle.WrapAngle(heading - f.Heading);
            float omega = dt > 0f ? dh / dt : 0f;
            f.Heading = heading;
            f.Pitch = pitch;
            float bankT = ground ? 0f : (float)Math.Atan(f.Speed * omega / Gravity);
            float cap = 25f * (float)(Math.PI / 180.0);
            bankT = Math.Max(-cap, Math.Min(cap, bankT));
            f.Bank += (bankT - f.Bank) * Math.Min(1f, dt * 1.5f);
            f.Gear = f.Heli ? 1f : f.Gear;
            if (!f.Heli && f.Phase != FlightPhase.Approach && f.Phase != FlightPhase.Climb) f.Gear = 1f;
        }

        private void Publish()
        {
            lock (_snapLock)
            {
                if (_front.Length < _flights.Count) _front = new AircraftState[_flights.Count * 2];
                int n = 0;
                foreach (Flight f in _flights)
                {
                    bool ground = f.Phase != FlightPhase.Approach && f.Phase != FlightPhase.Climb && !(f.Phase == FlightPhase.Hover && f.PhaseT < 15f);
                    bool running = !(f.Phase == FlightPhase.Parked && f.Rpm <= 1f);
                    LightState l = LightState.None;
                    if (running) l |= LightState.Navigation | LightState.Beacon;
                    if (f.Phase == FlightPhase.LineUp || f.Phase == FlightPhase.TakeoffRoll || f.Phase == FlightPhase.Climb ||
                        f.Phase == FlightPhase.Approach || f.Phase == FlightPhase.Rollout)
                        l |= LightState.Strobe;
                    if ((f.Phase == FlightPhase.Approach || f.Phase == FlightPhase.Climb || f.Phase == FlightPhase.TakeoffRoll ||
                         f.Phase == FlightPhase.Rollout) && f.Y < 3050f)
                        l |= LightState.Landing;
                    if (f.Phase == FlightPhase.TaxiIn || f.Phase == FlightPhase.TaxiOut || f.Phase == FlightPhase.Backtrack) l |= LightState.Taxi;
                    float y = f.Y;
                    if (f.Heli && f.Phase == FlightPhase.Hover) y = _c.ElevationM + Math.Max(0f, 20f * (1f - f.PhaseT / 15f));
                    if (f.Heli && f.Phase == FlightPhase.Climb && f.S < 200f) y = Math.Max(y, _c.ElevationM + 20f * f.S / 200f);
                    _front[n++] = new AircraftState
                    {
                        Id = f.Id, Class = f.Class, Livery = f.Livery, X = f.X, Z = f.Z, Y = y, HeadingRad = f.Heading, PitchRad = f.Pitch,
                        BankRad = f.Bank, SpeedMps = f.Speed, GearDown01 = f.Gear, PropRpm = Jet(f.Class) ? 0f : f.Rpm, Lights = l,
                        OnGround = ground, Phase = f.Phase,
                    };
                }
                _frontCount = n;
            }
        }

        /// <summary>Copies the aircraft of the last step and returns the count. Thread-safe.</summary>
        public int CopyStates(AircraftState[] dst)
        {
            lock (_snapLock)
            {
                int n = Math.Min(dst.Length, _frontCount);
                Array.Copy(_front, dst, n);
                return n;
            }
        }
    }
}
