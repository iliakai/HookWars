using UnityEngine;

[DefaultExecutionOrder(250)]
public class HookVisual : MonoBehaviour
{
    [SerializeField] private float angleOffset = 270f;

    private HookAbility owner;
    private Vector3 previousPosition;

    public void Initialize(HookAbility hookOwner, Vector2 direction)
    {
        owner = hookOwner;
        previousPosition = transform.position;
        SetDirection(direction);
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.000001f)
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

    private void LateUpdate()
    {
        Vector3 movement = transform.position - previousPosition;

        // Поворачиваем только при полёте наружу.
        // На возврате и при подтягивании сохраняем ориентацию.
        if (owner != null && owner.IsFlyingOut)
        {
            SetDirection(new Vector2(movement.x, movement.y));
        }

        previousPosition = transform.position;
    }
}