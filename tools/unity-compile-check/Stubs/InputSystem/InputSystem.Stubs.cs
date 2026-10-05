// Declaration stubs for the part of the Input System package (com.unity.inputsystem, the version pinned in
// game/Packages/manifest.json) that game/Assets/Ghumante uses. Same rules as Stubs/UnityEditor: exact
// signatures, used members only, verified by the API audit against the package source
// (Unity-Technologies/InputSystem at the manifest's version tag). With this stub, generate.py also sets the
// versionDefines our asmdefs declare for the package (GHUMANTE_INPUT_SYSTEM), so that code is compiled too.
#pragma warning disable 1591
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityEngine.InputSystem
{
    public abstract class InputControl
    {
        public InputDevice device { get { throw null; } }
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

    public class Pointer : InputDevice
    {
        public Vector2Control position { get { throw null; } protected set { } }
        public DeltaControl delta { get { throw null; } protected set { } }
        public ButtonControl press { get { throw null; } protected set { } }
        public static Pointer current { get { throw null; } internal set { } }
    }

    public class Mouse : Pointer
    {
        public DeltaControl scroll { get { throw null; } protected set { } }
        public ButtonControl leftButton { get { throw null; } protected set { } }
        public ButtonControl rightButton { get { throw null; } protected set { } }
        public ButtonControl middleButton { get { throw null; } protected set { } }
        public new static Mouse current { get { throw null; } private set { } }
    }

    public class Touchscreen : Pointer
    {
        public TouchControl primaryTouch { get { throw null; } protected set { } }
        public ReadOnlyArray<TouchControl> touches { get { throw null; } protected set { } }
        public new static Touchscreen current { get { throw null; } internal set { } }
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get { throw null; } private set { } }
        public KeyControl wKey { get { throw null; } }
        public KeyControl aKey { get { throw null; } }
        public KeyControl sKey { get { throw null; } }
        public KeyControl dKey { get { throw null; } }
        public KeyControl qKey { get { throw null; } }
        public KeyControl eKey { get { throw null; } }
        public KeyControl rKey { get { throw null; } }
        public KeyControl tKey { get { throw null; } }
        public KeyControl pKey { get { throw null; } }
        public KeyControl f3Key { get { throw null; } }
        public KeyControl leftShiftKey { get { throw null; } }
        public KeyControl rightShiftKey { get { throw null; } }
        public KeyControl leftBracketKey { get { throw null; } }
        public KeyControl rightBracketKey { get { throw null; } }
        public KeyControl escapeKey { get { throw null; } }
    }

    public class Gamepad : InputDevice
    {
        public ButtonControl buttonEast { get { throw null; } protected set { } }
        public static Gamepad current { get { throw null; } private set { } }
    }

    public enum InputActionType
    {
        Value = 0,
        Button = 1,
        PassThrough = 2,
    }

    public sealed class InputAction : System.IDisposable
    {
        public string name { get { throw null; } }
        public InputControl activeControl { get { throw null; } }
        public TValue ReadValue<TValue>() where TValue : struct { throw null; }
        public bool IsPressed() { throw null; }
        public bool WasPressedThisFrame() { throw null; }
        public void Enable() { throw null; }
        public void Disable() { throw null; }
        public void Dispose() { throw null; }
    }

    public sealed class InputActionMap : System.IDisposable
    {
        public InputActionMap(string name) { }
        public bool enabled { get { throw null; } }
        public void Enable() { throw null; }
        public void Disable() { throw null; }
        public void Dispose() { throw null; }
    }

    public static class InputActionSetupExtensions
    {
        public static InputAction AddAction(this InputActionMap map, string name, InputActionType type = default, string binding = null,
            string interactions = null, string processors = null, string groups = null, string expectedControlLayout = null) { throw null; }

        public static BindingSyntax AddBinding(this InputAction action, string path, string interactions = null,
            string processors = null, string groups = null) { throw null; }

        public static CompositeSyntax AddCompositeBinding(this InputAction action, string composite,
            string interactions = null, string processors = null) { throw null; }

        public struct BindingSyntax
        {
        }

        public struct CompositeSyntax
        {
            public CompositeSyntax With(string name, string binding, string groups = null, string processors = null) { throw null; }
        }
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

    public class Vector2Control : InputControl<Vector2>
    {
    }

    public class DeltaControl : Vector2Control
    {
    }

    public class AxisControl : InputControl<float>
    {
    }

    public class ButtonControl : AxisControl
    {
        public bool isPressed { get { throw null; } }
        public bool wasPressedThisFrame { get { throw null; } }
        public bool wasReleasedThisFrame { get { throw null; } }
    }

    public class KeyControl : ButtonControl
    {
    }

    public class TouchPressControl : ButtonControl
    {
    }

    public class TouchControl : InputControl<TouchState>
    {
        public TouchPressControl press { get { throw null; } set { } }
        public Vector2Control position { get { throw null; } set { } }
        public DeltaControl delta { get { throw null; } set { } }
        public bool isInProgress { get { throw null; } }
    }
}

namespace UnityEngine.InputSystem.LowLevel
{
    public struct TouchState
    {
    }
}

namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<TValue> : IReadOnlyList<TValue>
    {
        public int Count { get { throw null; } }
        public TValue this[int index] { get { throw null; } }
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() { throw null; }
        IEnumerator IEnumerable.GetEnumerator() { throw null; }
    }
}
