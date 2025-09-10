using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.Audio;

[AddComponentMenu("Combat/Sword Equip Controller")]
[DisallowMultipleComponent]
public class SwordEquipController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public GameObject swordBack;   // ativa nas costas
    public GameObject swordHand;   // ativa na mão

    [Header("Optional: Physics on hand")]
    public Collider handCollider;  // collider da espada na mão (se tiver)
    public Rigidbody handRb;       // rigidbody da espada na mão (se tiver)

    [Header("Optional: Audio")]
    public AudioMixerGroup sfxGroup;  // arraste um grupo do seu Mixer (ex.: SFX)
    public AudioClip drawClip;        // som ao sacar (metal)
    public AudioClip sheathClip;      // som ao guardar
    public float foleyVol = 1f;

    [Header("Animator Params")]
    public string trigDraw = "Draw";
    public string trigSheath = "Sheath";
    public string boolArmed = "Armed";

    [Header("Start State")]
    public bool startArmed = false;

    AudioSource _oneshot;
    bool _armed;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        // oneshot escondido pra tocar foley
        _oneshot = gameObject.AddComponent<AudioSource>();
        _oneshot.playOnAwake = false;
        _oneshot.spatialBlend = 1f;
        if (sfxGroup) _oneshot.outputAudioMixerGroup = sfxGroup;

        SetArmedImmediate(startArmed);
    }

    void SetArmedImmediate(bool armed)
    {
        _armed = armed;
        if (animator && !string.IsNullOrEmpty(boolArmed))
            animator.SetBool(boolArmed, _armed);

        if (swordBack) swordBack.SetActive(!_armed);
        if (swordHand) swordHand.SetActive(_armed);

        if (handCollider) handCollider.enabled = _armed;
        if (handRb) handRb.isKinematic = !_armed;
    }

    // ===== API de jogo (chame por input) =====
    public void Toggle()
    {
        if (_armed)
        {
            animator.SetTrigger(trigSheath); // guarda
            _armed = false;
        }
        else
        {
            animator.SetTrigger(trigDraw); // saca
            _armed = true;
        }
        animator.SetBool(boolArmed, _armed);
    }


    public void Draw()
    {
        if (_armed) return;
        animator.ResetTrigger(trigSheath);
        animator.SetTrigger(trigDraw);
        // A troca visual acontece no Animation Event -> SwapToHand()
    }

    public void Sheath()
    {
        if (!_armed) return;
        animator.ResetTrigger(trigDraw);
        animator.SetTrigger(trigSheath);
        // A troca visual acontece no Animation Event -> SwapToBack()
    }

    // ======= MÉTODOS DE ANIMATION EVENT =======
    // chame no clipe de "sacar" quando a mão pegar a espada
    public void SwapToHand()
    {
        _armed = true;
        if (animator && !string.IsNullOrEmpty(boolArmed))
            animator.SetBool(boolArmed, true);

        if (swordBack) swordBack.SetActive(false);
        if (swordHand) swordHand.SetActive(true);

        if (handCollider) handCollider.enabled = true;
        if (handRb) handRb.isKinematic = false;
    }

    // chame no clipe de "guardar" quando encaixar nas costas
    public void SwapToBack()
    {
        _armed = false;
        if (animator && !string.IsNullOrEmpty(boolArmed))
            animator.SetBool(boolArmed, false);

        if (swordHand) swordHand.SetActive(false);
        if (swordBack) swordBack.SetActive(true);

        if (handCollider) handCollider.enabled = false;
        if (handRb) handRb.isKinematic = true;
    }

    // opcional: som pelos events (String = "draw" ou "sheath")
    public void PlayFoley(string id)
    {
        if (_oneshot == null) return;
        AudioClip c = null;
        if (id == "draw") c = drawClip;
        else if (id == "sheath") c = sheathClip;
        if (c) _oneshot.PlayOneShot(c, Mathf.Max(0f, foleyVol));
    }

    // getters
    public bool IsArmed => _armed;
}
