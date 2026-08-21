using System.Collections;
using UnityEngine;

public class mssatpamwalking : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 3f;

    private Animator animator;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        animator.Play("Walking(1)");
        StartCoroutine(WalkToTarget());
    }

    private IEnumerator WalkToTarget()
    {
        while (Vector3.Distance(transform.position, target.position) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = target.position;
        animator.Play("Idle");
    }
}
