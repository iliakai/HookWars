using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingDurability : MonoBehaviour
{
    private GameBuilding building;
    private bool destroyed;
    public float MaximumHP {get;private set;}
    public float CurrentHP {get;private set;}
    private void Awake()
    {
        building=GetComponent<GameBuilding>();
        MaximumHP=building is SausageFactory?3000:building is Barracks?800:400;CurrentHP=MaximumHP;
    }
    internal bool Hit(float damage)
    {
        if(destroyed)return true;
        CurrentHP=Mathf.Max(0,CurrentHP-damage);
        if(CurrentHP>0)return false;
        destroyed=true;
        if(building is SausageFactory)MatchPause.Instance?.EndMatch(building.Owner);
        foreach(var collider in GetComponentsInChildren<Collider2D>())collider.enabled=false;
        gameObject.SetActive(false);Destroy(gameObject);return true;
    }
}
