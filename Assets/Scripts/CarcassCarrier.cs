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
    [SerializeField] private SpriteRenderer cargoMarker;

    private readonly List<Carcass> cargo = new List<Carcass>();
    private Health health;
    private HookVictim victim;
    public float CapacityKg => capacityKg;
    public float CapacityPerUpgrade => capacityPerUpgrade;
    public int Count => cargo.Count;
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
        RefreshMarker();
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

    private bool CanCollect() => health != null && !health.IsDead && !victim.IsCaught;

    public bool TryCollect(Carcass carcass)
    {
        if (!CanCollect() || carcass == null || !carcass.IsAvailable ||
            CurrentMassKg + carcass.MassKg > capacityKg) return false;
        if (!carcass.TryStore(this)) return false;
        cargo.Add(carcass);
        RefreshMarker();
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

    private void OnDied(Health target) => DropAll();

    public void DropAll()
    {
        Vector3 ground = Carcass.SafeGroundPosition(transform.position);
        foreach (Carcass carcass in cargo)
            if (carcass != null) carcass.Drop(ground);
        cargo.Clear();
        RefreshMarker();
    }

    private void RefreshMarker()
    {
        if (cargoMarker != null) cargoMarker.enabled = cargo.Count > 0;
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }
}
