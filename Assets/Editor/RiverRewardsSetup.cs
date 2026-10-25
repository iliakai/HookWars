using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RiverRewardsSetup
{
    public const string Root="Assets/HookWarsCoinPickup",Visual=Root+"/Integration/RiverCoin25D.prefab",Coin="Assets/Prefabs/RiverCoin.prefab";
    [MenuItem("HookWars/25D/Setup river coins and stone boundaries")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        AssetDatabase.Refresh();
        string modelPath=Root+"/Models/Gold_Coin_Static.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.SaveAndReimport();
        foreach(string path in Directory.GetFiles(Root+"/Textures","*.png").Append("Assets/Settings/MountainRock.png"))
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            texture.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=path.Contains("BaseColor")||path.Contains("MountainRock");
            texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=8;
            texture.mipmapEnabled=true;texture.maxTextureSize=2048;texture.SaveAndReimport();
        }
        Material field=Gold("GoldField"),rim=Gold("GoldRim"),relief=Gold("GoldRelief");
        var visual=new GameObject("RiverCoin25D");
        try
        {
            var holder=new GameObject("Geometry");holder.transform.SetParent(visual.transform,false);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),holder.transform);
            var filter=model.GetComponentInChildren<MeshFilter>();filter.sharedMesh=CoinRenderMesh(filter.sharedMesh);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null&&m.name.Contains("Field")?field:m!=null&&m.name.Contains("Relief")?relief:rim).ToArray();
            Bounds bounds=Bounds(model);float scale=.64f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            holder.transform.localScale=Vector3.one*scale;holder.transform.localPosition=-bounds.center*scale;
            PrefabUtility.SaveAsPrefabAsset(visual,Visual);
        }
        finally{Object.DestroyImmediate(visual);}
        var logic=new GameObject("RiverCoin");
        try
        {
            logic.layer=LayerMask.NameToLayer("Carcasses");var collider=logic.AddComponent<CircleCollider2D>();collider.radius=.32f;collider.isTrigger=true;
            logic.AddComponent<RiverCoin>();PrefabUtility.SaveAsPrefabAsset(logic,Coin);
        }
        finally{Object.DestroyImmediate(logic);}
        var stone=GetMaterial("Assets/Settings/Arena25D-Mountains.mat");
        stone.SetColor("_BaseColor",Color.white);stone.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/MountainRock.png"));
        stone.SetFloat("_Metallic",0);stone.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(stone);
        var scene=EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();var data=new SerializedObject(display);
        data.FindProperty("mountainMaterial").objectReferenceValue=stone;
        var models=data.FindProperty("models");int index=-1;
        for(int i=0;i<models.arraySize;i++)if(models.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex==(int)ArenaPresentation25D.VisualKind.Coin)index=i;
        if(index<0){index=models.arraySize;models.arraySize++;}
        var entry=models.GetArrayElementAtIndex(index);entry.FindPropertyRelative("kind").enumValueIndex=(int)ArenaPresentation25D.VisualKind.Coin;
        entry.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Visual);data.ApplyModifiedPropertiesWithoutUndo();
        var stream=display.GetComponent<RiverCoinStream>()??display.gameObject.AddComponent<RiverCoinStream>();
        var streamData=new SerializedObject(stream);streamData.FindProperty("coinPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<RiverCoin>(Coin);
        streamData.FindProperty("presentation").objectReferenceValue=display;streamData.ApplyModifiedPropertiesWithoutUndo();
        if(display.GetComponent<MatchHud25D>()==null)display.gameObject.AddComponent<MatchHud25D>();
        foreach(var owner in Object.FindObjectsByType<PlayerProgression>())
        {var wallet=new SerializedObject(owner);wallet.FindProperty("goldPerSecond").floatValue=.5f;wallet.ApplyModifiedPropertiesWithoutUndo();}
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_RIVER_REWARDS_SETUP_PASS");
    }
    static Material GetMaterial(string path)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        return material;
    }
    static Mesh CoinRenderMesh(Mesh source)
    {
        // The donor FBX imports inside-out in Unity. Preserve the original file;
        // correct winding, normals and tangent handedness only in a render copy.
        var mesh=Object.Instantiate(source);mesh.name="Gold coin outward surfaces";
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var indices=mesh.GetTriangles(sub);
            for(int i=0;i<indices.Length;i+=3){int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}
            mesh.SetTriangles(indices,sub);
        }
        mesh.normals=mesh.normals.Select(n=>-n).ToArray();
        mesh.tangents=mesh.tangents.Select(t=>new Vector4(t.x,t.y,t.z,-t.w)).ToArray();
        string path=Root+"/Integration/GoldCoinRender.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
    static Material Gold(string prefix)
    {
        var material=GetMaterial(Root+"/Integration/"+prefix+".mat");material.SetColor("_BaseColor",Color.white);
        var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+prefix+"_BaseColor.png");
        material.SetTexture("_BaseMap",albedo);
        // With no studio reflection rig, fully metallic faces go almost black.
        // A restrained texture-colored fill keeps this pickup readable at game scale.
        material.SetTexture("_EmissionMap",albedo);material.SetColor("_EmissionColor",new Color(1.2f,1.2f,1.2f));
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        material.EnableKeyword(new UnityEngine.Rendering.LocalKeyword(material.shader,"_EMISSION"));
        var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+prefix+"_UnityMask.png");
        material.SetTexture("_MetallicGlossMap",mask);material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
        material.SetFloat("_Cull",0); // The rotating pickup must remain readable from both faces.
        material.SetTexture("_OcclusionMap",mask);material.SetFloat("_OcclusionStrength",1);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Gold_Normal.png"));
        material.SetFloat("_BumpScale",.6f);material.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(material);return material;
    }
    static Bounds Bounds(GameObject model)
    {
        bool first=true;Bounds bounds=default;
        foreach(var mesh in model.GetComponentsInChildren<MeshFilter>())foreach(var vertex in mesh.sharedMesh.vertices)
        {Vector3 point=mesh.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
        if(first)throw new InvalidOperationException("Coin model has no geometry");return bounds;
    }
}
