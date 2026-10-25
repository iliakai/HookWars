using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(180),RequireComponent(typeof(Health),typeof(HookVictim),typeof(CircleCollider2D))]
public sealed class SausageWarrior : MonoBehaviour
{
    public const float BodyRadius=.2f;
    public const float MoveSpeed=1.5f,DetectionRange=4;
    private Health health,target;
    private HookVictim victim;
    private bool homeRight,impactApplied;
    private float attackPhase,nextSearch,nextWander,nextPath;
    private Vector3 wanderGoal,pathGoal;
    private readonly List<Vector3> path=new List<Vector3>();
    private static readonly Vector2Int[] steps={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    public PlayerProgression Owner {get;private set;}
    public WarriorStats Stats {get;private set;}
    public Vector2 AimDirection {get;private set;}=Vector2.up;
    public Vector2 Velocity {get;private set;}
    public bool Attacking {get;private set;}
    public float AttackPhase=>attackPhase;
    public int AttackSerial {get;private set;}
    public Health Target=>target;
    public bool HomeRightBank=>homeRight;
    private void Awake()
    {
        health=GetComponent<Health>();victim=GetComponent<HookVictim>();
        health.Died+=Died;victim.Captured+=CancelAttack;
    }
    public void Bind(PlayerProgression owner,WarriorStats stats,bool rightBank)
    {
        Owner=owner;Stats=stats;homeRight=rightBank;
        gameObject.layer=LayerMask.NameToLayer(owner.GetComponent<PlayerMovement>()!=null?"PlayerTarget":"HookTarget");
        GetComponent<CircleCollider2D>().radius=BodyRadius;health.ConfigureSpawnHealth(stats.Health);
        wanderGoal=transform.position;nextWander=0;
    }
    private bool Available(Health candidate)
        =>candidate!=null&&!candidate.IsDead&&candidate.isActiveAndEnabled&&CombatOwnership.Enemies(health,candidate)&&
          ArenaLayout.IsInsideBank(candidate.transform.position,homeRight,0)&&candidate.GetComponent<HookVictim>()?.IsCaught!=true;
    private void Update()
    {
        Velocity=Vector2.zero;
        if(Owner==null||health.IsDead||victim.IsCaught){CancelAttack();return;}
        if(MatchPause.BlocksGameplay||Time.deltaTime<=0)return;
        if(!ArenaLayout.IsInsideBank(transform.position,homeRight,BodyRadius))
        {
            // A hook may have transported the soldier. Adopt the dry bank of landing;
            // no autonomous river crossing or teleport back across the river.
            if(Mathf.Abs(transform.position.x)>=ArenaLayout.RiverHalfWidth+BodyRadius)homeRight=transform.position.x>0;
            else return;
        }
        if(Attacking)
        {
            attackPhase+=Time.deltaTime/Stats.AttackPeriod;
            if(!impactApplied&&attackPhase>=.55f){impactApplied=true;ApplyStrike();}
            if(attackPhase>=1)Attacking=false;
            return;
        }
        if(!Available(target)||Vector2.Distance(transform.position,target.transform.position)>DetectionRange+Stats.Range)target=null;
        if(Time.time>=nextSearch)
        {
            nextSearch=Time.time+.3f;float best=DetectionRange*DetectionRange;Health nearest=null;
            foreach(var candidate in FindObjectsByType<Health>())
            {
                if(!Available(candidate))continue;
                float distance=(candidate.transform.position-transform.position).sqrMagnitude;
                if(distance<best&&WorldCollision.ClearAttackLine(transform.position,candidate.transform.position)){best=distance;nearest=candidate;}
            }
            if(nearest!=null)target=nearest;
        }
        if(target!=null)
        {
            Vector2 toTarget=target.transform.position-transform.position;
            if(toTarget.sqrMagnitude>.00001f)AimDirection=toTarget.normalized;
            if(toTarget.magnitude<=Stats.Range&&WorldCollision.ClearAttackLine(transform.position,target.transform.position))
            {path.Clear();Attacking=true;impactApplied=false;attackPhase=0;AttackSerial++;return;}
            WalkTo(target.transform.position,Stats.Range*.9f);return;
        }
        if(Time.time>=nextWander||Vector2.Distance(transform.position,wanderGoal)<.15f)
        {
            nextWander=Time.time+Random.Range(2,5);
            var bank=ArenaLayout.BankBounds(homeRight,BodyRadius+.03f);
            // Choose a reachable grid position: a random subcell point can have no
            // path node within the small arrival radius and leave a soldier idle.
            wanderGoal=new Vector3(bank.xMin+Random.Range(0,Mathf.FloorToInt(bank.width/.5f)+1)*.5f,
                bank.yMin+Random.Range(0,Mathf.FloorToInt(bank.height/.5f)+1)*.5f);nextPath=0;
        }
        WalkTo(wanderGoal,.2f);
    }
    private void ApplyStrike()
    {
        if(health.IsDead||victim.IsCaught||MatchPause.BlocksGameplay)return;
        Physics2D.SyncTransforms();
        float damage=Stats.Damage*(Random.value<Stats.CriticalChance?2:1);
        var hitOnce=new HashSet<Health>();
        foreach(var hit in Physics2D.OverlapCircleAll(transform.position,Stats.Range,WorldCollision.WalkingLayers))
        {
            var enemy=hit.GetComponent<Health>();
            if(!Available(enemy)||!hitOnce.Add(enemy))continue;
            Vector2 direction=enemy.transform.position-transform.position;
            if(direction.magnitude>Stats.Range||Vector2.Angle(AimDirection,direction)>50||!WorldCollision.ClearAttackLine(transform.position,enemy.transform.position))continue;
            enemy.TakeDamage(damage,health);
            if(!enemy.IsDead)WarriorPoison.Apply(enemy,health,Stats.Poison);
        }
    }
    private void WalkTo(Vector3 goal,float arrival)
    {
        if(Time.time>=nextPath||(goal-pathGoal).sqrMagnitude>.25f)
        {nextPath=Time.time+.6f;pathGoal=goal;BuildPath(goal,arrival);}
        if(path.Count==0)return;
        while(path.Count>1&&Vector2.Distance(transform.position,path[0])<.08f)path.RemoveAt(0);
        Vector3 before=transform.position;
        Vector3 next=WorldCollision.Slide(transform,Vector3.MoveTowards(before,path[0],MoveSpeed*Time.deltaTime),BodyRadius);
        next=ArenaLayout.ClampToBank(next,homeRight,BodyRadius);
        transform.position=next;Velocity=(next-before)/Time.deltaTime;
        if(target==null&&Velocity.sqrMagnitude>.00001f)AimDirection=Velocity.normalized;
        Physics2D.SyncTransforms();
        if(Vector2.Distance(next,path[0])<.04f)path.RemoveAt(0);
    }
    private void BuildPath(Vector3 goal,float arrival)
    {
        path.Clear();var bank=ArenaLayout.BankBounds(homeRight,BodyRadius+.03f);
        const float cell=.5f;int width=Mathf.FloorToInt(bank.width/cell)+1,height=Mathf.FloorToInt(bank.height/cell)+1;
        int sx=Mathf.Clamp(Mathf.RoundToInt((transform.position.x-bank.xMin)/cell),0,width-1),sy=Mathf.Clamp(Mathf.RoundToInt((transform.position.y-bank.yMin)/cell),0,height-1);
        int start=sy*width+sx,found=-1;var previous=new int[width*height];System.Array.Fill(previous,-2);
        Vector3 Point(int i)=>new Vector3(bank.xMin+i%width*cell,bank.yMin+i/width*cell);
        bool Free(Vector3 point)
        {
            foreach(var hit in Physics2D.OverlapCircleAll(point,BodyRadius+.01f,WorldCollision.WalkingLayers))
                if(!hit.isTrigger&&hit.transform!=transform)return false;
            return true;
        }
        if(Vector2.Distance(transform.position,goal)<=arrival)return;
        var queue=new Queue<int>();queue.Enqueue(start);previous[start]=-1;
        while(queue.Count>0)
        {
            int i=queue.Dequeue();Vector3 current=Point(i);
            if(Vector2.Distance(current,goal)<=arrival&&WorldCollision.ClearAttackLine(current,goal)){found=i;break;}
            foreach(var step in steps)
            {
                int x=i%width+step.x,y=i/width+step.y;
                if(x<0||y<0||x>=width||y>=height)continue;
                int n=y*width+x;
                if(previous[n]!=-2||!Free(Point(n)))continue;
                bool blocked=false;
                foreach(var hit in Physics2D.CircleCastAll(current,BodyRadius,(Point(n)-current).normalized,cell,WorldCollision.WalkingLayers))
                    if(!hit.collider.isTrigger&&hit.transform!=transform){blocked=true;break;}
                if(blocked)continue;
                previous[n]=i;queue.Enqueue(n);
            }
        }
        if(found<0)return;
        for(int i=found;i!=start&&i>=0;i=previous[i])path.Add(Point(i));path.Reverse();
        // The snapped grid start can sit behind an obstacle relative to the soldier.
        if(path.Count>0&&!WorldCollision.CanMove(transform,path[0],BodyRadius))path.Clear();
    }
    private void CancelAttack(){Attacking=false;attackPhase=0;path.Clear();Velocity=Vector2.zero;}
    private void Died(Health _){CancelAttack();StartCoroutine(RemoveAfterPull());}
    private IEnumerator RemoveAfterPull(){yield return null;while(victim!=null&&victim.IsCaught)yield return null;Destroy(gameObject);}
    private void OnDisable()=>CancelAttack();
    private void OnDestroy(){if(health!=null)health.Died-=Died;if(victim!=null)victim.Captured-=CancelAttack;}
}
