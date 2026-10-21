using UnityEngine;

public class BoneWall : GameBuilding
{
    [SerializeField, Min(1)] private float maxHealth = 100;
    [SerializeField] private float currentHealth;
    [SerializeField, Min(1)] private float healthIncrease = 50;
    [SerializeField, Min(1)] private int upgradeCost = 5;
    private bool destroyed;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => destroyed;
    public override BuildingKind Kind => BuildingKind.Wall;
    public override string DisplayName => "Стена из костей";
    public override string Info => $"Здоровье: {currentHealth:0.##} / {maxHealth:0.##}\nПостройка и улучшения только за кости";
    public override string UpgradeLabel(int index) => $"Макс. и текущее HP +{healthIncrease}: {upgradeCost} костей";
    private void Awake() => currentHealth = maxHealth;
    public bool TakeHookDamage(PlayerProgression attacker, float damage)
    {
        if (destroyed) return true;
        if (attacker == null || attacker == owner || damage <= 0) return false;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth > 0) return false;
        destroyed = true;
        // Release physics occupancy immediately, before deferred Unity destruction.
        foreach (Collider2D collider in GetComponents<Collider2D>()) collider.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
    public override bool TryUpgrade(int index)
    {
        if (owner == null || destroyed || index != 0 || !owner.GetComponent<OwnerEconomy>().TrySpendBones(upgradeCost)) return false;
        maxHealth += healthIncrease;
        currentHealth += healthIncrease;
        upgradeCost = NextCost(upgradeCost);
        return true;
    }
}
