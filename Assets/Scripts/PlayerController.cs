using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
//using UnityEngine.N3DS;
//using UnityEngine.N3DS.GamePad; //that's for analog input values

public class PlayerController : MonoBehaviour {
    [SerializeField] private CarController car;
    [SerializeField] private CameraController playerView;
    [SerializeField] private CameraFollow carView;

    private bool driving;
    private bool wasModelVisible;
    private Vector3 playerCameraOffset;
    private Quaternion playerCameraRotation;

	public Transform projectileStartPoint;
    public GameObject projectilePrefab;

	private bool isFalling = false;

	public float fuckOff(){
		return 5;
	}
	
	private bool moved2hook = false;
	
	private float slideSpeed = 3f;
	private float groundDistance = .00001f;
	private float wallDistance = 2f;
	private float pushForce = 30f;
	
	private int hitTimer = 0;

	public GameObject shadow; // объект тени
	public float shadowDistance = 15f; // расстояние, на которое нужно выпустить рэйкаст
	public LayerMask layerMask;

	public List<GameObject> torsoObjects; // Список объектов торса
    public List<GameObject> rightHandObjects; // Список объектов правой руки
    public List<GameObject> leftHandObjects; // Список объектов левой руки
    private int currentCostumeIndex = 0; // Индекс текущего выбранного костюма
	
	public Renderer kasaRenderer;
	public List<Material> kasaMaterials;
	private int currentKasaIndex = 0;


	public int startingHearts = 4;
    public Image[] heartImages;
 
    private int hearts;
	private bool dead = false;
	private int respawnIn = 200;

	public int springyTime = 0;
	public float hp;
	public float damageKnockBack;
	private bool AllowDashAir = true;
	public int coyoteTime = 15;
	public int afterGround = 10;
	public float steepSlopeAngle;

	public int timeToDrown = 40;
	public float underwaterSpeed = 1.4f;
	public Animator splashAni;

	public Transform hook;
	public bool hasHook = false;
	public bool hasDash = false;
	public int hit = 0;
	private int hitCountdown = 0;
	public float moveSpeed;
	private float speed;
	//public Rigidbody theRB;
	public float jumpForce;
	private Vector3 moveToHook;
	public float rush;
	public float rushSpeed;
	public float rushCountdown;
	public CharacterController controller;
	public float jumpPressed, jumpPressedTime;

	private Vector3 moveDirection;
	private Vector3 moveDir;
	private Vector3 pushDirection;
	public float gravityScale;

	public float jumpSpeed;//make private
	public bool stopFall;

	public Animator anim;
	public Transform pivot1;
	public Transform pivot2;
	public float rotateSpeed; //18f

	public GameObject playerModel;
	public GameObject hooker;


	//public Text dashText;
	//private float energy;
	
	/*public Image imgDash;
	public Image imgKasa;
	public Image imgHook;*/
	
	//public Text dialText;
	public Image dialBox;
	private Color dialBoxColor;

	public Vector3 checkPoint;

	public AudioSource source_jump;
	public AudioClip clip_jump;
	public AudioSource source_jumpheli;
	public AudioClip clip_jumpheli;
	public AudioSource source_dash;
	public AudioClip clip_dash;
	public AudioSource source_chaindeployed;
	public AudioClip clip_chaindeployed;
	public AudioSource source_chain;
	public AudioClip clip_chain;
	public AudioSource source_hit;
	public AudioClip clip_hit;
	public AudioSource source_energyupped;
	public AudioClip clip_energyupped;
	public AudioClip clip_ph_death;
	public List<AudioClip> clip_ph_hit;

	//public Image imageKey;
	public GameObject imageKey;

	public Vector3 lastSpeed = Vector3.zero;
	//public float inercia;

	public int targetFrameRate = 60;

	public bool inDialog = false;
	
	private float horizontalInput;
    private float verticalInput;
	private Vector3 currentVelocity;
	
	private float moveSmoothing = 8f;
	private float stopSmoothing = 16f;
	
	private Vector3 hitKnockback;
	private float lerpSpeed = 2f; // Скорость приближения
	
	bool paused;
	
	public Animator loadingScreen;
	
	private int waitTillPause = 10;
	
	private float timerRSP = 0f;
	
	public int weapon;
	private bool rightClick = false;
	

	// Use this for initialization
	void Start () {
		//Application.targetFrameRate = 60;
		controller = GetComponent<CharacterController>();
		//theRB = GetComponent<Rigidbody>();

		//imageKey.transform.position = new Vector3 (9999f, imageKey.transform.position.y, imageKey.transform.position.z);
		if(GameManager.bld_platform != 2 ){
			QualitySettings.vSyncCount = 0;
		}
		//Application.targetFrameRate = targetFrameRate;
		
		//hearts = startingHearts;
		
		SwitchToCostumeByIndex(GameManager.shirt);
		
		/*if(hasHook){
			imgHook.enabled=true;
		}
		else{
			imgHook.enabled=false;
		}*/
		
		/*dialText.text = "";
		dialBoxColor = dialBox.color;
		dialBoxColor.a = 0f;
		dialBox.color = dialBoxColor;*/
		
		layerMask = LayerMask.GetMask("thinObjects", "HitBox");
	}

	// Update is called once per frame
	void Update () {
        bool interact = VehicleInput.InteractPressed;
        if (interact && car != null && carView != null && playerView != null
            && !GameManager.paused && !dead && !inDialog)
        {
            if (driving)
                ExitCar();
            else if ((car.transform.position - transform.position).sqrMagnitude <= 16f)
                EnterCar();
        }

        if (driving)
            return;
		
		// Кидаем рейкаст вниз
		RaycastHit hitRSP;
		if (!Physics.Raycast(transform.position, Vector3.down, out hitRSP))
		{
			// Если рейкаст не касается земли, начинаем отсчет
			isFalling = true;
		}
		else{
			isFalling = false;
			timerRSP = 0f;
		}
		if(controller.isGrounded){
			timerRSP = 0f;
		}
		
		// Если падение началось, начинаем отсчет
		if (isFalling && !controller.isGrounded && moveToHook == new Vector3(0f,0f,0f))
		{
			timerRSP += Time.deltaTime;
			
			// Проверяем, достиг ли таймер значения 0
			if (timerRSP >= 4f)
			{
				// Выполняем респаун
				doRespawn();
				
				// Сбрасываем переменные
				isFalling = false;
				timerRSP = 0f;
			}
		}
		
		//Debug.Log(moveToHook);
		
		RaycastHit hit_;
		if (Physics.Raycast(transform.position, Vector3.down, out hit_, shadowDistance, -1, QueryTriggerInteraction.Ignore))
		{
			shadow.transform.position = hit_.point + Vector3.up * 0.1f; // устанавливаем позицию объекта тени на место контакта + смещение вверх, чтобы избежать коллизий
			shadow.SetActive(true); // включаем объект тени, если рэйкаст попал на объект
		}
		else
		{
			shadow.SetActive(false); // выключаем объект тени, если рэйкаст не попал на объект
		}
		
		if(inDialog){
			waitTillPause=10;
		}
		else if(waitTillPause>0){
			waitTillPause--;
		}
		
		paused = GameManager.paused;
		
		//if ( ( Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel") || GamePad.GetButtonTrigger(N3dsButton.Start) ) && !inDialog && !dead && waitTillPause<=0)
        if ( ( Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel") ) && !inDialog && !dead && waitTillPause<=0)
       
		{
			GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
                gameManager.doPause();
		}

		hitKnockback = Vector3.Lerp(hitKnockback, Vector3.zero, lerpSpeed * Time.deltaTime);
		//Debug.Log(hitKnockback);
		
		if(dead){
			respawnIn--;
			if(respawnIn==100){
				loadingScreen.SetBool ("load", true);
			}
			if(respawnIn<=0){
				SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
			}
		}
		
		if (springyTime > 0) {
			springyTime--;
		} else {
			//anim.SetBool ("springy", false);
		}

		if (timeToDrown == 39) {
			//underwater = .1f;
			//splashAni.SetBool ("underwater", true);
			hit = 2; hitCountdown += 5;
			timeToDrown--;
		}
		if (timeToDrown < 39) {
			
			jumpSpeed += underwaterSpeed;
			timeToDrown--;
		}
		if (timeToDrown == 36) {
			//splashAni.SetBool ("underwater", false);
		}
		if (timeToDrown == 0) {
			//splashAni.SetBool ("underwater", false);
			//underwater = 1;
			doRespawn ();
			timeToDrown = 40;
		}
		//if(Application.targetFrameRate != targetFrameRate)
			//Application.targetFrameRate = targetFrameRate;

		if (transform.position.y < -80) {
			doRespawn ();
		}

		
		
			//energy = 150 - rushCountdown;
		
		/*if (energy == 148) {
			source_energyupped.PlayOneShot(clip_energyupped);
		}*/
		/*if(energy<150){dashText.text = "" + energy;imgDash.enabled=false;
			if(energy==0){dashText.text = "";}
		}
		else{dashText.text = "";
			if(AllowDashAir){
				imgDash.enabled=true;
			}
			else{
				imgDash.enabled=false;
			}
		}*/
				
		/*if (rushCountdown > 0 && hasDash ) {
			if(GameManager.bld_platform==1){
				rushCountdown-=2;
			}
			else{
				rushCountdown--;
			}
		}*/

		if (Input.GetButtonDown ("Fire3") && rushCountdown == 0 && !inDialog && !dead && !paused) {
			if (controller.isGrounded){
				rushCountdown += 150;
			}
			if (AllowDashAir) {
				rushSpeed = rush;AllowDashAir = false; jumpSpeed = jumpForce*.1f;
				if (source_dash != null && clip_dash != null)
                    source_dash.PlayOneShot(clip_dash);
			}
		}
		/*if (Input.GetButtonDown ("Fire1") && hitCountdown == 0 && !inDialog && !dead && !paused) {
			moveToHook = new Vector3 (0,0,0);
			//if (jumpSpeed > 0 && !controller.isGrounded) {
			if (!controller.isGrounded) {
				imgKasa.enabled=false; hit = 2; hitCountdown += 5; jumpSpeed = jumpForce*.72f / 4.9f;
				source_jumpheli.PlayOneShot(clip_jumpheli);
			} else {
				hit = 1; hitCountdown += 5;
				source_hit.PlayOneShot(clip_hit);
			}
		}*/

		/*if (Input.GetButtonDown ("Fire2") && hasHook && !inDialog && !dead && !paused){
			//moveToHook = new Vector3 (0,0,0);
			hook.transform.position = transform.position;
			hooker.GetComponent<Hook>().resetLife();
			source_chaindeployed.PlayOneShot(clip_chaindeployed);
		}*/

		if (hitCountdown > 3) {
			hitCountdown--;
		}
		if (hitCountdown ==3) {
			hit=0;
		}

		if (controller.isGrounded) {
			//imgKasa.enabled=true;
			afterGround = 15;
			AllowDashAir = true;
			if (hitCountdown > 0) {
				hitCountdown--;
			}
		} else {
			if (afterGround > 0) {
				afterGround--;
			}
		}

		if (damageKnockBack > 0) {
			damageKnockBack *= .9f;
		}


		var step = moveSpeed * Time.deltaTime;
		if(moveToHook!=new Vector3 (0,0,0)){
			moved2hook = true;
			transform.position = Vector3.MoveTowards (transform.position, moveToHook, step*4);
		}
		else{
			moved2hook = false;
		}
		
		

		//slop slide 
		/*Vector3 reyDerection = playerModel.transform.TransformDirection(Vector3.forward).normalized;
		reyDerection.y = 0;
		for (int i = 8; i > 0; i--)
		{
			RaycastHit reyhitObj;
			if (Physics.Raycast(transform.position, reyDerection, out reyhitObj, 1))
			{
				float slopeAngle = Mathf.Deg2Rad * Vector3.Angle(Vector3.up, reyhitObj.normal);
				//Debug.DrawRay(transform.position, reyDerection , Color.red);

				if (slopeAngle >= steepSlopeAngle * Mathf.Deg2Rad && slopeAngle < 90 * Mathf.Deg2Rad)
				{
					//moveDirection = reyhitObj.normal * 2;
					moveDirection = reyDerection.normalized * slideSpeed;
					afterGround = 0;
					//Debug.DrawRay(transform.position, reyhitObj.normal, Color.green);
				}
			}
			reyDerection = Quaternion.Euler(0, 45, 0) * reyDerection;
		}*/
		
		//slop slide 
		Vector3 reyDirection = playerModel.transform.TransformDirection(Vector3.forward).normalized;
		reyDirection.y = 0;

		RaycastHit groundHit;
		RaycastHit wallHit;
		bool grounded = Physics.Raycast(transform.position, Vector3.down, out groundHit, groundDistance);
		bool slide = false;
		bool wallCollision = Physics.SphereCast(transform.position, controller.radius, reyDirection, out wallHit, wallDistance);
		//Debug.DrawRay(transform.position, reyDirection , Color.red);

		for (int i = 8; i > 0; i--)
		{
			RaycastHit hit;
			if (Physics.Raycast(transform.position, reyDirection, out hit, 1, -1, QueryTriggerInteraction.Ignore))
			//if (Physics.Raycast(transform.position, reyDirection, out hit, -1, QueryTriggerInteraction.Ignore))
				
			{
				float slopeAngle = Mathf.Deg2Rad * Vector3.Angle(Vector3.up, hit.normal);

				if (slopeAngle >= steepSlopeAngle * Mathf.Deg2Rad && slopeAngle < 90 * Mathf.Deg2Rad)
				{
					moveDirection = reyDirection.normalized * slideSpeed;
					afterGround = 0;
					slide = true;
					//Debug.DrawRay(transform.position, hit.normal, Color.blue);
				}
			}

			reyDirection = Quaternion.Euler(0, 45, 0) * reyDirection;
		}
		
		if(slide){
			//Vector3 wallDirection = -reyDirection; // Инвертируем направление вашего луча
			//pushDirection = wallDirection * pushForce;
			Vector3 wallNormal = wallHit.normal;
			pushDirection = wallNormal * pushForce;
			rotateSpeed = 4f;
		}
		else{
			rotateSpeed = Mathf.Lerp(rotateSpeed, 18f, Time.deltaTime*1);
		}

		/*if (!grounded && !slide && wallCollision && !controller.isGrounded)
		{
			Vector3 wallNormal = wallHit.normal;
			// Ваш код для отталкивания от стены
			//moveDirection += wallNormal * pushForce;
			Vector3 wallDirection = -reyDirection; // Инвертируем направление вашего луча
			//pushDirection = wallDirection * pushForce;
			
			// Визуализация SphereCast
			Debug.DrawRay(transform.position, reyDirection * wallDistance, Color.yellow);
			Debug.DrawRay(transform.position, wallNormal, Color.black);
		}*/
		
		pushDirection.x = pushDirection.x * .05f;
		pushDirection.y = pushDirection.y * .05f;
		pushDirection.z = pushDirection.z * .05f;
		//Debug.Log(pushDirection);
		//Debug.Log(pushDirection);
		

		jumpPressed -= Time.deltaTime;
		if (Input.GetButtonDown ("Jump") && !inDialog && !paused) {
			jumpPressed = jumpPressedTime;
		}

		if (afterGround > 0) {
			if (jumpPressed > 0) {
				if(!dead){
					jumpSpeed = jumpForce / 6.7f; afterGround = 0;
					if (source_jump != null && clip_jump != null)
                        source_jump.PlayOneShot(clip_jump);
				}
			}
		}

		if (!controller.isGrounded && !inDialog && !paused && !moved2hook) {
			jumpSpeed = jumpSpeed + (Physics.gravity.y * .07f * Time.deltaTime * gravityScale);
		//} else if (!Input.GetButtonDown ("Jump")) {
		} else if (afterGround<=0&&jumpPressed<=0) {
			jumpSpeed = -2;
		}
		if (jumpSpeed > 70) {
			jumpSpeed = 70;}
		else if (jumpSpeed < -20) {
			jumpSpeed = -20;}


		//moveDirection.y = jumpSpeed;// + (Physics.gravity.y * 1.2f * Time.deltaTime * gravityScale);
		//controller.Move (moveDirection * Time.deltaTime);

		rushSpeed = rushSpeed - Time.deltaTime * 15; //0.84
		if (rushSpeed < .1) {
			rushSpeed = 0;
		}


        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        Vector2 padMove = Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;
        if (padMove.sqrMagnitude > horizontalInput * horizontalInput + verticalInput * verticalInput)
        {
            horizontalInput = padMove.x;
            verticalInput = padMove.y;
        }
		//Move the player in different direcctions based on camera look direction
		//if ( (Input.GetAxis ("Horizontal") != 0 || Input.GetAxis ("Vertical") != 0 || GamePad.CirclePad.x !=0 || GamePad.CirclePad.y !=0) && !dead && !inDialog && !paused ) {//3DS
		if ((horizontalInput != 0 || verticalInput != 0) && !dead && !inDialog && !paused) {//PC
			if (pivot2.rotation.eulerAngles.y != 0f) {
				transform.rotation = Quaternion.Euler (0f, pivot2.rotation.eulerAngles.y, 0f);
				Quaternion newRotation = Quaternion.LookRotation (new Vector3 (moveDirection.x, 0f, moveDirection.z));
				//transform.rotation = Quaternion.Slerp (playerModel.transform.rotation, newRotation, rotateSpeed * Time.deltaTime);
				if( newRotation.y != 0f ){
					playerModel.transform.rotation = Quaternion.Slerp (playerModel.transform.rotation, newRotation, rotateSpeed * Time.deltaTime);
				}
				//Debug.Log (playerModel.transform.rotation+" "+newRotation);
			}
			
		}

		anim.SetBool ("isGrounded", controller.isGrounded);
		if(!controller.isGrounded){
			anim.SetFloat ("jump_speed", jumpSpeed);
		}
		else{
			anim.SetFloat ("jump_speed", 0f);
		}
		
		if (Input.GetButton ("Fire2")){
			rightClick = true;
			anim.SetInteger("weapon",weapon);
			anim.SetBool("rightClick",rightClick);
		}
		else{
			rightClick = false;
			anim.SetBool("rightClick",rightClick);
		}
		
		if (Input.GetButtonDown("Fire1") && rightClick)
        {
			// 1) Кидаем рейкаст из центра камеры
			Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
			RaycastHit hit;
			
			Vector3 targetPoint;
			
			// Если рейкаст столкнулся с коллайдером, запоминаем точку столкновения
            if (Physics.Raycast(ray, out hit))
            {
                targetPoint = hit.point;
            }
            else
            {
                // Если рейкаст не столкнулся ни с чем, используем конечную точку луча
                targetPoint = ray.GetPoint(200); // Предполагаемая длина луча
            }
			// 2) Клонируем projectile и помещаем его в позицию, как у projectileStartPoint
            GameObject newProjectile = Instantiate(projectilePrefab, projectileStartPoint.position, Quaternion.identity);

            // 3) Рассчитываем вектор от клона projectile до targetPoint
            Vector3 direction = (targetPoint - newProjectile.transform.position).normalized;
			
			// 4) Передаем этот вектор внутрь клона projectile
            Projectile projectileScript = newProjectile.GetComponent<Projectile>();
            if (projectileScript != null)
            {
                projectileScript.initialDirection(direction);
            }
		}
		
		//anim.SetFloat ("Speed", (Mathf.Abs(Input.GetAxis("Vertical")) + Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(GamePad.CirclePad.x) + Mathf.Abs(GamePad.CirclePad.y)));//3DS
		/*anim.SetFloat ("Speed", (Mathf.Abs(Input.GetAxis("Vertical")) + Mathf.Abs(Input.GetAxis("Horizontal"))));//PC
		anim.SetFloat ("rushSpeed", rushSpeed);
		anim.SetInteger ("hit", hit);*/

		lastSpeed = moveDirection;
		
		// Получаем положение контроллера для PC

		//Debug.Log(Input.GetAxisRaw("Horizontal"));
		// Получаем положение контроллера для 3DS
		//horizontalInput = Input.GetAxisRaw("Horizontal") + GamePad.CirclePad.x;
		//verticalInput = Input.GetAxisRaw("Vertical") + GamePad.CirclePad.y;
		
		/*if(hearts<=0){
			if(!dead){
				source_jumpheli.PlayOneShot(clip_ph_death);
			}
			dead = true;
			anim.SetBool("dead",dead);
		}*/
		if(hitTimer>0){
			hitTimer--;
		}

	}
	
    private void EnterCar()
    {
        playerCameraOffset = playerView.transform.position - transform.position;
        playerCameraRotation = playerView.transform.rotation;
        wasModelVisible = playerModel.activeSelf;
        playerModel.SetActive(false);
        shadow.SetActive(false);
        controller.enabled = false;
        playerView.enabled = false;
        carView.enabled = true;
        car.SetDriving(true);
        driving = true;
    }

    private void ExitCar()
    {
        if (!TryGetExitPosition(1f, out Vector3 position) && !TryGetExitPosition(-1f, out position))
            return;

        car.SetDriving(false);
        transform.position = position;
        currentVelocity = moveDirection = moveDir = hitKnockback = pushDirection = Vector3.zero;
        jumpPressed = rushSpeed = 0f;
        jumpSpeed = 0f;
        timerRSP = 0f;
        controller.enabled = true;
        playerModel.SetActive(wasModelVisible);
        carView.enabled = false;
        playerView.transform.SetPositionAndRotation(position + playerCameraOffset, playerCameraRotation);
        playerView.enabled = true;
        driving = false;
    }

    private bool TryGetExitPosition(float side, out Vector3 position)
    {
        Vector3 carRight = Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f) * Vector3.right;
        Vector3 origin = car.transform.position + carRight * (2.2f * side) + Vector3.up * 2f;
        position = Vector3.zero;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit ground, 4f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        float halfHeight = controller.height * 0.5f;
        float radius = controller.radius;
        Vector3 centre = ground.point + Vector3.up * (halfHeight + 0.06f);
        Vector3 capsuleOffset = Vector3.up * Mathf.Max(0f, halfHeight - radius);
        position = centre - transform.rotation * controller.center;
        return !Physics.CheckCapsule(centre - capsuleOffset, centre + capsuleOffset, radius, ~0,
            QueryTriggerInteraction.Ignore);
    }

	private void FixedUpdate()
	{
        if (driving)
            return;
		
		
		
		
		/*float yStore = moveDirection.y;
		if (!inDialog && !dead && !paused) {
			// Переменная moveDirection определяет скорость движения
            moveDirection = (transform.forward * verticalInput) + (transform.right * horizontalInput); // обновляем скорость движения
			
			//Debug.Log(horizontalInput);
			moveDirection = moveDirection + pushDirection;
            //moveDirection = moveDirection.normalized * moveSpeed; // нормализуем скорость движения
			moveDirection = moveDirection.normalized * moveSpeed;
		} else {
			moveDirection = new Vector3(0,0,0); // если находимся в диалоге, то останавливаемся на месте
		}
		
		if(!paused){
			moveDirection.y = yStore;
			moveDirection.x *= (rushSpeed*.35f + 1); // ускоряемся в сторону движения
			moveDirection.z *= (rushSpeed*.35f + 1); // ускоряемся в сторону движения
			
			
			if (jumpSpeed > 70) { // ограничиваем максимальную скорость прыжка
				jumpSpeed = 70;
			}
			else if (jumpSpeed < -20) { // ограничиваем минимальную скорость прыжка
				jumpSpeed = -20;
			}
			
			// Smooth stop when there is no input
			if (currentVelocity.magnitude > 0.1f && moveDirection.Equals(Vector3.zero)) {
				currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, stopSmoothing * Time.deltaTime);
			} else {
				currentVelocity = Vector3.Lerp(currentVelocity, moveDirection, moveSmoothing * Time.deltaTime);
			}
			
			currentVelocity.y = jumpSpeed;
			controller.Move( (currentVelocity + hitKnockback) * Time.deltaTime);
			
			anim.SetFloat ("walk_speed", Mathf.Abs(moveDirection.x) + Mathf.Abs(moveDirection.z));
			
		}*/
		
        UnityEngine.Vector3 direction = new UnityEngine.Vector3(horizontalInput, 0f, verticalInput);
		
		
		//moveDir.y = yStore;
		
		if (jumpSpeed > 70) { // ограничиваем максимальную скорость прыжка
			jumpSpeed = 70;
		}
		else if (jumpSpeed < -20) { // ограничиваем минимальную скорость прыжка
			jumpSpeed = -20;
		}

        //if (direction.magnitude >= 0.01f)
        //{
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + pivot2.eulerAngles.y;
            //transform.rotation = UnityEngine.Quaternion.Euler(0f, targetAngle, 0f);

			
            moveDir = UnityEngine.Quaternion.Euler(0f, targetAngle, 0f) * UnityEngine.Vector3.forward;
			moveDir.y = jumpSpeed;
			
			if (Input.GetButton ("Run") && (Mathf.Abs(direction.x) + Mathf.Abs(direction.z)) != 0f ){
				//speed = Mathf.Lerp(speed,moveSpeed + 5f, 15f * Time.deltaTime);
				speed = moveSpeed + 5f;
			}
			else{
				//speed = Mathf.Lerp(speed,direction.magnitude * moveSpeed, 15f * Time.deltaTime);
				speed = direction.magnitude * moveSpeed;
			}
			moveDir.x = moveDir.x * speed;
			moveDir.z = moveDir.z * speed;
			
			
            controller.Move(moveDir * Time.deltaTime);
			moveDirection = (transform.forward * verticalInput) + (transform.right * horizontalInput);
        //}
		
		
		if(paused){
			anim.speed = 0f;
		}
		else{
			if(speed<=4f && speed >= 0.1f && controller.isGrounded){
				anim.speed = speed / 4f;
				anim.SetBool("slow",true);
			}
			else{
				anim.speed = 1f;
				anim.SetBool("slow",false);
			}
		}
		
		anim.SetFloat ("walk_speed", speed);
		
		Debug.Log(speed);
	}
	
	public void ApplyKnockback(Vector3 knockbackVelocity)
	{
		if( hitTimer <= 0 ){
			// Применить отталкивание к скорости игрока
			hitKnockback = knockbackVelocity;
			source_jump.PlayOneShot(clip_ph_hit[Random.Range(0,4)]); 
			hearts--;
			hearts = Mathf.Max(hearts, 0);
			UpdateHeartImages();
			hitTimer += 15;
			//currentVelocity += knockbackVelocity;
		}
	}

	public void takeDamage (float damage, float knockBack){
		hp -= damage;
		damageKnockBack += knockBack;
		//hp -= 50;
	}

	public void vortexUp (){
		jumpSpeed = jumpForce / 8;
	}

	public void springUp (int speed, bool springy){
		if (springy) {
			anim.SetBool ("springy", true);
			springyTime = 4;
		}
		jumpSpeed = speed / 8;
		hitCountdown = 0;
		source_jump.PlayOneShot(clip_jump);
	}

	public void AddDash(){
		hasDash = true;
		AllowDashAir = true;
		rushCountdown = 0;
	}

	public void moveToMe(Vector3 pos){
		moveToHook = pos; 
		if(pos!=new Vector3(0,0,0)){
			source_chain.PlayOneShot(clip_chain);
		}
	}

	public void giveHook(){
		//imgHook.enabled=true;
		hasHook = true;
		//imgHook.enabled=false;
	}

	public void respawn(Vector3 pos){
		checkPoint = pos;
	}

	public void doRespawn(){
		transform.position = checkPoint;
		moveDirection = new Vector3 (0, 0, 0);
	}

	public void dashinst(){
		rushSpeed = rush; jumpSpeed = jumpForce*.1f;
		source_dash.PlayOneShot(clip_dash);
	}

	public void doDrown(){
		timeToDrown = 39;
	}

	public void switchDialog(){
		moveDirection = Vector3.zero;
		inDialog = !inDialog;
	}
	
	private void UpdateHeartImages()
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (i < hearts)
            {
                heartImages[i].enabled = true;
            }
            else
            {
                heartImages[i].enabled = false;
            }
        }
    }
	
	public void SwitchToCostumeByIndex(int costumeIndex)
	{
		// Проверяем, что переданный индекс находится в допустимом диапазоне
		if (costumeIndex >= 0 && costumeIndex < torsoObjects.Count)
		{
			// Отключаем текущий костюм
			DisableCurrentCostume();

			// Устанавливаем новый индекс костюма
			currentCostumeIndex = costumeIndex;

			// Включаем новый костюм
			EnableCurrentCostume();
		}
	}
	
	// Метод для отключения всех костюмов
    private void DisableCurrentCostume()
    {
        for (int i = 0; i < torsoObjects.Count; i++)
		{
			torsoObjects[i].SetActive(false);
			rightHandObjects[i].SetActive(false);
			leftHandObjects[i].SetActive(false);
		}
    }

    // Метод для включения текущего костюма
    private void EnableCurrentCostume()
    {
        torsoObjects[currentCostumeIndex].SetActive(true);
        rightHandObjects[currentCostumeIndex].SetActive(true);
        leftHandObjects[currentCostumeIndex].SetActive(true);
    }
	
	// Метод для замены материала у объекта
	public void ChangeMaterial()
	{
		kasaRenderer.material = kasaMaterials[currentKasaIndex];
	}
	
	public void doShop(){
		inDialog = true;
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
	}
	
	public void undoShop(){
		inDialog = false;
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	public void heartUp(){
		hearts = startingHearts;
		UpdateHeartImages();
	}
}
