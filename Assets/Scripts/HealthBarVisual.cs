using UnityEngine;

[DefaultExecutionOrder(400)]
public class HealthBarVisual : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Transform fill;

    private Vector3 fullScale;
    private Vector3 fullPosition;
    private SpriteRenderer fillRenderer;

    private SpriteRenderer[] barRenderers;

    private void Awake()
    {
        barRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (health == null || fill == null)
        {
            Debug.LogWarning(
                "Назначь Health и Fill в Health Bar Visual.",
                this
            );

            enabled = false;
            return;
        }

        fullScale = fill.localScale;
        fullPosition = fill.localPosition;
        fillRenderer = fill.GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        bool visible = !health.IsDead;

        foreach (SpriteRenderer barRenderer in barRenderers)
        {
            barRenderer.enabled = visible;
        }

        if (!visible)
        {
            return;
        }

        float fraction = health.HealthFraction;

        // Уменьшаем ширину зелёной полосы.
        fill.localScale = new Vector3(
            fullScale.x * fraction,
            fullScale.y,
            fullScale.z
        );

        // Сдвигаем её так, чтобы левый край оставался на месте.
        fill.localPosition = new Vector3(
            fullPosition.x - fullScale.x * (1f - fraction) * 0.5f,
            fullPosition.y,
            fullPosition.z
        );

        if (fillRenderer != null)
        {
            fillRenderer.enabled = fraction > 0f;
        }
    }
}