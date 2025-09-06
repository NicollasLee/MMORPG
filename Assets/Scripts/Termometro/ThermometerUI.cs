using UnityEngine;
using UnityEngine.UI;

public class ThermometerUI : MonoBehaviour
{
    [Header("Referências")]
    public RectTransform fillArea;   // arraste o RectTransform do Fill
    public RectTransform cursor;     // arraste o RectTransform do Cursor

    [Header("Faixa de Temperatura (°C)")]
    public float minTemp = -30f;
    public float maxTemp = 50f;

    [Header("Teste manual")]
    [Range(-30, 50)] public float debugTemp = 0f;

    float minY, maxY;

    void Start()
    {
        // limites do fill em espaço local (de baixo pra cima)
        var r = fillArea.rect;
        minY = -r.height * 0.5f;
        maxY = r.height * 0.5f;
    }

    void Update()
    {
        // escolha: use debugTemp ou troque por sua variável real de temperatura
        float t = Mathf.InverseLerp(minTemp, maxTemp, debugTemp);
        float y = Mathf.Lerp(minY, maxY, t);
        var p = cursor.anchoredPosition;
        cursor.anchoredPosition = new Vector2(p.x, y);
    }
}
