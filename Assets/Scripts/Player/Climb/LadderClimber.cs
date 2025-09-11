using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Traversal/Ladder Climber")]
public class LadderClimber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    [SerializeField] private MoveController moveController;

    [Header("Anim Params")]
    [SerializeField] private string boolOnLadder = "OnLadder";
    [SerializeField] private string floatLadderY = "LadderY";
    [SerializeField] private string trigUpStart = "UpStart";
    [SerializeField] private string trigUpEnd = "UpEnd";
    [SerializeField] private string trigDownEnd = "DownEnd";

    [Header("Fall (queda)")]
    [SerializeField] private string trigFall = "Fall";         // seu trigger de queda (ex.: "Fall" ou "Drop")
    [SerializeField] private string fallStatePath = "Base Layer.Fall"; // caminho completo do estado de queda

    [Header("Animator State Paths (ladder)")]
    [SerializeField] private string upPlayStatePath = "Base Layer.Ladder.Up_Play";
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
    [SerializeField] private LayerMask groundMask = ~0; // desmarque a Layer Ladder
    [SerializeField] private float extraForwardAtTop = 0.30f;
    [SerializeField] private float groundSnapClearance = 0.02f;
    [SerializeField] private float edgeDetachThreshold = 0.15f;

    [Header("Jump-off (impulso ao sair da escada)")]
    [SerializeField] private float jumpOutForward = 0.45f;
    [SerializeField] private float jumpOutUp = 0.35f;

    // estado/eixos
    private Transform curLadder, tBottom, tTop;
    private Vector3 basePos, upAxis, normal;
    private float minDot, maxDot, curDot;
    private bool onLadder;
    private float inputY;

    // cache
    private float originalStepOffset;
    private int upPlayHash, hangIdleHash, fallHash;

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

        upPlayHash = Animator.StringToHash(upPlayStatePath);
        hangIdleHash = Animator.StringToHash(hangIdleStatePath);
        fallHash = Animator.StringToHash(fallStatePath);
    }

    void OnDisable()
    {
        if (moveController) moveController.SetSuspendedByLadder(false);
        if (animator) animator.SetBool(boolOnLadder, false);
        onLadder = false;
    }

    // ===== Input =====
    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 v = ctx.ReadValue<Vector2>();
        inputY = Mathf.Clamp(v.y, -1f, 1f);
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (onLadder) { TryDetachAtCurrentHeight(); return; }

        Vector3 origin = controller ? controller.bounds.center : transform.position;
        Vector3 probePos = origin + transform.forward.normalized * attachDistance;
        var hits = Physics.OverlapSphere(probePos, probeRadius, ladderMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

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

        if (IsAtTop() || HasTopGroundAhead()) { DetachToTop(); return; }

        // fundo ou meio → jump-off + queda
        JumpOffDownOrMid(applyUp: true);
    }

    // ===== Loop =====
    void Update()
    {
        if (!onLadder || controller == null || !controller.enabled) return;

        if (animator)
            animator.SetFloat(floatLadderY, Mathf.Abs(inputY) < deadZone ? 0f : inputY);

        Quaternion targetRot = Quaternion.LookRotation(-normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, alignSpeed * Time.deltaTime);

        float y = Mathf.Abs(inputY) < deadZone ? 0f : inputY;
        curDot += y * climbSpeed * Time.deltaTime;
        curDot = Mathf.Clamp(curDot, minDot, maxDot);

        if (animator)
        {
            if (IsAtTop() && inputY > 0.1f) animator.SetTrigger(trigUpEnd);
            if (IsAtBottom() && inputY < -0.1f) animator.SetTrigger(trigDownEnd);
        }

        Vector3 target = basePos + upAxis * curDot + normal * wallOffset;
        Vector3 delta = target - transform.position;
        if (delta.sqrMagnitude > 0.000001f) controller.Move(delta);
    }

    // ===== Attach / Detach =====
    private void Attach(Transform ladder, Transform bottom, Transform top)
    {
        curLadder = ladder; tBottom = bottom; tTop = top;

        upAxis = (tTop.position - tBottom.position).normalized;
        normal = -ladder.forward;

        basePos = tBottom.position;
        minDot = 0f;
        maxDot = Vector3.Dot(tTop.position - basePos, upAxis);
        curDot = Mathf.Clamp(Vector3.Dot(transform.position - basePos, upAxis), minDot, maxDot);

        Vector3 snap = basePos + upAxis * curDot + normal * wallOffset;

        originalStepOffset = controller.stepOffset;
        controller.stepOffset = 0f;

        Vector3 deltaSnap = snap - transform.position;
        if (deltaSnap.sqrMagnitude > 0.000001f) controller.Move(deltaSnap);

        if (moveController) moveController.SetSuspendedByLadder(true);

        if (animator)
        {
            animator.SetBool(boolOnLadder, true);
            animator.SetFloat(floatLadderY, 0f);
            animator.ResetTrigger(trigUpStart);
            animator.SetTrigger(trigUpStart);
        }

        onLadder = true;

        StartCoroutine(ForceStartStateNextFrame());
    }

    public void OnLadderExitToTop() => DetachToTop();
    public void OnLadderExitToBottom() => JumpOffDownOrMid(applyUp: false);

    private IEnumerator ForceStartStateNextFrame()
    {
        yield return null;
        if (!onLadder || animator == null) yield break;

        if (inputY > deadZone) animator.Play(upPlayHash, 0, 0f);
        else animator.Play(hangIdleHash, 0, 0f);
    }

    // ===== Helpers topo/fundo/ground =====
    private bool IsAtTop() => Mathf.Abs(curDot - maxDot) < edgeDetachThreshold;
    private bool IsAtBottom() => Mathf.Abs(curDot - minDot) < edgeDetachThreshold;

    private bool HasTopGroundAhead()
    {
        Vector3 probe = tTop.position + normal * (wallOffset + extraForwardAtTop) + Vector3.up * 0.6f;
        float r = controller ? Mathf.Max(0.05f, controller.radius * 0.9f) : 0.3f;
        return Physics.SphereCast(probe, r, Vector3.down, out _, 2.5f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private bool SnapFeetToGround(ref Vector3 pos, float maxDistance = 2.5f)
    {
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

    // ===== Saídas =====
    private void DetachToTop()
    {
        if (!onLadder || controller == null || !controller.enabled) return;

        Vector3 exitPos = tTop.position + normal * (wallOffset + extraForwardAtTop);
        SnapFeetToGround(ref exitPos);

        ImmediateDetachCommon(exitPos);
        // no topo não toca queda (há piso)
    }

    private void JumpOffDownOrMid(bool applyUp)
    {
        if (!onLadder || controller == null || !controller.enabled) return;

        Vector3 exitPos = basePos + upAxis * curDot + normal * (wallOffset + 0.06f);
        ImmediateDetachCommon(exitPos);

        controller.Move(normal * jumpOutForward);
        if (applyUp) controller.Move(Vector3.up * jumpOutUp);

        PlayFall();
    }

    private void ImmediateDetachCommon(Vector3 exitPos)
    {
        Vector3 delta = exitPos - transform.position;
        if (delta.sqrMagnitude > 0.000001f) controller.Move(delta);

        controller.Move(Vector3.down * 0.05f); // descola

        controller.stepOffset = originalStepOffset;

        if (moveController) moveController.SetSuspendedByLadder(false);

        if (animator)
        {
            animator.SetBool(boolOnLadder, false);
            animator.SetFloat(floatLadderY, 0f);
            animator.ResetTrigger(trigUpStart);
            animator.ResetTrigger(trigUpEnd);
            animator.ResetTrigger(trigDownEnd);
        }

        onLadder = false;
        curLadder = null;
    }

    private void PlayFall()
    {
        if (!animator) return;
        animator.ResetTrigger(trigFall);
        animator.SetTrigger(trigFall);
        if (!string.IsNullOrEmpty(fallStatePath))
            animator.CrossFadeInFixedTime(fallHash, 0.05f, 0);
    }

    // === ESTE É O MÉTODO QUE FALTAVA ===
    private bool TryDetachAtCurrentHeight()
    {
        if (!onLadder || controller == null || !controller.enabled) return false;

        Vector3 exitPos = basePos + upAxis * curDot + normal * (wallOffset + 0.06f);
        ImmediateDetachCommon(exitPos);
        return true;
    }

    // ===== Debug =====
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
