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
    static HookAbility travelHook;
    static HookAnchor travelAnchor;
    static Vector3 travelStart, anchorStart;
    static Carcass steakSmall, steakLarge;
    static Quaternion meatRotation;
    static int worldDepthIndex;
    static bool worldRotationCaptured;
    static GameplayExpansionChecks() { if (SessionState.GetBool(Key, false)) Subscribe(); }
    public static void Run(string name)
    {
        ValidationPlayScope.CurrentScene(); EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetInt(Key+".SavedDepth",MatchConfiguration.LastDepth);
        SessionState.SetString(Key + ".Stage", name); SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); Subscribe();
    }
    static void Subscribe()
    {
        stage = SessionState.GetString(Key + ".Stage", "mass"); phase = count = worldDepthIndex = 0; due = 0;
        worldRotationCaptured=false;
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
            if(stage=="world"){WorldTick();return;}
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
    static void Stop(){if(stage=="world")MatchConfiguration.RememberDepth(SessionState.GetInt(Key+".SavedDepth",16));phase=99;EditorApplication.isPlaying=false;}
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
    static void DisableAI()
    {
        foreach(var a in Object.FindObjectsByType<EnemyHookAI>())a.enabled=false;
        foreach(var a in Object.FindObjectsByType<EnemyChase>())a.enabled=false;
        foreach(var a in Object.FindObjectsByType<EnemyMelee>())a.enabled=false;
        foreach(var c in Object.FindObjectsByType<CarcassCarrier>())c.enabled=false;
    }
    static void AnchorCount()
    {
        var anchors=Object.FindObjectsByType<HookAnchor>();
        Check(anchors.Length==5,"Exactly five ground anchors at depth "+ArenaLayout.Depth);
        Check(anchors.All(a=>ArenaLayout.IsInsideBank(a.transform.position,a.transform.position.x>0,.28f)&&a.GetComponent<Health>()==null&&a.GetComponent<GameBuilding>()==null),"Anchors fit banks and cannot be damaged or demolished");
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
        foreach(var a in anchors)
        {
            var view=display.VisualFor(a);Check(view!=null,"Anchor has imported 3D visual");
            var attach=view.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Hook_Attach",StringComparison.Ordinal));
            Check(attach.position.y>view.position.y+.25f,"Imported attachment is upright above ground");
        }
    }
    static void WorldTick()
    {
        if(phase==1)
        {
            DisableAI();AnchorCount();
            var movement=Object.FindAnyObjectByType<PlayerMovement>();movement.enabled=false;player=movement.GetComponent<Health>();
            var cow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),movement.transform.position+Vector3.right*.95f,Quaternion.identity);cow.enabled=false;
            var cowBefore=cow.transform.position;Vector3 desired=movement.transform.position+Vector3.right*.3f;
            Vector3 allowed=WorldCollision.Slide(movement.transform,desired,ArenaLayout.HeroRadius);
            Check(Vector3.Distance(allowed,desired)<.001f&&Vector3.Distance(cowBefore,cow.transform.position)>.1f,"Hero mass pushes cow instead of being blocked");
            Check(!cow.TryPush(new Vector3(0,0)),"Pushed animal cannot enter river");Object.DestroyImmediate(cow.gameObject);
            steakSmall=Meat(4);steakSmall.SetAnimalSpecies(Animal.Species.Sheep);
            steakLarge=Meat(80);steakLarge.Initialize(80,player.GetComponent<HookVictim>());steakLarge.transform.position+=Vector3.right;
            bool lane=false;
            foreach(var a in Object.FindObjectsByType<HookAnchor>())
            {
                for(int i=0;i<8&&!lane;i++)
                {
                    Vector3 direction=new Vector3(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4));var p=a.transform.position+direction*3;
                    if(!ArenaLayout.IsInsideBank(p,a.transform.position.x>0,ArenaLayout.HeroRadius))continue;
                    bool clear=true;for(float d=1.1f;d<=3.01f;d+=.2f)clear&=WorldCollision.FreeCircle(a.transform.position+direction*d,ArenaLayout.HeroRadius);
                    if(!clear)continue;travelAnchor=a;anchorStart=a.transform.position;travelStart=p;lane=true;
                }
                if(lane)break;
            }
            Check(lane,"A free approach lane exists for real anchor hit");
            movement.transform.position=travelStart;Physics2D.SyncTransforms();travelHook=movement.GetComponent<HookAbility>();
            travelHook.TryThrow((Vector2)(anchorStart-travelStart));Check(travelHook.IsPreparingThrow,"Anchor throw uses existing animated windup");
            phase=2;due=0;started=Time.time;return;
        }
        if(phase==2)
        {
            var view=Object.FindAnyObjectByType<ArenaPresentation25D>().VisualFor(steakLarge);
            if(view!=null&&!worldRotationCaptured){meatRotation=view.Find("Model/Spin").localRotation;worldRotationCaptured=true;}
            if(!travelHook.IsSelfPulling&&(travelHook.ActiveHook!=null||travelHook.IsPreparingThrow)&&Time.time-started<3)return;
            Check(travelHook.IsSelfPulling||Vector3.Distance(travelStart,player.transform.position)>1,"Hook collision with eye starts owner pull");
            phase=3;return;
        }
        if(phase==3)
        {
            if(travelHook.ActiveHook!=null&&Time.time-started<4)return;
            Check(travelHook.ActiveHook==null&&!player.GetComponent<HookVictim>().IsCaught,"Self-pull ends and releases movement lock");
            Check(Vector3.Distance(travelStart,player.transform.position)>1.8f&&Vector3.Distance(anchorStart,player.transform.position)<1.2f,"Owner approaches stationary anchor without teleporting it");
            Check(travelAnchor.transform.position==anchorStart,"Anchor itself never moves");
            due=Time.time+1;phase=4;return;
        }
        if(phase==4)
        {
            var display=Object.FindAnyObjectByType<ArenaPresentation25D>();var small=display.VisualFor(steakSmall);var large=display.VisualFor(steakLarge);
            Check(small.GetComponentsInChildren<MeshFilter>().Length==2&&large.GetComponentsInChildren<Animator>().Length==1,"New rib steak uses two static meshes and one safe spin Animator");
            Check(Meat25DChecks.SafeVisual(large),"New meat has no combat scripts, root motion or events");
            Check(Quaternion.Angle(meatRotation,large.Find("Model/Spin").localRotation)>20,"New rib steak actually rotates in Play Mode");
            Butcher25DChecks.CaptureAt(large.position+Vector3.up*.5f,"new_rib_steak",1.1f);
            Butcher25DChecks.CaptureAt(display.VisualFor(travelAnchor).position+Vector3.up*.3f,"hook_eye_up",1.1f);
            MatchConfiguration.Prepare(4);UnityEngine.SceneManagement.SceneManager.LoadScene("Arena25D");phase=5;due=0;return;
        }
        if(phase==5)
        {
            DisableAI();var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
            if(display==null||Object.FindObjectsByType<HookAnchor>().Any(a=>display.VisualFor(a)==null))return;
            AnchorCount();
            if(worldDepthIndex++==0){MatchConfiguration.Prepare(20);UnityEngine.SceneManagement.SceneManager.LoadScene("Arena25D");due=0;return;}
            File.WriteAllText("docs/validation/016-world.txt",$"PASS: {count} actual Unity Play Mode assertions\nFive upright immutable anchors at 16/4/20m, actual hit and owner pull, safe ending, stationary fixture, cow pushing, river exclusion, new rib steak meshes and actual spin.\n");
            SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_EXPANSION_WORLD_PASS: "+count);Stop();
        }
    }
}
