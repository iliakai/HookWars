using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MatchAcceptance25DChecks
{
    public const string Key="HookWars.MatchAcceptance";
    static int phase,count;
    static double ready,deadline;
    static float due;
    static PlayerProgression owner;
    static CarcassCarrier carrier;
    static SausageFactory factory;
    static MeatShop shop;
    static PlayerBuilding builder;
    static Animal cow;
    static MatchAcceptance25DChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/World/Check full economy loop Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+100;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);count++;Debug.Log("ACCEPTANCE_CHECK: "+text);}
    static void Capture(string name)
    {
        Directory.CreateDirectory("Temp/AcceptancePreview");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        ScreenCapture.CaptureScreenshot("Temp/AcceptancePreview/"+name+".png");
        Butcher25DChecks.CaptureAt(ArenaSpace25D.ToWorld(factory.transform.position,.5f),"economy_"+name,3.4f);
    }
    static GameBuilding Build(string path,Vector3 cell)
    {
        builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(path));var built=builder.TryBuildAt(cell);
        Check(built!=null,"Real grid/resources build: "+path);return built;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Acceptance timeout");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying)return;
            if(phase==1)
            {
                foreach(var a in Object.FindObjectsByType<EnemyHookAI>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyChase>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyMelee>())a.enabled=false;
                var hero=Object.FindAnyObjectByType<PlayerMovement>();hero.enabled=false;owner=hero.GetComponent<PlayerProgression>();
                Check(owner.Level==1&&owner.Experience==0&&owner.Gold<3,"Fresh unmodified match before explicit test funding");
                owner.AddGold(1000);builder=hero.GetComponent<PlayerBuilding>();carrier=hero.GetComponent<CarcassCarrier>();carrier.enabled=false;
                factory=owner.GetComponent<OwnerEconomy>().Factory;
                Check(factory!=null&&factory.Footprint==Vector2Int.one*2,"Use the actual automatically generated four-cell workshop");
                float front=factory.transform.position.x>0?-1:1;
                shop=(MeatShop)Build(EconomySetup.ShopPath,PlayerBuilding.Snap(factory.transform.position+new Vector3(front*2.5f,.5f),Vector2Int.one));
                Build(EconomySetup.FarmPath,PlayerBuilding.Snap(factory.transform.position+new Vector3(front*4.5f,.5f),Vector2Int.one));builder.CloseInterface();
                var aim=hero.GetComponentInChildren<PlayerAim>();aim.enabled=false;EconomyChecks.Set(aim,"<AimDirection>k__BackingField",Vector2.up);
                cow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),hero.transform.position+Vector3.up*.85f,Quaternion.identity);cow.enabled=false;
                Physics2D.SyncTransforms();
                typeof(PlayerMelee).GetMethod("TryAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero.GetComponent<PlayerMelee>(),null);
                Check(hero.GetComponent<HeroActionTiming25D>().AxePending&&!cow.IsDead,"Accepted axe waits for its impact");
                due=Time.time+.6f;phase=2;return;
            }
            if(Time.time<due)return;
            var presentation=Object.FindAnyObjectByType<ArenaPresentation25D>();
            if(phase==2)
            {
                Check(cow==null&&Object.FindObjectsByType<Carcass>().Count(c=>c.AnimalSpecies==Animal.Species.Cow)==1,"One axe impact creates exactly one 10kg cow corpse");
                var corpse=Object.FindObjectsByType<Carcass>().Single(c=>c.AnimalSpecies==Animal.Species.Cow);
                var visual=presentation.VisualFor(corpse);Check(Meat25DChecks.SafeVisual(visual),"Cow meat has only visual spin without combat behaviour");
                Capture("01_static_corpse");due=Time.time+.2f;phase=3;return;
            }
            if(phase==3)
            {
                carrier.enabled=true;due=Time.time+.2f;phase=4;return;
            }
            if(phase==4)
            {
                Check(carrier.Count==1&&carrier.CurrentMassKg==10,"Automatic pickup preserves the complete cow mass");
                Capture("02_carried_corpse");owner.transform.position=factory.transform.position+Vector3.right*(factory.transform.position.x>0?-1.6f:1.6f);
                Physics2D.SyncTransforms();due=Time.time+.5f;phase=5;return;
            }
            if(phase==5)
            {
                Check(carrier.Count==0&&factory.RawKg>9,"Proximity delivery starts real processing without direct resource injection");
                var controller=presentation.VisualFor(factory).GetComponentInChildren<HookWars.Buildings.BuildingVisualController>();
                Check(controller!=null&&presentation.VisualFor(factory).GetComponentsInChildren<ParticleSystem>().Any(p=>p.isPlaying),"Real raw queue drives furnace/smoke");
                Capture("03_processing_and_sales");due=Time.time+21;phase=6;return;
            }
            if(phase==6)
            {
                var stock=owner.GetComponent<OwnerEconomy>();
                Check(factory.RawKg<1e-6&&stock.Bones==5,"Real-time 10kg processing yields exactly 5 bones");
                Check(stock.ProductKg>3.8&&stock.ProductKg<4.1&&shop.LastSaleTime>0,"Shop sold about 1kg at 10 gold/kg and retained about 4kg ready stock");
                Check(!presentation.VisualFor(factory).GetComponentsInChildren<ParticleSystem>().Any(p=>p.isPlaying),"Empty factory closes its smoke after processing");
                Vector3 wallCell=Vector3.zero;builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.WallPath));
                bool free=false;var bank=ArenaLayout.BankBounds(factory.transform.position.x>0,.5f);
                for(float x=bank.xMin;x<=bank.xMax&&!free;x++)for(float y=bank.yMin;y<=bank.yMax&&!free;y++)
                    if(builder.CanPlace(new Vector3(x,y))){wallCell=new Vector3(x,y);free=true;}
                Check(free,"A free wall cell exists on the actual generated map");
                var wall=Build(EconomySetup.WallPath,wallCell);builder.CloseInterface();Check(stock.Bones==0,"Wall spends the five genuinely produced bones");
                due=Time.time+.6f;phase=7;return;
            }
            if(phase==7)
            {
                Capture("04_closed_factory_and_bone_wall");
                Check(Object.FindObjectsByType<SpriteRenderer>().Length==0&&Object.FindObjectsByType<LineRenderer>().Length==0,"Complete live economy loop contains no flat world graphics");
                File.WriteAllText("Temp/HookWarsAutomation/acceptance.txt",$"PASS: {count} assertions\nReal accepted axe -> static cow corpse -> automatic 10kg cargo -> proximity delivery -> 21s native game processing/sales -> 5 bones -> paid BoneWall\nFunding: explicit test-only 1000 gold after fresh-state assertion; AI disabled only for this test. No production/sale timer acceleration.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_ACCEPTANCE_PASS: "+count+" assertions in real-time Play Mode");phase=98;due=Time.time+.3f;return;
            }
            if(phase==98)Stop();
        }
        catch(Exception error){Debug.LogError("HOOKWARS_ACCEPTANCE_FAIL: "+error);Stop();}
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
}
