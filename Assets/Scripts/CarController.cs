using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [SerializeField] private bool startDriving;
    [SerializeField] private Material tireSmokeMaterial;

    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;
    public Transform frontLeftWheelTransform;
    public Transform frontRightWheelTransform;
    public Transform rearLeftWheelTransform;
    public Transform rearRightWheelTransform;
    public AudioSource frontLeftSFX;
    public AudioSource frontRightSFX;
    public AudioSource rearLeftSFX;
    public AudioSource rearRightSFX;

    public Rigidbody carRigidbody;
    [Range(0, 2)] public int wd; // 0 = front, 1 = rear, 2 = all-wheel drive.
    public float maxSteeringAngle = 30f;
    public float motorForce = 1200f;
    [SerializeField] private float frontBrakeTorque = 4200f;
    [SerializeField] private float rearHandbrakeTorque = 4400f;
    [SerializeField] private float centerOfMass = 0.57f;
    [SerializeField] private float antiRollStiffness = 2600f;

    [Header("Transmission")]
    [Tooltip("Use this car's ratios, final drive and shift RPM instead of the built-in arcade setup.")]
    [SerializeField] private bool useCustomGearing;
    [SerializeField] private float[] gearRatios = (float[])ArcadeRatios.Clone();
    [SerializeField, Min(1f)] private float finalDrive = ArcadeFinalDrive;
    [SerializeField, Range(4500f, 6300f)] private float fullThrottleShiftRpm = ArcadeShiftRpm;
    private static readonly float[] ArcadeRatios = { 3.6f, 2.35f, 1.65f, 1.23f, 0.95f, 0.75f };
    private const float ArcadeFinalDrive = 5.2f;
    private const float ArcadeShiftRpm = 5950f;
    private const float ShiftDuration = 0.22f;
    private const float DriftUpshiftRpm = 6400f;
    private const float DriftDownshiftRpm = 4500f;
    private const float DriftEntryWindow = 0.75f;
    private int currentGear;
    public AudioSource gear_switch;
    public AudioClip gear_up;
    public AudioClip gear_down;

    public const float IdleRpm = 900f;
    public const float RedlineRpm = 7000f;
    public float EngineRpm { get; private set; } = IdleRpm;
    public float EngineLoad => CanDrive ? poweredThrottle * (1f - shiftTime / ShiftDuration) : 0f;
    public bool IsDriving => driving;
    public float DriftIntensity => driftAmount;
    public int GearNumber => currentGear + 1;
    public bool ManualGears { get; private set; }
    public float SpeedKph => carRigidbody != null ? carRigidbody.velocity.magnitude * 3.6f : 0f;
    public float Braking => CanDrive ? Mathf.Max(brake, handbrake ? 0.75f : 0f) : 0f;
    public float TurnRate => carRigidbody != null ? Vector3.Dot(carRigidbody.angularVelocity, transform.up) : 0f;

    private bool CanDrive => driving && !GameManager.paused;
    private bool driving;
    private bool handbrake;
    private float throttle;
    private float brake;
    private float steering;
    private float shiftTime;
    private int pendingShift;
    private float poweredThrottle;
    private float driftEntryTime;
    private float driftAmount;
    private float drivenWheelRadius;
    private float[] activeRatios;
    private float activeFinalDrive;
    private float activeShiftRpm;
    private Wheel[] wheels;
    private TireMarks tireMarks;

    private void Awake()
    {
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();

        tireMarks = new TireMarks(gameObject.scene);
        carRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        carRigidbody.centerOfMass = new Vector3(0f, centerOfMass, 0f);
        frontLeftWheelCollider.ConfigureVehicleSubsteps(8f, 8, 4);

        wheels = new[]
        {
            new Wheel(frontLeftWheelCollider, frontLeftWheelTransform, frontLeftSFX, true, wd != 1),
            new Wheel(frontRightWheelCollider, frontRightWheelTransform, frontRightSFX, true, wd != 1),
            new Wheel(rearLeftWheelCollider, rearLeftWheelTransform, rearLeftSFX, false, wd != 0),
            new Wheel(rearRightWheelCollider, rearRightWheelTransform, rearRightSFX, false, wd != 0),
        };

        activeRatios = useCustomGearing && gearRatios != null && gearRatios.Length > 0 ? gearRatios : ArcadeRatios;
        activeFinalDrive = useCustomGearing ? Mathf.Max(1f, finalDrive) : ArcadeFinalDrive;
        activeShiftRpm = useCustomGearing ? Mathf.Clamp(fullThrottleShiftRpm, 4500f, 6300f) : ArcadeShiftRpm;

        float radiusSum = 0f;
        int drivenWheels = 0;
        foreach (Wheel wheel in wheels)
        {
            if (wheel.IsDriven)
            {
                radiusSum += wheel.Collider.radius;
                drivenWheels++;
            }
            ConfigureSuspension(wheel.Collider);
            wheel.Smoke = new TireSmoke(wheel.Collider.transform, tireSmokeMaterial);
            wheel.SamplePose(carRigidbody);
            StartWheelSound(wheel.Sound);
        }

        drivenWheelRadius = Mathf.Max(0.01f, radiusSum / Mathf.Max(1, drivenWheels));
        SetDriving(startDriving);
    }

    private void ConfigureSuspension(WheelCollider wheel)
    {
        wheel.suspensionDistance = 0.28f;
        wheel.forceAppPointDistance = 0.29f;
        wheel.wheelDampingRate = 0.55f;

        float sprungMass = Mathf.Max(wheel.sprungMass, carRigidbody.mass * 0.16f);
        JointSpring suspension = wheel.suspensionSpring;
        suspension.spring = sprungMass * 82f;
        suspension.damper = 2f * Mathf.Sqrt(suspension.spring * sprungMass) * 0.68f;
        suspension.targetPosition = 0.5f;
        wheel.suspensionSpring = suspension;
    }

    private VehicleInput input;

    private void Update()
    {
        input = CanDrive ? VehicleInput.Read() : default;
        if (!CanDrive || input.ResumeAutomatic)
        {
            pendingShift = 0;
            if (input.ResumeAutomatic)
                ManualGears = false;
        }
        else if (input.GearStep != 0)
        {
            ManualGears = true;
            pendingShift = input.GearStep;
        }
    }

    public void SetDriving(bool active)
    {
        input = default;
        driving = active;
        ManualGears = false;
        pendingShift = currentGear = 0;
        shiftTime = driftEntryTime = driftAmount = poweredThrottle = 0f;
        if (active)
            carRigidbody.WakeUp();
        throttle = brake = steering = 0f;
        handbrake = false;
        if (!active && wheels != null)
            Park();
    }

    private void FixedUpdate()
    {
        float step = Time.fixedDeltaTime;
        float speed = carRigidbody.velocity.magnitude;
        foreach (Wheel wheel in wheels)
        {
            // Physics poses update on fixed ticks. Render wheels in the interpolated chassis frame.
            wheel.SamplePose(carRigidbody);
            wheel.Grounded = wheel.Collider.GetGroundHit(out wheel.Contact);
        }

        if (CanDrive)
            Drive(speed, step);
        else
            Park();

        ApplyAntiRoll(wheels[0], wheels[1]);
        ApplyAntiRoll(wheels[2], wheels[3]);

        foreach (Wheel wheel in wheels)
            UpdateTireEffects(wheel, speed, step);
    }

    private void Drive(float speed, float step)
    {
        if (input.Gas > 0f && carRigidbody.IsSleeping())
            carRigidbody.WakeUp();
        handbrake = input.Handbrake;
        // Ease in the binary keyboard pedal at low speed. RT keeps its full analog range.
        bool assistedStart = input.KeyboardGas && input.Brake < 0.15f;
        float gas = assistedStart ? Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(1f, 11f, speed)) : input.Gas;
        float pedalRate = gas > throttle ? (assistedStart ? 1.8f : 4.5f) : 10f;
        throttle = Mathf.MoveTowards(throttle, gas, step * pedalRate);
        brake = Mathf.MoveTowards(brake, input.Brake, step * 9f);

        Vector3 velocity = carRigidbody.velocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, transform.up);
        float roadWheelRpm = planarVelocity.magnitude * 60f / (2f * Mathf.PI * drivenWheelRadius);
        UpdateDrift(planarVelocity, step);
        float steeringAssist = Mathf.Max(driftAmount, 0.85f * driftEntryTime / DriftEntryWindow);
        steering = Mathf.MoveTowards(steering, input.Steer, step * Mathf.Lerp(4f, 15f, steeringAssist));
        UpdateGear(roadWheelRpm, step);

        // With both pedals down at low speed, the front brakes hold the car while the rear axle can spin.
        bool rearDriveOnGround = wd != 0 && !handbrake && (wheels[2].Grounded || wheels[3].Grounded);
        float burnout = rearDriveOnGround
            ? Mathf.InverseLerp(0.15f, 0.65f, brake) * Mathf.InverseLerp(0.45f, 0.85f, throttle)
                * (1f - Mathf.InverseLerp(4f, 8f, speed)) : 0f;
        float launchBurnout = rearDriveOnGround && currentGear == 0 && !input.KeyboardGas
            ? Mathf.InverseLerp(0.90f, 0.98f, throttle) * (1f - Mathf.InverseLerp(4f, 10f, speed))
                * (1f - brake) : 0f;
        float rearSpinAssist = Mathf.Max(burnout, launchBurnout);
        if (burnout > 0f && (wheels[0].Grounded || wheels[1].Grounded))
        {
            // Locked front wheels act as a pivot at low speed. Steering controls the yaw of the burnout.
            float yawAcceleration = (steering * 1.2f - TurnRate) * 6f * burnout;
            carRigidbody.AddTorque(transform.up * yawAcceleration, ForceMode.Acceleration);
        }
        float drivingThrottle = throttle * (1f - brake);
        poweredThrottle = Mathf.Lerp(drivingThrottle, throttle, burnout);

        float highSpeedSteering = Mathf.Lerp(0.32f, 1f, steeringAssist);
        float steerAngle = steering * maxSteeringAngle * Mathf.Lerp(1f, highSpeedSteering, Mathf.InverseLerp(7f, 33f, speed));
        float launch = Mathf.InverseLerp(0.88f, 1f, throttle) * (1f - Mathf.InverseLerp(4f, 16f, speed))
            * (1f - brake);
        float ratio = Ratio(currentGear);
        float torquePerThrottle = motorForce * ratio * Mathf.Lerp(0.62f, 1.06f, Mathf.Max(launch, rearSpinAssist))
            * Mathf.Lerp(1f, 0.65f, shiftTime / ShiftDuration) * Mathf.Lerp(1f, 1.42f, driftAmount);

        float wheelRpmSum = 0f;
        int groundedDrivenWheels = 0;
        foreach (Wheel wheel in wheels)
        {
            WheelCollider collider = wheel.Collider;
            collider.steerAngle = wheel.IsFront ? steerAngle : 0f;
            float wheelThrottle = wheel.IsFront ? drivingThrottle : (handbrake ? 0f : poweredThrottle);
            collider.motorTorque = wheel.IsDriven ? torquePerThrottle * wheelThrottle : 0f;
            collider.brakeTorque = wheel.IsFront ? frontBrakeTorque * brake : (handbrake ? rearHandbrakeTorque : 0f);

            WheelFrictionCurve forward = collider.forwardFriction;
            float launchGrip = wheel.IsFront ? Mathf.Lerp(1.32f, 0.78f, launch)
                : Mathf.Lerp(Mathf.Lerp(1.32f, 0.78f, launch), 0.48f, rearSpinAssist);
            forward.stiffness = wheel.IsDriven ? launchGrip : 1.32f;
            collider.forwardFriction = forward;

            WheelFrictionCurve sideways = collider.sidewaysFriction;
            float rearSlide = Mathf.Max(driftAmount, handbrake ? 1f : burnout);
            sideways.stiffness = wheel.IsFront ? 1.46f : Mathf.Lerp(1.65f, 1.04f, rearSlide);
            collider.sidewaysFriction = sideways;

            if (wheel.IsDriven && wheel.Grounded)
            {
                wheelRpmSum += Mathf.Abs(collider.rpm);
                groundedDrivenWheels++;
            }
        }

        float spinRpm = groundedDrivenWheels > 0 ? Mathf.Max(0f, wheelRpmSum / groundedDrivenWheels - roadWheelRpm) : 0f;
        float spinLimit = Mathf.Lerp(420f, RedlineRpm, rearSpinAssist);
        float coupledRpm = roadWheelRpm * ratio * activeFinalDrive
            + Mathf.Min(spinLimit, spinRpm * 0.62f * ratio * activeFinalDrive) * poweredThrottle;
        float launchFlare = poweredThrottle * 1350f * (1f - Mathf.InverseLerp(0.4f, 4.8f, speed));
        // Allow clutch slip under power to keep the engine in its useful range during a slide.
        float driftThrottle = Mathf.InverseLerp(0.12f, 0.5f, poweredThrottle);
        float driftClutch = driftAmount * driftThrottle * (handbrake ? 0f : 1f);
        float driftRpm = Mathf.Lerp(4900f, 5700f, poweredThrottle)
            * Mathf.Lerp(1f, 0.8f, shiftTime / ShiftDuration);
        float targetRpm = Mathf.Max(IdleRpm + launchFlare, coupledRpm);
        targetRpm = Mathf.Lerp(targetRpm, Mathf.Max(targetRpm, driftRpm), driftClutch);
        targetRpm = Mathf.Lerp(targetRpm, RedlineRpm, rearSpinAssist);
        targetRpm = Mathf.Clamp(targetRpm, IdleRpm, RedlineRpm);
        EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-step * (shiftTime > 0f ? 14f : 9f)));
    }

    private void UpdateDrift(Vector3 planarVelocity, float step)
    {
        float speed = planarVelocity.magnitude;
        bool axlesGrounded = (wheels[0].Grounded || wheels[1].Grounded)
            && (wheels[2].Grounded || wheels[3].Grounded);
        float lateralFraction = Mathf.Abs(Vector3.Dot(planarVelocity, transform.right)) / Mathf.Max(1f, speed);
        bool turning = speed > 5f && Mathf.Abs(input.Steer) > 0.18f;
        bool rearSliding = (wheels[2].Grounded && Mathf.Abs(wheels[2].Contact.sidewaysSlip) > 0.4f)
            || (wheels[3].Grounded && Mathf.Abs(wheels[3].Contact.sidewaysSlip) > 0.4f);
        bool powerOversteer = wd != 0 && throttle > 0.65f && lateralFraction > 0.14f && rearSliding;
        bool entry = axlesGrounded && turning && (handbrake || powerOversteer);
        driftEntryTime = entry ? DriftEntryWindow : Mathf.Max(0f, driftEntryTime - step);
        bool driftActive = axlesGrounded && (driftEntryTime > 0f || driftAmount > 0.1f);
        float target = driftActive
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.19f, lateralFraction))
                * Mathf.InverseLerp(3f, 5f, speed) * Mathf.InverseLerp(0.06f, 0.2f, throttle)
                * (1f - Mathf.InverseLerp(0.07f, 0.2f, brake)) : 0f;
        float blend = 1f - Mathf.Exp(-step * (target > driftAmount ? 12f : 3.5f));
        driftAmount = Mathf.Lerp(driftAmount, target, blend);

        if (axlesGrounded && driftAmount > 0.01f && !handbrake)
        {
            // The stick guides rotation. At larger slip angles, help the nose follow the slide.
            float slipAngle = Vector3.SignedAngle(transform.forward, planarVelocity, transform.up);
            float spinCorrection = Mathf.Sign(slipAngle) * Mathf.InverseLerp(28f, 53f, Mathf.Abs(slipAngle));
            float targetYawRate = steering * 1.35f + spinCorrection * 1.6f;
            float yawAcceleration = Mathf.Clamp((targetYawRate - TurnRate) * 4.5f, -6f, 6f) * driftAmount;
            carRigidbody.AddTorque(transform.up * yawAcceleration, ForceMode.Acceleration);
        }
    }

    private void Park()
    {
        foreach (Wheel wheel in wheels)
        {
            wheel.Collider.motorTorque = 0f;
            wheel.Collider.brakeTorque = 4200f;
            wheel.Collider.steerAngle = 0f;
        }
        EngineRpm = IdleRpm;
        poweredThrottle = driftAmount = driftEntryTime = 0f;
    }

    private void ApplyAntiRoll(Wheel left, Wheel right)
    {
        float lift = (left.Travel - right.Travel) * antiRollStiffness;
        if (left.Grounded)
            carRigidbody.AddForceAtPosition(-left.Collider.transform.up * lift, left.Collider.transform.position);
        if (right.Grounded)
            carRigidbody.AddForceAtPosition(right.Collider.transform.up * lift, right.Collider.transform.position);
    }

    private float Ratio(int gear) => Mathf.Max(0.1f, activeRatios[gear]);

    private void UpdateGear(float roadWheelRpm, float step)
    {
        shiftTime = Mathf.Max(0f, shiftTime - step);
        if (shiftTime > 0f)
            return;

        int topGear = activeRatios.Length - 1;
        int next = currentGear;
        float shaftRpm = roadWheelRpm * activeFinalDrive;
        if (ManualGears)
        {
            next = Mathf.Clamp(currentGear + pendingShift, 0, topGear);
            pendingShift = 0;
        }
        else
        {
            // Use chassis speed for the shift decision. Burnouts and locked wheels do not fool the gearbox.
            float normalUpshiftRpm = Mathf.Lerp(activeShiftRpm - 1300f, activeShiftRpm, throttle);
            float upshiftRpm = Mathf.Lerp(normalUpshiftRpm, DriftUpshiftRpm, driftAmount);
            float downshiftRpm = Mathf.Lerp(3400f, DriftDownshiftRpm, driftAmount);
            float roadRpm = shaftRpm * Ratio(currentGear);
            if (currentGear < topGear && !handbrake && roadRpm >= upshiftRpm)
                next++;
            else if (currentGear > 0 && roadRpm < downshiftRpm && shaftRpm * Ratio(currentGear - 1) < upshiftRpm - 850f)
                next--;
        }

        if (next == currentGear)
            return;

        AudioClip clip = next > currentGear ? gear_up : gear_down;
        currentGear = next;
        shiftTime = ShiftDuration;
        if (gear_switch != null && clip != null)
            gear_switch.PlayOneShot(clip);
    }

    private void UpdateTireEffects(Wheel wheel, float speed, float step)
    {
        float slip = 0f;
        if (CanDrive && wheel.Grounded)
        {
            float forwardSlip = wheel.Contact.forwardSlip;
            float sidewaysSlip = Mathf.Abs(wheel.Contact.sidewaysSlip);
            float spin = 0f;
            if (wheel.IsDriven && wheel.Collider.motorTorque > 0f)
            {
                // At very low chassis speed, the tread-to-road speed also detects a standing burnout.
                float treadSpeed = Mathf.Abs(wheel.Collider.rpm) * (2f * Mathf.PI * wheel.Collider.radius / 60f);
                float contactSpeed = Mathf.Abs(Vector3.Dot(carRigidbody.GetPointVelocity(wheel.Contact.point),
                    wheel.Contact.forwardDir));
                float spinSpeed = Mathf.InverseLerp(0.8f, 4f, treadSpeed - contactSpeed)
                    * (1f - Mathf.InverseLerp(4f, 9f, speed));
                spin = Mathf.Max(Mathf.InverseLerp(0.1f, 0.52f, -forwardSlip), spinSpeed);
            }
            float skid = Braking > 0.18f && speed > 1.5f
                ? Mathf.InverseLerp(0.05f, 0.32f, Mathf.Max(forwardSlip, sidewaysSlip)) : 0f;
            float drift = speed > 2f ? Mathf.InverseLerp(0.22f, 0.8f, sidewaysSlip) : 0f;

            slip = Mathf.Max(Mathf.Max(spin, skid), drift);
        }

        wheel.Smoke.Update(wheel.Contact, slip, step);
        tireMarks.Update(ref wheel.Mark, wheel.Contact, slip);
        if (wheel.Sound != null)
        {
            wheel.Sound.volume = Mathf.MoveTowards(wheel.Sound.volume, slip, step * 4f);
            wheel.Sound.pitch = 0.68f + 0.42f * wheel.Sound.volume;
        }
    }

    private static void StartWheelSound(AudioSource source)
    {
        if (source == null || source.clip == null)
            return;

        source.time = Random.Range(0f, source.clip.length);
        source.volume = 0f;
        source.Play();
    }

    private void OnDestroy() => tireMarks?.Dispose();

    private void LateUpdate()
    {
        tireMarks.Flush();
        float blend = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        foreach (Wheel wheel in wheels)
            wheel.Render(transform, blend, Time.deltaTime);
    }

    private sealed class Wheel
    {
        public readonly WheelCollider Collider;
        public readonly AudioSource Sound;
        public readonly bool IsFront;
        public readonly bool IsDriven;
        public TireSmoke Smoke;
        public TireMarks.Track Mark;
        public WheelHit Contact;
        public bool Grounded;

        private readonly Transform visual;
        private readonly Quaternion restRotation;
        private Vector3 previousPosition;
        private Vector3 position;
        private float previousSteer;
        private float steer;
        private float spin;
        private bool hasPose;

        public Wheel(WheelCollider collider, Transform visual, AudioSource sound, bool isFront, bool isDriven)
        {
            Collider = collider;
            this.visual = visual;
            Sound = sound;
            IsFront = isFront;
            IsDriven = isDriven;
            restRotation = Quaternion.Inverse(collider.attachedRigidbody.transform.rotation) * collider.transform.rotation;
        }

        public float Travel => Grounded ? Mathf.Clamp01(
            (-Collider.transform.InverseTransformPoint(Contact.point).y - Collider.radius) / Collider.suspensionDistance) : 1f;

        public void SamplePose(Rigidbody chassis)
        {
            Collider.GetWorldPose(out Vector3 worldPosition, out _);
            Vector3 local = Quaternion.Inverse(chassis.rotation) * (worldPosition - chassis.position);
            previousPosition = hasPose ? position : local;
            previousSteer = steer;
            position = local;
            steer = Collider.steerAngle;
            hasPose = true;
        }

        public void Render(Transform chassis, float blend, float delta)
        {
            if (visual == null)
                return;

            spin = Mathf.Repeat(spin + Collider.rpm * 6f * delta, 360f);
            Quaternion rotation = restRotation * Quaternion.AngleAxis(Mathf.Lerp(previousSteer, steer, blend), Vector3.up)
                * Quaternion.AngleAxis(spin, Vector3.right);
            visual.SetPositionAndRotation(chassis.TransformPoint(Vector3.Lerp(previousPosition, position, blend)),
                chassis.rotation * rotation);
        }
    }
}
