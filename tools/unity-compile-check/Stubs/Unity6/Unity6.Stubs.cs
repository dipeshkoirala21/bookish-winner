// Declarations of Unity 6 runtime APIs that are missing from the UnityEngine.Modules 2021.3.33 reference
// assemblies, for game code that uses them. Policy: keep this file (nearly) empty. Prefer APIs that
// exist in both; when a Unity-6-only API is genuinely needed, declare it here in a NEW type only (C#
// cannot add members to existing UnityEngine types from outside), and the API audit verifies it.
//
// Currently empty: the M0 runtime code needs no Unity-6-only API. The Unity-6-only USS property
// -unity-text-generator is used from USS, not C#, and is checked by check_uss.py.
namespace Ghumante.CompileCheck.Unity6
{
    internal static class Placeholder
    {
    }
}
