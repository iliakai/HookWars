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
            playerHealth.IsDead ||
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
            if (timing.BeginAxe(attackCooldown, ApplyAttack)) nextAttackTime = Time.time + attackCooldown;
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        ApplyAttack();
    }
    private void ApplyAttack()
    {
        if (!isActiveAndEnabled || ownHealth.IsDead || playerHealth == null || playerHealth.IsDead ||
            GetComponent<HookVictim>()?.IsCaught == true || !playerHealth.gameObject.activeInHierarchy) return;
        Vector2 toPlayer = (Vector2)(playerHealth.transform.position - transform.position);
        if (toPlayer.sqrMagnitude > attackRange * attackRange) return;

        playerHealth.TakeDamage(damage);
    }
    private void OnDisable() => GetComponent<HeroActionTiming25D>()?.CancelAxe();
}
