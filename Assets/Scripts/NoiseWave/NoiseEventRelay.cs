using UnityEngine;

[AddComponentMenu("HUD/Noise Event Relay")]
[DisallowMultipleComponent]
public class NoiseEventRelay : MonoBehaviour
{
    [Header("Destino principal (HUD)")]
    public NoiseMeterDriver driver;

    [Header("Opcional: Som de passo (se usar)")]
    public FootstepAudioEmitter emitter;          // se não tiver, ignore
    [Range(0f, 1f)] public float defaultStrength = 0.8f;

    [Header("Opcional: IA (se usar)")]
    public PlayerNoiseEmitter playerNoiseEmitter; // se não tiver, ignore

    // ========= Chamadas de animação =========
    public void Footstep() => FootstepFloat(defaultStrength);
    public void FootstepFloat(float strength01)
    {
        float s = (strength01 <= 0f) ? defaultStrength : Mathf.Clamp01(strength01);
        if (driver) driver.FootstepFloat(s);
        if (emitter) emitter.PlayFootstep(s);
        if (playerNoiseEmitter) playerNoiseEmitter.EmitFootstep(s);
    }

    public void Jump()
    {
        if (driver) driver.Jump();
        // jump raramente tem som de impacto: deixe a cargo do foley se quiser
        if (playerNoiseEmitter) playerNoiseEmitter.EmitFootstep(0.35f); // opcional
    }

    public void Land()
    {
        if (driver) driver.Land();
        if (playerNoiseEmitter) playerNoiseEmitter.EmitLand();
    }

    public void Roll()
    {
        if (driver) driver.Roll();
        if (playerNoiseEmitter) playerNoiseEmitter.EmitRoll();
    }
}
