using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    public Camera cam;
    public float minSize = 20f, maxSize = 80f, sens = 10f;

    void Reset() { cam = GetComponent<Camera>(); }

    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * sens,
                                               minSize, maxSize);
        }
    }
}
