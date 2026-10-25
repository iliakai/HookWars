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

    [SerializeField, Min(0f)] private float cooldown = 6f;
    [SerializeField, Min(0f)] private float damage = 40f;

    [Header("Pull Settings")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 14f;
    [SerializeField, Min(1f)] private float stopDistance = 1.1f;

    [Header("Ricochet")]
    [SerializeField, Range(0f, 100f)] private float bounceDistancePercent = 50f;

    private readonly List<Vector3> bends = new List<Vector3>();
    private float flightRemaining;
    private Collider2D lastObstacle;
    private readonly HashSet<Collider2D> wallContacts = new HashSet<Collider2D>();
    private const float Skin = 0.002f;
    public int MaxBounces => int.MaxValue;
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
        Pulling,
        PullingOwner
    }

    private HookState state = HookState.Ready;

    private Transform activeHook;
    public event System.Action HookReleased;
    public Transform ActiveHook => activeHook;
    public bool IsPreparingThrow => GetComponent<HeroActionTiming25D>()?.HookPending ?? false;

    public bool IsFlyingOut => state == HookState.Flying;
    public bool IsSelfPulling => state == HookState.PullingOwner;
    private HookAnchor caughtAnchor;
    private Vector3 ownerLastSafePosition;
    private Vector3 ownerLandingPosition;
    public enum PullMode { TargetToOwner, OwnerToTarget }
    public PullMode SelectedPullMode { get; private set; } = PullMode.TargetToOwner;
    public bool TryTogglePullMode()
    {
        if (!isActiveAndEnabled || MatchPause.BlocksGameplay || state != HookState.Ready || IsPreparingThrow ||
            GetComponent<Health>()?.IsDead == true || GetComponent<HookVictim>()?.IsCaught == true) return false;
        SelectedPullMode = SelectedPullMode == PullMode.TargetToOwner ? PullMode.OwnerToTarget : PullMode.TargetToOwner;
        return true;
    }

    public float Range => range;
    public float Damage => damage;
    public float Speed => speed;
    public float Width => width;
    public Vector2 VisualDirection => throwDirection;
    public float VisualScale => initialHookWidth>0?width/initialHookWidth:1;
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
        if(MatchPause.BlocksGameplay)return;
        Health ownHealth = GetComponent<Health>();
        HookVictim ownVictim = GetComponent<HookVictim>();

        bool blocked =
            (ownHealth != null && ownHealth.IsDead) ||
            (ownVictim != null && ownVictim.IsCaught && ownVictim.Captor != this);

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
        else if(state==HookState.PullingOwner)PullOwner();
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

        if (state != HookState.Ready || IsPreparingThrow)
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
            case UpgradeStat.BounceDistance:
                return false; // Unlimited ricochets are innate, never a paid skill.
            default:
                return false;
        }
    }

    public void TryThrow(Vector2 direction)
    {
        if(MatchPause.BlocksGameplay)return;
        Health ownHealth = GetComponent<Health>();
        HookVictim ownVictim = GetComponent<HookVictim>();

        if ((ownHealth != null && ownHealth.IsDead) ||
            (ownVictim != null && ownVictim.IsCaught))
        {
            return;
        }
        if (state != HookState.Ready || IsPreparingThrow || Time.time < nextThrowTime)
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
        HeroActionTiming25D timing = GetComponent<HeroActionTiming25D>();
        if (timing != null && timing.enabled)
        {
            Vector2 accepted = direction;
            if (timing.BeginHook(accepted, () => ReleaseThrow(accepted)))
                nextThrowTime = Time.time + cooldown;
            return;
        }
        nextThrowTime = Time.time + cooldown;
        ReleaseThrow(direction);
    }

    private void ReleaseThrow(Vector2 direction)
    {
        if (!isActiveAndEnabled || GetComponent<Health>()?.IsDead == true || GetComponent<HookVictim>()?.IsCaught == true) return;
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

        RememberInitialWidth();
        activeHook.localScale=Vector3.one;
        activeHook.up=throwDirection;

        bends.Clear();
        wallContacts.Clear();
        flightRemaining = range;
        lastObstacle = null;
        state = HookState.Flying;
        HookReleased?.Invoke();
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
        wallContacts.RemoveWhere(collider => collider == null || !collider.enabled ||
            Vector2.Distance(collider.ClosestPoint(activeHook.position), activeHook.position) > width * 0.5f + Skin * 2);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(activeHook.position,
            width * 0.5f, throwDirection, step, targetLayers.value | obstacleLayers.value | carcassLayers.value);
        RaycastHit2D best = default(RaycastHit2D);
        bool found = false;
        foreach (RaycastHit2D candidate in hits)
        {
            if(candidate.collider.GetComponent<RiverBridge>()!=null)continue;
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
        var anchor=best.collider.GetComponent<HookAnchor>();
        if(anchor!=null){BeginAnchorPull(anchor);return;}
        if (IsObstacle(best.collider))
        {
            GameBuilding wall = best.collider.GetComponent<GameBuilding>();
            if (wall != null && wallContacts.Add(best.collider) && wall.TakeDamage(GetComponent<PlayerProgression>(), damage))
            {
                // The wall is gone. Keep flying without spending a bounce or adding a bend.
                activeHook.position += throwDirection * Skin;
                flightRemaining = Mathf.Max(0, flightRemaining - Skin);
                lastObstacle = best.collider;
                return;
            }
            if (bounceDistancePercent <= 0f || best.distance <= 0.00001f)
            {
                state = HookState.Returning;
                return;
            }
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
            targetLayers.value | carcassLayers.value | LayerMask.GetMask("Buildings")
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
            var anchor=target.GetComponent<HookAnchor>();
            if(anchor!=null){activeHook.position=hit.centroid;BeginAnchorPull(anchor);return true;}

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
        Animal animal = target.GetComponent<Animal>();
        if (animal != null)
        {
            Carcass carcass = animal.Kill();
            if (carcass == null) { state = HookState.Returning; return; }
            target = carcass.transform;
        }
        Health targetHealth = target.GetComponent<Health>();
        if (targetHealth != null) targetHealth.TakeDamage(damage, GetComponent<Health>());
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
        if (SelectedPullMode == PullMode.OwnerToTarget && target.GetComponent<RiverCoin>() == null && !BeginOwnerPull())
        {
            caughtVictim.Release(this); caughtVictim = null; caughtTarget = null;
            state = HookState.Returning;
        }
        else if (IsSelfPulling) ownerLandingPosition = GetOwnerLandingPosition(target.position);

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
            CarcassCarrier droppedCargo = GetPulledDeathCargo();
            caughtTarget.GetComponent<RiverCoin>()?.TryCollect(this);
            ClearHook();
            CarcassCarrier carrier = GetComponent<CarcassCarrier>();
            if (carcass != null)
            {
                carcass.ReleaseFromHero(landing);
                if (carrier != null) carrier.TryCollect(carcass);
            }
            if (droppedCargo != null) droppedCargo.ReleaseDeathCargo(landing, carrier);
        }
    }

    private CarcassCarrier GetPulledDeathCargo()
    {
        if (caughtTarget == null) return null;
        Health health = caughtTarget.GetComponent<Health>();
        return health != null && health.IsDead ? caughtTarget.GetComponent<CarcassCarrier>() : null;
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
        if(caughtAnchor!=null||state==HookState.PullingOwner)
        {
            GetComponent<HookVictim>()?.Release(this);
            caughtAnchor=null;
        }
        wallContacts.Clear();
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
        GetComponent<HeroActionTiming25D>()?.CancelHook();
        if (IsSelfPulling && !ArenaLayout.IsInsideBank(transform.position, transform.position.x > 0))
            transform.position = ownerLastSafePosition;
        Carcass carcass = GetPulledCarcass();
        CarcassCarrier droppedCargo = GetPulledDeathCargo();
        // При прерывании захвата возвращаем цель в исходную
        // безопасную точку, чтобы не оставить её посреди реки.
        if (state == HookState.Pulling && caughtTarget != null)
        {
            caughtTarget.position = targetStartPosition;
        }

        ClearHook();
        if (carcass != null) carcass.ReleaseFromHero(targetStartPosition);
        if (droppedCargo != null) droppedCargo.ReleaseDeathCargo(targetStartPosition);
    }

    protected virtual void OnDisable()
    {
        CancelHook();
    }
    private void BeginAnchorPull(HookAnchor anchor)
    {
        if (!BeginOwnerPull()) { state=HookState.Returning;return; }
        caughtAnchor=anchor;
        ownerLandingPosition=GetOwnerLandingPosition(anchor.transform.position);
    }
    private Vector3 GetOwnerLandingPosition(Vector3 target)
    {
        Vector3 approach=bends.Count>0?bends[bends.Count-1]:transform.position;
        Vector3 direction=approach-target;direction.z=0;
        if(direction.sqrMagnitude<.0001f)direction=Vector3.up;
        Vector3 landing=ArenaLayout.ClampToBank(target+direction.normalized*stopDistance,target.x>0);
        if(Vector2.Distance(landing,target)<stopDistance*.99f)
        {
            float vertical=target.y>0?-1:1;
            landing=ArenaLayout.ClampToBank(target+Vector3.up*vertical*stopDistance,target.x>0);
        }
        return landing;
    }
    private bool BeginOwnerPull()
    {
        var self=GetComponent<HookVictim>();
        if(self==null||!self.TryCapture(this))return false;
        ownerLastSafePosition=ArenaLayout.ClampToBank(transform.position,transform.position.x>0);
        state=HookState.PullingOwner;return true;
    }
    private void CompleteOwnerPull()
    {
        var carcass=GetPulledCarcass();var cargo=GetPulledDeathCargo();
        Vector3 landing=caughtTarget!=null?caughtTarget.position:transform.position;
        ClearHook();
        var carrier=GetComponent<CarcassCarrier>();
        if(carcass!=null){carcass.ReleaseFromHero(landing);if(carrier!=null)carrier.TryCollect(carcass);}
        if(cargo!=null)cargo.ReleaseDeathCargo(landing,carrier);
    }
    private void PullOwner()
    {
        if(caughtAnchor==null&&caughtTarget==null){CancelHook();return;}
        if(caughtTarget!=null)activeHook.position=caughtTarget.position+hookOffset;
        float budget=pullSpeed*Time.deltaTime;
        while(budget>.00001f)
        {
            Vector3 goal=bends.Count>0?bends[0]:ownerLandingPosition;
            float remaining=Vector2.Distance(transform.position,goal);
            if(bends.Count==0&&remaining<=.01f){CompleteOwnerPull();return;}
            float travel=Mathf.Min(budget,remaining);
            Vector3 before=transform.position;
            Vector3 desired=Vector3.MoveTowards(before,goal,travel);
            transform.position=WorldCollision.Slide(transform,desired,ArenaLayout.HeroRadius);
            Physics2D.SyncTransforms();
            if(ArenaLayout.IsInsideBank(transform.position,transform.position.x>0))ownerLastSafePosition=transform.position;
            float moved=Vector2.Distance(before,transform.position);budget-=travel;
            if(moved<travel*.5f){CancelHook();return;}
            if(bends.Count>0&&Vector2.Distance(transform.position,goal)<.01f)bends.RemoveAt(0);
            else if(bends.Count==0&&Vector2.Distance(transform.position,goal)<=.01f){CompleteOwnerPull();return;}
            if(travel<.00001f)break;
        }
    }
}
