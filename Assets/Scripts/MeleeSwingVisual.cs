using UnityEngine;

[DefaultExecutionOrder(300)]
public class MeleeSwingVisual : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.2f;

    private SpriteRenderer[] parts;
    private float startTime;
    private float startAngle;
    private float endAngle;
    private bool isSwinging;

    private void Awake()
    {
        parts = GetComponentsInChildren<SpriteRenderer>();
        SetVisible(false);
    }

    public void PlaySwing(Vector2 direction, float arcAngle)
    {
        float aimAngle = Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;

        startAngle = aimAngle - arcAngle * 0.5f;
        endAngle = aimAngle + arcAngle * 0.5f;

        startTime = Time.time;
        isSwinging = true;

        transform.rotation = Quaternion.Euler(0f, 0f, startAngle);
        SetVisible(true);
    }

    private void LateUpdate()
    {
        if (!isSwinging)
        {
            return;
        }

        float progress = Mathf.Clamp01(
            (Time.time - startTime) / duration
        );

        float angle = Mathf.Lerp(startAngle, endAngle, progress);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (progress >= 1f)
        {
            isSwinging = false;
            SetVisible(false);
        }
    }

    private void SetVisible(bool visible)
    {
        if (parts == null)
        {
            return;
        }

        foreach (SpriteRenderer part in parts)
        {
            part.enabled = visible;
        }
    }

    private void OnDisable()
    {
        isSwinging = false;
        SetVisible(false);
    }
}