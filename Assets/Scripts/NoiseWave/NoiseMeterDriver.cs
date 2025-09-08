using UnityEngine;

[AddComponentMenu("HUD/Noise Meter Driver")]
[DisallowMultipleComponent]
public class NoiseMeterDriver : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private NoiseWaveUI meter;                // arraste o NoiseWaveUI do Canvas
    [SerializeField] private CharacterController controller;   // opcional (auto por velocidade)
    [SerializeField] private Animator animator;                // opcional

    [Header("Entrada contínua (auto da velocidade)")]
    [SerializeField] private bool autoFromVelocity = true;
    [SerializeField, Tooltip("m/s -> nível 0")] private float speedAtZero = 0.2f;
    [SerializeField, Tooltip("m/s -> nível 1")] private float speedAtOne = 6.0f;
    [SerializeField, Tooltip("Constante de tempo (s) p/ suavizar")] private float levelSmooth = 0.12f;

    [Header("Pulsos (0..1)")]
    [SerializeField, Range(0f, 1f)] private float footstepPulse = 0.35f;
    [SerializeField, Range(0f, 1f)] private float rollPulse = 0.65f;
    [SerializeField, Range(0f, 1f)] private float landPulse = 0.55f;
    [SerializeField, Range(0f, 1f)] private float jumpPulse = 0.25f;

    [Header("Histerese (opcional)")]
    [SerializeField] private bool useHysteresis = false;
    [SerializeField, Range(0f, 1f)] private float alertEnter = 0.65f;
    [SerializeField, Range(0f, 1f)] private float alertExit = 0.55f;

    // estado interno
    float contLevel, contVel;
    bool isAlert;

    // (opcional) multiplicador externo (ex.: superfície) -> SetExternalMultiplier(x)
    float externalMul = 1f;

    void Reset()
    {
        meter = FindFirstObjectByType<NoiseWaveUI>();
        controller = GetComponent<CharacterController>() ?? FindFirstObjectByType<CharacterController>();
        animator = GetComponent<Animator>() ?? FindFirstObjectByType<Animator>();
    }

    void Awake()
    {
        if (!meter) meter = FindFirstObjectByType<NoiseWaveUI>();
    }

    void Update()
    {
        if (!meter) return;

        if (autoFromVelocity && controller != null)
        {
            Vector3 v = controller.velocity; v.y = 0f;
            float speed = v.magnitude;
            float alvo = Mathf.InverseLerp(speedAtZero, speedAtOne, speed); // 0..1
            contLevel = Mathf.SmoothDamp(contLevel, alvo, ref contVel, Mathf.Max(0.0001f, levelSmooth));
        }

        float level = Mathf.Clamp01(contLevel * Mathf.Max(0f, externalMul));
        meter.SetContinuousLevel(level);

        if (useHysteresis)
        {
            float eff = meter.CurrentLevel;
            if (!isAlert && eff >= alertEnter) isAlert = true;
            else if (isAlert && eff <= alertExit) isAlert = false;
            // Aqui você pode disparar eventos de estado se quiser
        }
    }

    // ======= API pública =======
    /// Define continuamente o nível 0..1 (andar/correr/furtivo etc).
    public void SetMoveSpeed01(float v01)
    {
        contLevel = Mathf.Clamp01(v01);
        meter?.SetContinuousLevel(contLevel * Mathf.Max(0f, externalMul));
    }

    /// Pulso genérico 0..1 (tiro, salto, queda, impacto etc).
    public void Pulse(float amount01) => meter?.Pulse(Mathf.Clamp01(amount01));

    // Eventos de passo (use via Animation Event)
    public void Footstep() => Pulse(footstepPulse);
    public void FootstepFloat(float strength01) => Pulse(strength01 <= 0f ? footstepPulse : Mathf.Clamp01(strength01));

    // Atalhos semânticos
    public void Roll() => Pulse(rollPulse);
    public void Land() => Pulse(landPulse);
    public void Jump() => Pulse(jumpPulse);

    // Superfície/ambiente podem ajustar sensibilidade (0.5 = grama macia, 1.2 = pedra)
    public void SetExternalMultiplier(float mul) => externalMul = Mathf.Max(0f, mul);

    // Getters
    public float CurrentLevel01 => meter ? meter.CurrentLevel : contLevel;
    public bool IsAlert => isAlert;

    // Expor config (se quiser mexer por code)
    public float FootstepPulse => footstepPulse;
    public float RollPulse => rollPulse;
    public float LandPulse => landPulse;
    public float JumpPulse => jumpPulse;
}
