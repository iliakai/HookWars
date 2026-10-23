using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilding : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GameBuilding[] buildingPrefabs;
    private PlayerProgression progression;
    private Health health;
    private HookVictim victim;
    private Transform preview;
    public Color PreviewColor {get;private set;}
    public GameBuilding SelectedPrefab=>selectedPrefab;
    private GameBuilding selectedPrefab;
    private bool menuOpen, homeRightBank;
    private string status = "B — строительство";
    public Transform Preview => preview;
    public bool IsBuilding => selectedPrefab != null;
    private Rect MenuRect => new Rect(12, Screen.height - 206, 370, 194);
    public bool BlocksMouseInput
    {
        get
        {
            if (IsBuilding || GamePointer.OverInterface()) return true;
            if (Mouse.current == null) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y);
            BuildingUpgradeMenu upgrades = GetComponent<BuildingUpgradeMenu>();
            return menuOpen && MenuRect.Contains(gui) || upgrades != null && (upgrades.Contains(gui) || upgrades.SelectedThisFrame);
        }
    }
    private void Awake()
    {
        progression = GetComponent<PlayerProgression>(); health = GetComponent<Health>(); victim = GetComponent<HookVictim>();
        homeRightBank = transform.position.x > 0;
        if (worldCamera == null) worldCamera = Camera.main;
    }
    private bool CanAct => !MatchPause.BlocksGameplay && progression != null && (health == null || !health.IsDead) && (victim == null || !victim.IsCaught);
    public bool CloseInterface()
    {
        bool wasOpen=menuOpen||IsBuilding;CloseBuildMode();menuOpen=false;return wasOpen;
    }
    private void Update()
    {
        if (!CanAct) { CloseBuildMode(); menuOpen = false; return; }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.bKey.wasPressedThisFrame) menuOpen = !menuOpen;
        if (keyboard.escapeKey.wasPressedThisFrame) { CloseBuildMode(); menuOpen = false; }
        if (!IsBuilding || Mouse.current == null || worldCamera == null) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        if (!GamePointer.TryLogicalPoint(worldCamera, mouse, out Vector3 point)) return;
        Vector3 cell = new Vector3(Mathf.Floor(point.x) + 0.5f, Mathf.Floor(point.y) + 0.5f);
        preview.position = cell;
        bool valid = CanPlace(cell);
        PreviewColor = valid ? new Color(0.2f, 1, 0.3f, 0.6f) : new Color(1, 0.2f, 0.2f, 0.6f);
        status = valid ? "E — построить, Esc — отменить" : "Место занято, вне берега или не хватает ресурсов";
        if (keyboard.eKey.wasPressedThisFrame && !GamePointer.OverInterface() &&
            !(menuOpen && MenuRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y))) &&
            !(GetComponent<BuildingUpgradeMenu>()?.Contains(new Vector2(mouse.x, Screen.height - mouse.y)) ?? false)) TryBuildAt(cell);
    }
    public bool CanPlace(Vector3 cell)
    {
        if (Mathf.Abs(cell.x - (Mathf.Floor(cell.x) + 0.5f)) > 0.00001f ||
            Mathf.Abs(cell.y - (Mathf.Floor(cell.y) + 0.5f)) > 0.00001f) return false;
        if (!CanAct || selectedPrefab == null || !selectedPrefab.CanBuild(progression) ||
            !ArenaLayout.IsInsideBank(cell, homeRightBank, 0.5f) || !HasResources(selectedPrefab)) return false;
        Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(cell, Vector2.one * 0.9998f, 0))
        {
            if (hit.isTrigger) continue;
            Bounds bounds = hit.bounds;
            float overlapX = Mathf.Min(cell.x + 0.5f, bounds.max.x) - Mathf.Max(cell.x - 0.5f, bounds.min.x);
            float overlapY = Mathf.Min(cell.y + 0.5f, bounds.max.y) - Mathf.Max(cell.y - 0.5f, bounds.min.y);
            // Overlap queries can report touching shapes within the physics contact skin.
            // Tangency has no interior area; a real intersection must still reject placement.
            if (overlapX > 0.00001f && overlapY > 0.00001f) return false;
        }
        return true;
    }
    private bool HasResources(GameBuilding prefab) => prefab.Kind == BuildingKind.Wall
        ? progression.GetComponent<OwnerEconomy>().Bones >= prefab.BuildCost : progression.Gold >= prefab.BuildCost;
    public GameBuilding TryBuildAt(Vector3 cell)
    {
        if (!CanPlace(cell)) return null;
        GameBuilding prefab = selectedPrefab;
        GameBuilding built = Instantiate(prefab, cell, Quaternion.identity);
        if (!built.Initialize(progression)) { Destroy(built.gameObject); return null; }
        bool paid = prefab.Kind == BuildingKind.Wall ? progression.GetComponent<OwnerEconomy>().TrySpendBones(prefab.BuildCost)
            : progression.TrySpendGold(prefab.BuildCost);
        if (!paid) { Destroy(built.gameObject); return null; }
        CloseBuildMode();
        return built;
    }
    public void SelectBuilding(GameBuilding prefab)
    {
        CloseBuildMode();
        if (!CanAct || prefab == null) return;
        selectedPrefab = prefab;
        GameObject marker = new GameObject("BuildingPreview");
        preview = marker.transform; PreviewColor=Color.green;
    }
    private void CloseBuildMode()
    {
        selectedPrefab = null;
        if (preview != null) Destroy(preview.gameObject);
        preview = null;
    }
    private void OnGUI()
    {
        if (!CanAct) return;
        if (!menuOpen) { GUI.Label(new Rect(12, Screen.height - 34, 360, 26), status); return; }
        Rect box = MenuRect;
        GUI.Box(box, "Строительство: клетка 1×1 м");
        if (buildingPrefabs != null)
            for (int i = 0; i < buildingPrefabs.Length; i++)
            {
                GameBuilding prefab = buildingPrefabs[i];
                if (prefab != null && GUI.Button(new Rect(box.x + 10, box.y + 26 + i * 31, 350, 28),
                    $"{prefab.DisplayName}: {prefab.BuildCost} {(prefab.Kind == BuildingKind.Wall ? "костей" : "золота")}")) SelectBuilding(prefab);
            }
        GUI.Label(new Rect(box.x + 10, box.y + 158, 350, 30), status);
    }
    private void OnDisable() => CloseBuildMode();
}
