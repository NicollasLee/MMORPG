using UnityEngine;
using UnityEngine.InputSystem;

public class CameraOrbit : MonoBehaviour
{
    [Header("Alvo")]
    public Transform alvo;                       // arraste o Player
    public Vector3 offsetAlvo = new Vector3(0f, 1.6f, 0f);

    [Header("Órbita / Ângulos")]
    public float pitchMin = -30f;
    public float pitchMax = 60f;
    public bool inverterY = false;

    [Tooltip("Tempo (s) para suavizar yaw/pitch (SmoothDampAngle). 0.06–0.12 é bom.")]
    public float tempoSuavAng = 0.08f;

    [Header("Distância / Zoom")]
    public float distanciaInicial = 4.5f;
    public float distanciaMin = 2.0f;
    public float distanciaMax = 8.0f;
    public float sensZoom = 0.25f;

    [Tooltip("Velocidade de retorno quando a câmera está liberada de obstáculos (u/s).")]
    public float velRetornoDist = 6f;

    [Header("Colisão da câmera")]
    public bool usarColisao = true;
    public float raioEsfera = 0.28f;
    public float margemParede = 0.12f;
    [Tooltip("Deixe 'Everything' e o script ignora automaticamente a layer do alvo.")]
    public LayerMask mascaraObstaculos = ~0;

    [Header("Mouse")]
    [Tooltip("Graus por pixel horizontal/vertical.")]
    public float sensX = 0.15f, sensY = 0.12f;

    [Tooltip("Se ligado, só gira quando o botão direito do mouse está pressionado.")]
    public bool usarBotaoDireito = true;

    // --- estado interno ---
    private float yaw, pitch;             // ângulos suavizados
    private float yawAlvo, pitchAlvo;     // ângulos alvo (acumulam mouse)
    private float velYaw, velPitch;       // velocidades SmoothDampAngle (interno)
    private float distAlvo, distAtual;    // distância alvo e atual
    private Vector2 deltaOlhar;           // delta acumulado por frame
    private bool arrastandoCamera;
    private int layerDoAlvo = -1;

    void Awake()
    {
        if (alvo != null) layerDoAlvo = alvo.gameObject.layer;

        // ignora a layer do alvo na máscara (evita "bater" no próprio player)
        if (mascaraObstaculos == ~0 && layerDoAlvo >= 0)
            mascaraObstaculos &= ~(1 << layerDoAlvo);

        distAlvo = distAtual = Mathf.Clamp(distanciaInicial, distanciaMin, distanciaMax);
        yaw = yawAlvo = 0f;
        pitch = pitchAlvo = Mathf.Clamp(0f, pitchMin, pitchMax);
    }

    void LateUpdate()
    {
        if (alvo == null) return;

        // --- 1) integrar mouse nos ângulos ALVO (sem suavização) ---
        bool podeGirar = !usarBotaoDireito || arrastandoCamera;
        if (podeGirar)
        {
            float dx = deltaOlhar.x * sensX;
            float dy = (inverterY ? +1f : -1f) * deltaOlhar.y * sensY;
            yawAlvo += dx;
            pitchAlvo = Mathf.Clamp(pitchAlvo + dy, pitchMin, pitchMax);
        }
        deltaOlhar = Vector2.zero; // consome delta

        // --- 2) suavizar ângulos (estável em 360°) ---
        float t = Mathf.Max(0.0001f, tempoSuavAng);
        yaw = Mathf.SmoothDampAngle(yaw, yawAlvo, ref velYaw, t);
        pitch = Mathf.SmoothDampAngle(pitch, pitchAlvo, ref velPitch, t);

        // --- 3) calcular direção a partir dos ângulos suavizados ---
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 foco = alvo.position + offsetAlvo;
        Vector3 dirCam = rot * Vector3.back; // vetor unitário alvo->câmera

        // --- 4) colisão sem tremor (aproxima rápido, volta devagar) ---
        float distDesejada = Mathf.Clamp(distAlvo, distanciaMin, distanciaMax);
        float distPermitida = distDesejada;

        if (usarColisao)
        {
            if (Physics.SphereCast(foco, raioEsfera, dirCam, out RaycastHit hit,
                                   distDesejada, mascaraObstaculos, QueryTriggerInteraction.Ignore))
            {
                // encurta imediatamente até a parede (menos a margem)
                distPermitida = Mathf.Max(hit.distance - margemParede, distanciaMin);
            }
        }

        // aproxima rápido, afasta devagar (remove flicker em bordas)
        if (distAtual > distPermitida)
            distAtual = distPermitida; // snap-in instantâneo
        else
            distAtual = Mathf.MoveTowards(distAtual, distPermitida, velRetornoDist * Time.deltaTime);

        // --- 5) aplicar posição e rotação ---
        transform.position = foco + dirCam * distAtual;
        transform.rotation = Quaternion.LookRotation(foco - transform.position, Vector3.up);
    }

    // ==================== Input System (ligue pelo PlayerInput/Events) ====================
    public void OnOlhar(InputAction.CallbackContext ctx)
    {
        if (ctx.performed || ctx.started)
            deltaOlhar += ctx.ReadValue<Vector2>();
    }

    public void OnArrastarCamera(InputAction.CallbackContext ctx)
    {
        if (!usarBotaoDireito) return;
        if (ctx.started)
        {
            arrastandoCamera = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (ctx.canceled)
        {
            arrastandoCamera = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Action "Zoom" = Axis (<Mouse>/scroll/y)
    public void OnZoom(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        float s = ctx.ReadValue<float>(); // + pra cima, - pra baixo
        distAlvo = Mathf.Clamp(distAlvo - s * sensZoom, distanciaMin, distanciaMax);
    }

    // Opcional: alinhar a câmera com a frente do player no spawn
    public void SincronizarComAlvo()
    {
        if (alvo == null) return;
        Vector3 f = Vector3.ProjectOnPlane(alvo.forward, Vector3.up).normalized;
        yaw = yawAlvo = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }
}
