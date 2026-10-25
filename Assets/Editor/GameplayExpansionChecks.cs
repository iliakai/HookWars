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
    static Carcass lifetimeSmall, lifetimeLarge, lifetimePortion;
    static GameObject lifetimeRoot;
    static Transform lifetimeSmallView, lifetimeLargeView;
    static PlayerHook modesHook;
    static UnityEngine.InputSystem.Keyboard modesKeyboard;
    static GameObject modesRoot;
    static Transform auraView;
    static Renderer auraRenderer;
    static Material auraMaterial;
    static Mesh auraMesh;
    static Quaternion meatRotation;
    static int worldDepthIndex;
    static bool worldRotationCaptured;
    static FogOfWar25D fog;
    static Vector3 fogOrigin, fogTarget, fogProbe;
    static ButcherHelper fogHelper;
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
            if(stage=="fog"){FogTick();return;}
            if(stage=="lifetime"){LifetimeTick();return;}
            if(stage=="modes"){ModesTick();return;}
            if(stage=="aura"){AuraTick();return;}
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
    static void Age(Carcass meat, double seconds) => EconomyChecks.Invoke(meat, "AdvanceGroundLifetime", seconds);
    static Color AuraColor()
    {var properties=new MaterialPropertyBlock();auraRenderer.GetPropertyBlock(properties);return properties.GetColor("_BaseColor");}
    static void CaptureAura(string color)
    {
        Butcher25DChecks.CaptureAt(auraView.position+Vector3.up*.35f,"hook_aura_"+color,1.25f);
        File.Copy("Temp/ButcherPreview/hook_aura_"+color+".png","docs/validation/021-aura-"+color+".png",true);
    }
    static void AuraTick()
    {
        if(phase==1)
        {
            DisableAI();modesHook=Object.FindAnyObjectByType<PlayerHook>();modesHook.GetComponent<PlayerMovement>().enabled=false;
            modesHook.GetComponentInChildren<PlayerAim>().enabled=false;
            var display=Object.FindAnyObjectByType<ArenaPresentation25D>();auraView=display.VisualFor(modesHook.GetComponent<Health>());
            if(auraView==null||auraView.Find("Hook mode aura")==null)return;
            auraRenderer=auraView.Find("Hook mode aura").GetComponent<Renderer>();auraMaterial=auraRenderer.sharedMaterial;
            auraMesh=auraRenderer.GetComponent<MeshFilter>().sharedMesh;
            Check(auraRenderer.enabled&&auraMaterial.shader.isSupported,"Player aura uses a supported visible renderer");
            var red=AuraColor();Check(red.r>.9f&&red.b<.2f,"Default target-pull mode has a warm red aura");
            Check(auraMesh.colors.Min(c=>c.a)==0&&auraMesh.colors.Max(c=>c.a)<=.28f,
                "Soft halo fades to transparent edges with restrained opacity");
            Check(auraRenderer.GetComponents<Collider>().Length==0&&auraRenderer.GetComponents<Collider2D>().Length==0&&
                auraRenderer.GetComponents<Light>().Length==0&&auraRenderer.GetComponents<ParticleSystem>().Length==0,
                "Aura has no physics, light or particle effects");
            Check(auraRenderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&!auraRenderer.receiveShadows,
                "Aura does not cast or receive shadows");
            Check(Object.FindObjectsByType<Health>().Where(h=>h.GetComponent<PlayerHook>()==null).All(h=>display.VisualFor(h)?.Find("Hook mode aura")==null),
                "Mode indicator belongs to the controlled hero only");
            Check(!auraView.Find("Team").gameObject.activeSelf,"Old solid blue player disk does not conflict with red mode aura");
            CaptureAura("red");Check(modesHook.TryTogglePullMode(),"Select owner-flight aura mode");phase=2;due=Time.time+.1f;return;
        }
        if(phase==2)
        {
            var blue=AuraColor();Check(blue.b>.9f&&blue.r<.2f,"Mode change updates aura to light blue on the next game frame");
            Check(auraRenderer.sharedMaterial==auraMaterial&&auraRenderer.GetComponent<MeshFilter>().sharedMesh==auraMesh,
                "Changing mode reuses the same material and mesh");
            CaptureAura("blue");modesHook.transform.position+=new Vector3(.5f,.3f);phase=3;due=Time.time+.1f;return;
        }
        if(phase==3)
        {
            Check(Vector3.Distance(auraView.position,ArenaSpace25D.ToWorld(modesHook.transform.position))<.001f&&auraRenderer.transform.parent==auraView,
                "Halo follows the detached hero visual in XZ space");
            MatchPause.Instance.SetPaused(true);var before=AuraColor();
            Check(!modesHook.TryTogglePullMode()&&AuraColor()==before,"Pause keeps the selected aura color stable");MatchPause.Instance.SetPaused(false);
            modesHook.GetComponent<Health>().TakeDamage(100000);phase=4;due=Time.time+.1f;return;
        }
        if(phase==4)
        {
            Check(!auraRenderer.gameObject.activeInHierarchy,"Dead hero has no lingering aura");
            phase=5;due=Time.time+3.2f;return;
        }
        if(phase==5)
        {
            Check(!modesHook.GetComponent<Health>().IsDead&&auraRenderer.gameObject.activeInHierarchy&&AuraColor().b>.9f,"Native respawn restores aura in the selected mode");
            Check(modesHook.TryTogglePullMode(),"Return to target-pull mode");phase=6;due=Time.time+.1f;return;
        }
        if(phase==6)
        {
            Check(AuraColor().r>.9f&&AuraColor().b<.2f,"Switching back restores red aura");
            File.WriteAllText("docs/validation/021-aura.txt",$"PASS: {count} actual Unity Play Mode assertions\nActual renderer/property blocks; red/blue/red modes; feathered opacity; no physics/lights/particles; shared material/mesh; movement; pause; death/revival. Actual camera captures saved for both colors.\n");
            SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_AURA_PASS: "+count);Stop();
        }
    }
    static object HookCall(string method, params object[] args) => typeof(HookAbility).GetMethod(method,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(modesHook,args);
    static Transform ModeTarget(Vector3 position)
    {
        var target=new GameObject("Mode test victim");target.transform.SetParent(modesRoot.transform);
        target.transform.position=position;target.layer=LayerMask.NameToLayer("HookTarget");
        target.AddComponent<CircleCollider2D>().radius=.5f;target.AddComponent<Health>();target.AddComponent<HookVictim>();
        return target.transform;
    }
    static void ModeHit(Transform target, Vector3 origin, bool returning=false)
    {
        modesHook.CancelHook();modesHook.transform.position=origin;
        HookCall("ReleaseThrow",(Vector2)(target.position-origin).normalized);
        if(returning)
        {
            modesHook.ActiveHook.position=target.position+Vector3.right*2;
            Check((bool)HookCall("TryCatchOnReturn",origin,20f),"Returning hook finds a real target collider");
        }
        else { EconomyChecks.Set(modesHook,"speed",20f/Time.deltaTime);HookCall("FlyHook"); }
    }
    static void OwnerTravel(float distance)
    {EconomyChecks.Set(modesHook,"pullSpeed",distance/Time.deltaTime);HookCall("PullOwner");}
    static void ModesTick()
    {
        if(phase==1)
        {
            DisableAI();modesHook=Object.FindAnyObjectByType<PlayerHook>();modesHook.GetComponent<PlayerMovement>().enabled=false;
            foreach(var b in Object.FindObjectsByType<GameBuilding>())Object.DestroyImmediate(b.gameObject);
            foreach(var r in Object.FindObjectsByType<PermanentRock>())Object.DestroyImmediate(r.gameObject);
            foreach(var a in Object.FindObjectsByType<HookAnchor>())Object.DestroyImmediate(a.gameObject);
            modesRoot=new GameObject("Hook mode fixtures");
            Check(modesHook.SelectedPullMode==HookAbility.PullMode.TargetToOwner,"Fresh match defaults to pulling the target");
            modesKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            modesKeyboard.MakeCurrent();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(modesKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
            // Dispatch once explicitly: Editor focus/input passes can otherwise
            // consume the transient press before the play-loop LateUpdate.
            UnityEngine.InputSystem.InputSystem.Update();HookCall("LateUpdate");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(modesKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            UnityEngine.InputSystem.InputSystem.Update();
            due=Time.time+.1f;phase=2;return;
        }
        if(phase==2)
        {
            Check(modesHook.SelectedPullMode==HookAbility.PullMode.OwnerToTarget,"Actual E key event switches player hook to owner flight");
            UnityEngine.InputSystem.InputSystem.RemoveDevice(modesKeyboard);modesKeyboard=null;
            MatchPause.Instance.SetPaused(true);Check(!modesHook.TryTogglePullMode(),"Pause blocks mode changes");MatchPause.Instance.SetPaused(false);
            var target=ModeTarget(new Vector3(-4,-5));var targetStart=target.position;
            ModeHit(target,new Vector3(-10,-5));
            Check(modesHook.IsSelfPulling&&target.GetComponent<HookVictim>().IsCaught&&modesHook.GetComponent<HookVictim>().IsCaught,
                "Outbound hit locks both target and flying owner");
            Check(target.GetComponent<Health>().CurrentKg==60,"Owner-flight hit deals normal forty kg damage once");
            Check(!modesHook.TryTogglePullMode(),"Active hook cannot switch modes midway");
            OwnerTravel(2);
            Check(modesHook.transform.position.x>-9&&target.position==targetStart,"Owner moves while the captured target stays fixed");
            OwnerTravel(100);
            Check(modesHook.ActiveHook==null&&!target.GetComponent<HookVictim>().IsCaught&&!modesHook.GetComponent<HookVictim>().IsCaught,
                "Owner arrival clears hook and both capture locks");
            Check(Near(Vector2.Distance(modesHook.transform.position,target.position),1.1),"Owner stops at the normal safe separation");
            Object.DestroyImmediate(target.gameObject);

            target=ModeTarget(new Vector3(-4,-5));ModeHit(target,new Vector3(-10,-5));
            var bends=(System.Collections.Generic.List<Vector3>)typeof(HookAbility).GetField("bends",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(modesHook);
            bends.Add(new Vector3(-10,-7));bends.Add(new Vector3(-4,-7));
            EconomyChecks.Set(modesHook,"ownerLandingPosition",HookCall("GetOwnerLandingPosition",target.position));OwnerTravel(1);
            Check(Near(modesHook.transform.position.x,-10)&&modesHook.transform.position.y<-5.5f,"Owner follows the first saved bend instead of cutting across the route");
            OwnerTravel(100);Check(modesHook.ActiveHook==null&&Near(Vector2.Distance(modesHook.transform.position,target.position),1.1),"Owner completes the saved bent route");
            Object.DestroyImmediate(target.gameObject);

            target=ModeTarget(new Vector3(4,-6));targetStart=target.position;
            ModeHit(target,new Vector3(-4,-6),true);OwnerTravel(4);
            Check(!ArenaLayout.IsInsideBank(modesHook.transform.position,modesHook.transform.position.x>0),"Owner travel can cross the river");
            modesHook.CancelHook();
            Check(modesHook.transform.position==new Vector3(-4,-6)&&target.position==targetStart&&!target.GetComponent<HookVictim>().IsCaught,
                "Cancellation in river restores last dry position and releases stationary target");
            Object.DestroyImmediate(target.gameObject);

            target=ModeTarget(new Vector3(2.6f,-6));ModeHit(target,new Vector3(-4,-6));OwnerTravel(100);
            Check(ArenaLayout.IsInsideBank(modesHook.transform.position,true)&&modesHook.ActiveHook==null,
                "Near-shore target lands the flying owner on the destination bank");Object.DestroyImmediate(target.gameObject);

            target=ModeTarget(new Vector3(4,-6));ModeHit(target,new Vector3(-4,-6));
            var wall=new GameObject("Blocking flight wall");wall.transform.SetParent(modesRoot.transform);wall.layer=LayerMask.NameToLayer("Buildings");
            wall.transform.position=new Vector3(0,-6);wall.AddComponent<BoxCollider2D>().size=new Vector2(1,4);OwnerTravel(100);
            Check(modesHook.ActiveHook==null&&modesHook.transform.position.x<0&&!target.GetComponent<HookVictim>().IsCaught,
                "Impassable terrain cancels owner flight without passing through it");
            Object.DestroyImmediate(wall);Object.DestroyImmediate(target.gameObject);

            target=ModeTarget(new Vector3(4,-6));ModeHit(target,new Vector3(-4,-6));OwnerTravel(4);Object.DestroyImmediate(target.gameObject);OwnerTravel(1);
            Check(modesHook.ActiveHook==null&&ArenaLayout.IsInsideBank(modesHook.transform.position,false)&&!modesHook.GetComponent<HookVictim>().IsCaught,
                "Disappearing target safely cancels river flight");

            Check(modesHook.TryTogglePullMode(),"Ready hook switches back to target pull");
            target=ModeTarget(new Vector3(-4,-5));ModeHit(target,new Vector3(-10,-5));
            Check(modesHook.IsPullingTarget(target)&&!modesHook.IsSelfPulling&&modesHook.transform.position==new Vector3(-10,-5),
                "Default mode still pulls the target without moving owner");modesHook.CancelHook();Object.DestroyImmediate(target.gameObject);
            for(int mode=0;mode<2;mode++)
            {
                var anchor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<HookAnchor>("Assets/Prefabs/HookAnchor.prefab"),new Vector3(-4,6),Quaternion.identity);
                var anchorStart=anchor.transform.position;modesHook.transform.position=new Vector3(-8,6);
                HookCall("ReleaseThrow",Vector2.right);modesHook.ActiveHook.position=anchorStart;HookCall("BeginAnchorPull",anchor);
                Check(modesHook.IsSelfPulling,"Anchor always pulls owner in selected mode "+mode);OwnerTravel(100);
                Check(anchor.transform.position==anchorStart&&modesHook.ActiveHook==null,"Anchor remains stationary and releases owner in mode "+mode);
                Object.DestroyImmediate(anchor.gameObject);if(mode==0)Check(modesHook.TryTogglePullMode(),"Select owner mode for second anchor test");
            }

            var meat=Meat(6);meat.transform.position=new Vector3(-4,-5);ModeHit(meat.transform,new Vector3(-10,-5));OwnerTravel(100);
            var carrier=modesHook.GetComponent<CarcassCarrier>();
            Check(meat.IsStored&&carrier.CurrentMassKg==6,"Flying to meat collects it after releasing owner capture");
            carrier.DropAll();Object.DestroyImmediate(meat.gameObject);

            var cow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),new Vector3(-4,-5),Quaternion.identity);cow.enabled=false;
            ModeHit(cow.transform,new Vector3(-10,-5));OwnerTravel(100);
            Check(cow.IsDead&&carrier.CurrentMassKg==10,"Animal hit replaces its body with meat and owner flight collects the ten kg");
            carrier.DropAll();foreach(var item in Object.FindObjectsByType<Carcass>())Object.DestroyImmediate(item.gameObject);

            var enemy=Object.FindAnyObjectByType<EnemyChase>();enemy.transform.position=new Vector3(4,5);
            var cargo=Meat(4);Check(enemy.GetComponent<CarcassCarrier>().TryCollect(cargo),"Prepare enemy cargo before lethal owner-flight hook");
            EconomyChecks.Set(modesHook,"damage",200f);ModeHit(enemy.transform,new Vector3(-4,5));
            var corpse=enemy.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
            Check(enemy.GetComponent<Health>().IsDead&&corpse.IsReserved&&cargo.IsReserved,"Lethal owner-flight hit reserves corpse and dropped cargo");
            OwnerTravel(100);
            Check(carrier.CurrentMassKg==10&&corpse.MassKg==70&&cargo.IsAvailable&&cargo.MassKg==4,
                "Lethal owner flight preserves partial pickup and the full dropped cargo at target bank");
            Check(ArenaLayout.IsInsideBank(modesHook.transform.position,true)&&!corpse.IsReserved,"Lethal arrival releases corpse on dry destination bank");
            carrier.DropAll();
            target=ModeTarget(new Vector3(-4,-6));ModeHit(target,new Vector3(4,-6));OwnerTravel(4);
            modesHook.GetComponent<Health>().TakeDamage(100000);HookCall("LateUpdate");
            Check(modesHook.ActiveHook==null&&ArenaLayout.IsInsideBank(modesHook.transform.position,true)&&!target.GetComponent<HookVictim>().IsCaught,
                "Owner death during river flight automatically clears both captures and returns to dry ground");
            Check(!modesHook.TryTogglePullMode(),"Dead owner cannot change hook mode");Object.Destroy(modesRoot);
            File.WriteAllText("docs/validation/020-hook-modes.txt",$"PASS: {count} actual Unity Play Mode assertions\nActual E input; normal/owner modes; outbound/return hits; damage; pause/action guards; cross-river arrival/cancel; blocked/destroyed targets; both modes on anchors; meat pickup; lethal hero plus dropped cargo. Random terrain removed only in these isolated interaction fixtures.\n");
            SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_HOOK_MODES_PASS: "+count);Stop();
        }
    }
    static void LifetimeTick()
    {
        if (phase == 1)
        {
            DisableAI();
            lifetimeRoot = new GameObject("Ground meat lifetime fixtures");
            var source = new GameObject("Hero meat source"); source.transform.SetParent(lifetimeRoot.transform);
            source.AddComponent<Health>(); var victim = source.AddComponent<HookVictim>();
            var holder = new GameObject("Lifetime carrier"); holder.transform.SetParent(lifetimeRoot.transform);
            holder.transform.position = new Vector3(-6, -8);
            var carrier = holder.AddComponent<CarcassCarrier>(); carrier.enabled = false;
            var hook = Object.FindAnyObjectByType<PlayerMovement>().GetComponent<HookAbility>();
            lifetimeSmall = Meat(6); lifetimeLarge = Meat(80); lifetimeLarge.Initialize(80, victim);
            Check(lifetimeSmall.GroundLifetimeSeconds == 60 && lifetimeLarge.GroundLifetimeSeconds == 300,
                "Animal meat lives sixty ground seconds, hero meat three hundred");
            Age(lifetimeSmall, 59); Check(Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Small meat survives before one minute");
            Age(lifetimeSmall, 0); Age(lifetimeSmall, -1); Age(lifetimeSmall, double.NaN); Age(lifetimeSmall, double.PositiveInfinity);
            Check(Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Invalid elapsed time cannot alter meat lifetime");
            MatchPause.Instance.SetPaused(true); Age(lifetimeSmall, 600);
            Check(Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Game pause stops ground expiry");
            MatchPause.Instance.SetPaused(false);
            Check(lifetimeSmall.GetComponent<HookVictim>().TryCapture(hook), "Ground meat reserved by a real hook");
            Age(lifetimeSmall, 600);
            Check(Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Direct capture protects meat during pulling");
            lifetimeSmall.GetComponent<HookVictim>().Release(hook);
            Check(carrier.TryCollect(lifetimeSmall), "Near-expiry small meat can be carried");
            Age(lifetimeSmall, 600);
            Check(lifetimeSmall.IsStored && Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Stored meat does not expire or lose mass");
            carrier.DropAll();
            Check(lifetimeSmall.IsAvailable && Near(lifetimeSmall.RemainingGroundLifetimeSeconds, 1), "Dropping resumes previous age without refreshing it");
            Age(lifetimeLarge, 100);
            Check(carrier.TryCollect(lifetimeLarge) && lifetimeLarge.MassKg == 70, "Partial pickup leaves seventy kg of hero meat");
            lifetimePortion = carrier.StoredCargo[0];
            Check(lifetimePortion.IsHeroCarcass && lifetimePortion.GroundLifetimeSeconds == 300 &&
                Near(lifetimePortion.RemainingGroundLifetimeSeconds, 200) && Near(lifetimeLarge.RemainingGroundLifetimeSeconds, 200),
                "Both split pieces preserve hero type and original ground age");
            carrier.DropAll();
            var linked = Meat(80); linked.Initialize(80, victim);
            source.GetComponent<Health>().TakeDamage(100000); victim.TryCapture(hook); Age(linked, 600);
            Check(linked.IsReserved && Near(linked.RemainingGroundLifetimeSeconds, 300), "Lethal hero capture also protects its linked carcass");
            victim.Release(hook); Object.Destroy(linked.gameObject);
            var expired = Meat(4); Age(expired, 60);
            Check(!expired.IsAvailable && !carrier.TryCollect(expired), "Exactly sixty seconds blocks same-frame pickup before destruction");
            Age(lifetimeLarge, 199);
            Check(lifetimeSmall.IsAvailable && lifetimeLarge.IsAvailable, "Both types survive immediately before their own deadlines");
            due = Time.time + .05f; phase = 2; return;
        }
        if (phase == 2)
        {
            var display = Object.FindAnyObjectByType<ArenaPresentation25D>();
            lifetimeSmallView = display.VisualFor(lifetimeSmall); lifetimeLargeView = display.VisualFor(lifetimeLarge);
            Check(lifetimeSmallView != null && lifetimeLargeView != null, "Ground meat has real world visuals before expiry");
            due = Time.time + 1.3f; phase = 3; return;
        }
        if (phase == 3)
        {
            Check(lifetimeSmall == null && lifetimeLarge == null, "Native Update destroys both types after the remaining game time elapses");
            Check(lifetimeSmallView == null && lifetimeLargeView == null, "World presentation removes expired meat visuals too");
            Check(lifetimePortion != null && lifetimePortion.IsAvailable && lifetimePortion.MassKg == 10,
                "A separately dropped younger portion remains collectible");
            Object.Destroy(lifetimePortion.gameObject); Object.Destroy(lifetimeRoot);
            File.WriteAllText("docs/validation/019-meat-lifetime.txt", $"PASS: {count} Unity Play Mode assertions\n60/300 game seconds on ground; pause; carrying/drop; direct/lethal capture; split identity/age; same-frame pickup rejection; native Update expiry and world visual cleanup. Boundary setup advances elapsed time directly; final destruction uses actual game frames.\n");
            SessionState.SetBool(Key + ".Passed", true); Debug.Log("HOOKWARS_MEAT_LIFETIME_PASS: " + count); Stop();
        }
    }
    static void Stop(){if(modesKeyboard!=null){UnityEngine.InputSystem.InputSystem.RemoveDevice(modesKeyboard);modesKeyboard=null;}if(stage=="world")MatchConfiguration.RememberDepth(SessionState.GetInt(Key+".SavedDepth",16));phase=99;EditorApplication.isPlaying=false;}
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
        Check(anchors.Length==10,"Exactly ten ground anchors at depth "+ArenaLayout.Depth);
        Check(anchors.Count(a=>a.transform.position.x<0)==5&&anchors.Count(a=>a.transform.position.x>0)==5,"Exactly five anchors on each bank");
        var rocks=Object.FindObjectsByType<PermanentRock>();int expected=Mathf.Max(0,Mathf.Clamp(Mathf.FloorToInt(ArenaLayout.Depth/3),1,6)-1);
        Check(rocks.Count(r=>r.transform.position.x<0)==expected&&rocks.Count(r=>r.transform.position.x>0)==expected,"One fewer rock per bank at depth "+ArenaLayout.Depth);
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
            player.TakeDamage(player.CurrentKg-1);
            var weakCow=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),movement.transform.position+Vector3.right*.95f,Quaternion.identity);weakCow.enabled=false;
            Check(Vector3.Distance(WorldCollision.Slide(movement.transform,desired,ArenaLayout.HeroRadius),desired)<.001f,"Even injured hero is not stopped by a free-standing animal");Object.DestroyImmediate(weakCow.gameObject);
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
            File.WriteAllText("docs/validation/020-world.txt",$"PASS: {count} actual Unity Play Mode assertions\nFive upright immutable anchors per bank at 16/4/20m, one fewer rock per bank, actual hit and owner pull, safe ending, stationary fixture, cow pushing, river exclusion, new rib steak meshes and actual spin.\n");
            SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_EXPANSION_WORLD_PASS: "+count);Stop();
        }
    }
    static void CaptureFog(string name)
    {
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        ScreenCapture.CaptureScreenshot("docs/validation/016-fog-"+name+".png");
    }
    static void FogTick()
    {
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
        var enemy=Object.FindAnyObjectByType<EnemyChase>().GetComponent<Health>();
        if(phase==1)
        {
            DisableAI();var movement=Object.FindAnyObjectByType<PlayerMovement>();movement.enabled=false;player=movement.GetComponent<Health>();
            fog=Object.FindAnyObjectByType<FogOfWar25D>();if(fog==null||display.VisualFor(enemy)==null)return;
            fogOrigin=player.transform.position;fogTarget=enemy.transform.position;fog.RefreshObservers();
            Check(!fog.IsRevealed(fogTarget),"Enemy at home does not reveal its own fog");
            Check(display.VisualFor(enemy).GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Enemy mesh and health bar really hidden, not just tinted");
            Check(fog.IsRevealed(fogOrigin)&&fog.IsRevealed(Vector2.zero),"Own bank and neutral river stay visible");
            CaptureFog("hidden");phase=2;due=Time.time+.25f;return;
        }
        if(phase==2)
        {
            player.transform.position=fogTarget+Vector3.up*3;fog.RefreshObservers();
            Check(fog.IsRevealed(fogTarget)&&!fog.IsRevealed((Vector2)player.transform.position+Vector2.down*5),"Friendly hero reveals exactly four-meter circle on enemy bank");
            phase=3;due=Time.time+.2f;return;
        }
        if(phase==3)
        {
            Check(display.VisualFor(enemy).GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!r.forceRenderingOff),"Approaching hero restores actual enemy renderers");CaptureFog("hero");
            phase=8;due=Time.time+.15f;return;
        }
        if(phase==8)
        {
            player.transform.position=fogOrigin;
            fogHelper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ButcherHelper>(GameplayRevisionSetup.HelperPath),fogTarget+Vector3.up*3,Quaternion.identity);
            fogHelper.Bind(player.GetComponent<OwnerEconomy>().Factory,10);fogHelper.enabled=false;fogHelper.GetComponent<CarcassCarrier>().enabled=false;
            due=Time.time+.2f;phase=4;return;
        }
        if(phase==4)
        {
            fog.RefreshObservers();Check(fog.IsRevealed(fogTarget),"Owned helper also reveals its surroundings");Object.DestroyImmediate(fogHelper.gameObject);
            foreach(var rock in Object.FindObjectsByType<PermanentRock>())Object.DestroyImmediate(rock.gameObject);
            foreach(var anchor in Object.FindObjectsByType<HookAnchor>())Object.DestroyImmediate(anchor.gameObject);
            travelHook=player.GetComponent<HookAbility>();EconomyChecks.Set(travelHook,"targetLayers",(LayerMask)0);
            ReadyHookForFog();travelHook.TryThrow((Vector2)(fogTarget+Vector3.up*3-fogOrigin));
            started=Time.time;due=0;phase=5;return;
        }
        if(phase==5)
        {
            if((travelHook.ActiveHook==null||travelHook.ActiveHook.position.x<4.2f)&&Time.time-started<3)return;
            Check(travelHook.ActiveHook!=null&&travelHook.ActiveHook.position.x>2,"Real hook entered enemy bank");
            fog.RefreshObservers();Vector2 p=travelHook.ActiveHook.position,d=(p-(Vector2)fogOrigin).normalized,side=new Vector2(-d.y,d.x);
            Check(fog.IsRevealed(p+side*.9f)&&!fog.IsRevealed(p+side*1.5f),"Hook route reveals one meter each side without widening the whole bank");
            CaptureFog("hook");fogProbe=p;due=Time.time+.15f;phase=6;return;
        }
        if(phase==6){travelHook.CancelHook();due=Time.time+.2f;phase=7;return;}
        if(phase==7)
        {
            fog.RefreshObservers();Check(!fog.IsRevealed(fogProbe),"Returned hook leaves no permanently explored corridor");
            Check(display.VisualFor(enemy).GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Enemy becomes hidden again when observers leave");
            File.WriteAllText("docs/validation/016-fog.txt",$"PASS: {count} actual Unity Play Mode assertions\nPlayer perspective; own bank and river; real enemy renderer hiding; hero/helper circles; actual flying hook corridor and its removal. Random terrain removed only from the corridor test fixture.\n");
            SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_EXPANSION_FOG_PASS: "+count);Stop();
        }
    }
    static void ReadyHookForFog(){travelHook.CancelHook();EconomyChecks.Set(travelHook,"nextThrowTime",0f);}
}
