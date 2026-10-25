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
        if (Keyboard.current?.eKey.wasPressedThisFrame == true) TryTogglePullMode();
        PlayerBuilding builder = GetComponent<PlayerBuilding>();
        if (builder != null && builder.BlocksMouseInput) return;

        if (Mouse.current == null || playerAim == null)
        {
            return;
        }

        if (GetComponent<UnitCommander>()==null&&Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryThrowAtScreenPoint(Mouse.current.position.ReadValue());
        }
    }
    public void TryThrowAtScreenPoint(Vector2 screen)
    {
        if (GamePointer.OverInterface(screen) || playerAim == null) return;
        PlayerBuilding builder = GetComponent<PlayerBuilding>();
        if (builder != null && builder.BlocksMouseInput) return;
        if(GamePointer.TryLogicalPoint(Camera.main,screen,out var point))TryThrow((Vector2)(point-transform.position));
    }
}
