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
    public bool CanUpgrade=>owner!=null&&owner.Gold>=upgradeCost;
    public float MassMultiplier=>1+.2f*(Level-1);
    public override BuildingKind Kind => BuildingKind.Farm;
    public override string DisplayName => "Ферма";
    public override string Info => $"Уровень {Level} · животное каждые {spawnInterval:0.##} с\nВес: овца {4*MassMultiplier:0.#}, корова {10*MassMultiplier:0.#}, свинья {6*MassMultiplier:0.#} кг";
    public override string UpgradeLabel(int index) => $"Интервал → {Mathf.Max(minimumInterval, spawnInterval * intervalMultiplier):0.##} с, вес +20% базы: {upgradeCost} зол.";
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
            if (WorldCollision.FreeCircle(position, prefab.Radius))
            {
                var animal=Instantiate(prefab, position, Quaternion.identity);
                animal.ConfigureSpawnMass(prefab.MassKg*MassMultiplier);return animal;
            }
        }
        return null;
    }
    public override bool TryUpgrade(int index)
    {
        if (owner == null || index != 0 || !owner.TrySpendGold(upgradeCost)) return false;
        spawnInterval = Mathf.Max(minimumInterval, spawnInterval * intervalMultiplier);
        Level++;
        remaining = System.Math.Min(remaining, spawnInterval);
        upgradeCost = NextCost(upgradeCost);
        return true;
    }
}
