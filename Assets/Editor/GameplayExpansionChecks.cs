using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GameplayExpansionChecks
{
    public const string Key = "HookWars.ExpansionChecks";
    static int phase, count;
    static double ready, deadline;
    static float due, started, initial;
    static Health player;
    static string stage;
    static GameplayExpansionChecks() { if (SessionState.GetBool(Key, false)) Subscribe(); }
    public static void Run(string name)
    {
        ValidationPlayScope.CurrentScene(); EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetString(Key + ".Stage", name); SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); Subscribe();
    }
    static void Subscribe()
    {
        stage = SessionState.GetString(Key + ".Stage", "mass"); phase = count = 0; due = 0;
        ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 90;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); count++; Debug.Log("EXPANSION_CHECK: " + message); }
    static bool Near(double a, double b) => Math.Abs(a-b) < .001;
    static Carcass Meat(float mass)
    {
        var c = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath),new Vector3(-6,-8),Quaternion.identity);
        c.Initialize(mass); return c;
    }
    static void Mass()
    {
        player = Object.FindAnyObjectByType<PlayerMovement>().GetComponent<Health>();
        var enemy = Object.FindAnyObjectByType<EnemyChase>().GetComponent<Health>();
        var progression = player.GetComponent<PlayerProgression>();
        Check(player.MaximumKg == 100 && player.RegenerationPercentPerSecond == 0, "Initial 100kg and zero percent regen");
        progression.AddExperience(20);
        Check(progression.Level == 2 && player.MaximumKg == 100, "Level grants point without increasing kg");
        enemy.TakeDamage(60, player); Check(player.MaximumKg == 100, "Nonlethal damage grants no mass");
        enemy.TakeDamage(1000, player);
        Check(player.MaximumKg == 120 && player.CurrentKg == 120, "Enemy kill immediately grants 20kg current and maximum");
        Check(enemy.GetComponent<HeroCarcassSpawner>().CurrentCarcass.MassKg == 80, "Enemy leaves exactly 80 percent as meat");
        enemy.TakeDamage(1000, player); Check(player.MaximumKg == 120, "Dead target cannot award a second meal");
        var root = new GameObject("Partial pickup fixture");
        var carrier = root.AddComponent<CarcassCarrier>();
        var large = Meat(100); large.transform.position = new Vector3(-6,-7);
        var small = Meat(6); Check(carrier.TryCollect(small), "Fill first six kg");
        Check(carrier.TryCollect(large) && Near(carrier.CurrentMassKg,10) && Near(large.MassKg,96), "Only remaining four kg taken from large piece");
        Check(!carrier.TryCollect(large) && large.MassKg == 96, "Full carrier leaves remaining mass unchanged");
        var helper = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ButcherHelper>("Assets/Prefabs/ButcherHelper.prefab"), new Vector3(-6,-8), Quaternion.identity);
        helper.Bind(player.GetComponent<OwnerEconomy>().Factory,10); helper.enabled=false;
        var hc = helper.GetComponent<CarcassCarrier>(); hc.enabled=false;
        Check(hc.TryCollect(large) && Near(hc.CurrentMassKg,10) && Near(large.MassKg,86), "Real helper also takes a partial ten kg");
        Check(Near(carrier.CurrentMassKg+hc.CurrentMassKg+large.MassKg,106), "Mass conserved across carriers and remainder");
        large.GetComponent<HookVictim>().TryCapture(player.GetComponent<HookAbility>()); carrier.DropAll();
        Check(!carrier.TryCollect(large), "Hook-reserved remainder cannot be divided");
        large.GetComponent<HookVictim>().Release(player.GetComponent<HookAbility>());
        var factory = player.GetComponent<OwnerEconomy>().Factory; factory.enabled=false;
        Check((bool)EconomyChecks.Invoke(factory,"TryAcceptMass",11d), "Prepare eleven kg factory raw stock");
        Check(Near(hc.DeliverTo(factory),1) && Near(hc.CurrentMassKg,9) && Near(factory.RawKg,12), "Delivery splits cargo to fit one remaining raw kg");
        player.GetComponent<OwnerEconomy>().AddOutput(12,0);
        double xp=progression.Experience;
        Check(hc.DeliverTo(factory)==0 && Near(hc.CurrentMassKg,9) && progression.Experience==xp, "Full finished stock blocks partial delivery without XP");
        Check(player.TryUpgradeRegeneration(progression) && Near(player.RegenerationPercentPerSecond,.2) && Near(player.RegenerationKgPerSecond,.24), "Regen upgrade is .2 percent of current max per second");
        player.TakeDamage(20); initial=player.CurrentKg; started=Time.time; due=Time.time+.55f;
        Object.Destroy(root); Object.Destroy(helper.gameObject);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        ScreenCapture.CaptureScreenshot("docs/validation/016-mass.png");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Expansion checks timed out");
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                foreach(var a in Object.FindObjectsByType<EnemyHookAI>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyChase>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyMelee>())a.enabled=false;
                foreach(var c in Object.FindObjectsByType<CarcassCarrier>())c.enabled=false;
                if(stage=="mass")Mass();else if(stage=="build")Building();else throw new InvalidOperationException("Unknown expansion stage "+stage);
                phase=2;return;
            }
            if(phase==2)
            {
                if(stage=="mass")Check(Math.Abs((player.CurrentKg-initial)-player.RegenerationKgPerSecond*(Time.time-started))<.03,"Regen heals by max percent over real game time");
                if(stage=="build")
                {
                    var grid=Object.FindAnyObjectByType<BuildGrid25D>();
                    Check(grid!=null&&grid.VisibleCells==ArenaLayout.Depth*20,"One-meter grid covers only the player's own bank");
                    Check(grid.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled&&r.sharedMaterial.GetColor("_BaseColor").a<=.11f),"Grid uses faint translucent batched meshes");
                    ScreenCapture.CaptureScreenshot("docs/validation/016-build.png"); phase=3; due=Time.time+.3f; return;
                }
                Finish();
            }
            if(phase==3)Finish();
        }
        catch(Exception e){Debug.LogError("HOOKWARS_EXPANSION_FAIL: "+e);Stop();}
    }
    static void Finish()
    {
                File.WriteAllText("docs/validation/016-"+stage+".txt",$"PASS: {count} actual Unity Play Mode assertions; stage={stage}\nSee GameplayExpansionChecks for exact cases. Actual Game View snapshot saved.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_EXPANSION_"+stage.ToUpperInvariant()+"_PASS: "+count);Stop();
    }
    static void Stop(){phase=99;EditorApplication.isPlaying=false;}
    static void Building()
    {
        var builder=Object.FindAnyObjectByType<PlayerBuilding>();var owner=builder.GetComponent<PlayerProgression>();owner.AddGold(1000);
        builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.ShopPath));
        var bank=ArenaLayout.BankBounds(builder.HomeRightBank,.5f);Vector3 free=default;bool found=false;
        for(float x=bank.xMin;x<=bank.xMax&&!found;x++)for(float y=bank.yMin;y<=bank.yMax&&!found;y++)
            if(builder.CanPlace(new Vector3(x,y))){free=new Vector3(x,y);found=true;}
        Check(found&&Vector2.Distance(free,builder.transform.position)<=4,"Free legal cell lies inside four-meter build radius");
        double gold=owner.Gold;var far=new Vector3(builder.HomeRightBank?ArenaLayout.OuterEdge-.5f:-ArenaLayout.OuterEdge+.5f,9.5f);
        Check(!builder.CanPlace(far)&&builder.TryBuildAt(far)==null&&owner.Gold==gold,"Remote placement rejects without charging");
        var shop=builder.TryBuildAt(free);Check(shop!=null&&builder.IsBuilding,"Successful placement retains selected building");
        builder.RotatePlacement();Check(builder.QuarterTurns==1,"R-rotation still supported");
        var enemy=Object.FindObjectsByType<PlayerProgression>().First(p=>p!=owner);
        Check(!shop.Demolish(enemy),"Enemy cannot demolish owned building");
        Check(!owner.GetComponent<OwnerEconomy>().Factory.Demolish(owner),"Starting workshop is protected");
        gold=owner.Gold;Check(shop.Demolish(owner)&&owner.Gold==gold,"Owned shop demolishes without refund");
        Check(builder.CanPlace(free),"Demolition immediately frees occupancy");
        builder.CloseInterface();
        var rect=PlayerBuilding.ScreenRect;var point=new Vector2(rect.x+20,Screen.height-rect.y-20);
        Check(!builder.IsBuilding&&GamePointer.OverInterface(point),"Permanent construction panel remains a UI hit target after cancellation");
        int sequence=builder.GetComponent<HeroActionTiming25D>().HookSequence;
        builder.GetComponent<PlayerHook>().TryThrowAtScreenPoint(point);
        Check(builder.GetComponent<HeroActionTiming25D>().HookSequence==sequence,"Clicks on permanent panel cannot throw hooks");
        builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.ShopPath));
        MatchPause.Instance.HandleEscape();Check(MatchPause.Instance.IsPaused&&builder.IsBuilding,"Esc pauses while retaining build selection; Q is cancel");
        MatchPause.Instance.SetPaused(false);
        due=Time.time+2;
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();ScreenCapture.CaptureScreenshot("docs/validation/016-build.png");
    }
}
