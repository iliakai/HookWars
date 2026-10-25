using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class SiegeSetup
{
    public const string Root="Assets/HookWarsHotdogBridge",Output=Root+"/Integration",Visual=Output+"/Bridge25D.prefab",Logic="Assets/Prefabs/RiverBridge.prefab";
    [MenuItem("HookWars/25D/Setup bridges orders and siege")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        AssetDatabase.Refresh();if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"Integration");
        string path=Root+"/Models/Hotdog_Bridge.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;importer.addCollider=false;importer.isReadable=true;
        importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
        foreach(var file in Directory.GetFiles(Root+"/Textures","*.png"))
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            texture.textureType=file.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=file.Contains("BaseColor");texture.maxTextureSize=2048;texture.mipmapEnabled=true;texture.SaveAndReimport();
        }
        var root=new GameObject("Bridge25D");
        try
        {
            var dimensions=new GameObject("Bridge dimensions");dimensions.transform.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(dimensions.transform,false);
            var front=Find(model,"Facing_Reference").position-model.transform.position;front.y=0;
            model.transform.localScale*=1/Mathf.Max(.0001f,front.magnitude);model.transform.rotation=Quaternion.FromToRotation(front.normalized,Vector3.forward)*model.transform.rotation;
            var colors=new Dictionary<string,Color>{{"Ivory",Color.white},{"Bun_Crust",Color.white},{"Bun_Crumb",Color.white},{"Sausage",Color.white},{"Sesame",new Color(.99f,.87f,.61f)},{"Mustard",new Color(.98f,.62f,.055f)}};
            var materials=colors.ToDictionary(p=>p.Key,p=>MakeMaterial(p.Key,p.Value));
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name.StartsWith("COL_")){renderer.gameObject.SetActive(false);continue;}
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.First(pair=>m.name.StartsWith(pair.Key,StringComparison.Ordinal)).Value).ToArray();
            }
            dimensions.transform.localScale=new Vector3(RiverBridge.PassageWidth/2.44f,6f/14,6f/14);dimensions.transform.localRotation=Quaternion.Euler(0,90,0);
            var observer=new SerializedObject(root.AddComponent<BridgeVisual25D>());var stages=observer.FindProperty("stages");stages.arraySize=3;
            for(int i=0;i<3;i++)stages.GetArrayElementAtIndex(i).objectReferenceValue=Find(model,new[]{"Stage_1_Supports","Stage_2_Bun","Stage_3_Sausage"}[i]);
            var ghost=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Ghost.mat");
            if(ghost==null){ghost=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(ghost,Output+"/Ghost.mat");}
            ghost.SetColor("_BaseColor",new Color(.15f,.8f,1,.16f));ghost.SetFloat("_Surface",1);ghost.SetFloat("_ZWrite",0);
            ghost.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);ghost.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghost.SetOverrideTag("RenderType","Transparent");ghost.renderQueue=3000;ghost.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(ghost);
            observer.FindProperty("ghost").objectReferenceValue=ghost;observer.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,Visual);
        }
        finally{Object.DestroyImmediate(root);}
        root=new GameObject("RiverBridge");
        try
        {
            root.layer=LayerMask.NameToLayer("Buildings");var trigger=root.AddComponent<BoxCollider2D>();trigger.isTrigger=true;trigger.size=new Vector2(6.2f,3.4f);
            root.AddComponent<RiverBridge>();PrefabUtility.SaveAsPrefabAsset(root,Logic);
        }
        finally{Object.DestroyImmediate(root);}
        foreach(var prefab in new[]{EconomySetup.FactoryPath,EconomySetup.ShopPath,EconomySetup.FarmPath,BarracksSetup.Building})
        {
            var contents=PrefabUtility.LoadPrefabContents(prefab);
            try{if(contents.GetComponent<BuildingDurability>()==null)contents.AddComponent<BuildingDurability>();PrefabUtility.SaveAsPrefabAsset(contents,prefab);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);var map=new SerializedObject(Object.FindAnyObjectByType<MatchMap25D>());
        map.FindProperty("bridgePrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<RiverBridge>(Logic);map.ApplyModifiedPropertiesWithoutUndo();
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();WorldModels25DSetup.Assign(display,ArenaPresentation25D.VisualKind.Bridge,Visual);
        if(display.GetComponent<WorldBuildingHUD>()==null)display.gameObject.AddComponent<WorldBuildingHUD>();
        var player=Object.FindAnyObjectByType<PlayerMovement>();if(player.GetComponent<UnitCommander>()==null)player.gameObject.AddComponent<UnitCommander>();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("HOOKWARS_SIEGE_SETUP_PASS");
    }
    private static Transform Find(GameObject model,string name)=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    private static Material MakeMaterial(string name,Color color)
    {
        var path=Output+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.35f);
        var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+"_BaseColor.png");
        if(albedo!=null)
        {
            material.SetTexture("_BaseMap",albedo);material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+"_Normal.png"));material.SetFloat("_BumpScale",.3f);material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+"_UnityMask.png"));material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        EditorUtility.SetDirty(material);return material;
    }
}
