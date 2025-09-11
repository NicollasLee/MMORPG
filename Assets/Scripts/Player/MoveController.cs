using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public class MoveController : MonoBehaviour
{
    private enum RollDir { Front = 0, Back = 1, Left = 2, Right = 3 }

    private float speed01;        // 0..1 (parado→corrida)
    private float speed01Vel;     // ref p/ SmoothDamp
    private float strafe01;       // 0..1 (parado→strafe rápido)
    private float strafe01Vel;

    [Header("Movimento Básico")]
    public float velocidade = 5f;
    public float velocidadeCorrida = 8f; // corrida
    public float velocidadeRotacao = 12f;

    [Header("Pulo")]
    public float alturaPulo = 1.2f;

    // Gravidade “boa de jogar”
    public float gravidadeSubindo = -22f;   // quando vY > 0
    public float gravidadeCaindo = -36f;    // quando vY <= 0
    public float velocidadeTerminal = -55f; // limite de queda

    [Header("Proteções de Pulo")]
    public float cooldownPulo = 0.06f;
    public float cooldownAoPousar = 0.05f;
    public float coyoteTime = 0.12f;        // perdão ao sair da borda
    public float jumpBuffer = 0.12f;        // guarda o clique antes de tocar o chão

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;       // selecione camadas do chão
    public float groundProbeRadius = 0.22f; // raio da esfera
    public float groundProbeOffset = 0.05f; // quanto acima da sola checar

    [Header("Rolagem - custo & proteção")]
    public float cooldownRoll = 0.15f;
    private float bloqueioRollAte = 0f;
    public float custoRolagem = 20f;        // custo de stamina para rolar

    [Header("Stamina / Corrida")]
    public float staminaMax = 100f;
    public float consumoStaminaPorSegundo = 25f;
    public float regenPorSegundo = 15f;
    public float delayRegen = 0.75f;
    private float staminaAtual;
    private float podeRegenerarApos; // relógio
    private bool querCorrer;         // input
    private bool correndo;           // estado

    [Header("Referências (opcional)")]
    public Transform referenciaCamera;

    // === Integração com Escalada (Ladder) ===
    [Header("Integração com Escalada")]
    [Tooltip("Quando true, o MoveController pausa totalmente (usado pela escada).")]
    public bool suspendedByLadder = false;

    private CharacterController controlador;
    private Animator animador;

    private Vector2 entrada2D;
    private bool requisitouPulo;
    private bool requisitouRolagem;

    private float velocidadeY;
    private bool noChao;
    private bool noChaoAnterior;

    // buffers/estados de pulo
    private float bloqueioPuloAte = 0f;
    private float coyoteAte = 0f;
    private float jumpBufferAte = 0f;
    private bool pulouNesteFrame = false;

    public NoiseMeterDriver noise;
    [SerializeField] private SwordEquipController sword;

    // Exposto para UI
    public float Stamina01 => Mathf.Approximately(staminaMax, 0f) ? 1f : Mathf.Clamp01(staminaAtual / staminaMax);
    public bool Correndo => correndo;

    // Acesso útil para outros scripts (ex.: LadderClimber)
    public CharacterController Controller => controlador;

    void Awake()
    {
        controlador = GetComponent<CharacterController>();
        animador = GetComponent<Animator>();
        noise = noise ? noise : GetComponent<NoiseMeterDriver>();

        if (referenciaCamera == null && Camera.main != null)
            referenciaCamera = Camera.main.transform;

        // importante para snaps suaves
        controlador.minMoveDistance = 0f;
        staminaAtual = staminaMax; // começa cheia
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ========== MODO SUSPENSO (escada) ==========
        // Não desabilitamos o CharacterController; apenas paramos toda a locomoção aqui.
        if (suspendedByLadder)
        {
            // segurança: manter CC ativo e sem step para não "pular" degraus durante o climb
            if (controlador != null && controlador.enabled)
                controlador.stepOffset = 0f;

            // reset de sinais de animação de chão/movimento do locomotor
            animador.SetBool("NoChao", false);
            animador.SetBool("Move", false);
            animador.SetBool("MoveBack", false);
            animador.SetBool("Run", false);
            animador.SetBool("StrafeLeft", false);
            animador.SetBool("StrafeRight", false);
            animador.SetFloat("Speed01", 0f);
            animador.SetFloat("Strafe01", 0f);

            // sem gravidade e sem deslocamento enquanto o LadderClimber controla
            return;
        }

        // ---------- Entrada normalizada ----------
        Vector3 entradaPlano = new Vector3(entrada2D.x, 0f, entrada2D.y);
        if (entradaPlano.sqrMagnitude > 1f) entradaPlano.Normalize();

        // ---------- Direção relativa à câmera ----------
        Vector3 frente = Vector3.forward, direita = Vector3.right;
        if (referenciaCamera != null)
        {
            frente = referenciaCamera.forward; frente.y = 0f; frente.Normalize();
            direita = referenciaCamera.right; direita.y = 0f; direita.Normalize();
        }
        Vector3 direcaoMundo = frente * entradaPlano.z + direita * entradaPlano.x;

        // ---------- corrida / velocidade efetiva ----------
        bool temMovimento = entradaPlano.sqrMagnitude > 0.0001f;
        bool podeCorrer = temMovimento && staminaAtual > 0.01f && noChao;
        correndo = querCorrer && podeCorrer;

        float velAlvo = correndo ? velocidadeCorrida : velocidade;
        Vector3 deslocPlano = direcaoMundo * velAlvo;

        float alvoSpeed01 = Mathf.Clamp01(direcaoMundo.magnitude) * (correndo ? 1f : 0.6f);
        float alvoStrafe01 = Mathf.Clamp01(Mathf.Abs(entradaPlano.x)); // só lateral

        speed01 = Mathf.SmoothDamp(speed01, alvoSpeed01, ref speed01Vel, 0.08f);
        strafe01 = Mathf.SmoothDamp(strafe01, alvoStrafe01, ref strafe01Vel, 0.08f);

        animador.SetFloat("Speed01", speed01);
        animador.SetFloat("Strafe01", strafe01);

        // ---------- Sinais p/ animação ----------
        float dotFrente = 0f;
        if (direcaoMundo.sqrMagnitude > 0.0001f)
            dotFrente = Vector3.Dot(direcaoMundo.normalized, transform.forward);

        bool movendoFrente = direcaoMundo.sqrMagnitude > 0.0001f && dotFrente > 0.25f;
        bool movendoTras = direcaoMundo.sqrMagnitude > 0.0001f && dotFrente < -0.25f;

        bool strafeLeft = Mathf.Abs(entradaPlano.z) <= 0.25f && entradaPlano.x < -0.10f;
        bool strafeRight = Mathf.Abs(entradaPlano.z) <= 0.25f && entradaPlano.x > 0.10f;

        // ---------- Rotação ----------
        bool inputFrente = entradaPlano.z > 0.10f;
        bool diagonalParaTras = entradaPlano.z < -0.10f && Mathf.Abs(entradaPlano.x) > 0.10f;

        if (inputFrente)
        {
            Quaternion alvo = Quaternion.LookRotation(direcaoMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, velocidadeRotacao * Time.deltaTime);
        }
        else if (diagonalParaTras)
        {
            Quaternion alvo = Quaternion.LookRotation(-direcaoMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, velocidadeRotacao * Time.deltaTime);
        }

        // ===================== GROUND / PULO =====================
        noChaoAnterior = noChao;
        bool probe = ProbeGround();
        bool ccGrounded = controlador.isGrounded;
        noChao = probe || ccGrounded;

        controlador.stepOffset = noChao ? 0.35f : 0f;

        if (noChao)
        {
            if (velocidadeY < 0f) velocidadeY = -2f;
            coyoteAte = Time.time + coyoteTime;
        }

        if (requisitouPulo)
        {
            jumpBufferAte = Time.time + jumpBuffer;
            requisitouPulo = false;
        }

        pulouNesteFrame = false;
        bool podeAcionarPulo = Time.time >= bloqueioPuloAte;
        bool dentroDoBuffer = Time.time <= jumpBufferAte;
        bool dentroDoCoyote = Time.time <= coyoteAte;

        if (dentroDoBuffer && dentroDoCoyote && !animador.IsInTransition(0) && podeAcionarPulo)
        {
            velocidadeY = Mathf.Sqrt(2f * -gravidadeSubindo * Mathf.Max(0.01f, alturaPulo));
            animador.ResetTrigger("Jump");
            animador.SetTrigger("Jump");
            bloqueioPuloAte = Time.time + cooldownPulo;
            jumpBufferAte = 0f;
            pulouNesteFrame = true;
            noChao = false;

            if (noise) noise.Jump();
        }

        float g = (velocidadeY > 0f) ? gravidadeSubindo : gravidadeCaindo;
        velocidadeY += g * Time.deltaTime;
        if (velocidadeY < velocidadeTerminal) velocidadeY = velocidadeTerminal;

        // =================== FIM: GROUND / PULO ===================

        // ---------- Move ----------
        if (controlador != null && controlador.enabled)
        {
            Vector3 deslocTotal = deslocPlano + Vector3.up * velocidadeY;
            CollisionFlags flags = controlador.Move(deslocTotal * Time.deltaTime);

            if ((flags & CollisionFlags.Below) != 0) noChao = true;
        }

        // Aterrissagem
        if (!noChaoAnterior && noChao && !pulouNesteFrame)
        {
            if (noise) noise.Land();
            bloqueioPuloAte = Time.time + Mathf.Max(cooldownPulo, cooldownAoPousar);
        }

        animador.SetBool("NoChao", noChao);

        // ---------- Stamina ----------
        if (correndo)
        {
            staminaAtual -= consumoStaminaPorSegundo * Time.deltaTime;
            if (staminaAtual <= 0f) { staminaAtual = 0f; correndo = false; }
            podeRegenerarApos = Time.time + delayRegen;
        }
        else
        {
            if (Time.time >= podeRegenerarApos)
                staminaAtual = Mathf.Min(staminaMax, staminaAtual + regenPorSegundo * Time.deltaTime);
        }

        // ---------- Animações ----------
        animador.SetBool("Move", movendoFrente);
        animador.SetBool("MoveBack", movendoTras);
        animador.SetBool("Run", correndo);
        animador.SetBool("StrafeLeft", strafeLeft);
        animador.SetBool("StrafeRight", strafeRight);

        // ---------- Rolagem ----------
        bool emRolagem = EstaEmRolagem();
        bool podeRolar =
            Time.time >= bloqueioRollAte &&
            !emRolagem &&
            !animador.IsInTransition(0) &&
            noChao &&
            staminaAtual >= custoRolagem;

        if (requisitouRolagem && podeRolar)
        {
            staminaAtual -= custoRolagem;
            if (staminaAtual < 0f) staminaAtual = 0f;

            RollDir dir = ClassificarDirecaoDeRoll(direcaoMundo);
            animador.SetInteger("RollDir", (int)dir);

            animador.ResetTrigger("Roll");
            animador.SetTrigger("Roll");

            if (noise) noise.Roll();

            bloqueioRollAte = Time.time + cooldownRoll;
            podeRegenerarApos = Time.time + delayRegen;
        }
        requisitouRolagem = false;

        // ===== Exemplo futuro: multiplicador por superfície =====
        // if (noise) noise.SetExternalMultiplier(surfaceMul);
    }

    // ===== Input System =====
    public void OnMover(InputAction.CallbackContext ctx)
    {
        if (suspendedByLadder)
        {
            entrada2D = Vector2.zero;
            return;
        }

        if (ctx.performed || ctx.started) entrada2D = ctx.ReadValue<Vector2>();
        else if (ctx.canceled) entrada2D = Vector2.zero;
    }

    public void OnPular(InputAction.CallbackContext ctx)
    {
        if (suspendedByLadder) return;
        if (ctx.performed) requisitouPulo = true;
    }

    public void OnRolar(InputAction.CallbackContext ctx)
    {
        if (suspendedByLadder) return;
        if (ctx.performed) requisitouRolagem = true;
    }

    public void OnCorrer(InputAction.CallbackContext ctx)
    {
        if (suspendedByLadder) { querCorrer = false; return; }

        if (ctx.performed || ctx.started) querCorrer = true;
        else if (ctx.canceled) querCorrer = false;
    }

    private bool EstaEmRolagem()
    {
        var s0 = animador.GetCurrentAnimatorStateInfo(0);
        var s1 = animador.GetNextAnimatorStateInfo(0);
        if (s0.IsTag("Roll") || s1.IsTag("Roll")) return true;

        bool nomeAtual =
            s0.IsName("RollFront") || s0.IsName("RollBack") ||
            s0.IsName("RollLeft") || s0.IsName("RollRight") ||
            s0.IsName("Roll.RollFront") || s0.IsName("Roll.RollBack") ||
            s0.IsName("Roll.RollLeft") || s0.IsName("Roll.RollRight");

        bool nomeProx =
            s1.IsName("RollFront") || s1.IsName("RollBack") ||
            s1.IsName("RollLeft") || s1.IsName("RollRight") ||
            s1.IsName("Roll.RollFront") || s1.IsName("Roll.RollBack") ||
            s1.IsName("Roll.RollLeft") || s1.IsName("Roll.RollRight");

        return nomeAtual || nomeProx;
    }

    private RollDir ClassificarDirecaoDeRoll(Vector3 direcaoMundo)
    {
        if (direcaoMundo.sqrMagnitude < 0.0001f) return RollDir.Front;
        Vector3 local = transform.InverseTransformDirection(direcaoMundo).normalized;

        if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            return (local.x < 0f) ? RollDir.Left : RollDir.Right;
        else
            return (local.z < 0f) ? RollDir.Back : RollDir.Front;
    }

    private bool ProbeGround()
    {
        Bounds b = controlador.bounds;
        Vector3 foot = b.center + Vector3.down * (b.extents.y - groundProbeOffset);
        float r = Mathf.Max(0.01f, groundProbeRadius * Mathf.Max(0.5f, transform.lossyScale.y));
        return Physics.CheckSphere(foot, r, groundMask, QueryTriggerInteraction.Ignore);
    }

    public void OnEquip(InputAction.CallbackContext ctx)
    {
        if (suspendedByLadder) return;

        if (ctx.performed && sword != null)
        {
            sword.Toggle();
        }
    }

    // === API para Ladder ===
    public void SetSuspendedByLadder(bool on)
    {
        suspendedByLadder = on;

        if (on)
        {
            // zera inputs e velocidade vertical
            entrada2D = Vector2.zero;
            querCorrer = false;
            correndo = false;
            velocidadeY = 0f;

            // limpa sinais visuais do locomotor
            animador.SetFloat("Speed01", 0f);
            animador.SetFloat("Strafe01", 0f);
            animador.SetBool("Move", false);
            animador.SetBool("MoveBack", false);
            animador.SetBool("Run", false);
            animador.SetBool("StrafeLeft", false);
            animador.SetBool("StrafeRight", false);
        }
        else
        {
            // Ao sair da escada, voltamos a considerar o chão imediatamente
            coyoteAte = Time.time + coyoteTime;
        }
    }
}
