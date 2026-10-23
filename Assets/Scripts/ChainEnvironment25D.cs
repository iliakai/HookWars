using System.Collections.Generic;
using UnityEngine;

// Chain-only 3D collision proxies. The XY gameplay shapes remain authoritative.
[DefaultExecutionOrder(900)]
public sealed class ChainEnvironment25D : MonoBehaviour
{
    private readonly Dictionary<Collider2D,BoxCollider> proxies=new Dictionary<Collider2D,BoxCollider>();
    private readonly List<Collider2D> removed=new List<Collider2D>();
    private BoxCollider ground;
    private GameObject root;
    private void Start()
    {
        if(ChainPhysicsWorld25D.Instance==null)gameObject.AddComponent<ChainPhysicsWorld25D>();
        root=new GameObject("Chain collision proxies");ChainPhysicsWorld25D.Instance.Own(root);
        ground=Create("Ground");Refresh();
    }
    private BoxCollider Create(string label)
    {
        var go=new GameObject(label);go.layer=25;go.transform.SetParent(root.transform,false);
        var box=go.AddComponent<BoxCollider>();box.excludeLayers=~(1<<24);box.includeLayers=1<<24;return box;
    }
    private void FixedUpdate()=>Refresh();
    public void Refresh()
    {
        if(root==null)return;
        ground.transform.position=new Vector3(0,-.15f,0);ground.size=new Vector3(2*(ArenaLayout.OuterEdge+ArenaLayout.MountainWidth),.3f,ArenaLayout.FieldHeight);
        foreach(var shape in FindObjectsByType<Collider2D>(FindObjectsInactive.Include))
        {
            if(shape.gameObject.scene!=gameObject.scene)continue;
            bool mountain=shape.gameObject.layer==LayerMask.NameToLayer("Mountains");
            if(!mountain&&shape.GetComponent<GameBuilding>()==null)continue;
            if(!proxies.TryGetValue(shape,out var box)){box=Create(shape.name+" chain proxy");proxies.Add(shape,box);}
            box.enabled=shape.enabled&&shape.gameObject.activeInHierarchy&&!shape.isTrigger;
            if(!box.enabled)continue;
            Bounds b=shape.bounds;float height=shape.GetComponent<PermanentRock>()?.CollisionHeight??1.5f;
            if(!mountain)
            {
                var display=GetComponent<ArenaPresentation25D>();var model=display.ModelFor(shape.GetComponent<GameBuilding>());
                var building=shape.GetComponent<GameBuilding>();
                height=(model!=null?(model.GetComponent<BuildingPresentation25D>()?.CollisionHeight??1):1)*Mathf.Max(building.Footprint.x,building.Footprint.y);
            }
            box.transform.position=ArenaSpace25D.ToWorld(b.center,height*.5f);box.size=new Vector3(b.size.x,height,b.size.y);
        }
        removed.Clear();foreach(var pair in proxies)if(pair.Key==null){Destroy(pair.Value.gameObject);removed.Add(pair.Key);}
        foreach(var key in removed)proxies.Remove(key);
    }
    private void OnDisable(){if(root!=null)root.SetActive(false);}
    private void OnEnable(){if(root!=null)root.SetActive(true);}
    private void OnDestroy(){if(root!=null)Destroy(root);}
}
