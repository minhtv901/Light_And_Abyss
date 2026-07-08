using UnityEngine;
using UnityEngine.UI;

public class RageUI : MonoBehaviour
{
    public RageSystem rageSystem;
    public RectTransform rageFillRect;
    public Text rageText;

    public float fullWidth = 240f;

    private void Awake()
    {
        if (rageSystem == null)
        {
            rageSystem = FindFirstObjectByType<RageSystem>();
        }
    }

    private void OnEnable()
    {
        if (rageSystem != null)
        {
            rageSystem.OnRageChanged += UpdateUI;
        }
    }

    private void OnDisable()
    {
        if (rageSystem != null)
        {
            rageSystem.OnRageChanged -= UpdateUI;
        }
    }

    private void Start()
    {
        if (rageSystem != null)
        {
            UpdateUI(rageSystem.CurrentRage, rageSystem.MaxRage);
        }
    }

    private void UpdateUI(float current, float max)
    {
        float percent = current / max;
        percent = Mathf.Clamp01(percent);

        float targetWidth = fullWidth * percent;

        rageFillRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            fullWidth * percent
        );

        if (rageText != null)
        {
            rageText.text = Mathf.FloorToInt(current) + "/3";
        }
    }
}