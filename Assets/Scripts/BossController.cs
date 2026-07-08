using UnityEngine;

public class BossController : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Khi đại ca nhấn phím K, boss sẽ tung chiêu
        if (Input.GetKeyDown(KeyCode.K))
        {
            animator.SetBool("isCasting", true);
        }

        // Khi đại ca thả phím K ra, nó quay lại đứng im
        if (Input.GetKeyUp(KeyCode.K))
        {
            animator.SetBool("isCasting", false);
        }
    }
}