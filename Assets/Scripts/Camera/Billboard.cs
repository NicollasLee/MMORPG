using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main == null) return;
        transform.rotation = Quaternion.LookRotation(
            transform.position - Camera.main.transform.position, Vector3.up);
    }
}
