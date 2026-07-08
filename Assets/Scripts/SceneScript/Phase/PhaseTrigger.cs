using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class PhaseTrigger : MonoBehaviour
{
    [Header("Cấu hình Phase")]
    public int phaseNumber;

    [Header("Cấu hình Camera")]
    public CinemachineCamera virtualCamera;
    public Collider2D nextCamBoundary;

    [Header("Tường chắn sau lưng")]
    public GameObject invisibleWall;

    [Header("Khóa cổng đi tiếp cho đến khi clear quái")]
    public CombatZoneSpawner combatZoneSpawner;

    [Tooltip("Tường/collider chặn cổng đi tiếp. Khi combat bắt đầu thì bật, clear hết quái thì tắt.")]
    public GameObject nextGateLockWall;

    [Tooltip("Nếu cổng đi tiếp là Trigger chuyển màn/phase thì kéo collider trigger đó vào đây. Khi combat chưa clear thì trigger sẽ bị tắt.")]
    public Collider2D nextGateTrigger;

    public bool lockNextGateUntilCombatClear = true;

    private bool hasTriggered = false;
    private Coroutine waitClearRoutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered) return;

        hasTriggered = true;

        // 1. Đổi vùng giới hạn camera
        if (virtualCamera != null && nextCamBoundary != null)
        {
            CinemachineConfiner2D confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();

            if (confiner != null)
            {
                confiner.BoundingShape2D = nextCamBoundary;
                confiner.InvalidateBoundingShapeCache();

                Debug.Log("Đã đổi giới hạn Camera sang Phase " + phaseNumber);
            }
            else
            {
                Debug.LogWarning("Virtual Camera chưa có CinemachineConfiner2D.");
            }
        }

        // 2. Khóa đường lùi
        if (invisibleWall != null)
            invisibleWall.SetActive(true);

        // 3. Khóa cổng đi tiếp
        if (lockNextGateUntilCombatClear)
        {
            SetNextGateLocked(true);

            if (waitClearRoutine != null)
                StopCoroutine(waitClearRoutine);

            waitClearRoutine = StartCoroutine(WaitUntilCombatCleared());
        }

        Debug.Log("Bắt đầu Phase " + phaseNumber + ". Đang khóa cổng đi tiếp cho đến khi tiêu diệt hết quái.");
    }

    private IEnumerator WaitUntilCombatCleared()
    {
        if (combatZoneSpawner == null)
        {
            Debug.LogWarning("PhaseTrigger chưa được gán CombatZoneSpawner. Không thể tự mở cổng.");
            yield break;
        }

        // Chờ CombatZone thật sự bắt đầu
        while (!combatZoneSpawner.HasStarted)
            yield return null;

        // Chờ clear hết quái
        while (!combatZoneSpawner.IsCleared)
            yield return null;

        SetNextGateLocked(false);

        Debug.Log("Phase " + phaseNumber + " đã clear hết quái. Đã mở cổng đi tiếp.");
    }

    private void SetNextGateLocked(bool locked)
    {
        if (nextGateLockWall != null)
            nextGateLockWall.SetActive(locked);

        if (nextGateTrigger != null)
            nextGateTrigger.enabled = !locked;
    }
}