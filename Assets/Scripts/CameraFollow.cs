using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5.4f;
    [SerializeField] private float brakingDistance = 4.05f;
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float mouseSensitivity = 0.19f;
    [SerializeField] private float stickDegreesPerSecond = 145f;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private CarController car;
    private float heading;
    private float orbitYaw;
    private float orbitPitch;
    private float currentDistance;
    private float visibleDistance;
    private float roll;
    private Vector3 pivot;
    private CursorLockMode previousLock;
    private bool previousCursorVisible;

    private void OnEnable()
    {
        if (target == null)
            return;

        car = target.GetComponent<CarController>();
        heading = target.eulerAngles.y;
        orbitYaw = 0f;
        orbitPitch = 12f;
        currentDistance = visibleDistance = distance;
        roll = 0f;
        pivot = GetPivot();
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
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                CaptureCursor();

            Vector2 mouseLook = Cursor.lockState == CursorLockMode.Locked
                ? (mouse?.delta.ReadValue() ?? Vector2.zero) * mouseSensitivity : Vector2.zero;
            Vector2 stick = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            Vector2 look = mouseLook + stick * (stickDegreesPerSecond * delta);
            orbitYaw = Mathf.Repeat(orbitYaw + look.x + 180f, 360f) - 180f;
            orbitPitch = Mathf.Clamp(orbitPitch + look.y, -5f, 65f);
            if (Gamepad.current?.rightStickButton.wasPressedThisFrame == true)
            {
                orbitYaw = 0f;
                orbitPitch = 12f;
            }
        }

        UpdateView(delta, false);
    }

    private void UpdateView(float delta, bool snap)
    {
        float blend = snap ? 1f : 1f - Mathf.Exp(-followSpeed * delta);
        float braking = car != null ? car.Braking : 0f;
        float turnRate = car != null ? car.TurnRate : 0f;

        heading = Mathf.LerpAngle(heading, target.eulerAngles.y, blend * 0.75f);
        pivot = Vector3.Lerp(pivot, GetPivot(), blend);
        currentDistance = Mathf.Lerp(currentDistance, Mathf.Lerp(distance, brakingDistance, braking), blend);
        roll = Mathf.Lerp(roll, Mathf.Clamp(-turnRate * 5f, -9f, 9f), blend);

        Quaternion orbit = Quaternion.Euler(orbitPitch, heading + orbitYaw, 0f);
        Vector3 direction = orbit * Vector3.back;
        float clearance = FindClearance(direction);
        visibleDistance = clearance < visibleDistance ? clearance : Mathf.Lerp(visibleDistance, clearance, blend);
        Quaternion rotation = orbit * Quaternion.AngleAxis(roll, Vector3.forward);
        transform.SetPositionAndRotation(pivot + direction * visibleDistance, rotation);
    }

    private Vector3 GetPivot()
    {
        return target.position + Vector3.up * 1.38f;
    }

    private float FindClearance(Vector3 direction)
    {
        float clearance = currentDistance;
        int count = Physics.SphereCastNonAlloc(pivot, 0.2f, direction, hits, currentDistance, ~0,
            QueryTriggerInteraction.Ignore);
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
