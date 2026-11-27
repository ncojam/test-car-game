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
    [SerializeField] private float serviceBrakeTorque = 2350f;
    [SerializeField] private float centerOfMass = 0.57f;
    [SerializeField] private float antiRollStiffness = 2600f;

    public float[] gearRatios = { 3f, 1.9f, 1.25f, 1f, 0.75f, 0.4f };
    public float[] gearSpeeds = { 0f, 30f, 70f, 110f, 130f, 160f };
    public int currentGear;
    public AudioSource gear_switch;
    public AudioClip gear_up;
    public AudioClip gear_down;

    [SerializeField, Min(1f)] private float finalDrive = 5.2f;

    public const float IdleRpm = 900f;
    public const float RedlineRpm = 7000f;
    public float EngineRpm { get; private set; } = IdleRpm;
    public float EngineLoad => CanDrive ? throttle * (1f - brake) * (1f - shiftTime / 0.3f) : 0f;
    public bool IsDriving => driving;
    public int GearNumber => currentGear + 1;
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

        foreach (Wheel wheel in wheels)
        {
            ConfigureSuspension(wheel.Collider);
            wheel.Smoke = new TireSmoke(wheel.Collider.transform, tireSmokeMaterial);
            wheel.SamplePose(carRigidbody);
            StartWheelSound(wheel.Sound);
        }

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
    }

    public void SetDriving(bool active)
    {
        input = default;
        driving = active;
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

        UpdateGear(speed * 3.6f, step);
        float steerAngle = steering * maxSteeringAngle * Mathf.Lerp(1f, 0.32f, Mathf.InverseLerp(7f, 33f, speed));
        float launch = Mathf.InverseLerp(0.88f, 1f, throttle) * (1f - Mathf.InverseLerp(4f, 16f, speed))
            * (1f - brake);
        float ratio = currentGear >= 0 && currentGear < gearRatios.Length ? gearRatios[currentGear] : 1f;
        float torque = motorForce * ratio * throttle * (1f - brake)
            * Mathf.Lerp(0.62f, 1.06f, launch) * Mathf.Lerp(1f, 0.65f, shiftTime / 0.3f);

        float wheelRpmSum = 0f;
        float radiusSum = 0f;
        int drivenWheels = 0;
        foreach (Wheel wheel in wheels)
        {
            WheelCollider collider = wheel.Collider;
            collider.steerAngle = wheel.IsFront ? steerAngle : 0f;
            collider.motorTorque = wheel.IsDriven && (!handbrake || wheel.IsFront) ? torque : 0f;
            collider.brakeTorque = wheel.IsFront ? serviceBrakeTorque * brake
                : Mathf.Max(serviceBrakeTorque * 0.65f * brake, handbrake ? 3300f : 0f);

            // Dry asphalt is the baseline. Full-throttle launches relax longitudinal grip on the driven axle.
            WheelFrictionCurve forward = collider.forwardFriction;
            forward.stiffness = wheel.IsDriven ? Mathf.Lerp(1.32f, 0.78f, launch) : 1.32f;
            collider.forwardFriction = forward;

            WheelFrictionCurve sideways = collider.sidewaysFriction;
            sideways.stiffness = !wheel.IsFront && handbrake ? 1.04f : (wheel.IsFront ? 1.46f : 1.65f);
            collider.sidewaysFriction = sideways;

            if (wheel.IsDriven)
            {
                wheelRpmSum += Mathf.Abs(collider.rpm);
                radiusSum += collider.radius;
                drivenWheels++;
            }
        }

        if (drivenWheels > 0)
        {
            float radius = Mathf.Max(0.01f, radiusSum / drivenWheels);
            float forwardSpeed = Mathf.Abs(Vector3.Dot(carRigidbody.velocity, transform.forward));
            float roadWheelRpm = forwardSpeed * 60f / (2f * Mathf.PI * radius);
            float spinRpm = Mathf.Max(0f, wheelRpmSum / drivenWheels - roadWheelRpm);
            float coupledRpm = (roadWheelRpm + spinRpm * 0.62f) * ratio * finalDrive;
            float launchFlare = throttle * 1350f * (1f - Mathf.InverseLerp(0.4f, 4.8f, forwardSpeed));
            float targetRpm = Mathf.Clamp(Mathf.Max(IdleRpm + launchFlare, coupledRpm), IdleRpm, RedlineRpm);
            EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-step * (shiftTime > 0f ? 14f : 9f)));
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
    }

    private void ApplyAntiRoll(Wheel left, Wheel right)
    {
        float lift = (left.Travel - right.Travel) * antiRollStiffness;
        if (left.Grounded)
            carRigidbody.AddForceAtPosition(-left.Collider.transform.up * lift, left.Collider.transform.position);
        if (right.Grounded)
            carRigidbody.AddForceAtPosition(right.Collider.transform.up * lift, right.Collider.transform.position);
    }

    private void UpdateGear(float speedKph, float step)
    {
        shiftTime = Mathf.Max(0f, shiftTime - step);
        int topGear = Mathf.Max(0, Mathf.Min(gearRatios.Length, gearSpeeds.Length) - 1);
        currentGear = Mathf.Clamp(currentGear, 0, topGear);
        if (shiftTime > 0f)
            return;

        int next = currentGear;
        if (currentGear < topGear && speedKph > gearSpeeds[currentGear + 1])
            next++;
        else if (currentGear > 0 && speedKph < gearSpeeds[currentGear] - 6f)
            next--;

        if (next == currentGear)
            return;

        AudioClip clip = next > currentGear ? gear_up : gear_down;
        currentGear = next;
        shiftTime = 0.3f;
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
