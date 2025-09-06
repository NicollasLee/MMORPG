// em MinimapCamera
using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    public Transform alvo;
    public float altura = 40f;
    public bool rotacionarComJogador = true;

    void LateUpdate()
    {
        if (!alvo) return;
        Vector3 p = alvo.position; p.y += altura;
        transform.position = p;

        float yaw = rotacionarComJogador ? alvo.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }
}
