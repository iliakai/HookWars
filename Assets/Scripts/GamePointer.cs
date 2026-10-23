using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public static class GamePointer
{
    // Reserve both sides symmetrically even when a contextual panel is closed,
    // so opening construction never moves the camera or hides an edge cell.
    public static float FieldSideInset => Mathf.Max(
        Mathf.Max(TrainingMatch.ScreenRect.xMax,ProgressionHUD.ScreenRect.xMax),
        Mathf.Max(Mathf.Max(CarryingHUD.ScreenRect.xMax,PlayerBuilding.ScreenRect.xMax),
            Mathf.Max(Screen.width-HookUpgradeMenu.ScreenRect.x,Screen.width-BuildingUpgradeMenu.ScreenRect.x)))+12;
    public static bool TryLogicalPoint(Camera camera, Vector2 screen, out Vector3 point)
    {
        point = Vector3.zero;
        if (camera == null) return false;
        Ray ray = camera.ScreenPointToRay(screen);
        bool mapped = camera.GetComponent<ArenaCamera25D>() != null;
        Plane plane = new Plane(mapped ? Vector3.up : Vector3.forward, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 hit = ray.GetPoint(distance);
        point = mapped ? (Vector3)ArenaSpace25D.ToLogical(hit) : new Vector3(hit.x, hit.y, 0);
        return true;
    }

    // IMGUI panels have no EventSystem raycast targets. Keep these rectangles shared
    // with their drawing components, including panels with labels only.
    public static bool OverInterface(Vector2 screen)
    {
        if(MatchPause.BlocksGameplay)return true;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
        Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
        return TrainingMatch.ScreenRect.Contains(gui) || ProgressionHUD.ScreenRect.Contains(gui) ||
            CarryingHUD.ScreenRect.Contains(gui) || HookUpgradeMenu.ScreenRect.Contains(gui);
    }
    public static bool OverInterface() => Mouse.current != null && OverInterface(Mouse.current.position.ReadValue());
}
