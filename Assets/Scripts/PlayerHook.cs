using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class PlayerHook : HookAbility
{
    [Header("Player Input")]
    [SerializeField] private PlayerAim playerAim;

    protected override void LateUpdate()
    {
        // Общая механика: полёт, возврат и подтягивание.
        base.LateUpdate();
        PlayerBuilding builder = GetComponent<PlayerBuilding>();
        if (builder != null && builder.BlocksMouseInput) return;

        if (Mouse.current == null || playerAim == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryThrow(playerAim.AimDirection);
        }
    }
}
