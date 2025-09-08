using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("HUD/Noise Wave UI")]
[DisallowMultipleComponent]
public class NoiseWaveUI : Graphic
{
    [Header("Aparência")]
    [SerializeField, Range(0.5f, 16f)] private float thickness = 2.5f;
    [SerializeField, Range(8, 256)] private int samples = 96;
    [SerializeField] private Color waveColor = new Color(0.90f, 0.40f, 0.95f, 1f);

    [Header("Animação da Onda")]
    [SerializeField, Tooltip("Quantas cristas cabem na largura")]
    private float frequency = 6.0f;
    [SerializeField, Tooltip("Velocidade de deslocamento horizontal")]
    private float scrollSpeed = 2.0f;
    [SerializeField, Tooltip("Amplitude base (px) quando nível==0")]
    private float baseAmplitude = 0.0f;
    [SerializeField, Tooltip("Quanto a amplitude cresce com o nível (px)")]
    private float levelAmplitude = 10f;

    [Header("Envelope do Nível")]
    [SerializeField, Tooltip("Multiplica o nível contínuo (movimento).")]
    private float continuousWeight = 1.0f;

    [SerializeField, Tooltip("Tempo (s) para o pulso cair até ~10% (meia-vida exponencial).")]
    private float pulseHalfLife = 0.35f;

    [SerializeField, Tooltip("Constante de tempo (s) da suavização do nível contínuo.")]
    private float followSmooth = 0.08f;

    // estado
    float targetContinuous;   // alvo 0..1 de SetContinuousLevel
    float currentContinuous;  // suavizado
    float pulseEnvelope;      // decaindo
    float phase;              // deslocamento horizontal

    UIVertex[] vbuf;

    public override Texture mainTexture => s_WhiteTexture;

    protected override void Awake()
    {
        base.Awake();
        color = waveColor;
        raycastTarget = false; // HUD não bloqueia cliques
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // anima a fase
        phase += scrollSpeed * dt;

        // segue o alvo contínuo com suavização exponencial estável
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, followSmooth));
        currentContinuous = Mathf.Lerp(currentContinuous, Mathf.Clamp01(targetContinuous), k);

        // decai o pulso exponencial (meia-vida até ~10%)
        if (pulseEnvelope > 0f)
        {
            float lambda = Mathf.Log(10f) / Mathf.Max(0.0001f, pulseHalfLife);
            pulseEnvelope = Mathf.Max(0f, pulseEnvelope * Mathf.Exp(-lambda * dt));
        }

        SetVerticesDirty(); // redesenha
    }

    // ===== API pública =====
    public void SetContinuousLevel(float level01)
    {
        targetContinuous = Mathf.Clamp01(level01) * Mathf.Max(0f, continuousWeight);
    }

    public void Pulse(float amount01)
    {
        pulseEnvelope = Mathf.Clamp01(pulseEnvelope + Mathf.Clamp01(amount01));
    }

    // ===== Render =====
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        int n = Mathf.Clamp(samples, 8, 256);
        if (vbuf == null || vbuf.Length < n * 2)
            vbuf = new UIVertex[n * 2];

        Rect r = GetPixelAdjustedRect();
        float w = Mathf.Max(1f, r.width);
        float h = Mathf.Max(1f, r.height);
        float midY = r.y + h * 0.5f;

        float level = Mathf.Clamp01(Mathf.Max(currentContinuous, pulseEnvelope));
        float amp = Mathf.Max(0f, baseAmplitude + level * levelAmplitude);
        float halfT = Mathf.Max(0.25f, thickness * 0.5f);
        float freq = Mathf.Max(0f, frequency);

        for (int i = 0; i < n; i++)
        {
            float t = (n <= 1) ? 0f : (i / (n - 1f));
            float x = r.x + t * w;

            float yCenter = midY;
            if (freq > 0f && amp > 0f)
                yCenter += Mathf.Sin((t * freq + phase) * Mathf.PI * 2f) * amp;

            Vector3 p0 = new Vector3(x, yCenter - halfT, 0f);
            Vector3 p1 = new Vector3(x, yCenter + halfT, 0f);

            int vi = i * 2;
            vbuf[vi + 0] = MakeVert(p0, color);
            vbuf[vi + 1] = MakeVert(p1, color);

            if (i > 0)
            {
                int bi = (i - 1) * 2;
                vh.AddUIVertexQuad(new[]
                {
                    vbuf[bi + 0],
                    vbuf[bi + 1],
                    vbuf[vi + 1],
                    vbuf[vi + 0]
                });
            }
        }
    }

    static UIVertex MakeVert(Vector3 pos, Color32 col)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = col;
        v.uv0 = Vector2.zero;
        return v;
    }

    // Getters úteis se quiser ler do Driver
    public float CurrentLevel => Mathf.Clamp01(Mathf.Max(currentContinuous, pulseEnvelope));
}
