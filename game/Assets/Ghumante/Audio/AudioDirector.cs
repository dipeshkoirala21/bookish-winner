using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Synth;
using Ghumante.Platform;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>Audio counters for the debug HUD (W2_DESIGN 10.6 V11 overlay).</summary>
    public struct AudioStats
    {
        public AudioTier Tier;
        public int ClipVoices, ClipVoicesActive;
        public int SynthVoices, SynthVoicesActive;
        public int AircraftVoices, AircraftVoicesActive;
        public int Engines, Aircraft, Beds;
        public int BankClips, BankTotal;
        public long BankBytes;
    }

    /// <summary>
    /// The game's sound (W2_DESIGN 7; implements <see cref="ISoundService"/>). One per app, created on first use
    /// (<see cref="Instance"/>) or explicitly with <see cref="Create"/>; it survives scene loads.
    /// <list type="bullet">
    /// <item><b>Bank:</b> every one-shot and ambience loop is synthesised from the region seed on a worker thread at
    /// start and uploaded as AudioClips within 2 ms per frame (<see cref="ProceduralBank"/>). Nothing is
    /// downloaded or recorded.</item>
    /// <item><b>Voices:</b> per tier (Low / Mid / High) 24 / 32 / 48 real voices, of which 4 / 8 / 12 real-time
    /// synth engine voices (the player's engine always, then the loudest NPC engines) and up to 2 aircraft voices;
    /// clip voices are allocated by priority with same-sound caps and the −45 dBFS cull.</item>
    /// <item><b>Space:</b> custom rolloff per class, air absorption, our own Doppler from simulation velocities
    /// (never transform deltas; call <see cref="ShiftOrigin"/> on floating-origin rebases), the listener 35% from
    /// the camera to the head, occlusion rays every 250 ms, snapshots (Courtyard, DurbarSquare, Galli,
    /// VehicleInterior) as listener reverb / echo and bus gains.</item>
    /// <item><b>Ambience:</b> beds and spots by area type (<see cref="AreaTypeGrid"/>), sacred zone
    /// (<see cref="SacredZoneIndex"/>), time of day, month and weather.</item>
    /// </list>
    /// Positions passed in are Unity scene space; velocities come from the simulation. Main thread only.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Audio Director")]
    public sealed class AudioDirector : MonoBehaviour, ISoundService
    {
        /// <summary>Bank seed used until a region provides its own (see <see cref="Create"/>).</summary>
        public const uint DefaultSeed = 0x47484D41;

        /// <summary>Main-thread budget for creating bank clips per frame (ARCHITECTURE 7.2 upload cap).</summary>
        public const float BankUploadMs = 2f;

        private static AudioDirector _instance;

        private VoiceBudget _budget;
        private VoiceBudget _fullBudget;
        private AudioTier _tier;
        private uint _seed;
        private ClipBank _bank;
        private ClipVoices _clips;
        private AmbienceDirector _ambience;
        private ListenerRig _listener;
        private readonly OcclusionProbe _occlusion = new OcclusionProbe();
        private AudioClip _carrier;
        private EngineVoiceHost[] _engineHosts;
        private AircraftVoiceHost[] _airHosts;
        private readonly List<EngineHandle> _engines = new List<EngineHandle>();
        private readonly List<AircraftHandle> _aircraft = new List<AircraftHandle>();
        private readonly Stack<EngineHandle> _freeEngines = new Stack<EngineHandle>();
        private readonly Stack<AircraftHandle> _freeAircraft = new Stack<AircraftHandle>();
        private SynthCandidate[] _candidates = new SynthCandidate[32];
        private bool[] _selected = new bool[32];
        private readonly float[] _busDb = new float[10];
        private readonly float[] _busGain = new float[10];
        private readonly float[] _areaWeights = new float[AreaTypeGrid.TypeCount];
        private FootstepPicker _playerSteps;
        private FootstepPicker _npcSteps;
        private Noise _rng;
        private AreaTypeGrid _grid;
        private SacredZoneIndex _sacred;
        private double _gameX, _gameZ;
        private bool _hasGamePos;
        private bool _worldActive;
        private float _wetness;
        private bool _driving;
        private bool _thermal;
        private float _duck = 1f;
        private float _aircraftCapDb;

        /// <summary>Hero id whose compound caps aircraft at −6 dB (W2_DESIGN 8.6 [V]).</summary>
        public const string PashupatiHeroId = "her.ktm.pashupatinath";
        private volatile int _outputRate = 48000;
        private volatile bool _configChanged;
        private int _occlusionTracked;
        private readonly int[] _probeSlots = new int[OcclusionProbe.MaxVoices];
        private readonly EngineHandle[] _probeEngines = new EngineHandle[OcclusionProbe.MaxVoices];

        /// <summary>The director, created with the detected tier and <see cref="DefaultSeed"/> on first use.</summary>
        public static AudioDirector Instance
        {
            get
            {
                if (_instance == null) Create(DetectTier(), DefaultSeed);
                return _instance;
            }
        }

        public static bool Exists
        {
            get { return _instance != null; }
        }

        /// <summary>The audio tier for this device (from <see cref="DeviceTierDetector"/>).</summary>
        public static AudioTier DetectTier()
        {
            return (AudioTier)(int)DeviceTierDetector.Detect();
        }

        /// <summary>
        /// Creates (or returns) the director. <paramref name="regionSeed"/> seeds the bank and ambience (pass the
        /// region's SEED so every device hears the same variants). <paramref name="configureUnityVoices"/> sets
        /// Unity's real / virtual voice counts and DSP buffer for the tier (resets the audio system: do it at boot).
        /// </summary>
        public static AudioDirector Create(AudioTier tier, uint regionSeed, bool configureUnityVoices = true)
        {
            if (_instance != null) return _instance;
            var go = new GameObject("AudioDirector");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            AudioDirector d = go.AddComponent<AudioDirector>();
            d.Init(tier, regionSeed, configureUnityVoices);
            _instance = d;
            return d;
        }

        private void Init(AudioTier tier, uint seed, bool configure)
        {
            _tier = tier;
            _seed = seed;
            _fullBudget = VoiceBudget.For(tier);
            _budget = _fullBudget;
            if (configure) ConfigureUnity(_fullBudget);
            _outputRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            for (int i = 0; i < _busDb.Length; i++)
            {
                _busDb[i] = 0f;
                _busGain[i] = 1f;
            }
            _playerSteps = new FootstepPicker(Dsp.Mix(seed, 1));
            _npcSteps = new FootstepPicker(Dsp.Mix(seed, 2));
            _rng = new Noise(Dsp.Mix(seed, 3));

            _listener = new ListenerRig(transform);
            _carrier = SynthHostShared.CreateCarrier(_outputRate);
            _engineHosts = new EngineVoiceHost[_fullBudget.SynthVoices];
            for (int i = 0; i < _engineHosts.Length; i++)
            {
                _engineHosts[i] = EngineVoiceHost.Create(transform, _carrier, i);
                _engineHosts[i].SampleRate = _outputRate;
            }
            _airHosts = new AircraftVoiceHost[_fullBudget.AircraftVoices];
            for (int i = 0; i < _airHosts.Length; i++)
            {
                _airHosts[i] = AircraftVoiceHost.Create(transform, _carrier, i);
                _airHosts[i].SampleRate = _outputRate;
            }
            _clips = new ClipVoices(transform, _fullBudget.ClipVoices, _fullBudget);
            _ambience = new AmbienceDirector(transform, _fullBudget.BedVoices, seed);
            _occlusion.Query = null; // the world installs a footprint query (World/Audio PlaceAudio)
            _bank = new ClipBank(seed, _fullBudget.LowBank);
            _bank.Start();
        }

        private static void ConfigureUnity(in VoiceBudget b)
        {
            AudioConfiguration cfg = AudioSettings.GetConfiguration();
            bool change = cfg.numRealVoices != b.RealVoices || cfg.numVirtualVoices != b.VirtualVoices;
            cfg.numRealVoices = b.RealVoices;
            cfg.numVirtualVoices = b.VirtualVoices;
            // Low: "Best performance" DSP buffer (1,024 frames ≈ 21 ms; fine for a non-rhythm game, A §6.7).
            if (b.Tier == AudioTier.Low && cfg.dspBufferSize < 1024)
            {
                cfg.dspBufferSize = 1024;
                change = true;
            }
            if (change) AudioSettings.Reset(cfg);
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _outputRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _configChanged = true;
        }

        // ---------------------------------------------------------------------------------------------------
        // Properties and settings
        // ---------------------------------------------------------------------------------------------------

        public AudioTier Tier
        {
            get { return _tier; }
        }

        public VoiceBudget Budget
        {
            get { return _budget; }
        }

        /// <summary>True once every bank clip is created.</summary>
        public bool BankReady
        {
            get { return _bank != null && _bank.Complete; }
        }

        /// <summary>0..1 progress of the bank bake.</summary>
        public float BankProgress
        {
            get { return _bank == null || _bank.Expected == 0 ? 1f : (float)_bank.Done / _bank.Expected; }
        }

        /// <summary>The occlusion test (null = off, the default: the procedural world has no physics colliders, so
        /// the open world installs a building-footprint query and removes it on close).</summary>
        public IOcclusionQuery Occlusion
        {
            get { return _occlusion.Query; }
            set
            {
                _occlusion.Query = value;
                if (value == null) ClearOcclusion();
            }
        }

        /// <summary>True while a world is being played: ambience beds and spots run and synth voices are assigned.</summary>
        public bool WorldActive
        {
            get { return _worldActive; }
        }

        /// <summary>
        /// Starts or stops the world's sound (App: on when Explore starts playing, off when it closes; the director
        /// starts off, so nothing plays under the menu or the loading screen). Off fades the ambience beds out,
        /// stops spots and one-shots, releases every synth voice (with a gain ramp), forgets the listener's place
        /// and stops ambience updates. UI one-shots still play.
        /// </summary>
        public void SetWorldActive(bool active)
        {
            if (_worldActive == active) return;
            _worldActive = active;
            if (active) return;
            _hasGamePos = false;
            _ambience.ResetPlace();
            _aircraftCapDb = 0f;
            _clips.StopAll();
            for (int i = 0; i < _engines.Count; i++)
            {
                if (_engines[i].Host == null) continue;
                _engines[i].Host.End();
                _engines[i].Host = null;
            }
            for (int i = 0; i < _aircraft.Count; i++)
            {
                if (_aircraft[i].Host == null) continue;
                _aircraft[i].Host.End();
                _aircraft[i].Host = null;
            }
        }

        public AudioStats Stats
        {
            get
            {
                var s = new AudioStats
                {
                    Tier = _tier,
                    ClipVoices = _clips != null ? _clips.Capacity : 0,
                    ClipVoicesActive = _clips != null ? _clips.ActiveCount : 0,
                    SynthVoices = _budget.SynthVoices,
                    AircraftVoices = _budget.AircraftVoices,
                    Engines = _engines.Count,
                    Aircraft = _aircraft.Count,
                    Beds = _ambience != null ? _ambience.ActiveBeds : 0,
                    BankClips = _bank != null ? _bank.Done : 0,
                    BankTotal = _bank != null ? _bank.Expected : 0,
                    BankBytes = _bank != null ? _bank.Bytes : 0,
                };
                if (_engineHosts != null)
                    for (int i = 0; i < _engineHosts.Length; i++)
                        if (_engineHosts[i].Owner != null) s.SynthVoicesActive++;
                if (_airHosts != null)
                    for (int i = 0; i < _airHosts.Length; i++)
                        if (_airHosts[i].Owner != null) s.AircraftVoicesActive++;
                return s;
            }
        }

        /// <summary>Places the listener 35% of the way from <paramref name="camera"/> to <paramref name="playerHead"/>
        /// (disables the camera's own AudioListener while attached).</summary>
        public void AttachListener(Transform camera, Transform playerHead)
        {
            _listener.Attach(camera, playerHead);
        }

        public void DetachListener()
        {
            _listener.Detach();
        }

        /// <summary>The listener's (player's) simulation velocity, for Doppler. Scene axes, m/s.</summary>
        public void SetListenerVelocity(Vector3 velocity)
        {
            _listener.Velocity = velocity;
        }

        /// <summary>World data the ambience reads (either may be null).</summary>
        public void SetZones(AreaTypeGrid grid, SacredZoneIndex sacred)
        {
            _grid = grid;
            _sacred = sacred;
            if (grid == null && sacred == null)
            {
                // The world closed: its last place must not keep driving the beds.
                _hasGamePos = false;
                _ambience.ResetPlace();
            }
        }

        /// <summary>The listener in game metres (X east, Z north) and its ground elevation, for zone lookups.</summary>
        public void SetListenerGamePosition(double x, double z, float elevationM)
        {
            _gameX = x;
            _gameZ = z;
            _hasGamePos = _worldActive;
            _ambience.Inputs.ElevationM = elevationM;
        }

        /// <summary>Game clock: hour 0..24 and month 1..12 (ambience time bands and seasons).</summary>
        public void SetClock(float gameHour, int month)
        {
            _ambience.Inputs.Hour = Dsp.Sanitize(gameHour);
            _ambience.Inputs.Month = month;
        }

        /// <summary>Weather: rain and wind 0..1, ground wetness 0..1 (mud footsteps, tyre hiss).</summary>
        public void SetWeather(float rain01, float wind01, float wetness01)
        {
            _ambience.Inputs.Rain01 = Dsp.Clamp01(rain01);
            _ambience.Inputs.Wind01 = Dsp.Clamp01(wind01);
            _wetness = Dsp.Clamp01(wetness01);
        }

        /// <summary>Place overlays at the listener: nearness 0..1 to the Ring Road or an arterial, the aerodrome
        /// (+3 km), a park and a river; the lane width for the galli slapback (0 = open).</summary>
        public void SetPlace(float ringRoad01, float airport01, float park01, float water01, float laneWidthM)
        {
            _ambience.Inputs.RingRoad01 = Dsp.Clamp01(ringRoad01);
            _ambience.Inputs.Airport01 = Dsp.Clamp01(airport01);
            _ambience.Inputs.Park01 = Dsp.Clamp01(park01);
            _ambience.Inputs.Water01 = Dsp.Clamp01(water01);
            _ambience.Inputs.LaneWidthM = Math.Max(0f, Dsp.Sanitize(laneWidthM));
        }

        /// <summary>Driving (1.5 s ambience crossfades) and inside a car / bus cabin (VehicleInterior snapshot).</summary>
        public void SetPlayerState(bool driving, bool inVehicleInterior)
        {
            _driving = driving;
            _ambience.Inputs.InVehicle = inVehicleInterior;
        }

        /// <summary>Linear volume of a mixer bus (settings sliders; 1 = nominal).</summary>
        public void SetBusVolume(MixBus bus, float linear)
        {
            int i = (int)bus;
            if (i < 0 || i >= _busDb.Length) return;
            _busDb[i] = Dsp.GainToDb(Mathf.Clamp01(Dsp.Sanitize(linear)));
        }

        /// <summary>Thermal step-down (W2_DESIGN 7.1): halves the real-time synth voices while on.</summary>
        public void SetThermalStepDown(bool on)
        {
            _thermal = on;
            _budget = on ? _fullBudget.ThermalStepDown() : _fullBudget;
        }

        /// <summary>Floating-origin rebase: subtract <paramref name="delta"/> from every playing one-shot.</summary>
        public void ShiftOrigin(Vector3 delta)
        {
            _clips.ShiftOrigin(delta);
            for (int i = 0; i < _engines.Count; i++) _engines[i].Position -= delta;
            for (int i = 0; i < _aircraft.Count; i++) _aircraft[i].Position -= delta;
        }

        // ---------------------------------------------------------------------------------------------------
        // ISoundService
        // ---------------------------------------------------------------------------------------------------

        private double Now
        {
            get { return Time.unscaledTimeAsDouble; }
        }

        private float Bus(MixBus b)
        {
            int i = (int)b;
            return i >= 0 && i < _busGain.Length ? _busGain[i] : 1f;
        }

        public void PlayFootstep(byte footSurface, FootstepGait gait, float x, float y, float z, bool player, bool barefoot = false)
        {
            FootstepSurface s = FootstepPicker.Resolve(footSurface, _wetness, barefoot);
            BankSound sound;
            int variant;
            float pitch, db;
            if (player) _playerSteps.Next(s, gait, out sound, out variant, out pitch, out db);
            else _npcSteps.Next(s, gait, out sound, out variant, out pitch, out db);
            if (!_bank.TryGet(sound, variant, out AudioClip clip)) return;
            SoundClass cls = player ? SoundClass.PlayerFootstep : SoundClass.NpcFootstep;
            _clips.Play(clip, cls, new Vector3(x, y, z), Vector3.zero, false, db, pitch, false, _listener.Position, Now,
                        Bus(SoundClasses.Get(cls).Bus));
        }

        public void PlayHorn(HornKind kind, float x, float y, float z, float vx, float vy, float vz, float holdS)
        {
            BankSound sound = ProceduralBank.Horn(kind);
            if (sound == BankSound.None) return;
            int variant = kind == HornKind.BicycleBell ? _rng.Index(4) : EnginePresets.HornVariant(holdS);
            if (!_bank.TryGet(sound, variant, out AudioClip clip)) return;
            SoundClass cls = kind == HornKind.BicycleBell ? SoundClass.Bicycle
                           : kind == HornKind.Car || kind == HornKind.Bus ? SoundClass.Horn : SoundClass.HornMoto;
            _clips.Play(clip, cls, new Vector3(x, y, z), new Vector3(vx, vy, vz), true, 0f, 1f, false, _listener.Position, Now, Bus(MixBus.Traffic));
        }

        public void PlayTrafficHorn(byte vehicleClass, int agentId, byte hornReason, float x, float y, float z, float vx, float vy, float vz)
        {
            EngineModel m = EnginePresets.ForTrafficClass(vehicleClass, (uint)agentId);
            PlayHorn(EnginePresets.HornFor(m), x, y, z, vx, vy, vz, EnginePresets.HornHoldS(hornReason));
        }

        public void PlayBell(BellKind kind, float x, float y, float z)
        {
            BankSound sound = ProceduralBank.Bell(kind);
            if (sound == BankSound.None) return;
            if (!_bank.TryGet(sound, _rng.Index(Math.Max(1, _bank.VariantsOf(sound))), out AudioClip clip)) return;
            SoundClass cls = kind == BellKind.GreatBell ? SoundClass.GreatBell : kind == BellKind.Conch ? SoundClass.Conch : SoundClass.ShrineBell;
            _clips.Play(clip, cls, new Vector3(x, y, z), Vector3.zero, false, _ambience.Snapshot.SacredDb, 1f, false, _listener.Position, Now,
                        Bus(MixBus.Sacred));
        }

        public void PlayOneShot(BankSound sound, SoundClass cls, float x, float y, float z, float gainDb = 0f, float pitch = 1f)
        {
            int variants = _bank.VariantsOf(sound);
            if (variants <= 0 || !_bank.TryGet(sound, _rng.Index(variants), out AudioClip clip)) return;
            _clips.Play(clip, cls, new Vector3(x, y, z), Vector3.zero, false, gainDb, pitch, false, _listener.Position, Now,
                        Bus(SoundClasses.Get(cls).Bus));
        }

        public void PlayUi(BankSound sound, float gainDb = 0f)
        {
            int variants = _bank.VariantsOf(sound);
            if (variants <= 0 || !_bank.TryGet(sound, _rng.Index(variants), out AudioClip clip)) return;
            _clips.Play(clip, SoundClass.Ui, _listener.Position, Vector3.zero, false, gainDb, 1f, false, _listener.Position, Now, Bus(MixBus.Ui));
        }

        public IEngineSound OpenEngine(EngineModel model, uint seed, bool player)
        {
            EngineHandle h = _freeEngines.Count > 0 ? _freeEngines.Pop() : new EngineHandle();
            h.Open(this, model, seed, player);
            _engines.Add(h);
            return h;
        }

        public IAircraftSound OpenAircraft(AircraftSoundClass cls, uint seed)
        {
            AircraftHandle h = _freeAircraft.Count > 0 ? _freeAircraft.Pop() : new AircraftHandle();
            h.Open(this, cls, seed);
            _aircraft.Add(h);
            return h;
        }

        internal void ReleaseEngine(EngineHandle h)
        {
            if (h.Host != null)
            {
                h.Host.End();
                h.Host = null;
            }
            if (_engines.Remove(h)) _freeEngines.Push(h);
        }

        internal void ReleaseAircraft(AircraftHandle h)
        {
            if (h.Host != null)
            {
                h.Host.End();
                h.Host = null;
            }
            if (_aircraft.Remove(h)) _freeAircraft.Push(h);
        }

        internal void OnGearShift(EngineHandle h)
        {
            if (SoundClasses.ForEngine(h.Model, false) != SoundClass.TwoWheeler) return;
            PlayOneShot(BankSound.GearClunk, SoundClass.Foley, h.Position.x, h.Position.y, h.Position.z, -8f);
        }

        // ---------------------------------------------------------------------------------------------------
        // Frame update
        // ---------------------------------------------------------------------------------------------------

        // LateUpdate: vehicles, walkers and the camera rig have moved this frame, so positions and the listener are fresh.
        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_configChanged) RestartAfterConfigChange();
            _bank.Upload(BankUploadMs);
            _listener.Update();
            Vector3 lp = _listener.Position;
            Vector3 lv = _listener.Velocity;

            // Bus gains: user volume, aircraft ducking of the ambience (−3 dB during a flyby within 1 km).
            bool flyby = false;
            for (int i = 0; i < _aircraft.Count; i++)
                if (_aircraft[i].Host != null && (_aircraft[i].Position - lp).sqrMagnitude < 1000f * 1000f) flyby = true;
            _duck += ((flyby ? 0.708f : 1f) - _duck) * Mathf.Clamp01(dt / 0.5f);
            SnapshotParams snap = _ambience.Snapshot;
            for (int i = 0; i < _busGain.Length; i++) _busGain[i] = Dsp.DbToGain(_busDb[i]);
            _busGain[(int)MixBus.Ambience] *= _duck;
            _busGain[(int)MixBus.Traffic] *= Dsp.DbToGain(snap.StreetDb);
            _busGain[(int)MixBus.Aircraft] *= Dsp.DbToGain(_aircraftCapDb);

            UpdateAmbienceInputs();
            AssignSynthVoices(lp);
            PushEngines(dt, lp, lv);
            PushAircraft(dt, lp, lv);
            _clips.Update(dt, lp, lv, _busGain);
            _ambience.Update(dt, _bank, _clips, lp, Now, _busGain, _driving, _worldActive);
            _listener.Apply(_ambience.Snapshot);
            if (_worldActive) ProbeOcclusion(dt, lp);
        }

        private void RestartAfterConfigChange()
        {
            _configChanged = false;
            int rate = _outputRate;
            for (int i = 0; i < _engineHosts.Length; i++)
            {
                _engineHosts[i].SampleRate = rate;
                if (_engineHosts[i].Owner != null && !_engineHosts[i].Source.isPlaying) _engineHosts[i].Source.Play();
            }
            for (int i = 0; i < _airHosts.Length; i++)
            {
                _airHosts[i].SampleRate = rate;
                if (_airHosts[i].Owner != null && !_airHosts[i].Source.isPlaying) _airHosts[i].Source.Play();
            }
            // Beds restart on the next ambience pass.
            _ambience.StopAll();
        }

        private void UpdateAmbienceInputs()
        {
            ref AmbienceInputs a = ref _ambience.Inputs;
            if (!_hasGamePos) return;
            if (_grid != null)
            {
                _grid.Weights(_gameX, _gameZ, _areaWeights);
                for (int t = 0; t < _areaWeights.Length; t++) a.SetArea(t, _areaWeights[t]);
            }
            a.SacredKind = 0;
            _aircraftCapDb = 0f;
            if (_sacred != null && _sacred.TryGetZone(_gameX, _gameZ, out SacredZone zone))
            {
                a.SacredKind = (byte)zone.Kind;
                if (string.Equals(zone.HeroId, PashupatiHeroId, StringComparison.Ordinal)) _aircraftCapDb = -6f;
            }
        }

        private void AssignSynthVoices(in Vector3 lp)
        {
            int n = _engines.Count + _aircraft.Count;
            if (_candidates.Length < n)
            {
                _candidates = new SynthCandidate[n * 2];
                _selected = new bool[n * 2];
            }
            int k = 0;
            for (int i = 0; i < _engines.Count; i++)
            {
                EngineHandle h = _engines[i];
                SoundClass cls = SoundClasses.ForEngine(h.Model, h.Player);
                float d = Vector3.Distance(h.Position, lp);
                float score = h.Model == EngineModel.None ? -200f : SoundSpace.EstimateDb(cls, d, Dsp.GainToDb(Math.Max(1e-4f, h.Gain)));
                if (h.Host != null) score += 3f; // hysteresis: keep voices stable
                h.Score = score;
                _candidates[k++] = new SynthCandidate { Id = i, Score = score, Player = h.Player && h.Model != EngineModel.None };
            }
            for (int i = 0; i < _aircraft.Count; i++)
            {
                AircraftHandle h = _aircraft[i];
                float d = Vector3.Distance(h.Position, lp);
                float score = SoundSpace.EstimateDb(SoundClasses.ForAircraft(h.Class), d);
                if (h.Host != null) score += 3f;
                h.Score = score;
                _candidates[k++] = new SynthCandidate { Id = i, Score = score, Aircraft = true };
            }
            SynthAssign.Select(_candidates, k, _budget, _selected);
            if (!_worldActive)
                for (int c = 0; c < k; c++) _selected[c] = false;

            // Release first, then start, so a freed host can be reused this frame.
            for (int c = 0; c < k; c++)
            {
                if (_selected[c]) continue;
                if (_candidates[c].Aircraft)
                {
                    AircraftHandle h = _aircraft[_candidates[c].Id];
                    if (h.Host != null)
                    {
                        h.Host.End();
                        h.Host = null;
                    }
                }
                else
                {
                    EngineHandle h = _engines[_candidates[c].Id];
                    if (h.Host != null)
                    {
                        h.Host.End();
                        h.Host = null;
                    }
                }
            }
            // Thermal step-down may have shrunk the budget: stop hosts beyond it.
            int engineHostsAllowed = Math.Min(_engineHosts.Length, _budget.SynthVoices);
            for (int i = engineHostsAllowed; i < _engineHosts.Length; i++)
            {
                if (_engineHosts[i].Owner is EngineHandle eh)
                {
                    eh.Host = null;
                    _engineHosts[i].End();
                }
            }
            for (int c = 0; c < k; c++)
            {
                if (!_selected[c]) continue;
                if (_candidates[c].Aircraft)
                {
                    AircraftHandle h = _aircraft[_candidates[c].Id];
                    if (h.Host != null) continue;
                    AircraftVoiceHost host = FreeAirHost();
                    if (host == null) continue;
                    h.Host = host;
                    host.Owner = h;
                    h.Doppler = default;
                    host.Begin(h.Class, h.SeedValue, AircraftParams(h, lp, Vector3.zero, 0f));
                }
                else
                {
                    EngineHandle h = _engines[_candidates[c].Id];
                    if (h.Host != null) continue;
                    EngineVoiceHost host = FreeEngineHost(engineHostsAllowed);
                    if (host == null) continue;
                    h.Host = host;
                    host.Owner = h;
                    h.Doppler = default;
                    host.Begin(h.Model, h.SeedValue, EngineParams(h, lp, Vector3.zero, 0f), h.Player);
                }
            }
        }

        private EngineVoiceHost FreeEngineHost(int allowed)
        {
            for (int i = 0; i < allowed; i++)
                if (_engineHosts[i].Owner == null && !_engineHosts[i].Releasing) return _engineHosts[i];
            return null;
        }

        private AircraftVoiceHost FreeAirHost()
        {
            for (int i = 0; i < _airHosts.Length; i++)
                if (_airHosts[i].Owner == null && !_airHosts[i].Releasing) return _airHosts[i];
            return null;
        }

        private EngineVoiceParams EngineParams(EngineHandle h, in Vector3 lp, in Vector3 lv, float dt)
        {
            SoundClass cls = SoundClasses.ForEngine(h.Model, h.Player);
            SoundClassInfo info = SoundClasses.Get(cls);
            float d = Vector3.Distance(h.Position, lp);
            float dop = 1f;
            if (!h.Player)
            {
                float target = SoundSpace.DopplerPitch(h.Position.x, h.Position.y, h.Position.z, h.Velocity.x, h.Velocity.y, h.Velocity.z,
                                                       lp.x, lp.y, lp.z, lv.x, lv.y, lv.z, info.Doppler);
                dop = h.Doppler.Step(target, dt, SoundSpace.DopplerSmoothS);
            }
            h.Occlusion += (h.OcclusionTarget - h.Occlusion) * Mathf.Clamp01(dt / 0.2f);
            float air = info.AirLpf && d > info.AirFromM ? SoundSpace.AirCutoffHz(d) : 0f;
            if (h.Occlusion > 0.5f) air = air > 0f ? Math.Min(air, 1500f) : 1500f;
            float bus = Bus(h.Player ? MixBus.Player : MixBus.Traffic);
            return new EngineVoiceParams
            {
                Rpm = h.Rpm, Load = h.Load, SpeedMps = h.Speed,
                Gain = h.Gain * bus * (1f - 0.5f * h.Occlusion),
                DopplerPitch = dop, AirCutoffHz = air, Wetness = _wetness, Surface = (byte)h.Surface,
            };
        }

        private AircraftVoiceParams AircraftParams(AircraftHandle h, in Vector3 lp, in Vector3 lv, float dt)
        {
            SoundClassInfo info = SoundClasses.Get(SoundClasses.ForAircraft(h.Class));
            float d = Vector3.Distance(h.Position, lp);
            float target = SoundSpace.DopplerPitch(h.Position.x, h.Position.y, h.Position.z, h.Velocity.x, h.Velocity.y, h.Velocity.z,
                                                   lp.x, lp.y, lp.z, lv.x, lv.y, lv.z, info.Doppler);
            return new AircraftVoiceParams
            {
                Thrust01 = h.Thrust, Reverse01 = h.Reverse, Gain = Bus(MixBus.Aircraft),
                DopplerPitch = h.Doppler.Step(target, dt, SoundSpace.DopplerSmoothS),
                AirCutoffHz = d > info.AirFromM ? SoundSpace.AirCutoffHz(d) : 0f,
                ReflectionDelayS = SoundSpace.ReflectionDelayS(h.HeightAgl, d), ReflectionGain = 0.7f,
                OnGround = (byte)(h.OnGround ? 1 : 0),
            };
        }

        private void PushEngines(float dt, in Vector3 lp, in Vector3 lv)
        {
            for (int i = 0; i < _engines.Count; i++)
            {
                EngineHandle h = _engines[i];
                if (h.Host == null) continue;
                h.Host.transform.position = h.Position;
                h.Host.Push(h.Model, h.SeedValue, EngineParams(h, lp, lv, dt));
            }
        }

        private void PushAircraft(float dt, in Vector3 lp, in Vector3 lv)
        {
            for (int i = 0; i < _aircraft.Count; i++)
            {
                AircraftHandle h = _aircraft[i];
                if (h.Host == null) continue;
                h.Host.transform.position = h.Position;
                h.Host.Push(h.Class, h.SeedValue, AircraftParams(h, lp, lv, dt));
            }
        }

        private void ClearOcclusion()
        {
            for (int i = 0; i < _engines.Count; i++) _engines[i].OcclusionTarget = 0f;
            if (_clips == null) return;
            for (int s = 0; s < _clips.Capacity; s++)
                if (_clips.TryGetProbe(s, out _, out _)) _clips.SetOcclusion(s, false);
        }

        private void ProbeOcclusion(float dt, in Vector3 lp)
        {
            if (_occlusion.Query == null) return;
            // Track the most important 3D voices: hosted NPC engines first, then clip voices of priority ≤ 128.
            _occlusionTracked = 0;
            for (int i = 0; i < _engines.Count && _occlusionTracked < _probeSlots.Length; i++)
            {
                EngineHandle h = _engines[i];
                if (h.Host == null || h.Player) continue;
                _probeEngines[_occlusionTracked] = h;
                _probeSlots[_occlusionTracked] = -1;
                _occlusionTracked++;
            }
            for (int s = 0; s < _clips.Capacity && _occlusionTracked < _probeSlots.Length; s++)
            {
                if (!_clips.TryGetProbe(s, out _, out _)) continue;
                _probeEngines[_occlusionTracked] = null;
                _probeSlots[_occlusionTracked] = s;
                _occlusionTracked++;
            }
            int rays = _occlusion.RaysThisFrame(dt, _occlusionTracked);
            for (int r = 0; r < rays; r++)
            {
                int i = _occlusion.Next(_occlusionTracked);
                if (i < 0) break;
                EngineHandle eh = _probeEngines[i];
                if (eh != null)
                {
                    eh.OcclusionTarget = _occlusion.Query.Occluded(lp, eh.Position) ? 1f : 0f;
                }
                else if (_clips.TryGetProbe(_probeSlots[i], out Vector3 pos, out _))
                {
                    _clips.SetOcclusion(_probeSlots[i], _occlusion.Query.Occluded(lp, pos));
                }
            }
            for (int i = 0; i < _probeEngines.Length; i++) _probeEngines[i] = null;
        }

        private void OnApplicationPause(bool paused)
        {
            AudioListener.pause = paused;
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            for (int i = _engines.Count - 1; i >= 0; i--) _engines[i].Release();
            for (int i = _aircraft.Count - 1; i >= 0; i--) _aircraft[i].Release();
            _clips?.Dispose();
            _ambience?.Dispose();
            _listener?.Dispose();
            _bank?.Dispose();
            if (_carrier != null) Destroy(_carrier);
            if (_instance == this) _instance = null;
        }
    }
}
