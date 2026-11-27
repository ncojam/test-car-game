using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Дистанция")]
    [SerializeField] private float distance = 5.2f;
    [SerializeField] private float brakingDistance = 4.2f;
    [SerializeField] private float maxSpeedPullback = 0.75f;
    [SerializeField] private float maxSpeedKphForEffects = 140f;

    [Header("Плавность и повороты")]
    [SerializeField] private float pivotFollowSpeed = 24f;
    [SerializeField] private float headingFollowSpeed = 4.2f;
    [SerializeField] private float distanceFollowSpeed = 6f;
    [SerializeField] private float maxRoll = 5.5f;

    [Header("Обзор и автовозврат")]
    [SerializeField] private float defaultPitch = 12f;
    [SerializeField] private float mouseSensitivity = 0.19f;
    [SerializeField] private float legacyMouseSensitivity = 2.5f;
    [SerializeField] private float stickDegreesPerSecond = 145f;
    [SerializeField] private float autoReturnDelay = 1.4f;
    [SerializeField] private float autoReturnSpeed = 3.5f;

    [Header("Динамический FOV")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxFov = 76f;
    [SerializeField] private float fovLerpSpeed = 5f;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private Camera cam;
    private CarController car;
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
    }

    private void OnEnable()
    {
        if (target == null)
            return;

        car = target.GetComponent<CarController>();
        heading = target.eulerAngles.y;
        orbitYaw = 0f;
        orbitPitch = defaultPitch;
        orbitIdleTimer = autoReturnDelay;
        currentDistance = visibleDistance = distance;
        roll = 0f;
        pivot = GetPivot();

        if (cam != null)
            cam.fieldOfView = baseFov;

        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        CaptureCursor();
        UpdateView(0f, true);
    }

    private void OnDisable()
    {
        if (target == null)
            return;

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
                orbitPitch = Mathf.Clamp(orbitPitch - look.y, -5f, 65f);
                orbitIdleTimer = 0f;
            }
            else
            {
                orbitIdleTimer += delta;
                if (orbitIdleTimer >= autoReturnDelay)
                {
                    float ret = 1f - Mathf.Exp(-autoReturnSpeed * delta);
                    orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, ret);
                    orbitPitch = Mathf.Lerp(orbitPitch, defaultPitch, ret);
                }
            }

            if (Gamepad.current?.rightStickButton.wasPressedThisFrame == true)
            {
                orbitYaw = 0f;
                orbitPitch = defaultPitch;
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
        float speedNorm = car != null ? Mathf.Clamp01(car.SpeedKph / maxSpeedKphForEffects) : 0f;

        // Запаздывание поворота камеры за машиной
        heading = Mathf.LerpAngle(heading, target.eulerAngles.y, headingBlend);
        pivot = Vector3.Lerp(pivot, GetPivot(), pivotBlend);

        // Контролируемый отъезд назад от скорости + приближение при торможении
        float targetDistance = Mathf.Lerp(distance + speedNorm * maxSpeedPullback, brakingDistance, braking);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, distBlend);

        // Легкий крен в поворотах
        float targetRoll = Mathf.Clamp(-turnRate * 3.2f, -maxRoll, maxRoll);
        roll = Mathf.Lerp(roll, targetRoll, distBlend);

        // Динамический FOV
        if (cam != null)
        {
            float targetFov = Mathf.Lerp(baseFov, maxFov, speedNorm);
            cam.fieldOfView = snap ? targetFov : Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovLerpSpeed * delta));
        }

        Quaternion orbit = Quaternion.Euler(orbitPitch, heading + orbitYaw, 0f);
        Vector3 direction = orbit * Vector3.back;
        float clearance = FindClearance(direction);
        visibleDistance = clearance < visibleDistance ? clearance : Mathf.Lerp(visibleDistance, clearance, distBlend);

        Quaternion rotation = orbit * Quaternion.AngleAxis(roll, Vector3.forward);
        transform.SetPositionAndRotation(pivot + direction * visibleDistance, rotation);
    }

    private Vector3 GetPivot() => target.position + Vector3.up * 1.38f;

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