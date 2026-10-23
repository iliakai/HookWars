using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Grass25DSetup
{
    public const string TexturePath = "Assets/Settings/Grass.png";
    public const string MaterialPath = "Assets/Settings/Arena25D-Grass.mat";
    [MenuItem("HookWars/25D/Setup grass texture")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.ImportAsset(TexturePath);
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing imported Grass.png");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8; importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None; importer.isReadable = false;
        importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetFloat("_Smoothness", .08f); material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);
        var scene = EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var settings = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<ArenaPresentation25D>());
        settings.FindProperty("bankMaterial").objectReferenceValue = material;
        settings.FindProperty("grassTileMeters").floatValue = 4;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_GRASS_SETUP: repeated 4m grass tiles on both banks");
    }
}
