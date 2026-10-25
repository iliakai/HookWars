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
        if (MatchPause.BlocksGameplay || ownHealth.IsDead ||
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

        if(TryAimAtCoin(out Vector2 coinDirection))
        {hook.TryThrow(coinDirection);return;}
        if(playerHealth==null||playerHealth.IsDead)return;

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
    private bool TryAimAtCoin(out Vector2 direction)
    {
        direction=Vector2.zero;float nearest=float.PositiveInfinity;
        Physics2D.SyncTransforms();
        foreach(var coin in FindObjectsByType<RiverCoin>())
        {
            if(!coin.IsAvailable)continue;
            float distance=Vector2.Distance(transform.position,coin.transform.position);
            float lead=HeroActionTiming25D.HookRelease+distance/Mathf.Max(.1f,hook.Speed);
            Vector2 target=(Vector2)coin.transform.position+Vector2.down*(coin.FlowSpeed*lead);
            Vector2 aim=target-(Vector2)transform.position;distance=aim.magnitude;
            if(distance<.1f||distance>hook.Range||distance>=nearest||target.y < -ArenaLayout.FieldHeight*.5f)continue;
            var hits=Physics2D.CircleCastAll(transform.position,hook.Width*.5f,aim.normalized,distance,LayerMask.GetMask("Buildings","Mountains"));
            bool blocked=false;foreach(var hit in hits)
                if(hit.transform!=transform&&(!hit.collider.isTrigger||hit.collider.GetComponent<HookAnchor>()!=null)){blocked=true;break;}
            if(blocked)continue;
            nearest=distance;direction=aim.normalized;
        }
        return nearest<float.PositiveInfinity;
    }
}
