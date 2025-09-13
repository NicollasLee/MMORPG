using UnityEngine;
using UnityEngine.Audio;

[AddComponentMenu("Combat/Sword Equip Controller")]
[DisallowMultipleComponent]
public class SwordEquipController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Animator animator;
    [SerializeField] GameObject swordBack;   // espada nas costas / bainha (ativa quando DESARMADO)
    [SerializeField] GameObject swordHand;   // espada na mão (ativa quando ARMADO)

    [Header("Optional: Physics on hand")]
    [SerializeField] Collider handCollider;  // collider da espada na mão (se existir)
    [SerializeField] Rigidbody handRb;       // rigidbody da espada na mão (se existir)

    [Header("Optional: Audio")]
    [SerializeField] AudioMixerGroup sfxGroup; // mixer p/ SFX
    [SerializeField] AudioClip drawClip;       // som ao sacar
    [SerializeField] AudioClip sheathClip;     // som ao guardar
    [SerializeField, Range(0f, 1f)] float foleyVol = 1f;

    [Header("Animator Params")]
    [SerializeField] string boolArmed = "Armed";  // precisa existir no Animator

    [Header("Start State")]
    [SerializeField] bool startArmed = false;     // usado se o Animator não tiver o bool

    AudioSource _oneshot;
    bool _armed;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        // Áudio one-shot 3D para foley
        _oneshot = gameObject.AddComponent<AudioSource>();
        _oneshot.playOnAwake = false;
        _oneshot.spatialBlend = 1f;
        if (sfxGroup) _oneshot.outputAudioMixerGroup = sfxGroup;

        // Estado inicial: se o Animator já tem o parâmetro, usamos ele; senão, startArmed
        bool hasArmedParam = HasBool(animator, boolArmed);
        bool initial = hasArmedParam ? animator.GetBool(boolArmed) : startArmed;

        ApplyVisualState(initial, /*instant*/ true);
        if (hasArmedParam) animator.SetBool(boolArmed, initial);
    }

    // ============================================================
    // ===              API PRINCIPAL (EVENT-DRIVEN)             ===
    // ============================================================
    // Chame estes dois via Animation Events nos clipes:
    //  - No clipe de "sacar" (Draw): no frame em que a mão pega a espada -> ShowWeapon()
    //  - No clipe de "guardar" (Sheath): quando a espada entra na bainha -> HideWeapon()

    public void ShowWeapon()   // Animation Event
    {
        SetEquipped(true);
        PlayFoley("draw");
    }

    public void HideWeapon()   // Animation Event
    {
        SetEquipped(false);
        PlayFoley("sheath");
    }

    // Aliases para compatibilidade com nomes antigos de evento (se você já os colocou no clip)
    public void SwapToHand() => ShowWeapon();
    public void SwapToBack() => HideWeapon();

    /// <summary>
    /// Força estado de armado/desarmado. Útil para sincronizar por código em casos especiais.
    /// Normalmente, **deixe os Animation Events** chamarem ShowWeapon/HideWeapon.
    /// </summary>
    public void SetEquipped(bool armed, bool alsoSetAnimatorBool = true)
    {
        _armed = armed;

        if (alsoSetAnimatorBool && HasBool(animator, boolArmed))
            animator.SetBool(boolArmed, _armed);

        ApplyVisualState(_armed, /*instant*/ true);
    }

    // ============================================================
    // ===                    SUPORTE / ÁUDIO                    ===
    // ============================================================

    public void PlayFoley(string id)
    {
        if (_oneshot == null) return;
        AudioClip clip = null;
        if (id == "draw") clip = drawClip;
        else if (id == "sheath") clip = sheathClip;
        if (clip) _oneshot.PlayOneShot(clip, Mathf.Clamp01(foleyVol));
    }

    public bool IsArmed => _armed;

    // ============================================================
    // ===                   IMPLEMENTAÇÃO                       ===
    // ============================================================

    void ApplyVisualState(bool armed, bool instant)
    {
        // Liga/desliga objetos
        if (swordBack) swordBack.SetActive(!armed);
        if (swordHand) swordHand.SetActive(armed);

        // Physics na mão
        if (handCollider) handCollider.enabled = armed;
        if (handRb) handRb.isKinematic = !armed;

        // (Se quiser interpolar/animar visualmente no futuro, use 'instant' para decidir)
    }

    static bool HasBool(Animator anim, string name)
    {
        if (!anim || string.IsNullOrEmpty(name)) return false;
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].type == AnimatorControllerParameterType.Bool && ps[i].name == name)
                return true;
        return false;
    }

    // ==========================
    // ===== Deprecations =======
    // ==========================
    // Mantidos apenas para compatibilidade. Não dispare animação por aqui.
    // O fluxo oficial é: input alterna 'Armed' + Trigger 'Equip' (na UpperBody),
    // e a troca visual acontece por Animation Events (ShowWeapon/HideWeapon).

    [ContextMenu("DEBUG/Toggle Immediate (visual only)")]
    void Debug_ToggleImmediate()
    {
        SetEquipped(!_armed);
    }
}
