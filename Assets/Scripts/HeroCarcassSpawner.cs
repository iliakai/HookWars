using UnityEngine;

[RequireComponent(typeof(Health), typeof(HookVictim))]
[DisallowMultipleComponent]
public class HeroCarcassSpawner : MonoBehaviour
{
    [SerializeField] private Carcass carcassPrefab;
    [SerializeField, Min(0.01f)] private float heroMassKg = 6f;
    private Health health;
    public Carcass CurrentCarcass { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        health.Died += OnDied;
        health.Revived += OnRevived;
    }

    private void OnDied(Health target)
    {
        if (carcassPrefab == null)
        {
            Debug.LogError("Назначь Carcass Prefab в Hero Carcass Spawner.", this);
            return;
        }
        CurrentCarcass = Instantiate(carcassPrefab,
            Carcass.SafeGroundPosition(transform.position), Quaternion.identity);
        CurrentCarcass.Initialize(heroMassKg, GetComponent<HookVictim>());
    }

    private void OnRevived(Health target)
    {
        // The hero may already be at its spawn point. Never move its old carcass there.
        if (CurrentCarcass != null) CurrentCarcass.DetachFromHero();
        CurrentCarcass = null;
    }

    private void OnDestroy()
    {
        if (health == null) return;
        health.Died -= OnDied;
        health.Revived -= OnRevived;
    }
}
