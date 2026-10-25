using System;
using UnityEngine;

public enum BuildingKind { Factory, Shop, Farm, Wall, Barracks }

public abstract class GameBuilding : MonoBehaviour
{
    [SerializeField] protected PlayerProgression owner;
    [SerializeField, Min(1)] private int buildCost = 75;
    [SerializeField, Min(1)] private float upgradeCostGrowth = 1.5f;
    public PlayerProgression Owner => owner;
    public int BuildCost => buildCost;
    public bool IsOperating {get;private set;}=true;
    public float MaximumHP=>this is BoneWall wall?wall.MaxHealth:GetComponent<BuildingDurability>()?.MaximumHP??0;
    public float CurrentHP=>this is BoneWall wall?wall.CurrentHealth:GetComponent<BuildingDurability>()?.CurrentHP??0;
    public bool SetOperating(PlayerProgression requester,bool active)
    {
        if(requester!=owner||requester==null||MatchPause.BlocksGameplay||requester.GetComponent<Health>()?.IsDead==true||requester.GetComponent<HookVictim>()?.IsCaught==true)return false;
        IsOperating=active;return true;
    }
    public bool TakeDamage(PlayerProgression attacker,float damage)
    {
        if(attacker==null||attacker==owner||owner==null||MatchPause.BlocksGameplay||!isActiveAndEnabled||!float.IsFinite(damage)||damage<=0)return false;
        if(this is BoneWall wall)return wall.TakeHookDamage(attacker,damage);
        return GetComponent<BuildingDurability>()?.Hit(damage)??false;
    }
    public Vector2 ClosestPoint(Vector2 from)=>GetComponent<Collider2D>()?.ClosestPoint(from)??(Vector2)transform.position;
    public virtual Vector2Int Footprint=>Vector2Int.one;
    public abstract BuildingKind Kind { get; }
    public abstract string DisplayName { get; }
    public virtual int UpgradeCount => 1;
    public abstract string Info { get; }
    public abstract string UpgradeLabel(int index);
    public abstract bool TryUpgrade(int index);
    public bool Demolish(PlayerProgression requester)
    {
        if (requester == null || requester != owner || Kind == BuildingKind.Factory || MatchPause.BlocksGameplay ||
            requester.GetComponent<Health>()?.IsDead == true || requester.GetComponent<HookVictim>()?.IsCaught == true) return false;
        foreach(var collider in GetComponentsInChildren<Collider2D>())collider.enabled=false;
        gameObject.SetActive(false); Destroy(gameObject); return true;
    }
    public virtual bool CanBuild(PlayerProgression candidate) => candidate != null;
    public virtual bool Initialize(PlayerProgression candidate)
    {
        if (owner != null || !CanBuild(candidate)) return false;
        owner = candidate;
        if(!(this is BoneWall)&&GetComponent<BuildingDurability>()==null)gameObject.AddComponent<BuildingDurability>();
        return true;
    }
    protected int NextCost(int cost) => (int)Math.Min(int.MaxValue, Math.Ceiling(cost * (double)upgradeCostGrowth));
}
