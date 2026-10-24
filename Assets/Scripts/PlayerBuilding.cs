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
    private bool homeRightBank;
    public bool HomeRightBank => homeRightBank;
    public const float BuildRadius = 4;
    private string status = "Выберите постройку мышью";
    public Transform Preview => preview;
    public bool IsBuilding => selectedPrefab != null;
    public int QuarterTurns {get;private set;}
    public Quaternion PlacementRotation=>Quaternion.Euler(0,0,-90*QuarterTurns);
    public Vector2Int PlacementSize=>selectedPrefab==null?Vector2Int.one:(QuarterTurns%2==0?selectedPrefab.Footprint:new Vector2Int(selectedPrefab.Footprint.y,selectedPrefab.Footprint.x));
    public static Rect ScreenRect => new Rect(12, Screen.height - 206, 370, 194);
    private Rect MenuRect => ScreenRect;
    public bool BlocksMouseInput
    {
        get
        {
            if (IsBuilding || GamePointer.OverInterface()) return true;
            if (Mouse.current == null) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y);
            BuildingUpgradeMenu upgrades = GetComponent<BuildingUpgradeMenu>();
            return MenuRect.Contains(gui) || upgrades != null && (upgrades.Contains(gui) || upgrades.SelectedThisFrame);
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
        bool wasOpen=IsBuilding;CloseBuildMode();return wasOpen;
    }
    private void Update()
    {
        if (MatchPause.BlocksGameplay) return;
        if (!CanAct) { CloseBuildMode(); return; }
        Keyboard keyboard = Keyboard.current;
        if (keyboard?.qKey.wasPressedThisFrame == true) CloseBuildMode();
        if (!IsBuilding || Mouse.current == null || worldCamera == null) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        if (!GamePointer.TryLogicalPoint(worldCamera, mouse, out Vector3 point)) return;
        if(keyboard?.rKey.wasPressedThisFrame == true)RotatePlacement();
        Vector3 cell = Snap(point,PlacementSize);
        preview.position = cell;
        preview.rotation=PlacementRotation;
        bool valid = CanPlace(cell);
        PreviewColor = valid ? new Color(0.2f, 1, 0.3f, 0.6f) : new Color(1, 0.2f, 0.2f, 0.6f);
        status = valid ? "ЛКМ — строить; R — поворот; Q — отмена" : "Нельзя: занято / дальше 4 м / нет ресурсов";
        if (Mouse.current.leftButton.wasPressedThisFrame && !GamePointer.OverInterface() &&
            !MenuRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y)) &&
            !(GetComponent<BuildingUpgradeMenu>()?.Contains(new Vector2(mouse.x, Screen.height - mouse.y)) ?? false)) TryBuildAt(cell);
    }
    public bool CanPlace(Vector3 cell)
        => CanPlaceInternal(cell, true);
    public bool CanPlaceForGrid(Vector3 cell) => CanPlaceInternal(cell, false);
    private bool CanPlaceInternal(Vector3 cell, bool syncPhysics)
    {
        Vector2Int size=PlacementSize;
        if((cell-Snap(cell,size)).sqrMagnitude>.00000001f)return false;
        if (!CanAct || selectedPrefab == null || Vector2.Distance(transform.position,cell)>BuildRadius || !selectedPrefab.CanBuild(progression) ||
            !ArenaLayout.IsInsideBank(cell-new Vector3(size.x*.5f,size.y*.5f),homeRightBank,0)||
            !ArenaLayout.IsInsideBank(cell+new Vector3(size.x*.5f,size.y*.5f),homeRightBank,0)||!HasResources(selectedPrefab)) return false;
        if(syncPhysics)Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(cell, (Vector2)size-Vector2.one*.0002f, 0))
        {
            if (hit.isTrigger) continue;
            Bounds bounds = hit.bounds;
            float overlapX = Mathf.Min(cell.x + size.x*.5f, bounds.max.x) - Mathf.Max(cell.x - size.x*.5f, bounds.min.x);
            float overlapY = Mathf.Min(cell.y + size.y*.5f, bounds.max.y) - Mathf.Max(cell.y - size.y*.5f, bounds.min.y);
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
        GameBuilding built = Instantiate(prefab, cell, PlacementRotation);
        if (!built.Initialize(progression)) { Destroy(built.gameObject); return null; }
        bool paid = prefab.Kind == BuildingKind.Wall ? progression.GetComponent<OwnerEconomy>().TrySpendBones(prefab.BuildCost)
            : progression.TrySpendGold(prefab.BuildCost);
        if (!paid) { Destroy(built.gameObject); return null; }
        Physics2D.SyncTransforms();
        return built;
    }
    public void SelectBuilding(GameBuilding prefab)
    {
        CloseBuildMode();
        if (!CanAct || prefab == null || prefab.Kind==BuildingKind.Factory) return;
        selectedPrefab = prefab;
        QuarterTurns=0;
        GameObject marker = new GameObject("BuildingPreview");
        preview = marker.transform; PreviewColor=Color.green;
    }
    public void RotatePlacement(){if(IsBuilding)QuarterTurns=(QuarterTurns+1)%4;}
    public static Vector3 Snap(Vector3 point,Vector2Int size)
    {
        float x=size.x%2==0?0:.5f,y=size.y%2==0?0:.5f;
        return new Vector3(Mathf.Floor(point.x-x+.5f)+x,Mathf.Floor(point.y-y+.5f)+y,0);
    }
    private void CloseBuildMode()
    {
        selectedPrefab = null;
        status="Выберите постройку мышью";
        if (preview != null) Destroy(preview.gameObject);
        preview = null;
    }
    private void OnGUI()
    {
        if (MatchPause.BlocksGameplay) return;
        bool wasEnabled=GUI.enabled; GUI.enabled=CanAct;
        Rect box = MenuRect;
        GUI.Box(box, "Строительство: клетка 1×1 м");
        if (buildingPrefabs != null)
            for (int i = 0; i < buildingPrefabs.Length; i++)
            {
                GameBuilding prefab = buildingPrefabs[i];
                if (prefab != null && GUI.Button(new Rect(box.x + 10, box.y + 26 + i * 31, 350, 28),
                    $"{prefab.DisplayName}: {prefab.BuildCost} {(prefab.Kind == BuildingKind.Wall ? "кг костей" : "золота")}")) SelectBuilding(prefab);
            }
        GUI.Label(new Rect(box.x + 10, box.y + 158, 350, 30), status);
        if(IsBuilding&&GUI.Button(new Rect(box.x+10,box.y+124,350,28),"Отменить выбор [Q]"))CloseBuildMode();
        GUI.enabled=wasEnabled;
    }
    private void OnDisable() => CloseBuildMode();
}
