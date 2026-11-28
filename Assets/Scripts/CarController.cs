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
    private int currentGear;
    public AudioSource gear_switch;
    public AudioClip gear_up;
    public AudioClip gear_down;

    public const float IdleRpm = 900f;
    public const float RedlineRpm = 7000f;
    public float EngineRpm { get; private set; } = IdleRpm;
    public float EngineLoad => CanDrive ? poweredThrottle * (1f - shiftTime / ShiftDuration) : 0f;
    public bool IsDriving => driving;
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

    private void Awake()
    {
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();

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
        throttle = Mathf.MoveTowards(throttle, input.Gas, step * (input.Gas > throttle ? 4.5f : 10f));
        brake = Mathf.MoveTowards(brake, input.Brake, step * 9f);
        steering = Mathf.MoveTowards(steering, input.Steer, step * 4f);

        Vector3 velocity = carRigidbody.velocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, transform.up);
        float roadWheelRpm = planarVelocity.magnitude * 60f / (2f * Mathf.PI * drivenWheelRadius);
        UpdateDrift(planarVelocity, step);
        UpdateGear(roadWheelRpm, step);

        // With both pedals down at low speed, the front brakes hold the car while the rear axle can spin.
        float burnout = handbrake || wd == 0 ? 0f
            : Mathf.InverseLerp(0.15f, 0.65f, brake) * Mathf.InverseLerp(0.45f, 0.85f, throttle)
                * (1f - Mathf.InverseLerp(4f, 8f, speed));
        float drivingThrottle = throttle * (1f - brake);
        poweredThrottle = Mathf.Lerp(drivingThrottle, throttle, burnout);

        float highSpeedSteering = Mathf.Lerp(0.32f, 0.55f, driftAmount);
        float steerAngle = steering * maxSteeringAngle * Mathf.Lerp(1f, highSpeedSteering, Mathf.InverseLerp(7f, 33f, speed));
        float launch = Mathf.InverseLerp(0.88f, 1f, throttle) * (1f - Mathf.InverseLerp(4f, 16f, speed))
            * (1f - brake);
        float ratio = Ratio(currentGear);
        float torquePerThrottle = motorForce * ratio * Mathf.Lerp(0.62f, 1.06f, Mathf.Max(launch, burnout))
            * Mathf.Lerp(1f, 0.65f, shiftTime / ShiftDuration) * Mathf.Lerp(1f, 1.28f, driftAmount);

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
            float spinAssist = wheel.IsFront ? launch : Mathf.Max(launch, burnout);
            forward.stiffness = wheel.IsDriven ? Mathf.Lerp(1.32f, 0.78f, spinAssist) : 1.32f;
            collider.forwardFriction = forward;

            WheelFrictionCurve sideways = collider.sidewaysFriction;
            sideways.stiffness = wheel.IsFront ? 1.46f : Mathf.Lerp(1.65f, 1.04f, Mathf.Max(driftAmount, handbrake ? 1f : 0f));
            collider.sidewaysFriction = sideways;

            if (wheel.IsDriven && wheel.Grounded)
            {
                wheelRpmSum += Mathf.Abs(collider.rpm);
                groundedDrivenWheels++;
            }
        }

        float spinRpm = groundedDrivenWheels > 0 ? Mathf.Max(0f, wheelRpmSum / groundedDrivenWheels - roadWheelRpm) : 0f;
        float spinLimit = Mathf.Lerp(420f, RedlineRpm, burnout);
        float coupledRpm = roadWheelRpm * ratio * activeFinalDrive
            + Mathf.Min(spinLimit, spinRpm * 0.62f * ratio * activeFinalDrive) * poweredThrottle;
        float launchFlare = poweredThrottle * 1350f * (1f - Mathf.InverseLerp(0.4f, 4.8f, speed));
        float targetRpm = Mathf.Clamp(Mathf.Max(IdleRpm + launchFlare, coupledRpm), IdleRpm, RedlineRpm);
        EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-step * (shiftTime > 0f ? 14f : 9f)));
    }

    private void UpdateDrift(Vector3 planarVelocity, float step)
    {
        float speed = planarVelocity.magnitude;
        bool axlesGrounded = (wheels[0].Grounded || wheels[1].Grounded)
            && (wheels[2].Grounded || wheels[3].Grounded);
        float lateralFraction = Mathf.Abs(Vector3.Dot(planarVelocity, transform.right)) / Mathf.Max(1f, speed);
        bool entry = handbrake && axlesGrounded && speed > 5.5f && Mathf.Abs(steering) > 0.18f;
        driftEntryTime = entry ? 0.4f : Mathf.Max(0f, driftEntryTime - step);
        bool sustain = axlesGrounded && speed > 4f && lateralFraction > 0.15f
            && (driftEntryTime > 0f || driftAmount > 0.1f) && throttle > 0.12f && brake < 0.1f;
        driftAmount = Mathf.MoveTowards(driftAmount, sustain ? 1f : 0f, step * (sustain ? 6f : 4f));
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
            float upshiftRpm = Mathf.Lerp(activeShiftRpm - 1300f, activeShiftRpm, throttle);
            float roadRpm = shaftRpm * Ratio(currentGear);
            if (currentGear < topGear && !handbrake && roadRpm >= upshiftRpm)
                next++;
            else if (currentGear > 0 && roadRpm < 3400f && shaftRpm * Ratio(currentGear - 1) < upshiftRpm - 850f)
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
            float spin = wheel.IsDriven && wheel.Collider.motorTorque > 0f
                ? Mathf.InverseLerp(0.1f, 0.52f, -forwardSlip) : 0f;
            float skid = Braking > 0.18f && speed > 1.5f
                ? Mathf.InverseLerp(0.05f, 0.32f, Mathf.Max(forwardSlip, sidewaysSlip)) : 0f;
            float drift = speed > 2f ? Mathf.InverseLerp(0.22f, 0.8f, sidewaysSlip) : 0f;

            slip = Mathf.Max(Mathf.Max(spin, skid), drift);
        }

        wheel.Smoke.Update(wheel.Contact, slip, step);
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

    private void LateUpdate()
    {
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
