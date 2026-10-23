using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Arena25DSetup
{
    public const string ScenePath = "Assets/Scenes/Arena25D.unity";
    public const string PipelinePath = "Assets/Settings/Arena25D-URP.asset";
    [MenuItem("HookWars/25D/Setup Arena25D")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(CarryingSetup.ArenaPath, ScenePath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Arena25D-Renderer.asset");
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/Arena25D-Renderer.asset");
        }
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset"));
            pipeline.name = "Arena25D-URP";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        SerializedObject pipe = new SerializedObject(pipeline);
        var list = pipe.FindProperty("m_RendererDataList"); list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        pipe.FindProperty("m_DefaultRendererIndex").intValue = 0;
        pipe.FindProperty("m_ShadowDistance").floatValue = 120;
        pipe.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        pipe.ApplyModifiedPropertiesWithoutUndo();
        Material lit = MaterialAt("Assets/Settings/Arena25D-Lit.mat", "Universal Render Pipeline/Lit", Color.white);
        Material chain = MaterialAt("Assets/Settings/Arena25D-Chain.mat", "Universal Render Pipeline/Unlit", new Color(0.95f, 0.82f, 0.42f));
        ArenaLayout layout = Object.FindAnyObjectByType<ArenaLayout>();
        SerializedObject arena = new SerializedObject(layout); arena.FindProperty("fitCamera").boolValue = false; arena.ApplyModifiedPropertiesWithoutUndo();
        Camera camera = Camera.main;
        if (camera.GetComponent<ArenaCamera25D>() == null) camera.gameObject.AddComponent<ArenaCamera25D>();
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f);
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Arena25D Presentation");
        if (root == null) root = new GameObject("Arena25D Presentation");
        var presentation = root.GetComponent<ArenaPresentation25D>();
        if (presentation == null) presentation = root.AddComponent<ArenaPresentation25D>();
        // OnEnable's temporary pipeline change must not become an Editor/global setting.
        SerializedObject visual = new SerializedObject(presentation);
        visual.FindProperty("pipeline").objectReferenceValue = pipeline;
        visual.FindProperty("litMaterial").objectReferenceValue = lit;
        visual.FindProperty("chainMaterial").objectReferenceValue = chain;
        visual.ApplyModifiedPropertiesWithoutUndo();
        var sunObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Arena25D Sun");
        if (sunObject == null) sunObject = new GameObject("Arena25D Sun");
        Light sun = sunObject.GetComponent<Light>(); if (sun == null) sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(48, -32, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.55f);
        RenderSettings.sun = sun;
        camera.GetComponent<ArenaCamera25D>().Fit();
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_25D_SETUP: " + ScenePath);
    }
    private static Material MaterialAt(string path, string shader, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader found = Shader.Find(shader); if (found == null) throw new InvalidOperationException("Missing " + shader);
            material = new Material(found); material.color = color; AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
    public static void BatchSetup()
    {
        try { Configure(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
