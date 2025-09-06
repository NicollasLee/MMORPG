using UnityEngine;
using UnityEngine.UI;

public class StaminaWheelUI : MonoBehaviour
{
    public MoveController alvo;    // arraste o Player
    public Image barra;            // StaminaFill (o da frente)
    public CanvasGroup grupo;      // add CanvasGroup no Canvas
    public float delaySumir = 0.6f;
    public float velFade = 6f;
    public Color corCheia = new Color(0.2f, 1f, 0.4f, 1f); // verde
    public Color corBaixa = new Color(1f, 0.3f, 0.2f, 1f);  // vermelho
    [Range(0f, 1f)] public float limiarBaixo = 0.2f;

    float tMostrarAte;

    void LateUpdate()
    {
        if (!alvo || !barra) return;

        barra.fillAmount = alvo.Stamina01;

        bool precisaMostrar = alvo.Correndo || barra.fillAmount < 0.999f;
        if (precisaMostrar) tMostrarAte = Time.time + delaySumir;

        float alvoAlpha = (Time.time < tMostrarAte) ? 1f : 0f;
        if (grupo)
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alvoAlpha, velFade * Time.deltaTime);

        float s = alvo.Stamina01;
        Color alvoCor = (s <= limiarBaixo) ? corBaixa : corCheia;
        barra.color = Color.Lerp(barra.color, alvoCor, 10f * Time.deltaTime);
    }
}
