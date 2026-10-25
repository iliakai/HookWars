using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Launch25DChecks
{
    public const string Key="HookWars.LaunchChecks";
    static int phase,cycle,count,staleHits;
    static readonly int[] depths={4,16,20,16};
    static double ready,due,deadline,pausedGold;
    static float incomeTime;
    static double incomeGold;
    static bool snapshot;
    static PlayerProgression owner;
    static PlayerBuilding builder;
    static HookAbility hook;
    static HeroActionTiming25D timing;
    static Vector3 pausePosition;
    static Launch25DChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/Launch/Check match sessions Play Mode")]
    public static void Run()
    {
        Launch25DSetup.Configure();string before=File.ReadAllText(Launch25DSetup.MenuPath)+File.ReadAllText(Arena25DSetup.ScenePath)+File.ReadAllText("ProjectSettings/EditorBuildSettings.asset");
        Launch25DSetup.Configure();Check(before==File.ReadAllText(Launch25DSetup.MenuPath)+File.ReadAllText(Arena25DSetup.ScenePath)+File.ReadAllText("ProjectSettings/EditorBuildSettings.asset"),"Menu setup is byte stable");
        SessionState.SetInt(Key+".SavedDepth",MatchConfiguration.LastDepth);SessionState.SetBool(Key+".Finishing",false);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);ValidationPlayScope.CurrentScene();Subscribe();
    }
    static void Subscribe()
    {
        phase=SessionState.GetBool(Key+".Finishing",false)?99:0;cycle=count=staleHits=0;
        ready=EditorApplication.timeSinceStartup+3;deadline=ready+120;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static object Read(object target,string field)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);count++;Debug.Log("LAUNCH_CHECK: "+message);}
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!=MainMenuController.BattleScene)return;
        var player=Object.FindAnyObjectByType<PlayerMovement>();owner=player.GetComponent<PlayerProgression>();
        builder=player.GetComponent<PlayerBuilding>();hook=player.GetComponent<HookAbility>();timing=player.GetComponent<HeroActionTiming25D>();
        var stock=player.GetComponent<OwnerEconomy>();
        snapshot=owner.Gold==0&&owner.Experience==0&&owner.Level==1&&owner.UpgradePoints==0&&stock.ProductKg==0&&stock.Bones==0&&stock.BoneRemainder==0&&hook.Range==12&&player.GetComponent<CarcassCarrier>().Count==0;
        snapshot &= ArenaLayout.Depth==depths[cycle];
        var spawn=(Vector3)Read(player.GetComponent<PlayerRespawn>(),"spawnPosition");
        snapshot &= spawn==player.transform.position&&Mathf.Approximately(Mathf.Abs(spawn.x),2+Mathf.Min(4,depths[cycle]*.5f));
        foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
    }
    static void Capture(string label)
    {
        Directory.CreateDirectory("Temp/LaunchPreview");
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Show();
        ScreenCapture.CaptureScreenshot("Temp/LaunchPreview/"+label+".png");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling)return;
            if(phase==99)
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key,false);EditorApplication.update-=Tick;SceneManager.sceneLoaded-=Loaded;
                    MatchConfiguration.RememberDepth(SessionState.GetInt(Key+".SavedDepth",16));
                }
                return;
            }
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Launch cycle validation timed out at phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying)return;
            if(EditorApplication.timeSinceStartup<due)return;
            if(phase==1)
            {
                if(SceneManager.GetActiveScene().name!=MainMenuController.MenuScene||SceneManager.sceneCount!=1)return;
                Check(Object.FindObjectsByType<Health>(FindObjectsInactive.Include).Length==0&&Object.FindObjectsByType<AnimalFarm>().Length==0&&Object.FindObjectsByType<PlayerProgression>().Length==0,"Menu has no match actors, economy or farms");
                Check(Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length==0,"Menu retains no pooled physical bodies from previous match");
                Check(Time.timeScale==1&&staleHits==0,"Menu restores time scale and cancels old delayed hits");
                var menu=Object.FindAnyObjectByType<MainMenuController>();
                if(cycle==0)foreach(string invalid in new[]{"","3","21","4.5","abc","-5"})
                {menu.SetDepthText(invalid);Check(!menu.IsValid&&!menu.StartBattle()&&!menu.Loading,"Reject invalid depth: "+invalid);}
                menu.SetDepthText((cycle==3?20:depths[cycle]).ToString());Check(menu.IsValid,"Accept integer depth "+menu.DepthText);
                Capture("menu_"+cycle);due=EditorApplication.timeSinceStartup+.25;phase=2;return;
            }
            if(phase==2)
            {
                Check(File.Exists("Temp/LaunchPreview/menu_"+cycle+".png"),"Real Game View menu screenshot captured");
                snapshot=false;
                if(cycle==3)SceneManager.LoadSceneAsync(MainMenuController.BattleScene,LoadSceneMode.Single);
                else {var menu=Object.FindAnyObjectByType<MainMenuController>();Check(menu.StartBattle()&&!menu.StartBattle(),"Start button accepts exactly one asynchronous match load");}
                phase=3;due=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==3)
            {
                if(SceneManager.GetActiveScene().name!=MainMenuController.BattleScene||MatchPause.Instance==null)return;
                Check(snapshot,"Configuration and zeroed session exist after Awake before first Update at depth "+depths[cycle]);
                Check(Object.FindObjectsByType<ArenaLayout>().Length==1&&Object.FindObjectsByType<MatchPause>().Length==1&&Object.FindObjectsByType<ArenaPresentation25D>().Length==1,"One layout/session/presentation per battle");
                Check(owner.GoldPerSecond==.5f&&owner.ExperiencePerSecond==0,"New match keeps +0.5 gold/s and no passive XP");
                Check(Object.FindObjectsByType<GameBuilding>().Length==2&&Object.FindObjectsByType<SausageFactory>().Length==2&&Object.FindObjectsByType<Carcass>().Length==0&&Object.FindObjectsByType<ButcherHelper>().Length==0,"New match has exactly two fresh factories and no old buildings, helpers or corpses");
                Check((int)Read(Object.FindAnyObjectByType<TrainingMatch>(),"kills")==0&&(int)Read(Object.FindAnyObjectByType<TrainingMatch>(),"deaths")==0,"Score starts from zero");
                Check(Mathf.Abs(ArenaLayout.BankBounds(false).width-(depths[cycle]-1))<.001f,"Logical bank uses selected depth");
                var enemy=Object.FindAnyObjectByType<EnemyChase>();
                Check((Vector3)Read(Object.FindAnyObjectByType<TrainingMatch>(),"enemySpawnPosition")==enemy.transform.position,"Enemy respawn snapshot uses the new layout");
                owner.AddGold(1000);owner.GetComponent<OwnerEconomy>().AddOutput(5,10);owner.AddExperience(100);
                hook.TryUpgrade(HookAbility.UpgradeStat.Range);
                var farm=AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.FarmPath);builder.SelectBuilding(farm);
                var cell=new Vector3(-ArenaLayout.OuterEdge+.5f,8.5f);
                // Random permanent terrain legitimately reserves some edge cells.
                for(int row=0;row<20&&!builder.CanPlace(cell);row++)cell.y=9.5f-row;
                Check(builder.TryBuildAt(cell)!=null,"Build on extreme one-metre cell at selected depth");
                Check(owner.GetComponent<OwnerEconomy>().Factory.TryOrderHelper(),"Dirty match includes a live ordered helper before leaving");
                builder.SelectBuilding(farm);MatchPause.Instance.HandleEscape();
                Check(!builder.IsBuilding&&!MatchPause.Instance.IsPaused,"First Escape closes construction before pausing");
                incomeTime=Time.time;incomeGold=owner.Gold;
                due=EditorApplication.timeSinceStartup+.25;phase=4;return;
            }
            if(phase==4)
            {
                Check(Math.Abs(owner.Gold-incomeGold-.5*(Time.time-incomeTime))<.05,"Passive income uses game time exactly once");
                Check(timing.BeginAxe(.7f,()=>staleHits++),"Begin accepted delayed action before pause");
                MatchPause.Instance.HandleEscape();Check(MatchPause.Instance.IsPaused&&Time.timeScale==0,"Second Escape pauses the match");
                pausedGold=owner.Gold;pausePosition=owner.transform.position;
                Capture("pause_"+cycle);due=EditorApplication.timeSinceStartup+.3;phase=5;return;
            }
            if(phase==5)
            {
                Check(owner.Gold==pausedGold&&owner.transform.position==pausePosition&&staleHits==0&&timing.AxePending,"Pause freezes income, movement and pending action time");
                int sequence=timing.HookSequence;hook.TryThrow(Vector2.up);
                Check(timing.HookSequence==sequence&&!timing.BeginAxe(.7f,()=>staleHits++),"Paused input cannot accept attacks or casts");
                MatchPause.Instance.HandleEscape();timing.CancelAll();
                EconomyChecks.Set(hook,"nextThrowTime",0f);hook.TryThrow(Vector2.up);
                due=EditorApplication.timeSinceStartup+.5;phase=6;return;
            }
            if(phase==6)
            {
                Check(hook.ActiveHook!=null&&Object.FindObjectsByType<HookWars.Chain.PhysicalChain>().Any(c=>c.ActiveBodyCount>0),"A real hook and physical links are active before leaving battle");
                Capture("battle_"+cycle);due=EditorApplication.timeSinceStartup+.2;phase=8;return;
            }
            if(phase==8)
            {
                Object.FindAnyObjectByType<EnemyChase>().GetComponent<Health>().TakeDamage(10000);
                Check((int)Read(Object.FindAnyObjectByType<TrainingMatch>(),"kills")==1&&Object.FindObjectsByType<Carcass>().Length>0,"Dirty score and real corpse exist before unloading this session");
                timing.BeginAxe(.7f,()=>staleHits++);
                MatchPause.Instance.SetPaused(true);
                pausePosition=hook.ActiveHook.position;
                due=EditorApplication.timeSinceStartup+.2;phase=9;return;
            }
            if(phase==9)
            {
                Check(hook.ActiveHook.position==pausePosition&&hook.IsFlyingOut,"Pause preserves active hook position and flying state");
                Check(MatchPause.Instance.ReturnToMenu()&&!MatchPause.Instance.ReturnToMenu(),"Exit from pause accepts one load and cancels all match actions");
                cycle++;due=EditorApplication.timeSinceStartup+.25;
                if(cycle<depths.Length){phase=1;return;}
                phase=7;return;
            }
            if(phase==7)
            {
                if(SceneManager.GetActiveScene().name!=MainMenuController.MenuScene||SceneManager.sceneCount!=1)return;
                Check(Time.timeScale==1&&Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length==0&&staleHits==0,"Final menu has no residual physics, time scale or old delayed actions");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_LAUNCH_PASS: "+count+" assertions, 4/16/20m menu cycles and direct Arena25D default");Stop();
            }
        }
        catch(Exception error){Debug.LogError("HOOKWARS_LAUNCH_FAIL: "+error);Stop();}
    }
    static void Stop(){SessionState.SetBool(Key+".Finishing",true);phase=99;Time.timeScale=1;EditorApplication.isPlaying=false;}
}
