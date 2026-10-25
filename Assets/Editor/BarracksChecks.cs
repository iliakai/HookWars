using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BarracksChecks
{
    public const string Key="HookWars.BarracksChecks";
    static int phase,count;
    static double ready,deadline,pausedUntil;
    static float due,started,firstDamage;
    static PlayerProgression owner,rival;
    static Barracks barracks,second;
    static SausageWarrior oldSoldier,attacker,defender,ally;
    static ArenaPresentation25D display;
    static Vector3 wanderStart,frozen;
    static float frozenProgress;
    static string report="";
    static BarracksChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=0;report="";due=0;ready=EditorApplication.timeSinceStartup+2;deadline=ready+130;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string message)
    {if(!value)throw new InvalidOperationException(message);count++;report+="PASS "+message+"\n";Debug.Log("BARRACKS_CHECK: "+message);}
    static bool Near(double a,double b)=>Math.Abs(a-b)<.0001;
    static object Call(object obj,string name,params object[] args)
    {return obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(obj,args);}
    static SausageWarrior Soldier(PlayerProgression team,Vector3 position,WarriorStats stats)
    {var soldier=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SausageWarrior>(BarracksSetup.Soldier),position,Quaternion.identity);soldier.Bind(team,stats,false);return soldier;}
    static void ClearStock()
    {var stock=owner.GetComponent<OwnerEconomy>();stock.TakeProduct(stock.ProductKg);stock.TrySpendBones((int)stock.Bones);}
    static void CaptureSoldier(Transform view,string name,float framing)
    {
        var others=display.Views.Values.Where(go=>go!=null&&go.transform!=view&&go.activeSelf).ToArray();
        foreach(var go in others)go.SetActive(false);
        try{view.rotation=Quaternion.Euler(0,180,0);Butcher25DChecks.CaptureAt(view.position+Vector3.up*.55f,name,framing);}
        finally{foreach(var go in others)if(go!=null)go.SetActive(true);}
    }
    static void Finish(bool passed,string detail)
    {
        MatchPause.Instance?.SetPaused(false);SessionState.SetBool(Key+".Passed",passed);phase=99;
        File.WriteAllText("docs/validation/023-barracks.txt",report+detail+"\n");EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Barracks checks timed out in phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                foreach(var carrier in Object.FindObjectsByType<CarcassCarrier>())carrier.enabled=false;
                foreach(var fixture in Object.FindObjectsByType<GameBuilding>())foreach(var c in fixture.GetComponents<Collider2D>())c.enabled=false;
                foreach(var fixture in Object.FindObjectsByType<PermanentRock>())foreach(var c in fixture.GetComponents<Collider2D>())c.enabled=false;
                foreach(var fixture in Object.FindObjectsByType<HookAnchor>())foreach(var c in fixture.GetComponents<Collider2D>())c.enabled=false;
                var player=Object.FindAnyObjectByType<PlayerMovement>();player.enabled=false;owner=player.GetComponent<PlayerProgression>();owner.enabled=false;
                player.GetComponentInChildren<PlayerAim>().enabled=false;
                rival=Object.FindAnyObjectByType<EnemyChase>().GetComponent<PlayerProgression>();rival.enabled=false;
                owner.transform.position=new Vector3(-10,-8);rival.transform.position=new Vector3(10,8);
                display=Object.FindAnyObjectByType<ArenaPresentation25D>();
                var builder=owner.GetComponent<PlayerBuilding>();var prefab=AssetDatabase.LoadAssetAtPath<Barracks>(BarracksSetup.Building);
                Check(prefab.BuildCost==250&&prefab.Footprint==new Vector2Int(2,2),"Barracks costs 250 gold and occupies two by two cells");
                builder.SelectBuilding(prefab);Check(builder.TryBuildAt(new Vector3(-10,-6))==null,"Insufficient building gold creates no barracks");
                owner.AddGold(30000);double gold=owner.Gold;
                barracks=(Barracks)builder.TryBuildAt(new Vector3(-10,-6));builder.CloseInterface();
                Check(barracks!=null&&Near(gold-owner.Gold,250),"Actual construction places and pays for barracks once");
                Check(!barracks.AutoProduction&&!barracks.HasOrder,"New barracks starts switched off without a queued unit");
                owner.transform.position=new Vector3(-12.5f,-6);
                var stock=owner.GetComponent<OwnerEconomy>();stock.AddOutput(4,2);
                gold=owner.Gold;Check(!stock.TrySpendResources(50,5,2)&&Near(stock.ProductKg,4)&&Near(stock.BoneKg,2)&&owner.Gold==gold,"Missing meat cannot partially spend bones or gold");
                Check(!stock.TrySpendResources(50,4,3)&&Near(stock.ProductKg,4)&&owner.Gold==gold,"Missing bones cannot partially spend meat or gold");
                Check(!stock.TrySpendResources(50000,4,2)&&Near(stock.ProductKg,4)&&Near(stock.BoneKg,2),"Missing gold cannot partially spend stock");
                Check(!stock.TrySpendResources(-1,4,2)&&!stock.TrySpendResources(0,double.NaN,0),"Invalid transaction inputs are rejected without mutation");
                Check(barracks.SetProduction(true),"Owner can enable automatic production");
                barracks.Advance(.01f);Check(barracks.HasOrder&&Near(gold-owner.Gold,50)&&Near(stock.ProductKg,0)&&Near(stock.BoneKg,0),"Order atomically spends exactly fifty gold and four meat and two bones");
                for(int i=0;i<6;i++)
                {gold=owner.Gold;Check(barracks.TryUpgrade(i)&&Near(gold-owner.Gold,1000)&&barracks.Upgrades.Level(i)==1,"Improvement branch "+i+" costs one thousand gold");}
                Check(barracks.OrderStats.healthLevel==0&&barracks.OrderStats.criticalLevel==0,"Already started order retains its original upgrade snapshot");
                gold=owner.Gold;Check(barracks.TryUpgrade(0)&&barracks.Upgrades.healthLevel==2&&Near(gold-owner.Gold,1000),"Repeated upgrade still costs exactly one thousand");
                Check(!barracks.TryUpgrade(-1)&&!barracks.TryUpgrade(6),"Invalid upgrade indices are rejected");
                float progress=barracks.Progress;barracks.SetProduction(false);barracks.Advance(30);
                Check(barracks.HasOrder&&barracks.Progress==progress,"Switching production off preserves the paid order and freezes progress");
                barracks.SetProduction(true);MatchPause.Instance.SetPaused(true);barracks.Advance(30);gold=owner.Gold;
                Check(barracks.Progress==progress&&!barracks.TryUpgrade(1)&&!barracks.SetProduction(false)&&owner.Gold==gold,"Pause blocks production and paid management actions");MatchPause.Instance.SetPaused(false);
                var blocker=new GameObject("Validation exit blocker");blocker.layer=LayerMask.NameToLayer("Buildings");blocker.transform.position=new Vector3(-10,-8.1f);blocker.AddComponent<BoxCollider2D>().size=new Vector2(3,2);
                barracks.Advance(20);Check(barracks.HasOrder&&barracks.Progress==1&&Object.FindObjectsByType<SausageWarrior>().Length==0,"Completed unit waits when its exit is blocked");
                gold=owner.Gold;barracks.Advance(30);Check(owner.Gold==gold&&barracks.HasOrder,"Waiting at blocked exit cannot spend another order");
                Object.DestroyImmediate(blocker);EconomyChecks.Set(barracks,"nextExitCheck",0f);barracks.Advance(.01f);
                oldSoldier=Object.FindAnyObjectByType<SausageWarrior>();
                Check(oldSoldier!=null&&oldSoldier.Stats.healthLevel==0&&oldSoldier.GetComponent<Health>().MaximumKg==40,"Released unit has the original forty kilogram snapshot");
                Check(!barracks.HasOrder&&ArenaLayout.IsInsideBank(oldSoldier.transform.position,false,SausageWarrior.BodyRadius),"One completed order produces one soldier on dry land");
                oldSoldier.transform.position=new Vector3(-14,-4);oldSoldier.enabled=false;
                second=Object.Instantiate(prefab,new Vector3(-6,-6),Quaternion.identity);second.Initialize(owner);second.SetProduction(true);
                Check(second.Upgrades.healthLevel==0,"Different barracks has independent upgrade levels");
                stock.AddOutput(4,2);gold=owner.Gold;barracks.Advance(.01f);second.Advance(.01f);
                Check(barracks.HasOrder&&!second.HasOrder&&Near(gold-owner.Gold,50),"Competing barracks cannot spend the same stock twice");
                Check(barracks.OrderStats.healthLevel==2&&barracks.OrderStats.poisonLevel==1,"Next order captures all purchased upgrades");
                barracks.SetProduction(false);second.SetProduction(false);
                var stats=barracks.Upgrades;
                Check(stats.Health==60&&stats.Damage==7&&Near(stats.Range,1)&&Near(stats.AttackPeriod,1.2/1.1),"Health damage range and attack period follow the approved formulas");
                Check(Near(stats.CriticalChance,.118)&&stats.Poison==1&&stats.BodyScale>1&&stats.ClubLength>1&&stats.ClubWidth>1,"Poison probability and model growth follow purchased levels");
                var high=new WarriorStats{healthLevel=100000,criticalLevel=1000,speedLevel=1000};
                Check(high.Health>stats.Health&&high.AttackPeriod>0&&high.CriticalChance<=1&&high.CriticalChance>.99,"Large levels retain finite health positive periods and valid probability");
                ClearStock();stock.AddOutput(4,2);
                second.SetProduction(true);started=Time.time;wanderStart=oldSoldier.transform.position;oldSoldier.enabled=true;
                phase=2;due=Time.time+.25f;return;
            }
            if(phase==2)
            {
                Check(second.HasOrder&&second.Progress>0&&second.Progress<.15f,"Real Update starts and advances an automatic twelve second order");
                var view=display.VisualFor(barracks);var soldierView=display.VisualFor(oldSoldier.GetComponent<Health>());
                Check(view!=null&&soldierView!=null,"Both models are registered as detached XZ presentation");
                Check(view.GetComponentsInChildren<MeshRenderer>().Length>=11&&soldierView.GetComponentsInChildren<SkinnedMeshRenderer>().Length==4,"Authored barracks meshes and all four soldier surfaces are present");
                Check(view.GetComponentsInChildren<Collider>().Length==0&&soldierView.GetComponentsInChildren<Collider>().Length==0,"Imported graphics contain no duplicate three dimensional gameplay physics");
                Check(soldierView.GetComponentInChildren<Animator>().runtimeAnimatorController!=null&&!soldierView.GetComponentInChildren<Animator>().applyRootMotion,"Soldier has configured Generic animation with root motion disabled");
                Check(soldierView.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),"All imported model materials use supported shaders");
                Butcher25DChecks.CaptureAt(view.position+Vector3.up*.6f,"barracks",1.8f);File.Copy("Temp/ButcherPreview/barracks.png","docs/validation/023-barracks.png",true);
                CaptureSoldier(soldierView,"sausage_warrior",.85f);File.Copy("Temp/ButcherPreview/sausage_warrior.png","docs/validation/023-warrior.png",true);
                phase=3;due=started+3;return;
            }
            if(phase==3)
            {
                Check(Vector2.Distance(oldSoldier.transform.position,wanderStart)>.1f,"An idle soldier wanders on actual game frames");
                Check(ArenaLayout.IsInsideBank(oldSoldier.transform.position,false,SausageWarrior.BodyRadius),"Wandering soldier stays on its dry home territory");
                frozen=oldSoldier.transform.position;frozenProgress=second.Progress;MatchPause.Instance.SetPaused(true);pausedUntil=EditorApplication.timeSinceStartup+.5;phase=4;return;
            }
            if(phase==4)
            {
                if(EditorApplication.timeSinceStartup<pausedUntil)return;
                Check(oldSoldier.transform.position==frozen&&second.Progress==frozenProgress,"Real pause freezes wandering and automatic production");MatchPause.Instance.SetPaused(false);
                phase=5;due=Time.time+(12*(1-second.Progress))+.25f;return;
            }
            if(phase==5)
            {
                Check(Object.FindObjectsByType<SausageWarrior>().Length==2&&!second.HasOrder,"Twelve second automatic production releases exactly one new soldier and waits for more resources");
                int parking=0;foreach(var soldier in Object.FindObjectsByType<SausageWarrior>()){soldier.enabled=false;soldier.transform.position=new Vector3(-15,-8+parking++);}
                attacker=Soldier(owner,new Vector3(-7,-3.7f),new WarriorStats{poisonLevel=1});
                defender=Soldier(rival,new Vector3(-7,-3),new WarriorStats{healthLevel=20});defender.enabled=false;
                ally=Soldier(owner,new Vector3(-6.8f,-3.4f),default);ally.enabled=false;
                started=Time.time;phase=6;due=Time.time+.2f;return;
            }
            if(phase==6)
            {
                if(!attacker.Attacking&&Time.time-started<2)return;
                Check(attacker.Attacking&&defender.GetComponent<Health>().CurrentKg==240,"Soldier starts its attack animation without dealing immediate damage");
                frozenProgress=attacker.AttackPhase;MatchPause.Instance.SetPaused(true);pausedUntil=EditorApplication.timeSinceStartup+.4;phase=14;due=0;return;
            }
            if(phase==14)
            {
                if(EditorApplication.timeSinceStartup<pausedUntil)return;
                Check(attacker.Attacking&&attacker.AttackPhase==frozenProgress&&defender.GetComponent<Health>().CurrentKg==240,"Pausing mid-swing freezes its phase without cancelling or restarting the attack");
                MatchPause.Instance.SetPaused(false);phase=7;due=Time.time+.55f;return;
            }
            if(phase==7)
            {
                float damage=240-defender.GetComponent<Health>().CurrentKg;
                if(damage==0&&Time.time-started<3)return;
                Check(damage==5||damage==10,"Native animation timing applies one normal hit or exactly double critical damage");
                Check(ally.GetComponent<Health>().CurrentKg==40,"An ally in the same attack sector takes no friendly damage");
                Check(defender.GetComponent<WarriorPoison>()?.ActiveDoses==1,"Successful strike applies poison once to its enemy");
                attacker.enabled=false;firstDamage=defender.GetComponent<Health>().CurrentKg;
                phase=8;due=Time.time+1.1f;return;
            }
            if(phase==8)
            {
                var poison=defender.GetComponent<WarriorPoison>();poison.enabled=false;
                Check(Near(firstDamage-defender.GetComponent<Health>().CurrentKg,1),"Poison deals one kilogram on a real one second game tick");
                var fixture=Soldier(rival,new Vector3(-5,5),new WarriorStats{healthLevel=100});fixture.enabled=false;
                var h=fixture.GetComponent<Health>();float before=h.CurrentKg;
                WarriorPoison.Apply(h,attacker.GetComponent<Health>(),1);var effect=h.GetComponent<WarriorPoison>();effect.enabled=false;
                // Disabling clears effects; reapply before manually advancing isolated ticks.
                WarriorPoison.Apply(h,attacker.GetComponent<Health>(),1);effect.Advance(10);
                Check(Near(before-h.CurrentKg,10)&&effect.ActiveDoses==0,"Fresh poison expires after exactly ten isolated one second ticks");
                before=h.CurrentKg;WarriorPoison.Apply(h,attacker.GetComponent<Health>(),2);WarriorPoison.Apply(h,ally.GetComponent<Health>(),1);effect.Advance(1);
                Check(Near(before-h.CurrentKg,2),"Overlapping poisons use the strongest damage instead of adding together");
                WarriorPoison.Apply(h,attacker.GetComponent<Health>(),2);MatchPause.Instance.SetPaused(true);before=h.CurrentKg;effect.Advance(10);
                Check(before==h.CurrentKg,"Pause also blocks isolated poison advancement");MatchPause.Instance.SetPaused(false);
                h.TakeDamage(10000);Check(effect.ActiveDoses==0,"Death clears all poison doses");h.Revive();Check(effect.ActiveDoses==0,"Reviving cannot restore poison from the previous life");Object.DestroyImmediate(fixture.gameObject);
                var wall=new GameObject("Validation attack wall");wall.layer=LayerMask.NameToLayer("Buildings");wall.transform.position=new Vector3(-7,-3.35f);wall.AddComponent<BoxCollider2D>().size=new Vector2(1,.1f);
                Check(!WorldCollision.ClearAttackLine(attacker.transform.position,defender.transform.position),"A solid wall blocks the melee line");
                before=defender.GetComponent<Health>().CurrentKg;Call(attacker,"ApplyStrike");Check(defender.GetComponent<Health>().CurrentKg==before,"An actual strike cannot damage through the wall");Object.DestroyImmediate(wall);
                var hook=owner.GetComponent<PlayerHook>();owner.transform.position=new Vector3(-9,1);defender.transform.position=new Vector3(-7,1);
                CallBase(hook,"ReleaseThrow",Vector2.right);CallBase(hook,"HitAndPull",defender.transform);
                Check(defender.GetComponent<HookVictim>().IsCaught,"Existing hero hook can damage and capture a sausage warrior");hook.CancelHook();
                Check(!defender.GetComponent<HookVictim>().IsCaught,"Cancelling the hook releases the warrior");
                var upgraded=Soldier(owner,new Vector3(-12,-1),new WarriorStats{healthLevel=4,damageLevel=5,areaLevel=4,poisonLevel=10,criticalLevel=10});upgraded.enabled=false;
                oldSoldier=upgraded;phase=9;due=Time.time+.2f;return;
            }
            if(phase==9)
            {
                var view=display.VisualFor(oldSoldier.GetComponent<Health>());Check(view!=null&&Near(view.localScale.x,1.2),"Health improvements visibly enlarge the soldier model");
                var weapon=view.GetComponentsInChildren<Transform>().Single(t=>t.name=="Weapon");
                Check(weapon.localScale.y>weapon.localScale.x,"Area upgrade extends the weapon bone independently from its damage thickness");
                var club=view.GetComponentsInChildren<Renderer>().Single(r=>r.name.Contains("Femur_Club"));var block=new MaterialPropertyBlock();club.GetPropertyBlock(block,0);
                Check(block.GetColor("_BaseColor")==oldSoldier.Stats.ClubColor,"Poison upgrades tint only the club with the soldier's acid color");
                var body=view.GetComponentsInChildren<Renderer>().Single(r=>r.name.Contains("Sausage_Body"));body.GetPropertyBlock(block,0);
                Check(block.GetColor("_BaseColor")==oldSoldier.Stats.BodyColor,"Critical chance upgrades apply the individual roasted body tint");
                CaptureSoldier(view,"sausage_upgraded",1.05f);File.Copy("Temp/ButcherPreview/sausage_upgraded.png","docs/validation/023-warrior-upgraded.png",true);
                double gold=owner.Gold;var stock=owner.GetComponent<OwnerEconomy>();double meat=stock.ProductKg,bones=stock.BoneKg;int alive=Object.FindObjectsByType<SausageWarrior>().Length;
                owner.transform.position=new Vector3(-10,-8);
                Check(barracks.Demolish(owner)&&!barracks.gameObject.activeSelf,"Owner can demolish a barracks with an unfinished paid order");
                Check(gold==owner.Gold&&meat==stock.ProductKg&&bones==stock.BoneKg&&Object.FindObjectsByType<SausageWarrior>().Length==alive,"Demolition refunds nothing and preserves already released soldiers");
                var critical=Soldier(owner,new Vector3(-8,5),new WarriorStats{criticalLevel=2000});critical.enabled=false;
                var criticalTarget=Soldier(rival,new Vector3(-8,5.7f),new WarriorStats{healthLevel=20});criticalTarget.enabled=false;
                float healthBefore=criticalTarget.GetComponent<Health>().CurrentKg;Call(critical,"ApplyStrike");
                Check(Near(healthBefore-criticalTarget.GetComponent<Health>().CurrentKg,10),"A guaranteed critical hit doubles base damage instead of changing its multiplier with upgrades");
                Object.DestroyImmediate(critical.gameObject);Object.DestroyImmediate(criticalTarget.gameObject);
                rival.transform.position=oldSoldier.transform.position+Vector3.up*.7f;
                firstDamage=oldSoldier.GetComponent<Health>().CurrentKg;rival.GetComponent<EnemyMelee>().enabled=true;
                var fog=Object.FindAnyObjectByType<FogOfWar25D>();defender.transform.position=new Vector3(6,0);phase=10;due=Time.time+.2f;return;
            }
            if(phase==10)
            {
                var view=display.VisualFor(defender.GetComponent<Health>());Check(view.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Enemy soldier and health graphics obey the unrevealed enemy bank fog");
                phase=11;due=Time.time+1.1f;return;
            }
            if(phase==11)
            {
                Check(oldSoldier.GetComponent<Health>().CurrentKg<firstDamage,"Enemy hero retaliates against a nearby hostile warrior on native animation frames");
                rival.GetComponent<EnemyMelee>().enabled=false;
                Check(MatchPause.Instance.ReturnToMenu(),"Leaving a populated barracks match is accepted");phase=12;due=0;return;
            }
            if(phase==12)
            {
                var menu=Object.FindAnyObjectByType<MainMenuController>();if(menu==null)return;
                Check(Object.FindObjectsByType<Barracks>().Length==0&&Object.FindObjectsByType<SausageWarrior>().Length==0&&Object.FindObjectsByType<WarriorPoison>().Length==0&&Time.timeScale==1,"Returning to menu removes barracks soldiers poison and pause state");
                Check(menu.StartBattle(),"A new match can start after barracks production and combat");phase=13;return;
            }
            if(phase==13)
            {
                var player=Object.FindAnyObjectByType<PlayerMovement>();if(player==null||Object.FindAnyObjectByType<MainMenuController>()!=null)return;
                Check(Object.FindObjectsByType<Barracks>().Length==0&&Object.FindObjectsByType<SausageWarrior>().Length==0&&player.GetComponent<OwnerEconomy>().ProductKg==0,"New match starts without previous soldiers barracks upgrades or product stock");
                string summary="HOOKWARS_BARRACKS_PASS: "+count;Debug.Log(summary);Finish(true,summary);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(false,"FAIL "+error);}
    }
    static object CallBase(HookAbility ability,string name,params object[] args)=>typeof(HookAbility).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ability,args);
}
