using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class PlayerMelee : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAim playerAim;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private MeleeSwingVisual swingVisual;

    [Header("Attack Stats")]
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 0.7f;
    [SerializeField, Range(1f, 360f)] private float attackAngle = 100f;

    private PlayerHook playerHook;
    private float nextAttackTime;

    private void Awake()
    {
        playerHook = GetComponent<PlayerHook>();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null || playerAim == null)
        {
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        PlayerBuilding builder = GetComponent<PlayerBuilding>();
        if (builder != null && builder.BlocksMouseInput) return;
        HookVictim victim = GetComponent<HookVictim>();

        if (victim != null && victim.IsCaught)
        {
            return;
        }
        if (Time.time < nextAttackTime)
        {
            return;
        }

        // Пока крюк в действии, руки заняты.
        if (playerHook != null && playerHook.ActiveHook != null)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;

        if (swingVisual != null)
        {
            swingVisual.PlaySwing(playerAim.AimDirection, attackAngle);
        }

        // Учитываем последнее перемещение целей через Transform.
        Physics2D.SyncTransforms();

        Collider2D[] targets = Physics2D.OverlapCircleAll(
            transform.position,
            attackRange,
            targetLayers
        );

        // Одна цель получает урон только один раз за взмах.
        HashSet<Health> damagedTargets = new HashSet<Health>();
        HashSet<Animal> killedAnimals = new HashSet<Animal>();
        int hitCount = 0;

        foreach (Collider2D target in targets)
        {
            Health health = target.GetComponent<Health>();
            Animal animal = target.GetComponent<Animal>();

            if ((health == null && animal == null) || (health != null && health.IsDead) ||
                (animal != null && animal.IsDead) || target.gameObject == gameObject)
            {
                continue;
            }

            Vector2 toTarget =
                (Vector2)target.bounds.center -
                (Vector2)transform.position;

            // Проверяем расстояние именно между центрами.
            if (toTarget.sqrMagnitude > attackRange * attackRange)
            {
                continue;
            }

            float angle = Vector2.Angle(
                playerAim.AimDirection,
                toTarget
            );

            if (angle > attackAngle * 0.5f)
            {
                continue;
            }

            if (animal != null && killedAnimals.Add(animal))
            {
                animal.Kill();
                hitCount++;
            }
            else if (health != null && damagedTargets.Add(health))
            {
                health.TakeDamage(damage);
                hitCount++;
            }
        }

        if (hitCount == 0)
        {
            Debug.Log("Топор: промах.", this);
        }
        else
        {
            Debug.Log("Топор: попадание.", this);
        }
    }
}
