using System.Collections;
using UnityEngine;

public class mscwalking : MonoBehaviour
{
    [SerializeField] private Vector3 targetPosition = new Vector3(-42.9f, 5.316201f, -1.017436f);
    [SerializeField] private float speed = 3f;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetBool("IsWalking", true);
        StartCoroutine(WalkToTarget());
    }

    private IEnumerator WalkToTarget()
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
        animator.SetBool("IsWalking", false);
    }
}
