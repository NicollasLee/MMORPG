using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
public class MoveController : MonoBehaviour
{
    [Header("Movimento Básico")]
    public float velocidade = 5f;
    public float velocidadeCorrida = 8f; // << corrida
    public float velocidadeRotacao = 12f;
    public float alturaPulo = 1.2f;
    public float gravidade = -20f;

    [Header("Pulo - proteção")]
    public float cooldownPulo = 0.06f;
    public float cooldownAoPousar = 0.05f;
    private float bloqueioPuloAte = 0f;

    [Header("Rolagem - custo & proteção")]
    public float cooldownRoll = 0.15f;
    private float bloqueioRollAte = 0f;
    public float custoRolagem = 20f;            // [NOVO] custo de stamina para rolar

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

    private CharacterController controlador;
    private Animator animador;

    private Vector2 entrada2D;
    private bool requisitouPulo;
    private bool requisitouRolagem;

    private float velocidadeY;
    private bool noChao;

    // Exposto para UI
    public float Stamina01 => Mathf.Approximately(staminaMax, 0f) ? 1f : Mathf.Clamp01(staminaAtual / staminaMax);
    public bool Correndo => correndo;

    void Awake()
    {
        controlador = GetComponent<CharacterController>();
        animador = GetComponent<Animator>();

        if (referenciaCamera == null && Camera.main != null)
            referenciaCamera = Camera.main.transform;

        controlador.minMoveDistance = 0f;
        staminaAtual = staminaMax; // começa cheia
    }

    void Update()
    {
        float dt = Time.deltaTime;

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

        // ---------- Sinais p/ animação ----------
        float dotFrente = 0f;
        if (direcaoMundo.sqrMagnitude > 0.0001f)
            dotFrente = Vector3.Dot(direcaoMundo.normalized, transform.forward);

        bool movendoFrente = direcaoMundo.sqrMagnitude > 0.0001f && dotFrente > 0.25f;
        bool movendoTras = direcaoMundo.sqrMagnitude > 0.0001f && dotFrente < -0.25f;

        // ---------- Rotação ----------
        bool inputFrente = entradaPlano.z > 0.10f;
        bool diagonalParaTras = entradaPlano.z < -0.10f && Mathf.Abs(entradaPlano.x) > 0.10f;

        if (inputFrente)
        {
            Quaternion alvo = Quaternion.LookRotation(direcaoMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, velocidadeRotacao * dt);
        }
        else if (diagonalParaTras)
        {
            Quaternion alvo = Quaternion.LookRotation(-direcaoMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, velocidadeRotacao * dt);
        }

        // ---------- Pulo / Gravidade ----------
        if (noChao && velocidadeY < 0f) velocidadeY = -2f;

        bool podeAcionarPulo = Time.time >= bloqueioPuloAte;
        if (requisitouPulo && noChao && !animador.IsInTransition(0) && podeAcionarPulo)
        {
            velocidadeY = Mathf.Sqrt(alturaPulo * -2f * gravidade);
            animador.ResetTrigger("Jump");
            animador.SetTrigger("Jump");
            bloqueioPuloAte = Time.time + cooldownPulo;
        }
        requisitouPulo = false;

        velocidadeY += gravidade * dt;

        // ---------- Move único ----------
        Vector3 deslocTotal = deslocPlano + Vector3.up * velocidadeY;
        CollisionFlags flags = controlador.Move(deslocTotal * dt);

        bool noChaoAnterior = noChao;
        noChao = (flags & CollisionFlags.Below) != 0;

        if (!noChaoAnterior && noChao)
            bloqueioPuloAte = Time.time + Mathf.Max(cooldownPulo, cooldownAoPousar);

        animador.SetBool("NoChao", noChao);

        // ---------- Stamina (dreno e regen) ----------
        if (correndo)
        {
            staminaAtual -= consumoStaminaPorSegundo * dt;
            if (staminaAtual <= 0f) { staminaAtual = 0f; correndo = false; } // exausto
            podeRegenerarApos = Time.time + delayRegen;
        }
        else
        {
            if (Time.time >= podeRegenerarApos)
                staminaAtual = Mathf.Min(staminaMax, staminaAtual + regenPorSegundo * dt);
        }

        // ---------- Animações ----------
        animador.SetBool("Move", movendoFrente);
        animador.SetBool("MoveBack", movendoTras);
        animador.SetBool("Run", correndo); // use se tiver estado de corrida

        // ---------- Rolagem ----------
        bool emRolagem = EstaEmRolagem();
        bool podeRolar =
            Time.time >= bloqueioRollAte &&
            !emRolagem &&
            !animador.IsInTransition(0) &&
            noChao &&
            staminaAtual >= custoRolagem;                // [NOVO] precisa ter stamina

        if (requisitouRolagem && podeRolar)
        {
            // [NOVO] desconta stamina uma única vez ao iniciar o roll
            staminaAtual -= custoRolagem;
            if (staminaAtual < 0f) staminaAtual = 0f;

            animador.ResetTrigger("Roll");
            animador.SetTrigger("Roll");

            bloqueioRollAte = Time.time + cooldownRoll;
            podeRegenerarApos = Time.time + delayRegen; // [NOVO] empurra regen após rolar
        }
        requisitouRolagem = false;
    }

    // ===== Input System (PlayerInput / Invoke Unity Events) =====
    public void OnMover(InputAction.CallbackContext ctx)
    {
        if (ctx.performed || ctx.started)
            entrada2D = ctx.ReadValue<Vector2>();
        else if (ctx.canceled)
            entrada2D = Vector2.zero;
    }

    public void OnPular(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) requisitouPulo = true;
    }

    public void OnRolar(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) requisitouRolagem = true;
    }

    public void OnCorrer(InputAction.CallbackContext ctx)
    {
        if (ctx.performed || ctx.started) querCorrer = true;
        else if (ctx.canceled) querCorrer = false;
    }

    // Detecta se está em um estado de roll
    private bool EstaEmRolagem()
    {
        var s0 = animador.GetCurrentAnimatorStateInfo(0);
        var s1 = animador.GetNextAnimatorStateInfo(0);
        if (s0.IsTag("Roll") || s1.IsTag("Roll")) return true;

        bool nomeAtual =
            s0.IsName("RollFront") || s0.IsName("RollBack") ||
            s0.IsName("Roll.RollFront") || s0.IsName("Roll.RollBack");
        bool nomeProx =
            s1.IsName("RollFront") || s1.IsName("RollBack") ||
            s1.IsName("Roll.RollFront") || s1.IsName("Roll.RollBack");
        return nomeAtual || nomeProx;
    }
}
