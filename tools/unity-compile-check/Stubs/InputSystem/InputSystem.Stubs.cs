// Declaration stubs for the part of the Input System package (com.unity.inputsystem, the version pinned in
// game/Packages/manifest.json) that game/Assets/Ghumante uses. Same rules as Stubs/UnityEditor: exact
// signatures, used members only, verified by the API audit against the package source
// (Unity-Technologies/InputSystem at the manifest's version tag). With this stub, generate.py also sets the
// versionDefines our asmdefs declare for the package (GHUMANTE_INPUT_SYSTEM), so that code is compiled too.
#pragma warning disable 1591
using UnityEngine.InputSystem.Controls;

namespace UnityEngine.InputSystem
{
    public abstract class InputControl
    {
    }

    public abstract class InputControl<TValue> : InputControl
        where TValue : struct
    {
        public TValue ReadValue() { throw null; }
    }

    public class InputDevice : InputControl
    {
        public bool enabled { get { throw null; } }
        public bool added { get { throw null; } }
    }

    public class Sensor : InputDevice
    {
    }

    public class Accelerometer : Sensor
    {
        public Vector3Control acceleration { get { throw null; } protected set { } }
        public static Accelerometer current { get { throw null; } private set { } }
    }

    public class GravitySensor : Sensor
    {
        public Vector3Control gravity { get { throw null; } protected set { } }
        public static GravitySensor current { get { throw null; } private set { } }
    }

    public static partial class InputSystem
    {
        public static void EnableDevice(InputDevice device) { throw null; }
        public static void DisableDevice(InputDevice device, bool keepSendingEvents = false) { throw null; }
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class Vector3Control : InputControl<Vector3>
    {
    }
}
