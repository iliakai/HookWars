using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Player-perspective visibility. No physics, target selection or persistent exploration.
[DefaultExecutionOrder(1700)]
public sealed class FogOfWar25D : MonoBehaviour
{
    private ArenaPresentation25D display;
    private PlayerBuilding viewer;
    private PlayerProgression owner;
    private Material material;
    private Mesh mesh;
    private Color[] colors;
    private Vector2[] points;
    private float depth;
    private float nextPaint;
    private readonly List<Vector2> eyes=new List<Vector2>();
    private readonly List<Vector2> routeStart=new List<Vector2>(),routeEnd=new List<Vector2>();
    private readonly Dictionary<GameObject,Renderer[]> renderers=new Dictionary<GameObject,Renderer[]>();
    private readonly Dictionary<Light,int> lightMasks=new Dictionary<Light,int>();
    private readonly List<GameObject> retired=new List<GameObject>();
    public const float HeroRadius=4,HookRadius=1;
    public void Bind(ArenaPresentation25D presentation,PlayerBuilding player,Material template)
    {
        display=presentation;viewer=player;owner=viewer.GetComponent<PlayerProgression>();
        material=new Material(template);material.SetFloat("_UseVertexColor",1);material.SetColor("_BaseColor",Color.white);material.renderQueue=3010;
        mesh=new Mesh{name="Enemy bank fog"};mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        BuildMesh();RefreshObservers();
    }
    private bool Friendly(Health h)=>h.GetComponent<PlayerProgression>()==owner||h.GetComponent<ButcherHelper>()?.Factory?.Owner==owner;
    public void RefreshObservers()
    {
        eyes.Clear();routeStart.Clear();routeEnd.Clear();
        foreach(var h in FindObjectsByType<Health>())
            if(!h.IsDead&&Friendly(h)&&Mathf.Abs(h.transform.position.x)>=ArenaLayout.RiverHalfWidth&&(h.transform.position.x>0)!=viewer.HomeRightBank)eyes.Add(h.transform.position);
        foreach(var hook in FindObjectsByType<HookAbility>())
        {
            if(hook.ActiveHook==null||hook.GetComponent<PlayerProgression>()!=owner)continue;
            for(int i=1;i<hook.ChainPointCount;i++){routeStart.Add(hook.GetChainPoint(i-1));routeEnd.Add(hook.GetChainPoint(i));}
        }
    }
    private float SignedDistance(Vector2 point)
    {
        float distance=float.PositiveInfinity;
        foreach(var eye in eyes)distance=Mathf.Min(distance,Vector2.Distance(eye,point)-HeroRadius);
        for(int i=0;i<routeStart.Count;i++)
        {
            Vector2 a=routeStart[i],d=routeEnd[i]-a;
            float t=d.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude):0;
            distance=Mathf.Min(distance,Vector2.Distance(a+d*t,point)-HookRadius);
        }
        return distance;
    }
    public bool IsRevealed(Vector2 point)=>Mathf.Abs(point.x)<=ArenaLayout.RiverHalfWidth||(point.x>0)==viewer.HomeRightBank||SignedDistance(point)<=0;
    private bool WorldVisible(Vector3 point)=>IsRevealed(ArenaSpace25D.ToLogical(point));
    private void BuildMesh()
    {
        depth=ArenaLayout.Depth;var bank=ArenaLayout.BankBounds(!viewer.HomeRightBank,0);
        var vertices=new List<Vector3>();var triangles=new List<int>();var logical=new List<Vector2>();
        const float step=.25f;
        for(float x=bank.xMin;x<bank.xMax-.001f;x+=step)for(float y=bank.yMin;y<bank.yMax-.001f;y+=step)
        {
            int n=vertices.Count;
            foreach(var p in new[]{new Vector2(x,y),new Vector2(x,y+step),new Vector2(x+step,y+step),new Vector2(x+step,y)})
            {logical.Add(p);vertices.Add(ArenaSpace25D.ToWorld(p,.025f));}
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        points=logical.ToArray();colors=new Color[points.Length];mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();nextPaint=0;
    }
    private void LateUpdate()
    {
        if(viewer==null||display==null)return;
        if(depth!=ArenaLayout.Depth)BuildMesh();
        RefreshObservers();
        if(Time.unscaledTime>=nextPaint)
        {
            nextPaint=Time.unscaledTime+.08f;
            for(int i=0;i<points.Length;i++)
            {
                float alpha=.78f*Mathf.SmoothStep(0,1,Mathf.Clamp01(SignedDistance(points[i])/.35f));
                colors[i]=new Color(.38f,.40f,.42f,alpha);
            }
            mesh.colors=colors;
        }
        foreach(var pair in display.Views)
        {
            if(pair.Key==null||pair.Value==null)continue;
            bool visible=IsRevealed(pair.Key.transform.position);
            if(!renderers.TryGetValue(pair.Value,out var list)||pair.Key is Health)
            {list=pair.Value.GetComponentsInChildren<Renderer>(true);renderers[pair.Value]=list;}
            foreach(var renderer in list)if(renderer!=null)renderer.forceRenderingOff=!visible;
            foreach(var light in pair.Value.GetComponentsInChildren<Light>(true))
            {
                if(!lightMasks.TryGetValue(light,out int mask)){mask=light.cullingMask;lightMasks.Add(light,mask);}
                light.cullingMask=visible?mask:0;
            }
        }
        foreach(var chain in FindObjectsByType<HookChain25D>())foreach(var span in chain.Spans)span.SetPresentationVisibility(WorldVisible);
        retired.Clear();foreach(var pair in renderers)if(pair.Key==null)retired.Add(pair.Key);foreach(var old in retired)renderers.Remove(old);
    }
    private void OnDisable()
    {
        foreach(var list in renderers.Values)foreach(var renderer in list)if(renderer!=null)renderer.forceRenderingOff=false;
        foreach(var pair in lightMasks)if(pair.Key!=null)pair.Key.cullingMask=pair.Value;
        foreach(var chain in FindObjectsByType<HookChain25D>())foreach(var span in chain.Spans)span.SetPresentationVisibility(_=>true);
    }
    private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
}
