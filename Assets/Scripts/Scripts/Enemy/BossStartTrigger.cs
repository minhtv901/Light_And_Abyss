using UnityEngine;

public class BossStartTrigger : MonoBehaviour
{
    public StationaryGreenFlameBossAI boss;
    public string playerTag = "Player";
    public bool triggerOnlyOnce = true;

    private bool used;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used && triggerOnlyOnce) return;
        if (!other.CompareTag(playerTag)) return;

        used = true;

        if (boss != null)
        {
            boss.BeginBossFight();
        }
    }
}
