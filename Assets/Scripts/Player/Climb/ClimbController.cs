using UnityEngine;

[AddComponentMenu("Movement/Climb Controller")]
[RequireComponent(typeof(CharacterController))]
public class ClimbController : MonoBehaviour
{
    public enum ClimbState { None, Hanging, ClimbUp, Drop }

    [Header("Refs")]
    public Animator animator;
    public CharacterController controller;
    public LedgeDetector detector;

    [Header("Input (usa MoveController inputs)")]
    public string paramClimbing = "Climbing";
    public string paramClimbX = "ClimbX";
    public string trigMantle = "Mantle";
    public string trigDrop = "Drop";

    [Header("Shimmy")]
    public float shimmySpeed = 1.8f; // m/s

    LedgeInfo ledge;
    ClimbState state = ClimbState.None;
    Vector3 climbRight; // tangente da parede
    float lastMoveTime;

    void Reset()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        detector = GetComponent<LedgeDetector>();
    }

    public bool IsClimbing => state != ClimbState.None;

    // ---- Entrada externa (ex.: do MoveController) ----
    public bool TryStartClimb(Vector3 inputDirWorld)
    {
        if (state != ClimbState.None) return false;

        if (detector.FindLedge(out ledge, inputDirWorld))
        {
            EnterHang(ledge);
            return true;
        }
        return false;
    }

    public void RequestMantle()   // W / Espaço
    {
        if (state == ClimbState.Hanging)
        {
            state = ClimbState.ClimbUp;
            animator.ResetTrigger(trigDrop);
            animator.SetTrigger(trigMantle);
        }
    }

    public void RequestDrop()     // S
    {
        if (state == ClimbState.Hanging)
        {
            state = ClimbState.Drop;
            animator.ResetTrigger(trigMantle);
            animator.SetTrigger(trigDrop);
        }
    }

    void EnterHang(LedgeInfo li)
    {
        state = ClimbState.Hanging;
        ledge = li;

        // trava controller e posiciona
        controller.enabled = false;
        transform.position = li.hangPoint;
        transform.rotation = Quaternion.LookRotation(-li.normal, Vector3.up);
        controller.enabled = true;

        // tangente p/ shimmy
        climbRight = Vector3.Cross(Vector3.up, li.normal).normalized;

        animator.SetBool(paramClimbing, true);
        animator.SetFloat(paramClimbX, 0f);
        // forçar entrar no HangIdle: se quiser, animator.CrossFade("HangIdle", 0.05f);
    }

    // chamado por Animation Event no final de ClimbUp
    public void OnMantleStand()
    {
        // teleporta para o topo e sai do climb
        controller.enabled = false;
        transform.position = ledge.standPoint;
        controller.enabled = true;

        animator.SetBool(paramClimbing, false);
        state = ClimbState.None;
    }

    // chamado por Animation Event no final de Drop
    public void OnDropFinished()
    {
        animator.SetBool(paramClimbing, false);
        state = ClimbState.None;
    }

    void Update()
    {
        if (state == ClimbState.Hanging)
        {
            // leitura simples de A/D para shimmy
            float x = Input.GetAxisRaw("Horizontal"); // se usar Input System novo, injete via outro script
            animator.SetFloat(paramClimbX, x);

            if (Mathf.Abs(x) > 0.01f)
            {
                Vector3 delta = climbRight * (x * shimmySpeed * Time.deltaTime);

                // mantém distância da parede
                Vector3 desired = transform.position + delta;
                Vector3 backToWall = ledge.normal * (detector.hangForwardOffset + controller.radius);
                desired = new Vector3(desired.x, ledge.hangPoint.y, desired.z);
                desired -= backToWall; // “cola” novamente

                controller.enabled = false;
                transform.position = desired;
                controller.enabled = true;

                lastMoveTime = Time.time;
            }
            else
            {
                // mantém altura na borda (evita drift)
                Vector3 p = transform.position;
                p.y = ledge.hangPoint.y;
                controller.enabled = false;
                transform.position = p;
                controller.enabled = true;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (state != ClimbState.None)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(ledge.hangPoint, 0.05f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(ledge.standPoint, 0.05f);
        }
    }
}
