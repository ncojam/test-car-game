using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

internal struct VehicleInput
{
    public float Gas;
    public bool KeyboardGas;
    public float Brake;
    public float Steer;
    public bool Handbrake;
    public int GearStep;
    public bool ResumeAutomatic;

    private const float StickDeadzone = 0.12f;
    private const float TriggerDeadzone = 0.05f;

    private static Gamepad ActivePad
    {
        get
        {
            if (Gamepad.current != null && Gamepad.current.enabled)
                return Gamepad.current;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.enabled)
                    return pad;
            return null;
        }
    }

    public static bool InteractPressed => Input.GetKeyDown(KeyCode.E)
        || (ActivePad?.buttonWest.wasPressedThisFrame
            ?? (LinuxPad?.West.wasPressedThisFrame ?? Input.GetKeyDown(KeyCode.JoystickButton2)));

    public static bool ResetLookPressed => ActivePad?.rightStickButton.wasPressedThisFrame
        ?? (LinuxPad?.RightStickClick?.wasPressedThisFrame ?? false);

    public static Vector2 MoveStick => ApplyStickDeadzone(ActivePad?.leftStick.ReadValue()
        ?? LinuxPad?.Move
        ?? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));

    public static Vector2 LookStick
    {
        get
        {
            // A recognized pad can still report an unmapped (always zero) right stick.
            Gamepad pad = ActivePad;
            Vector2 look = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
            if (look.sqrMagnitude > 0f)
                return look;

            look = ApplyStickDeadzone(LinuxPad?.Look ?? Vector2.zero);
            if (look.sqrMagnitude > 0f || !HasLegacyPad())
                return look;

            return ApplyStickDeadzone(new Vector2(Input.GetAxisRaw("StickR X"), -Input.GetAxisRaw("StickR Y")));
        }
    }

    private static Vector2 ApplyStickDeadzone(Vector2 raw)
    {
        float mag = raw.magnitude;
        if (mag < StickDeadzone)
            return Vector2.zero;
        return raw.normalized * Mathf.InverseLerp(StickDeadzone, 1f, Mathf.Min(1f, mag));
    }

    private static float ApplyTriggerDeadzone(float value)
    {
        return value < TriggerDeadzone ? 0f : Mathf.InverseLerp(TriggerDeadzone, 1f, value);
    }

    private static bool signedGas;
    private static bool signedBrake;

    private static float Pedal(float value, ref bool signed)
    {
        if (value < -0.8f)
            signed = true;
        float norm = signed ? Mathf.InverseLerp(-1f, 1f, value) : Mathf.Clamp01(value);
        return ApplyTriggerDeadzone(norm);
    }

    public static VehicleInput Read()
    {
        var input = new VehicleInput();
        Gamepad pad = ActivePad;

        if (pad != null)
        {
            input.Gas = ApplyTriggerDeadzone(pad.rightTrigger.ReadValue());
            input.Brake = ApplyTriggerDeadzone(pad.leftTrigger.ReadValue());
            input.Steer = ApplyStickDeadzone(pad.leftStick.ReadValue()).x;
            input.Handbrake = pad.buttonSouth.isPressed;
            input.ReadShifters(pad.leftShoulder, pad.rightShoulder);
        }
        else if (LinuxPad is LinuxController linux)
        {
            input.Gas = linux.Gas;
            input.Brake = linux.Brake;
            input.Steer = ApplyStickDeadzone(linux.Move).x;
            input.Handbrake = linux.South.isPressed;
            input.ReadShifters(linux.LeftShoulder, linux.RightShoulder);
            if (linux.LeftShoulder == null && linux.RightShoulder == null && HasLegacyPad())
                input.ReadLegacyShifters();
        }
        else if (HasLegacyPad())
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            input.Gas = Pedal(Input.GetAxisRaw("Car RT Windows"), ref signedGas);
            input.Brake = Pedal(Input.GetAxisRaw("Car LT Windows"), ref signedBrake);
#else
            input.Gas = Pedal(Input.GetAxisRaw("Car RT"), ref signedGas);
            input.Brake = Pedal(Input.GetAxisRaw("Car LT"), ref signedBrake);
#endif
            input.Steer = ApplyStickDeadzone(new Vector2(Input.GetAxisRaw("Horizontal"), 0f)).x;
            input.Handbrake = Input.GetKey(KeyCode.JoystickButton0);
            input.ReadLegacyShifters();
        }

        if (input.Gas <= TriggerDeadzone && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)))
        {
            input.KeyboardGas = true;
            input.Gas = 1f;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.Brake = 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.Steer = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.Steer = 1f;

        input.Handbrake |= Input.GetKey(KeyCode.Space);
        return input;
    }

    private void ReadShifters(ButtonControl left, ButtonControl right)
    {
        bool down = left?.wasPressedThisFrame ?? false;
        bool up = right?.wasPressedThisFrame ?? false;
        ResumeAutomatic = (up || down) && left?.isPressed == true && right?.isPressed == true;
        GearStep = (up ? 1 : 0) - (down ? 1 : 0);
    }

    private void ReadLegacyShifters()
    {
        bool down = Input.GetKeyDown(KeyCode.JoystickButton4);
        bool up = Input.GetKeyDown(KeyCode.JoystickButton5);
        ResumeAutomatic = (up || down) && Input.GetKey(KeyCode.JoystickButton4) && Input.GetKey(KeyCode.JoystickButton5);
        GearStep = (up ? 1 : 0) - (down ? 1 : 0);
    }

    private static string legacyDevice;
    private static float nextDeviceCheck;

    private static bool HasLegacyPad()
    {
        if (Time.unscaledTime >= nextDeviceCheck)
        {
            nextDeviceCheck = Time.unscaledTime + 0.5f;
            string device = System.Array.Find(Input.GetJoystickNames(), name => !string.IsNullOrEmpty(name));
            if (device != legacyDevice)
            {
                legacyDevice = device;
                signedGas = signedBrake = false;
            }
        }
        return legacyDevice != null;
    }

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
    private static LinuxController linuxPad;
    private static float nextLinuxDeviceCheck;
#endif

    private static LinuxController LinuxPad
    {
        get
        {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            if (linuxPad != null && linuxPad.Device.added && linuxPad.Device.enabled)
                return linuxPad;

            linuxPad = null;
            if (Time.unscaledTime < nextLinuxDeviceCheck)
                return null;

            nextLinuxDeviceCheck = Time.unscaledTime + 0.5f;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!device.enabled || device.description.interfaceName != "Linux")
                    continue;
                linuxPad = LinuxController.TryCreate(device);
                if (linuxPad != null)
                    break;
            }
            return linuxPad;
#else
            return null;
#endif
        }
    }

    // Input System 1.7 builds Linux device layouts from the reported evdev usages.
    // These controls can exist on an InputDevice that has no Gamepad layout.
    private sealed class LinuxController
    {
        public InputDevice Device { get; }
        public ButtonControl West { get; }
        public ButtonControl South { get; }
        public ButtonControl RightStickClick { get; }
        public ButtonControl LeftShoulder { get; }
        public ButtonControl RightShoulder { get; }
        public Vector2 Move => leftStick?.ReadValue() ?? new Vector2(leftX.ReadValue(), leftY.ReadValue());
        public Vector2 Look => rightStick?.ReadValue()
            ?? new Vector2(rightX?.ReadValue() ?? 0f, -(rightY?.ReadValue() ?? 0f));
        public float Gas => Pedal(gas.ReadValue(), ref gasSigned);
        public float Brake => Pedal(brake.ReadValue(), ref brakeSigned);

        private readonly StickControl leftStick;
        private readonly AxisControl leftX;
        private readonly AxisControl leftY;
        private readonly StickControl rightStick;
        private readonly AxisControl rightX;
        private readonly AxisControl rightY;
        private readonly AxisControl gas;
        private readonly AxisControl brake;
        private bool gasSigned;
        private bool brakeSigned;

        private LinuxController(InputDevice device, StickControl stick, AxisControl x, AxisControl y,
            AxisControl rx, AxisControl ry, AxisControl gasAxis, AxisControl brakeAxis,
            ButtonControl west, ButtonControl south)
        {
            Device = device;
            leftStick = stick;
            leftX = x;
            leftY = y;
            rightStick = device.TryGetChildControl("rightStick") as StickControl;
            rightX = rx;
            rightY = ry;
            gas = gasAxis;
            brake = brakeAxis;
            West = west;
            South = south;
            RightStickClick = device.TryGetChildControl("ThumbRight") as ButtonControl;
            LeftShoulder = device.TryGetChildControl("TriggerLeft") as ButtonControl;
            RightShoulder = device.TryGetChildControl("TriggerRight") as ButtonControl;
            gasSigned = gas.ReadValue() < -0.8f;
            brakeSigned = brake.ReadValue() < -0.8f;
        }

        public static LinuxController TryCreate(InputDevice device)
        {
            var stick = device.TryGetChildControl("Stick") as StickControl;
            var x = device.TryGetChildControl("X") as AxisControl;
            if (x is ButtonControl)
                x = null;
            var y = device.TryGetChildControl("Y") as AxisControl;
            if (y is ButtonControl)
                y = null;
            var rx = device.TryGetChildControl("RotateX") as AxisControl;
            var ry = device.TryGetChildControl("RotateY") as AxisControl;
            var gas = device.TryGetChildControl("Gas") as AxisControl
                ?? device.TryGetChildControl("RotateZ") as AxisControl;
            var brake = device.TryGetChildControl("Brake") as AxisControl
                ?? device.TryGetChildControl("Z") as AxisControl;
            var west = device.TryGetChildControl("X") as ButtonControl;
            var south = device.TryGetChildControl("A") as ButtonControl;
            if ((stick == null && (x == null || y == null))
                || gas == null || brake == null || west == null || south == null)
                return null;

            return new LinuxController(device, stick, x, y, rx, ry, gas, brake, west, south);
        }
    }
}
