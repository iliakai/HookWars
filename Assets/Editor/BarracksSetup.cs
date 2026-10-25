using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BarracksSetup
{
    public const string SoldierRoot="Assets/HookWarsSausage",BuildingRoot="Assets/HookWarsBarracks";
    public const string SoldierVisual=SoldierRoot+"/Integration/Warrior25D.prefab",BuildingVisual=BuildingRoot+"/Integration/Barracks25D.prefab";
    public const string Soldier="Assets/Prefabs/SausageWarrior.prefab",Building="Assets/Prefabs/Barracks.prefab";
    public static void ConfigureBatch()
    {
        try{Configure();Debug.Log("HOOKWARS_BARRACKS_SETUP_PASS");}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    [MenuItem("HookWars/25D/Setup barracks and sausage warriors")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        AssetDatabase.Refresh();
        foreach(var root in new[]{SoldierRoot,BuildingRoot})
        {
            if(!AssetDatabase.IsValidFolder(root+"/Integration"))AssetDatabase.CreateFolder(root,"Integration");
            foreach(var file in Directory.GetFiles(root+"/Textures","*.png"))
            {
                var path=file.Replace('\\','/');var texture=(TextureImporter)AssetImporter.GetAtPath(path);
                texture.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                texture.sRGBTexture=path.Contains("BaseColor");texture.mipmapEnabled=true;texture.maxTextureSize=2048;
                texture.SaveAndReimport();
            }
            var importer=(ModelImporter)AssetImporter.GetAtPath(root+"/Models/"+(root==SoldierRoot?"Sausage_Hero.fbx":"Sausage_Barracks.fbx"));
            importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.importAnimation=root==SoldierRoot;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            if(root==SoldierRoot)
            {
                importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            }
            importer.SaveAndReimport();
            if(root==SoldierRoot)
            {
                var clips=importer.defaultClipAnimations;
                foreach(var clip in clips)
                {
                    clip.loopTime=!clip.name.Contains("Attack");clip.loopPose=false;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;
                    clip.keepOriginalOrientation=true;clip.keepOriginalPositionY=true;clip.keepOriginalPositionXZ=true;clip.events=Array.Empty<AnimationEvent>();
                }
                importer.clipAnimations=clips;importer.SaveAndReimport();
            }
        }
        BuildSoldierVisual();BuildBarracksVisual();BuildLogic();
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
        WorldModels25DSetup.Assign(display,ArenaPresentation25D.VisualKind.Barracks,BuildingVisual);
        WorldModels25DSetup.Assign(display,ArenaPresentation25D.VisualKind.Warrior,SoldierVisual);
        foreach(var builder in Object.FindObjectsByType<PlayerBuilding>())
        {
            var data=new SerializedObject(builder);var prefabs=data.FindProperty("buildingPrefabs");
            var prefab=AssetDatabase.LoadAssetAtPath<Barracks>(Building);
            bool exists=false;for(int i=0;i<prefabs.arraySize;i++)if(prefabs.GetArrayElementAtIndex(i).objectReferenceValue==prefab)exists=true;
            if(!exists){prefabs.arraySize++;prefabs.GetArrayElementAtIndex(prefabs.arraySize-1).objectReferenceValue=prefab;}
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    private static GameObject Model(string path,Transform parent)
    {
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        model.transform.SetParent(parent,false);
        var front=Find(model.transform,"Facing_Reference").position-model.transform.position;front.y=0;
        model.transform.localScale*=1/Mathf.Max(.0001f,front.magnitude);
        model.transform.rotation=Quaternion.FromToRotation(front.normalized,Vector3.forward)*model.transform.rotation;
        return model;
    }
    private static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    private static Material Material(string root,string name,Color color,string texturePrefix=null)
    {
        string path=root+"/Integration/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.3f);material.SetFloat("_Metallic",0);
        if(texturePrefix!=null)
        {
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/"+texturePrefix+"_BaseColor.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/"+texturePrefix+"_Normal.png"));
            material.SetFloat("_BumpScale",.3f);material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/"+texturePrefix+"_UnityMask.png"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        EditorUtility.SetDirty(material);return material;
    }
    private static void AssignMaterials(GameObject model,Dictionary<string,Material> materials)
    {
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.First(pair=>m!=null&&m.name.StartsWith(pair.Key,StringComparison.Ordinal)).Value).ToArray();
            renderer.gameObject.name="Surface_"+renderer.gameObject.name;
        }
    }
    private static void BuildSoldierVisual()
    {
        var root=new GameObject("Warrior25D");
        try
        {
            string modelPath=SoldierRoot+"/Models/Sausage_Hero.fbx";
            var model=Model(modelPath,root.transform);
            float eastSign=Mathf.Sign((Find(model.transform,"East_Reference").position-model.transform.position).x);
            model.transform.localScale*=1/2.55f;
            var colors=new Dictionary<string,Color>{{"Sausage_Tintable",Color.white},{"Ivory_Bone",Color.white},{"Eye_White",new Color(.98f,.95f,.83f)},{"Iris_Amber",new Color(.35f,.16f,.045f)},{"Pupil",new Color(.032f,.018f,.012f)},{"Brow_Dark",new Color(.19f,.065f,.025f)},{"Mouth_Inside",new Color(.16f,.025f,.018f)},{"Teeth",new Color(.98f,.9f,.7f)}};
            var materials=colors.ToDictionary(pair=>pair.Key,pair=>Material(SoldierRoot,pair.Key,pair.Value,pair.Key=="Sausage_Tintable"?"Sausage":pair.Key=="Ivory_Bone"?"Bone":null));
            AssignMaterials(model,materials);
            var animator=model.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController=SoldierController(modelPath,eastSign);
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())skin.localBounds=new Bounds(new Vector3(0,1.35f,0),Vector3.one*8);
            var data=new SerializedObject(root.AddComponent<WarriorVisual25D>());
            data.FindProperty("animator").objectReferenceValue=animator;data.FindProperty("weapon").objectReferenceValue=Find(model.transform,"Weapon");data.FindProperty("eastSign").floatValue=eastSign;
            data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,SoldierVisual);
        }
        finally{Object.DestroyImmediate(root);}
    }
    private static AnimatorController SoldierController(string modelPath,float eastSign)
    {
        string path=SoldierRoot+"/Integration/Warrior.controller";
        var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(existing!=null)return existing;
        var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach(var name in new[]{"MoveX","MoveY","Speed","WalkRate","AttackRate"})controller.AddParameter(name,AnimatorControllerParameterType.Float);
        controller.parameters=controller.parameters.Select(p=>{if(p.name=="AttackRate"||p.name=="WalkRate")p.defaultFloat=1;return p;}).ToArray();
        var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string name)=>clips.Single(c=>Butcher25DSetup.Matches(c.name,name));
        var machine=controller.layers[0].stateMachine;
        var idle=machine.AddState("Idle");idle.motion=Clip("Idle_Breathe");machine.defaultState=idle;
        var tree=new BlendTree{name="Walk 8 directions",blendType=BlendTreeType.SimpleDirectional2D,blendParameter="MoveX",blendParameterY="MoveY",useAutomaticThresholds=false};
        AssetDatabase.AddObjectToAsset(tree,controller);
        string[] names={"N","NE","E","SE","S","SW","W","NW"};
        for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;tree.AddChild(Clip("Walk_"+names[i]),new Vector2(Mathf.Sin(angle)*eastSign,Mathf.Cos(angle)));}
        var walk=machine.AddState("Walk");walk.motion=tree;walk.speedParameter="WalkRate";walk.speedParameterActive=true;
        var go=idle.AddTransition(walk);go.hasExitTime=false;go.duration=.1f;go.AddCondition(AnimatorConditionMode.Greater,.03f,"Speed");
        var stop=walk.AddTransition(idle);stop.hasExitTime=false;stop.duration=.1f;stop.AddCondition(AnimatorConditionMode.Less,.03f,"Speed");
        var attack=machine.AddState("Attack_Bone");attack.motion=Clip("Attack_Bone");attack.speedParameter="AttackRate";attack.speedParameterActive=true;
        foreach(var state in new[]{idle,walk,attack})state.writeDefaultValues=true;
        EditorUtility.SetDirty(controller);return controller;
    }
    private static void BuildBarracksVisual()
    {
        var root=new GameObject("Barracks25D");
        try
        {
            var model=Model(BuildingRoot+"/Models/Sausage_Barracks.fbx",root.transform);
            model.transform.rotation=Quaternion.Euler(0,180,0)*model.transform.rotation;
            var colors=new Dictionary<string,Color>{{"Wood",Color.white},{"Plaster",Color.white},{"Roof",Color.white},{"Stone",Color.white},{"Dark_Iron",new Color(.12f,.14f,.16f)},{"Bone_Emblem",new Color(.91f,.83f,.63f)},{"Sausage_Emblem",new Color(.73f,.25f,.1f)},{"Warm_Brass",new Color(.67f,.45f,.16f)},{"Lantern_Glow",new Color(1,.57f,.13f)},{"Team_Cloth",new Color(.68f,.16f,.085f)},{"Interior_Dark",new Color(.13f,.115f,.09f)}};
            AssignMaterials(model,colors.ToDictionary(pair=>pair.Key,pair=>Material(BuildingRoot,pair.Key,pair.Value,new[]{"Wood","Plaster","Roof","Stone"}.Contains(pair.Key)?pair.Key:null)));
            var meshes=model.GetComponentsInChildren<MeshFilter>();var bounds=new Bounds();bool first=true;
            foreach(var mesh in meshes)foreach(var vertex in mesh.sharedMesh.vertices)
            {var point=mesh.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
            // The view is multiplied by the 2x2 footprint in ArenaPresentation25D.
            float scale=.96f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var data=new SerializedObject(root.AddComponent<BarracksVisual25D>());
            data.FindProperty("leftHinge").objectReferenceValue=Find(model.transform,"Door_Left_Hinge");data.FindProperty("rightHinge").objectReferenceValue=Find(model.transform,"Door_Right_Hinge");
            data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,BuildingVisual);
        }
        finally{Object.DestroyImmediate(root);}
    }
    private static void BuildLogic()
    {
        var root=new GameObject("SausageWarrior");
        try
        {
            root.layer=LayerMask.NameToLayer("PlayerTarget");root.AddComponent<CircleCollider2D>().radius=SausageWarrior.BodyRadius;
            root.AddComponent<Health>();root.AddComponent<HookVictim>();root.AddComponent<SausageWarrior>();PrefabUtility.SaveAsPrefabAsset(root,Soldier);
        }
        finally{Object.DestroyImmediate(root);}
        root=new GameObject("Barracks");
        try
        {
            root.layer=LayerMask.NameToLayer("Buildings");root.AddComponent<BoxCollider2D>().size=Vector2.one*2;
            var data=new SerializedObject(root.AddComponent<Barracks>());
            data.FindProperty("buildCost").intValue=250;data.FindProperty("warriorPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<SausageWarrior>(Soldier);
            data.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,Building);
        }
        finally{Object.DestroyImmediate(root);}
    }
}
