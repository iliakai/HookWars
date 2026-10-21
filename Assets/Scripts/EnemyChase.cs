using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(HookVictim))]
public class EnemyChase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHook playerHook;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
    [SerializeField, Min(1f)] private float stopDistance = 1.2f;

    [Header("Patrol")]
    [SerializeField, Min(0f)] private float minPause = 0.4f;
    [SerializeField, Min(0f)] private float maxPause = 1.2f;

    private Health ownHealth;
    private HookVictim victim;

    private Vector3 patrolPoint;
    private bool hasPatrolPoint;
    private bool patrolRightBank;
    private float nextPatrolTime;

    private void Awake()
    {
        ownHealth = GetComponent<Health>();
        victim = GetComponent<HookVictim>();
    }

    private void LateUpdate()
    {
        if (playerHealth == null)
        {
            return;
        }

        if (ownHealth.IsDead || victim.IsCaught)
        {
            ResetPatrol();
            return;
        }

        Vector3 enemyPosition = transform.position;

        // Дополнительная защита: пешком по реке не ходим.
        if (Mathf.Abs(enemyPosition.x) < ArenaLayout.RiverHalfWidth + ArenaLayout.HeroRadius)
        {
            ResetPatrol();
            return;
        }

        bool rightBank = enemyPosition.x > 0f;

        if (hasPatrolPoint && patrolRightBank != rightBank)
        {
            ResetPatrol();
        }

        Vector3 playerPosition = playerHealth.transform.position;

        bool sameBank = ArenaLayout.IsInsideBank(playerPosition, rightBank);

        if (!playerHealth.IsDead && sameBank)
        {
            ResetPatrol();
            ChasePlayer(playerPosition, rightBank);
        }
        else
        {
            Patrol(rightBank);
        }
    }

    private void ChasePlayer(Vector3 playerPosition, bool rightBank)
    {
        Vector2 toPlayer =
            (Vector2)playerPosition - (Vector2)transform.position;

        float distance = toPlayer.magnitude;

        if (distance <= stopDistance)
        {
            return;
        }

        float step = Mathf.Min(
            moveSpeed * Time.deltaTime,
            distance - stopDistance
        );

        Vector3 position = transform.position;
        Vector2 movement = toPlayer.normalized * step;

        position.x += movement.x;
        position.y += movement.y;

        ApplyPosition(position, rightBank);
    }

    private void Patrol(bool rightBank)
    {
        if (!hasPatrolPoint)
        {
            if (Time.time < nextPatrolTime)
            {
                return;
            }

            // Держимся ближе к реке, чтобы крюк чаще доставал.
            float fromRiver = Random.Range(1f,
                Mathf.Min(4f, ArenaLayout.Depth - ArenaLayout.HeroRadius));
            float x = (ArenaLayout.RiverHalfWidth + fromRiver) * (rightBank ? 1f : -1f);
            float yLimit = Mathf.Min(8f, ArenaLayout.FieldHeight * 0.5f - ArenaLayout.HeroRadius);
            patrolPoint = ArenaLayout.ClampToBank(new Vector3(
                x, Random.Range(-yLimit, yLimit), transform.position.z), rightBank);

            patrolRightBank = rightBank;
            hasPatrolPoint = true;
        }

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            patrolPoint,
            moveSpeed * Time.deltaTime
        );

        ApplyPosition(nextPosition, rightBank);

        if (Vector3.Distance(transform.position, patrolPoint) < 0.05f)
        {
            hasPatrolPoint = false;

            nextPatrolTime = Time.time + Random.Range(
                minPause,
                Mathf.Max(minPause, maxPause)
            );
        }
    }

    private void ApplyPosition(Vector3 position, bool rightBank)
    {
        position = ArenaLayout.ClampToBank(position, rightBank);

        transform.position = position;

        // Крюки должны видеть актуальное положение коллайдера.
        Physics2D.SyncTransforms();
    }

    private void ResetPatrol()
    {
        hasPatrolPoint = false;
        nextPatrolTime = 0f;
    }
}