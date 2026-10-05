// Ghumante.Save: Local save storage (atomic writes, rotating backups) and cloud-save adapters (ARCHITECTURE.md 7.10).
// M0 has LocalSaveStore (settings persistence) and SettingsChoices; cloud adapters arrive in M1+.
namespace Ghumante.Save
{
    /// <summary>Marker for the <c>Ghumante.Save</c> assembly.</summary>
    public static class SaveAssembly
    {
        /// <summary>Assembly name, as declared in the asmdef.</summary>
        public const string Name = "Ghumante.Save";
    }
}
