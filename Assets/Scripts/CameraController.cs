using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.UI;
//using UnityEngine.N3DS;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour {

	//public static int shit = 5000049;

	PlayerController playerController;
	//public void doInPainting();
	
	public GameObject playerOne;
	public GameObject playerOneHead;
	public GameObject playerTorso;
	public bool inPainting;


	public Transform targetPlayer;
	//public Transform targetPlayer;

	public Vector3 offset;
	public Vector3 offset2;
	public Vector3 desiredPosition;
	
	public bool useOffsetValues;

	public float rotateSpeed;
	public float rotateSpeedY;
	public float damping = .5f;

	//public float playerRotationSaved;

	public Transform pivot;
	public Transform pivot2;
	//public Transform hook;

	public float nextFov;
	public float fastFov;

	public float setFov;
	public float cameraYOffset;
	private float savedOffset;

	private Vector3 pivotPos;
	public float pivotVertical;
	public float downShift;
	public bool doDownShift = false;

	public float horizontal, vertical;
	public LayerMask layerMask;
	public LayerMask layerMaskThin;
	public float thinObjectSize = .2f;
	public int thinObjectVertexCount = 3;

	public int targetFrameRate = 60;

	private Vector3 bothRotations;

	public bool main;
	private float camDist;
	private float camDist2;
	private float camDistPower;
	private float camDist2Close;
	Vector3 playaHead;
	
	private bool wasPlayerTorsoEnabled = true;
	private int inv = 1;
	private Vector3 offsetNormal;
	private Vector3 offsetClose;
	
	public bool cameraClose;
	public float sideOffset;
	private float cameraSideOffset;
	
	//public Text t0;
	//public Text t1;

	public void giveDownShift(int value){
		//downShift = -value;
		doDownShift = true;
	}
	
	public void doInPainting(){
		Debug.Log("пошёл нахуй");
	}

	// Use this for initialization
	void Start () {
		

		layerMask = LayerMask.GetMask("Default", "Ground", "Obstacles");
		layerMaskThin = LayerMask.GetMask("thinObjects", "Characters", "Weapons", "HitBox");
		
		if(GameManager.bld_platform != 2 ){
			QualitySettings.vSyncCount = 0;
			//TURN THAT ON FOR PC!!!
			Application.targetFrameRate = targetFrameRate;//PC
		}

		playerController = playerOne.GetComponent<PlayerController>();

		if (!useOffsetValues) {
			offset = targetPlayer.position - transform.position; 
		}
		offsetNormal = offset;
		offsetClose = new Vector3(offset.x * .37f, offset.y * .76f, offset.z * .37f);

		//pivot.transform.position = targetPlayer.transform.position;
		pivot.transform.parent = null;
		pivot2.transform.parent = null;

		//pivot.transform.parent = targetPlayer.transform;

		if(SceneManager.GetActiveScene().buildIndex!=4){
			Cursor.lockState = CursorLockMode.Locked;
		}
		/*else if(SceneManager.GetActiveScene().buildIndex==4){
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}*/

		nextFov = Camera.main.fieldOfView;

		savedOffset = cameraYOffset;

	}
	
	void Update () {
		if(!GameManager.paused){
			//get the x pos of the mouse & rotate the target
			//horizontal = (Input.GetAxis("Mouse X") + ( GamePad.CirclePadPro.x * 37 ) + Input.GetAxis("StickR X")*20) * rotateSpeed  * Time.deltaTime;//3DS
			horizontal = (Input.GetAxis("Mouse X") + VehicleInput.LookStick.x*20) * rotateSpeed  * Time.deltaTime;//PC
			pivot2.Rotate (0, horizontal, 0);

			//get the Y position of the mouse & rotate the pivot
			//vertical = (Input.GetAxis("Mouse Y") + ( GamePad.CirclePadPro.y * 37 ) - Input.GetAxis("StickR Y")*20) * rotateSpeedY * inv * Time.deltaTime;//3DS
			vertical = (Input.GetAxis("Mouse Y") + VehicleInput.LookStick.y*20) * rotateSpeedY * inv * Time.deltaTime;//PC
			pivot.Rotate (-vertical, 0, 0);
		}
		
		if (Input.GetButton ("Fire2")){
			cameraClose = true;
		}
		else{
			cameraClose = false;
		}
	}
	
	// Update is called once per frame
	void LateUpdate () {

		//playaHead = new Vector3(playerOne.transform.position.x, playerOne.transform.position.y + 1f , playerOne.transform.position.x);
		playaHead = pivot.transform.position;
		//playaHead = playerOne.transform.position;
		//Debug.Log(playerOne.transform.position+" "+playaHead);
		/*camDist = Vector3.Distance(transform.position, playaHead);
		camDist2 = Vector3.Distance(transform.position, playerOneHead.transform.position);
		camDistPower = (1f-camDist)*50f;*/
		
		camDist = Vector3.Distance(transform.position, playerOne.transform.position);
		camDistPower = (1f-camDist)*50f;

		//Debug.Log(camDist);

		if (downShift > -1 && downShift < 1) {
			downShift = 0;
		}
		if (!doDownShift && downShift < 0) {
			downShift += 5;
			//downShift /= 2;
		}
		if (doDownShift) {
			downShift += -(downShift + 250) / 6;
			if (downShift < -249) {
				doDownShift = false;
			}
		}

		//TURN THAT ON FOR PC!!!
		if(Application.targetFrameRate != targetFrameRate)
			Application.targetFrameRate = targetFrameRate;
		
		if (!cameraClose)camDist2Close = 1.3f;
		if (cameraClose)camDist2Close = .3f;
		if (camDist < camDist2Close && camDist > 0f)
        {
			cameraYOffset = savedOffset*(camDist/2f);
		}
		else if(camDist<=0f){
			cameraYOffset = 0f;
		}
		else{
			cameraYOffset = savedOffset;
		}
		
		if (camDist < 1.3f && wasPlayerTorsoEnabled)
		{
			playerTorso.SetActive(false);
			wasPlayerTorsoEnabled = false;
		}
		else if (camDist >= 1.3f && !wasPlayerTorsoEnabled)
		{
			playerTorso.SetActive(true);
			wasPlayerTorsoEnabled = true;
		}

		//Debug.Log(camDist);
		if (camDist < 1f)
        {
            // Вычисление направления перемещения пивота в противоположную сторону относительно камеры
            Vector3 camDir = pivot.transform.position - desiredPosition;
            Vector3 targetPos = playerOne.transform.position + camDir.normalized * camDist * camDistPower;
            float pivotPosY = playerOne.transform.position.y + cameraYOffset + pivotVertical - downShift / 100;
			//Debug.Log("dist:"+camDist+" dir:"+camDir+" dir.nrm"+camDir.normalized+" distpwr"+camDistPower);
            pivot.transform.position = new Vector3(targetPos.x, pivotPosY, targetPos.z);
			
			//playerTorso.SetActive(false);
        }
		else{			
			pivot.transform.position = new Vector3 (targetPlayer.transform.position.x, targetPlayer.transform.position.y + cameraYOffset + pivotVertical - downShift/100, targetPlayer.transform.position.z);
			//pivot.transform.position = new Vector3 (targetPlayer.transform.position.x, targetPlayer.transform.position.y, targetPlayer.transform.position.z);
			
			//playerTorso.SetActive(true);
		}
		pivot2.transform.position = new Vector3 (targetPlayer.transform.position.x, targetPlayer.transform.position.y + cameraYOffset + pivotVertical - downShift/100, targetPlayer.transform.position.z);
		


		/*if (Input.GetButtonDown ("Fire2") && !GameManager.paused) {
			//hook.transform.rotation = new Vector3 (rotationX, rotationY, targetPlayer.transform.position.z);
			hook.rotation = Quaternion.Euler (pivot.rotation.eulerAngles.x - 23, pivot2.rotation.eulerAngles.y, pivot.rotation.eulerAngles.z);;
		}*/

		//fastFov = controller.fuckOff();
		//Camera.main.fieldOfView = nextFov + fastFov*100;
		if (main) {
			fastFov = playerController.rushSpeed;
			//Camera.main.fieldOfView = nextFov + fastFov * setFov;
		}


		

		float upLimit, downLimit;
		
		if(camDist<1f){
			upLimit=30f; downLimit=359f;
		}
		else{
			upLimit=30f; downLimit=315f;
		}
		
		//limit up/down camera rotation
		if (pivot.rotation.eulerAngles.x > upLimit && pivot.rotation.eulerAngles.x < 180f) {
			pivot.rotation = Quaternion.Euler (upLimit, pivot.rotation.eulerAngles.y, pivot.rotation.eulerAngles.z);
		}
		if (pivot.rotation.eulerAngles.x > 180f && pivot.rotation.eulerAngles.x < downLimit) {
			pivot.rotation = Quaternion.Euler (downLimit, pivot.rotation.eulerAngles.y, pivot.rotation.eulerAngles.z);
		}
		
		
		if (pivot.rotation.eulerAngles.x >= 315f && pivot.rotation.eulerAngles.x < 360f) {
			pivotVertical = (360 - pivot.rotation.eulerAngles.x) / 20;
		} else {
			pivotVertical = 0f;
		}
		
		
		/*if (pivot.rotation.eulerAngles.x >= 300f && pivot.rotation.eulerAngles.x < 360f) {
			// Нормализация в диапазоне от 0 до 1
			float normalizedValue = Mathf.InverseLerp(300f, 360f, pivot.rotation.eulerAngles.x);
			//camDist = normalizedValue;
			// Масштабирование в нужный вам диапазон
			camDist = Mathf.Lerp(1f, 1f, normalizedValue);
			
			Debug.Log(camDist+" "+normalizedValue+" ");
		} else {
			camDist = 1f;
		}*/
		camDist = 1f;
		
		
		
		//Debug.Log(pivot.rotation.eulerAngles.x);
		
		//UnityEngine.Debug.Log(pivotVertical+" "+pivot.rotation.eulerAngles.x);
		
		//t0 = pivotVertical;
		//t1 = pivot.rotation.x;
		//pivot.transform.position = targetPlayer.transform.position + new Vector3(vertical*100,vertical*100,0);

			//playerRotationSaved = target;
		//}

		/*if(Input.GetButtonDown("Horizontal")||Input.GetButtonDown("Vertical")){
			targetPlayer.transform.forward = new Vector3 (target.transform.forward.x, targetPlayer.transform.forward.y, target.transform.forward.z);
			target.transform.forward = new Vector3 (0, 0, 0);
		}*/

		//move the camera based on current rotation of target and original offset
		//float desiredYAngle = targetPlayer.eulerAngles.y;
		//float desiredYAngle;
		float desiredXAngle = pivot.eulerAngles.x;

		//if (Input.GetButtonDown ("Horizontal") || Input.GetButtonDown ("Vertical")) {
		float desiredYAngle = pivot2.eulerAngles.y;
		//} else {
		//	desiredYAngle = pivot.eulerAngles.y;
		//}
			Quaternion rotation = Quaternion.Euler(desiredXAngle, desiredYAngle, 0);
		//transform.position = targetPlayer.position - (rotation * offset);
		desiredPosition = targetPlayer.position - (rotation * offset * camDist);
		
		/*if(cameraClose){
			// Получаем угол вращения вокруг оси Y
			float angle = pivot2.eulerAngles.y; 
			// Рассчитываем смещение по X и Z
			cameraSideOffset = Mathf.Lerp(cameraSideOffset,.35f,2f * Time.deltaTime);
			float xOffset = Mathf.Cos(angle * Mathf.Deg2Rad) * cameraSideOffset; 
			float zOffset = -Mathf.Sin(angle * Mathf.Deg2Rad) * cameraSideOffset; 
			// Модифицируем desiredPosition
			desiredPosition += new Vector3(xOffset, 0f, zOffset);
			// Модифицируем позицию пивота
			Vector3 pivotOffset = new Vector3(xOffset, 0f, zOffset);
			pivot.position += pivot.TransformDirection(pivotOffset);
			//pivot.position = pivot.TransformPoint(pivotOffset);
			
			offset = Vector3.Lerp(offset,offsetClose,5f * Time.deltaTime);
		}
		else{
			offset = Vector3.Lerp(offset,offsetNormal,5f * Time.deltaTime);
		}*/
		
		if(cameraClose){
			cameraSideOffset = Mathf.Lerp(cameraSideOffset,sideOffset,2f * Time.deltaTime);
			offset = Vector3.Lerp(offset,offsetClose,5f * Time.deltaTime);
		}
		else{
			cameraSideOffset = Mathf.Lerp(cameraSideOffset,0f,2f * Time.deltaTime);
			offset = Vector3.Lerp(offset,offsetNormal,5f * Time.deltaTime);
		}
		// Получаем угол вращения вокруг оси Y
			float angle = pivot2.eulerAngles.y; 
			// Рассчитываем смещение по X и Z
			cameraSideOffset = Mathf.Lerp(cameraSideOffset,sideOffset,2f * Time.deltaTime);
			float xOffset = Mathf.Cos(angle * Mathf.Deg2Rad) * cameraSideOffset; 
			float zOffset = -Mathf.Sin(angle * Mathf.Deg2Rad) * cameraSideOffset; 
			// Модифицируем desiredPosition
			desiredPosition += new Vector3(xOffset, 0f, zOffset);
			// Модифицируем позицию пивота
			Vector3 pivotOffset = new Vector3(xOffset, 0f, zOffset);
			pivot.transform.position += pivot.TransformDirection(pivotOffset);
			//помимо исправления строки 535 нужно ещё будет сделать пуш пивота в случае, если камера близко, или если камера въезжает в стену
			
			//pivot.position = pivot.TransformPoint(pivotOffset);
		
		
		
		
		
		//ОПРЕДЕЛЕНИЕ ТОЛЩИНЫ----------------------------------------------
		
		// Создаем вектор направления между игроком и камерой
		Vector3 direction = (transform.position - pivot2.transform.position).normalized;

		// Умножаем вектор направления на желаемую длину луча (например, 1000)
		float raycastDistance = 30f;
		Vector3 raycastDirection = direction * raycastDistance;
		
		float wallThickness = 0f;

		// Объявляем переменную RaycastHit, чтобы получить данные о столкновении от функции Raycast()
		RaycastHit hitA, hitB;

		// Стреляем лучем и проверяем столкновение
		if (Physics.Raycast(pivot2.transform.position, raycastDirection, out hitA, raycastDistance)) 
		{
			// Рисуем зеленый луч до места столкновения
			//Debug.DrawRay(pivot2.transform.position, hitA.point - pivot2.transform.position, Color.green);

			// Рассчитываем вторую точку пересечения луча
			Vector3 raycast2Origin = hitA.point + (direction * 0.01f); // Небольшое приращение, чтобы избежать пересечения с той же точкой
			if (Physics.Raycast(raycast2Origin, direction, out hitB, raycastDistance))
			{
				// Определяем толщину стены
				wallThickness = Vector3.Distance(hitA.point, hitB.point);

				// Рисуем красный луч между точками
				//Debug.DrawRay(hitA.point, hitB.point - hitA.point, Color.red);

				// Рисуем бирюзовый луч после выхода из объекта
				//Debug.DrawRay(hitB.point, raycastDirection - (hitB.point - hitA.point).magnitude * direction, Color.cyan);

				
			}
			
			// Выводим толщину стены в консоль
			//Debug.Log("Толщина стены: " + wallThickness);
		}
		else
		{
			// Рисуем луч для дебага, если столкновение не произошло
			//Debug.DrawRay(pivot2.transform.position, raycastDirection, Color.red);
			//Debug.Log("Отсосите бля");
		}
		
		
		
		
		
		//ОПРЕДЕЛЕНИЕ ТОЛЩИНЫ----------------------------------------------
		
		
		
		// Create a Ray from the player's position to the desired camera position
		Ray ray = new Ray(pivot2.transform.position, desiredPosition - pivot2.transform.position);

		// Declare a RaycastHit variable to store the information about any collisions
		RaycastHit hit;

		// Cast the ray and check for collisions
		if (Physics.Raycast(ray, out hit, offset.magnitude, layerMask) && !Physics.Raycast(ray, out hit, offset.magnitude, layerMaskThin))
		{
			
			Collider collider = hit.collider;
			
			Vector3 newPos = hit.point + hit.normal * 0.5f;
			//transform.position = newPos;
			transform.position = Vector3.MoveTowards(transform.position,newPos, 15f * Time.deltaTime);
		}
		// Otherwise, move the camera to the desired position
		else
		{
			transform.position = desiredPosition;
		}



		//transform.position = target.position - offset;
		if (downShift == 0) {
			if (transform.position.y < targetPlayer.position.y) {
				//transform.position = new Vector3 (transform.position.x, targetPlayer.position.y, transform.position.z);
				transform.position = new Vector3 (transform.position.x, targetPlayer.position.y, transform.position.z);
			}
		} else {
			//transform.position = new Vector3 (transform.position.x, targetPlayer.position.y + downShift/100, transform.position.z);
			fastFov = 60 - downShift/6;
			//Camera.main.fieldOfView = nextFov + fastFov * setFov;
			Camera.main.fieldOfView = fastFov;
		}
		//Debug.Log(downShift);

		//transform.position = new Vector3 (transform.position.x, transform.position.y + downShift, transform.position.x);

		//transform.LookAt(targetPlayer.transform);
		transform.LookAt(pivot.transform);
		//pivotPos = targetPlayer.transform;
		//pivotPos.y = targetPlayer.transform.y + cameraYOffset;
		//transform.LookAt(pivotPos);
		
		
		
		
		
		
		

	}
	
	// Function to check if a collider is thin
	bool IsThinObject(Collider collider)
	{
		//Debug.Log(collider.bounds.size.magnitude);
		
		// Check the criteria for a "thin" object
		if (collider.bounds.size.magnitude < thinObjectSize)
		{
			return true;
		}
		if ((collider as MeshCollider) != null && (collider as MeshCollider).sharedMesh.vertexCount < thinObjectVertexCount)
		{
			return true;
		}
		if (collider.gameObject.GetComponent<MeshFilter>() && collider.gameObject.GetComponent<MeshFilter>().sharedMesh.vertexCount < thinObjectVertexCount)
		{
			return true;
		}
		return false;
	}
	
	public void doInvertY(){
		if(inv==1){inv=-1;}
		else{inv=1;}
	}
	
	

	
}

