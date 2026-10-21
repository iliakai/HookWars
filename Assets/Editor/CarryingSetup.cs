using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CarryingSetup
{
    public const string ArenaPath = "Assets/Scenes/Arena.unity";
    public const string PrefabPath = "Assets/Prefabs/HeroCarcass.prefab";

    [MenuItem("HookWars/Setup carcass carrying")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Остановите Play Mode перед настройкой.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ArenaPath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ArenaPath);
        }

        int layer = EnsureLayer("Carcasses");
        Health[] heroes = Object.FindObjectsByType<Health>()
            .Where(h => h.gameObject.scene == scene && h.GetComponent<HookVictim>() != null).ToArray();
        if (heroes.Length != 2)
            throw new System.InvalidOperationException("В Arena ожидаются два героя с Health и HookVictim.");
        SpriteRenderer source = heroes[0].GetComponentsInChildren<SpriteRenderer>(true)
            .First(r => r.name == "BodyVisual" && r.sprite != null);
        Carcass prefab = AssetDatabase.LoadAssetAtPath<Carcass>(PrefabPath);
        if (prefab == null)
        {
            GameObject item = new GameObject("HeroCarcass");
            item.layer = layer;
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.color = new Color(0.65f, 0.22f, 0.25f);
            renderer.sortingOrder = 12;
            float dimension = Mathf.Max(source.sprite.bounds.size.x, source.sprite.bounds.size.y);
            item.transform.localScale = Vector3.one * (0.65f / dimension);
            CircleCollider2D collider = item.AddComponent<CircleCollider2D>();
            collider.radius = dimension * 0.5f;
            // Solid collider is intentional: casts also work with queriesHitTriggers disabled.
            item.AddComponent<HookVictim>();
            item.AddComponent<Carcass>();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(item, PrefabPath);
            Object.DestroyImmediate(item);
            prefab = saved.GetComponent<Carcass>();
        }

        foreach (Health hero in heroes)
        {
            CarcassCarrier carrier = GetOrAdd<CarcassCarrier>(hero.gameObject);
            HeroCarcassSpawner spawner = GetOrAdd<HeroCarcassSpawner>(hero.gameObject);
            Transform markerTransform = hero.transform.Find("CargoMarker");
            GameObject marker = markerTransform != null ? markerTransform.gameObject : new GameObject("CargoMarker");
            if (markerTransform == null)
            {
                marker.transform.SetParent(hero.transform, false);
                marker.transform.localPosition = new Vector3(0f, -0.65f, 0f);
            }
            float spriteDimension = Mathf.Max(source.sprite.bounds.size.x, source.sprite.bounds.size.y);
            marker.transform.localScale = Vector3.one * (0.4f / spriteDimension);
            SpriteRenderer cargoRenderer = GetOrAdd<SpriteRenderer>(marker);
            cargoRenderer.sprite = source.sprite;
            cargoRenderer.sharedMaterial = source.sharedMaterial;
            cargoRenderer.color = new Color(0.8f, 0.35f, 0.2f);
            cargoRenderer.sortingOrder = 30;
            cargoRenderer.enabled = false;
            SetObject(carrier, "cargoMarker", cargoRenderer);
            SetInt(carrier, "carcassLayers", 1 << layer);
            SetObject(spawner, "carcassPrefab", prefab);
            SetInt(hero.GetComponent<HookAbility>(), "carcassLayers", 1 << layer);
            if (hero.GetComponent<PlayerProgression>() != null) GetOrAdd<CarryingHUD>(hero.gameObject);
            EditorUtility.SetDirty(hero.gameObject);
        }

        // Preserve all other scene entries, but make the real game the first enabled scene.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaPath, true) }
            .Concat(EditorBuildSettings.scenes.Where(s => s.path != ArenaPath)).ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("HookWars: переноска настроена; Arena — стартовая сцена. Повторный запуск безопасен.");
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
            layers.GetArrayElementAtIndex(i).stringValue = name;
            tags.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        throw new System.InvalidOperationException("Нет свободного слоя для туш.");
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetObject(Object target, string property, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string property, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
