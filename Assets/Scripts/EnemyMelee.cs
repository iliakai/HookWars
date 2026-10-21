using UnityEngine;

[DefaultExecutionOrder(250)]
[RequireComponent(typeof(Health))]
public class EnemyMelee : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHook playerHook;
    [SerializeField] private MeleeSwingVisual swingVisual;

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
        if (playerHealth == null || playerHook == null)
        {
            return;
        }

        HookAbility ownHook = GetComponent<HookAbility>();

        bool blocked =
            ownHealth.IsDead ||
            playerHealth.IsDead ||
            playerHook.IsPullingTarget(transform) ||
            (ownHook != null && ownHook.ActiveHook != null);

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

        nextAttackTime = Time.time + attackCooldown;

        if (swingVisual != null)
        {
            swingVisual.PlaySwing(toPlayer.normalized, 100f);
        }

        playerHealth.TakeDamage(damage);
    }
}