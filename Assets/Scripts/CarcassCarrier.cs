using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(HookVictim))]
[DisallowMultipleComponent]
public class CarcassCarrier : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float capacityKg = 10f;
    [SerializeField, Min(0.01f)] private float capacityPerUpgrade = 2f;
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.25f;
    [SerializeField] private LayerMask carcassLayers;

    private readonly List<Carcass> cargo = new List<Carcass>();
    private readonly List<Carcass> deathCargo = new List<Carcass>();
    private Health health;
    private HookVictim victim;
    public void ConfigureHelper(float capacity){capacityKg=capacity;pickupRadius=.8f;carcassLayers=LayerMask.GetMask("Carcasses");}
    public void SetHelperCapacity(float capacity){if(GetComponent<ButcherHelper>()!=null)capacityKg=Mathf.Max(capacityKg,capacity);}
    public float CapacityKg => capacityKg;
    public float CapacityPerUpgrade => capacityPerUpgrade;
    public int Count => cargo.Count;
    public IReadOnlyList<Carcass> StoredCargo=>cargo;
    public float CurrentMassKg
    {
        get
        {
            float mass = 0f;
            foreach (Carcass carcass in cargo)
                if (carcass != null) mass += carcass.MassKg;
            return mass;
        }
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        victim = GetComponent<HookVictim>();
        health.Died += OnDied;
        health.Revived += OnRevived;
    }

    private void Update()
    {
        if (!CanCollect()) return;
        Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, pickupRadius, carcassLayers))
        {
            Carcass carcass = hit.GetComponent<Carcass>();
            if (carcass != null) TryCollect(carcass);
        }
    }

    private bool CanCollect() => !MatchPause.BlocksGameplay&&health != null && !health.IsDead && !victim.IsCaught;

    public bool TryCollect(Carcass carcass)
    {
        if (!CanCollect() || carcass == null || !carcass.IsAvailable) return false;
        float free = Mathf.Max(0, capacityKg - CurrentMassKg);
        if (free < .0001f) return false;
        var portion = carcass.TakePortion(Mathf.Min(free, carcass.MassKg));
        if (portion == null || !portion.TryStore(this)) return false;
        cargo.Add(portion);
        return true;
    }

    public bool TryUpgradeCapacity(PlayerProgression progression)
    {
        if (!CanCollect() || progression == null || progression.gameObject != gameObject ||
            capacityPerUpgrade <= 0f || float.IsNaN(capacityPerUpgrade) ||
            float.IsInfinity(capacityKg + capacityPerUpgrade)) return false;
        if (!progression.TrySpendUpgradePoint()) return false;
        capacityKg += capacityPerUpgrade;
        return true;
    }

    private void OnDied(Health target)
    {
        deathCargo.AddRange(cargo);
        DropAll();
        // Damage is applied before a lethal hook captures the hero. Keep the link
        // now so the whole drop is reserved as soon as that capture begins.
        foreach (Carcass carcass in deathCargo)
            if (carcass != null) carcass.FollowHero(victim);
    }

    public double DeliverTo(SausageFactory factory)
    {
        var helper=GetComponent<ButcherHelper>();var economicOwner=helper!=null?helper.Factory?.Owner:GetComponent<PlayerProgression>();
        if (!CanCollect() || factory == null || factory.Owner != economicOwner) return 0;
        double delivered = 0;
        for (int i = 0; i < cargo.Count;)
        {
            Carcass carcass = cargo[i];
            float amount = carcass != null ? (float)System.Math.Min(carcass.MassKg, factory.FreeRawKg) : 0;
            if (amount < .0001f || !factory.TryAcceptMass(amount)) { i++; continue; }
            delivered += amount;
            carcass.ConsumeMass(amount);
            if (carcass.MassKg > .0001f) { i++; continue; }
            cargo.RemoveAt(i); Destroy(carcass.gameObject);
        }
        return delivered;
    }

    private void OnRevived(Health target)
    {
        foreach (Carcass carcass in deathCargo)
            if (carcass != null && carcass.IsLinkedTo(victim)) carcass.DetachFromHero();
        deathCargo.Clear();
    }

    internal void ReleaseDeathCargo(Vector3 position, CarcassCarrier recipient = null)
    {
        foreach (Carcass carcass in deathCargo)
        {
            // A normally dropped item may have already been collected independently.
            if (carcass == null || !carcass.IsLinkedTo(victim)) continue;
            carcass.ReleaseFromHero(position);
            if (recipient != null) recipient.TryCollect(carcass);
        }
        deathCargo.Clear();
    }

    public void DropAll()
    {
        Vector3 ground = Carcass.SafeGroundPosition(transform.position);
        foreach (Carcass carcass in cargo)
            if (carcass != null) carcass.Drop(ground);
        cargo.Clear();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
            health.Revived -= OnRevived;
        }
    }
}
