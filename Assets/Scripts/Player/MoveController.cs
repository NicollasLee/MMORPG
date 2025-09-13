using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public class MoveController : MonoBehaviour
{
    private enum RollDir { Front = 0, Back = 1, Left = 2, Right = 3 }

    // ===== Config =====
    [Header("Locomotion (BlendTree MoveX/MoveY)")]
    [SerializeField] private float locomotionDamp = 0.08f; // damping do BT
    [SerializeField] private float runBoost = 1.4f;        // escala Y quando correndo

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravityUp = -22f;
    [SerializeField] private float gravityDown = -36f;
    [SerializeField] private float terminalVelocity = -55f;

    [Header("Jump Safeguards")]
    [SerializeField] private float jumpCooldown = 0.06f;
    [SerializeField] private float landedCooldown = 0.05f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeRadius = 0.22f;
    [SerializeField] private float groundProbeOffset = 0.05f;

    [Header("Roll")]
    [SerializeField] private float rollCooldown = 0.15f;
    [SerializeField] private float rollStaminaCost = 20f;

    [Header("Stamina / Run")]
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaDrainPerSec = 25f;
    [SerializeField] private float staminaRegenPerSec = 15f;
    [SerializeField] private float staminaRegenDelay = 0.75f;

    [Header("Optional Refs")]
    [SerializeField] private Transform cameraRef;
    [SerializeField] private string fallTrigger = "Fall";
    [SerializeField] private SwordEquipController sword;

    // ===== Runtime =====
    private CharacterController cc;
    private Animator animator;
    private NoiseMeterDriver noise;

    private Vector2 moveInput;
    private bool requestJump;
    private bool requestRoll;
    private bool wantRun;
    private bool running;

    private float stamina;
    private float regenAllowedAt;

    private float yVelocity;
    private bool grounded;
    private bool groundedLastFrame;
    private bool fallTriggered;
    private bool suspendedByLadder;

    private float blockRollUntil;

    // buffers pulo
    private float blockJumpUntil;
    private float coyoteUntil;
    private float bufferJumpUntil;
    private bool jumpedThisFrame;

    // impulso externo (1 frame) p/ dash/pulo na parede
    private Vector3 externalPlanarImpulse;

    // UI exposure
    public float Stamina01 => Mathf.Approximately(staminaMax, 0f) ? 1f : Mathf.Clamp01(stamina / staminaMax);
    public bool Running => running;
    public bool IsGrounded => grounded;

    private void Reset()
    {
        cameraRef = Camera.main ? Camera.main.transform : null;
    }

    /// <summary>Inicializa componentes e estado.</summary>
    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        noise = GetComponent<NoiseMeterDriver>() ?? noise;

        if (cameraRef == null && Camera.main != null) cameraRef = Camera.main.transform;
        cc.minMoveDistance = 0f;
        stamina = staminaMax;
    }

    /// <summary>Loop principal de movimento.</summary>
    private void Update()
    {
        if (cc == null || !cc.enabled) return;
        if (suspendedByLadder) return;

        float dt = Time.deltaTime;

        // 1) Direção no mundo a partir do input + câmera
        Vector3 worldDir = BuildWorldMoveDirection(moveInput);

        // 2) Corrida (só no chão e com stamina)
        UpdateRunState(worldDir);

        // 3) Alimenta BlendTree 2D (MoveX/MoveY)
        UpdateBlendTree(worldDir, dt);

        // 4) Rotação de acordo com a direção
        RotateCharacter(worldDir, dt);

        // 5) Grounding & Pulo
        UpdateGrounding();
        BufferJumpInput();
        TryJumpIfAllowed();

        // 6) Gravidade
        ApplyGravity(dt);

        // 7) Move (inclui impulso planar externo – dash/pulo parede)
        MoveCharacter(worldDir, dt);

        // 8) Aterrissagem / Fall
        HandleLandingEffects();
        UpdateFallTrigger();

        // 9) Stamina e anima flags auxiliares
        UpdateStamina(dt);
        PushAnimatorFlags(worldDir);

        // 10) Roll
        HandleRoll(worldDir);
        requestRoll = false;
    }

    // ====== API externa ======

    /// <summary>Suspende a locomoção (ex.: quando escada domina o movimento).</summary>
    public void SetSuspendedByLadder(bool enabled)
    {
        suspendedByLadder = enabled;
        if (!enabled) return;

        running = false;
        animator.SetBool("Run", false);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
    }

    /// <summary>Aplica um impulso horizontal (plano XZ) consumido no próximo Move().</summary>
    public void ApplyPlanarImpulse(Vector3 impulse)
    {
        impulse.y = 0f;
        externalPlanarImpulse += impulse;
    }

    /// <summary>Adiciona velocidade vertical (usado por JumpOut/DashUp).</summary>
    public void AddVerticalVelocity(float addY)
    {
        yVelocity = Mathf.Max(yVelocity, addY);
    }

    // ====== Input System (encaminhados pelo TraversalController) ======

    /// <summary>Entrada de movimento (Vector2).</summary>
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (ctx.performed || ctx.started) moveInput = ctx.ReadValue<Vector2>();
        else if (ctx.canceled) moveInput = Vector2.zero;
    }

    /// <summary>Entrada de pulo.</summary>
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) requestJump = true;
    }

    /// <summary>Entrada de rolagem.</summary>
    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) requestRoll = true;
    }

    /// <summary>Entrada de corrida (hold).</summary>
    public void OnRun(InputAction.CallbackContext ctx)
    {
        if (ctx.performed || ctx.started) wantRun = true;
        else if (ctx.canceled) wantRun = false;
    }

    /// <summary>Equipar/Guardar arma e entrar na submáquina UpperBody.</summary>
    public void OnEquip(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || animator == null) return;

        bool armed = !animator.GetBool("Armed");
        animator.SetBool("Armed", armed);
        animator.ResetTrigger("Equip");
        animator.SetTrigger("Equip");
    }

    // ====== Núcleo ======

    /// <summary>Constroi direção de movimento no mundo a partir do input e da câmera.</summary>
    private Vector3 BuildWorldMoveDirection(Vector2 input)
    {
        Vector3 v = new Vector3(input.x, 0f, input.y);
        if (v.sqrMagnitude > 1f) v.Normalize();

        if (cameraRef == null) return v;
        Vector3 fwd = cameraRef.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cameraRef.right; right.y = 0f; right.Normalize();
        return fwd * v.z + right * v.x;
    }

    /// <summary>Atualiza o estado de corrida.</summary>
    private void UpdateRunState(Vector3 worldDir)
    {
        bool hasInput = worldDir.sqrMagnitude > 0.0001f;
        bool canRun = hasInput && stamina > 0.01f && grounded;
        running = wantRun && canRun;
    }

    /// <summary>Alimenta o BlendTree 2D MoveX/MoveY (local space + intensidade).</summary>
    private void UpdateBlendTree(Vector3 worldDir, float dt)
    {
        float mag = Mathf.Clamp01(worldDir.magnitude);
        float intensity = running ? mag * runBoost : mag;

        Vector3 local = transform.InverseTransformDirection(worldDir.normalized * (mag > 0f ? 1f : 0f));
        float targetX = local.x * intensity; // strafe
        float targetY = local.z * intensity; // frente/trás (+ corrida)

        animator.SetFloat("MoveX", targetX, locomotionDamp, dt);
        animator.SetFloat("MoveY", targetY, locomotionDamp, dt);
    }

    /// <summary>Rotaciona suavemente para a direção de movimento.</summary>
    private void RotateCharacter(Vector3 worldDir, float dt)
    {
        if (worldDir.sqrMagnitude <= 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(worldDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * dt);
    }

    /// <summary>Atualiza contato com o chão e coyote.</summary>
    private void UpdateGrounding()
    {
        groundedLastFrame = grounded;

        bool probe = ProbeGround();
        bool ccGround = cc.isGrounded;
        grounded = probe || ccGround;

        cc.stepOffset = grounded ? 0.35f : 0f;

        if (grounded)
        {
            if (yVelocity < 0f) yVelocity = -2f;
            coyoteUntil = Time.time + coyoteTime;
        }
    }

    /// <summary>Observa a flag de input de pulo para buffer.</summary>
    private void BufferJumpInput()
    {
        if (!requestJump) return;
        bufferJumpUntil = Time.time + jumpBufferTime;
        requestJump = false;
    }

    /// <summary>Tenta pular quando dentro de janela de coyote + buffer + cooldown.</summary>
    private void TryJumpIfAllowed()
    {
        jumpedThisFrame = false;

        bool canByTime = Time.time >= blockJumpUntil;
        bool inBuffer = Time.time <= bufferJumpUntil;
        bool inCoyote = Time.time <= coyoteUntil;

        if (!inBuffer || !inCoyote || !canByTime || animator.IsInTransition(0)) return;

        yVelocity = Mathf.Sqrt(2f * -gravityUp * Mathf.Max(0.01f, jumpHeight));
        animator.ResetTrigger("Jump");
        animator.SetTrigger("Jump");

        blockJumpUntil = Time.time + jumpCooldown;
        bufferJumpUntil = 0f;
        jumpedThisFrame = true;
        grounded = false;

        if (noise) noise.Jump();
    }

    /// <summary>Aplica gravidade parabólica com terminal velocity.</summary>
    private void ApplyGravity(float dt)
    {
        float g = (yVelocity > 0f) ? gravityUp : gravityDown;
        yVelocity += g * dt;
        if (yVelocity < terminalVelocity) yVelocity = terminalVelocity;
    }
    /// <summary>Move o CharacterController (inclui impulso externo 1-frame).</summary>
    private void MoveCharacter(Vector3 worldDir, float dt)
    {
        if (cc == null || !cc.enabled) return;

        float speed = running ? runSpeed : walkSpeed;
        Vector3 planar = worldDir * speed;

        Vector3 deslocTotal = planar + externalPlanarImpulse + Vector3.up * yVelocity;
        externalPlanarImpulse = Vector3.zero; // consome o impulso

        // segurança: se CC foi desativado no meio do caminho, não chame Move
        if (!cc.enabled) return;

        var flags = cc.Move(deslocTotal * dt);
        if ((flags & CollisionFlags.Below) != 0) grounded = true;

        animator.SetBool("NoChao", grounded);
    }

    /// <summary>Trata aterrissagem (sons/bloqueios).</summary>
    private void HandleLandingEffects()
    {
        if (groundedLastFrame || !grounded || jumpedThisFrame) return;

        if (noise) noise.Land();
        blockJumpUntil = Time.time + Mathf.Max(jumpCooldown, landedCooldown);
    }

    /// <summary>Dispara trigger de queda ao cair.</summary>
    private void UpdateFallTrigger()
    {
        if (!grounded && yVelocity < -0.05f && !jumpedThisFrame)
        {
            if (!fallTriggered)
            {
                animator.ResetTrigger(fallTrigger);
                animator.SetTrigger(fallTrigger);
                fallTriggered = true;
                animator.SetBool("Run", false);
            }
        }
        else
        {
            if (grounded && fallTriggered) fallTriggered = false;
        }
    }

    /// <summary>Consome/Regenera stamina (com delay).</summary>
    private void UpdateStamina(float dt)
    {
        if (running)
        {
            stamina -= staminaDrainPerSec * dt;
            if (stamina <= 0f) { stamina = 0f; running = false; }
            regenAllowedAt = Time.time + staminaRegenDelay;
            return;
        }

        if (Time.time < regenAllowedAt) return;
        stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * dt);
    }

    /// <summary>Flags auxiliares (opcionais para clipes legados).</summary>
    private void PushAnimatorFlags(Vector3 worldDir)
    {
        float dotForward = (worldDir.sqrMagnitude > 0.0001f)
            ? Vector3.Dot(worldDir.normalized, transform.forward)
            : 0f;

        bool movingFwd = worldDir.sqrMagnitude > 0.0001f && dotForward > 0.25f;
        bool movingBack = worldDir.sqrMagnitude > 0.0001f && dotForward < -0.25f;

        animator.SetBool("Move", movingFwd);
        animator.SetBool("MoveBack", movingBack);
        animator.SetBool("Run", running);
    }

    /// <summary>Executa rolagem se apto.</summary>
    private void HandleRoll(Vector3 worldDir)
    {
        if (!requestRoll) return;

        bool canRoll = Time.time >= blockRollUntil &&
                       grounded &&
                       !animator.IsInTransition(0) &&
                       stamina >= rollStaminaCost;

        if (!canRoll) return;

        stamina -= rollStaminaCost;
        if (stamina < 0f) stamina = 0f;

        RollDir dir = ClassifyRollDirection(worldDir);
        animator.SetInteger("RollDir", (int)dir);
        animator.ResetTrigger("Roll");
        animator.SetTrigger("Roll");

        if (noise) noise.Roll();

        blockRollUntil = Time.time + rollCooldown;
        regenAllowedAt = Time.time + 0.5f;
    }

    /// <summary>Classifica direção da rolagem baseada no input local.</summary>
    private RollDir ClassifyRollDirection(Vector3 worldDir)
    {
        if (worldDir.sqrMagnitude < 0.0001f) return RollDir.Front;
        Vector3 local = transform.InverseTransformDirection(worldDir).normalized;

        if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            return (local.x < 0f) ? RollDir.Left : RollDir.Right;

        return (local.z < 0f) ? RollDir.Back : RollDir.Front;
    }

    /// <summary>Probe simples de chão com esfera.</summary>
    private bool ProbeGround()
    {
        Bounds b = cc.bounds;
        Vector3 foot = b.center + Vector3.down * (b.extents.y - groundProbeOffset);
        float r = Mathf.Max(0.01f, groundProbeRadius * Mathf.Max(0.5f, transform.lossyScale.y));
        return Physics.CheckSphere(foot, r, groundMask, QueryTriggerInteraction.Ignore);
    }
}
