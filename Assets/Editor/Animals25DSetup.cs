using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using HookWars.Animals;
using Object=UnityEngine.Object;

public static class Animals25DSetup
{
    public const string Root="Assets/HookWarsAnimals", Output=Root+"/Integration";
    public static readonly string[] Names={"Sheep","Cow","Pig"};
    [MenuItem("HookWars/25D/World/Setup animal models")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"Integration");
        for(int i=0;i<3;i++)Build(i);
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var presentation=Object.FindAnyObjectByType<ArenaPresentation25D>();
        for(int i=0;i<3;i++)
        {
            WorldModels25DSetup.Assign(presentation,(ArenaPresentation25D.VisualKind)(2+i),Output+"/"+Names[i]+".prefab");
            WorldModels25DSetup.Assign(presentation,(ArenaPresentation25D.VisualKind)(11+i),Output+"/"+Names[i]+"Carcass.prefab");
            string path="Assets/Prefabs/"+Names[i].ToLowerInvariant()+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { var data=new SerializedObject(root.GetComponent<Animal>()); data.FindProperty("species").intValue=i; data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        EditorSceneManager.SaveScene(presentation.gameObject.scene);AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_ANIMALS_SETUP_PASS: three live models and three static corpse prefabs");
    }
    private static void Build(int index)
    {
        string name=Names[index],path=Root+"/Models/"+name+".fbx";
        var clips=WorldModels25DSetup.Import(path);
        var controller=WorldModels25DSetup.Controller(Output+"/"+name+".controller",clips,new[]{"Walk","Graze"},"Graze");
        controller.parameters=new[]{new AnimatorControllerParameter{name="WalkRate",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
        var walk=controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Walk").state;
        walk.speedParameter="WalkRate";walk.speedParameterActive=true;EditorUtility.SetDirty(controller);
        var root=new GameObject(name+"25D");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform,false);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+name+".mat");
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Output+"/"+name+".mat");}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+"_Albedo.png")); mat.SetColor("_BaseColor",Color.white); mat.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(mat);
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers){r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();bounds.Encapsulate(r.bounds);r.gameObject.name="Surface_"+r.name;}
            float scale=.7f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale*=scale;model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            model.transform.position+=Vector3.up*.015f;
            var animator=model.GetComponent<Animator>();animator.applyRootMotion=false;animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var motion=root.AddComponent<AnimalMotion>();motion.animator=animator;motion.automaticSpeed=false;motion.referenceWalkSpeed=(index==1?.5384615f:.4786325f)*scale;
            root.AddComponent<AnimalPresentation25D>();
            PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
            SaveCorpse(root,name,mat);
            Debug.Log($"HOOKWARS_ANIMAL_IMPORT: {name} scale={scale:F6}, referenceWalkSpeed={motion.referenceWalkSpeed:F6}, clips={string.Join(",",clips.Select(c=>c.name+":"+c.length))}");
        }
        finally{Object.DestroyImmediate(root);}
    }
    // Skin with the actual bone matrices: BakeMesh's import-scale conventions
    // must not be applied a second time through a scaled renderer Transform.
    public static Mesh BakeWorld(SkinnedMeshRenderer skin,Transform relative)
    {
        var source=skin.sharedMesh;var vertices=source.vertices;var weights=source.boneWeights;
        var matrices=skin.bones.Select((b,i)=>relative.worldToLocalMatrix*b.localToWorldMatrix*source.bindposes[i]).ToArray();
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=vertices[i];var w=weights[i];
            vertices[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=source.triangles;mesh.uv=source.uv;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    private static void SaveCorpse(GameObject live,string name,Material material)
    {
        var root=new GameObject(name+"Carcass25D");
        try
        {
            foreach(var skin in live.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=BakeWorld(skin,live.transform);mesh.name=name+"CorpseMesh";
                string path=Output+"/"+name+"CorpseMesh.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){saved=mesh;AssetDatabase.CreateAsset(mesh,path);}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                var part=new GameObject("Body");part.transform.SetParent(root.transform,false);part.transform.localRotation=Quaternion.Euler(0,0,90);
                part.AddComponent<MeshFilter>().sharedMesh=saved;part.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            var parts=root.GetComponentsInChildren<Renderer>();Bounds bounds=parts[0].bounds;foreach(var p in parts)bounds.Encapsulate(p.bounds);
            float scale=.65f/Mathf.Max(bounds.size.x,bounds.size.z);
            foreach(var p in parts){p.transform.localScale*=scale;p.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;p.transform.position+=Vector3.up*.015f;}
            PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+"Carcass.prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }
}
