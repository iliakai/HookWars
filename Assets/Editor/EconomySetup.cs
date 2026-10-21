using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EconomySetup
{
    public const string FactoryPath = "Assets/Prefabs/SausageFactory.prefab";
    public const string ShopPath = "Assets/Prefabs/MeatShop.prefab";
    public const string FarmPath = "Assets/Prefabs/AnimalFarm.prefab";
    public const string WallPath = "Assets/Prefabs/BoneWall.prefab";
    [MenuItem("HookWars/Economy/Setup all stages")]
    public static void Stage5()
    {
        Stage4();
        BoneWall wall = ConfigureBuilding<BoneWall>(WallPath, "BoneWall.png", 5);
        foreach (PlayerBuilding builder in Object.FindObjectsByType<PlayerBuilding>())
            SetPrefabs(builder, new GameBuilding[] { AssetDatabase.LoadAssetAtPath<SausageFactory>(FactoryPath),
                AssetDatabase.LoadAssetAtPath<MeatShop>(ShopPath), AssetDatabase.LoadAssetAtPath<AnimalFarm>(FarmPath), wall });
        Save();
    }
    [MenuItem("HookWars/Economy/Setup stage 4")]
    public static void Stage4()
    {
        Stage3();
        int layer = EnsureLayer("Animals");
        Animal[] animals = { ConfigureAnimal("sheep", 4, layer), ConfigureAnimal("cow", 10, layer), ConfigureAnimal("pig", 6, layer) };
        AnimalFarm farm = ConfigureBuilding<AnimalFarm>(FarmPath, "AnimalFarm.png", 150);
        using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(FarmPath))
        {
            SerializedObject config = new SerializedObject(scope.prefabContentsRoot.GetComponent<AnimalFarm>());
            SerializedProperty list = config.FindProperty("animalPrefabs"); list.arraySize = animals.Length;
            for (int i = 0; i < animals.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = animals[i];
            config.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (HookAbility hook in Object.FindObjectsByType<HookAbility>()) AddLayer(hook, "targetLayers", layer);
        foreach (PlayerMelee melee in Object.FindObjectsByType<PlayerMelee>()) AddLayer(melee, "targetLayers", layer);
        foreach (PlayerBuilding builder in Object.FindObjectsByType<PlayerBuilding>())
            SetPrefabs(builder, new GameBuilding[] { AssetDatabase.LoadAssetAtPath<SausageFactory>(FactoryPath),
                AssetDatabase.LoadAssetAtPath<MeatShop>(ShopPath), farm });
        Save();
    }
    [System.Serializable] private class FrameManifest { public int width, height; public FrameEntry[] frames; }
    [System.Serializable] private class FrameEntry { public string file; public int durationMs; }
    private static Animal ConfigureAnimal(string species, float mass, int layer)
    {
        string folder = "Assets/Textures/Animals/Frames/" + species + "_walk";
        FrameManifest manifest = JsonUtility.FromJson<FrameManifest>(System.IO.File.ReadAllText(folder + "/frames.json"));
        Sprite[] sprites = new Sprite[manifest.frames.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            string texture = folder + "/" + manifest.frames[i].file;
            AssetDatabase.ImportAsset(texture);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texture);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 480 || importer.spritePivot != new Vector2(0.5f, 0))
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 480; importer.spritePivot = new Vector2(0.5f, 0);
                TextureImporterSettings settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(0.5f, 0);
                importer.SetTextureSettings(settings); importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(texture);
        }
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        string clipPath = "Assets/Animations/" + species + "_walk.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        clip.frameRate = 100;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        float time = 0;
        for (int i = 0; i < sprites.Length; i++) { keys[i] = new ObjectReferenceKeyframe { time = time, value = sprites[i] }; time += manifest.frames[i].durationMs / 1000f; }
        // Unity extends sprite curves by one sample after the final key. Hold the
        // final sprite at (duration - sample), so the loop is exactly the GIF duration.
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = time - 1f / clip.frameRate, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "Graphic", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = true; clipSettings.stopTime = time;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
        string controllerPath = "Assets/Animations/" + species + "_walk.controller";
        UnityEditor.Animations.AnimatorController controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            UnityEditor.Animations.AnimatorState state = controller.layers[0].stateMachine.AddState("Walk");
            controller.layers[0].stateMachine.defaultState = state;
        }
        controller.layers[0].stateMachine.defaultState.motion = clip; EditorUtility.SetDirty(controller);
        string path = "Assets/Prefabs/" + species + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(species);
        root.layer = layer;
        GetOrAdd<CircleCollider2D>(root).radius = 0.35f;
        GetOrAdd<HookVictim>(root);
        GetOrAdd<Animator>(root).runtimeAnimatorController = controller;
        Animal animal = GetOrAdd<Animal>(root);
        Transform graphic = root.transform.Find("Graphic");
        if (graphic == null) { graphic = new GameObject("Graphic").transform; graphic.SetParent(root.transform, false); }
        graphic.localPosition = new Vector3(0, -0.4f, 0); graphic.localScale = Vector3.one;
        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(graphic.gameObject); renderer.sprite = sprites[0]; renderer.sortingOrder = 10;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mine.prefab").GetComponent<SpriteRenderer>().sharedMaterial;
        SerializedObject animalData = new SerializedObject(animal);
        animalData.FindProperty("visual").objectReferenceValue = renderer;
        animalData.FindProperty("carcassPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath);
        if (!exists) animalData.FindProperty("massKg").floatValue = mass;
        animalData.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<CircleCollider2D>().radius = animal.Radius;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        Debug.Log($"GIF {species}: {sprites.Length} composed frames, loop {time:0.00}s; original GIF sprite count = " +
            AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/Animals/" + species + "_walk.gif").OfType<Sprite>().Count());
        return saved.GetComponent<Animal>();
    }
    public static int EnsureLayer(string name)
    {
        int layer = LayerMask.NameToLayer(name); if (layer >= 0) return layer;
        SerializedObject data = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = data.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == "")
        { layers.GetArrayElementAtIndex(i).stringValue = name; data.ApplyModifiedPropertiesWithoutUndo(); return i; }
        throw new System.InvalidOperationException("No free Unity layer");
    }
    private static void AddLayer(Object target, string field, int layer)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty mask = data.FindProperty(field); mask.intValue |= 1 << layer; data.ApplyModifiedPropertiesWithoutUndo();
    }
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
