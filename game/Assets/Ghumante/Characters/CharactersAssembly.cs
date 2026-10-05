// Ghumante.Characters: the player (ARCHITECTURE.md 7.7; W2_DESIGN 6). W2 track D adds the detailed cartoon character
// (Avatar/PlayerAvatar: Core's HumanoidMesher skinned to the 37-bone hum rig, posed by Core's CharacterPoser), the
// vehicles the player drives (Rides/: garage and community fleet, every catalogue class), the ExplorerController (walk,
// jump, namaste, hop on and off any vehicle, ride along as a passenger, calm mode in sacred zones), the controls
// (ControlFrame, ControlMapper, ExplorerInput) and the per-class, orientation-aware chase camera (Cameras/).
namespace Ghumante.Characters
{
    /// <summary>Marker for the <c>Ghumante.Characters</c> assembly.</summary>
    public static class CharactersAssembly
    {
        /// <summary>Assembly name, as declared in the asmdef.</summary>
        public const string Name = "Ghumante.Characters";
    }
}
