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
public static class RiverRewardsChecks
{
    public const string Key="HookWars.RiverRewardsChecks";
    static int phase,count;
    static double ready,deadline,pausedUntil,frozenClock,frozenSpawn,enemyGold;
    static float due;
    static Vector3 frozenCoin;
    static PlayerProgression owner;
    static PlayerHook hook;
    static RiverCoinStream stream;
    static MatchHud25D hud;
    static ArenaPresentation25D display;
    static RiverCoin visualCoin,probe,aiCoin;
    static EnemyChase enemy;
    static RiverRewardsChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe()
    {phase=count=0;due=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+90;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string message)
    {if(!value)throw new InvalidOperationException(message);count++;Debug.Log("RIVER_REWARDS_CHECK: "+message);}
    static bool Near(double a,double b)=>Math.Abs(a-b)<.0001;
    static object Call(HookAbility ability,string method,params object[] args)=>typeof(HookAbility).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ability,args);
    static void Catch(RiverCoin coin,bool returning)
    {
        hook.CancelHook();hook.transform.position=new Vector3(-4,coin.transform.position.y);
        Call(hook,"ReleaseThrow",Vector2.right);
        if(returning)
        {hook.ActiveHook.position=coin.transform.position+Vector3.right;Check((bool)Call(hook,"TryCatchOnReturn",hook.transform.position,20f),"Returning hook catches the actual coin collider");}
        else{EconomyChecks.Set(hook,"speed",20f/Time.deltaTime);Call(hook,"FlyHook");}
    }
    static void Stop(){MatchPause.Instance?.SetPaused(false);phase=99;EditorApplication.isPlaying=false;}
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("River rewards checks timed out");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                foreach(var carrier in Object.FindObjectsByType<CarcassCarrier>())carrier.enabled=false;
                hook=Object.FindAnyObjectByType<PlayerHook>();owner=hook.GetComponent<PlayerProgression>();
                hook.GetComponent<PlayerMovement>().enabled=false;hook.GetComponentInChildren<PlayerAim>().enabled=false;
                display=Object.FindAnyObjectByType<ArenaPresentation25D>();stream=Object.FindAnyObjectByType<RiverCoinStream>();stream.enabled=false;
                hud=MatchHud25D.Instance;enemy=Object.FindAnyObjectByType<EnemyChase>();
                Check(owner.GoldPerSecond==.5f&&enemy.GetComponent<PlayerProgression>().GoldPerSecond==.5f,"Both heroes start with half a gold per second");
                Check(hud!=null&&hud.ElapsedSeconds<3&&hud.PopupCount==0,"Fresh match has a zero-based clock without old reward labels");
                Check(MatchHud25D.FormatTime(3661)=="01:01:01"&&MatchHud25D.FormatTime(90061)=="25:01:01","Clock formats hours, minutes and seconds without wrapping at a day");
                var rect=MatchHud25D.ScreenRect;Check(GamePointer.OverInterface(new Vector2(rect.center.x,Screen.height-rect.center.y)),"Top-center clock blocks gameplay clicks");
                var mountain=display.transform.Find("Generated 25D visuals/left mountains");
                if(mountain==null)return;
                Check(mountain.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/MountainRock.png"),"Mountain boundary uses the supplied stone texture");
                var uv=mountain.GetComponent<MeshFilter>().sharedMesh.uv;
                Check(uv.Max(p=>p.x)-uv.Min(p=>p.x)>9,"Long mountain face repeats stone instead of stretching one image");
                Butcher25DChecks.CaptureAt(mountain.position,"mountain_stone",2);
                File.Copy("Temp/ButcherPreview/mountain_stone.png","docs/validation/022-mountain.png",true);
                Check(stream.SecondsUntilSpawn>=25&&stream.SecondsUntilSpawn<=35,"First spawn interval lies between twenty-five and thirty-five seconds");
                stream.Advance(stream.SecondsUntilSpawn-.01);Check(Object.FindObjectsByType<RiverCoin>().Length==0,"No coin appears before the first interval");
                stream.Advance(.02);Check(Object.FindObjectsByType<RiverCoin>().Length==1&&stream.SecondsUntilSpawn>=25&&stream.SecondsUntilSpawn<=35,"Interval completion creates one coin and schedules a new random interval");
                stream.Advance(1000);Check(Object.FindObjectsByType<RiverCoin>().Length==2,"Overdue spawner does not release a queued burst");
                foreach(var coin in Object.FindObjectsByType<RiverCoin>())Object.DestroyImmediate(coin.gameObject);
                bool values=true,positions=true;var amounts=new System.Collections.Generic.HashSet<int>();
                for(int i=0;i<48;i++)
                {
                    var sample=stream.SpawnOne();values&=sample.Value>=25&&sample.Value<=75;amounts.Add(sample.Value);
                    positions&=Math.Abs(sample.transform.position.x)<=1.59f&&sample.transform.position.y>ArenaLayout.FieldHeight*.5f;
                    Object.DestroyImmediate(sample.gameObject);
                }
                Check(values&&amounts.Count>5,"Spawned coin rewards are varied integers within inclusive bounds");
                Check(positions,"Coins enter at random water positions above the river");
                visualCoin=stream.SpawnOne();visualCoin.transform.position=Vector3.zero;int value=visualCoin.Value;
                visualCoin.Advance(2);Check(Near(visualCoin.transform.position.y,-1.2)&&visualCoin.Value==value,"Coin follows water downward without rerolling its reward");
                visualCoin.transform.position=Vector3.zero;phase=2;due=Time.time+.15f;return;
            }
            if(phase==2)
            {
                var view=display.VisualFor(visualCoin);Check(view!=null,"Imported coin has a detached 3D presentation");
                var meshes=view.GetComponentsInChildren<MeshRenderer>();
                Check(meshes[0].sharedMaterials.All(m=>m.GetFloat("_Cull")==0&&m.IsKeywordEnabled("_EMISSION")),"Rotating coin retains both-face rendering and readable gold fill after import");
                Check(meshes.Length==1&&meshes[0].sharedMaterials.Length==3&&meshes[0].sharedMaterials.All(m=>m.shader.isSupported&&m.GetTexture("_BaseMap")!=null),"Coin uses the authored mesh with three supported textured gold materials");
                Check(meshes[0].bounds.min.y>.2f&&view.GetComponentsInChildren<Collider>().Length==0&&view.GetComponentsInChildren<Animator>().Length==0,"Coin graphics hover above water without extra physics or duplicate animation");
                view.Find("Model").localRotation=Quaternion.Euler(0,150,0);
                Butcher25DChecks.CaptureAt(view.position+Vector3.up*.62f,"river_coin",.65f);File.Copy("Temp/ButcherPreview/river_coin.png","docs/validation/022-coin.png",true);
                foreach(var fixture in Object.FindObjectsByType<GameBuilding>())foreach(var collider in fixture.GetComponents<Collider2D>())collider.enabled=false;
                foreach(var fixture in Object.FindObjectsByType<PermanentRock>())foreach(var collider in fixture.GetComponents<Collider2D>())collider.enabled=false;
                foreach(var fixture in Object.FindObjectsByType<HookAnchor>())foreach(var collider in fixture.GetComponents<Collider2D>())collider.enabled=false;
                for(int mode=0;mode<2;mode++)
                {
                    var desired=mode==0?HookAbility.PullMode.TargetToOwner:HookAbility.PullMode.OwnerToTarget;
                    if(hook.SelectedPullMode!=desired)Check(hook.TryTogglePullMode(),"Select blue mode for coin exception check");
                    var coin=stream.SpawnOne();coin.transform.position=new Vector3(0,2+mode);coin.Initialize(mode==0?25:75,stream.FlowSpeed);
                    double gold=owner.Gold,xp=owner.Experience;int popups=hud.PopupCount;Vector3 start=coin.transform.position;
                    Catch(coin,mode==1);
                    Check(hook.IsPullingTarget(coin.transform)&&!hook.IsSelfPulling,"Coin always pulls to owner in selected mode "+mode);
                    Check(owner.Gold==gold&&owner.Experience==xp&&hud.PopupCount==popups,"Capture alone grants no gold, experience or reward label");
                    coin.Advance(100);Check(coin.transform.position==start,"Captured coin stops drifting and cannot wash away");
                    if(mode==0)
                    {
                        hook.CancelHook();Check(coin.IsAvailable&&coin.transform.position==start&&owner.Gold==gold,"Cancellation restores the coin to its capture point without reward");
                        coin.Advance(.5);Check(coin.transform.position.y<start.y,"Released coin resumes river flow");Catch(coin,false);
                    }
                    var rival=enemy.GetComponent<HookAbility>();
                    Check(!coin.GetComponent<HookVictim>().TryCapture(rival)&&!(bool)typeof(RiverCoin).GetMethod("TryCollect",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(coin,new object[]{rival}),"A second hero cannot steal an already hooked coin");
                    EconomyChecks.Set(hook,"pullSpeed",100f/Time.deltaTime);Call(hook,"PullTarget");
                    Check(!coin.isActiveAndEnabled&&Near(owner.Gold-gold,coin.Value)&&owner.Experience==xp,"Delivery awards the exact integer value once, without XP");
                    Check(!(bool)typeof(RiverCoin).GetMethod("TryCollect",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(coin,new object[]{hook})&&Near(owner.Gold-gold,coin.Value),"Repeated collection cannot duplicate gold");
                    Check(hud.PopupCount==popups+1,"Exactly one rising reward label is queued per delivered coin");
                    if(mode==0){hud.Advance(1.1);Check(hud.PopupCount==0,"Reward label retires after one game second without granting more gold");}
                }
                visualCoin.transform.position=new Vector3(0,-10.55f);visualCoin.Advance(.2);
                Check(!visualCoin.isActiveAndEnabled,"Missed coin retires beyond the river exit");
                phase=3;due=Time.time+.1f;return;
            }
            if(phase==3)
            {
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
                ScreenCapture.CaptureScreenshot("docs/validation/022-clock-popup.png");
                phase=4;due=Time.time+.1f;return;
            }
            if(phase==4)
            {
                Check(visualCoin==null,"Washed-away coin is actually destroyed on game frames");
                probe=stream.SpawnOne();probe.transform.position=new Vector3(-1,0);
                MatchPause.Instance.SetPaused(true);frozenClock=hud.ElapsedSeconds;frozenSpawn=stream.SecondsUntilSpawn;frozenCoin=probe.transform.position;
                hud.Advance(100);stream.Advance(100);probe.Advance(100);
                Check(hud.ElapsedSeconds==frozenClock&&stream.SecondsUntilSpawn==frozenSpawn&&probe.transform.position==frozenCoin,"Pause blocks manual clock, spawning and river advancement");
                pausedUntil=EditorApplication.timeSinceStartup+.3;phase=5;return;
            }
            if(phase==5)
            {
                if(EditorApplication.timeSinceStartup<pausedUntil)return;
                Check(hud.ElapsedSeconds==frozenClock&&probe.transform.position==frozenCoin&&hud.PopupCount==1,"Native paused frames freeze the clock, coin and reward fade");
                MatchPause.Instance.SetPaused(false);hud.Advance(1.1);Check(hud.PopupCount==0,"Reward label disappears after resumed game time");
                foreach(var coin in Object.FindObjectsByType<RiverCoin>())Object.DestroyImmediate(coin.gameObject);
                aiCoin=stream.SpawnOne();aiCoin.transform.position=Vector3.zero;aiCoin.Initialize(50,stream.FlowSpeed);
                enemy.transform.position=new Vector3(4,0);hook.transform.position=new Vector3(-4,6);
                var ai=enemy.GetComponent<EnemyHookAI>();var enemyHook=enemy.GetComponent<HookAbility>();enemyHook.CancelHook();
                EconomyChecks.Set(enemyHook,"nextThrowTime",0f);EconomyChecks.Set(ai,"nextDecisionTime",0f);enemyGold=enemy.GetComponent<PlayerProgression>().Gold;
                typeof(EnemyHookAI).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ai,null);
                Check(enemyHook.IsPreparingThrow,"AI prioritizes the moving coin and starts an actual animated throw");
                phase=6;due=Time.time+1.3f;return;
            }
            if(phase==6)
            {
                Check(aiCoin==null&&enemy.GetComponent<PlayerProgression>().Gold>=enemyGold+50,"Native AI throw predicts drift, catches the coin and receives its reward");
                var visible=typeof(MatchHud25D).GetMethod("RewardVisible",BindingFlags.Instance|BindingFlags.NonPublic);
                Check((bool)visible.Invoke(hud,new object[]{hook.transform.position})&&!(bool)visible.Invoke(hud,new object[]{enemy.transform.position}),"Gold labels respect fog and do not reveal a hidden enemy");
                Check(hud.ElapsedSeconds>frozenClock+1,"Clock resumes advancing after pause");
                SceneManager.LoadScene("Arena25D");phase=7;due=0;return;
            }
            if(phase==7)
            {
                var fresh=MatchHud25D.Instance;var freshStream=Object.FindAnyObjectByType<RiverCoinStream>();
                if(fresh==null||freshStream==null)return;
                Check(fresh.ElapsedSeconds<1&&fresh.PopupCount==0&&Object.FindObjectsByType<RiverCoin>().Length==0,"New match clears the clock, coins and reward labels");
                Check(freshStream.SecondsUntilSpawn>=24&&freshStream.SecondsUntilSpawn<=35,"New match restarts the first random spawn delay");
                File.WriteAllText("docs/validation/022-rewards.txt",$"PASS: {count} actual Unity Play Mode assertions\nStone texture/metre UVs; native coin model; random intervals/values/positions; drift/exit/pause; direct/return and red/blue hook; capture/arrival/cancellation/concurrency/duplicate awards; rising one-second labels; real AI prediction and collection; HH:MM:SS and fresh match. Controlled interaction fixtures disable random terrain colliders only.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_RIVER_REWARDS_PASS: "+count);Stop();
            }
        }
        catch(Exception error){Debug.LogError("HOOKWARS_RIVER_REWARDS_FAIL: "+error);Stop();}
    }
}
