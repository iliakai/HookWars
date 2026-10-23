using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GameplayRevisionSetup
{
    public const string Rocks="Assets/HookWarsRocks",Output=Rocks+"/Integration",RockLogic="Assets/Prefabs/PermanentRock.prefab";
    public const string HelperPath="Assets/Prefabs/ButcherHelper.prefab";
    [MenuItem("HookWars/25D/Revision/Setup map and factories")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Rocks,"Integration");
        string fbx=Rocks+"/Models/RockPile.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Rock.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"/Rock.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Rocks+"/Textures/RockPalette.png"));material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);var root=new GameObject("Rock25D");float height;
        try
        {
            var model=Object.Instantiate(source,root.transform);model.name="RockPile";
            var bounds=BoundsOf(model);float scale=.96f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;bounds=BoundsOf(model);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y-.015f,bounds.center.z);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
            foreach(var collider in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
            height=bounds.size.y+.015f;PrefabUtility.SaveAsPrefabAsset(root,Output+"/Rock25D.prefab");
        }
        finally{Object.DestroyImmediate(root);}
        root=new GameObject("PermanentRock");
        try
        {
            root.layer=LayerMask.NameToLayer("Mountains");root.AddComponent<BoxCollider2D>().size=Vector2.one;
            var rock=root.AddComponent<PermanentRock>();var data=new SerializedObject(rock);data.FindProperty("collisionHeight").floatValue=height;data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,RockLogic);
        }
        finally{Object.DestroyImmediate(root);}
        root=PrefabUtility.LoadPrefabContents(EconomySetup.FactoryPath);
        try{root.GetComponent<BoxCollider2D>().size=Vector2.one*2;PrefabUtility.SaveAsPrefabAsset(root,EconomySetup.FactoryPath);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        root=new GameObject("ButcherHelper");
        try
        {
            root.AddComponent<CircleCollider2D>().radius=.25f;
            root.AddComponent<Health>();root.AddComponent<HookVictim>();root.AddComponent<CarcassCarrier>();
            root.AddComponent<HeroActionTiming25D>();root.AddComponent<ButcherHelper>();
            var corpse=root.AddComponent<HeroCarcassSpawner>();var data=new SerializedObject(corpse);
            data.FindProperty("carcassPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath);data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,HelperPath);
        }
        finally{Object.DestroyImmediate(root);}
        root=PrefabUtility.LoadPrefabContents(EconomySetup.FactoryPath);
        try
        {
            var data=new SerializedObject(root.GetComponent<SausageFactory>());
            data.FindProperty("helperPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<ButcherHelper>(HelperPath);data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,EconomySetup.FactoryPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var map=Object.FindAnyObjectByType<MatchMap25D>();if(map==null)map=new GameObject("Match map generator").AddComponent<MatchMap25D>();
        var config=new SerializedObject(map);config.FindProperty("factoryPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<SausageFactory>(EconomySetup.FactoryPath);
        config.FindProperty("rockPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<PermanentRock>(RockLogic);config.ApplyModifiedPropertiesWithoutUndo();
        foreach(var builder in Object.FindObjectsByType<PlayerBuilding>())
        {
            var data=new SerializedObject(builder);var choices=data.FindProperty("buildingPrefabs");choices.arraySize=3;
            string[] paths={EconomySetup.ShopPath,EconomySetup.FarmPath,EconomySetup.WallPath};
            for(int i=0;i<3;i++)choices.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameBuilding>(paths[i]);data.ApplyModifiedPropertiesWithoutUndo();
        }
        var display=new SerializedObject(Object.FindAnyObjectByType<ArenaPresentation25D>());var models=display.FindProperty("models");int index=-1;
        for(int i=0;i<models.arraySize;i++)if(models.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex==(int)ArenaPresentation25D.VisualKind.Rock)index=i;
        if(index<0){index=models.arraySize;models.arraySize++;}
        var entry=models.GetArrayElementAtIndex(index);entry.FindPropertyRelative("kind").enumValueIndex=(int)ArenaPresentation25D.VisualKind.Rock;
        entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Rock25D.prefab");display.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_REVISION_MAP_SETUP_PASS: two-cell factory and permanent rocks configured");
    }
    static Bounds BoundsOf(GameObject model)
    {
        var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("Missing rock geometry");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
}
