namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Footstep surfaces. The byte values equal <c>Ghumante.Core.Driving.FootSurface</c> (W2_DESIGN 10.3:
    /// Asphalt 0 … Water 10), so callers cast <c>GroundSample.Foot</c> straight to this type.
    /// <see cref="Barefoot"/> is audio-only (curated shoes-off zones, A §2.1 [V]).
    /// </summary>
    public enum FootstepSurface : byte
    {
        Asphalt = 0,
        Concrete = 1,
        Brick = 2,
        Stone = 3,
        Gravel = 4,
        Dirt = 5,
        Mud = 6,
        Grass = 7,
        Wood = 8,
        Metal = 9,
        Water = 10,
        Barefoot = 11,
    }

    /// <summary>Which footstep of a set (A §2.1: 6 walk + 6 run variants, plus land and scuff).</summary>
    public enum FootstepGait : byte
    {
        Walk = 0,
        Run = 1,
        Land = 2,
        Scuff = 3,
    }

    /// <summary>Horn types (A §2.6). The musical truck horn is highways-only and not part of S1.</summary>
    public enum HornKind : byte
    {
        None = 0,
        Moto = 1,
        Scooter = 2,
        Car = 3,
        Bus = 4,
        Tempo = 5,
        BicycleBell = 6,
    }

    /// <summary>Sacred bells and sounds the world can ring (A §2.8).</summary>
    public enum BellKind : byte
    {
        /// <summary>Hung shrine bell (ghanta), small: f0 ≈ 700–900 Hz.</summary>
        ShrineSmall = 0,
        /// <summary>Hung shrine bell, medium: f0 ≈ 450–650 Hz.</summary>
        ShrineMedium = 1,
        /// <summary>Hung shrine bell, large: f0 ≈ 300–420 Hz.</summary>
        ShrineLarge = 2,
        /// <summary>Hand bell (ghanti) rung through a puja.</summary>
        HandBell = 3,
        /// <summary>Prayer-wheel axle creak plus the once-per-revolution bell.</summary>
        PrayerWheel = 4,
        /// <summary>Conch (shankha) at dawn and dusk worship.</summary>
        Conch = 5,
        /// <summary>Small hand cymbals (jhyali).</summary>
        Cymbal = 6,
        /// <summary>A great Taleju bell (baked low-rate in S1; real-time modal voice in S2).</summary>
        GreatBell = 7,
    }

    /// <summary>Aircraft sound classes (W2_DESIGN 8.5–8.6).</summary>
    public enum AircraftSoundClass : byte
    {
        Turboprop = 0,
        StolTurboprop = 1,
        NarrowBody = 2,
        WideBody = 3,
        Helicopter = 4,
    }

    /// <summary>
    /// Mixer buses (W2_DESIGN 7.1 mixer graph). The Unity side applies a gain per bus; ducking and snapshots
    /// act on these.
    /// </summary>
    public enum MixBus : byte
    {
        Music = 0,
        Ui = 1,
        Stings = 2,
        Ambience = 3,
        Sacred = 4,
        Traffic = 5,
        Player = 6,
        Nature = 7,
        Aircraft = 8,
        Voice = 9,
    }
}
