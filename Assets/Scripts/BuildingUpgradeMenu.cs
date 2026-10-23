using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingUpgradeMenu : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    private GameBuilding selected;
    public bool SelectedThisFrame { get; private set; }
    public static Rect ScreenRect => new Rect(Screen.width - 412, Mathf.Max(70,Screen.height-390), 400, 378);
    private Rect Panel => ScreenRect;
    public bool Contains(Vector2 point) => selected != null && Panel.Contains(point);
    private void Awake() { if (worldCamera == null) worldCamera = Camera.main; }
    private bool CanAct => !MatchPause.BlocksGameplay && !GetComponent<Health>().IsDead && !GetComponent<HookVictim>().IsCaught;
    public bool CloseInterface(){bool had=selected!=null;selected=null;return had;}
    private void Update()
    {
        SelectedThisFrame = false;
        if (!CanAct) { selected = null; return; }
        if (Mouse.current == null || worldCamera == null || GetComponent<PlayerBuilding>().IsBuilding) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        if (Contains(new Vector2(mouse.x, Screen.height - mouse.y))) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame || GetComponent<PlayerBuilding>().BlocksMouseInput) return;
        SelectAtScreenPoint(mouse);
    }
    public bool SelectAtScreenPoint(Vector2 mouse)
    {
        if (!CanAct || GamePointer.OverInterface(mouse) || GetComponent<PlayerBuilding>().IsBuilding) return false;
        if (!GamePointer.TryLogicalPoint(worldCamera, mouse, out Vector3 point)) return false;
        Collider2D hit = Physics2D.OverlapPoint(point, LayerMask.GetMask("Buildings"));
        GameBuilding candidate = hit != null ? hit.GetComponent<GameBuilding>() : null;
        selected = candidate != null && candidate.Owner == GetComponent<PlayerProgression>() ? candidate : null;
        SelectedThisFrame = selected != null;
        return SelectedThisFrame;
    }
    private void OnGUI()
    {
        if (selected == null || !CanAct) return;
        Rect box = Panel;
        GUI.Box(box, selected.DisplayName);
        GUI.Label(new Rect(box.x + 10, box.y + 24, 380, 74), selected.Info);
        for (int i = 0; i < selected.UpgradeCount; i++)
            if (GUI.Button(new Rect(box.x + 10, box.y + 104 + i * 40, 380, 36), selected.UpgradeLabel(i)))
                selected.TryUpgrade(i);
        if (GUI.Button(new Rect(box.x + 320, box.y + box.height-38, 70, 26), "Закрыть")) selected = null;
    }
}
