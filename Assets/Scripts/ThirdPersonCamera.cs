using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;       // arraste o Player aqui
    public float followDistance = 3f;
    public float height = 3f;
    public float followDamp = 2f; // suavidade de posição
    public float lookDamp = 2f;   // suavidade de rotação

    void LateUpdate()
    {
        if (target == null) return;

        // posição: atrás do player, na altura definida
        Vector3 desiredPos = target.position - target.forward * followDistance + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followDamp * Time.deltaTime));

        // olha para o player suavemente
        Vector3 lookPoint = target.position + Vector3.up * (height * 0.5f);
        Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-lookDamp * Time.deltaTime));
    }
}
