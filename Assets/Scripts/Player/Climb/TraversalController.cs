using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Traversal/Traversal Controller (Ladder Only)")]
[DisallowMultipleComponent]
public class TraversalController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MoveController move;
    [SerializeField] private LadderClimber ladder;
    [SerializeField] private Animator animator;

    private void Reset()
    {
        move = GetComponent<MoveController>();
        ladder = GetComponent<LadderClimber>();
        animator = GetComponent<Animator>();
    }

    // Movimento 2D
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (move) move.OnMove(ctx);
        if (ladder) ladder.OnMove(ctx);
    }

    // Pulo (prioridade ladder)
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (ladder) { ladder.OnJump(ctx); return; }
        if (move) move.OnJump(ctx);
    }

    // Correr → Dash quando estiver escalando
    public void OnRun(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && ladder != null) ladder.RequestDashUp();
        if (move) move.OnRun(ctx);
    }

    // Interagir → entra/solta da ladder
    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (ladder) ladder.OnInteract(ctx);
    }

    // Rolar / Equip (passa pra locomoção base)
    public void OnRoll(InputAction.CallbackContext ctx) { if (move) move.OnRoll(ctx); }
    public void OnEquip(InputAction.CallbackContext ctx) { if (move) move.OnEquip(ctx); }
}
