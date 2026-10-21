using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private HookVictim victim;

    private void Awake()
    {
        victim = GetComponent<HookVictim>();
    }

    private void Update()
    {
        if (victim != null && victim.IsCaught)
        {
            return;
        }

        Move(ReadMovementInput());
    }

    private Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 direction = Vector2.zero;

        if (keyboard.wKey.isPressed) direction.y += 1f;
        if (keyboard.sKey.isPressed) direction.y -= 1f;
        if (keyboard.aKey.isPressed) direction.x += -1f;
        if (keyboard.dKey.isPressed) direction.x += 1f;

        return Vector2.ClampMagnitude(direction, 1f);
    }

    private void Move(Vector2 direction)
    {
        Vector3 position = transform.position;

        // Определяем берег до перемещения.
        bool rightBank = position.x > 0f;

        position.x += direction.x * moveSpeed * Time.deltaTime;
        position.y += direction.y * moveSpeed * Time.deltaTime;

        position = ArenaLayout.ClampToBank(position, rightBank);

        transform.position = position;
        Physics2D.SyncTransforms();
    }
}