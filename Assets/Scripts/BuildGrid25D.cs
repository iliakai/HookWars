using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Two batched translucent meshes, no per-cell GameObjects or colliders.
public sealed class BuildGrid25D : MonoBehaviour
{
    private PlayerBuilding builder;
    private readonly Mesh[] meshes=new Mesh[2];
    private readonly MeshRenderer[] renderers=new MeshRenderer[2];
    private readonly Material[] materials=new Material[2];
    private readonly List<Vector3>[] vertices={new List<Vector3>(),new List<Vector3>()};
    private readonly List<int>[] triangles={new List<int>(),new List<int>()};
    private float nextRefresh;
    public int VisibleCells {get;private set;}
    public void Bind(PlayerBuilding source)
    {
        builder=source;
        for(int i=0;i<2;i++)
        {
            var go=new GameObject(i==0?"Available cells":"Unavailable cells");go.transform.SetParent(transform,false);
            meshes[i]=new Mesh{name="Build grid cells"};meshes[i].MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=meshes[i];
            renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));materials[i]=m;
            m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
            m.SetColor("_BaseColor",i==0?new Color(.45f,1,.55f,.10f):new Color(1,.45f,.45f,.10f));renderers[i].sharedMaterial=m;
        }
    }
    private void LateUpdate()
    {
        bool show=builder!=null&&builder.IsBuilding&&!MatchPause.BlocksGameplay;
        foreach(var r in renderers)if(r!=null)r.enabled=show;
        if(!show){VisibleCells=0;nextRefresh=0;return;}
        if(Time.time<nextRefresh)return;nextRefresh=Time.time+.15f;
        Physics2D.SyncTransforms();VisibleCells=0;
        for(int i=0;i<2;i++){vertices[i].Clear();triangles[i].Clear();}
        var bank=ArenaLayout.BankBounds(builder.HomeRightBank,0);
        for(float x=bank.xMin+.5f;x<bank.xMax;x++)for(float y=bank.yMin+.5f;y<bank.yMax;y++)
        {
            var cell=new Vector3(x,y);int kind=builder.CanPlaceForGrid(cell)?0:1;
            var v=vertices[kind];var t=triangles[kind];int n=v.Count;
            v.Add(new Vector3(x-.485f,.012f,y-.485f));v.Add(new Vector3(x-.485f,.012f,y+.485f));
            v.Add(new Vector3(x+.485f,.012f,y+.485f));v.Add(new Vector3(x+.485f,.012f,y-.485f));
            t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);VisibleCells++;
        }
        for(int i=0;i<2;i++){meshes[i].Clear();meshes[i].SetVertices(vertices[i]);meshes[i].SetTriangles(triangles[i],0);meshes[i].RecalculateBounds();}
    }
    private void OnDestroy(){foreach(var m in meshes)if(m!=null)Destroy(m);foreach(var m in materials)if(m!=null)Destroy(m);}
}
