using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class River25DSetup
{
    public const string TexturePath = "Assets/Settings/River.png";
    public const string MaterialPath = "Assets/Settings/Arena25D-River.mat";
    public const string MeshPath = "Assets/Settings/Arena25D-RiverSurface.asset";
    [MenuItem("HookWars/25D/Setup flowing river")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.ImportAsset(TexturePath);
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing imported River.png");
        importer.textureType = TextureImporterType.Default; importer.spriteImportMode = SpriteImportMode.None;
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
        material.SetFloat("_Smoothness", .65f); material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);
        // Explicit top-face UVs avoid dependence on Unity's built-in cube UV orientation.
        // Local Y=.5 keeps the rendered surface at exactly world Y=0 like the banks.
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { mesh = new Mesh { name = "River top surface" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
        mesh.Clear();
        mesh.vertices = new[] { new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(.5f,.5f,-.5f) };
        mesh.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
        mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        var scene = EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var settings = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<ArenaPresentation25D>());
        settings.FindProperty("riverMaterial").objectReferenceValue = material;
        settings.FindProperty("riverSurfaceMesh").objectReferenceValue = mesh;
        settings.FindProperty("riverTileMeters").floatValue = 4;
        settings.FindProperty("riverFlowMetersPerSecond").floatValue = .6f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_RIVER_SETUP: 4m tiles, .6m/s toward screen bottom");
    }
}
