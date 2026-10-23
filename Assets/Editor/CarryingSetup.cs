using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Core logical assets are versioned. Validate their existing GUID-based wiring;
// never regenerate legacy graphics or change the launch scene list here.
public static class CarryingSetup
{
    public const string ArenaPath="Assets/Scenes/Arena25D.unity";
    public const string PrefabPath="Assets/Prefabs/HeroCarcass.prefab";
    [MenuItem("HookWars/Validate core match wiring")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        EditorSceneManager.OpenScene(ArenaPath);
        Carcass carcass=AssetDatabase.LoadAssetAtPath<Carcass>(PrefabPath);
        if(carcass==null||carcass.GetComponent<CircleCollider2D>()==null)throw new InvalidOperationException("Missing logical carcass asset");
        foreach(var hero in Object.FindObjectsByType<Health>())
        {
            if(hero.GetComponent<CarcassCarrier>()==null||hero.GetComponent<HeroCarcassSpawner>()==null||hero.GetComponent<HookAbility>()==null)
                throw new InvalidOperationException("Missing core hero wiring: "+hero.name);
        }
        if(Object.FindObjectsByType<ArenaLayout>().Length!=1)throw new InvalidOperationException("One layout required");
    }
}
