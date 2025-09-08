using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Audio/Footstep Audio Emitter")]
[DisallowMultipleComponent]
public class FootstepAudioEmitter : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceSet
    {
        public string key;                 // "Grass", "Stone", etc. (igual ao PhysicMaterial.name ou Tag)
        public AudioClip[] clips;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.95f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.05f;
    }

    [Header("Refs")]
    public Transform footOrigin;           // ponto no pé/centro
    public LayerMask groundMask = ~0;

    [Header("Pool")]
    public int poolSize = 6;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float minDistance = 1.5f;
    public float maxDistance = 18f;

    [Header("Superfícies")]
    public SurfaceSet[] sets;
    public SurfaceSet defaultSet;

    readonly Queue<AudioSource> pool = new Queue<AudioSource>();
    readonly Dictionary<string, SurfaceSet> map = new Dictionary<string, SurfaceSet>();
    RaycastHit _hit;

    void Awake()
    {
        foreach (var s in sets) if (s != null && !map.ContainsKey(s.key)) map.Add(s.key, s);
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("AS_Footstep_" + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            pool.Enqueue(src);
        }
    }

    AudioSource GetSrc()
    {
        var src = pool.Dequeue();
        pool.Enqueue(src);
        return src;
    }

    string DetectSurfaceKey()
    {
        Vector3 origin = footOrigin ? footOrigin.position : transform.position;
        if (Physics.Raycast(origin + Vector3.up * 0.25f, Vector3.down, out _hit, 1.2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            var pm = _hit.collider.sharedMaterial;
            if (pm && map.ContainsKey(pm.name)) return pm.name;

            var tag = _hit.collider.tag;
            if (!string.IsNullOrEmpty(tag) && map.ContainsKey(tag)) return tag;
        }
        return defaultSet != null ? defaultSet.key : null;
    }

    public void PlayFootstep(float strength01 = 1f)
    {
        var key = DetectSurfaceKey();
        SurfaceSet set = null;
        if (key != null && map.TryGetValue(key, out var found)) set = found;
        else set = defaultSet;

        if (set == null || set.clips == null || set.clips.Length == 0) return;

        var src = GetSrc();
        src.clip = set.clips[Random.Range(0, set.clips.Length)];
        src.volume = set.volume * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(strength01));
        src.pitch = Random.Range(set.pitchMin, set.pitchMax);
        src.transform.position = footOrigin ? footOrigin.position : transform.position;
        src.Play();
    }
}
