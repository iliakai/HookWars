using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class PlayerMelee : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAim playerAim;
    [SerializeField] private LayerMask targetLayers;

    [Header("Attack Stats")]
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 0.7f;
    [SerializeField, Range(1f, 360f)] private float attackAngle = 100f;
    [SerializeField,Min(.01f)] private float damagePerUpgrade=5,rangePerUpgrade=.2f;
    public float Damage=>damage;
    public float AttackRange=>attackRange;
    public float VisualSize=>attackRange/1.6f;
    public bool TryUpgrade(bool size,PlayerProgression owner)
    {
        if(owner==null||owner.gameObject!=gameObject||GetComponent<Health>().IsDead||GetComponent<HookVictim>().IsCaught||
            GetComponent<HeroActionTiming25D>()?.AxeAnimating==true||!owner.TrySpendUpgradePoint())return false;
        if(size)attackRange+=rangePerUpgrade;else damage+=damagePerUpgrade;return true;
    }

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
            TryAttackAtScreenPoint(Mouse.current.position.ReadValue());
        }
    }
    public void TryAttackAtScreenPoint(Vector2 screen)
    {
        if (!GamePointer.OverInterface(screen)) TryAttack();
    }

    private void TryAttack()
    {
        if(MatchPause.BlocksGameplay)return;
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
        if (playerHook != null && (playerHook.ActiveHook != null || playerHook.IsPreparingThrow))
        {
            return;
        }

        if (GetComponent<Health>()?.IsDead == true) return;
        HeroActionTiming25D timing = GetComponent<HeroActionTiming25D>();
        if (timing != null && timing.enabled)
        {
            if (timing.BeginAxe(attackCooldown, () => ApplyAttack(timing.ResolveFacing(playerAim.AimDirection))))
                nextAttackTime = Time.time + attackCooldown;
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        ApplyAttack(playerAim.AimDirection);
    }
    private void ApplyAttack(Vector2 direction)
    {
        if (!isActiveAndEnabled || GetComponent<Health>()?.IsDead == true || GetComponent<HookVictim>()?.IsCaught == true) return;

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
                direction,
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
                health.TakeDamage(damage, GetComponent<Health>());
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
    private void OnDisable() => GetComponent<HeroActionTiming25D>()?.CancelAxe();
}
