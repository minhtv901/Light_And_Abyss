using TMPro;
using UnityEngine;

public class RageBarMaskUI : MonoBehaviour
{
    [Header("References")]
    public RageSystem rageSystem;

    [Tooltip("Object có RectMask2D, dùng để cắt phần nộ full từ trái sang phải.")]
    public RectTransform rageFullMask;

    [Tooltip("Image con nằm trong RageFullMask, gán sprite nộ full đã cắt riêng.")]
    public RectTransform rageFullImage;

    public TMP_Text rageText;

    [Header("Empty Bar Size - theo UI hiện tại của bạn")]
    public float emptyBarWidth = 412.5388f;
    public float emptyBarHeight = 79.861f;

    [Header("Fill Area - vùng cyan nằm trong khung empty")]
    public float fillAreaWidth = 317.6f;
    public float fillAreaHeight = 36.1f;

    [Tooltip("Khoảng cách từ mép trái RagePanel tới điểm bắt đầu phần cyan.")]
    public float fillOffsetX = 47.2f;

    [Tooltip("Độ lệch dọc của phần cyan. Âm là xuống dưới.")]
    public float fillOffsetY = -4.4f;

    [Header("Smooth Fill")]
    public bool smoothFill = true;

    [Tooltip("Càng nhỏ thì thanh nộ chạy càng chậm. Gợi ý: 0.6 - 1.5")]
    public float fillSpeed = 1.0f;

    [Header("Debug")]
    public bool autoFindRageSystem = true;
    public bool autoApplyRectSize = true;

    private float visualRatio = 0f;

    private void Awake()
    {
        if (rageSystem == null && autoFindRageSystem)
        {
            rageSystem = FindFirstObjectByType<RageSystem>();
        }

        visualRatio = GetTargetRatio();
        ApplyRectSetup();
        ApplyFill();
        UpdateText();
    }

    private void OnEnable()
    {
        visualRatio = GetTargetRatio();
        ApplyRectSetup();
        ApplyFill();
        UpdateText();
    }

    private void Update()
    {
        if (rageSystem == null || rageFullMask == null)
            return;

        float targetRatio = GetTargetRatio();

        if (smoothFill)
        {
            visualRatio = Mathf.MoveTowards(
                visualRatio,
                targetRatio,
                fillSpeed * Time.unscaledDeltaTime
            );
        }
        else
        {
            visualRatio = targetRatio;
        }

        ApplyFill();
        UpdateText();
    }

    private float GetTargetRatio()
    {
        if (rageSystem == null)
            return 0f;

        float max = Mathf.Max(0.01f, rageSystem.MaxRage);
        return Mathf.Clamp01(rageSystem.CurrentRage / max);
    }

    private void ApplyFill()
    {
        if (rageFullMask == null)
            return;

        float currentWidth = fillAreaWidth * Mathf.Clamp01(visualRatio);

        rageFullMask.sizeDelta = new Vector2(currentWidth, fillAreaHeight);

        if (rageFullImage != null)
        {
            rageFullImage.sizeDelta = new Vector2(fillAreaWidth, fillAreaHeight);
            rageFullImage.anchoredPosition = Vector2.zero;
        }
    }

    private void ApplyRectSetup()
    {
        if (!autoApplyRectSize)
            return;

        if (rageFullMask != null)
        {
            rageFullMask.anchorMin = new Vector2(0f, 0.5f);
            rageFullMask.anchorMax = new Vector2(0f, 0.5f);
            rageFullMask.pivot = new Vector2(0f, 0.5f);
            rageFullMask.anchoredPosition = new Vector2(fillOffsetX, fillOffsetY);
            rageFullMask.sizeDelta = new Vector2(fillAreaWidth * Mathf.Clamp01(visualRatio), fillAreaHeight);
        }

        if (rageFullImage != null)
        {
            rageFullImage.anchorMin = new Vector2(0f, 0.5f);
            rageFullImage.anchorMax = new Vector2(0f, 0.5f);
            rageFullImage.pivot = new Vector2(0f, 0.5f);
            rageFullImage.anchoredPosition = Vector2.zero;
            rageFullImage.sizeDelta = new Vector2(fillAreaWidth, fillAreaHeight);
        }
    }

    private void UpdateText()
    {
        if (rageText == null || rageSystem == null)
            return;

        rageText.text = $"{rageSystem.CurrentRage:0.0} / {rageSystem.MaxRage:0}";
    }

    [ContextMenu("Apply Rage UI Rect Setup")]
    private void ApplySetupFromInspector()
    {
        ApplyRectSetup();
        ApplyFill();
    }
}