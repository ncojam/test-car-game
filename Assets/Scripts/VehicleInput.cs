using UnityEngine;
using UnityEngine.InputSystem;

internal struct VehicleInput
{
    public float Gas;
    public float Brake;
    public float Steer;
    public bool Handbrake;

    public static bool InteractPressed => Input.GetKeyDown(KeyCode.E)
        || (Gamepad.current?.buttonWest.wasPressedThisFrame ?? false)
        || (Gamepad.current == null && Input.GetKeyDown(KeyCode.JoystickButton2));

    public static Vector2 LookStick => Gamepad.current?.rightStick.ReadValue()
        ?? new Vector2(Input.GetAxisRaw("StickR X"), -Input.GetAxisRaw("StickR Y"));

    // The legacy Linux Xbox/Steam Input mapping can expose signed trigger axes.
    private static bool signedGas;
    private static bool signedBrake;

    private static float Pedal(string axis, ref bool signed)
    {
        float value = Input.GetAxisRaw(axis);
        if (value < -0.8f)
            signed = true;
        return signed ? Mathf.InverseLerp(-1f, 1f, value) : Mathf.Clamp01(value);
    }

    public static VehicleInput Read()
    {
        var input = new VehicleInput();
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            input.Gas = pad.rightTrigger.ReadValue();
            input.Brake = pad.leftTrigger.ReadValue();
            input.Steer = pad.leftStick.ReadValue().x;
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
            input.Steer = Input.GetAxisRaw("Horizontal");
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
