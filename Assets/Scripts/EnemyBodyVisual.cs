using UnityEngine;

[DefaultExecutionOrder(300)]
public class EnemyBodyVisual : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float angleOffset = 0f;

    private Health ownHealth;

    private void Awake()
    {
        ownHealth = GetComponentInParent<Health>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Труп сохраняет последнее направление.
        if (ownHealth != null && ownHealth.IsDead)
        {
            return;
        }

        Vector2 direction =
            (Vector2)target.position - (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            angle + angleOffset
        );
    }
}