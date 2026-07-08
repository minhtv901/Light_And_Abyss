using System;
using UnityEngine;

public class RageSystem : MonoBehaviour
{
    [Header("Rage Settings")]
    public float maxRage = 3f;
    public float currentRage = 0f;

    [Header("Gain")]
    public float normalAttackGain = 0.25f;
    public float subSkillGain = 0.35f;

    [Header("Cost")]
    public float skillCost = 1f;
    public float ultimateCost = 3f;

    public event Action<float, float> OnRageChanged;

    public float CurrentRage => currentRage;
    public float MaxRage => maxRage;
    public int FullStacks => Mathf.FloorToInt(currentRage);

    private void Start()
    {
        currentRage = Mathf.Clamp(currentRage, 0f, maxRage);
        NotifyChanged();
    }

    public void AddRage(float amount)
    {
        if (amount <= 0f) return;

        currentRage += amount;
        currentRage = Mathf.Clamp(currentRage, 0f, maxRage);

        NotifyChanged();
    }

    public bool HasEnoughRage(float amount)
    {
        return currentRage >= amount;
    }

    public bool TrySpendRage(float amount)
    {
        if (!HasEnoughRage(amount))
        {
            Debug.Log("Không đủ nộ.");
            return false;
        }

        currentRage -= amount;
        currentRage = Mathf.Clamp(currentRage, 0f, maxRage);

        NotifyChanged();
        return true;
    }

    public bool TrySpendSkill()
    {
        return TrySpendRage(skillCost);
    }

    public bool TrySpendUltimate()
    {
        return TrySpendRage(ultimateCost);
    }

    public bool IsFull()
    {
        return currentRage >= maxRage;
    }

    private void NotifyChanged()
    {
        OnRageChanged?.Invoke(currentRage, maxRage);
    }
}