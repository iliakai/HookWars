using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private MineBuilding minePrefab;

    [SerializeField] private IncomeBuilding universityPrefab;
    [SerializeField, Min(1)] private int universityCost = 75;

    [Header("Construction")]
    [SerializeField, Min(1)] private int mineCost = 50;

    private PlayerProgression progression;
    private Health health;
    private HookVictim victim;

    private SpriteRenderer preview;
    private bool building;
    private bool homeRightBank;

    private Vector3 buildPosition;
    private bool validPosition;
    private string status = "";

    private IncomeBuilding selectedPrefab;
    private int selectedCost;
    public bool IsBuilding => building;

    private void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        health = GetComponent<Health>();
        victim = GetComponent<HookVictim>();

        homeRightBank = transform.position.x > 0f;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (progression == null ||
            minePrefab == null ||
            worldCamera == null ||
            Keyboard.current == null ||
            Mouse.current == null)
        {
            return;
        }

        bool blocked =
            (health != null && health.IsDead) ||
            (victim != null && victim.IsCaught);

        if (blocked)
        {
            CloseBuildMode();
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard.bKey.wasPressedThisFrame)
        {
            OpenBuildMode(minePrefab, mineCost);
        }

        if (keyboard.nKey.wasPressedThisFrame)
        {
            OpenBuildMode(universityPrefab, universityCost);
        }

        if (!building)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseBuildMode();
            return;
        }

        UpdatePreview();

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryBuild();
        }
    }

    private void OpenBuildMode(IncomeBuilding prefab, int cost)
    {
        CloseBuildMode();

        if (prefab == null)
        {
            Debug.LogWarning("Назначь заготовку здания в Player Building.", this);
            return;
        }

        SpriteRenderer source = prefab.GetComponent<SpriteRenderer>();

        if (source == null)
        {
            Debug.LogError("У здания должен быть Sprite Renderer.", prefab);
            return;
        }

        selectedPrefab = prefab;
        selectedCost = cost;

        GameObject previewObject = new GameObject("BuildingPreview");
        preview = previewObject.AddComponent<SpriteRenderer>();

        preview.sprite = source.sprite;
        preview.sharedMaterial = source.sharedMaterial;
        preview.sortingOrder = 25;

        building = true;
    }

    private void UpdatePreview()
    {
        Vector2 screenPosition = Mouse.current.position.ReadValue();

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(
                screenPosition.x,
                screenPosition.y,
                -worldCamera.transform.position.z
            )
        );

        // Центры метровых клеток: ... -2.5, -1.5, -0.5, 0.5 ...
        buildPosition = new Vector3(
            Mathf.Floor(worldPosition.x) + 0.5f,
            Mathf.Floor(worldPosition.y) + 0.5f,
            0f
        );

        preview.transform.position = buildPosition;

        // Для метрового здания оставляем отступ полклетки от краёв.
        bool insideBank = ArenaLayout.IsInsideBank(buildPosition, homeRightBank, 0.5f);

        Physics2D.SyncTransforms();

        // Горы ограничивают поле через ArenaLayout.
        // Занятость клетки проверяем по остальным объектам.
        int placementMask = ~LayerMask.GetMask("Mountains");

        bool occupied = Physics2D.OverlapBox(
            buildPosition,
            new Vector2(0.98f, 0.98f),
            0f,
            placementMask
        ) != null;

        validPosition = insideBank && !occupied;
        bool enoughGold = progression.Gold >= selectedCost;

        preview.color = validPosition && enoughGold
            ? new Color(0.2f, 1f, 0.3f, 0.65f)
            : new Color(1f, 0.2f, 0.2f, 0.65f);

        if (!insideBank)
        {
            status = "Выбери клетку на своём исходном берегу.";
        }
        else if (occupied)
        {
            status = "Место занято.";
        }
        else if (!enoughGold)
        {
            status = "Недостаточно золота.";
        }
        else
        {
            status = "Место свободно. Нажми E.";
        }
    }

    private void TryBuild()
    {
        if (!validPosition ||
            selectedPrefab == null ||
            !progression.TrySpendGold(selectedCost))
        {
            return;
        }

        IncomeBuilding newBuilding = Instantiate(
            selectedPrefab,
            buildPosition,
            Quaternion.identity
        );

        newBuilding.Initialize(progression);
        CloseBuildMode();
    }

    private void CloseBuildMode()
    {
        building = false;

        if (preview != null)
        {
            Destroy(preview.gameObject);
            preview = null;
        }
    }

    private void OnGUI()
    {
        if (!building)
        {
            return;
        }

        float left = (Screen.width - 420f) * 0.5f;
        float top = Screen.height - 100f;

        GUI.Box(new Rect(left, top, 420, 88), GUIContent.none);

        GUI.Label(
            new Rect(left + 12, top + 8, 396, 24),
            $"{selectedPrefab.DisplayName}: {selectedCost} золота | E — построить | Esc — отмена"
        );

        GUI.Label(
            new Rect(left + 12, top + 40, 396, 30),
            status
        );
    }

    private void OnDisable()
    {
        CloseBuildMode();
    }
}