using UnityEngine;

[DefaultExecutionOrder(250)]
[RequireComponent(typeof(Health))]
public class EnemyMelee : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHook playerHook;

    [Header("Attack Stats")]
    [SerializeField, Min(0f)] private float damage = 15f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.1f;
    [SerializeField, Min(0f)] private float reactionDelay = 0.5f;

    private Health ownHealth;
    private float readyAt;
    private float nextAttackTime;
    private Health attackTarget;
    private GameBuilding attackBuilding;
    public Vector2 AimDirection {get;private set;}=Vector2.up;

    private void Awake()
    {
        ownHealth = GetComponent<Health>();
    }

    private void OnEnable()
    {
        readyAt = Time.time + reactionDelay;
    }

    private void LateUpdate()
    {
        if(MatchPause.BlocksGameplay)return;
        if (playerHealth == null || playerHook == null)
        {
            return;
        }

        HookAbility ownHook = GetComponent<HookAbility>();

        bool blocked =
            ownHealth.IsDead ||
            playerHook.IsPullingTarget(transform) ||
            (ownHook != null && (ownHook.ActiveHook != null || ownHook.IsPreparingThrow));

        if (blocked)
        {
            readyAt = Time.time + reactionDelay;
            return;
        }

        Vector2 toPlayer =
            (Vector2)playerHealth.transform.position -
            (Vector2)transform.position;

        Health desiredTarget=playerHealth.IsDead?null:playerHealth;
        float nearest=desiredTarget!=null?toPlayer.sqrMagnitude:float.PositiveInfinity;
        foreach(var warrior in FindObjectsByType<SausageWarrior>())
        {
            var candidate=warrior.GetComponent<Health>();
            if(candidate.IsDead||warrior.GetComponent<HookVictim>().IsCaught||!CombatOwnership.Enemies(ownHealth,candidate))continue;
            float distance=(candidate.transform.position-transform.position).sqrMagnitude;
            if(distance<nearest&&WorldCollision.ClearAttackLine(transform.position,candidate.transform.position)){nearest=distance;desiredTarget=candidate;}
        }
        GameBuilding desiredBuilding=null;var ownOwner=GetComponent<PlayerProgression>();
        foreach(var building in FindObjectsByType<GameBuilding>())
        {
            if(building.Owner==null||building.Owner==ownOwner||building.CurrentHP<=0)continue;
            Vector2 point=building.ClosestPoint(transform.position);float distance=(point-(Vector2)transform.position).sqrMagnitude;
            if(distance<nearest&&WorldCollision.ClearAttackLine(transform.position,point,building.GetComponent<Collider2D>())){nearest=distance;desiredBuilding=building;desiredTarget=null;}
        }
        if(desiredTarget==null&&desiredBuilding==null){readyAt=Time.time+reactionDelay;return;}
        toPlayer=desiredBuilding!=null?desiredBuilding.ClosestPoint(transform.position)-(Vector2)transform.position:(Vector2)(desiredTarget.transform.position-transform.position);
        if(toPlayer.sqrMagnitude>.00001f)AimDirection=toPlayer.normalized;

        if (toPlayer.sqrMagnitude > attackRange * attackRange)
        {
            readyAt = Time.time + reactionDelay;
            return;
        }

        if (Time.time < readyAt || Time.time < nextAttackTime)
        {
            return;
        }

        HeroActionTiming25D timing = GetComponent<HeroActionTiming25D>();
        if (timing != null && timing.enabled)
        {
            if (timing.BeginAxe(attackCooldown, ApplyAttack)){attackTarget=desiredTarget;attackBuilding=desiredBuilding;nextAttackTime = Time.time + attackCooldown;}
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        attackTarget=desiredTarget;
        attackBuilding=desiredBuilding;
        ApplyAttack();
    }
    private void ApplyAttack()
    {
        if(attackBuilding!=null)
        {
            Vector2 point=attackBuilding.ClosestPoint(transform.position);
            if(isActiveAndEnabled&&!ownHealth.IsDead&&GetComponent<HookVictim>()?.IsCaught!=true&&Vector2.Distance(transform.position,point)<=attackRange&&WorldCollision.ClearAttackLine(transform.position,point,attackBuilding.GetComponent<Collider2D>()))
                attackBuilding.TakeDamage(GetComponent<PlayerProgression>(),damage);
            return;
        }
        if (!isActiveAndEnabled || ownHealth.IsDead || attackTarget == null || attackTarget.IsDead ||
            GetComponent<HookVictim>()?.IsCaught == true || !attackTarget.gameObject.activeInHierarchy) return;
        Vector2 toPlayer = (Vector2)(attackTarget.transform.position - transform.position);
        if (toPlayer.sqrMagnitude > attackRange * attackRange) return;
        if(attackTarget.GetComponent<SausageWarrior>()!=null&&!WorldCollision.ClearAttackLine(transform.position,attackTarget.transform.position))return;
        attackTarget.TakeDamage(damage, ownHealth);
    }
    private void OnDisable() => GetComponent<HeroActionTiming25D>()?.CancelAxe();
}
