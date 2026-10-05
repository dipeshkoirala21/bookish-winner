namespace Ghumante.Core.Synth
{
    /// <summary>Audio quality tier; values match <c>Ghumante.Platform.DeviceTier</c> (Low 0, Mid 1, High 2).</summary>
    public enum AudioTier : byte
    {
        Low = 0,
        Mid = 1,
        High = 2,
    }

    /// <summary>
    /// Voice and bank budget per tier (W2_DESIGN 7.1 "Voices", 7.4, 10.4; A §6.1): real voices 24 / 32 / 48,
    /// real-time synth voices 4 / 8 / 12 (the player's engine + the nearest NPC engines), virtual voices
    /// 128 / 256 / 512, at most 2 real-time aircraft voices, NPC footsteps 2 / 4 / 6, one-shots per second
    /// 12 / 20 / 30. Low bakes the bank at ≤ 22.05 kHz with the reduced variant counts (≈ 3 MB).
    /// </summary>
    public readonly struct VoiceBudget
    {
        public readonly AudioTier Tier;
        public readonly int RealVoices;
        public readonly int SynthVoices;
        public readonly int VirtualVoices;
        public readonly int AircraftVoices;
        public readonly int MaxNpcFootsteps;
        public readonly int MaxOneShotsPerSecond;
        /// <summary>Ambience bed sources playing at once (crossfaded).</summary>
        public readonly int BedVoices;
        /// <summary>True: bake with <see cref="BankSoundInfo.LowVariants"/> and at most 22.05 kHz.</summary>
        public readonly bool LowBank;
        /// <summary>Resident audio cap (MB, ARCHITECTURE 10).</summary>
        public readonly int MemoryCapMb;

        public VoiceBudget(AudioTier tier, int real, int synth, int virt, int aircraft, int npcSteps, int oneShots, int beds,
                           bool lowBank, int capMb)
        {
            Tier = tier;
            RealVoices = real;
            SynthVoices = synth;
            VirtualVoices = virt;
            AircraftVoices = aircraft;
            MaxNpcFootsteps = npcSteps;
            MaxOneShotsPerSecond = oneShots;
            BedVoices = beds;
            LowBank = lowBank;
            MemoryCapMb = capMb;
        }

        public static VoiceBudget For(AudioTier tier)
        {
            switch (tier)
            {
                case AudioTier.Low: return new VoiceBudget(AudioTier.Low, 24, 4, 128, 1, 2, 12, 3, true, 25);
                case AudioTier.High: return new VoiceBudget(AudioTier.High, 48, 12, 512, 2, 6, 30, 4, false, 50);
                default: return new VoiceBudget(AudioTier.Mid, 32, 8, 256, 2, 4, 20, 4, false, 40);
            }
        }

        /// <summary>Thermal step-down (W2_DESIGN 7.1): half the synth voices, at least the player's.</summary>
        public VoiceBudget ThermalStepDown()
        {
            int synth = SynthVoices / 2;
            if (synth < 1) synth = 1;
            int air = AircraftVoices > 1 ? 1 : AircraftVoices;
            return new VoiceBudget(Tier, RealVoices, synth, VirtualVoices, air, MaxNpcFootsteps, MaxOneShotsPerSecond, BedVoices,
                                   LowBank, MemoryCapMb);
        }

        /// <summary>One-shots, beds and the voices left over for clips after synth voices are taken.</summary>
        public int ClipVoices
        {
            get
            {
                int n = RealVoices - SynthVoices - AircraftVoices - BedVoices;
                return n < 4 ? 4 : n;
            }
        }
    }
}
