using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -7f);
    [SerializeField] private float followSpeed = 8f;

    private void OnEnable()
    {
        Follow(1f);
    }

    private void LateUpdate()
    {
        Follow(1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }

    private void Follow(float blend)
    {
        if (target == null)
            return;

        Quaternion heading = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 position = target.position + heading * offset;
        Vector3 lookAt = target.position + heading * new Vector3(0f, 1.4f, 1.5f);
        Quaternion rotation = Quaternion.LookRotation(lookAt - position);

        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, position, blend),
            Quaternion.Slerp(transform.rotation, rotation, blend));
    }
}
