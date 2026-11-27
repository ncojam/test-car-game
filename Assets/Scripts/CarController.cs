using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CarController : MonoBehaviour
{
    private float horizontalInput;
    private float verticalInput;
    private float steerAngle;
    private bool isBreaking;
	
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
	
	public float[] gearRatios;
	public float[] gearSpeeds;
    public int currentGear = 1;
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

	void Start()
	{
		carRigidbody.centerOfMass = new Vector3(0, centerOfMass, 0); // Пример значения, подберите под свои нужды
		DisplayCenterOfMass();
		
		frontLeftSFX.time = Random.Range(0f, frontLeftSFX.clip.length);
		frontRightSFX.time = Random.Range(0f, frontRightSFX.clip.length);
		rearLeftSFX.time = Random.Range(0f, rearLeftSFX.clip.length);
		rearRightSFX.time = Random.Range(0f, rearRightSFX.clip.length);
		frontLeftSFX.Play();
		frontRightSFX.Play();
		rearLeftSFX.Play();
		rearRightSFX.Play();
		
	}

    private void FixedUpdate()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
		HandleGear();
		HandleSlip();
		
		
		
		
		// Добавим вывод в консоль
		//Debug.Log(frontLeftSlip+" "+rearLeftSlip);
        /*Debug.Log("Gear: " + currentGear + " Torque: " + frontLeftWheelCollider.motorTorque +
		" rpm: " + engineRPM + " max: " + maxRPM + "Speed: " + GetComponent<Rigidbody>().velocity.magnitude * 3.6f + " km/h");*/
		//Debug.Log("Gear: " + currentGear +
		//" rpm: " + engineRPM + " max: " + maxRPM + " Torque: " + frontLeftWheelCollider.motorTorque);
		//Debug.Log("Speed: " + GetComponent<Rigidbody>().velocity.magnitude * 3.6f + " km/h");
		//Debug.Log("engineRPM: " + engineRPM);
		//Debug.Log("slip: " + frontLeftSlip + "," + frontRightSlip + "," + rearLeftSlip);
		//Debug.Log("rear slip: " + rearLeftSlip + " front slip: " + frontLeftSlip);
		//Debug.Log("maxRPM: " + maxRPM);
        /*Debug.Log("Current Gear: " + currentGear);
		Debug.Log("Torque: " + frontLeftWheelCollider.motorTorque);*/
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
        //frontLeftWheelCollider.motorTorque = verticalInput * motorForce;
        //frontRightWheelCollider.motorTorque = verticalInput * motorForce;

        /*brakeForce = isBreaking ? 3000f : 0f;
        frontLeftWheelCollider.brakeTorque = brakeForce;
        frontRightWheelCollider.brakeTorque = brakeForce;
        rearLeftWheelCollider.brakeTorque = brakeForce;
        rearRightWheelCollider.brakeTorque = brakeForce;*/
		
		if(verticalInput<0f){
			brakeForce = true ? 3000f : 0f;
		}
		else{
			brakeForce = 0f;
		}
		
		if(wd==1){
			frontLeftWheelCollider.brakeTorque = brakeForce;
			frontRightWheelCollider.brakeTorque = brakeForce;
		}
		else{
			frontLeftWheelCollider.brakeTorque = brakeForce;
			frontRightWheelCollider.brakeTorque = brakeForce;
			rearLeftWheelCollider.brakeTorque = brakeForce;
			rearRightWheelCollider.brakeTorque = brakeForce;
		}
		
		brakeForceRear = isBreaking ? 9000f : 0f;
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
		
		Debug.Log("slip: "+frontLeftSlip+frontRightSlip+rearLeftSlip+rearRightSlip);
		
		frontLeftSFX.volume = frontLeftSlip;
		frontLeftSFX.pitch = frontLeftSlip + .1f;
		frontRightSFX.volume = frontRightSlip;
		frontRightSFX.pitch = frontRightSlip + .1f;
		rearLeftSFX.volume = rearLeftSlip;
		rearLeftSFX.pitch = rearLeftSlip - .2f;
		rearRightSFX.volume = rearRightSlip;
		rearRightSFX.pitch = rearRightSlip - .2f;
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
	
	private void DisplayCenterOfMass()
    {
        Rigidbody carRigidbody = GetComponent<Rigidbody>();

        if (carRigidbody != null)
        {
            Vector3 centerOfMass = carRigidbody.centerOfMass;
            Debug.Log("Center of Mass: " + centerOfMass);
        }
        else
        {
            Debug.LogError("Rigidbody component not found!");
        }
    }
	
	private void HandleGear()
   {
		float speed = GetComponent<Rigidbody>().velocity.magnitude * 3.6f; // скорость в км/ч

		
		if (speed > gearSpeeds[currentGear+1] && currentGear < gearSpeeds.Length - 1)
		{
			//if (Input.GetKeyDown(KeyCode.G))
			//{
				//engineRPM = engineRPM/3;
				if(clutch<.2f){
					engineRPM = engineRPM/3f;
					gear_switch.clip = gear_up;
					gear_switch.Play();
					
					frontLeftWheelCollider.motorTorque = frontLeftWheelCollider.motorTorque/1.65f;
					frontRightWheelCollider.motorTorque = frontRightWheelCollider.motorTorque/1.65f;
					
					currentGear++;
					
					clutch = 10;
				}
				
				
			//}
		}
		
		if (speed < gearSpeeds[currentGear]-5 && currentGear > 0)
		{
			//if (Input.GetKeyDown(KeyCode.G))
			//{
			if(clutch_down<.2f){
				gear_switch.clip = gear_down;
				gear_switch.Play();
				currentGear--;
			}
			//}
		}

		/*float gearRatio = gearRatios[currentGear];
		frontLeftWheelCollider.motorTorque = verticalInput * motorForce * gearRatio;
		frontRightWheelCollider.motorTorque = verticalInput * motorForce * gearRatio;
		//rearLeftWheelCollider.motorTorque = verticalInput * motorForce * gearRatio;
		//rearRightWheelCollider.motorTorque = verticalInput * motorForce * gearRatio;
		engineRPM = verticalInput * motorForce * gearRatio;*/
		
		//float targetTorque;
		
		
		float targetTorque = verticalInput * motorForce * gearRatios[currentGear];
		if(targetTorque<0f){
			targetTorque=0f;
		}
		ApplyTorque(targetTorque);
		maxRPM = motorForce * gearRatios[0] + 100f * currentGear;
		maxRPM2 = motorForce * gearRatios[currentGear] + 100f * currentGear;
		
		if(clutch>.2f){
			clutch = Mathf.Lerp(clutch, 0, Time.fixedDeltaTime/.1f);
		}
		else{
			clutch = 0f;
		}
		if(clutch_down>.2f){
			clutch_down = Mathf.Lerp(clutch_down, 0, Time.fixedDeltaTime/.1f);
		}
		else{
			clutch_down = 0f;
		}
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
