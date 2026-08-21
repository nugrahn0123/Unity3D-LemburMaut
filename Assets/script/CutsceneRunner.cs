using UnityEngine;

// Penggerak lurus untuk kebutuhan cutscene/rekaman: bergerak maju ke arah
// target (mis. jebakan) tanpa AI, lalu berhenti saat sampai.
public class CutsceneRunner : MonoBehaviour
{
    [Tooltip("Target tujuan, mis. object Jebakan. Kosong = lari lurus searah hadap.")]
    public Transform target;
    public float speed = 6f;
    public float stopDistance = 0.5f;
    [Tooltip("Mulai bergerak otomatis saat Play. Matikan jika mau dipicu manual.")]
    public bool playOnStart = true;

    [Header("Saat Sampai Target")]
    [Tooltip("Nama trigger Animator yang dipicu saat sampai (mis. animasi mati). Kosongkan jika tidak perlu.")]
    public string arrivalTrigger = "Die";

    private bool running;
    private Animator animator;

    void Start()
    {
        running = playOnStart;
        animator = GetComponentInChildren<Animator>();

        if (target != null)
        {
            Vector3 look = target.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look);
        }
    }

    public void Play() => running = true;

    void Update()
    {
        if (!running)
            return;

        Vector3 direction = transform.forward;

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= Mathf.Max(0.05f, stopDistance))
            {
                running = false;
                OnArrived();
                return;
            }

            direction = toTarget.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        transform.position += direction * speed * Time.deltaTime;
    }

    void OnArrived()
    {
        if (animator == null || string.IsNullOrEmpty(arrivalTrigger))
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == arrivalTrigger)
            {
                animator.SetTrigger(arrivalTrigger);
                return;
            }
        }
    }
}
