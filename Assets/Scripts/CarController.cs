using UnityEngine;

public class CarController : MonoBehaviour
{
    private float horizontalInput;
    private float verticalInput;
    private float steerAngle;
    private bool isBreaking;

    [SerializeField] private bool startDriving;
    public float Throttle => IsDriving && !GameManager.paused ? Mathf.Clamp01(verticalInput) : 0f;

    private bool IsDriving { get; set; }
	
	public int wd;
	private int[] wd_consts = {1,0,0,1,1,1};

    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;
    public Transform frontLeftWheelTransform;
    public Transform frontRightWheelTransform;
    public Transform rearLeftWheelTransform;
    public Transform rearRightWheelTransform;

    public float maxSteeringAngle = 30f;
    public float motorForce;
    public float brakeForce = 0f;
	public float brakeForceRear = 0f;
	
	public float engineRPM;
	public float maxRPM;
	public float maxRPM2;
	
	public Rigidbody carRigidbody;
	public float centerOfMass;
	
	public float[] gearRatios = { 3f, 1.9f, 1.25f, 1f, 0.75f, 0.4f };
	public float[] gearSpeeds = { 0f, 30f, 70f, 110f, 130f, 160f };
    public int currentGear;
	public float clutch = 0f;
	public float clutch_down = 0f;
	public AudioSource gear_switch;
	public AudioClip gear_up;
	public AudioClip gear_down;
	
	private float frontLeftSlip;
	private float frontRightSlip;
	private float rearLeftSlip;
	private float rearRightSlip;
	
	/*private float n_frontLeftSlip;
	private float n_frontRightSlip;
	private float n_rearLeftSlip;
	private float n_rearRightSlip;*/
	
	public AudioSource frontLeftSFX;
	public AudioSource frontRightSFX;
	public AudioSource rearLeftSFX;
	public AudioSource rearRightSFX;

    private void Awake()
    {
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass = new Vector3(0f, centerOfMass, 0f);
        maxRPM = maxRPM2 = motorForce * gearRatios[0];
        SetDriving(startDriving);
    }

    private void Start()
    {
        StartWheelSound(frontLeftSFX);
        StartWheelSound(frontRightSFX);
        StartWheelSound(rearLeftSFX);
        StartWheelSound(rearRightSFX);
    }

    private static void StartWheelSound(AudioSource source)
    {
        if (source == null || source.clip == null)
            return;

        source.time = Random.Range(0f, source.clip.length);
        source.volume = 0f;
        source.Play();
    }

    public void SetDriving(bool driving)
    {
        IsDriving = driving;
        if (!driving)
            Park();
    }

    private void FixedUpdate()
    {
        if (IsDriving && !GameManager.paused)
        {
            GetInput();
            HandleMotor();
            HandleSteering();
            HandleGear();
        }
        else
        {
            Park();
        }

        UpdateWheels();
        HandleSlip();
    }

    private void Park()
    {
        horizontalInput = 0f;
        verticalInput = 0f;
        isBreaking = false;
        engineRPM = 0f;
        ParkWheel(frontLeftWheelCollider);
        ParkWheel(frontRightWheelCollider);
        ParkWheel(rearLeftWheelCollider);
        ParkWheel(rearRightWheelCollider);
    }

    private static void ParkWheel(WheelCollider wheel)
    {
        wheel.motorTorque = 0f;
        wheel.brakeTorque = 9000f;
        wheel.steerAngle = 0f;
    }

    private void GetInput()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        isBreaking = Input.GetKey(KeyCode.Space);
    }

    private void HandleSteering()
    {
        steerAngle = maxSteeringAngle * horizontalInput;
        frontLeftWheelCollider.steerAngle = steerAngle;
        frontRightWheelCollider.steerAngle = steerAngle;
    }

    private void HandleMotor()
    {
        brakeForce = verticalInput < 0f ? 3000f : 0f;
        brakeForceRear = isBreaking ? 9000f : (wd == 1 ? 0f : brakeForce);
        frontLeftWheelCollider.brakeTorque = brakeForce;
        frontRightWheelCollider.brakeTorque = brakeForce;
        rearLeftWheelCollider.brakeTorque = brakeForceRear;
        rearRightWheelCollider.brakeTorque = brakeForceRear;
    }

	private void HandleSlip()
	{
		float slip_factor;
		if(isBreaking){
			slip_factor = .1f;
		}
		else{
			slip_factor = .3f;
		}
		// Расчет уровня скольжения для каждого колеса
		frontLeftSlip = Mathf.Lerp(frontLeftSlip, CalculateSlip(frontLeftWheelCollider), Time.fixedDeltaTime / slip_factor);
		frontRightSlip = Mathf.Lerp(frontRightSlip, CalculateSlip(frontRightWheelCollider), Time.fixedDeltaTime / slip_factor);
		rearLeftSlip = Mathf.Lerp(rearLeftSlip, CalculateRearSlip(rearLeftWheelCollider), Time.fixedDeltaTime / slip_factor);
		rearRightSlip = Mathf.Lerp(rearRightSlip, CalculateRearSlip(rearRightWheelCollider), Time.fixedDeltaTime / slip_factor);
		
        UpdateWheelSound(frontLeftSFX, frontLeftSlip, 0.1f);
        UpdateWheelSound(frontRightSFX, frontRightSlip, 0.1f);
        UpdateWheelSound(rearLeftSFX, rearLeftSlip, -0.2f);
        UpdateWheelSound(rearRightSFX, rearRightSlip, -0.2f);
    }

    private static void UpdateWheelSound(AudioSource source, float slip, float pitchOffset)
    {
        if (source == null)
            return;

        source.volume = Mathf.Clamp01(slip);
        source.pitch = Mathf.Clamp(slip + pitchOffset, 0.1f, 3f);
    }

    private void UpdateWheels()
    {
        UpdateWheelPos(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateWheelPos(frontRightWheelCollider, frontRightWheelTransform);
        UpdateWheelPos(rearLeftWheelCollider, rearLeftWheelTransform);
        UpdateWheelPos(rearRightWheelCollider, rearRightWheelTransform);
    }

    private void UpdateWheelPos(WheelCollider wheelCollider, Transform trans)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        trans.rotation = rot;
        trans.position = pos;
    }
	
    private void HandleGear()
    {
        float speed = carRigidbody.velocity.magnitude * 3.6f;
        int topGear = Mathf.Min(gearRatios.Length, gearSpeeds.Length) - 1;
        currentGear = Mathf.Clamp(currentGear, 0, topGear);

        if (currentGear < topGear && speed > gearSpeeds[currentGear + 1] && clutch < 0.2f)
        {
            engineRPM /= 3f;
            PlayShiftSound(gear_up);
            frontLeftWheelCollider.motorTorque /= 1.65f;
            frontRightWheelCollider.motorTorque /= 1.65f;
            currentGear++;
            clutch = 10f;
        }

        if (currentGear > 0 && speed < gearSpeeds[currentGear] - 5f && clutch_down < 0.2f)
        {
            PlayShiftSound(gear_down);
            currentGear--;
        }

        ApplyTorque(Throttle * motorForce * gearRatios[currentGear]);
        maxRPM = motorForce * gearRatios[0] + 100f * currentGear;
        maxRPM2 = motorForce * gearRatios[currentGear] + 100f * currentGear;
        clutch = clutch > 0.2f ? Mathf.Lerp(clutch, 0f, Time.fixedDeltaTime / 0.1f) : 0f;
        clutch_down = clutch_down > 0.2f ? Mathf.Lerp(clutch_down, 0f, Time.fixedDeltaTime / 0.2f) : 0f;
    }

    private void PlayShiftSound(AudioClip clip)
    {
        if (gear_switch == null || clip == null)
            return;

        gear_switch.clip = clip;
        gear_switch.Play();
    }

	private void ApplyTorque(float targetTorque)
	{
		float smoothTime = 0.6f; // Время плавного изменения, можно регулировать

		
		frontLeftWheelCollider.motorTorque = Mathf.Lerp(frontLeftWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime) * wd_consts[wd*2];
		frontRightWheelCollider.motorTorque = Mathf.Lerp(frontRightWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime) * wd_consts[wd*2];
		rearLeftWheelCollider.motorTorque = Mathf.Lerp(rearLeftWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime) * wd_consts[wd*2 + 1];
		rearRightWheelCollider.motorTorque = Mathf.Lerp(rearRightWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime) * wd_consts[wd*2 + 1];
		
		if( frontLeftWheelCollider.motorTorque < 10 ) { frontLeftWheelCollider.motorTorque = 0f; frontRightWheelCollider.motorTorque = 0f; }
		
		//rearLeftWheelCollider.motorTorque = Mathf.Lerp(rearLeftWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime);
		//rearRightWheelCollider.motorTorque = Mathf.Lerp(rearRightWheelCollider.motorTorque, targetTorque, Time.fixedDeltaTime / smoothTime);

		if(clutch<.2f){
			if(wd==0){
				engineRPM = Mathf.Lerp(engineRPM, frontLeftWheelCollider.motorTorque, Time.fixedDeltaTime / .8f);
			}
			else{
				engineRPM = Mathf.Lerp(engineRPM, rearLeftWheelCollider.motorTorque, Time.fixedDeltaTime / .8f);
			}
		}
		else{
			//engineRPM = Mathf.Lerp(engineRPM, engineRPM/3, Time.fixedDeltaTime / .2f);
		}
	}
	
	private void ApplyExactTorque(float targetTorque)
	{
		frontLeftWheelCollider.motorTorque = targetTorque;
		frontRightWheelCollider.motorTorque = targetTorque;
	}
	
	float CalculateSlip(WheelCollider wheelCollider)
	{
		WheelHit wheelHit;
		if (wheelCollider.GetGroundHit(out wheelHit))
		{
			// forwardSlip - скольжение вперед, sidewaysSlip - скольжение вбок
			float forwardSlip = ( wheelHit.forwardSlip * 1.2f + wheelHit.sidewaysSlip * .8f ) / 1.5f;
			return (-forwardSlip - 0.2f) / 0.8f;
		}
		else
		{
			return 0f;
		}
	}
	
	float CalculateRearSlip(WheelCollider wheelCollider)
	{
		WheelHit wheelHit;
		if (wheelCollider.GetGroundHit(out wheelHit))
		{
			// forwardSlip - скольжение вперед, sidewaysSlip - скольжение вбок
			float forwardSlip = ( wheelHit.forwardSlip * 1.2f + wheelHit.sidewaysSlip * .8f ) / 1.5f;
			// Нормализуем в диапазоне [0, 1]
			return (-forwardSlip - 0.5f) / 0.2f;
		}
		else
		{
			return 0f;
		}
	}

}
