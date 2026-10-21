using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    [Header("Passive Income")]
    [SerializeField, Min(0f)] private float goldPerSecond = 2f;
    [SerializeField, Min(0f)] private float experiencePerSecond = 1f;

    [Header("Level Settings")]
    [SerializeField, Min(1)] private int firstLevelExperience = 20;
    [SerializeField, Min(1f)] private float experienceGrowth = 1.25f;

    [Header("Current Progress")]
    [SerializeField] private float gold;
    [SerializeField] private float experience;
    [SerializeField] private int level = 1;
    [SerializeField] private int upgradePoints;
    [SerializeField] private int experienceToNextLevel;

    public float Gold => gold;
    public float Experience => experience;
    public int Level => level;
    public int UpgradePoints => upgradePoints;
    public int ExperienceToNextLevel => experienceToNextLevel;

    public float GoldPerSecond => goldPerSecond;
    public float ExperiencePerSecond => experiencePerSecond;

    private void Awake()
    {
        gold = 0f;
        experience = 0f;
        level = 1;
        upgradePoints = 0;
        experienceToNextLevel = Mathf.Max(1, firstLevelExperience);
    }

    private void Update()
    {
        AddGold(goldPerSecond * Time.deltaTime);
        AddExperience(experiencePerSecond * Time.deltaTime);
    }

    public void AddGold(float amount)
    {
        if (amount > 0f)
        {
            gold += amount;
        }
    }

    public bool TrySpendGold(int amount)
    {
        if (amount < 0 || gold < amount)
        {
            return false;
        }

        gold -= amount;
        return true;
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        experience += amount;

        // За крупную награду можно получить несколько уровней сразу.
        while (experience >= experienceToNextLevel)
        {
            experience -= experienceToNextLevel;
            level++;
            upgradePoints++;

            experienceToNextLevel = Mathf.CeilToInt(
                experienceToNextLevel * experienceGrowth
            );

            Debug.Log(
                $"Получен уровень {level}. " +
                $"Очки улучшения: {upgradePoints}",
                this
            );
        }
    }

    public bool TrySpendUpgradePoint()
    {
        if (upgradePoints <= 0)
        {
            return false;
        }

        upgradePoints--;
        return true;
    }

    // Эти методы позже будут использовать здания.
    public void IncreaseGoldIncome(float amount)
    {
        goldPerSecond = Mathf.Max(0f, goldPerSecond + amount);
    }

    public void IncreaseExperienceIncome(float amount)
    {
        experiencePerSecond = Mathf.Max(
            0f,
            experiencePerSecond + amount
        );
    }
}