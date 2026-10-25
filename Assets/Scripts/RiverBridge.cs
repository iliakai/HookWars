using System.Collections.Generic;
using UnityEngine;

public sealed class RiverBridge : MonoBehaviour
{
    private static readonly List<RiverBridge> bridges=new List<RiverBridge>();
    public static IReadOnlyList<RiverBridge> All=>bridges;
    public const float HalfLength=3,PassageWidth=1.4f;
    public PlayerProgression Owner {get;private set;}
    public int Stage {get;private set;}
    public bool Complete=>Stage==3;
    public string NextCost=>Stage==0?"Кости: 500 кг":Stage==1?"Золото: 10 000":Stage==2?"Готовое мясо: 500 кг":"Мост готов — проход открыт всем";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()=>bridges.Clear();
    private void OnEnable(){if(!bridges.Contains(this))bridges.Add(this);}
    private void OnDisable()=>bridges.Remove(this);
    public void Initialize(PlayerProgression owner){Owner=owner;Stage=0;}
    public bool CanAfford=>Owner!=null&&Stage<3&&(Stage==0?Owner.GetComponent<OwnerEconomy>().BoneKg>=500:Stage==1?Owner.Gold>=10000:Owner.GetComponent<OwnerEconomy>().ProductKg>=500);
    public bool TryBuildNext(PlayerProgression requester)
    {
        if(requester==null||requester!=Owner||Stage>=3||!isActiveAndEnabled||MatchPause.BlocksGameplay||requester.GetComponent<Health>()?.IsDead==true||requester.GetComponent<HookVictim>()?.IsCaught==true)return false;
        if(!Owner.GetComponent<OwnerEconomy>().TrySpendResources(Stage==1?10000:0,Stage==2?500:0,Stage==0?500:0))return false;
        Stage++;return true;
    }
    public bool Contains(Vector2 point,float radius)=>Complete&&Mathf.Abs(point.x)<=HalfLength+.05f&&Mathf.Abs(point.y-transform.position.y)<=PassageWidth*.5f-radius+.0001f;
    public static bool AnyComplete=>bridges.Exists(b=>b!=null&&b.Complete);
    public static bool OwnComplete(PlayerProgression owner)=>bridges.Exists(b=>b!=null&&b.Owner==owner&&b.Complete);
    public static bool Walkable(Vector2 point,float radius)
    {
        if(Mathf.Abs(point.y)>ArenaLayout.FieldHeight*.5f-radius||Mathf.Abs(point.x)>ArenaLayout.OuterEdge-radius)return false;
        if(Mathf.Abs(point.x)>=ArenaLayout.RiverHalfWidth+radius)return true;
        foreach(var bridge in bridges)if(bridge!=null&&bridge.Contains(point,radius))return true;
        return false;
    }
    public static bool Reserved(Vector2 point,float radius)
    {
        // Keep both entrances and the visible bun clear before construction too.
        foreach(var bridge in bridges)if(bridge!=null&&Mathf.Abs(point.x)<HalfLength+1.2f+radius&&Mathf.Abs(point.y-bridge.transform.position.y)<1.8f+radius)return true;
        return false;
    }
    public static float Height(Vector2 point)
    {
        foreach(var bridge in bridges)
            if(bridge!=null&&bridge.Complete&&Mathf.Abs(point.x)<HalfLength&&Mathf.Abs(point.y-bridge.transform.position.y)<PassageWidth*.5f+.3f)
                return .95f*(1+Mathf.Cos(Mathf.PI*point.x/HalfLength))*6/14;
        return 0;
    }
    public static Vector3 ClampStep(Vector3 from,Vector3 to,float radius)
    {
        if(Walkable(to,radius)&&WalkNavigation25D.Segment(from,to,radius))return to;
        var horizontal=new Vector3(to.x,from.y,0);if(Walkable(horizontal,radius)&&WalkNavigation25D.Segment(from,horizontal,radius))return horizontal;
        var vertical=new Vector3(from.x,to.y,0);if(Walkable(vertical,radius)&&WalkNavigation25D.Segment(from,vertical,radius))return vertical;
        return from;
    }
}
