using UnityEngine;

public class mscstandidle : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.Play("Standing W_Jcase Idle");
    }
}
