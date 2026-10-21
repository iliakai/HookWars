using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(HookAbility))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(HookVictim))]
public class EnemyHookAI : MonoBehaviour
{
    [SerializeField] private Health playerHealth;

    [Header("Aim")]
    [SerializeField, Min(0.1f)] private float decisionInterval = 0.8f;
    [SerializeField, Range(0f, 30f)] private float aimError = 6f;
    [SerializeField, Min(0f)] private float minimumThrowDistance = 2f;

    private HookAbility hook;
    private Health ownHealth;
    private HookVictim victim;
    private float nextDecisionTime;

    private void Awake()
    {
        hook = GetComponent<HookAbility>();
        ownHealth = GetComponent<Health>();
        victim = GetComponent<HookVictim>();
    }

    private void OnEnable()
    {
        nextDecisionTime = Time.time + 1.5f;
    }

    private void LateUpdate()
    {
        if (playerHealth == null ||
            ownHealth.IsDead ||
            playerHealth.IsDead ||
            victim.IsCaught)
        {
            nextDecisionTime = Time.time + decisionInterval;
            return;
        }

        if (Time.time < nextDecisionTime || hook.ActiveHook != null)
        {
            return;
        }

        nextDecisionTime = Time.time + decisionInterval;

        Vector2 direction =
            (Vector2)playerHealth.transform.position -
            (Vector2)transform.position;

        float distance = direction.magnitude;

        if (distance < minimumThrowDistance || distance > hook.Range)
        {
            return;
        }

        // Небольшая случайная ошибка прицеливания в градусах.
        float error = Random.Range(-aimError, aimError);

        Vector3 inaccurateDirection =
            Quaternion.Euler(0f, 0f, error) *
            new Vector3(direction.x, direction.y, 0f);

        hook.TryThrow(new Vector2(
            inaccurateDirection.x,
            inaccurateDirection.y
        ));
    }
}