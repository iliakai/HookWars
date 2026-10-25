using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in checks in the actual development player. Ordinary games never install this.
public sealed class WindowsSmoke25D : MonoBehaviour
{
#if !UNITY_EDITOR
    static string output;
    int assertions,savedDepth;
    bool failed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!Debug.isDebugBuild)return;
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-hookwars-smoke");
        if(index<0)return;if(index+1>=args.Length)throw new ArgumentException("Smoke output directory required");
        output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
        var go=new GameObject("Explicit Windows acceptance runner");DontDestroyOnLoad(go);go.AddComponent<WindowsSmoke25D>();
    }
    void Check(bool value,string message)
    {
        if(!value){failed=true;File.AppendAllText(Path.Combine(output,"result.txt"),"FAIL: "+message+"\n");Application.Quit(1);throw new InvalidOperationException(message);}
        assertions++;File.AppendAllText(Path.Combine(output,"result.txt"),"PASS: "+message+"\n");
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;savedDepth=MatchConfiguration.LastDepth;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Development Windows player acceptance, helper harvest delivery027\n");
        foreach(int depth in new[]{12,24,36})
        {
            yield return new WaitForSecondsRealtime(.3f);
            var menu=FindAnyObjectByType<MainMenuController>();Check(menu!=null,"Main menu starts without a match");
            menu.SetDepthText("8");Check(!menu.StartBattle(),"Removed eight metre depth is rejected");menu.SetDepthText(depth.ToString());Check(menu.StartBattle(),"New depth starts: "+depth);
            yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="Arena25D");yield return null;yield return null;
            var hero=FindAnyObjectByType<PlayerMovement>();var owner=hero.GetComponent<PlayerProgression>();var stock=hero.GetComponent<OwnerEconomy>();
            Check(ArenaLayout.Depth==depth&&owner.GoldPerSecond==.5f&&owner.Experience==0&&owner.Level==1,"Fresh map preserves normal hero progression and half-gold income");
            Check(RiverBridge.All.Count==2&&RiverBridge.All.All(b=>b.Stage==0),"New match creates two unfinished bridges");
            var anchors=FindObjectsByType<HookAnchor>();Check(anchors.Length==8&&anchors.All(a=>ArenaLayout.OuterEdge-Mathf.Abs(a.transform.position.x)>=8),"Four hook eyes on each bank keep eight metres clear of mountains");
            Check(FindObjectsByType<Barracks>().Length==0&&FindObjectsByType<SausageWarrior>().Length==0&&!MatchPause.Instance.MatchEnded,"Fresh match has no old troops orders or match result");
            Check(stock.Factory.MaximumHP==3000&&double.IsPositiveInfinity(stock.Factory.ProductCapacityKg),"Factory is strongest and finished storage is unlimited");
            Check(!stock.Factory.TakeDamage(owner,100)&&stock.Factory.CurrentHP==3000,"Friendly damage cannot hurt its own factory");
            var bridge=RiverBridge.All.Single(b=>b.Owner==owner);Check(!bridge.TryBuildNext(owner)&&bridge.Stage==0,"Unpaid bridge stage cannot complete");
            Check(!RiverBridge.Walkable(new Vector2(0,7),.5f),"Unfinished bridge does not open a walking route");
            var display=FindAnyObjectByType<ArenaPresentation25D>();var visual=display.VisualFor(bridge);
            Check(visual!=null&&visual.GetComponentsInChildren<Collider>().Length==0,"Bridge model loads without duplicate three dimensional physics");
            Check(visual.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),"Bridge materials survive player shader stripping");
            var camera=Camera.main.GetComponent<ArenaCamera25D>();float size=Camera.main.orthographicSize;camera.SetZoom(2);
            Check(camera.Zoom==2&&Camera.main.orthographicSize<size,"Windows map can zoom in");camera.SetZoom(1);
            foreach(var ai in FindObjectsByType<EnemyHookAI>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyChase>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyMelee>())ai.enabled=false;
            foreach(var carrier in FindObjectsByType<CarcassCarrier>())carrier.enabled=false;
            hero.enabled=false;owner.enabled=false;
            var rival=FindAnyObjectByType<EnemyChase>().GetComponent<PlayerProgression>();rival.enabled=false;
            if(depth==12)yield return ExerciseHarvest(hero,owner,stock);
            if(depth==24)yield return ExerciseSiege(hero,owner,rival,bridge,display);
            else
            {
                stock.AddOutput(1000000,1000000);
                Check(stock.Factory.TryAcceptMass(1),"Large ready inventory permits raw acceptance");
                stock.Factory.Advance(2);Check(stock.ProductKg>1000000&&stock.BoneKg>1000000,"Both ready meat and bones can exceed old capacity");
                MatchPause.Instance.SetPaused(true);Check(Time.timeScale==0,"Pause freezes the ordinary match");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"siege_"+depth+".png"));yield return new WaitForEndOfFrame();
            Check(MatchPause.Instance.ReturnToMenu(),"Return to menu is accepted");
            yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="MainMenu");yield return null;
            Check(Time.timeScale==1&&RiverBridge.All.Count==0&&FindObjectsByType<SausageWarrior>().Length==0&&FindObjectsByType<Barracks>().Length==0,"Menu clears bridges troops queues and pause/result state");
        }
        MatchConfiguration.RememberDepth(savedDepth);
        File.AppendAllText(Path.Combine(output,"result.txt"),"HOOKWARS_WINDOWS_SMOKE_PASS: "+assertions+" assertions\n");Application.Quit(0);
    }
    IEnumerator ExerciseHarvest(PlayerMovement hero,PlayerProgression owner,OwnerEconomy stock)
    {
        var factory=stock.Factory;owner.AddGold(1000);
        Check(factory.GetComponent<BoxCollider2D>().enabled&&!factory.GetComponent<BoxCollider2D>().isTrigger,"Windows harvesting keeps the real factory solid");
        Check(factory.TryOrderHelper(),"Windows normal factory order spawns a helper for a complete harvesting cycle");
        var worker=FindObjectsByType<ButcherHelper>().Single();
        Vector3 FreePoint(float distance)
        {
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI/16;var p=factory.transform.position+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*distance;
                if(ArenaLayout.IsInsideBank(p,p.x>0,.4f)&&WorldCollision.FreeCircle(p,.4f))return p;
            }
            throw new InvalidOperationException("No free Windows harvest fixture point");
        }
        var farmPrefab=hero.GetComponent<PlayerBuilding>().BuildingPrefabs.OfType<AnimalFarm>().Single();
        var fixture=Instantiate(farmPrefab,FreePoint(7),Quaternion.identity);fixture.Initialize(owner);fixture.enabled=false;
        var prey=fixture.TrySpawn();Check(prey!=null,"Windows harvest fixture creates an actual moving animal");
        prey.transform.position=FreePoint(4.5f);float mass=prey.MassKg;Destroy(fixture.gameObject);
        Carcass loose=null;bool sawCargo=false;float until=Time.time+18;
        double Accepted()=>factory.RawKg+stock.ProductKg*2;
        while((Accepted()<mass-.001||worker.Returning||prey!=null&&!prey.IsDead)&&Time.time<until)
        {
            if(worker.Returning)
            {
                sawCargo=true;
                if(loose==null){loose=Instantiate(worker.GetComponent<CarcassCarrier>().StoredCargo[0],FreePoint(5),Quaternion.identity);loose.Initialize(6);}
            }
            yield return null;
        }
        Check((prey==null||prey.IsDead)&&sawCargo&&Accepted()>=mass-.001&&!worker.Returning,"Windows worker kills collects and delivers animal meat with factory collision enabled");
        var rest=worker.transform.position;int resets=worker.IdleRecoveries;yield return new WaitForSeconds(2.2f);
        Check(Vector3.Distance(rest,worker.transform.position)<.02f&&worker.IdleRecoveries==resets,"Windows completed delivery has no periodic sideways movement");
        Check(loose!=null,"Windows first collection provides a loose meat fixture");loose.gameObject.SetActive(true);until=Time.time+18;sawCargo=false;
        while((Accepted()<mass+6-.001||worker.Returning)&&Time.time<until){sawCargo|=worker.Returning;yield return null;}
        Check(sawCargo&&Accepted()>=mass+6-.001&&!worker.Returning,"Windows worker completes a second loose-meat delivery to its solid factory");
        Check(factory.GetComponent<BoxCollider2D>().enabled,"Windows full harvesting test never disabled factory collision");Destroy(worker.gameObject);yield return null;
    }
    IEnumerator ExerciseSiege(PlayerMovement hero,PlayerProgression owner,PlayerProgression rival,RiverBridge bridge,ArenaPresentation25D display)
    {
        foreach(var item in FindObjectsByType<GameBuilding>())foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
        foreach(var item in FindObjectsByType<PermanentRock>())foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
        foreach(var item in FindObjectsByType<HookAnchor>())foreach(var c in item.GetComponents<Collider2D>())c.enabled=false;
        var stock=owner.GetComponent<OwnerEconomy>();owner.AddGold(30000);stock.AddOutput(500,500);
        Check(bridge.TryBuildNext(owner)&&bridge.Stage==1&&stock.BoneKg==0,"Bridge supports consume five hundred bones");
        double gold=owner.Gold;Check(bridge.TryBuildNext(owner)&&bridge.Stage==2&&gold-owner.Gold==10000,"Bridge bun consumes ten thousand gold");
        Check(bridge.TryBuildNext(owner)&&bridge.Complete&&stock.ProductKg==0,"Bridge sausage consumes five hundred ready meat");
        Check(!bridge.TryBuildNext(owner)&&RiverBridge.Walkable(new Vector2(0,7),.5f)&&!RiverBridge.Walkable(Vector2.zero,.2f),"Finished bridge opens only its own crossing and cannot charge a fourth stage");
        var builder=hero.GetComponent<PlayerBuilding>();var barracksPrefab=builder.BuildingPrefabs.OfType<Barracks>().Single();
        hero.transform.position=new Vector3(-12.5f,-3);rival.transform.position=new Vector3(12,0);
        builder.SelectBuilding(barracksPrefab);var barracks=(Barracks)builder.TryBuildAt(new Vector3(-10,-3));builder.CloseInterface();
        Check(barracks!=null&&barracks.MaximumHP==800&&!barracks.AutoProduction,"Windows constructs a durable barracks with conveyor off");
        stock.AddOutput(8,4);gold=owner.Gold;
        Check(barracks.TryQueue()&&barracks.TryQueue()&&barracks.QueueCount==2&&gold-owner.Gold==100&&stock.ProductKg==0&&stock.BoneKg==0,"Two left-click orders immediately reserve and spend their complete cost");
        for(int i=0;i<6;i++){gold=owner.Gold;Check(barracks.TryUpgrade(i)&&gold-owner.Gold==1000,"Upgrade branch "+i+" costs one thousand");}
        yield return new WaitForSeconds(12.2f);
        Check(FindObjectsByType<SausageWarrior>().Length==1&&barracks.QueueCount==1,"First paid warrior is produced after twelve native game seconds");
        var first=FindAnyObjectByType<SausageWarrior>();Check(first.Stats.healthLevel==0&&first.GetComponent<Health>().MaximumKg==40,"Paid queue snapshot precedes later upgrades");
        first.enabled=false;first.transform.position=new Vector3(-16,-6);
        yield return new WaitForSeconds(12.2f);
        var units=FindObjectsByType<SausageWarrior>();Check(units.Length==2&&barracks.QueueCount==0&&!barracks.AutoProduction,"Second order completes sequentially and manual production stops");
        foreach(var unit in units){unit.enabled=false;unit.transform.position=new Vector3(-16,-6-Array.IndexOf(units,unit));}
        Check(!barracks.SetSending(true)&&units.All(w=>!w.AutoRaiding),"Restored bridge keeps warriors under individual orders");barracks.SetSending(false);
        first.transform.position=new Vector3(-4,7);first.enabled=true;
        var commander=hero.GetComponent<UnitCommander>();var screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(first.transform.position));
        commander.SelectUnits(new Rect(screen.x-24,screen.y-24,48,48));Check(commander.SelectionCount==1&&first.IsSelected,"Windows selection picks only the intended friendly warrior");
        Check(commander.GiveOrder(new Vector3(4,7))&&first.HasManualOrder&&commander.HasOrderMarker&&!commander.LastOrderIsAttack,"Windows group movement produces a green destination marker");
        Time.timeScale=3;float until=Time.time+18;bool arch=false;
        while(first.transform.position.x<3.2f&&Time.time<until)
        {
            var view=display.VisualFor(first.GetComponent<Health>());if(Mathf.Abs(first.transform.position.x)<1)arch|=view!=null&&view.position.y>.6f;yield return null;
        }
        Check(first.transform.position.x>3.2f&&arch,"Windows warrior traverses the completed public bridge along its raised arch");Time.timeScale=1;
        var shopPrefab=builder.BuildingPrefabs.OfType<MeatShop>().Single();var enemyShop=Instantiate(shopPrefab,new Vector3(5.5f,7.5f),Quaternion.identity);enemyShop.Initialize(rival);enemyShop.SetOperating(rival,false);
        Check(enemyShop.MaximumHP==400&&!enemyShop.TakeDamage(rival,100),"Enemy shop has durability and rejects its own owner's damage");
        float before=enemyShop.CurrentHP;Check(commander.GiveOrder(enemyShop.transform.position,enemyShop)&&commander.HasOrderMarker&&commander.LastOrderIsAttack,"Windows attack command shows a red destination marker");
        until=Time.time+5;while(enemyShop.CurrentHP==before&&Time.time<until)yield return null;
        Check(enemyShop.CurrentHP<before,"Warrior animated attack damages a hostile building");first.enabled=false;
        stock.AddOutput(4,2);Check(barracks.TryQueue(),"A new paid order can be placed after upgrades");yield return new WaitForSeconds(12.2f);
        var upgraded=FindObjectsByType<SausageWarrior>().Single(w=>!units.Contains(w));upgraded.enabled=false;
        Check(upgraded.GetComponent<Health>().MaximumKg==50&&upgraded.Stats.poisonLevel==1,"New paid warrior inherits upgraded combat characteristics");
        var upgradedView=display.VisualFor(upgraded.GetComponent<Health>());
        Check(upgradedView!=null&&Math.Abs(upgradedView.localScale.x-1.15f/1.5f)<.001&&upgraded.Stats.ClubWidth>=1.24f&&upgraded.Stats.ClubLength>1.3f,"Body and club upgrades have visibly stronger model scaling");
        var club=upgradedView.GetComponentsInChildren<Renderer>().Single(r=>r.name.Contains("Femur_Club"));var tint=new MaterialPropertyBlock();club.GetPropertyBlock(tint,0);
        Check(tint.GetColor("_BaseColor")==upgraded.Stats.ClubColor,"Acid club tint survives the Windows player build");
        var ownShop=Instantiate(shopPrefab,new Vector3(-8.5f,1.5f),Quaternion.identity);ownShop.Initialize(owner);stock.AddOutput(10,0);
        ownShop.SetOperating(owner,false);double meat=stock.ProductKg;gold=owner.Gold;yield return new WaitForSeconds(.2f);
        Check(stock.ProductKg==meat&&owner.Gold==gold,"Stopped shop consumes no meat and grants no income");
        ownShop.SetOperating(owner,true);yield return new WaitForSeconds(1.1f);Check(stock.ProductKg<meat&&owner.Gold>gold,"Restarted shop resumes actual native-frame sales");ownShop.enabled=false;
        Check(ownShop.TryUpgrade(0)&&ownShop.Level==2&&Math.Abs(ownShop.SaleKgPerSecond-.2f)<.001&&ownShop.GoldPerKg==25,"Windows shop upgrade improves price and throughput together");
        gold=owner.Gold;ownShop.Advance(1);Check(Math.Abs(owner.Gold-gold-5)<.001,"Windows upgraded shop sells for five gold per second");
        var farmPrefab=builder.BuildingPrefabs.OfType<AnimalFarm>().Single();var farm=Instantiate(farmPrefab,new Vector3(-11,2),Quaternion.identity);farm.Initialize(owner);farm.enabled=false;
        var born=farm.TrySpawn();float birthMass=born.MassKg;
        Check(farm.TryUpgrade(0)&&farm.Level==2&&farm.SpawnInterval==12&&Math.Abs(farm.MassMultiplier-1.2f)<.001&&born.MassKg==birthMass,"Windows farm improves interval and future weight without mutating an existing animal");
        var heavier=farm.TrySpawn();Check(heavier!=null&&Math.Abs(heavier.MassKg-(heavier.Kind==Animal.Species.Cow?10:heavier.Kind==Animal.Species.Pig?6:4)*1.2f)<.001,"Windows farm really creates a heavier animal");
        yield return null;
        Check(Math.Abs(display.VisualFor(heavier).localScale.x-1/1.5f)<.001&&Math.Abs(heavier.GetComponent<CircleCollider2D>().radius-.35f/1.5f)<.001,"Windows animal model and collider shrink together");
        Check(Math.Abs(display.VisualFor(hero.GetComponent<Health>()).localScale.x-1)<.001,"Windows main hero size remains unchanged");
        Destroy(born.gameObject);Destroy(heavier.gameObject);
        Check(stock.Factory.TryOrderHelper(),"Windows factory can order its faster helper");
        var helper=FindObjectsByType<ButcherHelper>().Single();helper.enabled=false;helper.GetComponent<CarcassCarrier>().enabled=false;
        helper.transform.position=new Vector3(-12,-7);var prey=farm.TrySpawn();prey.enabled=false;prey.ConfigureSpawnMass(50);prey.transform.position=helper.transform.position+Vector3.right*.7f;helper.enabled=true;
        yield return new WaitForSeconds(.2f);
        Check(helper.GetComponent<HeroActionTiming25D>().AxePending,"Windows helper starts its real delayed animal strike");prey.transform.position+=Vector3.right;
        yield return new WaitForSeconds(.4f);
        Check(prey==null||prey.IsDead,"Windows helper kills moving heavy animal in a single committed blow");
        Check(helper.Returning&&helper.GetComponent<CarcassCarrier>().CurrentMassKg==10&&helper.MoveSpeed==4.5f,"Windows helper preserves cargo and uses faster movement");
        Check(Math.Abs(display.VisualFor(helper.GetComponent<Health>()).localScale.x-.5f/1.5f)<.001&&Math.Abs(helper.GetComponent<CircleCollider2D>().radius-.25f/1.5f)<.001,"Windows helper model and collider use reduced size");
        helper.enabled=false;Destroy(helper.GetComponent<CarcassCarrier>().StoredCargo[0].gameObject);yield return null;
        Check(!helper.Returning&&helper.GetComponent<CarcassCarrier>().Count==0,"Windows destroyed cargo cannot leave the worker stuck returning");
        helper.enabled=true;yield return new WaitForSeconds(1.5f);
        Check(helper.Returning&&helper.GetComponent<CarcassCarrier>().CurrentMassKg==10,"Windows worker resumes meat gathering after missing cargo cleanup");
        helper.enabled=false;
        var tiny=Instantiate(helper.GetComponent<CarcassCarrier>().StoredCargo[0],helper.transform.position,Quaternion.identity);tiny.Initialize(.00001f);
        helper.GetComponent<CarcassCarrier>().DropAll();foreach(var c in FindObjectsByType<Carcass>())Destroy(c.gameObject);yield return null;
        helper.enabled=true;int resets=helper.IdleRecoveries;yield return new WaitForSeconds(3.3f);
        Check(helper.IdleRecoveries==resets,"Windows worker without tasks stays still without periodic reassignment");
        helper.enabled=false;helper.transform.position=stock.Factory.transform.position+Vector3.right*1.5f;
        tiny.transform.position=helper.transform.position;tiny.gameObject.SetActive(true);
        Check(helper.GetComponent<CarcassCarrier>().TryCollect(tiny),"Windows tiny residue is actually picked up as cargo");
        double xp=owner.Experience;helper.enabled=true;yield return new WaitForSeconds(.2f);
        Check(!helper.Returning&&owner.Experience>xp,"Windows tiny residue is delivered through factory acceptance instead of hanging forever");
        helper.enabled=false;Destroy(helper.gameObject);
        yield return null;
        var menu=hero.GetComponent<BuildingUpgradeMenu>();screen=Camera.main.WorldToScreenPoint(ArenaSpace25D.ToWorld(ownShop.transform.position));
        Check(menu.SelectAtScreenPoint(screen)&&menu.Selected==ownShop&&menu.Contains(menu.ToolbarRect.center),"Selecting a shop uses its compact building toolbar");
        yield return null;Check(display.VisualFor(ownShop).Find("Selection")?.gameObject.activeSelf==true,"Selected building has its ground highlight");
        var enemyHealth=rival.GetComponent<Health>();enemyHealth.TakeDamage(10000,hero.GetComponent<Health>());
        var carcass=rival.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        Check(hero.GetComponent<CarcassCarrier>().TryCollect(carcass),"Actual enemy kill supports partial hero meat pickup");yield return null;yield return null;
        var cargo=display.VisualFor(hero.GetComponent<Health>()).Find("Cargo");
        Check(cargo.GetComponentsInChildren<Renderer>().Length>0&&cargo.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"Carried meat casts no stretched screen-wide shadow");
        Check(stock.Factory.CurrentHP==3000,"Own factory still retains its strongest full durability");
        Check(rival.GetComponent<OwnerEconomy>().Factory.TakeDamage(owner,3000)&&MatchPause.Instance.MatchEnded&&Time.timeScale==0,"Enemy factory destruction ends and freezes the Windows match");
        Check(!barracks.TryQueue(),"Finished match rejects further paid production");
    }
    void Update()
    {
        if(Time.realtimeSinceStartup>112&&!failed){failed=true;File.AppendAllText(Path.Combine(output,"result.txt"),"FAIL: timeout\n");Application.Quit(1);}
    }
#endif
}
