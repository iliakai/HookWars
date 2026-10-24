using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class Meat25DSetup
{
    public const string Root="Assets/HookWarsMeat",Output=Root+"/Integration";
    public const string AnimalPrefab=Output+"/AnimalMeat.prefab",HeroPrefab=Output+"/HeroMeat.prefab";
    public const float HoverGap=.25f,AnimalSize=.5f,HeroSize=.9f;
    [MenuItem("HookWars/25D/Meat/Setup hovering meat")]
    public static void Configure()=>ExpansionAssetsSetup.ConfigureMeat();
    public static void ApplyIfConfigured(ArenaPresentation25D display)
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(AnimalPrefab)==null||AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab)==null)return;
        foreach(var kind in new[]{ArenaPresentation25D.VisualKind.SheepCarcass,ArenaPresentation25D.VisualKind.CowCarcass,ArenaPresentation25D.VisualKind.PigCarcass})
            WorldModels25DSetup.Assign(display,kind,AnimalPrefab);
        WorldModels25DSetup.Assign(display,ArenaPresentation25D.VisualKind.Carcass,HeroPrefab);
    }
}
