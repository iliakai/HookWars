using UnityEngine;

// Выполняется до Awake у движения, строительства и возрождения.
[DefaultExecutionOrder(-1000)]
public class ArenaLayout : MonoBehaviour
{
    [Header("Size in metres - applied on Play")]
    [SerializeField, Range(4, 20)] private int bankDepth = 16;
    [SerializeField, Min(0.1f)] private float mountainWidth = 1f;

    [Header("Existing scene objects")]
    [SerializeField] private SpriteRenderer leftBank;
    [SerializeField] private SpriteRenderer rightBank;
    [SerializeField] private SpriteRenderer river;
    [SerializeField] private SpriteRenderer leftMountains;
    [SerializeField] private SpriteRenderer rightMountains;
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemy;

    [Header("Camera")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool fitCamera = true;
    [SerializeField, Min(0f)] private float cameraMargin = 1f;

    public const float RiverWidth = 4f;
    public const float FieldHeight = 20f;
    public const float HeroRadius = 0.5f;
    public const float RiverHalfWidth = RiverWidth * 0.5f;

    public static ArenaLayout Instance { get; private set; }
    private int appliedDepth;
    private float appliedMountainWidth;
    private float lastAspect = -1f;

    // Без компонента остаются прежние границы, чтобы старые сцены работали.
    public static float Depth => Instance != null ? Instance.appliedDepth : 8f;
    public static float OuterEdge => RiverHalfWidth + Depth;

    public static Rect BankBounds(bool right, float inset = HeroRadius)
    {
        float left = right ? RiverHalfWidth : -OuterEdge;
        return new Rect(left + inset, -FieldHeight * 0.5f + inset,
            Depth - 2f * inset, FieldHeight - 2f * inset);
    }

    public static Vector3 ClampToBank(Vector3 position, bool right, float inset = HeroRadius)
    {
        Rect bounds = BankBounds(right, inset);
        position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
        position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
        return position;
    }

    public static bool IsInsideBank(Vector3 position, bool right, float inset = HeroRadius)
    {
        Rect bounds = BankBounds(right, inset);
        return position.x >= bounds.xMin && position.x <= bounds.xMax &&
            position.y >= bounds.yMin && position.y <= bounds.yMax;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("В сцене должен быть только один ArenaLayout.", this);
            enabled = false;
            return;
        }

        if (worldCamera == null) worldCamera = Camera.main;
        if (player == null || enemy == null || player == enemy ||
            !ValidSurface(leftBank) || !ValidSurface(rightBank) ||
            !ValidSurface(river) || !ValidSurface(leftMountains) ||
            !ValidSurface(rightMountains) ||
            leftMountains.GetComponent<BoxCollider2D>() == null ||
            rightMountains.GetComponent<BoxCollider2D>() == null)
        {
            Debug.LogError("ArenaLayout: назначь пять разных SpriteRenderer, Player и Enemy. " +
                "У гор нужен BoxCollider2D. Поверхности должны быть без поворота и с положительным Scale.", this);
            enabled = false;
            return;
        }

        SpriteRenderer[] surfaces = { leftBank, rightBank, river, leftMountains, rightMountains };
        for (int i = 0; i < surfaces.Length; i++)
            for (int j = i + 1; j < surfaces.Length; j++)
                if (surfaces[i] == surfaces[j])
                {
                    Debug.LogError("ArenaLayout: в поля поверхностей назначены одинаковые объекты.", this);
                    enabled = false;
                    return;
                }

        appliedDepth = Mathf.Clamp(bankDepth, 4, 20);
        appliedMountainWidth = Mathf.Max(0.1f, mountainWidth);
        Instance = this;

        float bankCentre = RiverHalfWidth + appliedDepth * 0.5f;
        ResizeSurface(leftBank, -bankCentre, appliedDepth, FieldHeight);
        ResizeSurface(rightBank, bankCentre, appliedDepth, FieldHeight);
        ResizeSurface(river, 0f, RiverWidth, FieldHeight);
        ResizeMountain(leftMountains, -OuterEdge - appliedMountainWidth * 0.5f);
        ResizeMountain(rightMountains, OuterEdge + appliedMountainWidth * 0.5f);

        // Сохраняем исходную сторону игрока. Враг появляется напротив.
        bool playerRight = player.position.x > 0f;
        PlaceHero(player, playerRight);
        PlaceHero(enemy, !playerRight);
        Physics2D.SyncTransforms();
        UpdateCamera();
    }

    private static bool ValidSurface(SpriteRenderer surface)
    {
        return surface != null && surface.sprite != null &&
            surface.transform.lossyScale.x > 0.0001f &&
            surface.transform.lossyScale.y > 0.0001f &&
            Quaternion.Angle(surface.transform.rotation, Quaternion.identity) < 0.01f;
    }

    private static void ResizeSurface(SpriteRenderer surface, float centreX, float width, float height)
    {
        // Меняем число тайлов, сохраняя масштаб рисунка и материал.
        Vector3 scale = surface.transform.lossyScale;
        surface.drawMode = SpriteDrawMode.Tiled;
        surface.tileMode = SpriteTileMode.Continuous;
        surface.size = new Vector2(width / scale.x, height / scale.y);
        // Учитываем также спрайты с pivot не в центре.
        Vector3 desired = new Vector3(centreX, 0f, surface.bounds.center.z);
        surface.transform.position += desired - surface.bounds.center;
    }

    private void ResizeMountain(SpriteRenderer surface, float centreX)
    {
        ResizeSurface(surface, centreX, appliedMountainWidth, FieldHeight);
        BoxCollider2D box = surface.GetComponent<BoxCollider2D>();
        box.autoTiling = false;
        box.isTrigger = false;
        box.edgeRadius = 0f;
        box.size = surface.size;
        Vector3 localCentre = surface.transform.InverseTransformPoint(surface.bounds.center);
        box.offset = new Vector2(localCentre.x, localCentre.y);
    }

    private static void PlaceHero(Transform hero, bool right)
    {
        // Не раздвигаем героев за дальность начального крюка на большой карте.
        float fromRiver = Mathf.Min(4f, Depth * 0.5f);
        float x = (RiverHalfWidth + fromRiver) * (right ? 1f : -1f);
        hero.position = ClampToBank(new Vector3(x, 0f, hero.position.z), right);
    }

    private void LateUpdate()
    {
        if (fitCamera && worldCamera != null &&
            !Mathf.Approximately(lastAspect, worldCamera.aspect)) UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (!fitCamera || worldCamera == null) return;
        worldCamera.orthographic = true;
        Vector3 position = worldCamera.transform.position;
        position.x = 0f;
        position.y = 0f;
        worldCamera.transform.position = position;
        lastAspect = Mathf.Max(0.01f, worldCamera.aspect);
        float halfWidth = OuterEdge + appliedMountainWidth + cameraMargin;
        float halfHeight = FieldHeight * 0.5f + cameraMargin;
        worldCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / lastAspect);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
