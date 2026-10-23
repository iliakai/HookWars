using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HookWars.Buildings;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class World25DChecks
{
    public const string Key="HookWars.WorldChecks";
    private static int phase,count;
    private static double ready;
    private static float due;
    private static GameBuilding[] buildings;
    private static PlayerProgression owner;
    static World25DChecks() { if(SessionState.GetBool(Key,false)) Subscribe(); }
    [MenuItem("HookWars/25D/World/Check buildings Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();
        WorldModels25DSetup.ConfigureBuildings(); string before=Signature();
        Directory.CreateDirectory("Temp/WorldIdempotence");
        foreach(string p in Directory.GetFiles(WorldModels25DSetup.Output)) File.Copy(p,"Temp/WorldIdempotence/"+Path.GetFileName(p),true);
        WorldModels25DSetup.ConfigureBuildings();
        foreach(string p in Directory.GetFiles(WorldModels25DSetup.Output)) if(!File.ReadAllBytes(p).SequenceEqual(File.ReadAllBytes("Temp/WorldIdempotence/"+Path.GetFileName(p)))) Debug.LogError("WORLD_CHANGED: "+p);
        Check(before==Signature(),"Building setup is byte stable");
        SessionState.SetBool(Key,true); SessionState.SetBool(Key+".Passed",false); Subscribe();
    }
    private static string Signature()=>string.Join("|",Directory.GetFiles(WorldModels25DSetup.Output).OrderBy(p=>p).Select(p=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))))+File.ReadAllText(Arena25DSetup.ScenePath);
    private static void Subscribe() { phase=0; count=0; ready=EditorApplication.timeSinceStartup+3; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); count++; Debug.Log("WORLD_CHECK: "+message); }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling)return;
            if(phase==99) { if(!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key,false); EditorApplication.update-=Tick; } return; }
            if(phase==0) { EditorApplication.isPlaying=true; phase=1; return; }
            if(!EditorApplication.isPlaying)return;
            if(phase==1)
            {
                owner=Object.FindAnyObjectByType<PlayerMovement>().GetComponent<PlayerProgression>();
                buildings=new GameBuilding[4];
                for(int i=0;i<4;i++)
                {
                    string[] paths={EconomySetup.FactoryPath,EconomySetup.ShopPath,EconomySetup.FarmPath,EconomySetup.WallPath};
                    buildings[i]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameBuilding>(paths[i]),new Vector3(-10+i*2,4),Quaternion.identity);
                    Check(buildings[i].Initialize(owner),"Initialize real economic building "+i);
                }
                due=Time.time+.2f; phase=2; return;
            }
            if(Time.time<due)return;
            var presentation=Object.FindAnyObjectByType<ArenaPresentation25D>();
            if(phase==2)
            {
                foreach(var building in buildings)
                {
                    var view=presentation.VisualFor(building); var visual=view.GetComponentInChildren<BuildingVisualController>();
                    Check(visual!=null,"Ready model and controller for "+building.Kind);
                    var foundation=view.GetComponentsInChildren<Renderer>(true).Single(r=>r.name.Contains("_Static"));
                    Check(foundation.bounds.size.x<=.961f && foundation.bounds.size.z<=.961f,"Full foundation fits 1m cell "+building.Kind);
                    Check(view.GetComponentsInChildren<Collider>(true).Length==0,"No demo 3D collider "+building.Kind);
                    Check(view.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("PreviewFX")).All(r=>!r.gameObject.activeInHierarchy),"PreviewFX disabled "+building.Kind);
                    Check(visual.IsActive==(building is AnimalFarm||building is BoneWall),"Empty economy selects correct initial mode "+building.Kind);
                }
                EconomyChecks.Invoke(buildings[0],"TryAcceptMass",2d);
                owner.GetComponent<OwnerEconomy>().AddOutput(2,0);
                due=Time.time+.15f; phase=3; return;
            }
            if(phase==3)
            {
                for(int i=0;i<2;i++)
                {
                    var visual=presentation.VisualFor(buildings[i]).GetComponentInChildren<BuildingVisualController>();
                    Check(visual.IsActive,"Economic work activates building "+i);
                    float t=visual.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    presentation.VisualFor(buildings[i]).GetComponentInChildren<BuildingPresentation25D>().Refresh();
                    Check(visual.animator.GetCurrentAnimatorStateInfo(0).normalizedTime==t,"Refresh does not restart animation");
                    Butcher25DChecks.CaptureAt(presentation.VisualFor(buildings[i]).position+Vector3.up*.3f,"building_"+i);
                }
                var stock=owner.GetComponent<OwnerEconomy>(); stock.TakeProduct(1000);
                buildings[0].enabled=false; buildings[1].enabled=false;
                due=Time.time+.4f; phase=4; return;
            }
            if(phase==4)
            {
                foreach(var building in buildings.Take(2))
                {
                    var visual=presentation.VisualFor(building).GetComponentInChildren<BuildingVisualController>();
                    Check(!visual.IsActive && visual.effects.All(p=>!p.isPlaying),"Disabled economic building closes and clears effects");
                }
                foreach(var building in buildings.Skip(2)) Butcher25DChecks.CaptureAt(presentation.VisualFor(building).position+Vector3.up*.3f,"building_"+building.Kind);
                var wallView=presentation.VisualFor(buildings[3]); Object.Destroy(buildings[3].gameObject);
                due=Time.time+.15f; phase=5; return;
            }
            if(phase==5)
            {
                Check(Object.FindObjectsByType<BuildingVisualController>().Length==3,"Destroyed wall removes model and effects");
                SessionState.SetBool(Key+".Passed",true); Debug.Log("HOOKWARS_WORLD_PASS: "+count+" building assertions in Unity Play Mode"); Stop();
            }
        }
        catch(Exception ex) { Debug.LogError("HOOKWARS_WORLD_FAIL: "+ex); Stop(); }
    }
    private static void Stop() { phase=99; EditorApplication.isPlaying=false; }
}
