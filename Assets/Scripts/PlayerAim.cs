using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if(MatchPause.BlocksGameplay)return;
        if (Mouse.current == null || worldCamera == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        if (!GamePointer.TryLogicalPoint(worldCamera, mouseScreenPosition, out Vector3 mouseWorldPosition)) return;

        Vector2 direction =
            (Vector2)mouseWorldPosition - (Vector2)transform.position;

        // Если курсор точно в центре игрока, сохраняем прежнее направление.
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        AimDirection = direction.normalized;

        float angle = Mathf.Atan2(AimDirection.y, AimDirection.x)
            * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
