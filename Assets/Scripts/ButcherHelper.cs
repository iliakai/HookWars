using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(Health),typeof(HookVictim),typeof(CarcassCarrier))]
public sealed class ButcherHelper : MonoBehaviour
{
    [SerializeField,Min(.1f)] private float moveSpeed=4.5f;
    [SerializeField,Min(.1f)] private float attackRange=.9f,attackCooldown=1;
    private static readonly Dictionary<Component,ButcherHelper> claims=new Dictionary<Component,ButcherHelper>();
    private readonly Dictionary<Component,float> retryAfter=new Dictionary<Component,float>();
    private readonly List<Vector3> path=new List<Vector3>();
    private Animal target;
    private Carcass loose;
    private Health health;
    private HookVictim victim;
    private CarcassCarrier carrier;
    private HeroActionTiming25D action;
    private float nextAttack,nextSearch,nextPath,stalled;
    private Vector3 pathGoal,progressPoint;
    public int IdleRecoveries {get;private set;}
    public SausageFactory Factory {get;private set;}
    public Vector2 AimDirection {get;private set;}=Vector2.up;
    public float MoveSpeed=>moveSpeed;
    public float Radius=>.25f/1.5f;
    public bool Returning=>carrier!=null&&carrier.Count>0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetClaims()=>claims.Clear();
    private void Awake()
    {
        health=GetComponent<Health>();victim=GetComponent<HookVictim>();carrier=GetComponent<CarcassCarrier>();action=GetComponent<HeroActionTiming25D>();
        health.Died+=Died;victim.Captured+=Interrupted;
    }
    public void Bind(SausageFactory factory,float capacity)
    {
        Factory=factory;gameObject.layer=LayerMask.NameToLayer(factory.Owner.GetComponent<PlayerMovement>()!=null?"PlayerTarget":"HookTarget");
        carrier.ConfigureHelper(capacity);GetComponent<CircleCollider2D>().radius=Radius;progressPoint=transform.position;
    }
    private bool SameBank(Vector3 p)=>(p.x>0)==(Factory.transform.position.x>0);
    private void Update()
    {
        if(Time.deltaTime<=0||Factory==null||health.IsDead||victim.IsCaught)return;
        if(Returning)
        {
            ReleaseWork();
            if(Factory.InDeliveryRange(transform.position)){carrier.DeliverTo(Factory);path.Clear();stalled=0;return;}
            WalkTo(Factory.transform.position,1.95f,Factory.GetComponent<Collider2D>());return;
        }
        // A committed swing follows the animal through its ordinary movement.
        if(action!=null&&action.AxePending)
        {
            if(Time.time-action.AxeStarted<=HeroActionTiming25D.AxeImpact/action.AxeSpeed+.35f)return;
            action.CancelAxe();ReleaseWork();nextSearch=0;IdleRecoveries++;
        }
        if(target!=null&&(target.IsDead||target.GetComponent<HookVictim>().IsCaught||!SameBank(target.transform.position)))ReleaseWork();
        if(loose!=null&&(!loose.IsAvailable||!SameBank(loose.transform.position)))ReleaseWork();
        if(target==null&&loose==null&&Time.time>=nextSearch)Search();
        if(loose!=null)
        {
            if(Vector2.Distance(transform.position,loose.transform.position)<.8f){carrier.TryCollect(loose);ReleaseWork();return;}
            WalkTo(loose.transform.position,.7f);return;
        }
        if(target==null)return;
        Vector2 direction=target.transform.position-transform.position;if(direction.sqrMagnitude>.0001f)AimDirection=direction.normalized;
        if(direction.magnitude>attackRange||!WorldCollision.ClearAttackLine(transform.position,target.transform.position))
        {WalkTo(target.transform.position,attackRange*.8f);return;}
        path.Clear();stalled=0;
        if(Time.time<nextAttack||action==null)return;
        Animal accepted=target;
        if(action.BeginAxe(attackCooldown,()=>
        {
            if(health.IsDead||victim.IsCaught||accepted==null||accepted.IsDead||accepted.GetComponent<HookVictim>().IsCaught)return;
            var corpse=accepted.Kill();if(corpse!=null)carrier.TryCollect(corpse);ReleaseWork();
        }))nextAttack=Time.time+attackCooldown;
    }
    private void MoveStep(Vector3 goal)
    {
        Vector3 before=transform.position;
        Vector3 next=WorldCollision.Slide(transform,Vector3.MoveTowards(before,goal,moveSpeed*Time.deltaTime),Radius);
        next=RiverBridge.ClampStep(before,next,Radius);
        Vector2 travel=next-before;if(travel.sqrMagnitude>.000001f)AimDirection=travel.normalized;
        transform.position=next;Physics2D.SyncTransforms();
    }
    private bool CanClaim(Component item)
    {
        if(retryAfter.TryGetValue(item,out var until)&&Time.time<until)return false;
        return !claims.TryGetValue(item,out var worker)||worker==null||worker==this||worker.health.IsDead||worker.victim.IsCaught;
    }
    private bool Reachable(Component item,float arrival)
    {
        if(Vector2.Distance(transform.position,item.transform.position)<=arrival&&WorldCollision.ClearAttackLine(transform.position,item.transform.position))return true;
        WalkNavigation25D.Build(transform,item.transform.position,Radius,arrival,path,null,true);
        if(path.Count>0)return true;
        retryAfter[item]=Time.time+3;return false;
    }
    private void Search()
    {
        ReleaseWork();nextSearch=Time.time+.5f;
        foreach(var item in new List<Component>(retryAfter.Keys))if(item==null||retryAfter[item]<=Time.time)retryAfter.Remove(item);
        var corpses=new List<Carcass>(FindObjectsByType<Carcass>());
        corpses.Sort((a,b)=>(a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude));
        foreach(var corpse in corpses)
            if(corpse.IsAvailable&&SameBank(corpse.transform.position)&&CanClaim(corpse)&&Reachable(corpse,.7f))
            {loose=corpse;claims[corpse]=this;BeginWork();return;}
        var animals=new List<Animal>(FindObjectsByType<Animal>());
        animals.Sort((a,b)=>(a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude));
        foreach(var animal in animals)
            if(!animal.IsDead&&!animal.GetComponent<HookVictim>().IsCaught&&SameBank(animal.transform.position)&&CanClaim(animal)&&Reachable(animal,attackRange*.8f))
            {target=animal;claims[animal]=this;BeginWork();return;}
    }
    private void BeginWork(){progressPoint=transform.position;stalled=0;nextPath=0;}
    private void WalkTo(Vector3 goal,float arrival,Collider2D destination=null)
    {
        if((goal-pathGoal).sqrMagnitude>.25f||Time.time>=nextPath&&(path.Count==0||!WorldCollision.CanMove(transform,path[0],Radius)))
        {pathGoal=goal;nextPath=Time.time+.5f;WalkNavigation25D.Build(transform,goal,Radius,arrival,path,destination,true,true);}
        if(path.Count>0)
        {
            while(path.Count>1&&Vector2.Distance(transform.position,path[0])<.08f)path.RemoveAt(0);
            MoveStep(path[0]);
            if(Vector2.Distance(transform.position,path[0])<.04f)path.RemoveAt(0);
        }
        if(Vector2.Distance(progressPoint,transform.position)>.12f){progressPoint=transform.position;stalled=0;}
        else stalled+=Time.deltaTime;
        if(stalled<2)return;
        nextPath=0;stalled=0;path.Clear();IdleRecoveries++;
        // Never discard cargo. Delivery retries even when the factory is full.
        if(Returning)return;
        Component work=target!=null?(Component)target:loose;
        if(work!=null)retryAfter[work]=Time.time+3;
        ReleaseWork();nextSearch=0;
    }
    private void ReleaseWork()
    {
        if(!ReferenceEquals(target,null)&&claims.TryGetValue(target,out var worker)&&worker==this)claims.Remove(target);
        if(!ReferenceEquals(loose,null)&&claims.TryGetValue(loose,out worker)&&worker==this)claims.Remove(loose);
        target=null;loose=null;
    }
    private void Interrupted(){ReleaseWork();path.Clear();action?.CancelAll();nextSearch=nextPath=0;stalled=0;}
    private void Died(Health _){Interrupted();StartCoroutine(RemoveAfterPull());}
    private IEnumerator RemoveAfterPull(){yield return null;while(victim!=null&&victim.IsCaught)yield return null;Destroy(gameObject);}
    private void OnDisable()=>Interrupted();
    private void OnDestroy(){ReleaseWork();if(health!=null)health.Died-=Died;if(victim!=null)victim.Captured-=Interrupted;}
}
