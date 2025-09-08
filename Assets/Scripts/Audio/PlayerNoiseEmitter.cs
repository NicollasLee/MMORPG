using UnityEngine;

[AddComponentMenu("Audio/Player Noise Emitter")]
[DisallowMultipleComponent]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Raio base (m) por evento")]
    public float footstepBaseRadius = 6f;
    public float rollBaseRadius = 8f;
    public float landBaseRadius = 9f;

    [Tooltip("Origem do som (pé/centro do player). Se vazio, usa o transform atual.")]
    public Transform source;

    public void EmitFootstep(float strength01 = 1f)
    {
        if (NoiseSystem.I == null) return;
        NoiseSystem.I.Emit(new NoisePing
        {
            pos = source ? source.position : transform.position,
            strength01 = Mathf.Clamp01(strength01),
            baseRadius = footstepBaseRadius
        });
    }

    public void EmitRoll()
    {
        if (NoiseSystem.I == null) return;
        NoiseSystem.I.Emit(new NoisePing
        {
            pos = source ? source.position : transform.position,
            strength01 = 0.8f,
            baseRadius = rollBaseRadius
        });
    }

    public void EmitLand()
    {
        if (NoiseSystem.I == null) return;
        NoiseSystem.I.Emit(new NoisePing
        {
            pos = source ? source.position : transform.position,
            strength01 = 0.9f,
            baseRadius = landBaseRadius
        });
    }
}
