using UnityEngine;

public class SceneGate : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName = "mew2";

    [Header("Optional Settings")]
    [SerializeField] private bool requireAllEnemiesDead = false;
    [SerializeField] private string enemyTag = "Enemy";

    private bool used;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        if (requireAllEnemiesDead)
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            if (enemies.Length > 0)
            {
                Debug.Log("Chua diet het quai, chua the qua man.");
                return;
            }
        }

        used = true;

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("SceneTransitionManager chua ton tai trong scene. Hay tao object SceneTransitionManager va gan script vao.");
            return;
        }

        SceneTransitionManager.Instance.LoadSceneWithFade(targetSceneName);
    }
}
