using UnityEngine;
using System.Collections.Generic;

public struct NoisePing
{
    public Vector3 pos;
    public float strength01; // 0..1
    public float baseRadius; // metros (raio base antes de multiplicadores)
}

[AddComponentMenu("AI/Noise System (Global)")]
[DisallowMultipleComponent]
public class NoiseSystem : MonoBehaviour
{
    public static NoiseSystem I;

    [Header("Ajustes globais (opcional)")]
    [Tooltip("Multiplicador global aplicado ao raio final de todos os pings.")]
    public float globalRadiusMul = 1f;

    readonly List<EnemyHearing> listeners = new List<EnemyHearing>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        // Opcional: DontDestroyOnLoad(gameObject);
    }

    public void Register(EnemyHearing h)
    {
        if (h != null && !listeners.Contains(h)) listeners.Add(h);
    }

    public void Unregister(EnemyHearing h)
    {
        listeners.Remove(h);
    }

    public void Emit(NoisePing ping)
    {
        // Raio final: cresce levemente com a força (0..1) — ajuste a gosto
        float radius = Mathf.Max(0f, ping.baseRadius) * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(ping.strength01));
        radius *= Mathf.Max(0f, globalRadiusMul);

        for (int i = 0; i < listeners.Count; i++)
            listeners[i].OnNoise(ping.pos, radius, Mathf.Clamp01(ping.strength01));
    }
}

[AddComponentMenu("AI/Enemy Hearing (Listener)")]
public class EnemyHearing : MonoBehaviour
{
    [Header("Audição")]
    public LayerMask occluders = ~0;           // obstáculos que bloqueiam som
    [Range(0f, 1f)] public float minReactStrength = 0.15f;

    void OnEnable() { if (NoiseSystem.I) NoiseSystem.I.Register(this); }
    void OnDisable() { if (NoiseSystem.I) NoiseSystem.I.Unregister(this); }

    /// Chamado pelo NoiseSystem
    public void OnNoise(Vector3 pos, float radius, float strength01)
    {
        if (strength01 < minReactStrength) return;

        float d = Vector3.Distance(transform.position, pos);
        if (d > radius) return;

        // “linha de visão acústica” simples
        if (Physics.Linecast(pos + Vector3.up * 0.1f, transform.position + Vector3.up * 1.5f, occluders))
            return;

        // Reação mínima: vire para investigar (substitua pelo seu brain de IA)
        var to = (pos - transform.position);
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

        // TODO: EnemyBrain.NotifyHeardSound(pos, strength01);
    }
}
