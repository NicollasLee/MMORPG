using UnityEngine;

[System.Serializable]
public struct LedgeInfo
{
    public Vector3 wallPoint;   // ponto do impacto na parede
    public Vector3 normal;      // normal da parede
    public Vector3 topPoint;    // ponto no topo (beiral)
    public Vector3 hangPoint;   // onde o peito/mãos “param” (snap)
    public Vector3 standPoint;  // onde o player deve ficar ao subir
}

[AddComponentMenu("Movement/Ledge Detector")]
public class LedgeDetector : MonoBehaviour
{
    [Header("Refs")]
    public CharacterController controller;

    [Header("Mask / Distâncias")]
    public LayerMask climbMask;             // defina p/ Layer "Climbable"
    public float wallCheckDist = 0.8f;      // quão longe procurar parede
    public float chestHeight = 1.2f;        // altura do peito p/ checar parede
    public float ledgeMin = 0.4f;           // quanto acima do peito o beiral pode estar (mín)
    public float ledgeMax = 1.6f;           // quanto acima do peito o beiral pode estar (máx)

    [Header("Offsets")]
    public float hangForwardOffset = 0.42f; // distância do corpo até a parede
    public float hangDownOffset = 0.60f;    // o quanto abaixo do topo as mãos ficam
    public float standForwardOffset = 0.30f;// offset ao subir, p/ não “sair voando”

    public bool FindLedge(out LedgeInfo info, Vector3 fwdHint)
    {
        info = default;

        Vector3 originWall = transform.position + Vector3.up * chestHeight;
        Vector3 dir = fwdHint.sqrMagnitude > 0.0001f ? fwdHint.normalized : transform.forward;

        // 1) detecta parede à frente
        if (!Physics.SphereCast(originWall, controller.radius * 0.5f, dir, out var wallHit, wallCheckDist, climbMask, QueryTriggerInteraction.Ignore))
            return false;

        // 2) a partir de cima da borda, “olha” pra baixo para achar o topo/beiral
        Vector3 over = wallHit.point + Vector3.up * ledgeMax - dir * 0.2f; // um pouco recuado
        if (!Physics.Raycast(over, Vector3.down, out var downHit, ledgeMax - ledgeMin, climbMask, QueryTriggerInteraction.Ignore))
            return false;

        info.wallPoint = wallHit.point;
        info.normal = wallHit.normal;
        info.topPoint = downHit.point;

        // ponto de pendurar (peito/mãos)
        float hangY = downHit.point.y - hangDownOffset;
        Vector3 outFromWall = info.normal * (controller.radius + hangForwardOffset);
        info.hangPoint = new Vector3(wallHit.point.x, hangY, wallHit.point.z) + outFromWall;

        // ponto onde o player “fica em pé” ao subir
        info.standPoint = downHit.point + info.normal * standForwardOffset;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!controller) controller = GetComponent<CharacterController>();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * chestHeight, 0.05f);
    }
}
