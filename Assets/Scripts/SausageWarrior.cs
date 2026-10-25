using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(180),RequireComponent(typeof(Health),typeof(HookVictim),typeof(CircleCollider2D))]
public sealed class SausageWarrior : MonoBehaviour
{
    public const float BodyRadius=.2f/1.5f,MoveSpeed=1.5f,DetectionRange=4;
    private Health health,target;
    private GameBuilding buildingTarget;
    private HookVictim victim;
    private bool homeRight,impactApplied,manualMove,manualAttack;
    private float attackPhase,nextSearch,nextPath;
    private Vector3 pathGoal,moveGoal;
    private readonly List<Vector3> path=new List<Vector3>();
    public PlayerProgression Owner {get;private set;}
    public Barracks SourceBarracks {get;private set;}
    public WarriorStats Stats {get;private set;}
    public Vector2 AimDirection {get;private set;}=Vector2.up;
    public Vector2 Velocity {get;private set;}
    public bool Attacking {get;private set;}
    public bool IsSelected {get;set;}
    public bool HasManualOrder=>manualMove||manualAttack;
    public bool AutoRaiding=>false;
    public float AttackPhase=>attackPhase;
    public int AttackSerial {get;private set;}
    public Health Target=>target;
    public GameBuilding BuildingTarget=>buildingTarget;
    public bool HomeRightBank=>homeRight;
    private void Awake(){health=GetComponent<Health>();victim=GetComponent<HookVictim>();health.Died+=Died;victim.Captured+=CancelAttack;}
    public void Bind(PlayerProgression owner,WarriorStats stats,bool rightBank)
    {
        Owner=owner;Stats=stats;homeRight=rightBank;
        gameObject.layer=LayerMask.NameToLayer(owner.GetComponent<PlayerMovement>()!=null?"PlayerTarget":"HookTarget");
        GetComponent<CircleCollider2D>().radius=BodyRadius;health.ConfigureSpawnHealth(stats.Health);
    }
    public void AssignBarracks(Barracks barracks)=>SourceBarracks=barracks;
    public void SetAutoRaid(bool enabled){}
    public bool CommandMove(Vector3 position)
    {
        if(MatchPause.BlocksGameplay||health.IsDead||!RiverBridge.Walkable(position,BodyRadius))return false;
        CancelAttack();manualMove=true;manualAttack=false;target=null;buildingTarget=null;moveGoal=position;nextPath=0;return true;
    }
    public bool CommandAttack(Component enemy)
    {
        if(MatchPause.BlocksGameplay||health.IsDead||enemy==null)return false;
        var h=enemy.GetComponent<Health>();var b=enemy.GetComponent<GameBuilding>();
        if(!(Available(h)||Available(b)))return false;
        CancelAttack();manualMove=false;manualAttack=true;target=h;buildingTarget=b;nextPath=0;return true;
    }
    private bool Available(Health h)=>h!=null&&!h.IsDead&&h.isActiveAndEnabled&&CombatOwnership.Enemies(health,h)&&h.GetComponent<HookVictim>()?.IsCaught!=true;
    private bool Available(GameBuilding b)=>b!=null&&b.isActiveAndEnabled&&b.Owner!=null&&b.Owner!=Owner&&b.CurrentHP>0;
    private void Update()
    {
        Velocity=Vector2.zero;
        if(Owner==null||health.IsDead||victim.IsCaught){CancelAttack();return;}
        if(MatchPause.BlocksGameplay||Time.deltaTime<=0)return;
        if(Attacking)
        {
            attackPhase+=Time.deltaTime/Stats.AttackPeriod;
            if(!impactApplied&&attackPhase>=.55f){impactApplied=true;ApplyStrike();}
            if(attackPhase>=1)Attacking=false;return;
        }
        if(manualMove)
        {
            WalkTo(moveGoal,.36f);if(Vector2.Distance(transform.position,moveGoal)<.4f)manualMove=false;
            return;
        }
        if(!Available(target)||!manualAttack&&target!=null&&!SameBank(target.transform.position))target=null;if(!Available(buildingTarget))buildingTarget=null;
        if(manualAttack&&target==null&&buildingTarget==null)manualAttack=false;
        if(!manualAttack&&Time.time>=nextSearch)
        {
            nextSearch=Time.time+.4f;target=null;buildingTarget=null;
            float best=DetectionRange*DetectionRange;
            foreach(var h in FindObjectsByType<Health>())
            {
                if(!SameBank(h.transform.position))continue;
                if(!Available(h))continue;float distance=(h.transform.position-transform.position).sqrMagnitude;
                if(distance<best&&WorldCollision.ClearAttackLine(transform.position,h.transform.position)){best=distance;target=h;}
            }

        }
        if(target!=null||buildingTarget!=null)
        {
            var collider=buildingTarget!=null?buildingTarget.GetComponent<Collider2D>():target.GetComponent<Collider2D>();
            Vector3 goal=buildingTarget!=null?buildingTarget.transform.position:target.transform.position;
            Vector2 aim=buildingTarget!=null?buildingTarget.ClosestPoint(transform.position):(Vector2)goal;
            Vector2 direction=aim-(Vector2)transform.position;if(direction.sqrMagnitude>.00001f)AimDirection=direction.normalized;
            if(SameBank(goal)&&direction.magnitude<=Stats.Range&&WorldCollision.ClearAttackLine(transform.position,aim,buildingTarget!=null?collider:null))
            {path.Clear();Attacking=true;impactApplied=false;attackPhase=0;AttackSerial++;return;}
            WalkTo(goal,SameBank(goal)?Stats.Range*.85f:.4f,buildingTarget!=null?collider:null);return;
        }
        path.Clear();
    }
    private bool SameBank(Vector3 point)=>(point.x>0)==(transform.position.x>0);
    private void ApplyStrike()
    {
        if(health.IsDead||victim.IsCaught||MatchPause.BlocksGameplay)return;
        Physics2D.SyncTransforms();float damage=Stats.Damage*(Random.value<Stats.CriticalChance?2:1);
        var hitOnce=new HashSet<Health>();var buildings=new HashSet<GameBuilding>();
        foreach(var hit in Physics2D.OverlapCircleAll(transform.position,Stats.Range,WorldCollision.WalkingLayers))
        {
            var h=hit.GetComponent<Health>();var b=hit.GetComponent<GameBuilding>();
            Vector2 point=b!=null?b.ClosestPoint(transform.position):(Vector2)hit.bounds.center;
            Vector2 direction=point-(Vector2)transform.position;
            if(!SameBank(hit.transform.position)||direction.magnitude>Stats.Range||Vector2.Angle(AimDirection,direction)>50||!WorldCollision.ClearAttackLine(transform.position,point,b!=null?hit:null))continue;
            if(Available(h)&&hitOnce.Add(h)){h.TakeDamage(damage,health);if(!h.IsDead)WarriorPoison.Apply(h,health,Stats.Poison);}
            if(Available(b)&&buildings.Add(b))b.TakeDamage(Owner,damage);
        }
    }
    private void WalkTo(Vector3 goal,float arrival,Collider2D goalCollider=null)
    {
        if(Time.time>=nextPath||(goal-pathGoal).sqrMagnitude>.25f)
        {nextPath=Time.time+.8f;pathGoal=goal;WalkNavigation25D.Build(transform,goal,BodyRadius,arrival,path,goalCollider);}
        if(path.Count==0)return;
        while(path.Count>1&&Vector2.Distance(transform.position,path[0])<.08f)path.RemoveAt(0);
        Vector3 before=transform.position,next=WorldCollision.Slide(transform,Vector3.MoveTowards(before,path[0],MoveSpeed*Time.deltaTime),BodyRadius);
        next=RiverBridge.ClampStep(before,next,BodyRadius);transform.position=next;Velocity=(next-before)/Time.deltaTime;
        if(target==null&&buildingTarget==null&&Velocity.sqrMagnitude>.00001f)AimDirection=Velocity.normalized;
        Physics2D.SyncTransforms();if(Vector2.Distance(next,path[0])<.04f)path.RemoveAt(0);
    }
    private void CancelAttack(){Attacking=false;attackPhase=0;path.Clear();Velocity=Vector2.zero;}
    private void Died(Health _){CancelAttack();IsSelected=false;StartCoroutine(RemoveAfterPull());}
    private IEnumerator RemoveAfterPull(){yield return null;while(victim!=null&&victim.IsCaught)yield return null;Destroy(gameObject);}
    private void OnDisable(){CancelAttack();IsSelected=false;}
    private void OnDestroy(){if(health!=null)health.Died-=Died;if(victim!=null)victim.Captured-=CancelAttack;}
}
