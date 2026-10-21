using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EconomySetup
{
    public const string FactoryPath = "Assets/Prefabs/SausageFactory.prefab";
    public const string ShopPath = "Assets/Prefabs/MeatShop.prefab";
    [MenuItem("HookWars/Economy/Setup stage 3")]
    public static void Stage3()
    {
        Stage2();
        MeatShop shop = ConfigureBuilding<MeatShop>(ShopPath, "MeatShop.png", 50);
        foreach (IncomeBuilding old in Object.FindObjectsByType<IncomeBuilding>()) Migrate(old, shop);
        foreach (PlayerBuilding builder in Object.FindObjectsByType<PlayerBuilding>())
            SetPrefabs(builder, new GameBuilding[] { AssetDatabase.LoadAssetAtPath<SausageFactory>(FactoryPath), shop });
        Save();
    }
    [MenuItem("HookWars/Economy/Setup stage 2")]
    public static void Stage2()
    {
        CarryingSetup.Configure();
        SausageFactory factory = ConfigureBuilding<SausageFactory>(FactoryPath, "Factory.png", 75);
        foreach (Health hero in Object.FindObjectsByType<Health>())
        {
            OwnerEconomy economy = GetOrAdd<OwnerEconomy>(hero.gameObject);
            PlayerProgression progression = economy.GetComponent<PlayerProgression>();
            SerializedObject stats = new SerializedObject(progression);
            stats.FindProperty("goldPerSecond").floatValue = 2;
            stats.FindProperty("experiencePerSecond").floatValue = 0;
            stats.FindProperty("gold").doubleValue = 0;
            stats.ApplyModifiedPropertiesWithoutUndo();
            PlayerBuilding builder = hero.GetComponent<PlayerBuilding>();
            if (builder != null) SetPrefabs(builder, new GameBuilding[] { factory });
        }
        foreach (IncomeBuilding old in Object.FindObjectsByType<IncomeBuilding>())
        {
            // Legacy University uses resource=Experience on the same MineBuilding class.
            SerializedObject data = new SerializedObject(old);
            if (data.FindProperty("resource").enumValueIndex != 1) continue;
            Migrate(old, factory);
        }
        Save();
    }
    public static T ConfigureBuilding<T>(string path, string image, int cost) where T : GameBuilding
    {
        string texturePath = "Assets/Textures/Buildings/" + image;
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(typeof(T).Name);
        root.layer = LayerMask.NameToLayer("Buildings");
        root.transform.localScale = Vector3.one;
        BoxCollider2D box = GetOrAdd<BoxCollider2D>(root);
        box.size = Vector2.one; box.offset = Vector2.zero;
        T component = GetOrAdd<T>(root);
        if (!exists)
        {
            SerializedObject config = new SerializedObject(component);
            config.FindProperty("buildCost").intValue = cost;
            config.ApplyModifiedPropertiesWithoutUndo();
        }
        Transform graphic = root.transform.Find("Graphic");
        if (graphic == null) { graphic = new GameObject("Graphic").transform; graphic.SetParent(root.transform, false); }
        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(graphic.gameObject);
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mine.prefab").GetComponent<SpriteRenderer>().sharedMaterial;
        renderer.sortingOrder = 5;
        graphic.localScale = Vector3.one / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        GameObject result = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        return result.GetComponent<T>();
    }
    public static void Migrate(IncomeBuilding old, GameBuilding replacement)
    {
        GameObject root = old.gameObject;
        PlayerProgression owner = old.Owner;
        if (owner == null) owner = Object.FindObjectsByType<PlayerProgression>()
            .OrderBy(p => Vector2.Distance(root.transform.position, p.transform.position)).First();
        Object.DestroyImmediate(old);
        GameBuilding building = (GameBuilding)root.AddComponent(replacement.GetType());
        SerializedObject serialized = new SerializedObject(building);
        serialized.FindProperty("owner").objectReferenceValue = owner;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.transform.localScale = Vector3.one;
        BoxCollider2D box = GetOrAdd<BoxCollider2D>(root); box.size = Vector2.one;
        SpriteRenderer source = replacement.GetComponentInChildren<SpriteRenderer>();
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        if (renderer != null) { renderer.sprite = source.sprite; renderer.enabled = false; }
        Transform graphic = root.transform.Find("Graphic");
        if (graphic == null) { graphic = new GameObject("Graphic").transform; graphic.SetParent(root.transform, false); }
        SpriteRenderer visual = GetOrAdd<SpriteRenderer>(graphic.gameObject);
        visual.sprite = source.sprite; visual.sharedMaterial = source.sharedMaterial; visual.sortingOrder = 5;
        graphic.localScale = source.transform.localScale;
        root.name = building.DisplayName + " (migrated)";
        Debug.Log("Migrated legacy building preserving root and owner: " + root.name);
    }
    public static void SetPrefabs(PlayerBuilding builder, GameBuilding[] prefabs)
    {
        SerializedObject data = new SerializedObject(builder);
        SerializedProperty array = data.FindProperty("buildingPrefabs");
        array.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    public static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>(); return component != null ? component : root.AddComponent<T>();
    }
    public static void Save()
    {
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
}
