using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using HookWars.Buildings;
using Object = UnityEngine.Object;

public static class WorldModels25DSetup
{
    public const string Buildings = "Assets/HookWarsBuildings";
    public const string Output = Buildings + "/Integration";
    public static readonly string[] Names = { "Factory", "MeatShop", "AnimalFarm", "BoneWall" };
    private static readonly string[] Loops = { "Factory_Work", "Shop_OpenLoop", "Farm_Loop", "Wall_Ambient" };
    [MenuItem("HookWars/25D/World/Setup building models")]
    public static void ConfigureBuildings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode");
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Buildings, "Integration");
        for (int i = 0; i < Names.Length; i++) Build(i);
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var presentation = Object.FindAnyObjectByType<ArenaPresentation25D>();
        for (int i = 0; i < Names.Length; i++)
            Assign(presentation, (ArenaPresentation25D.VisualKind)(6+i), Output + "/" + Names[i] + ".prefab");
        EditorSceneManager.SaveScene(presentation.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_BUILDINGS_SETUP_PASS: four fixed visual prefabs, no demo colliders/scenes");
    }
    public static void Assign(ArenaPresentation25D presentation, ArenaPresentation25D.VisualKind kind, string path)
    {
        var so = new SerializedObject(presentation); var models = so.FindProperty("models");
        int index = -1;
        for (int i=0;i<models.arraySize;i++) if(models.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue==(int)kind) index=i;
        if(index<0) { index=models.arraySize; models.arraySize++; }
        var entry=models.GetArrayElementAtIndex(index); entry.FindPropertyRelative("kind").intValue=(int)kind;
        entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static AnimationClip[] Import(string path)
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType=ModelImporterAnimationType.Generic; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects=false; importer.importAnimation=true; importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips) { clip.loopTime=!clip.name.Contains("Closed"); clip.loopPose=false; clip.lockRootHeightY=true; clip.lockRootPositionXZ=true; clip.lockRootRotation=true; clip.events=Array.Empty<AnimationEvent>(); }
        importer.clipAnimations=clips; importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
    }
    public static AnimatorController Controller(string path, AnimationClip[] clips, string[] names, string initial)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)??AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine=controller.layers[0].stateMachine;
        foreach(string name in names)
        {
            var matches=clips.Where(c=>Butcher25DSetup.Matches(c.name,name)).ToArray();
            if(matches.Length!=1) throw new InvalidOperationException("Ambiguous/missing clip "+name);
            var state=machine.states.Select(s=>s.state).SingleOrDefault(s=>s.name==name)??machine.AddState(name);
            state.motion=matches[0]; state.writeDefaultValues=false;
        }
        machine.defaultState=machine.states.Single(s=>s.state.name==initial).state; EditorUtility.SetDirty(controller); return controller;
    }
    private static void Build(int index)
    {
        string name=Names[index], path=Buildings+"/Models/"+name+".fbx";
        var clips=Import(path);
        string closed=index==0?"Factory_Closed":"Shop_Closed";
        var controller=Controller(Output+"/"+name+".controller",clips,index<2?new[]{Loops[index],closed}:new[]{Loops[index]},index<2?closed:Loops[index]);
        GameObject root=new GameObject(name+"25D");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+name+".mat");
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,Output+"/"+name+".mat"); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Buildings+"/Textures/"+name+"_Albedo.png"));
            material.SetColor("_BaseColor",Color.white); material.SetFloat("_Smoothness",.18f); EditorUtility.SetDirty(material);
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in renderers)
            {
                renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                if(renderer.name.StartsWith("PreviewFX")) renderer.gameObject.SetActive(false);
                // Export has identically named mesh objects and bones. Unique mesh
                // names let SaveAsPrefabAsset retain fileIDs on repeated setup.
                renderer.gameObject.name="Surface_"+renderer.gameObject.name;
            }
            var foundation=renderers.Single(r=>r.name.Contains("_Static"));
            Bounds bounds=foundation.bounds;
            float scale=.96f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            model.transform.position+=Vector3.up*.015f;
            var animator=model.GetComponent<Animator>(); animator.applyRootMotion=false; animator.runtimeAnimatorController=controller;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var visual=root.AddComponent<BuildingVisualController>(); visual.animator=animator;
            visual.kind=index==0?HookWars.Buildings.BuildingKind.Factory:index==1?HookWars.Buildings.BuildingKind.MeatShop:index==2?HookWars.Buildings.BuildingKind.AnimalFarm:HookWars.Buildings.BuildingKind.BoneWall;
            visual.activeOnStart=index>=2;
            var effects=new System.Collections.Generic.List<ParticleSystem>();
            foreach(var socket in model.GetComponentsInChildren<Transform>(true))
                if(socket.name.StartsWith("SmokeSocket")||socket.name=="OdorSocket") effects.Add(Particles(root.transform,socket,material,index==3,scale));
            visual.effects=effects.ToArray();
            if(index==0)
            {
                var socket=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FireGlow");
                var lamp=new GameObject("Working furnace light"); lamp.transform.SetParent(root.transform,false); lamp.transform.position=socket.position;
                var light=lamp.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.25f,.03f); light.range=.45f; light.intensity=.5f;
                light.enabled=false; visual.workingLights=new[]{light};
            }
            root.AddComponent<BuildingPresentation25D>();
            PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
            Debug.Log($"HOOKWARS_BUILDING_IMPORT: {name} base={bounds.size:F4}, scale={scale:F6}, clips={string.Join(",",clips.Select(c=>c.name+":"+c.length))}");
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static ParticleSystem Particles(Transform parent, Transform socket, Material material, bool odor,float scale)
    {
        var go=new GameObject((odor?"Odor particles ":"Smoke particles ")+socket.name); go.transform.SetParent(parent,false); go.transform.position=socket.position;
        var p=go.AddComponent<ParticleSystem>(); p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=p.main; main.playOnAwake=false; main.startLifetime=2; main.startSpeed=.3f*scale; main.startSize=.25f*scale;
        main.startColor=odor?new Color(.4f,.6f,.1f,.25f):new Color(.4f,.4f,.4f,.4f); main.maxParticles=35;
        var emission=p.emission; emission.rateOverTime=5;
        var shape=p.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.radius=.08f*scale;
        go.transform.rotation=Quaternion.Euler(-90,0,0);
        // Independent material: particle tint must never alter building albedo.
        string path=Output+"/Particles.mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m,path); }
        string texturePath=Output+"/SoftParticle.asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture==null)
        {
            texture=new Texture2D(32,32,TextureFormat.RGBA32,false); texture.name="SoftParticle";
            var pixels=new Color[1024];
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) { float a=Mathf.Clamp01(1-new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).sqrMagnitude); pixels[y*32+x]=new Color(1,1,1,a*a); }
            texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture,texturePath);
        }
        m.SetTexture("_BaseMap",texture); m.SetFloat("_Surface",1); m.SetFloat("_SrcBlend",5); m.SetFloat("_DstBlend",10); m.SetFloat("_ZWrite",0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue=3000; EditorUtility.SetDirty(m);
        p.GetComponent<ParticleSystemRenderer>().sharedMaterial=m; return p;
    }
}
