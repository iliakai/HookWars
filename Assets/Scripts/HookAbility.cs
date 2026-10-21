using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(100)]
public class HookAbility: MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform hookPrefab;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask carcassLayers;

    [Header("Hook Stats")]
    [SerializeField, Min(0.1f)] private float range = 12f;
    [SerializeField, Min(0.1f)] private float speed = 10f;
    [SerializeField, Min(0.1f)] private float width = 0.2f;

    [SerializeField, Min(0.1f)] private float hookVisualSize = 1.5f;
    [SerializeField, Min(0f)] private float cooldown = 6f;
    [SerializeField, Min(0f)] private float damage = 40f;

    [Header("Pull Settings")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 14f;
    [SerializeField, Min(1f)] private float stopDistance = 1.1f;

    [Header("Ricochet")]
    [SerializeField, Min(0)] private int maxBounces = 0;
    [SerializeField, Range(0f, 100f)] private float bounceDistancePercent = 50f;

    private readonly List<Vector3> bends = new List<Vector3>();
    private int bouncesLeft;
    private float flightRemaining;
    private Collider2D lastObstacle;
    private const float Skin = 0.002f;
    public int MaxBounces => maxBounces;
    public float BounceDistancePercent => bounceDistancePercent;
    public int ChainPointCount => activeHook == null ? 0 : bends.Count + 2;
    public Vector3 GetChainPoint(int index)
    {
        if (index == 0) return transform.position;
        if (index <= bends.Count) return bends[index - 1];
        return activeHook != null ? activeHook.position : transform.position;
    }

    private HookVictim caughtVictim;

    private enum HookState
    {
        Ready,
        Flying,
        Returning,
        Pulling
    }

    private HookState state = HookState.Ready;

    private Transform activeHook;
    public Transform ActiveHook => activeHook;

    public bool IsFlyingOut => state == HookState.Flying;

    public float Range => range;
    public float Damage => damage;
    public float Speed => speed;
    public float Width => width;
    public float Cooldown => cooldown;

    public const float MinimumCooldown = 1f;

    public enum UpgradeStat
    {
        Range,
        Damage,
        Width,
        Speed,
        Cooldown,
        Bounces,
        BounceDistance
    }

    public bool IsPullingTarget(Transform target) =>
    state == HookState.Pulling && caughtTarget == target;

    private Vector3 throwDirection;
    private float nextThrowTime;

    private Transform caughtTarget;
    private Vector3 targetStartPosition;
    private Vector3 landingDirection;
    private Vector3 hookOffset;

    private float initialHookWidth;

    protected virtual void LateUpdate()
    {
        Health ownHealth = GetComponent<Health>();
        HookVictim ownVictim = GetComponent<HookVictim>();

        bool blocked =
            (ownHealth != null && ownHealth.IsDead) ||
            (ownVictim != null && ownVictim.IsCaught);

        if (blocked)
        {
            if (activeHook != null)
            {
                CancelHook();
            }

            return;
        }

        if (activeHook != null && TrimToChainLimit())
        {
            if (state == HookState.Flying) state = HookState.Returning;
        }

        if (state == HookState.Flying)
        {
            FlyHook();
        }
        else if (state == HookState.Returning)
        {
            ReturnHook();
        }
        else if (state == HookState.Pulling)
        {
            PullTarget();
        }
    }

    private void RememberInitialWidth()
    {
        if (initialHookWidth <= 0f)
        {
            initialHookWidth = Mathf.Max(width, 0.0001f);
        }
    }

    public bool TryUpgrade(UpgradeStat stat)
    {
        RememberInitialWidth();

        if (state != HookState.Ready)
        {
            return false;
        }

        switch (stat)
        {
            case UpgradeStat.Range:
                range += 1f;
                return true;

            case UpgradeStat.Damage:
                damage += 10f;
                return true;

            case UpgradeStat.Width:
                width += 0.1f;
                return true;

            case UpgradeStat.Speed:
                speed += 1f;
                return true;

            case UpgradeStat.Cooldown:
                if (cooldown <= MinimumCooldown)
                {
                    return false;
                }

                cooldown = Mathf.Max(
                    MinimumCooldown,
                    cooldown - 0.25f
                );

                return true;

            case UpgradeStat.Bounces:
                maxBounces++;
                return true;
            case UpgradeStat.BounceDistance:
                if (bounceDistancePercent >= 100f) return false;
                bounceDistancePercent = Mathf.Min(100f, bounceDistancePercent + 10f);
                return true;
            default:
                return false;
        }
    }

    public void TryThrow(Vector2 direction)
    {
        Health ownHealth = GetComponent<Health>();
        HookVictim ownVictim = GetComponent<HookVictim>();

        if ((ownHealth != null && ownHealth.IsDead) ||
            (ownVictim != null && ownVictim.IsCaught))
        {
            return;
        }
        if (state != HookState.Ready || Time.time < nextThrowTime)
        {
            return;
        }

        if (hookPrefab == null)
        {
            Debug.LogWarning("Назначь Hook Prefab.", this);
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        throwDirection = new Vector3(
            direction.x,
            direction.y,
            0f
        );

        activeHook = Instantiate(
            hookPrefab,
            transform.position,
            Quaternion.identity
        );

        SpriteRenderer hookSprite = activeHook.GetComponent<SpriteRenderer>();

        float spriteSize = 1f;

        if (hookSprite != null && hookSprite.sprite != null)
        {
            Vector2 size = hookSprite.sprite.bounds.size;
            spriteSize = Mathf.Max(size.x, size.y);
        }

        RememberInitialWidth();

        float widthMultiplier = width / initialHookWidth;

        float visualScale =
            hookVisualSize * widthMultiplier / Mathf.Max(spriteSize, 0.0001f);

        activeHook.localScale = new Vector3(
            visualScale,
            visualScale,
            1f
        );

        HookVisual visual = activeHook.GetComponent<HookVisual>();

        if (visual != null)
        {
            visual.Initialize(this, new Vector2(
                throwDirection.x,
                throwDirection.y
            ));
        }

        bends.Clear();
        bouncesLeft = maxBounces;
        flightRemaining = range;
        lastObstacle = null;
        nextThrowTime = Time.time + cooldown;
        state = HookState.Flying;
    }

    // The full polyline, not the straight-line distance, consumes chain length.
    private bool TrimToChainLimit()
    {
        Vector3 previous = transform.position;
        float left = range;
        for (int i = 0; i <= bends.Count; i++)
        {
            Vector3 next = i < bends.Count ? bends[i] : activeHook.position;
            float length = Vector3.Distance(previous, next);
            if (length > left + 0.00001f)
            {
                activeHook.position = Vector3.MoveTowards(previous, next, Mathf.Max(0f, left));
                if (i < bends.Count) bends.RemoveRange(i, bends.Count - i);
                if (state == HookState.Pulling && caughtTarget != null)
                    caughtTarget.position = activeHook.position - hookOffset;
                return true;
            }
            left = Mathf.Max(0f, left - length);
            previous = next;
        }
        return false;
    }

    private float AvailableForwardDistance()
    {
        Vector3 anchor = transform.position;
        float prefix = 0f;
        foreach (Vector3 bend in bends)
        {
            prefix += Vector3.Distance(anchor, bend);
            anchor = bend;
        }
        float radius = Mathf.Max(0f, range - prefix);
        Vector2 offset = (Vector2)(activeHook.position - anchor);
        float projection = Vector2.Dot(offset, (Vector2)throwDirection);
        return Mathf.Max(0f, -projection + Mathf.Sqrt(Mathf.Max(0f,
            projection * projection + radius * radius - offset.sqrMagnitude)));
    }

    private void FlyHook()
    {
        float allowed = Mathf.Min(flightRemaining, AvailableForwardDistance());
        float step = Mathf.Min(speed * Time.deltaTime, allowed);
        if (step <= 0.00001f)
        {
            state = HookState.Returning;
            return;
        }

        Physics2D.SyncTransforms();
        RaycastHit2D[] hits = Physics2D.CircleCastAll(activeHook.position,
            width * 0.5f, throwDirection, step, targetLayers.value | obstacleLayers.value | carcassLayers.value);
        RaycastHit2D best = default(RaycastHit2D);
        bool found = false;
        foreach (RaycastHit2D candidate in hits)
        {
            if (candidate.collider.transform == transform ||
                candidate.collider.transform.IsChildOf(transform)) continue;
            // Ignore only numerical re-contact with the surface just left.
            if (candidate.collider == lastObstacle && candidate.distance <= 0.00001f) continue;
            bool obstacle = IsObstacle(candidate.collider);
            if (!obstacle && candidate.collider.GetComponent<HookVictim>() == null) continue;
            Carcass carcass = candidate.collider.GetComponent<Carcass>();
            if (carcass != null && !carcass.IsAvailable) continue;
            if (!found || candidate.distance < best.distance - 0.00001f ||
                (Mathf.Abs(candidate.distance - best.distance) <= 0.00001f && obstacle))
            {
                best = candidate;
                found = true;
            }
        }

        if (!found)
        {
            activeHook.position += throwDirection * step;
            flightRemaining -= step;
            if (step >= allowed - 0.00001f) state = HookState.Returning;
            return;
        }

        activeHook.position = new Vector3(best.centroid.x, best.centroid.y, activeHook.position.z);
        flightRemaining -= best.distance;
        if (IsObstacle(best.collider))
        {
            if (bouncesLeft <= 0 || bounceDistancePercent <= 0f || best.distance <= 0.00001f)
            {
                state = HookState.Returning;
                return;
            }
            bouncesLeft--;
            Vector2 reflected = Vector2.Reflect((Vector2)throwDirection, best.normal).normalized;
            throwDirection = new Vector3(reflected.x, reflected.y, 0f);
            activeHook.position += (Vector3)(best.normal * Skin);
            bends.Add(activeHook.position);
            lastObstacle = best.collider;
            flightRemaining = range * bounceDistancePercent * 0.01f;
            if (TrimToChainLimit()) state = HookState.Returning;
            return;
        }

        HookVictim victim = best.collider.GetComponent<HookVictim>();
        if (victim.IsCaught)
        {
            state = HookState.Returning;
            return;
        }
        HitAndPull(best.collider.transform);
    }

    private bool IsObstacle(Collider2D collider)
    {
        return (obstacleLayers.value & (1 << collider.gameObject.layer)) != 0;
    }

    // Reverse the saved bounce route, using one movement budget per frame.
    private bool MoveBackAlongRoute(
        float budget,
        Vector3 destination,
        bool catchTargets = false)
    {
        while (bends.Count > 0)
        {
            int index = bends.Count - 1;
            Vector3 waypoint = bends[index];

            float distance = Vector3.Distance(
                activeHook.position,
                waypoint
            );

            if (catchTargets && TryCatchOnReturn(waypoint, budget))
            {
                // Крюк захватил цель. Не продолжаем обычный возврат.
                return false;
            }

            if (distance > budget)
            {
                activeHook.position = Vector3.MoveTowards(
                    activeHook.position,
                    waypoint,
                    budget
                );

                return false;
            }

            activeHook.position = waypoint;
            budget = Mathf.Max(0f, budget - distance);
            bends.RemoveAt(index);
        }

        if (catchTargets && TryCatchOnReturn(destination, budget))
        {
            return false;
        }

        float lastDistance = Vector3.Distance(
            activeHook.position,
            destination
        );

        activeHook.position = Vector3.MoveTowards(
            activeHook.position,
            destination,
            budget
        );

        return lastDistance <= budget;
    }

    private bool TryCatchOnReturn(Vector3 destination, float movementBudget)
    {
        Vector2 direction =
            (Vector2)destination - (Vector2)activeHook.position;

        float distance = direction.magnitude;

        if (distance <= 0.00001f)
        {
            return false;
        }

        float checkDistance = Mathf.Min(distance, movementBudget);

        Physics2D.SyncTransforms();

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            activeHook.position,
            width * 0.5f,
            direction.normalized,
            checkDistance,
            targetLayers.value | carcassLayers.value
        );

        // CircleCastAll возвращает попадания по возрастанию расстояния.
        foreach (RaycastHit2D hit in hits)
        {
            Transform target = hit.collider.transform;

            // Владелец не может поймать самого себя.
            if (target == transform || target.IsChildOf(transform))
            {
                continue;
            }

            HookVictim victim = target.GetComponent<HookVictim>();

            if (victim == null || victim.IsCaught)
            {
                continue;
            }

            Carcass carcass = target.GetComponent<Carcass>();
            if (carcass != null && !carcass.IsAvailable) continue;

            Health targetHealth = target.GetComponent<Health>();

            // Уже лежащие трупы по-прежнему не подбираем.
            if (targetHealth != null && targetHealth.IsDead)
            {
                continue;
            }

            activeHook.position = new Vector3(
                hit.centroid.x,
                hit.centroid.y,
                activeHook.position.z
            );

            HitAndPull(target);
            return true;
        }

        return false;
    }

    private void HitAndPull(Transform target)
    {
        Health targetHealth = target.GetComponent<Health>();
        if (targetHealth != null) targetHealth.TakeDamage(damage);
        // Keep pulling the dead hero; its separate carcass is reserved until landing.
        BeginPull(target);
    }

    private void ReturnHook()
    {
        bool arrived = MoveBackAlongRoute(
            speed * Time.deltaTime,
            transform.position,
            true
        );

        if (arrived)
        {
            ClearHook();
        }
    }

    private void BeginPull(Transform target)
    {
        HookVictim victim = target.GetComponent<HookVictim>();

        if (victim == null || !victim.TryCapture(this))
        {
            state = HookState.Returning;
            return;
        }

        // Захваченный персонаж прекращает использование своего крюка.
        HookAbility targetHook = target.GetComponent<HookAbility>();

        if (targetHook != null && targetHook != this)
        {
            targetHook.CancelHook();
        }

        caughtVictim = victim;
        caughtTarget = target;
        targetStartPosition = target.position;

        hookOffset = activeHook.position - target.position;

        landingDirection = target.position - transform.position;
        landingDirection.z = 0f;

        if (landingDirection.sqrMagnitude < 0.0001f)
        {
            landingDirection = Vector3.right;
        }

        landingDirection.Normalize();
        state = HookState.Pulling;

        Debug.Log("Притягиваем: " + target.name, target);
    }

    private Vector3 GetLandingPosition()
    {
        Vector3 playerPosition = transform.position;

        Vector3 destination =
            playerPosition + landingDirection * stopDistance;

        // Границы берега берём из общих настроек арены.
        bool rightBank = playerPosition.x > 0f;
        destination = ArenaLayout.ClampToBank(destination, rightBank);
        destination.z = playerPosition.z;

        // У края берега не размещаем врага поверх игрока.
        // Если спереди тесно, выбираем место выше или ниже.
        if (Vector3.Distance(destination, playerPosition) < stopDistance * 0.99f)
        {
            float verticalDirection = playerPosition.y > 0f ? -1f : 1f;

            destination = playerPosition +
                Vector3.up * verticalDirection * stopDistance;

            destination = ArenaLayout.ClampToBank(destination, rightBank);
        }

        return destination;
    }

    private void PullTarget()
    {
        if (caughtTarget == null)
        {
            if (caughtVictim != null) caughtVictim.Release(this);
            caughtVictim = null;
            state = HookState.Returning;
            return;
        }
        Vector3 landing = GetLandingPosition();
        bool arrived = MoveBackAlongRoute(pullSpeed * Time.deltaTime, landing + hookOffset);
        caughtTarget.position = activeHook.position - hookOffset;
        if (arrived)
        {
            caughtTarget.position = landing;
            Carcass carcass = GetPulledCarcass();
            ClearHook();
            if (carcass != null)
            {
                carcass.ReleaseFromHero(landing);
                CarcassCarrier carrier = GetComponent<CarcassCarrier>();
                if (carrier != null) carrier.TryCollect(carcass);
            }
        }
    }

    private Carcass GetPulledCarcass()
    {
        if (caughtTarget == null) return null;
        Carcass carcass = caughtTarget.GetComponent<Carcass>();
        if (carcass != null) return carcass;
        Health health = caughtTarget.GetComponent<Health>();
        HeroCarcassSpawner spawner = caughtTarget.GetComponent<HeroCarcassSpawner>();
        return health != null && health.IsDead && spawner != null ? spawner.CurrentCarcass : null;
    }

    private void ClearHook()
    {
        bends.Clear();
        lastObstacle = null;
        if (caughtVictim != null)
        {
            caughtVictim.Release(this);
            caughtVictim = null;
        }

        if (activeHook != null)
        {
            Destroy(activeHook.gameObject);
            activeHook = null;
        }

        caughtTarget = null;
        state = HookState.Ready;
    }

    public void CancelHook()
    {
        Carcass carcass = GetPulledCarcass();
        // При прерывании захвата возвращаем цель в исходную
        // безопасную точку, чтобы не оставить её посреди реки.
        if (state == HookState.Pulling && caughtTarget != null)
        {
            caughtTarget.position = targetStartPosition;
        }

        ClearHook();
        if (carcass != null) carcass.ReleaseFromHero(targetStartPosition);
    }

    protected virtual void OnDisable()
    {
        CancelHook();
    }
}
