using UnityEngine;
using UnityEngine.InputSystem;

internal struct VehicleInput
{
    public float Gas;
    public float Brake;
    public float Steer;
    public bool Handbrake;

    private const float StickDeadzone = 0.12f;
    private const float TriggerDeadzone = 0.05f;

    private static Gamepad ActivePad
    {
        get
        {
            if (Gamepad.current != null)
                return Gamepad.current;
            return Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        }
    }

    public static bool InteractPressed
    {
        get
        {
            Gamepad pad = ActivePad;
            return Input.GetKeyDown(KeyCode.E)
                || (pad != null && pad.buttonWest.wasPressedThisFrame)
                || (pad == null && Input.GetKeyDown(KeyCode.JoystickButton2));
        }
    }

    public static Vector2 MoveStick
    {
        get
        {
            Gamepad pad = ActivePad;
            if (pad != null)
                return ApplyStickDeadzone(pad.leftStick.ReadValue());

            return ApplyStickDeadzone(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
        }
    }

    public static Vector2 LookStick
    {
        get
        {
            Gamepad pad = ActivePad;
            if (pad != null)
                return ApplyStickDeadzone(pad.rightStick.ReadValue());

            if (!HasLegacyPad())
                return Vector2.zero;

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

    private static float Pedal(string axis, ref bool signed)
    {
        float value = Input.GetAxisRaw(axis);
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
        }
        else if (HasLegacyPad())
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            input.Gas = Pedal("Car RT Windows", ref signedGas);
            input.Brake = Pedal("Car LT Windows", ref signedBrake);
#else
            input.Gas = Pedal("Car RT", ref signedGas);
            input.Brake = Pedal("Car LT", ref signedBrake);
#endif
            input.Steer = ApplyStickDeadzone(new Vector2(Input.GetAxisRaw("Horizontal"), 0f)).x;
            input.Handbrake = Input.GetKey(KeyCode.JoystickButton0);
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            input.Gas = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.Brake = 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.Steer = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.Steer = 1f;

        input.Handbrake |= Input.GetKey(KeyCode.Space);
        return input;
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
}