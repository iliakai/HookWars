using System;
using UnityEngine;

public enum BuildingKind { Factory, Shop, Farm, Wall }

public abstract class GameBuilding : MonoBehaviour
{
    [SerializeField] protected PlayerProgression owner;
    [SerializeField, Min(1)] private int buildCost = 75;
    [SerializeField, Min(1)] private float upgradeCostGrowth = 1.5f;
    public PlayerProgression Owner => owner;
    public int BuildCost => buildCost;
    public virtual Vector2Int Footprint=>Vector2Int.one;
    public abstract BuildingKind Kind { get; }
    public abstract string DisplayName { get; }
    public virtual int UpgradeCount => 1;
    public abstract string Info { get; }
    public abstract string UpgradeLabel(int index);
    public abstract bool TryUpgrade(int index);
    public virtual bool CanBuild(PlayerProgression candidate) => candidate != null;
    public virtual bool Initialize(PlayerProgression candidate)
    {
        if (owner != null || !CanBuild(candidate)) return false;
        owner = candidate;
        return true;
    }
    protected int NextCost(int cost) => (int)Math.Min(int.MaxValue, Math.Ceiling(cost * (double)upgradeCostGrowth));
}
