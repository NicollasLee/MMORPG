using UnityEngine;

public class MinimapArrow : MonoBehaviour
{
    [Header("Refs")]
    public Transform alvo;                 // Player (Transform)
    public RectTransform seta;             // Image da seta

    [Header("Comportamento")]
    public bool mapaGiraComJogador = true; // igual ao da camera
    [Tooltip("Correção para seu sprite (0, 90, 180, 270).")]
    public float offsetGraus = 0f;         // coloque 180 se estiver “de cabeça pra baixo”

    void LateUpdate()
    {
        if (!alvo || !seta) return;

        if (mapaGiraComJogador)
        {
            // mapa já gira com o player → seta fica fixa “apontando pra cima”
            seta.localEulerAngles = new Vector3(0, 0, offsetGraus);
        }
        else
        {
            // mapa fixo (norte para cima) → seta gira para onde o player olha
            float y = alvo.eulerAngles.y;
            seta.localEulerAngles = new Vector3(0, 0, -y + offsetGraus);
        }
    }
}
