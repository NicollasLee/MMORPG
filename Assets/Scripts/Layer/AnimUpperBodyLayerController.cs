using UnityEngine;

[AddComponentMenu("Animation/Anim Upper Body Layer Controller")]
public class AnimUpperBodyLayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Animator animator;

    [Header("Config")]
    [SerializeField] string upperBodyLayerName = "UpperBody"; // nome da layer no Animator
    [SerializeField] string armedBool = "Armed";               // bool que indica arma sacada
    [SerializeField] string onLadderBool = "OnLadder";         // bool setado pelo LadderClimber

    [Header("Blend (peso por segundo)")]
    [SerializeField] float blendInSpeed = 8f;   // qu�o r�pido sobe para 1
    [SerializeField] float blendOutSpeed = 10f;  // qu�o r�pido desce para 0

    // nomes dos estados upper-body que devem manter a layer ligada
    [Header("Estados que mant�m a layer ligada")]
    [SerializeField] string ubDrawState = "EquipUB/UB_Draw";
    [SerializeField] string ubSheathState = "EquipUB/UB_Sheath";
    [SerializeField] string ubArmedIdleState = "EquipUB/UB_ArmedIdle";

    int layerIndex;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        layerIndex = animator.GetLayerIndex(upperBodyLayerName);
        if (layerIndex < 0)
            Debug.LogError($"UpperBody layer '{upperBodyLayerName}' n�o encontrada no Animator.", this);

        // come�a desligada
        if (layerIndex >= 0) animator.SetLayerWeight(layerIndex, 0f);
    }

    void Update()
    {
        if (layerIndex < 0) return;

        // Leitura de estado
        bool armed = SafeGetBool(armedBool);
        bool onLadder = SafeGetBool(onLadderBool);

        var curInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        var nextInfo = animator.GetNextAnimatorStateInfo(layerIndex);
        bool inUB =
            curInfo.IsName(ubDrawState) || curInfo.IsName(ubSheathState) || curInfo.IsName(ubArmedIdleState) ||
            nextInfo.IsName(ubDrawState) || nextInfo.IsName(ubSheathState) || nextInfo.IsName(ubArmedIdleState);

        // Regra: layer ligada se (arma sacada OU tocando equip) E N�O estiver na escada
        float target = (armed || inUB) && !onLadder ? 1f : 0f;

        float current = animator.GetLayerWeight(layerIndex);
        float speed = target > current ? blendInSpeed : blendOutSpeed;
        current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
        animator.SetLayerWeight(layerIndex, current);
    }

    bool SafeGetBool(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // Evita exception se o par�metro n�o existir
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool)
                return animator.GetBool(name);
        return false;
    }
}
