using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Traversal/Ladder Climber")]
[DisallowMultipleComponent]
public class LadderClimber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    [SerializeField] private MoveController moveController;

    [Header("Animator Params")]
    [SerializeField] private string boolOnLadder = "OnLadder";
    [SerializeField] private string floatWallX = "WallX";      // A/D
    [SerializeField] private string floatWallY = "WallY";      // W/S
    [SerializeField] private string trigUpStart = "UpStart";
    [SerializeField] private string trigUpEnd = "UpEnd";
    [SerializeField] private string trigDownEnd = "DownEnd";
    [SerializeField] private string trigJumpOut = "JumpOut";
    [SerializeField] private string trigWallDash = "WallDash";
    [SerializeField] private string trigFall = "Fall";

    [Header("Animator State Paths")]
    [Tooltip("Use Copy Path no estado LadderBT e cole aqui.")]
    [SerializeField] private string btStatePath = "Base Layer.Ladder.LadderBT";
    [SerializeField] private string fallStatePath = "Base Layer.Fall";

    [Header("Detect/Attach")]
    [SerializeField] private LayerMask ladderMask;
    [SerializeField] private float probeRadius = 0.35f;
    [SerializeField] private float attachDistance = 0.6f;
    [SerializeField] private float wallOffset = 0.25f;
    [SerializeField] private float alignSpeed = 25f;

    [Header("Climb")]
    [SerializeField] private float climbSpeed = 1.8f;
    [SerializeField] private float inputDeadZone = 0.10f;

    [Header("Shimmy (lateral)")]
    [SerializeField] private float shimmySpeed = 1.8f;
    [SerializeField] private float sideSoftClamp = 1.2f;
    [SerializeField] private bool invertHorizontal = false; // << se A/D ainda ficar invertido, marque isto

    [Header("Dash")]
    [SerializeField] private float dashUpVelocity = 3.0f;
    [SerializeField] private float dashBackImpulse = 0.25f;
    [SerializeField] private float dashCooldown = 0.35f;

    [Header("Air-Grab (regrudar)")]
    [SerializeField] private float airGrabWindow = 0.35f;
    [SerializeField] private float regrabProbeDist = 1.0f;
    [SerializeField] private float regrabProbeRadius = 0.30f;

    [Header("Exit / Ground Snap")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float edgeDetachThreshold = 0.15f;
    [SerializeField] private float microNudge = 0.02f;

    // ---- estado interno
    private Transform curLadder, tBottom, tTop;
    private Vector3 basePos, upAxis, normal, rightAxis;
    private float minDot, maxDot, curDot;
    private float curSide;
    private bool onLadder;
    private float inputY, inputX;
    private float originalStepOffset;
    private float airGrabUntil;
    private float nextDashAt;

    private int btHash, fallHash;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!controller) controller = GetComponent<CharacterController>();
        if (!moveController) moveController = GetComponent<MoveController>();

        btHash = Animator.StringToHash(btStatePath);
        fallHash = Animator.StringToHash(fallStatePath);
    }

    private void OnDisable()
    {
        SetSuspended(false);
        if (animator) animator.SetBool(boolOnLadder, false);
        onLadder = false;
    }

    // ===== INPUTS =====
    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 v = ctx.ReadValue<Vector2>();
        inputY = Mathf.Clamp(v.y, -1f, 1f);
        inputX = Mathf.Clamp(v.x, -1f, 1f);
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (onLadder) { TryDetachAtCurrentHeight(); return; }
        TryAttachInFront();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !onLadder) return;
        if (IsAtTop()) { DetachToTop(); return; }

        JumpOffDownOrMid(applyUp: true);
        airGrabUntil = Time.time + airGrabWindow;
    }

    public void RequestDashUp()
    {
        if (!onLadder || Time.time < nextDashAt) return;

        if (animator) { animator.ResetTrigger(trigWallDash); animator.SetTrigger(trigWallDash); }
        if (moveController)
        {
            moveController.AddVerticalVelocity(dashUpVelocity);
            moveController.ApplyPlanarImpulse(-normal * dashBackImpulse);
        }
        nextDashAt = Time.time + dashCooldown;
    }

    private void Update()
    {
        // janela de regrudar
        if (!onLadder && Time.time <= airGrabUntil)
        {
            if (TryAttachInFront()) airGrabUntil = 0f;
        }

        if (!onLadder || controller == null || !controller.enabled) return;

        float y = Mathf.Abs(inputY) < inputDeadZone ? 0f : inputY;
        float x = Mathf.Abs(inputX) < inputDeadZone ? 0f : inputX;
        if (invertHorizontal) x = -x; // <<< corrige inversão facilmente

        // alimenta BT
        if (animator)
        {
            animator.SetFloat(floatWallY, y);
            animator.SetFloat(floatWallX, x);
        }

        // rotaciona para a superfície
        Quaternion targetRot = Quaternion.LookRotation(-normal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, alignSpeed * Time.deltaTime);

        // mover vertical
        curDot += y * climbSpeed * Time.deltaTime;
        curDot = Mathf.Clamp(curDot, minDot, maxDot);

        // mover lateral
        curSide += x * shimmySpeed * Time.deltaTime;
        if (sideSoftClamp > 0f) curSide = Mathf.Clamp(curSide, -sideSoftClamp, sideSoftClamp);

        // triggers topo/base
        if (animator)
        {
            if (IsAtTop() && y > 0.05f) animator.SetTrigger(trigUpEnd);
            if (IsAtBottom() && y < -0.05f) animator.SetTrigger(trigDownEnd);
        }

        // snap final
        Vector3 lateralAxis = rightAxis * (invertHorizontal ? -1f : 1f);
        Vector3 target = basePos + upAxis * curDot + lateralAxis * curSide + normal * wallOffset;
        Vector3 delta = target - transform.position;
        if (delta.sqrMagnitude > 0.0000001f) controller.Move(delta);
    }

    // ===== ATTACH / DETACH =====
    public bool TryAttachInFront()
    {
        if (!controller) return false;

        Vector3 origin = controller.bounds.center + transform.forward.normalized * attachDistance;
        var hits = Physics.OverlapSphere(origin, probeRadius, ladderMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

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
        if (!bottom || !top) return false;

        Attach(ladderRoot, bottom, top);
        return true;
    }

    private void Attach(Transform ladder, Transform bottom, Transform top)
    {
        curLadder = ladder; tBottom = bottom; tTop = top;

        upAxis = (tTop.position - tBottom.position).normalized;
        normal = -ladder.forward;

        // >>> correção de eixo lateral: use Cross(normal, up) (ordem invertida)
        rightAxis = Vector3.Cross(normal, upAxis).normalized;

        basePos = tBottom.position;
        minDot = 0f;
        maxDot = Vector3.Dot(tTop.position - basePos, upAxis);

        curDot = Mathf.Clamp(Vector3.Dot(transform.position - basePos, upAxis), minDot, maxDot);
        curSide = 0f;

        Vector3 snap = basePos + upAxis * curDot + normal * wallOffset;

        originalStepOffset = controller.stepOffset;
        controller.stepOffset = Mathf.Max(0.0001f, originalStepOffset);

        Vector3 deltaSnap = snap - transform.position;
        if (deltaSnap.sqrMagnitude > 0.0000001f) controller.Move(deltaSnap);

        SetSuspended(true);

        if (animator)
        {
            animator.SetBool(boolOnLadder, true);
            animator.SetFloat(floatWallX, 0f);
            animator.SetFloat(floatWallY, 0f);
            animator.ResetTrigger(trigUpStart);
            animator.SetTrigger(trigUpStart);
        }

        onLadder = true;
        StartCoroutine(ForceBTNextFrame());
    }

    private IEnumerator ForceBTNextFrame()
    {
        yield return null;
        if (!onLadder || animator == null) yield break;
        if (btHash != 0) animator.Play(btHash, 0, 0f);
    }

    public void OnLadderExitToTop() => DetachToTop();
    public void OnLadderExitToBottom() => JumpOffDownOrMid(applyUp: false);

    private void DetachToTop()
    {
        if (!onLadder || controller == null || !controller.enabled) return;
        Vector3 exitPos = transform.position + normal * microNudge;
        ImmediateDetachCommon(exitPos);
    }

    private void JumpOffDownOrMid(bool applyUp)
    {
        if (!onLadder || controller == null || !controller.enabled) return;

        Vector3 exitPos = transform.position;
        ImmediateDetachCommon(exitPos);

        controller.Move(normal * 0.35f);
        if (applyUp) controller.Move(Vector3.up * 0.30f);

        if (animator) { animator.ResetTrigger(trigJumpOut); animator.SetTrigger(trigJumpOut); }
        PlayFall();
    }

    private void ImmediateDetachCommon(Vector3 exitPos)
    {
        Vector3 delta = exitPos - transform.position;
        if (delta.sqrMagnitude > 0.0000001f) controller.Move(delta);

        controller.Move(Vector3.down * 0.05f);
        controller.stepOffset = Mathf.Max(0.0001f, originalStepOffset);

        SetSuspended(false);

        if (animator)
        {
            animator.SetBool(boolOnLadder, false);
            animator.SetFloat(floatWallX, 0f);
            animator.SetFloat(floatWallY, 0f);
            animator.ResetTrigger(trigUpStart);
            animator.ResetTrigger(trigUpEnd);
            animator.ResetTrigger(trigDownEnd);
        }

        onLadder = false;
        curLadder = null;
    }

    // ===== Helpers =====
    private bool IsAtTop() => Mathf.Abs(curDot - maxDot) < edgeDetachThreshold;
    private bool IsAtBottom() => Mathf.Abs(curDot - minDot) < edgeDetachThreshold;

    private bool TryDetachAtCurrentHeight()
    {
        if (!onLadder || controller == null || !controller.enabled) return false;
        Vector3 exitPos = transform.position + normal * microNudge;
        ImmediateDetachCommon(exitPos);
        return true;
    }

    private void PlayFall()
    {
        if (!animator) return;
        animator.ResetTrigger(trigFall);
        animator.SetTrigger(trigFall);
        if (!string.IsNullOrEmpty(fallStatePath))
            animator.CrossFadeInFixedTime(fallHash, 0.05f, 0);
    }

    private void SetSuspended(bool enable)
    {
        if (moveController) moveController.SetSuspendedByLadder(enable);
    }
}
