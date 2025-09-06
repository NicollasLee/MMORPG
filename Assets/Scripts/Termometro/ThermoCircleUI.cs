using UnityEngine;

public class ThermoCircleUI : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform needlePivot;   // arraste o NeedlePivot aqui

    [Header("Temperatura (°C)")]
    public float minTemp = -40f;
    public float maxTemp = 40f;

    [Header("Ângulo (graus)")]
    // ângulos extremos do arco (ajuste fino até ficar igual ao visual)
    public float minAngle = 150f;  // extremo frio (lado esquerdo-inferior)
    public float maxAngle = -150f;  // extremo quente (lado direito-inferior)

    [Header("Teste manual")]
    [Range(-40, 40)] public float debugTemp = 0f;

    void Update()
    {
        // troque debugTemp pela sua temp real quando tiver o sistema
        float t = Mathf.InverseLerp(minTemp, maxTemp, debugTemp);
        float angle = Mathf.Lerp(minAngle, maxAngle, t);
        if (needlePivot) needlePivot.localEulerAngles = new Vector3(0, 0, angle);
    }
}
