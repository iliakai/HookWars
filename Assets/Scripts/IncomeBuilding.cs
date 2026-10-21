using UnityEngine;

public class IncomeBuilding : MonoBehaviour
{
    public enum ResourceType
    {
        Gold,
        Experience
    }

    [SerializeField] private ResourceType resource = ResourceType.Gold;
    [SerializeField, Min(0f)] private float baseBonus = 1f;
    [SerializeField, Min(0f)] private float bonusPerUpgrade = 1f;
    [SerializeField, Min(1)] private int firstUpgradeCost = 75;
    [SerializeField, Min(1f)] private float costGrowth = 1.5f;

    private PlayerProgression owner;
    private bool initialized;

    private int level = 1;
    private int upgradeCost;
    private float totalBonus;

    public PlayerProgression Owner => owner;
    public int Level => level;
    public int UpgradeCost => upgradeCost;
    public float TotalBonus => totalBonus;

    public string DisplayName =>
        resource == ResourceType.Gold ? "Рудник" : "Университет";

    public string ResourceLabel =>
        resource == ResourceType.Gold ? "золота/с" : "опыта/с";

    public void Initialize(PlayerProgression newOwner)
    {
        if (initialized || newOwner == null)
        {
            return;
        }

        owner = newOwner;
        initialized = true;

        level = 1;
        upgradeCost = firstUpgradeCost;
        totalBonus = baseBonus;

        ChangeIncome(baseBonus);
    }

    public bool TryUpgrade()
    {
        if (!initialized || owner == null)
        {
            return false;
        }

        if (!owner.TrySpendGold(upgradeCost))
        {
            return false;
        }

        level++;
        totalBonus += bonusPerUpgrade;
        ChangeIncome(bonusPerUpgrade);

        // Защита от переполнения числового типа при очень больших ценах.
        double nextCost = System.Math.Ceiling(
            (double)upgradeCost * costGrowth
        );

        upgradeCost = (int)System.Math.Min(nextCost, int.MaxValue);

        return true;
    }

    private void ChangeIncome(float amount)
    {
        if (resource == ResourceType.Gold)
        {
            owner.IncreaseGoldIncome(amount);
        }
        else
        {
            owner.IncreaseExperienceIncome(amount);
        }
    }

    protected virtual void OnDestroy()
    {
        if (initialized && owner != null)
        {
            ChangeIncome(-totalBonus);
        }
    }
}