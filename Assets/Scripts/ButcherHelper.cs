using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(Health),typeof(HookVictim),typeof(CarcassCarrier))]
public sealed class ButcherHelper : MonoBehaviour
{
    [SerializeField,Min(.1f)] private float moveSpeed=3;
    [SerializeField,Min(.1f)] private float attackRange=.9f,attackCooldown=1;
    private static readonly Dictionary<Animal,ButcherHelper> claims=new Dictionary<Animal,ButcherHelper>();
    private readonly List<Vector3> path=new List<Vector3>();
    private Animal target;
    private Carcass loose;
    private static readonly Vector2Int[] directions={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
    private Health health;
    private HookVictim victim;
    private CarcassCarrier carrier;
    private HeroActionTiming25D action;
    private float nextAttack,nextSearch,nextPath;
    private Vector3 pathGoal;
    public SausageFactory Factory {get;private set;}
    public Vector2 AimDirection {get;private set;}=Vector2.up;
    public float MoveSpeed=>moveSpeed;
    public float Radius=>.25f;
    public bool Returning=>carrier!=null&&carrier.Count>0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetClaims()=>claims.Clear();
    private void Awake()
    {
        health=GetComponent<Health>();victim=GetComponent<HookVictim>();carrier=GetComponent<CarcassCarrier>();action=GetComponent<HeroActionTiming25D>();
        health.Died+=Died;
    }
    public void Bind(SausageFactory factory,float capacity)
    {
        Factory=factory;gameObject.layer=LayerMask.NameToLayer(factory.Owner.GetComponent<PlayerMovement>()!=null?"PlayerTarget":"HookTarget");
        carrier.ConfigureHelper(capacity);GetComponent<CircleCollider2D>().radius=Radius;
    }
    private bool SameBank(Vector3 p)=>(p.x>0)==(Factory.transform.position.x>0);
    private void Update()
    {
        if(Time.deltaTime<=0||Factory==null||health.IsDead||victim.IsCaught)return;
        if(target==null)ReleaseTarget();
        if(Returning)
        {
            ReleaseTarget();
            if(Factory.InDeliveryRange(transform.position)){carrier.DeliverTo(Factory);path.Clear();return;}
            WalkTo(Factory.transform.position,1.95f);return;
        }
        if(target!=null&&(target.IsDead||target.GetComponent<HookVictim>().IsCaught||!SameBank(target.transform.position)))ReleaseTarget();
        if(loose!=null&&(!loose.IsAvailable||!SameBank(loose.transform.position)))loose=null;
        if(loose!=null)
        {WalkTo(loose.transform.position,.5f);if(Vector2.Distance(transform.position,loose.transform.position)<.8f)carrier.TryCollect(loose);return;}
        if(target==null&&Time.time>=nextSearch)
        {
            nextSearch=Time.time+.5f;float best=float.PositiveInfinity;
            // Prefer already available animal corpses, without duplicating mass.
            foreach(var corpse in FindObjectsByType<Carcass>())
                if(corpse.IsAvailable&&SameBank(corpse.transform.position))
                {loose=corpse;return;}
            foreach(var animal in FindObjectsByType<Animal>())
            {
                if(animal.IsDead||animal.GetComponent<HookVictim>().IsCaught||!SameBank(animal.transform.position))continue;
                if(claims.TryGetValue(animal,out var helper)&&helper!=null&&helper!=this&&!helper.health.IsDead)continue;
                float distance=(animal.transform.position-transform.position).sqrMagnitude;
                if(distance<best){target=animal;best=distance;}
            }
            if(target!=null)claims[target]=this;
        }
        if(target==null)return;
        Vector2 direction=target.transform.position-transform.position;if(direction.sqrMagnitude>.0001f)AimDirection=direction.normalized;
        if(direction.magnitude>attackRange){WalkTo(target.transform.position,attackRange*.8f);return;}
        path.Clear();
        if(Time.time<nextAttack||action==null)return;
        Animal accepted=target;
        if(action.BeginAxe(attackCooldown,()=>
        {
            if(health.IsDead||victim.IsCaught||accepted==null||accepted.IsDead||accepted.GetComponent<HookVictim>().IsCaught||
                Vector2.Distance(transform.position,accepted.transform.position)>attackRange)return;
            var corpse=accepted.Kill();if(corpse!=null)carrier.TryCollect(corpse);ReleaseTarget();
        }))nextAttack=Time.time+attackCooldown;
    }
    private void WalkTo(Vector3 goal,float arrival)
    {
        if(Time.time>=nextPath||(goal-pathGoal).sqrMagnitude>.5f)
        {pathGoal=goal;nextPath=Time.time+.5f;BuildPath(goal,arrival);}
        if(path.Count==0)return;
        while(path.Count>1&&Vector2.Distance(transform.position,path[0])<.08f)path.RemoveAt(0);
        Vector3 before=transform.position;Vector3 next=Vector3.MoveTowards(before,path[0],moveSpeed*Time.deltaTime);
        next=WorldCollision.Slide(transform,next,Radius);next=RiverBridge.ClampStep(before,next,Radius);
        Vector2 travel=next-before;if(travel.sqrMagnitude>.000001f)AimDirection=travel.normalized;
        transform.position=next;Physics2D.SyncTransforms();
        if(Vector2.Distance(next,path[0])<.04f)path.RemoveAt(0);
    }
    private void BuildPath(Vector3 goal,float arrival)
    {
        if(RiverBridge.AnyComplete){WalkNavigation25D.Build(transform,goal,Radius,arrival,path);return;}
        path.Clear();Rect bank=ArenaLayout.BankBounds(Factory.transform.position.x>0,Radius+.02f);
        const float step=.5f;int width=Mathf.FloorToInt(bank.width/step)+1,height=Mathf.FloorToInt(bank.height/step)+1;
        int sx=Mathf.Clamp(Mathf.RoundToInt((transform.position.x-bank.xMin)/step),0,width-1),sy=Mathf.Clamp(Mathf.RoundToInt((transform.position.y-bank.yMin)/step),0,height-1);
        int start=sy*width+sx,found=-1;var previous=new int[width*height];for(int i=0;i<previous.Length;i++)previous[i]=-2;
        var queue=new Queue<int>();queue.Enqueue(start);previous[start]=-1;
        int mask=LayerMask.GetMask("Buildings","Mountains","HookTarget","PlayerTarget","Animals");
        Vector3 Point(int i)=>new Vector3(bank.xMin+(i%width)*step,bank.yMin+(i/width)*step,0);
        while(queue.Count>0)
        {
            int i=queue.Dequeue();if(Vector2.Distance(Point(i),goal)<=arrival){found=i;break;}
            int x=i%width,y=i/width;
            foreach(var delta in directions)
            {
                int nx=x+delta.x,ny=y+delta.y;if(nx<0||ny<0||nx>=width||ny>=height)continue;
                int n=ny*width+nx;if(previous[n]!=-2)continue;
                bool blocked=false;
                foreach(var obstacle in Physics2D.OverlapCircleAll(Point(n),Radius+.015f,mask))
                {
                    if(obstacle.isTrigger||obstacle.transform==transform||obstacle.transform.IsChildOf(transform)||
                        (target!=null&&obstacle.GetComponent<Animal>()==target))continue;
                    blocked=true;break;
                }
                if(blocked)continue;
                previous[n]=i;queue.Enqueue(n);
            }
        }
        if(found<0)return;
        if(found==start){path.Add(Point(start));return;}
        for(int i=found;i!=start&&i>=0;i=previous[i])path.Add(Point(i));path.Reverse();
    }
    private void ReleaseTarget(){if(!ReferenceEquals(target,null)&&claims.TryGetValue(target,out var worker)&&worker==this)claims.Remove(target);target=null;}
    private void Died(Health _){ReleaseTarget();StartCoroutine(RemoveAfterPull());}
    private IEnumerator RemoveAfterPull(){yield return null;while(victim!=null&&victim.IsCaught)yield return null;Destroy(gameObject);}
    private void OnDisable(){ReleaseTarget();path.Clear();action?.CancelAll();}
    private void OnDestroy(){ReleaseTarget();if(health!=null)health.Died-=Died;}
}
