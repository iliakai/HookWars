using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class Legacy25DCleanup
{
    private static readonly HashSet<string> OldVisuals=new HashSet<string>{"PlayerBodyVisual","EnemyBodyVisual","MeleeSwingVisual","HookVisual","HookChain","HealthBarVisual","Light2D","PixelPerfectCamera"};
    public static void Migrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        var layout=Object.FindAnyObjectByType<ArenaLayout>();var data=new SerializedObject(layout);
        foreach(string side in new[]{"left","right"})
        {
            var old=data.FindProperty(side+"Mountains");var sprite=old!=null?old.objectReferenceValue as SpriteRenderer:null;
            if(sprite!=null)data.FindProperty(side+"Boundary").objectReferenceValue=sprite.GetComponent<BoxCollider2D>();
        }
        var legacy=data.FindProperty("logicalSurfacesOnly");if(legacy!=null)legacy.boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var root in layout.gameObject.scene.GetRootGameObjects())Strip(root);
        foreach(var builder in Object.FindObjectsByType<PlayerBuilding>())
        {
            var b=new SerializedObject(builder);
            foreach(string field in new[]{"minePrefab","universityPrefab"}){var p=b.FindProperty(field);if(p!=null)p.objectReferenceValue=null;}
            b.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(string label in new[]{"left bank","right bank","river","global light 2d","global light2d"})
        {
            var go=layout.gameObject.scene.GetRootGameObjects().SingleOrDefault(g=>g.name.ToLowerInvariant()==label);
            if(go!=null&&go.GetComponents<Component>().Length==1&&go.transform.childCount==0)Object.DestroyImmediate(go);
        }
        EditorSceneManager.SaveScene(layout.gameObject.scene);
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            if(path.EndsWith("/Mine.prefab")||path.EndsWith("/University.prefab"))continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                Strip(root);
                if(root.GetComponent<Carcass>()!=null)
                {
                    var circle=root.GetComponent<CircleCollider2D>();float radius=circle.radius*Mathf.Abs(root.transform.lossyScale.x);
                    root.transform.localScale=Vector3.one;circle.radius=radius;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(Arena25DSetup.PipelinePath);
        GraphicsSettings.defaultRenderPipeline=pipeline;
        int current=QualitySettings.GetQualityLevel();
        for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
        QualitySettings.SetQualityLevel(current,false);AssetDatabase.SaveAssets();
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var defaultScene=settings.FindProperty("templateDefaultScene");if(defaultScene!=null)defaultScene.stringValue=Launch25DSetup.MenuPath;
        settings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
        Debug.Log("HOOKWARS_CLEANUP_MIGRATE_PASS: sprite components and legacy drivers removed; 3D pipeline assigned for player shader variants");
    }
    private static void Strip(GameObject root)
    {
        foreach(var component in root.GetComponentsInChildren<Component>(true).OrderBy(c=>c is SpriteRenderer||c is Animator||c is LineRenderer?1:0))
        {
            if(component==null)continue;
            if(component is SpriteRenderer||component is LineRenderer||component is Animator||OldVisuals.Contains(component.GetType().Name))
                Object.DestroyImmediate(component);
        }
    }
    public static void Audit(bool delete)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode");
        string[] files={"Assets/Scenes/Arena.unity","Assets/Scenes/SampleScene.unity","Assets/Prefabs/Mine.prefab","Assets/Prefabs/University.prefab",
            "Assets/Materials/RiverFlowMaterial.mat","Assets/Shaders/RiverFlow.shader","Assets/Settings/UniversalRP.asset","Assets/Settings/Renderer2D.asset",
            "Assets/Settings/Scenes/URP2DSceneTemplate.unity","Assets/Settings/Lit2DSceneTemplate.scenetemplate","Assets/Settings/Arena25D-Chain.mat",
            "Assets/Scripts/PlayerBodyVisual.cs","Assets/Scripts/EnemyBodyVisual.cs","Assets/Scripts/MeleeSwingVisual.cs","Assets/Scripts/HookVisual.cs",
            "Assets/Scripts/HookChain.cs","Assets/Scripts/HealthBarVisual.cs","Assets/Scripts/IncomeBuilding.cs","Assets/Scripts/MineBuilding.cs"};
        string[] all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).ToArray();
        var candidates=new HashSet<string>(all.Where(p=>p.StartsWith("Assets/Textures/")||p.StartsWith("Assets/Animations/")||files.Contains(p)));
        if(candidates.Count==0){Debug.Log("HOOKWARS_CLEANUP_AUDIT_PASS: no obsolete candidates remain");return;}
        var reasons=new Dictionary<string,HashSet<string>>();
        void Keep(string asset,string reason){if(!reasons.TryGetValue(asset,out var list))reasons[asset]=list=new HashSet<string>();list.Add(reason);}
        foreach(string path in all.Where(p=>!candidates.Contains(p)))
            foreach(string dependency in AssetDatabase.GetDependencies(path,false))
                if(candidates.Contains(dependency))Keep(dependency,"asset dependency: "+path);
        var guidMap=candidates.ToDictionary(AssetDatabase.AssetPathToGUID,p=>p);
        foreach(string path in Directory.GetFiles("ProjectSettings").Where(p=>p.EndsWith(".asset")||p.EndsWith(".json")))
        {
            string source=File.ReadAllText(path);
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source,"[0-9a-f]{32}"))
                if(guidMap.TryGetValue(match.Value,out string asset))Keep(asset,"project settings GUID: "+path);
            foreach(string asset in candidates)if(source.Contains(asset))Keep(asset,"project settings path: "+path);
        }
        foreach(string path in all.Where(p=>p.EndsWith(".cs")&&!candidates.Contains(p)&&p!="Assets/Editor/Legacy25DCleanup.cs"))
        {
            string source=File.ReadAllText(path);
            foreach(string asset in candidates)if(source.Contains(asset))Keep(asset,"code path (review loader): "+path);
            if(source.Contains("SceneManager.LoadScene")&&(source.Contains("\"Arena\"")||source.Contains("\"SampleScene\"")))
                throw new InvalidOperationException("Old scene loaded by name: "+path);
            if(source.Contains("Resources.Load")||source.Contains("Addressables.Load"))
                foreach(string asset in candidates)if(source.Contains("\""+Path.GetFileNameWithoutExtension(asset)+"\""))Keep(asset,"dynamic load by name: "+path);
        }
        // Preserve the full dependency closure of any genuinely shared asset.
        bool changed=true;
        while(changed)
        {
            int before=reasons.Count;
            foreach(string asset in reasons.Keys.ToArray())foreach(string dependency in AssetDatabase.GetDependencies(asset,true))
                if(candidates.Contains(dependency)&&dependency!=asset)Keep(dependency,"dependency of retained: "+asset);
            changed=before!=reasons.Count;
        }
        var report=new System.Text.StringBuilder("Checkpoint: checkpoint/before-legacy-cleanup-20261023 (35d6ef1)\n");
        report.AppendLine("Audit: AssetDatabase dependencies, ProjectSettings GUID/path refs, source path/string loaders, Resources and Addressables.");
        report.AppendLine("Resources assets="+all.Count(p=>p.Contains("/Resources/"))+"; Addressables data="+all.Count(p=>p.Contains("/AddressableAssetsData/")));
        report.AppendLine("Welcome assets are retained: Editor landing-page configuration is outside gameplay cleanup, including user settings.");
        foreach(string asset in candidates.OrderBy(p=>p))report.AppendLine((reasons.TryGetValue(asset,out var why)?"RETAIN "+asset+" <- "+string.Join("; ",why):"UNREFERENCED "+asset));
        Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/008-legacy-asset-audit.txt",report.ToString());
        if(delete)
        {
            if(reasons.ContainsKey("Assets/Scenes/Arena.unity")||reasons.ContainsKey("Assets/Scenes/SampleScene.unity"))throw new InvalidOperationException("Old scene still referenced; see audit");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string asset in candidates.Where(p=>!reasons.ContainsKey(p)).OrderBy(p=>p.EndsWith(".cs")?1:0).ThenBy(p=>p))
                {
                    string absolute=Path.GetFullPath(asset).Replace('\\','/');
                    if(!absolute.StartsWith(Application.dataPath+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Deletion outside Assets: "+absolute);
                    if(!AssetDatabase.DeleteAsset(asset))throw new InvalidOperationException("Cannot delete "+asset);
                    if(File.Exists(asset)||File.Exists(asset+".meta"))throw new InvalidOperationException("Asset/meta remained: "+asset);
                    report.AppendLine("DELETED WITH META "+asset);
                }
                foreach(string folder in new[]{"Assets/Textures","Assets/Animations","Assets/Materials","Assets/Shaders","Assets/Settings/Scenes"})
                {
                    string absolute=Path.GetFullPath(folder).Replace('\\','/');
                    if(!absolute.StartsWith(Application.dataPath+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Folder outside Assets: "+absolute);
                    // Only prune physically empty folders. A new shared asset must
                    // never become a casualty of this one-time migration.
                    if(AssetDatabase.IsValidFolder(folder)&&!Directory.EnumerateFileSystemEntries(absolute).Any())AssetDatabase.DeleteAsset(folder);
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            File.WriteAllText("docs/validation/008-legacy-asset-audit.txt",report.ToString());AssetDatabase.Refresh();
        }
        Debug.Log("HOOKWARS_CLEANUP_AUDIT_PASS: candidates="+candidates.Count+", retained="+reasons.Count+", deleted="+(delete?candidates.Count-reasons.Count:0));
    }
    public static void Verify(string reportPath="docs/validation/008-cleanup-verification.txt")
    {
        string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        int checkedObjects=0;
        void CheckRoot(GameObject root,bool logic)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                checkedObjects++;
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException("Missing script: "+t.name);
            }
            if(root.GetComponentsInChildren<SpriteRenderer>(true).Length>0||root.GetComponentsInChildren<LineRenderer>(true).Length>0)
                throw new InvalidOperationException("Legacy graphics remain: "+root.name);
            if(logic&&root.GetComponentsInChildren<Animator>(true).Length>0)throw new InvalidOperationException("Legacy logical animator: "+root.name);
        }
        foreach(string path in new[]{Launch25DSetup.MenuPath,Arena25DSetup.ScenePath})
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects())CheckRoot(root,true);
        }
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/HookWarsButcher/Integration","Assets/HookWarsAnimals/Integration","Assets/HookWarsBuildings/Integration","Assets/HookWarsRocks/Integration","Assets/HookWarsMeat/Integration","Assets/HookWarsHookEye/Integration","Assets/HookWarsCoinPickup/Integration","Assets/HookWarsBarracks/Integration","Assets/HookWarsSausage/Integration","Assets/HookWarsHotdogBridge/Integration"}).Select(AssetDatabase.GUIDToAssetPath))
            CheckRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path),path.StartsWith("Assets/Prefabs/"));
        var missing=new List<string>();
        foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&(p.EndsWith(".unity")||p.EndsWith(".prefab")||p.EndsWith(".mat"))))
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path),"guid: ([0-9a-f]{32})"))
                if(!match.Groups[1].Value.StartsWith("0000000000000000")&&AssetDatabase.GUIDToAssetPath(match.Groups[1].Value)=="")missing.Add(path+": "+match.Groups[1].Value);
        if(missing.Count>0)throw new InvalidOperationException("Unresolved GUIDs:\n"+string.Join("\n",missing));
        if(File.Exists("Assets/Scenes/Arena.unity")||File.Exists("Assets/Scenes/SampleScene.unity"))throw new InvalidOperationException("Old scenes still exist");
        if(EditorBuildSettings.globalScenes.Length!=2||EditorBuildSettings.globalScenes[0].path!=Launch25DSetup.MenuPath||EditorBuildSettings.globalScenes[1].path!=Arena25DSetup.ScenePath)
            throw new InvalidOperationException("Unexpected build scenes");
        EditorSceneManager.OpenScene(File.Exists(previous)?previous:Launch25DSetup.MenuPath);
        File.WriteAllText(reportPath,"Checked hierarchy objects="+checkedObjects+"\nMissing scripts=0\nWorld sprites/line renderers/GIF animators=0\nUnresolved scene/prefab/material GUIDs=0\nBuild scenes: MainMenu, Arena25D\n");
        Debug.Log("HOOKWARS_CLEANUP_VERIFY_PASS: "+checkedObjects+" hierarchy objects, no missing scripts or GUIDs");
    }
}
