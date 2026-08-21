using System.Collections;
using UnityEngine;

public class msctyping : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.Play("Typing");
        StartCoroutine(StandAfterTyping());
    }

    private IEnumerator StandAfterTyping()
    {
        // Tunggu sampai state Typing sedang berjalan
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Typing"));

        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(duration);

        animator.SetBool("IsStand", true);
    }
}
