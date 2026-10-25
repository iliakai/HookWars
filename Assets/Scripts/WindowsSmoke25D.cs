using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit opt-in development-player acceptance run. No effect in ordinary games.
public sealed class WindowsSmoke25D : MonoBehaviour
{
#if !UNITY_EDITOR
    static string output;
    int assertions;
    int previousDepth;
    bool failed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!Debug.isDebugBuild)return;
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-hookwars-smoke");
        if(index<0)return;
        if(index+1>=args.Length)throw new ArgumentException("-hookwars-smoke needs an output directory");
        output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
        var go=new GameObject("Explicit Windows acceptance runner");DontDestroyOnLoad(go);go.AddComponent<WindowsSmoke25D>();
    }
    void Check(bool value,string text)
    {
        if(!value){failed=true;File.AppendAllText(Path.Combine(output,"result.txt"),"FAIL: "+text+"\n");Application.Quit(1);throw new InvalidOperationException(text);}
        assertions++;File.AppendAllText(Path.Combine(output,"result.txt"),"PASS: "+text+"\n");
    }
    static Color AuraTint(Renderer renderer)
    {var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);return properties.GetColor("_BaseColor");}
    IEnumerator Start()
    {
        Application.runInBackground=true;previousDepth=MatchConfiguration.LastDepth;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Development Windows player acceptance\nPersistent data directory: "+Application.persistentDataPath+"\n");
        foreach(int depth in new[]{4,16,20})
        {
            yield return new WaitForSecondsRealtime(.6f);
            var menu=FindAnyObjectByType<MainMenuController>();Check(menu!=null&&SceneManager.GetActiveScene().name=="MainMenu","First/return scene MainMenu");
            Check(FindAnyObjectByType<PlayerProgression>()==null&&FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length==0,"Menu has no match logic/chain bodies");
            menu.SetDepthText("invalid");Check(!menu.StartBattle(),"Invalid input rejected");menu.SetDepthText(depth.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"menu_"+depth+".png"));yield return new WaitForEndOfFrame();
            Check(menu.StartBattle()&&!menu.StartBattle(),"Single accepted match load");
            yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="Arena25D");
            var hero=FindAnyObjectByType<PlayerMovement>();var progress=hero.GetComponent<PlayerProgression>();
            var clock=MatchHud25D.Instance;var coinStream=FindAnyObjectByType<RiverCoinStream>();
            Check(clock!=null&&clock.ElapsedSeconds<1&&clock.PopupCount==0&&coinStream!=null&&coinStream.SecondsUntilSpawn>=24&&coinStream.SecondsUntilSpawn<=35&&progress.GoldPerSecond==.5f,
                "Windows fresh match starts HH:MM:SS clock, random coin delay and half-gold passive income");
            coinStream.enabled=false; // Explicit fixtures below control rewards; ordinary games keep autonomous spawning.
            Check(ArenaLayout.Depth==depth,"Configured depth "+depth);
            Check(progress.Gold<2&&progress.Experience==0&&progress.Level==1&&hero.GetComponent<OwnerEconomy>().Bones==0&&hero.GetComponent<OwnerEconomy>().ProductKg==0,"Fresh resources and level");
            Check(hero.GetComponent<Health>().MaximumKg==100&&hero.GetComponent<Health>().RegenerationPercentPerSecond==0,"Fresh session resets kill growth and percent regeneration");
            var anchors=FindObjectsByType<HookAnchor>();
            Check(anchors.Length==10&&anchors.Count(a=>a.transform.position.x<0)==5&&anchors.Count(a=>a.transform.position.x>0)==5,"Five hook eyes generated on each bank");
            int rocksPerBank=Mathf.Max(0,Mathf.Clamp(Mathf.FloorToInt(ArenaLayout.Depth/3),1,6)-1);
            var rocks=FindObjectsByType<PermanentRock>();
            Check(rocks.Count(r=>r.transform.position.x<0)==rocksPerBank&&rocks.Count(r=>r.transform.position.x>0)==rocksPerBank,"One fewer rock per bank at selected depth");
            var visibility=FindAnyObjectByType<FogOfWar25D>();
            visibility.RefreshObservers();
            Check(!visibility.IsRevealed(FindAnyObjectByType<EnemyChase>().transform.position)&&visibility.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Enemy bank is hidden and fog shader works in Windows player");
            foreach(var ai in FindObjectsByType<EnemyHookAI>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyMelee>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyChase>())ai.enabled=false;
            if(depth==16)
            {
                var stock = progress.GetComponent<OwnerEconomy>();
                var factory = stock.Factory; factory.enabled = false;
                Check(factory.CapacityKg==12 && factory.ProductCapacityKg==18 && factory.SecondsPerKg==2,
                    "Windows factory starts with 12kg raw, 18kg product and 2 seconds per kg");
                Check(factory.TryAcceptMass(1), "Windows factory accepts one kg raw meat");
                factory.Advance(2);
                Check(Math.Abs(stock.ProductKg-.5)<1e-6 && Math.Abs(stock.BoneKg-.5)<1e-6,
                    "Windows one kg raw yields half product and half bones");
                stock.AddOutput(17.5, 0);
                Check(!factory.TryAcceptMass(1), "Windows full finished storage blocks raw delivery");
                var saleObject = new GameObject("Explicit economy smoke shop");
                var shop = saleObject.AddComponent<MeatShop>(); shop.Initialize(progress);
                yield return new WaitForSeconds(2.2f);
                Check(Math.Abs(progress.CurrentGoldIncomePerSecond-2.5)<.03,
                    "Updated Windows player displays total income 2.5 with level one shop");
                progress.AddGold(100); Check(shop.TryUpgrade(0), "Windows shop level upgrade accepted");
                yield return new WaitForSeconds(2.2f);
                Check(Math.Abs(progress.CurrentGoldIncomePerSecond-4.5)<.03 && shop.GoldPerKg==20 && shop.Level==2,
                    "Updated Windows player displays total income 4.5 with level two shop");
                progress.AddGold(1000);
                Check(factory.TryUpgrade(0) && Math.Abs(factory.SecondsPerKg-1.4)<1e-6,
                    "Windows first processing upgrade reduces time to 1.4 seconds per kg");
                Check(factory.TryUpgrade(0) && Math.Abs(factory.SecondsPerKg-.98)<1e-6,
                    "Windows second processing upgrade reduces time to .98 seconds per kg");
                Check(factory.TryUpgrade(1) && factory.CapacityKg==30 && factory.ProductCapacityKg==36,
                    "Windows first capacity upgrade adds 18kg to both warehouses");
                Check(factory.TryUpgrade(1) && factory.CapacityKg==48 && factory.ProductCapacityKg==54,
                    "Windows second capacity upgrade adds another 18kg to both warehouses");
                Destroy(saleObject); factory.enabled = true;
            }
            yield return new WaitForSecondsRealtime(.7f);
            Check(FindObjectsByType<ButcherVisual25D>().Length==2&&FindObjectsByType<SpriteRenderer>().Length==0,"Both 3D heroes and zero world sprites");
            var heroView=FindAnyObjectByType<ArenaPresentation25D>().VisualFor(hero.GetComponent<Health>());
            var aura=heroView.Find("Hook mode aura")?.GetComponent<Renderer>();
            Check(aura!=null&&aura.sharedMaterial.shader.isSupported&&AuraTint(aura).r>.9f&&AuraTint(aura).b<.2f&&
                !heroView.Find("Team").gameObject.activeSelf,"Windows player shows red aura without the conflicting solid blue disk");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"battle_"+depth+".png"));yield return new WaitForEndOfFrame();
            var hook=hero.GetComponent<HookAbility>();
            Vector2 direction=Vector2.zero;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;var candidate=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                Vector3 end=hero.transform.position+(Vector3)(candidate*4);
                if(Mathf.Abs(end.y)<ArenaLayout.FieldHeight*.5f-.5f&&Mathf.Abs(end.x)<ArenaLayout.OuterEdge-.5f&&WorldCollision.CanMove(hero.transform,end,hook.Width*.5f))
                {direction=candidate;break;}
            }
            Check(direction!=Vector2.zero,"Generated map contains a clear four-meter hook test lane");
            hook.TryThrow(direction);Check(hero.GetComponent<HeroActionTiming25D>().HookPending,"Accepted animated hook cast");
            bool issued=false;float until=Time.time+1.2f;
            while(Time.time<until&&!issued)
            {
                yield return new WaitForEndOfFrame();
                issued=FindObjectsByType<HookChain25D>().Any(c=>c.ActiveLinks>0);
            }
            Check(hero.GetComponent<HookFlightAudio>()?.PlaybackCount==1,"Flight audio triggered once on actual hook release");
            Check(issued,"Native PhysX chain issued in Windows player");
            if(depth==16)
            {
                var victim=FindAnyObjectByType<EnemyChase>().GetComponent<Health>();var eater=hero.GetComponent<Health>();
                victim.TakeDamage(100000,eater);
                var meat=victim.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                Check(eater.MaximumKg==120&&meat.MassKg==80,"Native hero kill divides weight 20/80");
                var carrier=hero.GetComponent<CarcassCarrier>();
                Check(carrier.TryCollect(meat)&&carrier.CurrentMassKg==10&&meat.MassKg==70,"Native partial pickup takes ten kg and leaves seventy");
                victim.TakeDamage(100000,eater);Check(eater.MaximumKg==120,"Repeated dead-target damage cannot award weight twice");
                var smallObject = new GameObject("Explicit small lifetime fixture"); smallObject.AddComponent<BoxCollider2D>();
                var smallMeat = smallObject.AddComponent<Carcass>(); smallMeat.Initialize(6);
                var largeObject = new GameObject("Explicit large lifetime fixture"); largeObject.AddComponent<BoxCollider2D>();
                var largeMeat = largeObject.AddComponent<Carcass>(); largeMeat.Initialize(80, eater.GetComponent<HookVictim>());
                Check(smallMeat.GroundLifetimeSeconds==60 && largeMeat.GroundLifetimeSeconds==300,
                    "Windows small and large meat use sixty and three hundred game seconds");
                smallMeat.AdvanceGroundLifetime(59); largeMeat.AdvanceGroundLifetime(299);
                Check(smallMeat.IsAvailable && largeMeat.IsAvailable, "Windows meat survives before each expiry deadline");
                smallMeat.AdvanceGroundLifetime(1); largeMeat.AdvanceGroundLifetime(1);
                Check(!smallMeat.IsAvailable && !largeMeat.IsAvailable, "Windows expiry immediately prevents pickup");
                yield return null;
                Check(smallMeat==null && largeMeat==null, "Windows player destroys both expired meat objects");
                hook.CancelHook();
                Check(hook.SelectedPullMode==HookAbility.PullMode.TargetToOwner&&hook.TryTogglePullMode()&&hook.SelectedPullMode==HookAbility.PullMode.OwnerToTarget,
                    "Windows hook switches from default pull to owner-flight mode");
                Vector3 ownerStart=hero.transform.position,flightTarget=ownerStart;Vector2 flightDirection=Vector2.zero;
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;var candidate=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                    var end=ownerStart+(Vector3)(candidate*3);
                    if(ArenaLayout.IsInsideBank(end,end.x>0)&&WorldCollision.CanMove(hero.transform,end,ArenaLayout.HeroRadius))
                    {flightDirection=candidate;flightTarget=end;break;}
                }
                Check(flightDirection!=Vector2.zero,"Windows map has a safe owner-flight lane");
                var flightObject=new GameObject("Explicit owner-flight target fixture");flightObject.transform.position=flightTarget;
                flightObject.layer=LayerMask.NameToLayer("HookTarget");flightObject.AddComponent<BoxCollider2D>().size=Vector2.one*.3f;
                flightObject.AddComponent<HookVictim>();var flightHealth=flightObject.AddComponent<Health>();
                yield return new WaitForSeconds(hook.Cooldown+.1f);
                Check(AuraTint(aura).b>.9f&&AuraTint(aura).r<.2f,"Windows owner-flight mode shows light blue aura");
                hook.TryThrow(flightDirection);Check(hook.IsPreparingThrow,"Windows owner-flight uses normal animated windup");
                float flightDeadline=Time.time+4;
                while((hook.IsPreparingThrow||hook.ActiveHook!=null)&&Time.time<flightDeadline)yield return null;
                Check(hook.ActiveHook==null&&!hero.GetComponent<HookVictim>().IsCaught&&Vector2.Distance(ownerStart,hero.transform.position)>1.5f&&
                    Vector2.Distance(flightTarget,hero.transform.position)<1.2f&&flightObject.transform.position==flightTarget&&flightHealth.CurrentKg==60,
                    "Windows animated hook flies owner to stationary target, deals damage and releases movement lock");
                Check(hook.TryTogglePullMode()&&hook.SelectedPullMode==HookAbility.PullMode.TargetToOwner,"Windows E-mode action returns to target pull");
                yield return null;
                Check(AuraTint(aura).r>.9f&&AuraTint(aura).b<.2f,"Windows switching back restores red aura");
                Destroy(flightObject);
                yield return new WaitForSeconds(hook.Cooldown+.1f);
                Vector3 coinOrigin=Vector3.zero;bool coinLane=false;
                for(float y=-7;y<=7&&!coinLane;y++)
                {
                    var candidate=new Vector3(hero.transform.position.x>0?4:-4,y);
                    if(!WorldCollision.FreeCircle(candidate,ArenaLayout.HeroRadius))continue;
                    Vector2 intoRiver=new Vector2(-Mathf.Sign(candidate.x),0);
                    bool blocked=Physics2D.CircleCastAll(candidate,hook.Width*.5f,intoRiver,Mathf.Abs(candidate.x),LayerMask.GetMask("Buildings","Mountains","HookTarget"))
                        .Any(hit=>hit.transform!=hero.transform&&(!hit.collider.isTrigger||hit.collider.GetComponent<HookAnchor>()!=null));
                    if(!blocked){coinOrigin=candidate;coinLane=true;}
                }
                Check(coinLane,"Windows generated map contains a clear bank-to-river coin lane");hero.transform.position=coinOrigin;Physics2D.SyncTransforms();
                var coin=coinStream.SpawnOne();coin.transform.position=new Vector3(0,coinOrigin.y);
                int reward=coin.Value;Check(reward>=25&&reward<=75,"Windows coin reward is an integer in inclusive range twenty-five to seventy-five");
                Check(hook.TryTogglePullMode()&&hook.SelectedPullMode==HookAbility.PullMode.OwnerToTarget,"Select blue mode to verify coin-only pull override");
                yield return null;
                var coinView=FindAnyObjectByType<ArenaPresentation25D>().VisualFor(coin);
                Check(coinView!=null&&coinView.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterials.All(m=>m.shader.isSupported&&m.GetFloat("_Cull")==0&&m.IsKeywordEnabled("_EMISSION"))),"Windows imported gold coin has supported shaders, both-face rendering and readable gold fill");
                float distance=Vector2.Distance(coinOrigin,coin.transform.position);
                Vector2 predicted=(Vector2)coin.transform.position+Vector2.down*coin.FlowSpeed*(HeroActionTiming25D.HookRelease+distance/hook.Speed);
                double goldBefore=progress.Gold,xpBefore=progress.Experience;float coinStarted=Time.time;
                hook.TryThrow(predicted-(Vector2)coinOrigin);Check(hook.IsPreparingThrow,"Windows coin catch starts an actual animated throw");
                float coinDeadline=Time.time+4;while(coin!=null&&Time.time<coinDeadline)yield return null;
                Check(coin==null&&Vector2.Distance(hero.transform.position,coinOrigin)<.001f&&
                    Math.Abs(progress.Gold-goldBefore-.5*(Time.time-coinStarted)-reward)<.1&&progress.Experience==xpBefore,
                    "Windows blue hook pulls coin to stationary hero and awards its value once without XP");
                Check(clock.PopupCount==1,"Windows delivered coin shows one rising gold reward label");
                yield return new WaitForSeconds(1.1f);Check(clock.PopupCount==0,"Windows reward label disappears after one game second");
                var missed=coinStream.SpawnOne();missed.transform.position=new Vector3(0,-10.55f);missed.Advance(1);yield return null;
                Check(missed==null,"Windows missed coin washes out and is destroyed");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"chain_"+depth+".png"));yield return new WaitForEndOfFrame();
            var pause=MatchPause.Instance;pause.HandleEscape();Check(pause.IsPaused&&Time.timeScale==0,"Pause freezes game");
            double pausedClock=clock.ElapsedSeconds;yield return new WaitForSecondsRealtime(.15f);
            Check(clock.ElapsedSeconds==pausedClock,"Windows match clock freezes during pause");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"pause_"+depth+".png"));yield return new WaitForEndOfFrame();
            Check(pause.ReturnToMenu()&&!pause.ReturnToMenu(),"Return is accepted only once");
            yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="MainMenu");
            yield return new WaitForSecondsRealtime(.4f);
            Check(Time.timeScale==1&&SceneManager.sceneCount==1&&MatchHud25D.Instance==null&&FindObjectsByType<RiverCoin>().Length==0,"No residual physics scene, clock, coins or time scale");
        }
        MatchConfiguration.RememberDepth(previousDepth);
        File.AppendAllText(Path.Combine(output,"result.txt"),"HOOKWARS_WINDOWS_SMOKE_PASS: "+assertions+" assertions\n");Application.Quit(0);
    }
    void Update(){if(Time.realtimeSinceStartup>100&&!failed){failed=true;File.AppendAllText(Path.Combine(output,"result.txt"),"FAIL: timeout\n");Application.Quit(1);}}
#endif
}
