using UnityEngine;

[AddComponentMenu("Climb/Ladder Zone")]
public class LadderZone : MonoBehaviour
{
    [Header("Refs")]
    public Transform bottom;
    public Transform top;

    [Header("Snap")]
    public float snapForward = 0.25f; // o quanto o player fica “colado” na escada

    void Reset()
    {
        var b = new GameObject("Bottom").transform; b.SetParent(transform); b.localPosition = Vector3.zero;
        var t = new GameObject("Top").transform; t.SetParent(transform); t.localPosition = Vector3.up * 3f;
        bottom = b; top = t;

        var box = GetComponent<BoxCollider>();
        if (!box) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
    }

    // direcao “para cima” da escada
    public Vector3 UpDir => (top.position - bottom.position).normalized;

    // normal “de frente” da escada (para onde o player olha).
    // assumindo que o eixo Z+ do Ladder aponta para fora da escada:
    public Vector3 Front => transform.forward;

    // ponto “snapado” no eixo vertical da escada, clamped entre bottom e top
    public Vector3 GetSnapPoint(float t01, float skinForward)
    {
        t01 = Mathf.Clamp01(t01);
        Vector3 line = Vector3.Lerp(bottom.position, top.position, t01);
        return line - Front * skinForward; // puxa para “grudar” na escada
    }

    public float Height => Vector3.Distance(bottom.position, top.position);

    // converte posição do player para t em [0..1] ao longo da escada
    public float WorldToT(Vector3 worldPos)
    {
        var v = worldPos - bottom.position;
        float along = Vector3.Dot(v, UpDir);
        return Mathf.InverseLerp(0f, Height, along);
    }
}
