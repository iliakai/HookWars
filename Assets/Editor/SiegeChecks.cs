using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SiegeChecks
{
    public const string Key="HookWars.SiegeChecks";
    private static int phase,count,depthCycle;
    private static float due,started,hp;
    private static double ready,deadline;
    private static string report;
    private static PlayerProgression owner,rival;
    private static Barracks barracks;
    private static RiverBridge bridge;
    private static SausageWarrior soldier;
    private static MeatShop shop,enemyShop;
    private static AnimalFarm farm;
    private static ArenaPresentation25D display;
    private static Mouse mouse;
    private static Keyboard keyboard;
    private static ButcherHelper returningHelper;
    private static bool helperCrossed;
    private static Vector2 dragMin,dragMax;
    private static bool sawArch;
    static SiegeChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();SessionState.SetInt(Key+".Depth",MatchConfiguration.LastDepth);
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=depthCycle=0;report="";due=0;ready=EditorApplication.timeSinceStartup+3;deadline=ready+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);count++;report+="PASS "+text+"\n";Debug.Log("SIEGE_CHECK: "+text);}
    static bool Near(double a,double b)=>Math.Abs(a-b)<.001;
    static T Spawn<T>(string path,Vector3 position,PlayerProgression team) where T:GameBuilding
    {var b=Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>(path),position,Quaternion.identity);if(!b.Initialize(team))throw new InvalidOperationException("Fixture ownership");return b;}
    static void MouseState(Vector2 position,ushort buttons)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=buttons});
    static void Capture(Component source,string file,float size,float height=.8f)
    {
        var view=display.VisualFor(source);if(view==null)throw new InvalidOperationException("Missing view for "+source);
        var others=display.Views.Where(p=>p.Key!=source&&p.Value!=null&&p.Value.activeSelf).Select(p=>p.Value).ToArray();
        foreach(var go in others)go.SetActive(false);
        try{Butcher25DChecks.CaptureAt(view.position+Vector3.up*height,file,size);File.Copy("Temp/ButcherPreview/"+file+".png","docs/validation/"+file+".png",true);}
        finally{foreach(var go in others)if(go!=null)go.SetActive(true);}
    }
    static void Finish(bool passed,string detail)
    {
        if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);mouse=null;
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
        Time.timeScale=1;MatchConfiguration.RememberDepth(SessionState.GetInt(Key+".Depth",16));
        SessionState.SetBool(Key+".Passed",passed);File.WriteAllText("docs/validation/024-siege.txt",report+detail+"\n");
        phase=99;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Siege checks phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                foreach(var ai in Object.FindObjectsByType<EnemyHookAI>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyChase>())ai.enabled=false;
                foreach(var ai in Object.FindObjectsByType<EnemyMelee>())ai.enabled=false;
                foreach(var c in Object.FindObjectsByType<CarcassCarrier>())c.enabled=false;
                var player=Object.FindAnyObjectByType<PlayerMovement>();player.enabled=false;player.GetComponentInChildren<PlayerAim>().enabled=false;
                owner=player.GetComponent<PlayerProgression>();owner.enabled=false;rival=Object.FindAnyObjectByType<EnemyChase>().GetComponent<PlayerProgression>();rival.enabled=false;
                EconomyChecks.Set(owner,"gold",0d);
                display=Object.FindAnyObjectByType<ArenaPresentation25D>();bridge=RiverBridge.All.Single(b=>b.Owner==owner);
                Check(MatchConfiguration.Valid(12)&&MatchConfiguration.Valid(36)&&!MatchConfiguration.Valid(4)&&!MatchConfiguration.Valid(8)&&!MatchConfiguration.Valid(37),"Map depth range is twelve through thirty-six metres");
                Check(RiverBridge.All.Count==2&&RiverBridge.All.Single(b=>b.Owner.transform.position.x<0).transform.position.y==7&&RiverBridge.All.Single(b=>b.Owner.transform.position.x>0).transform.position.y==-7,"Each owner starts with its own bridge at the specified river end");
                Check(RiverBridge.All.All(b=>b.Stage==0)&&!RiverBridge.Walkable(new Vector2(0,7),.5f),"Ghost bridges do not permit walking through water");
                var anchors=Object.FindObjectsByType<HookAnchor>();Check(anchors.Count(a=>a.transform.position.x<0)==4&&anchors.Count(a=>a.transform.position.x>0)==4,"Exactly four hook eyes exist on each bank");
                Check(anchors.All(a=>ArenaLayout.OuterEdge-Mathf.Abs(a.transform.position.x)>=8),"All hook eyes stay at least eight metres from mountain boundaries");
                foreach(var item in Object.FindObjectsByType<GameBuilding>())foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
                foreach(var item in Object.FindObjectsByType<PermanentRock>())foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
                foreach(var item in anchors)foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
                barracks=Spawn<Barracks>(BarracksSetup.Building,new Vector3(-12,-3),owner);
                Check(barracks.MaximumHP==800&&owner.GetComponent<OwnerEconomy>().Factory.MaximumHP==3000,"Barracks and factory have their approved durability");
                Check(!barracks.SetSending(true)&&!barracks.AutoProduction&&barracks.QueueCount==0,"Fresh barracks has no conveyor queue or sending before its bridge is complete");
                var stock=owner.GetComponent<OwnerEconomy>();stock.AddOutput(499,499);
                Check(!bridge.TryBuildNext(rival)&&bridge.Stage==0,"A rival cannot pay for another owner's bridge");
                Check(!bridge.TryBuildNext(owner)&&stock.BoneKg==499&&bridge.Stage==0,"Missing first-stage bones do not partially spend stock");
                stock.AddOutput(0,1);Check(bridge.TryBuildNext(owner)&&bridge.Stage==1&&stock.BoneKg==0&&stock.ProductKg==499,"First stage spends exactly five hundred bones once");
                owner.AddGold(9999);Check(!bridge.TryBuildNext(owner)&&owner.Gold==9999&&bridge.Stage==1,"Second stage rejects nine thousand nine hundred ninety-nine gold without spending");
                owner.AddGold(1);Check(bridge.TryBuildNext(owner)&&bridge.Stage==2&&owner.Gold==0,"Second stage spends exactly ten thousand gold");
                Check(!bridge.TryBuildNext(owner)&&stock.ProductKg==499&&!bridge.Complete,"Third stage requires all five hundred ready meat");
                due=Time.time+.2f;phase=2;return;
            }
            if(phase==2)
            {
                Capture(bridge,"024-bridge-stage2",2.8f,.3f);
                owner.GetComponent<OwnerEconomy>().AddOutput(1,0);Check(bridge.TryBuildNext(owner)&&bridge.Complete&&owner.GetComponent<OwnerEconomy>().ProductKg==0,"Third stage consumes five hundred meat and opens the crossing");
                Check(!bridge.TryBuildNext(owner)&&!RiverBridge.All.Single(b=>b!=bridge).Complete,"Repeated completion cannot charge again or open the other owner's bridge");
                Check(RiverBridge.Walkable(new Vector2(0,7),.5f)&&!RiverBridge.Walkable(Vector2.zero,.2f),"Completed bridge is public walkable terrain while the rest of the river remains blocked");
                Check(Near(RiverBridge.Height(new Vector2(0,7)),1.9*6/14)&&RiverBridge.Height(new Vector2(3,7))==0,"Presentation height follows the authored bridge arch and returns to shore level");
                var stock=owner.GetComponent<OwnerEconomy>();stock.AddOutput(1000000,1000000);
                var factory=stock.Factory;Check(double.IsPositiveInfinity(factory.ProductCapacityKg)&&(bool)EconomyChecks.Invoke(factory,"TryAcceptMass",1d),"A million ready meat does not block raw acceptance");
                factory.Advance(2);Check(stock.ProductKg>1000000&&stock.BoneKg>1000000,"Ready meat and bones can both grow past their old limits without loss");
                stock.TakeProduct(stock.ProductKg);stock.TrySpendBones((int)stock.Bones);
                Check(!barracks.TryQueue()&&barracks.QueueCount==0,"Queue rejects an unpaid order when resources are absent");
                owner.AddGold(20000);stock.AddOutput(12,6);double gold=owner.Gold;
                Check(barracks.TryQueue()&&barracks.TryQueue()&&barracks.TryQueue()&&barracks.QueueCount==3&&Near(gold-owner.Gold,150)&&Near(stock.ProductKg,0),"Three clicks immediately pay for three queued warriors without enabling a conveyor");
                for(int i=0;i<6;i++)Check(barracks.TryUpgrade(i),"Upgrade branch "+i+" is purchasable for later paid orders");
                barracks.Advance(12);soldier=Object.FindAnyObjectByType<SausageWarrior>();soldier.enabled=false;soldier.transform.position=new Vector3(-15,-7);
                Check(soldier.Stats.healthLevel==0&&barracks.QueueCount==2,"Already paid queue retains its original stats after upgrades");
                EconomyChecks.Set(barracks,"nextExitCheck",0f);
                barracks.Advance(12);var second=Object.FindObjectsByType<SausageWarrior>().Single(w=>w!=soldier);second.enabled=false;second.transform.position=new Vector3(-15,-6);
                EconomyChecks.Set(barracks,"nextExitCheck",0f);
                barracks.Advance(12);foreach(var unit in Object.FindObjectsByType<SausageWarrior>()){unit.enabled=false;unit.transform.position=new Vector3(-15,-5-Array.IndexOf(Object.FindObjectsByType<SausageWarrior>(),unit));}
                Check(barracks.QueueCount==0&&!barracks.AutoProduction&&Object.FindObjectsByType<SausageWarrior>().Length==3,"Paid manual queue is produced sequentially and stops when empty");
                stock.AddOutput(4,2);barracks.SetProduction(true);barracks.Advance(.1f);Check(barracks.QueueCount==1,"Explicit conveyor mode pays one next order when stock is available");
                barracks.SetProduction(false);EconomyChecks.Set(barracks,"nextExitCheck",0f);barracks.Advance(12);Check(Object.FindObjectsByType<SausageWarrior>().Length==4&&barracks.QueueCount==0,"Disabling conveyor preserves and finishes its already paid order");
                Check(!barracks.SetSending(true)&&Object.FindObjectsByType<SausageWarrior>().All(w=>!w.AutoRaiding),"Completed bridges do not enable automatic troop sending");
                owner.transform.position=new Vector3(-10,0);rival.transform.position=new Vector3(12,0);
                soldier.transform.position=new Vector3(-4,7);soldier.enabled=true;soldier.SetAutoRaid(false);
                var camera=Camera.main;var screen=(Vector2)camera.WorldToScreenPoint(ArenaSpace25D.ToWorld(soldier.transform.position));dragMin=screen-Vector2.one*24;dragMax=screen+Vector2.one*24;
                mouse=InputSystem.AddDevice<Mouse>("Siege validation mouse");MouseState(dragMin,1);phase=3;due=Time.time+.1f;return;
            }
            if(phase==3){MouseState(dragMax,1);phase=4;due=Time.time+.1f;return;}
            if(phase==4){MouseState(dragMax,0);phase=5;due=Time.time+.1f;return;}
            if(phase==5)
            {
                var commander=owner.GetComponent<UnitCommander>();Check(commander.SelectionCount==1&&soldier.IsSelected,"Native mouse dragging selects the intended friendly warrior");
                Check(owner.GetComponent<HookAbility>().ActiveHook==null&&!owner.GetComponent<HookAbility>().IsPreparingThrow,"Selection drag does not cast the hero hook");
                var point=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(new Vector3(4,7)));MouseState(point,2);started=Time.time;phase=6;due=Time.time+.1f;return;
            }
            if(phase==6)
            {
                MouseState(mouse.position.ReadValue(),0);Check(soldier.HasManualOrder&&owner.GetComponent<HeroActionTiming25D>().AxeSequence==0,"Native right-click issues a troop order without swinging the hero axe");
                Time.timeScale=3;sawArch=false;phase=7;due=0;return;
            }
            if(phase==7)
            {
                if(Mathf.Abs(soldier.transform.position.x)<1)
                {var view=display.VisualFor(soldier.GetComponent<Health>());sawArch|=view!=null&&view.position.y>.6f;}
                if(soldier.transform.position.x<3.2f&&Time.time-started<18)return;
                Check(soldier.transform.position.x>3.2f&&sawArch,"A commanded warrior follows a real route across the bridge with raised arch presentation");Time.timeScale=1;
                Capture(bridge,"024-bridge-complete",2.8f,.3f);
                enemyShop=Spawn<MeatShop>(EconomySetup.ShopPath,new Vector3(5.5f,7.5f),rival);enemyShop.SetOperating(rival,false);
                shop=Spawn<MeatShop>(EconomySetup.ShopPath,new Vector3(-7.5f,1.5f),owner);shop.SetOperating(owner,false);
                farm=Spawn<AnimalFarm>(EconomySetup.FarmPath,new Vector3(-10.5f,1.5f),owner);farm.SetOperating(owner,false);
                Check(enemyShop.MaximumHP==400&&farm.MaximumHP==400,"Shops and farms have four hundred durability");
                var fog=Object.FindAnyObjectByType<FogOfWar25D>();fog.RefreshObservers();Check(fog.IsRevealed(enemyShop.transform.position),"A friendly warrior reveals the approved four metre scouting circle after crossing");
                hp=enemyShop.CurrentHP;Check(!enemyShop.TakeDamage(rival,100)&&enemyShop.CurrentHP==hp,"Own attacks cannot damage own buildings");
                Check(!soldier.CommandAttack(shop)&&soldier.CommandAttack(enemyShop),"Troop attack orders reject allied buildings and accept enemy buildings");
                started=Time.time;phase=8;due=0;return;
            }
            if(phase==8)
            {
                if(enemyShop.CurrentHP==hp&&Time.time-started<6)return;
                Check(enemyShop.CurrentHP<hp,"Warrior attacks actually reduce the enemy building durability on animation frames");soldier.enabled=false;
                var stock=owner.GetComponent<OwnerEconomy>();stock.AddOutput(10,0);
                double meat=stock.ProductKg,gold=owner.Gold;Check(shop.SetOperating(owner,false)&&shop.Advance(10)==0&&stock.ProductKg==meat&&owner.Gold==gold,"Stopped shop neither consumes meat nor grants income");
                Check(farm.SetOperating(owner,false)&&farm.Advance(100)==null,"Stopped farm cannot spawn animals or accumulate a burst");
                Check(shop.SetOperating(owner,true)&&shop.Advance(1)>0&&owner.Gold>gold,"Restarting the selected shop resumes real sales");
                owner.TrySpendGold((int)Math.Floor(owner.Gold));Check(!shop.CanUpgrade,"Upgrade arrow is unavailable when the next level cannot be paid");
                owner.AddGold(shop.UpgradeCost);Check(shop.CanUpgrade&&shop.TryUpgrade(0)&&!shop.CanUpgrade,"Exact next-level gold enables one upgrade and then disables the more expensive next level");
                var menu=owner.GetComponent<BuildingUpgradeMenu>();var screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(shop.transform.position));
                Check(menu.SelectAtScreenPoint(screen)&&menu.Selected==shop&&menu.Contains(menu.ToolbarRect.center),"Selecting a shop exposes its compact world toolbar rather than the former side menu");
                var gui=menu.ToolbarRect.center;Check(GamePointer.OverInterface(new Vector2(gui.x,Screen.height-gui.y)),"World building icons block gameplay clicks");
                var camera=Camera.main.GetComponent<ArenaCamera25D>();float size=Camera.main.orthographicSize;camera.SetZoom(2);
                Check(Camera.main.orthographicSize<size&&camera.Zoom==2,"Camera zoom changes the actual orthographic framing");camera.SetZoom(1);
                var bodies=new System.Collections.Generic.List<SausageWarrior>();
                foreach(int level in new[]{0,1,3})
                {
                    var unit=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SausageWarrior>(BarracksSetup.Soldier),new Vector3(-12+level,0),Quaternion.identity);
                    unit.Bind(owner,new WarriorStats{healthLevel=level,damageLevel=level,areaLevel=level,poisonLevel=level,criticalLevel=level},false);unit.enabled=false;bodies.Add(unit);
                }
                phase=9;due=Time.time+.2f;return;
            }
            if(phase==9)
            {
                ScreenCapture.CaptureScreenshot("docs/validation/024-building-controls.png");
                var menu=owner.GetComponent<BuildingUpgradeMenu>();var view=display.VisualFor(shop);Check(view.Find("Selection")?.gameObject.activeSelf==true,"Selected building is highlighted on the ground");
                Capture(shop,"024-building",1.4f,.55f);
                foreach(int level in new[]{0,1,3})
                {
                    var unit=Object.FindObjectsByType<SausageWarrior>().Single(w=>w.Stats.healthLevel==level&&w.SourceBarracks==null);
                    var visual=display.VisualFor(unit.GetComponent<Health>());visual.rotation=Quaternion.Euler(0,180,0);
                    Check(Near(visual.localScale.x,(1+.15f*level)/1.5f),"Health level "+level+" has triple visible growth per purchase");
                    var club=visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name.Contains("Femur_Club"));var baked=new Mesh();club.BakeMesh(baked);
                    float lowest=baked.vertices.Min(v=>club.transform.TransformPoint(v).y);Object.DestroyImmediate(baked);
                    Check(lowest>=-.025f,"Upgraded club at level "+level+" remains visible above the ground");
                    Capture(unit.GetComponent<Health>(),"024-warrior-level"+level,1.6f,.65f);
                }
                Check(new WarriorStats{damageLevel=1}.ClubWidth>=1.24f&&new WarriorStats{areaLevel=1}.ClubLength>1.3f,"Damage and area improvements have three times their previous visual increments");
                rival.GetComponent<Health>().TakeDamage(10000,owner.GetComponent<Health>());
                var carcass=rival.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                Check(owner.GetComponent<CarcassCarrier>().TryCollect(carcass)&&owner.GetComponent<CarcassCarrier>().CurrentMassKg==10,"Real enemy kill and partial meat pickup reproduce the carried-cargo scenario");
                phase=10;started=Time.time;due=Time.time+.2f;return;
            }
            if(phase==10)
            {
                var view=display.VisualFor(owner.GetComponent<Health>());var cargo=view.Find("Cargo");
                if(cargo.GetComponentsInChildren<Renderer>().Length==0&&Time.time-started<2)return;
                Debug.Log("SIEGE_CHECK_SHADOW: cargo active="+cargo.gameObject.activeInHierarchy+" renderers="+cargo.GetComponentsInChildren<Renderer>().Length+" modes="+string.Join(",",cargo.GetComponentsInChildren<Renderer>().Select(r=>r.shadowCastingMode.ToString())));
                Check(cargo.GetComponentsInChildren<Renderer>().Length>0&&cargo.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"Actual partially collected hero meat cannot cast a long carried shadow");
                Capture(owner.GetComponent<Health>(),"024-carried-meat",1.6f,.7f);
                owner.transform.position=new Vector3(-3,7);owner.GetComponent<PlayerMovement>().enabled=true;
                keyboard=InputSystem.AddDevice<Keyboard>("Bridge movement keyboard");InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));
                started=Time.time;phase=13;due=0;return;
            }
            if(phase==13)
            {
                if(owner.transform.position.x<2.8f&&Time.time-started<4)return;
                Check(owner.transform.position.x>=2.8f,"Native WASD hero movement can walk across the completed bridge");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);keyboard=null;
                owner.GetComponent<PlayerMovement>().enabled=false;owner.transform.position=new Vector3(-ArenaLayout.OuterEdge+1,-9);
                returningHelper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ButcherHelper>(GameplayRevisionSetup.HelperPath),new Vector3(4,7),Quaternion.identity);
                returningHelper.Bind(owner.GetComponent<OwnerEconomy>().Factory,10);
                var meat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath),returningHelper.transform.position,Quaternion.identity);meat.Initialize(6);
                Check(returningHelper.GetComponent<CarcassCarrier>().TryCollect(meat),"A helper stranded across the river can carry a real meat portion");
                started=Time.time;Time.timeScale=3;helperCrossed=false;phase=14;return;
            }
            if(phase==14)
            {
                helperCrossed|=returningHelper.transform.position.x<-2.8f;
                if(returningHelper.GetComponent<CarcassCarrier>().CurrentMassKg>0&&Time.time-started<30)return;
                Debug.Log("SIEGE_CHECK_HELPER: p="+returningHelper.transform.position+" cargo="+returningHelper.GetComponent<CarcassCarrier>().CurrentMassKg+" factory="+returningHelper.Factory.transform.position+" raw="+returningHelper.Factory.RawKg+" free="+returningHelper.Factory.FreeRawKg+" crossed="+helperCrossed+" colliders="+string.Join(",",Physics2D.OverlapCircleAll(returningHelper.transform.position,returningHelper.Radius+.02f).Select(c=>c.name)));
                Check(helperCrossed&&returningHelper.GetComponent<CarcassCarrier>().CurrentMassKg==0,"Native helper AI returns over the bridge and delivers meat to its own factory");Time.timeScale=1;
                var factory=rival.GetComponent<OwnerEconomy>().Factory;
                Check(factory.MaximumHP==3000&&factory.CurrentHP==3000,"Enemy factory retains its full strongest durability before siege");
                Check(factory.TakeDamage(owner,3000)&&MatchPause.Instance.MatchEnded&&MatchPause.BlocksGameplay&&Time.timeScale==0,"Destroying the enemy factory ends and freezes the match exactly once");
                Check(!barracks.TryQueue()&&!bridge.TryBuildNext(owner),"Finished match blocks further production and bridge purchases");
                MatchPause.Instance.SetPaused(false);Check(Time.timeScale==0,"Pause controls cannot resume an already finished match");
                Check(MatchPause.Instance.ReturnToMenu(),"Finished match permits returning to the main menu");phase=11;due=0;return;
            }
            if(phase==11)
            {
                var menu=Object.FindAnyObjectByType<MainMenuController>();if(menu==null)return;
                Check(RiverBridge.All.Count==0&&Object.FindObjectsByType<SausageWarrior>().Length==0&&Time.timeScale==1,"Menu clears bridges troops and the finished-match freeze");
                menu.SetDepthText(depthCycle==0?"12":"36");Check(menu.StartBattle(),"A fresh match accepts the new depth "+(depthCycle==0?12:36));phase=12;return;
            }
            if(phase==12)
            {
                var player=Object.FindAnyObjectByType<PlayerMovement>();if(player==null||Object.FindAnyObjectByType<MainMenuController>()!=null)return;
                int depth=depthCycle==0?12:36;Check(ArenaLayout.Depth==depth&&RiverBridge.All.Count==2&&RiverBridge.All.All(b=>b.Stage==0),"Fresh depth "+depth+" regenerates two unfinished bridges without old stages");
                var anchors=Object.FindObjectsByType<HookAnchor>();Check(anchors.Length==8&&anchors.All(a=>ArenaLayout.OuterEdge-Mathf.Abs(a.transform.position.x)>=8),"Both range extremes retain four safe hook eyes per bank");
                Check(!MatchPause.Instance.MatchEnded&&Object.FindObjectsByType<SausageWarrior>().Length==0,"New match has no old result troops or paid queues");
                if(depthCycle++==0){MatchPause.Instance.ReturnToMenu();phase=11;return;}
                string summary="HOOKWARS_SIEGE_PASS: "+count;Debug.Log(summary);Finish(true,summary);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(false,"FAIL "+error);}
    }
}
