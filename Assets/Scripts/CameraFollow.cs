using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera), typeof(AudioListener))]
[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Дистанция")]
    [SerializeField] private float pivotHeight = 1.38f;
    [SerializeField, Range(0f, 0.4f)] private float brakingPullIn = 0.25f;
    [SerializeField, Min(0f)] private float maxSpeedPullback = 0.35f;
    [SerializeField] private float maxSpeedKphForEffects = 140f;

    [Header("Плавность и повороты")]
    [SerializeField] private float pivotFollowSpeed = 24f;
    [SerializeField] private float headingFollowSpeed = 4.2f;
    [SerializeField] private float distanceFollowSpeed = 6f;
    [SerializeField] private float maxRoll = 5.5f;

    [Header("Обзор и автовозврат")]
    [SerializeField] private float mouseSensitivity = 0.19f;
    [SerializeField] private float legacyMouseSensitivity = 2.5f;
    [SerializeField] private float stickDegreesPerSecond = 145f;
    [SerializeField] private float autoReturnDelay = 1.4f;
    [SerializeField] private float autoReturnSpeed = 3.5f;

    [Header("Динамический FOV")]
    [SerializeField, Range(0f, 40f)] private float speedFovIncrease = 22f;
    [SerializeField] private float fovLerpSpeed = 5f;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private Camera cam;
    private AudioListener listener;
    private CarController car;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 startDirection;
    private Quaternion startRotation;
    private Vector3 pitchAxis;
    private float startDistance;
    private float startFov;
    private float startPitch;
    private bool viewActive;
    private float heading;
    private float orbitYaw;
    private float orbitPitch;
    private float orbitIdleTimer;
    private float currentDistance;
    private float visibleDistance;
    private float roll;
    private Vector3 pivot;
    private CursorLockMode previousLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        listener = GetComponent<AudioListener>();
        if (target == null)
            return;

        // Capture the pose once, before the player or vehicle moves.
        car = target.GetComponent<CarController>();
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        startFov = cam.fieldOfView;
        Quaternion inverseHeading = Quaternion.Inverse(Quaternion.Euler(0f, target.eulerAngles.y, 0f));
        Vector3 offset = inverseHeading * (transform.position - GetPivot());
        startDistance = Mathf.Max(0.25f, offset.magnitude);
        startDirection = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.back;
        startRotation = inverseHeading * transform.rotation;
        pitchAxis = Vector3.Cross(startDirection, Vector3.up).normalized;
        if (pitchAxis.sqrMagnitude < 0.001f)
            pitchAxis = Vector3.right;
        startPitch = Mathf.Asin(startDirection.y) * Mathf.Rad2Deg;
    }

    private void OnEnable()
    {
        if (target == null)
            return;

        heading = target.eulerAngles.y;
        orbitYaw = 0f;
        orbitPitch = 0f;
        orbitIdleTimer = autoReturnDelay;
        currentDistance = visibleDistance = startDistance;
        roll = 0f;
        pivot = GetPivot();

        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        CaptureCursor();
        UpdateView(0f, true);
        cam.enabled = true;
        listener.enabled = true;
        viewActive = true;
    }

    private void OnDisable()
    {
        if (!viewActive)
            return;

        viewActive = false;
        listener.enabled = false;
        cam.enabled = false;
        cam.fieldOfView = startFov;
        transform.SetLocalPositionAndRotation(startLocalPosition, startLocalRotation);
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float delta = Time.deltaTime;
        if (!GameManager.paused)
        {
            Mouse mouse = Mouse.current;
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || Input.GetMouseButtonDown(0))
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                    CaptureCursor();
            }

            Vector2 mouseLook = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse != null)
                    mouseLook = mouse.delta.ReadValue() * mouseSensitivity;
                else
                    mouseLook = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * legacyMouseSensitivity;
            }

            Vector2 stick = VehicleInput.LookStick;
            Vector2 look = mouseLook + stick * (stickDegreesPerSecond * delta);

            if (look.sqrMagnitude > 0.0001f)
            {
                orbitYaw = Mathf.Repeat(orbitYaw + look.x + 180f, 360f) - 180f;
                orbitPitch = Mathf.Clamp(startPitch + orbitPitch - look.y,
                    Mathf.Min(startPitch, -5f), Mathf.Max(startPitch, 65f)) - startPitch;
                orbitIdleTimer = 0f;
            }
            else
            {
                orbitIdleTimer += delta;
                if (orbitIdleTimer >= autoReturnDelay)
                {
                    float ret = 1f - Mathf.Exp(-autoReturnSpeed * delta);
                    orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, ret);
                    orbitPitch = Mathf.Lerp(orbitPitch, 0f, ret);
                }
            }

            if (VehicleInput.ResetLookPressed)
            {
                orbitYaw = 0f;
                orbitPitch = 0f;
                orbitIdleTimer = autoReturnDelay;
            }
        }

        UpdateView(delta, false);
    }

    private void UpdateView(float delta, bool snap)
    {
        float pivotBlend = snap ? 1f : 1f - Mathf.Exp(-pivotFollowSpeed * delta);
        float headingBlend = snap ? 1f : 1f - Mathf.Exp(-headingFollowSpeed * delta);
        float distBlend = snap ? 1f : 1f - Mathf.Exp(-distanceFollowSpeed * delta);

        float braking = car != null ? car.Braking : 0f;
        float turnRate = car != null ? car.TurnRate : 0f;
        float speedNorm = car != null ? Mathf.Clamp01(car.SpeedKph / Mathf.Max(1f, maxSpeedKphForEffects)) : 0f;

        // Запаздывание поворота камеры за машиной
        heading = Mathf.LerpAngle(heading, target.eulerAngles.y, headingBlend);
        pivot = Vector3.Lerp(pivot, GetPivot(), pivotBlend);

        // Контролируемый отъезд назад от скорости + приближение при торможении
        float brakingDistance = startDistance * (1f - brakingPullIn);
        float targetDistance = Mathf.Lerp(startDistance + speedNorm * maxSpeedPullback, brakingDistance, braking);
        currentDistance = snap ? startDistance : Mathf.Lerp(currentDistance, targetDistance, distBlend);

        // Легкий крен в поворотах
        float targetRoll = Mathf.Clamp(-turnRate * 3.2f, -maxRoll, maxRoll);
        roll = snap ? 0f : Mathf.Lerp(roll, targetRoll, distBlend);

        // Динамический FOV
        float targetFov = Mathf.Min(170f, startFov + speedNorm * speedFovIncrease);
        cam.fieldOfView = snap ? startFov : Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovLerpSpeed * delta));

        Quaternion orbit = Quaternion.Euler(0f, heading + orbitYaw, 0f)
            * Quaternion.AngleAxis(orbitPitch, pitchAxis);
        Vector3 direction = orbit * startDirection;
        float clearance = FindClearance(direction);
        visibleDistance = clearance < visibleDistance ? clearance : Mathf.Lerp(visibleDistance, clearance, distBlend);

        Quaternion rotation = orbit * startRotation * Quaternion.AngleAxis(roll, Vector3.forward);
        transform.SetPositionAndRotation(pivot + direction * visibleDistance, rotation);
    }

    private Vector3 GetPivot() => target.position + Vector3.up * pivotHeight;

    private float FindClearance(Vector3 direction)
    {
        float clearance = currentDistance;
        int count = Physics.SphereCastNonAlloc(pivot, 0.2f, direction, hits, currentDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.transform.IsChildOf(target))
                continue;
            clearance = Mathf.Min(clearance, Mathf.Max(0.25f, hit.distance - 0.12f));
        }
        return clearance;
    }

    private static void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
