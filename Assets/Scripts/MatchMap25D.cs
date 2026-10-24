using System.Collections.Generic;
using UnityEngine;

// Configuration and ArenaLayout have run; owners have not started their economy.
[DefaultExecutionOrder(-700)]
public sealed class MatchMap25D : MonoBehaviour
{
    [SerializeField] private SausageFactory factoryPrefab;
    [SerializeField] private PermanentRock rockPrefab;
    [SerializeField] private HookAnchor anchorPrefab;
    private GameObject mapRoot;
    private readonly List<SausageFactory> factories=new List<SausageFactory>();
    public IReadOnlyList<SausageFactory> Factories=>factories;
    public Transform MapRoot=>mapRoot!=null?mapRoot.transform:null;
    private void Awake()=>Generate();
    public void Generate()
    {
        if(mapRoot!=null)return;
        if(factoryPrefab==null||rockPrefab==null)throw new System.InvalidOperationException("Assign factory and permanent rock prefabs");
        mapRoot=new GameObject("Match map");mapRoot.transform.SetParent(transform,false);
        foreach(var owner in FindObjectsByType<PlayerProgression>())
        {
            bool right=owner.transform.position.x>0;
            int min=Mathf.CeilToInt(Mathf.Max(ArenaLayout.RiverHalfWidth+1,ArenaLayout.RiverHalfWidth+ArenaLayout.Depth*.65f));
            int max=Mathf.FloorToInt(ArenaLayout.OuterEdge-1);
            if(min>max)min=max;
            Vector3 position=Vector3.zero;bool found=false;
            for(int attempt=0;attempt<80;attempt++)
            {
                position=new Vector3(Random.Range(min,max+1)*(right?1:-1),Random.Range(-8,9),0);
                Physics2D.SyncTransforms();
                if(WorldCollision.FreeBox(position,Vector2.one*2)){found=true;break;}
            }
            if(!found)throw new System.InvalidOperationException("Cannot place starting factory on its bank");
            var factory=Instantiate(factoryPrefab,position,Quaternion.identity,mapRoot.transform);
            if(!factory.Initialize(owner))throw new System.InvalidOperationException("Duplicate starting factory");
            factories.Add(factory);Physics2D.SyncTransforms();
        }
        foreach(bool right in new[]{false,true})
        {
            Rect bank=ArenaLayout.BankBounds(right,.5f);
            int desired=Mathf.Clamp(Mathf.FloorToInt(ArenaLayout.Depth/3),1,6);
            for(int n=0;n<desired;n++)
            {
                bool placed=false;
                for(int attempt=0;attempt<100;attempt++)
                {
                    Vector3 p=new Vector3(Random.Range(Mathf.CeilToInt(bank.xMin-.5f),Mathf.FloorToInt(bank.xMax-.5f)+1)+.5f,
                        Random.Range(-9,10)+.5f);
                    bool reserved=false;
                    foreach(var f in factories)
                    {
                        // Keep an approach lane from the river to the factory front.
                        if((f.transform.position.x>0)==right&&Mathf.Abs(p.y-f.transform.position.y)<2&&Mathf.Abs(p.x)<Mathf.Abs(f.transform.position.x)+2)reserved=true;
                    }
                    foreach(var hero in FindObjectsByType<PlayerProgression>())if(Vector2.Distance(hero.transform.position,p)<2)reserved=true;
                    if(reserved)continue;
                    if(!WorldCollision.FreeBox(p,Vector2.one))continue;
                    Instantiate(rockPrefab,p,Quaternion.Euler(0,0,90*Random.Range(0,4)),mapRoot.transform);Physics2D.SyncTransforms();placed=true;break;
                }
                if(!placed)Debug.LogWarning("Rock placement skipped after bounded attempts on "+(right?"right":"left")+" bank");
            }
        }
        if(anchorPrefab!=null)
        {
            bool firstRight=Random.value>.5f;
            for(int i=0;i<5;i++)
            {
                bool right=(i%2==0)==firstRight;Rect bank=ArenaLayout.BankBounds(right,.6f);bool found=false;
                for(int attempt=0;attempt<200;attempt++)
                {
                    Vector3 p=new Vector3(Random.Range(bank.xMin,bank.xMax),Random.Range(bank.yMin,bank.yMax));
                    if(!WorldCollision.FreeCircle(p,.45f))continue;
                    Instantiate(anchorPrefab,p,Quaternion.Euler(0,0,Random.Range(0,360)),mapRoot.transform);Physics2D.SyncTransforms();found=true;break;
                }
                if(!found)throw new System.InvalidOperationException("Cannot place five hook anchors on fresh map");
            }
        }
    }
}
