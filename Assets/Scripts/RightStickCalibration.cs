using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

internal sealed class RightStickCalibration
{
    private const string Prefs = "Car.RightStick.";
    private const string LegacyPrefix = "Car Look Axis ";
    private const float CaptureThreshold = 0.38f;

    private enum Stage { Idle, Center, Right, Release, Up }

    private sealed class Axis
    {
        public string DeviceLayout;
        public string Name;
        public AxisControl Control;
        public float Center;
        public float Direction;

        public bool TryRead(out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(Name))
                return false;
            if (string.IsNullOrEmpty(DeviceLayout))
            {
                value = Input.GetAxisRaw(Name);
                return true;
            }

            if (Control == null || !Control.device.added || !Control.device.enabled)
            {
                Control = null;
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (device.enabled && device.layout == DeviceLayout
                        && device.TryGetChildControl(Name) is AxisControl axis)
                    {
                        Control = axis;
                        break;
                    }
                }
            }
            if (Control == null)
                return false;
            value = Control.ReadValue();
            return true;
        }

        public string Label => string.IsNullOrEmpty(DeviceLayout) ? Name : $"{DeviceLayout}/{Name}";

        public static Axis Load(string key)
        {
            return new Axis
            {
                Name = PlayerPrefs.GetString(key + ".Name"),
                DeviceLayout = PlayerPrefs.GetString(key + ".Device"),
                Center = PlayerPrefs.GetFloat(key + ".Center"),
                Direction = PlayerPrefs.GetFloat(key + ".Direction", 1f)
            };
        }

        public void Save(string key)
        {
            PlayerPrefs.SetString(key + ".Name", Name ?? "");
            PlayerPrefs.SetString(key + ".Device", DeviceLayout ?? "");
            PlayerPrefs.SetFloat(key + ".Center", Center);
            PlayerPrefs.SetFloat(key + ".Direction", Direction);
        }
    }

    private readonly List<Axis> candidates = new List<Axis>();
    private Axis x = Axis.Load(Prefs + "X");
    private Axis y = Axis.Load(Prefs + "Y");
    private Axis selectedX;
    private Stage stage;
    private float sampleAfter;

    public string Prompt { get; private set; }

    public Vector2 Read()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            x = new Axis();
            y = new Axis();
            x.Save(Prefs + "X");
            y.Save(Prefs + "Y");
            PlayerPrefs.Save();
            Finish();
        }
        if (Input.GetKeyDown(KeyCode.F8))
        {
            stage = Stage.Center;
            sampleAfter = Time.unscaledTime + 0.65f;
            Prompt = "Release the right stick to start calibration";
        }

        if (stage != Stage.Idle)
        {
            Advance();
            return Vector2.zero;
        }

        if (!x.TryRead(out float horizontal) || !y.TryRead(out float vertical))
            return VehicleInput.LookStick;

        horizontal = (horizontal - x.Center) * x.Direction;
        vertical = (vertical - y.Center) * y.Direction;
        Vector2 stick = new Vector2(horizontal, vertical);
        float magnitude = stick.magnitude;
        return magnitude > 0.12f ? stick / magnitude * Mathf.InverseLerp(0.12f, 1f, magnitude) : Vector2.zero;
    }

    private void Advance()
    {
        if (stage == Stage.Center && Time.unscaledTime >= sampleAfter)
        {
            SampleCenters();
            stage = Stage.Right;
            Prompt = "Push the right stick fully RIGHT";
        }
        else if (stage == Stage.Right)
        {
            selectedX = ReadMovedAxis(null);
            if (selectedX != null)
            {
                stage = Stage.Release;
                Prompt = "Release the right stick";
            }
        }
        else if (stage == Stage.Release && selectedX.TryRead(out float position)
            && Mathf.Abs(position - selectedX.Center) < 0.18f)
        {
            stage = Stage.Up;
            Prompt = "Push the right stick fully UP";
        }
        else if (stage == Stage.Up)
        {
            Axis selectedY = ReadMovedAxis(selectedX);
            if (selectedY != null)
            {
                x = selectedX;
                y = selectedY;
                x.Save(Prefs + "X");
                y.Save(Prefs + "Y");
                PlayerPrefs.Save();
                Debug.Log($"Driving camera right stick: X = {x.Label}, Y = {y.Label}");
                Finish();
            }
        }
    }

    private void SampleCenters()
    {
        candidates.Clear();
        foreach (InputDevice device in InputSystem.devices)
        {
            if (!device.enabled || !(device is Gamepad || device is Joystick || device.description.interfaceName == "Linux"))
                continue;
            foreach (InputControl control in device.allControls)
            {
                if (control is AxisControl axis && !(axis is ButtonControl) && !axis.synthetic)
                {
                    candidates.Add(new Axis
                    {
                        DeviceLayout = device.layout,
                        Name = axis.path.Substring(device.path.Length + 1),
                        Control = axis,
                        Center = axis.ReadValue()
                    });
                }
            }
        }

        if (System.Array.Exists(Input.GetJoystickNames(), name => !string.IsNullOrEmpty(name)))
        {
            for (int i = 1; i <= 12; i++)
            {
                string name = LegacyPrefix + i;
                candidates.Add(new Axis { Name = name, Center = Input.GetAxisRaw(name) });
            }
        }
    }

    private Axis ReadMovedAxis(Axis ignore)
    {
        Axis selected = null;
        float strongest = CaptureThreshold;
        foreach (Axis axis in candidates)
        {
            if (axis == ignore || !axis.TryRead(out float value))
                continue;
            float delta = value - axis.Center;
            if (Mathf.Abs(delta) <= strongest)
                continue;
            strongest = Mathf.Abs(delta);
            selected = axis;
            selected.Direction = Mathf.Sign(delta);
        }
        return selected;
    }

    private void Finish()
    {
        candidates.Clear();
        selectedX = null;
        stage = Stage.Idle;
        Prompt = null;
    }
}
