using System.Collections.Generic;
using HookWars.Chain;
using UnityEngine;

public sealed class HookChain25D : MonoBehaviour
{
    private readonly List<PhysicalChain> spans=new List<PhysicalChain>();
    private HookAbility owner;
    private Transform throwIdentity;
    private Mesh mesh; private Material material;
    public float MaximumGameLength {get;private set;}
    public float IssuedPhysicalLength {get;private set;}
    public float LogicalLength {get;private set;}
    public int ActiveLinks {get;private set;}
    public int PooledLinks {get{int n=0;foreach(var span in spans)n+=span.PooledCount;return n;}}
    public IReadOnlyList<PhysicalChain> Spans=>spans;
    [Min(.1f)] public float linkScale=1;
    [Min(.1f)] public float thickness=1;
    [Range(0,.2f)] public float slackRatio=.025f;
    public void Initialize(HookAbility source,Mesh linkMesh,Material steel){owner=source;mesh=linkMesh;material=steel;}
    public void Present(ButcherVisual25D butcher,Vector3 tip)
    {
        if(owner==null||owner.ActiveHook==null||butcher==null||!owner.isActiveAndEnabled)
        {Clear();return;}
        if(throwIdentity!=owner.ActiveHook)
        {Clear();throwIdentity=owner.ActiveHook;MaximumGameLength=owner.Range;}
        int required=owner.ChainPointCount-1;
        while(spans.Count<required)
        {
            var root=new GameObject("Physical span "+spans.Count);root.transform.SetParent(transform,false);
            var chain=root.AddComponent<PhysicalChain>();chain.buildOnStart=false;chain.physicsLayer=24;chain.environmentLayer=25;
            chain.linkMesh=mesh;chain.material=material;chain.solverIterations=128;chain.massPerLink=.035f;spans.Add(chain);
            root.AddComponent<ChainProbe>().chain=chain;
        }
        IssuedPhysicalLength=LogicalLength=0;ActiveLinks=0;
        Vector3 previous=butcher.Socket.position;
        for(int i=0;i<required;i++)
        {
            Vector3 next=i==required-1?tip:ArenaSpace25D.ToWorld(owner.GetChainPoint(i+1),.65f);
            // Socket height/lateral offset add physical cable only; they never
            // change the XY hit point or maximum gameplay range.
            float chord=Vector3.Distance(previous,next);
            float length=chord*(1+slackRatio)+Mathf.Min(.045f,chord*.1f);
            var chain=spans[i];chain.linkScale=linkScale;chain.visualThickness=thickness;
            chain.maximumLinkVelocity=Mathf.Max(35,owner.Speed*3,(owner.GetComponent<PlayerMovement>()?.MoveSpeed??0)*3);
            chain.SetStreamingEndpoints(previous,next,length);
            IssuedPhysicalLength+=length;ActiveLinks+=chain.ActiveBodyCount;
            LogicalLength+=Vector3.Distance(owner.GetChainPoint(i),owner.GetChainPoint(i+1));
            previous=next;
        }
        for(int i=required;i<spans.Count;i++)spans[i].StopStreaming();
    }
    public void Clear()
    {
        foreach(var span in spans)if(span!=null)span.StopStreaming();
        throwIdentity=null;IssuedPhysicalLength=LogicalLength=0;ActiveLinks=0;
    }
    private void OnDisable()=>Clear();
    private void LateUpdate(){if(owner==null){Destroy(gameObject);return;}if(owner.ActiveHook==null||!owner.isActiveAndEnabled)Clear();}
}
