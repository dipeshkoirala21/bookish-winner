// Ghumante.Audio: procedural sound for the whole game (W2_DESIGN 7; ARCHITECTURE.md 7.1): the baked bank, real-time
// engine and aircraft voices, clip voices, ambience, listener effects and occlusion. The DSP itself is engine-free
// in Ghumante.Core.Synth; this assembly is the Unity glue (AudioDirector is the entry point).
namespace Ghumante.Audio
{
    /// <summary>Marker for the <c>Ghumante.Audio</c> assembly.</summary>
    public static class AudioAssembly
    {
        /// <summary>Assembly name, as declared in the asmdef.</summary>
        public const string Name = "Ghumante.Audio";
    }
}
