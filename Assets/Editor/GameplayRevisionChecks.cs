using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GameplayRevisionChecks
{
    public const string Key="HookWars.RevisionChecks";
    static int phase,count;
    static double ready,deadline;
    static float due;
    static PlayerBuilding builder;
    static GameBuilding first;
    static GameplayRevisionChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run(string mode)
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetString(Key+".Mode",mode);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+120;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);count++;Debug.Log("REVISION_CHECK: "+text);}
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Revision checks timeout");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                builder=Object.FindAnyObjectByType<PlayerBuilding>();
                if(SessionState.GetString(Key+".Mode","")=="construction")ConstructionStart();
                else throw new InvalidOperationException("Unsupported revision mode");
                due=Time.time+.25f;phase=2;return;
            }
            if(phase==2)
            {
                var display=Object.FindAnyObjectByType<ArenaPresentation25D>();var view=display.VisualFor(first);
                Check(view!=null&&Quaternion.Angle(view.transform.rotation,Quaternion.Euler(0,90,0))<.01f,"Building yaw maps clockwise XY rotation to XZ model");
                Check(builder.Preview!=null&&builder.IsBuilding,"Repeated placement retains one preview and selection");
                var hook=builder.GetComponent<PlayerHook>();hook.TryThrowAtScreenPoint(Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(new Vector2(-8,-5))));
                Check(!hook.IsPreparingThrow&&hook.ActiveHook==null,"Placement clicks cannot also cast a hook");
                MatchPause.Instance.HandleEscape();Check(!builder.IsBuilding&&!MatchPause.Instance.IsPaused,"First Esc ends repeated placement without pausing");
                MatchPause.Instance.HandleEscape();Check(MatchPause.Instance.IsPaused,"Next Esc opens pause");MatchPause.Instance.SetPaused(false);
                Directory.CreateDirectory("Temp/RevisionPreview");Butcher25DChecks.CaptureAt(ArenaSpace25D.ToWorld(first.transform.position,.5f),"revision_rotated_buildings",3);
                Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/009-construction.txt",$"PASS: {count} assertions in real Unity Play Mode\nRepeated grid placement, rotation, matching model yaw, occupied-cell rejection, resource spending, hook/UI isolation and Esc priority.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_REVISION_PASS: "+count+" construction assertions");phase=3;due=Time.time+.2f;return;
            }
            if(phase==3)Stop();
        }
        catch(Exception error){Debug.LogError("HOOKWARS_REVISION_FAIL: "+error);Stop();}
    }
    static void ConstructionStart()
    {
        var owner=builder.GetComponent<PlayerProgression>();owner.AddGold(1000);
        var shop=AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.ShopPath);builder.SelectBuilding(shop);builder.RotatePlacement();
        var startGold=owner.Gold;
        Check(builder.QuarterTurns==1&&builder.CanPlace(new Vector3(-10.5f,-6.5f)),"R selects a valid 90 degree placement");
        first=builder.TryBuildAt(new Vector3(-10.5f,-6.5f));Check(first!=null&&builder.IsBuilding,"First placement preserves build mode");
        Check(Mathf.Abs(first.transform.eulerAngles.z-270)<.01f,"Logical root stores quarter-turn rotation");
        Check(!builder.CanPlace(first.transform.position)&&builder.TryBuildAt(first.transform.position)==null,"Repeated click rejects occupied cell");
        var second=builder.TryBuildAt(new Vector3(-8.5f,-6.5f));Check(second!=null&&second!=first,"Same selection builds a second building");
        Check(Math.Abs(owner.Gold-(startGold-2*shop.BuildCost))<.001,"Exactly one payment for each accepted building");
        for(int i=0;i<3;i++)builder.RotatePlacement();Check(builder.QuarterTurns==0,"Four rotations restore original orientation");
        Check(!builder.CanPlace(new Vector3(-8.25f,-4.5f)),"Off-grid placement remains rejected");
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
}
