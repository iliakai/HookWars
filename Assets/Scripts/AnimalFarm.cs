using UnityEngine;

public class AnimalFarm : GameBuilding
{
    [SerializeField] private Animal[] animalPrefabs;
    [SerializeField, Min(0.1f)] private float spawnInterval = 15;
    [SerializeField, Min(0.1f)] private float minimumInterval = 3;
    [SerializeField, Range(0.1f, 0.99f)] private float intervalMultiplier = 0.8f;
    [SerializeField, Min(1)] private int upgradeCost = 75;
    [SerializeField, Min(1)] private int spawnAttempts = 24;
    [SerializeField, Min(0.1f)] private float retryDelay = 1;
    private double remaining;
    public float SpawnInterval => spawnInterval;
    public int Level {get;private set;}=1;
    public int UpgradeCost=>upgradeCost;
    public bool CanUpgrade=>owner!=null&&owner.Gold>=upgradeCost&&spawnInterval>minimumInterval;
    public override BuildingKind Kind => BuildingKind.Farm;
    public override string DisplayName => "Ферма";
    public override string Info => $"Случайное животное каждые {spawnInterval:0.##} с\nНа берегу фермы; овца, корова, свинья";
    public override string UpgradeLabel(int index) => $"Интервал → {Mathf.Max(minimumInterval, spawnInterval * intervalMultiplier):0.##} с: {upgradeCost} зол.";
    private void Awake() => remaining = spawnInterval;
    private void Update() => Advance(Time.deltaTime);
    public Animal Advance(double seconds)
    {
        if (owner == null || !IsOperating || MatchPause.BlocksGameplay || !double.IsFinite(seconds) || seconds <= 0) return null;
        remaining -= seconds;
        if (remaining > 0) return null;
        Animal result = TrySpawn();
        remaining = result != null ? spawnInterval : retryDelay;
        return result;
    }
    public Animal TrySpawn()
    {
        if (owner == null || !IsOperating || MatchPause.BlocksGameplay || animalPrefabs == null || animalPrefabs.Length == 0) return null;
        Animal prefab = animalPrefabs[Random.Range(0, animalPrefabs.Length)];
        bool right = transform.position.x > 0;
        Rect bounds = ArenaLayout.BankBounds(right, prefab.Radius);
        if (bounds.width <= 0 || bounds.height <= 0) return null;
        for (int attempt = 0; attempt < spawnAttempts; attempt++)
        {
            Vector3 position = new Vector3(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax));
            if (WorldCollision.FreeCircle(position, prefab.Radius)) return Instantiate(prefab, position, Quaternion.identity);
        }
        return null;
    }
    public override bool TryUpgrade(int index)
    {
        if (owner == null || index != 0 || spawnInterval <= minimumInterval || !owner.TrySpendGold(upgradeCost)) return false;
        spawnInterval = Mathf.Max(minimumInterval, spawnInterval * intervalMultiplier);
        Level++;
        remaining = System.Math.Min(remaining, spawnInterval);
        upgradeCost = NextCost(upgradeCost);
        return true;
    }
}
