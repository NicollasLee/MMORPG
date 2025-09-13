using UnityEngine;

[AddComponentMenu("Traversal/Ladder Zone")]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class LadderZone : MonoBehaviour
{
    [Header("Refs (auto se faltar)")]
    [SerializeField] private Transform bottom;
    [SerializeField] private Transform top;

    [Header("Snap")]
    [SerializeField, Min(0f)] private float snapForward = 0.25f; // quão “colado” o player fica da escada

    /// <summary>Direção "para cima" da escada (normalizada).</summary>
    public Vector3 UpDir => (top.position - bottom.position).normalized;

    /// <summary>Frente da escada (para onde o player olha). Assume Z+ apontando para fora.</summary>
    public Vector3 Front => transform.forward;

    /// <summary>Altura total da escada (metros).</summary>
    public float Height => Vector3.Distance(bottom.position, top.position);

    /// <summary>Retorna ponto "snapado" ao eixo vertical da escada (t ∈ [0..1]).</summary>
    public Vector3 GetSnapPoint(float t01, float skinForward)
    {
        t01 = Mathf.Clamp01(t01);
        Vector3 line = Vector3.Lerp(bottom.position, top.position, t01);
        return line - Front * skinForward;
    }

    /// <summary>Converte posição no mundo para t ∈ [0..1] ao longo da escada.</summary>
    public float WorldToT(Vector3 worldPos)
    {
        Vector3 v = worldPos - bottom.position;
        float along = Vector3.Dot(v, UpDir);
        return Mathf.InverseLerp(0f, Height, along);
    }

    /// <summary>Garante filhos Bottom/Top e BoxCollider como trigger.</summary>
    private void Reset()
    {
        if (!bottom)
        {
            var b = new GameObject("Bottom").transform;
            b.SetParent(transform); b.localPosition = Vector3.zero;
            bottom = b;
        }
        if (!top)
        {
            var t = new GameObject("Top").transform;
            t.SetParent(transform); t.localPosition = Vector3.up * 3f;
            top = t;
        }

        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    /// <summary>Valida referências e impede Bottom/Top no mesmo ponto.</summary>
    private void OnValidate()
    {
        var box = GetComponent<BoxCollider>();
        if (box) box.isTrigger = true;

        if (bottom == null || top == null) return;
        if (Vector3.Distance(bottom.position, top.position) < 0.001f)
            top.position = bottom.position + Vector3.up * 0.1f;
        if (snapForward < 0f) snapForward = 0f;
    }

    /// <summary>Desenha gizmos de ajuda no editor.</summary>
    private void OnDrawGizmos()
    {
        if (!bottom || !top) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(bottom.position, top.position);

        Gizmos.color = Color.cyan;
        Vector3 mid = Vector3.Lerp(bottom.position, top.position, 0.5f);
        Gizmos.DrawRay(mid, Front * 0.5f);
    }
}
