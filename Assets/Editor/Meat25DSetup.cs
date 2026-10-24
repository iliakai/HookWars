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
    public static void Configure()
    {
        if(System.IO.File.Exists(ExpansionAssetsSetup.MeatModel)){ExpansionAssetsSetup.ConfigureMeat();return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"Integration");
        var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/Meat_Albedo.png");
        textureImporter.textureType=TextureImporterType.Default;textureImporter.sRGBTexture=true;
        textureImporter.mipmapEnabled=true;textureImporter.alphaIsTransparency=false;textureImporter.SaveAndReimport();
        string path=Root+"/Models/MeatOnBone.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        var clips=WorldModels25DSetup.Import(path);
        var clip=clips.Single(c=>Butcher25DSetup.Matches(c.name,"Meat_Spin"));
        if(Mathf.Abs(clip.length-4)>.002f||!clip.isLooping||clip.events.Length!=0)throw new InvalidOperationException("Invalid meat spin clip");
        var controller=WorldModels25DSetup.Controller(Output+"/MeatSpin.controller",clips,new[]{"Meat_Spin"},"Meat_Spin");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Meat.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"/Meat.mat");}
        material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Meat_Albedo.png"));
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(material);
        Build(AnimalPrefab,path,material,controller,AnimalSize);Build(HeroPrefab,path,material,controller,HeroSize);
        // Retire only our uncommitted static prototype, not source FBX/metadata.
        string oldMesh=Output+"/Meat.asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(oldMesh)!=null)AssetDatabase.DeleteAsset(oldMesh);
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);ApplyIfConfigured(Object.FindAnyObjectByType<ArenaPresentation25D>());
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("HOOKWARS_MEAT_SETUP_PASS: authored 4s spin; meat .5/.9m; bottom .25m above ground");
    }
    static void Build(string path,string fbx,Material material,RuntimeAnimatorController controller,float size)
    {
        var root=new GameObject(path==AnimalPrefab?"AnimalMeat25D":"HeroMeat25D");
        try
        {
            var holder=new GameObject("Hover offset");holder.transform.SetParent(root.transform,false);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),holder.transform);model.name="Meat";
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
            var animator=model.GetComponent<Animator>()??model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(skins.Length==0)throw new InvalidOperationException("Meat FBX has no skinned geometry");
            Bounds bounds=default;bool first=true;
            foreach(var skin in skins)
            {
                var mesh=Animals25DSetup.BakeWorld(skin,root.transform);
                if(first){bounds=mesh.bounds;first=false;}else bounds.Encapsulate(mesh.bounds);Object.DestroyImmediate(mesh);
                skin.updateWhenOffscreen=true;
            }
            float scale=size/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);holder.transform.localScale=Vector3.one*scale;
            holder.transform.localPosition=new Vector3(-bounds.center.x*scale,HoverGap-bounds.min.y*scale,-bounds.center.z*scale);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void ApplyIfConfigured(ArenaPresentation25D display)
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(AnimalPrefab)==null||AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab)==null)return;
        foreach(var kind in new[]{ArenaPresentation25D.VisualKind.SheepCarcass,ArenaPresentation25D.VisualKind.CowCarcass,ArenaPresentation25D.VisualKind.PigCarcass})
            WorldModels25DSetup.Assign(display,kind,AnimalPrefab);
        WorldModels25DSetup.Assign(display,ArenaPresentation25D.VisualKind.Carcass,HeroPrefab);
    }
}
