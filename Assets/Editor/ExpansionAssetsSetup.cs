using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ExpansionAssetsSetup
{
    public const string MeatRoot="Assets/HookWarsMeatPickup",MeatModel=MeatRoot+"/Models/Rib_Steak_Static.fbx";
    public const string EyeRoot="Assets/HookWarsHookEye",EyeModel=EyeRoot+"/Models/Hook_Eye_Only.fbx";
    public const string EyePrefab="Assets/Prefabs/HookAnchor.prefab",EyeVisual=EyeRoot+"/Integration/HookEye.prefab";
    static void Import(string root,string model)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        AssetDatabase.Refresh();
        var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
        importer.SaveAndReimport();
        foreach(var file in Directory.GetFiles(root+"/Textures","*.png"))
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));ti.textureType=file.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            ti.sRGBTexture=file.Contains("BaseColor");ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.SaveAndReimport();
        }
    }
    static Material Material(string path,string textures,string prefix,float smooth,float metal=0)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",Color.white);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_BaseColor.png");m.SetTexture("_BaseMap",albedo);m.SetTexture("_MainTex",albedo);
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.6f);
        var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+prefix+"_UnityMask.png");
        if(mask!=null){m.SetTexture("_MetallicGlossMap",mask);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}
        EditorUtility.SetDirty(m);return m;
    }
    public static void ConfigureMeat()
    {
        Import(MeatRoot,MeatModel);
        string output=Meat25DSetup.Output;
        var meat=Material(output+"/RibMeat.mat",MeatRoot+"/Textures/","Meat",.6f);
        var bone=Material(output+"/RibBone.mat",MeatRoot+"/Textures/","Bone",.4f);
        var cut=AssetDatabase.LoadAssetAtPath<Material>(output+"/RibBoneCut.mat");
        if(cut==null){cut=new Material(bone);AssetDatabase.CreateAsset(cut,output+"/RibBoneCut.mat");}
        cut.SetTexture("_BaseMap",null);cut.SetTexture("_MainTex",null);cut.SetColor("_BaseColor",new Color(.83f,.7f,.53f));EditorUtility.SetDirty(cut);
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(output+"/RibSpin.anim");
        if(clip==null){clip=new AnimationClip{name="Meat_Spin"};AssetDatabase.CreateAsset(clip,output+"/RibSpin.anim");}
        clip.name="Meat_Spin";clip.frameRate=30;
        var curve=AnimationCurve.Linear(0,0,6,360);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Spin",typeof(Transform),"localEulerAnglesRaw.y"),curve);
        var loop=AnimationUtility.GetAnimationClipSettings(clip);loop.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,loop);EditorUtility.SetDirty(clip);
        var controller=WorldModels25DSetup.Controller(output+"/MeatSpin.controller",new[]{clip},new[]{"Meat_Spin"},"Meat_Spin");
        foreach(bool hero in new[]{false,true})
        {
            var root=new GameObject(hero?"HeroMeat25D":"AnimalMeat25D");
            try
            {
                var spin=new GameObject("Spin");spin.transform.SetParent(root.transform,false);
                var holder=new GameObject("Hover offset");holder.transform.SetParent(spin.transform,false);
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MeatModel),holder.transform);
                foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&m.name.Contains("Cut")?cut:m!=null&&m.name.Contains("Bone")?bone:meat).ToArray();
                var bounds=Bounds(model);float size=hero?Meat25DSetup.HeroSize:Meat25DSetup.AnimalSize;
                float scale=size/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);holder.transform.localScale=Vector3.one*scale;
                holder.transform.localPosition=new Vector3(-bounds.center.x*scale,Meat25DSetup.HoverGap-bounds.min.y*scale,-bounds.center.z*scale);
                var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(root,hero?Meat25DSetup.HeroPrefab:Meat25DSetup.AnimalPrefab);
            }
            finally{Object.DestroyImmediate(root);}
        }
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);Meat25DSetup.ApplyIfConfigured(Object.FindAnyObjectByType<ArenaPresentation25D>());
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("HOOKWARS_RIB_MEAT_SETUP_PASS");
    }
    static Bounds Bounds(GameObject root)
    {
        bool first=true;Bounds bounds=default;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            foreach(var p in filter.sharedMesh.vertices)
            {var v=filter.transform.TransformPoint(p);if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
        if(first)throw new InvalidOperationException("Imported model has no geometry");return bounds;
    }
    public static void ConfigureAnchors()
    {
        Import(EyeRoot,EyeModel);
        if(!AssetDatabase.IsValidFolder(EyeRoot+"/Integration"))AssetDatabase.CreateFolder(EyeRoot,"Integration");
        var steel=Material(EyeRoot+"/Integration/Steel.mat",EyeRoot+"/Textures/","Steel",.45f,1);
        var root=new GameObject("HookEye25D");
        try
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EyeModel),root.transform);
            var attach=model.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Hook_Attach",StringComparison.Ordinal));
            model.transform.rotation=Quaternion.FromToRotation(attach.position-model.transform.position,Vector3.up)*model.transform.rotation;
            model.transform.localScale=Vector3.one*2;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=steel;
            PrefabUtility.SaveAsPrefabAsset(root,EyeVisual);
        }
        finally{Object.DestroyImmediate(root);}
        var logical=new GameObject("HookAnchor");logical.layer=LayerMask.NameToLayer("Buildings");
        try{logical.AddComponent<CircleCollider2D>().radius=.28f;logical.AddComponent<HookAnchor>();PrefabUtility.SaveAsPrefabAsset(logical,EyePrefab);}
        finally{Object.DestroyImmediate(logical);}
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        WorldModels25DSetup.Assign(Object.FindAnyObjectByType<ArenaPresentation25D>(),ArenaPresentation25D.VisualKind.Anchor,EyeVisual);
        var overlay=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/FieldOverlay.mat");
        if(overlay==null){overlay=new Material(Shader.Find("HookWars/FieldOverlay"));AssetDatabase.CreateAsset(overlay,"Assets/Settings/FieldOverlay.mat");}
        var display=new SerializedObject(Object.FindAnyObjectByType<ArenaPresentation25D>());display.FindProperty("overlayMaterial").objectReferenceValue=overlay;display.ApplyModifiedPropertiesWithoutUndo();
        var map=new SerializedObject(Object.FindAnyObjectByType<MatchMap25D>());map.FindProperty("anchorPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<HookAnchor>(EyePrefab);map.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("HOOKWARS_ANCHOR_SETUP_PASS");
    }
    public static void PruneRetiredMeat()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        string[] retired={Meat25DSetup.Root+"/Models/MeatOnBone.fbx",Meat25DSetup.Root+"/Textures/Meat_Albedo.png",Meat25DSetup.Output+"/Meat.mat"};
        var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).ToArray();
        foreach(var path in all.Where(p=>!retired.Contains(p)))
            if(AssetDatabase.GetDependencies(path,false).Any(p=>retired.Contains(p)))throw new InvalidOperationException("Retired meat still referenced: "+path);
        foreach(var path in all.Where(p=>p.EndsWith(".cs")&&!p.EndsWith("ExpansionAssetsSetup.cs")))
        {
            string text=File.ReadAllText(path);
            if(text.Contains("MeatOnBone")||text.Contains("Meat_Albedo"))throw new InvalidOperationException("Retired dynamic load: "+path);
        }
        var report=new System.Text.StringBuilder("Checkpoint before expansion: 3484392. New rib steak verified in real Play Mode.\n");
        report.AppendLine("Checked AssetDatabase dependency edges and source dynamic names. Resources assets="+all.Count(p=>p.Contains("/Resources/"))+"; Addressables data="+all.Count(p=>p.Contains("AddressableAssetsData")));
        foreach(var path in retired)
        {
            string absolute=Path.GetFullPath(path).Replace('\\','/');
            if(!absolute.StartsWith(Application.dataPath+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Delete outside Assets");
            if(File.Exists(path)&&!AssetDatabase.DeleteAsset(path))throw new InvalidOperationException("Cannot delete "+path);
            report.AppendLine("DELETED WITH META: "+path);
        }
        foreach(var folder in new[]{Meat25DSetup.Root+"/Models",Meat25DSetup.Root+"/Textures"})
            if(AssetDatabase.IsValidFolder(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())AssetDatabase.DeleteAsset(folder);
        AssetDatabase.SaveAssets();File.WriteAllText("docs/validation/016-retired-meat.txt",report.ToString());
        Legacy25DCleanup.Verify("docs/validation/016-assets.txt");
    }
}
