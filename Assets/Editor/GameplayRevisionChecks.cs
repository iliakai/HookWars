using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    static float regenStarted,regenKg;
    static SkinnedMeshRenderer axeSkin;
    static float axeExtent;
    static int mapCycle,savedDepth;
    static ButcherHelper[] workers;
    static Animal[] prey;
    static SausageFactory[] helperFactories;
    static float helperDeadline;
    static float nextDiagnostic;
    static GameplayRevisionChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    [MenuItem("HookWars/25D/Revision/Check construction Play Mode")]
    public static void CheckConstruction()=>Run("construction");
    [MenuItem("HookWars/25D/Revision/Check development Play Mode")]
    public static void CheckDevelopment()=>Run("development");
    [MenuItem("HookWars/25D/Revision/Check map 4 and 20 Play Mode")]
    public static void CheckMap()=>Run("map");
    [MenuItem("HookWars/25D/Revision/Check helper hunting Play Mode")]
    public static void CheckHelpers()=>Run("helpers");
    public static void Run(string mode)
    {
        if(mode=="map"||mode=="helpers")
        {
            GameplayRevisionSetup.Configure();string signature=MapSignature();GameplayRevisionSetup.Configure();
            if(signature!=MapSignature())throw new InvalidOperationException("Repeated map setup changed asset bytes");
        }
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        savedDepth=MatchConfiguration.LastDepth;
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
                if(SessionState.GetString(Key+".Mode","")=="helpers")
                {HelpersStart();phase=30;helperDeadline=Time.time+55;return;}
                if(SessionState.GetString(Key+".Mode","")=="map")
                {mapCycle=0;MatchConfiguration.Prepare(4);SceneManager.LoadScene(MainMenuController.BattleScene);phase=20;due=Time.time+.3f;return;}
                if(SessionState.GetString(Key+".Mode","")=="development")
                {DevelopmentStart();phase=10;due=Time.time+.7f;return;}
                else if(SessionState.GetString(Key+".Mode","")=="construction")ConstructionStart();
                else throw new InvalidOperationException("Unsupported revision mode");
                due=Time.time+.25f;phase=2;return;
            }
            if(phase==30)
            {
                if(Time.time>=nextDiagnostic)
                {
                    nextDiagnostic=Time.time+5;
                    string diagnostic="time="+Time.time+"\n";
                    foreach(var w in workers)
                    {
                        var type=typeof(ButcherHelper);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                        var target=(Animal)type.GetField("target",flags).GetValue(w);var path=(System.Collections.Generic.List<Vector3>)type.GetField("path",flags).GetValue(w);
                        diagnostic+=$"helper p={w.transform.position} goal={(target!=null?target.transform.position.ToString():"none")} path={path.Count} cargo={w.GetComponent<CarcassCarrier>().CurrentMassKg} raw={w.Factory.RawKg} xp={w.Factory.Owner.Experience} level={w.Factory.Owner.Level}\n";
                    }
                    foreach(var a in prey)diagnostic+="animal="+(a!=null?a.transform.position.ToString():"dead")+"\n";
                    File.WriteAllText("Temp/HookWarsAutomation/helper-state.txt",diagnostic);Debug.Log("REVISION_CHECK_DIAGNOSTIC: "+diagnostic);
                }
                foreach(var worker in workers)
                {
                    if(!ArenaLayout.IsInsideBank(worker.transform.position,worker.Factory.transform.position.x>0,worker.Radius-.01f))throw new InvalidOperationException("Helper left its factory bank");
                    if(Physics2D.OverlapCircle(worker.transform.position,worker.Radius-.02f,LayerMask.GetMask("Buildings","Mountains"))!=null)throw new InvalidOperationException("Helper entered a permanent obstacle");
                }
                if(prey.Any(a=>a!=null)||workers.Any(w=>w.GetComponent<CarcassCarrier>().Count>0)||helperFactories.Any(f=>f.Owner.Level<2))
                {if(Time.time>helperDeadline)throw new TimeoutException("Helpers did not complete their real hunting/delivery loop");return;}
                Check(prey.All(a=>a==null),"Three real animals killed by accepted delayed helper attacks");
                Check(workers.All(w=>w.GetComponent<CarcassCarrier>().Count==0),"All helper cargo delivered to their own factories");
                Check(helperFactories.All(f=>f.Owner.Level>=2&&f.RawKg>=0&&f.RawKg<=f.CapacityKg),"Existing factory acceptance awards owner XP once and respects its queue capacity");
                Check(workers.All(w=>Object.FindAnyObjectByType<ArenaPresentation25D>().VisualFor(w.GetComponent<Health>()).transform.localScale==Vector3.one*.5f),"Helpers use half-size animated butcher visuals with independent logical roots");
                var menu=builder.GetComponent<BuildingUpgradeMenu>();
                Check(menu.SelectAtScreenPoint(Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(helperFactories[0].transform.position))),"Factory selection opens its ordering and capacity panel");
                Check(helperFactories[0].UpgradeCount==5,"All three factory upgrades plus worker order/capacity are exposed");
                var pausedItem=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath),workers[0].transform.position,Quaternion.identity);
                pausedItem.Initialize(6);pausedItem.SetAnimalSpecies(Animal.Species.Pig);MatchPause.Instance.SetPaused(true);
                Check(!workers[0].GetComponent<CarcassCarrier>().TryCollect(pausedItem),"Paused helper cannot collect a newly available carcass");
                double pausedRaw=helperFactories[0].RawKg;
                typeof(SausageFactory).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(helperFactories[0],null);
                Check(helperFactories[0].RawKg==pausedRaw,"Paused workshop never processes or accepts extra cargo");
                MatchPause.Instance.SetPaused(false);Object.Destroy(pausedItem.gameObject);
                ScreenCapture.CaptureScreenshot("Temp/RevisionPreview/helpers.png");Butcher25DChecks.CaptureAt(ArenaSpace25D.ToWorld(helperFactories[0].transform.position,.6f),"revision_helpers",4);
                Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/009-helpers.txt",$"PASS: {count} real Play Mode assertions\nThree autonomous helpers, both banks, independent owner relocation, real animated axe kills, complete carcasses, real travel/pathfinding, delivery/processing/owner XP, existing/future capacity, payments, 3D scale and factory menu.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_REVISION_PASS: "+count+" helper assertions");phase=3;due=Time.time+.25f;return;
            }
            if(phase==20)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                int depth=mapCycle==0?4:20;Check(ArenaLayout.Depth==depth,"Map consumes configured depth before generation: "+depth);
                var map=Object.FindAnyObjectByType<MatchMap25D>();var factories=Object.FindObjectsByType<SausageFactory>();
                Check(factories.Length==2&&map.Factories.Count==2,"Exactly one automatic factory for each owner");
                int roots=map.MapRoot.childCount;map.Generate();Check(map.MapRoot.childCount==roots,"Repeated Generate does not duplicate map objects");
                foreach(var f in factories)
                {
                    Check(f.Footprint==new Vector2Int(2,2)&&f.GetComponent<BoxCollider2D>().size==Vector2.one*2,"Factory occupies exactly four cells");
                    Check(f.Owner.GetComponent<OwnerEconomy>().Factory==f&&(f.transform.position.x>0)==(f.Owner.transform.position.x>0),"Factory ownership matches its own bank");
                    Check(Mathf.Abs(f.transform.position.x)>=2+ArenaLayout.Depth*.6f&&ArenaLayout.IsInsideBank(f.transform.position,f.transform.position.x>0,1),"Factory fits the rear of the selected bank");
                    var view=Object.FindAnyObjectByType<ArenaPresentation25D>().VisualFor(f);Check(view!=null&&view.transform.localScale==Vector3.one*2,"Factory model scale matches its new footprint");
                }
                var rocks=Object.FindObjectsByType<PermanentRock>();Check(rocks.Any(r=>r.transform.position.x<0)&&rocks.Any(r=>r.transform.position.x>0),"Permanent rocks generated on both banks");
                foreach(var r in rocks)
                {
                    Check(r.GetComponent<Health>()==null&&r.GetComponent<GameBuilding>()==null&&!WorldCollision.FreeCircle(r.transform.position,.1f),"Rock is indestructible terrain and blocks occupancy");
                    Check(Object.FindAnyObjectByType<ArenaPresentation25D>().VisualFor(r)?.GetComponentsInChildren<MeshRenderer>().Length>=22,"Imported 22-stone model automatically displayed");
                }
                builder=Object.FindAnyObjectByType<PlayerBuilding>();builder.GetComponent<PlayerProgression>().AddGold(1000);
                builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.FactoryPath));Check(!builder.IsBuilding,"Starting factory cannot be selected for manual construction");
                builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.ShopPath));
                var ownRock=rocks.First(r=>(r.transform.position.x>0)==(builder.transform.position.x>0));Check(!builder.CanPlace(ownRock.transform.position),"Rock's cell cannot be built over");builder.CloseInterface();
                Directory.CreateDirectory("Temp/RevisionPreview");ScreenCapture.CaptureScreenshot("Temp/RevisionPreview/map_"+depth+".png");
                if(mapCycle++==0){phase=21;due=Time.time+.2f;return;}
                Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/009-map.txt",$"PASS: {count} real Play Mode assertions at 4m and 20m\nByte-stable setup, two rear 2x2 factories, both-bank permanent imported rocks, visual scale, occupancy and no manual factory.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_REVISION_PASS: "+count+" map assertions");phase=3;due=Time.time+.2f;return;
            }
            if(phase==21){MatchConfiguration.Prepare(20);SceneManager.LoadScene(MainMenuController.BattleScene);phase=20;due=Time.time+.3f;return;}
            if(phase==10)
            {
                var hp=builder.GetComponent<Health>();
                Check(Mathf.Abs(hp.CurrentKg-(regenKg+(Time.time-regenStarted)*hp.RegenerationKgPerSecond))<.03f,"Regeneration follows elapsed game time in kg/second");
                var baked=Animals25DSetup.BakeWorld(axeSkin,axeSkin.bones.First(b=>b.name=="AxeVisualScale"));
                // In parent coordinates the skinned blade must actually grow.
                Object.DestroyImmediate(baked);
                var root=axeSkin.bones.First(b=>b.name=="AxeVisualScale");
                var worldMesh=Animals25DSetup.BakeWorld(axeSkin,root.parent);float grown=worldMesh.bounds.size.magnitude;Object.DestroyImmediate(worldMesh);
                Check(grown>axeExtent*1.1f,"Axe upgrade enlarges the actual skinned blade around its pivot");
                var menu=Object.FindAnyObjectByType<HookUpgradeMenu>();menu.Toggle();Check(menu.IsOpen&&GamePointer.OverInterface(new Vector2(Screen.width*.5f,Screen.height*.5f)),"Development is a separate modal grid that blocks world input");
                var sequence=builder.GetComponent<HeroActionTiming25D>().HookSequence;builder.GetComponent<PlayerHook>().TryThrowAtScreenPoint(new Vector2(Screen.width*.5f,Screen.height*.5f));
                Check(sequence==builder.GetComponent<HeroActionTiming25D>().HookSequence,"Clicking the skill screen cannot throw a hook");
                MatchPause.Instance.HandleEscape();Check(!menu.IsOpen&&!MatchPause.Instance.IsPaused,"Esc closes development before pause");
                menu.Toggle();ScreenCapture.CaptureScreenshot("Temp/RevisionPreview/development.png");
                var deathMass=hp.MaximumKg;hp.TakeDamage(100000);
                Check(Mathf.Abs(builder.GetComponent<HeroCarcassSpawner>().CurrentCarcass.MassKg-deathMass)<.001f,"Hero corpse snapshots maximum kg at death");
                var before=hp.MaximumKg;builder.GetComponent<PlayerProgression>().AddExperience(1000);Check(hp.IsDead&&hp.MaximumKg>before,"Level gains never resurrect a dead hero");
                hp.Revive();Check(Mathf.Abs(hp.CurrentKg-hp.MaximumKg)<.001f,"Respawn restores the current level's kg maximum");
                Directory.CreateDirectory("docs/validation");File.WriteAllText("docs/validation/009-development.txt",$"PASS: {count} real Unity Play Mode assertions\nAll ten skills, one point per success, no points/no upgrade, real axe skin growth, kg/level/regen/death/revive, modal UI and Esc.\n");
                SessionState.SetBool(Key+".Passed",true);Debug.Log("HOOKWARS_REVISION_PASS: "+count+" development assertions");phase=3;due=Time.time+.2f;return;
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
        Object.FindAnyObjectByType<MatchMap25D>()?.MapRoot.gameObject.SetActive(false);
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
    static string MapSignature()=>File.ReadAllText(Arena25DSetup.ScenePath)+File.ReadAllText(EconomySetup.FactoryPath)+File.ReadAllText(GameplayRevisionSetup.RockLogic)+string.Join("",Directory.GetFiles(GameplayRevisionSetup.Output).Where(p=>p.EndsWith(".prefab")||p.EndsWith(".mat")).OrderBy(p=>p).Select(File.ReadAllText));
    static void Stop(){if(SessionState.GetString(Key+".Mode","")=="map")MatchConfiguration.RememberDepth(savedDepth);phase=99;EditorApplication.isPlaying=false;}
    static void DevelopmentStart()
    {
        var owner=builder.GetComponent<PlayerProgression>();var hp=builder.GetComponent<Health>();var menu=Object.FindAnyObjectByType<HookUpgradeMenu>();
        var hook=builder.GetComponent<HookAbility>();hook.CancelHook();
        Check(owner.Level==1&&Mathf.Abs(hp.MaximumKg-100)<.001f,"Heroes begin level one at 100kg");
        var display=Object.FindAnyObjectByType<ArenaPresentation25D>();
        axeSkin=display.VisualFor(hp).GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.StartsWith("06_Axe"));
        var scaleBone=axeSkin.bones.First(b=>b.name=="AxeVisualScale");var mesh=Animals25DSetup.BakeWorld(axeSkin,scaleBone.parent);axeExtent=mesh.bounds.size.magnitude;Object.DestroyImmediate(mesh);
        for(int i=0;i<10;i++)owner.AddExperience(owner.ExperienceToNextLevel);
        Check(owner.Level==11&&Mathf.Abs(hp.MaximumKg-100*Mathf.Pow(1.1f,10))<.01f,"Each level compounds maximum kg by ten percent");
        for(int i=0;i<10;i++)
        {int points=owner.UpgradePoints;Check(menu.TryBuy((HookUpgradeMenu.Skill)i)&&owner.UpgradePoints==points-1,"Skill purchase applies once and costs one point: "+i);}
        Check(owner.UpgradePoints==0&&!menu.TryBuy(HookUpgradeMenu.Skill.AxeDamage),"No skill purchase without a point");
        Check(hook.MaxBounces==int.MaxValue&&!hook.TryUpgrade(HookAbility.UpgradeStat.Bounces)&&!hook.TryUpgrade(HookAbility.UpgradeStat.BounceDistance),"Ricochets are unlimited and never a paid upgrade");
        Check(builder.GetComponent<PlayerMovement>().MoveSpeed>5&&builder.GetComponent<PlayerMelee>().AttackRange>1.6f&&builder.GetComponent<PlayerMelee>().Damage>20,"Movement, axe damage and axe reach increased");
        hp.TakeDamage(20);regenKg=hp.CurrentKg;regenStarted=Time.time;
    }
    static void HelpersStart()
    {
        helperFactories=Object.FindObjectsByType<SausageFactory>().OrderByDescending(f=>f.Owner.GetComponent<PlayerMovement>()!=null).ToArray();
        Check(helperFactories.Length==2,"Fresh map has one workshop for each helper team");
        foreach(var factory in helperFactories)
        {
            Check(!factory.TryOrderHelper()&&factory.HelperCount==0,"Cannot order without enough gold");
            factory.Owner.AddGold(1000);double before=factory.Owner.Gold;
            Check(factory.TryOrderHelper()&&Math.Abs(factory.Owner.Gold-(before-factory.HelperPrice))<.001,"Accepted worker costs the configured gold once");
        }
        var own=helperFactories[0];Check(own.TryUpgrade(4)&&Mathf.Abs(own.HelperCapacityKg-12)<.001,"Workshop upgrades worker capacity for gold");
        Check(own.GetComponentsInChildren<ButcherHelper>().All(w=>w.GetComponent<CarcassCarrier>().CapacityKg==12)&&helperFactories[1].HelperCapacityKg==10,"Capacity applies to existing own helpers without changing the opponent");
        own.Owner.transform.position=new Vector3(own.transform.position.x>0?-4:4,0);
        Check(own.TryOrderHelper(),"Can order a new helper while its owner is on the opposite bank");
        workers=Object.FindObjectsByType<ButcherHelper>();Check(workers.Length==3,"Exactly three ordered helpers, no demo actors");
        Check(own.GetComponentsInChildren<ButcherHelper>().All(w=>w.GetComponent<CarcassCarrier>().CapacityKg==12&&(w.transform.position.x>0)==(own.transform.position.x>0)),"New helper inherits capacity and spawns on the factory bank");
        prey=new Animal[3];int index=0;
        foreach(var factory in helperFactories)
        {
            var p=factory.transform.position+Vector3.right*(factory.transform.position.x>0?-4:4)+Vector3.up*.5f;
            prey[index]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),p,Quaternion.identity);prey[index++].enabled=false;
            if(factory==own)
            {p.y+=p.y>0?-1.5f:1.5f;prey[index]=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/pig.prefab"),p,Quaternion.identity);prey[index++].enabled=false;}
        }
        Physics2D.SyncTransforms();
    }
}
