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
public static class WorkersChecks
{
    public const string Key="HookWars.WorkersChecks";
    static int phase,count;
    static double ready,deadline;
    static float due,started,hp;
    static string report;
    static PlayerProgression owner,rival;
    static SausageFactory factory;
    static AnimalFarm farm;
    static MeatShop shop;
    static SausageWarrior soldier;
    static ButcherHelper helper;
    static Animal animal;
    static Carcass trapped;
    static GameObject blocker;
    static ArenaPresentation25D display;
    static UnitCommander commander;
    static Mouse mouse;
    static Vector3 before;
    static float originalTilt;
    static WorkersChecks(){if(SessionState.GetBool(Key,false))Subscribe();}
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Passed",false);Subscribe();
    }
    static void Subscribe(){phase=count=0;due=0;report="";ready=EditorApplication.timeSinceStartup+3;deadline=ready+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);count++;report+="PASS "+text+"\n";Debug.Log("WORKERS_CHECK: "+text);}
    static bool Near(double a,double b)=>Math.Abs(a-b)<.001;
    static T Spawn<T>(string path,Vector3 at) where T:Component=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>(path),at,Quaternion.identity);
    static Carcass Meat(Vector3 at,float mass){var c=Spawn<Carcass>(CarryingSetup.PrefabPath,at);c.Initialize(mass);return c;}
    static void Next(int value,float wait){phase=value;due=Time.time+wait;}
    static void MouseState(Vector2 position,ushort buttons,Vector2 scroll=default)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=buttons,scroll=scroll});
    static void Finish(bool passed,string text)
    {
        if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);mouse=null;Time.timeScale=1;
        File.WriteAllText("docs/validation/025-workers.txt",report+text+"\n");SessionState.SetBool(Key+".Passed",passed);phase=99;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Workers checks phase "+phase);
            if(phase==0){EditorApplication.isPlaying=true;phase=1;return;}
            if(!EditorApplication.isPlaying||Time.time<due)return;
            if(phase==1)
            {
                Application.runInBackground=true;Directory.CreateDirectory("Temp/WorkersPreview");
                foreach(var a in Object.FindObjectsByType<EnemyHookAI>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyChase>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<EnemyMelee>())a.enabled=false;
                foreach(var a in Object.FindObjectsByType<Animal>())a.gameObject.SetActive(false);
                foreach(var a in Object.FindObjectsByType<CarcassCarrier>())a.enabled=false;
                var player=Object.FindAnyObjectByType<PlayerMovement>();player.enabled=false;player.GetComponentInChildren<PlayerAim>().enabled=false;
                owner=player.GetComponent<PlayerProgression>();owner.enabled=false;rival=Object.FindAnyObjectByType<EnemyChase>().GetComponent<PlayerProgression>();rival.enabled=false;
                owner.transform.position=new Vector3(-16,8);rival.transform.position=new Vector3(16,8);owner.AddGold(100000000);
                foreach(var c in Object.FindObjectsByType<Collider2D>())if(c.GetComponent<GameBuilding>()!=null||c.GetComponent<PermanentRock>()!=null||c.GetComponent<HookAnchor>()!=null)c.enabled=false;
                factory=owner.GetComponent<OwnerEconomy>().Factory;factory.enabled=false;factory.transform.position=new Vector3(-15,-4);
                display=Object.FindAnyObjectByType<ArenaPresentation25D>();commander=owner.GetComponent<UnitCommander>();
                farm=Spawn<AnimalFarm>(EconomySetup.FarmPath,new Vector3(-7,5));farm.Initialize(owner);farm.enabled=false;
                var born=farm.TrySpawn();Check(born!=null,"Base farm really spawns an animal");born.enabled=false;float mass=born.MassKg;
                Check(farm.TryUpgrade(0)&&farm.Level==2&&Near(farm.SpawnInterval,12)&&Near(farm.MassMultiplier,1.2),"One farm purchase improves spawn interval and weight together");
                Check(Near(born.MassKg,mass),"Previously born animals retain their weight");Object.Destroy(born.gameObject);
                for(int i=0;i<30;i++)
                {
                    var next=farm.TrySpawn();Check(next!=null&&Near(next.MassKg,(next.Kind==Animal.Species.Sheep?4:next.Kind==Animal.Species.Cow?10:6)*1.2),"Upgraded spawn receives species base mass times 1.2");Object.Destroy(next.gameObject);
                }
                for(int i=0;i<10;i++)Check(farm.TryUpgrade(0),"Further farm level is purchasable");
                Check(farm.SpawnInterval==3&&farm.CanUpgrade&&farm.TryUpgrade(0)&&farm.MassMultiplier>3,"Weight upgrades continue after minimum spawn interval");
                Check(farm.Info.Contains("3")&&farm.Info.Contains(farm.Level.ToString()),"Farm exposes current level interval and weight");
                shop=Spawn<MeatShop>(EconomySetup.ShopPath,new Vector3(-7,1));shop.Initialize(owner);shop.enabled=false;
                Check(shop.TryUpgrade(0)&&Near(shop.SaleKgPerSecond,.2)&&shop.GoldPerKg==25&&Near(shop.GoldPerSecond,5),"One shop purchase improves speed and price together");
                var stock=owner.GetComponent<OwnerEconomy>();stock.AddOutput(5,0);double gold=owner.Gold;shop.Advance(1);
                Check(Near(owner.Gold-gold,5)&&Near(stock.ProductKg,4.8),"Level two shop really sells .2kg for five gold");
                Check(shop.Info.Contains("25")&&shop.Info.Contains("2"),"Shop exposes current level price and sale speed");
                soldier=Spawn<SausageWarrior>(BarracksSetup.Soldier,new Vector3(-6,0));soldier.Bind(owner,default,false);
                helper=Spawn<ButcherHelper>(GameplayRevisionSetup.HelperPath,new Vector3(-12,-2));helper.Bind(factory,10);helper.enabled=false;helper.GetComponent<CarcassCarrier>().enabled=false;
                animal=Spawn<Animal>("Assets/Prefabs/cow.prefab",new Vector3(-9,-5));animal.enabled=false;
                Check(helper.MoveSpeed==4.5f&&Near(helper.GetComponent<CircleCollider2D>().radius,.25/1.5),"Helper moves fifty percent faster and has reduced collider");
                Check(Near(animal.Radius,.35/1.5)&&Near(animal.GetComponent<CircleCollider2D>().radius,animal.Radius),"Animal physical radius is reduced by one and a half");
                Check(Near(soldier.GetComponent<CircleCollider2D>().radius,.2/1.5),"Warrior physical radius is reduced by one and a half");
                before=soldier.transform.position;Next(2,1);return;
            }
            if(phase==2)
            {
                Check(Vector3.Distance(soldier.transform.position,before)<.001f,"Idle warrior stands still without random wandering");
                Check(Near(display.VisualFor(owner.GetComponent<Health>()).localScale.x,1),"Main hero keeps its visual scale");
                Check(Near(display.VisualFor(soldier.GetComponent<Health>()).localScale.x,1/1.5)&&Near(display.VisualFor(helper.GetComponent<Health>()).localScale.x,.5/1.5)&&Near(display.VisualFor(animal).localScale.x,1/1.5),"All three nonhero model kinds shrink by one and a half");
                var screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(soldier.transform.position));commander.SelectUnits(new Rect(screen.x-20,screen.y-20,40,40));
                Check(commander.SelectionCount==1&&commander.GiveOrder(new Vector3(-9,0)),"Friendly warrior accepts movement order");
                Check(commander.HasOrderMarker&&!commander.LastOrderIsAttack&&commander.LastOrderPosition==new Vector3(-9,0),"Accepted movement shows green destination marker");
                var green=GameObject.Find("Order destination").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                Check(green.g>green.r*2&&green.a>.5f,"Movement marker material is visibly green");
                rival.transform.position=new Vector3(-4.5f,0);hp=rival.GetComponent<Health>().CurrentKg;Next(3,3.1f);return;
            }
            if(phase==3)
            {
                Check(soldier.transform.position.x<-8.5f&&Near(rival.GetComponent<Health>().CurrentKg,hp),"Manual movement takes priority over a nearby enemy: x="+soldier.transform.position.x+", hp="+rival.GetComponent<Health>().CurrentKg+"/"+hp);
                Check(!commander.HasOrderMarker,"Destination flash disappears after its short lifetime");
                rival.transform.position=new Vector3(-8,0);Next(4,1.7f);return;
            }
            if(phase==4)
            {
                Check(rival.GetComponent<Health>().CurrentKg<hp,"Idle warrior pursues and strikes a nearby same-bank opponent");
                Check(commander.GiveOrder(rival.transform.position,rival.GetComponent<Health>())&&commander.HasOrderMarker&&commander.LastOrderIsAttack,"Accepted enemy attack shows red destination marker");
                var red=GameObject.Find("Order destination").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                Check(red.r>red.g*2&&red.a>.5f,"Attack marker material is visibly red");
                ScreenCapture.CaptureScreenshot("Temp/WorkersPreview/attack.png");
                soldier.enabled=false;rival.transform.position=new Vector3(16,8);Object.Destroy(animal.gameObject);
                animal=Spawn<Animal>("Assets/Prefabs/cow.prefab",helper.transform.position+Vector3.right*.7f);animal.ConfigureSpawnMass(100);animal.enabled=false;
                helper.enabled=true;started=Time.time;Next(5,0);return;
            }
            if(phase==5)
            {
                if(!helper.GetComponent<HeroActionTiming25D>().AxePending&&Time.time-started<2)return;
                Check(helper.GetComponent<HeroActionTiming25D>().AxePending,"Helper begins one committed swing against a heavy animal");
                animal.transform.position+=Vector3.right;Next(6,.5f);return;
            }
            if(phase==6)
            {
                Check(animal==null||animal.IsDead,"Animal movement during windup cannot evade the guaranteed killing blow");
                Check(helper.Returning&&Near(helper.GetComponent<CarcassCarrier>().CurrentMassKg,10),"Heavy animal is killed once and helper collects its capacity");
                helper.enabled=false;helper.GetComponent<CarcassCarrier>().DropAll();foreach(var c in Object.FindObjectsByType<Carcass>())Object.Destroy(c.gameObject);
                helper.transform.position=new Vector3(-12,-2);
                animal=Spawn<Animal>("Assets/Prefabs/sheep.prefab",helper.transform.position+Vector3.right*.7f);animal.enabled=false;helper.enabled=true;Next(7,.15f);return;
            }
            if(phase==7)
            {
                helper.GetComponent<HookVictim>().TryCapture(owner.GetComponent<HookAbility>());Next(8,.6f);return;
            }
            if(phase==8)
            {
                Check(animal!=null&&!animal.IsDead&&!helper.GetComponent<HeroActionTiming25D>().AxePending,"Capture cancels helper swing without killing the animal");
                helper.GetComponent<HookVictim>().Release(owner.GetComponent<HookAbility>());helper.enabled=false;Object.Destroy(animal.gameObject);
                helper.transform.position=new Vector3(-12,-2);
                trapped=Meat(new Vector3(-11,1),4);blocker=new GameObject("Unreachable corpse enclosure");blocker.layer=LayerMask.NameToLayer("Buildings");blocker.transform.position=trapped.transform.position;blocker.AddComponent<BoxCollider2D>().size=Vector2.one*2;
                Meat(new Vector3(-9,-2),3);helper.enabled=true;started=Time.time;Next(9,0);return;
            }
            if(phase==9)
            {
                if(factory.RawKg<3&&Time.time-started<8)return;
                Check(Near(factory.RawKg,3)&&trapped.IsAvailable,"Helper skips inaccessible corpse and delivers reachable meat instead");
                Object.Destroy(blocker);started=Time.time;Next(10,0);return;
            }
            if(phase==10)
            {
                if(factory.RawKg<7&&Time.time-started<10)return;
                Check(Near(factory.RawKg,7),"Temporarily unreachable meat is retried and delivered after obstacle removal");
                EconomyChecks.Invoke(factory,"TryAcceptMass",5d);helper.enabled=false;helper.transform.position=factory.transform.position+Vector3.right*1.5f;
                Check(helper.GetComponent<CarcassCarrier>().TryCollect(Meat(helper.transform.position,2)),"Delivery waiting fixture retains real cargo");helper.enabled=true;Next(11,2.5f);return;
            }
            if(phase==11)
            {
                Check(Near(factory.RawKg,12)&&Near(helper.GetComponent<CarcassCarrier>().CurrentMassKg,2),"Full raw storage makes helper wait without losing cargo");
                factory.Advance(4);Next(12,.3f);return;
            }
            if(phase==12)
            {
                Check(Near(factory.RawKg,12)&&!helper.Returning,"Helper resumes delivery when raw storage has room");helper.enabled=false;
                shop.enabled=true;farm.SetOperating(owner,false);farm.enabled=true;
                var screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(shop.transform.position));
                var menu=owner.GetComponent<BuildingUpgradeMenu>();Check(menu.SelectAtScreenPoint(screen)&&menu.Contains(menu.StatsRect.center),"Selected shop includes its current statistics in the clickable interface area");
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();Directory.CreateDirectory("Temp/WorkersPreview");ScreenCapture.CaptureScreenshot("Temp/WorkersPreview/shop.png");
                mouse=InputSystem.AddDevice<Mouse>("Workers validation mouse");originalTilt=Camera.main.GetComponent<ArenaCamera25D>().Tilt;
                MouseState(new Vector2(Screen.width*.5f,Screen.height*.5f),0,new Vector2(0,120));Next(13,.25f);return;
            }
            if(phase==13)
            {
                var camera=Camera.main.GetComponent<ArenaCamera25D>();Check(camera.Zoom>1.37f&&camera.Zoom<=1.4f,"Native wheel zoom quickly approaches forty percent per notch");
                MouseState(new Vector2(Screen.width*.5f,Screen.height*.5f),4);Next(14,.1f);return;
            }
            if(phase==14){MouseState(new Vector2(Screen.width*.5f+100,Screen.height*.5f+40),4);Next(15,.1f);return;}
            if(phase==15)
            {
                var camera=Camera.main.GetComponent<ArenaCamera25D>();Check(Near(camera.Yaw,30)&&Near(camera.Tilt,originalTilt+10),"Holding middle mouse rotates and tilts camera through real input");
                MouseState(mouse.position.ReadValue(),0);
                Vector3 point=new Vector3(-5,2);var projected=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(point));
                Check(GamePointer.TryLogicalPoint(Camera.main,projected,out var logical)&&Vector3.Distance(point,logical)<.01f,"Mouse ground projection remains accurate after zoom tilt and rotation");
                camera.Tilt=100;Check(camera.Tilt==80,"Camera tilt has upper limit");camera.Tilt=0;Check(camera.Tilt==25,"Camera tilt has lower limit");
                ScreenCapture.CaptureScreenshot("Temp/WorkersPreview/orbit.png");Next(16,.2f);return;
            }
            if(phase==16)
            {
                foreach(var file in new[]{"shop","orbit"})if(File.Exists("Temp/WorkersPreview/"+file+".png"))File.Copy("Temp/WorkersPreview/"+file+".png","docs/validation/025-"+file+".png",true);
                Camera.main.GetComponent<ArenaCamera25D>().SetZoom(1);
                soldier.transform.position=new Vector3(-2.3f,0);rival.transform.position=new Vector3(2.3f,0);
                soldier.Bind(owner,new WarriorStats{areaLevel=80},false);soldier.enabled=true;hp=rival.GetComponent<Health>().CurrentKg;
                Check(soldier.CommandAttack(rival.GetComponent<Health>()),"Explicit attack may target the opposite bank");Next(17,1.6f);return;
            }
            if(phase==17)
            {
                Check(Near(rival.GetComponent<Health>().CurrentKg,hp)&&soldier.transform.position.x<0,"Even upgraded long-range warrior cannot strike across the river");
                var bridge=RiverBridge.All.Single(b=>b.Owner==owner);owner.GetComponent<OwnerEconomy>().AddOutput(500,500);
                Check(bridge.TryBuildNext(owner)&&bridge.TryBuildNext(owner)&&bridge.TryBuildNext(owner),"An explicitly paid bridge opens the route for the cross-bank attack");
                soldier.CommandAttack(rival.GetComponent<Health>());started=Time.time;Next(21,0);return;
            }
            if(phase==21)
            {
                if(rival.GetComponent<Health>().CurrentKg>=hp&&Time.time-started<22)return;
                Check(soldier.transform.position.x>0&&rival.GetComponent<Health>().CurrentKg<hp,"Long-range attack order crosses a completed bridge before damaging the other bank");
                soldier.enabled=false;rival.transform.position=new Vector3(16,8);helper.transform.position=new Vector3(-12,-2);
                trapped=Meat(new Vector3(-8,-2),2);helper.enabled=true;Next(18,.15f);return;
            }
            if(phase==18)
            {
                blocker=new GameObject("New obstacle around an already acquired target");blocker.layer=LayerMask.NameToLayer("Buildings");blocker.transform.position=trapped.transform.position;blocker.AddComponent<BoxCollider2D>().size=Vector2.one*2;
                Meat(new Vector3(-11,-5),1);started=Time.time;Next(19,0);return;
            }
            if(phase==19)
            {
                if(!helper.Returning&&Time.time-started<7)return;
                Check(helper.Returning&&Near(helper.GetComponent<CarcassCarrier>().CurrentMassKg,1)&&trapped.IsAvailable,"A target blocked after acquisition is released and another job is collected");helper.enabled=false;

                MatchPause.Instance.SetPaused(true);MouseState(new Vector2(Screen.width*.5f,Screen.height*.5f),4,new Vector2(0,120));phase=20;ready=EditorApplication.timeSinceStartup+.2;return;
            }
            if(phase==20)
            {
                var camera=Camera.main.GetComponent<ArenaCamera25D>();Check(Near(camera.Zoom,1)&&Near(camera.Yaw,30),"Pause blocks camera zoom and rotation input");
                MatchPause.Instance.SetPaused(false);MouseState(mouse.position.ReadValue(),0);
                if(File.Exists("Temp/WorkersPreview/attack.png"))File.Copy("Temp/WorkersPreview/attack.png","docs/validation/025-attack.png",true);
                string summary="HOOKWARS_WORKERS_PASS: "+count+" assertions in Unity Play Mode";Debug.Log(summary);Finish(true,summary);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(false,"FAIL "+error);}
    }
}
