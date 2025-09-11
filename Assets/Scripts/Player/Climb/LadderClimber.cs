using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Traversal/Ladder Climber")]
public class LadderClimber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    [SerializeField] private MoveController moveController; // usa SetSuspendedByLadder(true/false)

    [Header("Anim Params")]
    [SerializeField] private string boolOnLadder = "OnLadder";
    [SerializeField] private string floatLadderY = "LadderY";
    [SerializeField] private string trigUpStart = "UpStart";
    [SerializeField] private string trigUpEnd = "UpEnd";
    [SerializeField] private string trigDownEnd = "DownEnd";

    [Header("Animator State Paths (full path)")]
    [Tooltip("Caminho completo do estado Up_Play (ex.: \"Base Layer.Ladder.Up_Play\")")]
    [SerializeField] private string upPlayStatePath = "Base Layer.Ladder.Up_Play";
    [Tooltip("Caminho completo do estado HangIdle (ex.: \"Base Layer.Ladder.HangIdle\")")]
    [SerializeField] private string hangIdleStatePath = "Base Layer.Ladder.HangIdle";

    [Header("Detect/Attach")]
    [SerializeField] private LayerMask ladderMask;
    [SerializeField] private float probeRadius = 0.35f;
    [SerializeField] private float attachDistance = 0.6f;
    [SerializeField] private float wallOffset = 0.25f;
    [SerializeField] private float alignSpeed = 25f;

    [Header("Climb")]
    [SerializeField] private float climbSpeed = 1.8f;
    [SerializeField] private float deadZone = 0.1f;

    [Header("Exit / Ground Snap")]
    [SerializeField] private LayerMask groundMask = ~0; // DESMARQUE a Layer Ladder aqui
    [SerializeField] private float extraForwardAtTop = 0.30f;
    [SerializeField] private float groundSnapClearance = 0.02f;
    [SerializeField] private float edgeDetachThreshold = 0.15f;

    // Estado/eixos
    private Transform curLadder, tBottom, tTop;
    private Vector3 basePos, upAxis, normal;
    private float minDot, maxDot, curDot;

    private bool onLadder;
    private float inputY;

    // Cache CC/Animator
    private float originalStepOffset;
    private int upPlayHash, hangIdleHash;

    void Reset()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        moveController = GetComponent<MoveController>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!controller) controller = GetComponent<CharacterController>();
        if (!moveController) moveController = GetComponent<MoveController>();

        // Hash dos caminhos completos (evita ambiguidade em sub state machines)
        upPlayHash = Animator.StringToHash(upPlayStatePath);
        hangIdleHash = Animator.StringToHash(hangIdleStatePath);
    }

    void OnDisable()
    {
        if (moveController) moveController.SetSuspendedByLadder(false);
        if (animator) animator.SetBool(boolOnLadder, false);
        onLadder = false;
    }

    // ===================== INPUT =====================
    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 v = ctx.ReadValue<Vector2>();
        inputY = Mathf.Clamp(v.y, -1f, 1f);
        // Obs: também atualizamos LadderY no Update para não depender do evento
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (onLadder)
        {
            TryDetachAtCurrentHeight(); // solta na altura atual
            return;
        }

        // Detecta escada à frente
        Vector3 origin = controller ? controller.bounds.center : transform.position;
        Vector3 probePos = origin + transform.forward.normalized * attachDistance;
        var hits = Physics.OverlapSphere(probePos, probeRadius, ladderMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        // escolhe o collider mais próximo
        Collider best = hits[0];
        float bestSqr = float.MaxValue;
        Vector3 myPos = transform.position;
        foreach (var h in hits)
        {
            float d = (h.ClosestPoint(myPos) - myPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = h; }
        }

        Transform ladderRoot = best.transform.root;
        Transform bottom = ladderRoot.Find("Bottom");
        Transform top = ladderRoot.Find("Top");
        if (!bottom || !top) return;

        Attach(ladderRoot, bottom, top);
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !onLadder) return;

        // Se perto do topo (ou há piso à frente), sai por cima via anima
        if (IsAtTop() || HasTopGroundAhead())
        {
            if (animator) animator.SetTrigger(trigUpEnd);
            return;
        }
        if (IsAtBottom())
        {
            if (animator) animator.SetTrigger(trigDownEnd);
            return;
        }

        // meio da escada → solta na altura atual
        TryDetachAtCurrentHeight();
    }

    // ===================== LOOP =====================
    void Update()
    {
        if (!onLadder) return;
        if (controller == null || !controller.enabled) return;

        // Atualiza LadderY SEMPRE (não depende do OnMove disparar novamente)
        if (animator)
            animator.SetFloat(floatLadderY, Mathf.Abs(inputY) < deadZone ? 0f : inputY);

        // Alinhar rotação
        Quaternion targetRot = Quaternion.LookRotation(-normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, alignSpeed * Time.deltaTime);

        // Mover ao longo da escada
        float y = Mathf.Abs(inputY) < deadZone ? 0f : inputY;
        curDot += y * climbSpeed * Time.deltaTime;
        curDot = Mathf.Clamp(curDot, minDot, maxDot);

        // Sair no topo/fundo quando empurra na direção
        if (animator)
        {
            if (IsAtTop() && inputY > 0.1f)
            {
                animator.ResetTrigger(trigDownEnd);
                animator.SetTrigger(trigUpEnd);
            }
            if (IsAtBottom() && inputY < -0.1f)
            {
                animator.ResetTrigger(trigUpEnd);
                animator.SetTrigger(trigDownEnd);
            }
        }

        // Snap contínuo com CC
        Vector3 target = basePos + upAxis * curDot + normal * wallOffset;
        Vector3 delta = target - transform.position;
        if (delta.sqrMagnitude > 0.000001f)
            controller.Move(delta);
    }

    // ===================== ATTACH / DETACH =====================
    private void Attach(Transform ladder, Transform bottom, Transform top)
    {
        curLadder = ladder;
        tBottom = bottom;
        tTop = top;

        upAxis = (tTop.position - tBottom.position).normalized;
        normal = -ladder.forward; // inverta o sinal se olhar pro lado errado

        basePos = tBottom.position;
        minDot = 0f;
        maxDot = Vector3.Dot(tTop.position - basePos, upAxis);
        curDot = Mathf.Clamp(Vector3.Dot(transform.position - basePos, upAxis), minDot, maxDot);

        Vector3 snap = basePos + upAxis * curDot + normal * wallOffset;

        originalStepOffset = controller.stepOffset;
        controller.stepOffset = 0f;

        Vector3 deltaSnap = snap - transform.position;
        if (deltaSnap.sqrMagnitude > 0.000001f)
            controller.Move(deltaSnap);

        if (moveController) moveController.SetSuspendedByLadder(true);

        if (animator)
        {
            animator.SetBool(boolOnLadder, true);
            animator.SetFloat(floatLadderY, 0f);
            animator.ResetTrigger(trigUpStart);
            animator.SetTrigger(trigUpStart); // anima de entrada
        }

        onLadder = true;

        // ✅ FIX: se já estiver segurando W, força o estado nítido no próximo frame
        StartCoroutine(ForceStartStateNextFrame());
    }

    public void OnLadderExitToTop() => TryDetach(true);
    public void OnLadderExitToBottom() => TryDetach(false);

    private IEnumerator ForceStartStateNextFrame()
    {
        // espera o Animator processar os triggers e o snap concluir
        yield return null;

        if (!onLadder || animator == null) yield break;

        // Se W está pressionado, vai DIRETO para Up_Play (pelo caminho completo)
        if (inputY > deadZone)
            animator.Play(upPlayHash, 0, 0f);
        else
            animator.Play(hangIdleHash, 0, 0f);
    }

    // === helpers topo/fundo/ground ===
    private bool IsAtTop() => Mathf.Abs(curDot - maxDot) < edgeDetachThreshold;
    private bool IsAtBottom() => Mathf.Abs(curDot - minDot) < edgeDetachThreshold;

    private bool HasTopGroundAhead()
    {
        Vector3 probe = tTop.position + normal * (wallOffset + extraForwardAtTop) + Vector3.up * 0.6f;
        // usar SphereCast para achar piso onde o player caberia
        float r = controller ? Mathf.Max(0.05f, controller.radius * 0.9f) : 0.3f;
        return Physics.SphereCast(probe, r, Vector3.down, out _, 2.5f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private bool SnapFeetToGround(ref Vector3 pos, float maxDistance = 2.5f)
    {
        // SphereCast pra achar piso sob os pés
        float r = controller ? Mathf.Max(0.05f, controller.radius * 0.9f) : 0.3f;
        Vector3 origin = pos + Vector3.up * 0.6f;
        if (Physics.SphereCast(origin, r, Vector3.down, out var hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            float half = controller ? controller.height * 0.5f : 0.9f;
            pos.y = hit.point.y + half + (controller ? controller.skinWidth : 0f) + groundSnapClearance;
            return true;
        }
        return false;
    }

    private bool TryDetach(bool toTop)
    {
        if (!onLadder || controller == null || !controller.enabled) return false;

        Vector3 exitBase = toTop ? tTop.position : tBottom.position;
        float forward = wallOffset + (toTop ? extraForwardAtTop : 0.06f);
        Vector3 exitPos = exitBase + normal * forward;

        if (toTop) SnapFeetToGround(ref exitPos);

        Vector3 delta = exitPos - transform.position;
        if (delta.sqrMagnitude > 0.000001f) controller.Move(delta);

        if (!toTop) controller.Move(Vector3.down * 0.05f);

        controller.stepOffset = originalStepOffset;
        if (moveController) moveController.SetSuspendedByLadder(false);

        if (animator)
        {
            animator.SetBool(boolOnLadder, false);
            animator.ResetTrigger(trigUpStart);
            animator.ResetTrigger(trigUpEnd);
            animator.ResetTrigger(trigDownEnd);
            animator.SetFloat(floatLadderY, 0f);
        }

        curLadder = null; onLadder = false;
        return true;
    }

    private bool TryDetachAtCurrentHeight()
    {
        if (!onLadder || controller == null || !controller.enabled) return false;

        Vector3 exitPos = basePos + upAxis * curDot + normal * (wallOffset + 0.06f);
        Vector3 delta = exitPos - transform.position;
        if (delta.sqrMagnitude > 0.000001f) controller.Move(delta);

        controller.Move(Vector3.down * 0.05f);
        controller.stepOffset = originalStepOffset;

        if (moveController) moveController.SetSuspendedByLadder(false);

        if (animator)
        {
            animator.SetBool(boolOnLadder, false);
            animator.ResetTrigger(trigUpStart);
            animator.ResetTrigger(trigUpEnd);
            animator.ResetTrigger(trigDownEnd);
            animator.SetFloat(floatLadderY, 0f);
        }

        curLadder = null; onLadder = false;
        return true;
    }

    // ===================== DEBUG =====================
    void OnDrawGizmosSelected()
    {
        Vector3 origin = controller ? controller.bounds.center : transform.position;
        Vector3 probePos = origin + transform.forward.normalized * attachDistance;

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
        Gizmos.DrawSphere(probePos, probeRadius);

        if (!onLadder || tBottom == null || tTop == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(tBottom.position, tTop.position);

        Gizmos.color = Color.cyan;
        Vector3 p = basePos + upAxis * curDot;
        Gizmos.DrawRay(p, normal * wallOffset);
    }
}
