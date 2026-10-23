using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class Chain25DSetup
{
    public const string Root="Assets/HookWarsChain",Output=Root+"/Integration";
    [MenuItem("HookWars/25D/World/Setup physical chain")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"Integration");
        var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/ChainLink.fbx"));
        try
        {
            var filter=source.GetComponentsInChildren<MeshFilter>().Single();
            Mesh copy=Object.Instantiate(filter.sharedMesh);copy.name="ChainLink25D";
            Vector3[] points=copy.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            Bounds bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            Vector3 axis=bounds.size.y>bounds.size.z&&bounds.size.y>bounds.size.x?Vector3.up:bounds.size.x>bounds.size.z?Vector3.right:Vector3.forward;
            Quaternion rotation=Quaternion.FromToRotation(axis,Vector3.forward);
            float factor=.24f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            copy.vertices=points.Select(v=>rotation*(v-bounds.center)*factor).ToArray();copy.RecalculateNormals();copy.RecalculateBounds();
            string path=Output+"/ChainLink25D.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){saved=copy;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(copy,saved);Object.DestroyImmediate(copy);EditorUtility.SetDirty(saved);}
            var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/ChainSteel.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"/ChainSteel.mat");}
            material.SetColor("_BaseColor",new Color(.32f,.35f,.39f));material.SetFloat("_Metallic",.8f);material.SetFloat("_Smoothness",.4f);EditorUtility.SetDirty(material);
            EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);var presentation=Object.FindAnyObjectByType<ArenaPresentation25D>();
            var data=new SerializedObject(presentation);data.FindProperty("physicalChainMesh").objectReferenceValue=saved;data.FindProperty("physicalChainMaterial").objectReferenceValue=material;data.ApplyModifiedPropertiesWithoutUndo();
            if(presentation.GetComponent<ChainEnvironment25D>()==null)presentation.gameObject.AddComponent<ChainEnvironment25D>();
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
            foreach(var pair in new[]{(24,"Chain25D"),(25,"ChainEnvironment25D")})
            {
                var layer=layers.GetArrayElementAtIndex(pair.Item1);if(layer.stringValue!=""&&layer.stringValue!=pair.Item2)throw new InvalidOperationException("Layer occupied: "+pair.Item1);layer.stringValue=pair.Item2;
            }
            tags.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(presentation.gameObject.scene);AssetDatabase.SaveAssets();
            Debug.Log("HOOKWARS_CHAIN_SETUP_PASS: imported link="+bounds+", derived="+saved.bounds+", pitch=.168m");
        }
        finally{Object.DestroyImmediate(source);}
    }
}
