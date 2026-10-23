using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class Launch25DSetup
{
    public const string MenuPath="Assets/Scenes/MainMenu.unity";
    [MenuItem("HookWars/25D/Launch/Setup MainMenu and build scenes")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var pause=Object.FindAnyObjectByType<MatchPause>();if(pause==null)new GameObject("Match session").AddComponent<MatchPause>();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Scene menu=File.Exists(MenuPath)?EditorSceneManager.OpenScene(MenuPath):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(Object.FindAnyObjectByType<MainMenuController>()==null)new GameObject("Main menu").AddComponent<MainMenuController>();
        var camera=Object.FindAnyObjectByType<Camera>();if(camera==null)camera=new GameObject("MainMenu Camera").AddComponent<Camera>();
        camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.075f);
        if(camera.GetComponent<AudioListener>()==null)camera.gameObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(menu,MenuPath);
        var scenes=new[]{new EditorBuildSettingsScene(MenuPath,true),new EditorBuildSettingsScene(Arena25DSetup.ScenePath,true)};
        EditorBuildSettings.globalScenes=scenes;
        var profile=BuildProfile.GetActiveBuildProfile();
        if(profile!=null){profile.overrideGlobalScenes=true;profile.scenes=scenes;EditorUtility.SetDirty(profile);}
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Active build target is "+EditorUserBuildSettings.activeBuildTarget+"; Windows64 required");
        AssetDatabase.SaveAssets();PlayFromMenu();
        string report="target="+EditorUserBuildSettings.activeBuildTarget+"\nactive profile="+(profile!=null?AssetDatabase.GetAssetPath(profile):"Windows platform profile; global scene list")+"\n"+
            string.Join("\n",(profile!=null?profile.GetScenesForBuild():EditorBuildSettings.globalScenes).Select(s=>s.enabled+" "+s.path))+"\nWindows module="+BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64);
        Directory.CreateDirectory("Temp/HookWarsAutomation");File.WriteAllText("Temp/HookWarsAutomation/build-profile.txt",report);
        Debug.Log("HOOKWARS_LAUNCH_SETUP_PASS: "+report);
    }
    [MenuItem("HookWars/25D/Launch/Play from MainMenu (default)")]
    public static void PlayFromMenu()=>EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
    [MenuItem("HookWars/25D/Launch/Play current scene for direct testing")]
    public static void PlayCurrent()=>EditorSceneManager.playModeStartScene=null;
}
