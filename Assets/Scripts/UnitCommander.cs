using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public sealed class UnitCommander : MonoBehaviour
{
    private readonly List<SausageWarrior> selected=new List<SausageWarrior>();
    private PlayerProgression owner;
    private bool holding;
    private Vector2 down,current;
    private GameObject orderMarker;
    private Mesh markerMesh;
    private Material markerMaterial;
    private float markerRemaining;
    public bool HasOrderMarker=>orderMarker!=null&&orderMarker.activeSelf;
    public bool LastOrderIsAttack {get;private set;}
    public Vector3 LastOrderPosition {get;private set;}
    public bool HasSelection {get{selected.RemoveAll(w=>w==null||w.GetComponent<Health>().IsDead);return selected.Count>0;}}
    public int SelectionCount=>selected.Count;
    public bool ConsumedRightClick {get;private set;}
    public Rect DragRect=>Rect.MinMaxRect(Mathf.Min(down.x,current.x),Mathf.Min(down.y,current.y),Mathf.Max(down.x,current.x),Mathf.Max(down.y,current.y));
    private void Awake()=>owner=GetComponent<PlayerProgression>();
    public void ClearSelection(){foreach(var unit in selected)if(unit!=null)unit.IsSelected=false;selected.Clear();}
    public void SelectUnits(Rect screenRect)
    {
        ClearSelection();var camera=Camera.main;if(camera==null)return;
        foreach(var unit in FindObjectsByType<SausageWarrior>())
            if(unit.Owner==owner&&!unit.GetComponent<Health>().IsDead&&screenRect.Contains(camera.WorldToScreenPoint(ArenaSpace25D.ToWorld(unit.transform.position,RiverBridge.Height(unit.transform.position)))))
            {selected.Add(unit);unit.IsSelected=true;}
    }
    public bool GiveOrder(Vector3 point,Component enemy=null)
    {
        if(MatchPause.BlocksGameplay||!HasSelection)return false;
        int index=0;bool accepted=false;int width=Mathf.CeilToInt(Mathf.Sqrt(selected.Count));
        foreach(var unit in selected)
        {
            if(unit==null)continue;
            if(enemy!=null)accepted|=unit.CommandAttack(enemy);
            else
            {
                Vector3 offset=new Vector3((index%width-(width-1)*.5f)*.55f,(index/width-(width-1)*.5f)*.55f);
                var goal=point+offset;if(!RiverBridge.Walkable(goal,SausageWarrior.BodyRadius))goal=point;
                accepted|=unit.CommandMove(goal);index++;
            }
        }
        if(accepted)ShowOrder(point,enemy!=null);
        return accepted;
    }
    private void ShowOrder(Vector3 point,bool attack)
    {
        LastOrderPosition=point;LastOrderIsAttack=attack;
        if(orderMarker==null)
        {
            var template=FindAnyObjectByType<ArenaPresentation25D>()?.OverlayTemplate;if(template==null)return;
            const int segments=48;var vertices=new Vector3[segments*2];var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=direction*.43f;vertices[i*2+1]=direction*.55f;
                int n=(i+1)%segments*2,k=i*6;triangles[k]=i*2;triangles[k+1]=n;triangles[k+2]=i*2+1;
                triangles[k+3]=n;triangles[k+4]=n+1;triangles[k+5]=i*2+1;
            }
            markerMesh=new Mesh{name="Order destination ring"};markerMesh.vertices=vertices;markerMesh.triangles=triangles;markerMesh.RecalculateBounds();
            markerMaterial=new Material(template){name="Order destination",renderQueue=3020};
            orderMarker=new GameObject("Order destination");orderMarker.AddComponent<MeshFilter>().sharedMesh=markerMesh;
            var renderer=orderMarker.AddComponent<MeshRenderer>();renderer.sharedMaterial=markerMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        markerRemaining=.7f;orderMarker.transform.position=ArenaSpace25D.ToWorld(point,RiverBridge.Height(point)+.06f);orderMarker.SetActive(true);AnimateMarker();
    }
    private void AnimateMarker()
    {
        if(!HasOrderMarker)return;
        float progress=1-markerRemaining/.7f;orderMarker.transform.localScale=Vector3.one*(.8f+progress*.6f);
        Color color=LastOrderIsAttack?new Color(1,.12f,.08f):new Color(.15f,1,.25f);
        color.a=Mathf.Clamp01(markerRemaining/.2f)*(.65f+.35f*Mathf.Sin(progress*Mathf.PI));markerMaterial.SetColor("_BaseColor",color);
        if(markerRemaining<=0)orderMarker.SetActive(false);
    }
    private void Update()
    {
        ConsumedRightClick=false;
        if(MatchPause.BlocksGameplay){holding=false;return;}
        markerRemaining-=Time.deltaTime;AnimateMarker();
        if(Keyboard.current?.qKey.wasPressedThisFrame==true)ClearSelection();
        var mouse=Mouse.current;if(mouse==null||Camera.main==null)return;
        current=mouse.position.ReadValue();
        if(GetComponent<PlayerBuilding>().IsBuilding){holding=false;return;}
        var menu=GetComponent<BuildingUpgradeMenu>();Vector2 gui=new Vector2(current.x,Screen.height-current.y);
        bool overUI=GamePointer.OverInterface(current)||menu.Contains(gui);
        if(mouse.leftButton.wasPressedThisFrame&&!overUI){holding=true;down=current;}
        if(holding&&mouse.leftButton.wasReleasedThisFrame)
        {
            holding=false;
            if(overUI)return;
            if(Vector2.Distance(down,current)>8){SelectUnits(DragRect);return;}
            SausageWarrior closest=null;float best=15*15;
            foreach(var unit in FindObjectsByType<SausageWarrior>())
            {
                if(unit.Owner!=owner||unit.GetComponent<Health>().IsDead)continue;
                Vector2 screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(unit.transform.position,RiverBridge.Height(unit.transform.position)+.45f*unit.Stats.BodyScale/1.5f));
                float distance=(screen-current).sqrMagnitude;if(distance<best){best=distance;closest=unit;}
            }
            ClearSelection();
            if(closest!=null){selected.Add(closest);closest.IsSelected=true;menu.CloseInterface();return;}
            if(menu.SelectAtScreenPoint(current))return;
            if(GamePointer.TryLogicalPoint(Camera.main,current,out var point))
            {
                // Enemy buildings consume selection clicks too; only right-click attacks.
                if(Physics2D.OverlapPoint(point,LayerMask.GetMask("Buildings"))?.GetComponent<GameBuilding>()!=null)return;
                GetComponent<PlayerHook>().TryThrowAtScreenPoint(current);
            }
        }
        if(mouse.rightButton.wasPressedThisFrame&&HasSelection&&!overUI)
        {
            ConsumedRightClick=true;
            if(!GamePointer.TryLogicalPoint(Camera.main,current,out var point))return;
            Component enemy=null;
            foreach(var hit in Physics2D.OverlapPointAll(point,WorldCollision.WalkingLayers))
            {
                var h=hit.GetComponent<Health>();var b=hit.GetComponent<GameBuilding>();
                if(h!=null&&CombatOwnership.OwnerOf(h)!=owner){enemy=h;break;}
                if(b!=null&&b.Owner!=owner){enemy=b;break;}
            }
            GiveOrder(point,enemy);
        }
    }
    private void OnGUI()
    {
        if(!holding||Vector2.Distance(down,current)<=8)return;
        Rect rect=DragRect;rect.y=Screen.height-rect.yMax;
        var color=GUI.color;GUI.color=new Color(.25f,1,.45f,.18f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=new Color(.25f,1,.45f,.9f);
        GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.x,rect.yMax-1,rect.width,1),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x,rect.y,1,rect.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(rect.xMax-1,rect.y,1,rect.height),Texture2D.whiteTexture);GUI.color=color;
    }
    private void OnDisable(){holding=false;ClearSelection();if(orderMarker!=null)orderMarker.SetActive(false);}
    private void OnDestroy(){if(orderMarker!=null)Destroy(orderMarker);if(markerMesh!=null)Destroy(markerMesh);if(markerMaterial!=null)Destroy(markerMaterial);}
}
