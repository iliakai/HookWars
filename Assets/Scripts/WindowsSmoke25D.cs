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
            Check(ArenaLayout.Depth==depth,"Configured depth "+depth);
            Check(progress.Gold<2&&progress.Experience==0&&progress.Level==1&&hero.GetComponent<OwnerEconomy>().Bones==0&&hero.GetComponent<OwnerEconomy>().ProductKg==0,"Fresh resources and level");
            foreach(var ai in FindObjectsByType<EnemyHookAI>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyMelee>())ai.enabled=false;
            foreach(var ai in FindObjectsByType<EnemyChase>())ai.enabled=false;
            if(depth==16)
            {
                var stock = progress.GetComponent<OwnerEconomy>();
                var factory = stock.Factory; factory.enabled = false;
                Check(factory.TryAcceptMass(1), "Windows factory accepts one kg raw meat");
                factory.Advance(2);
                Check(Math.Abs(stock.ProductKg-.5)<1e-6 && Math.Abs(stock.BoneKg-.5)<1e-6,
                    "Windows one kg raw yields half product and half bones");
                stock.AddOutput(11.5, 0);
                Check(!factory.TryAcceptMass(1), "Windows full finished storage blocks raw delivery");
                var saleObject = new GameObject("Explicit economy smoke shop");
                var shop = saleObject.AddComponent<MeatShop>(); shop.Initialize(progress);
                yield return new WaitForSeconds(2.2f);
                Check(Math.Abs(progress.CurrentGoldIncomePerSecond-2.5)<.03,
                    "Updated Windows player displays total income 2.5 with level one shop");
                progress.AddGold(100); Check(shop.TryUpgrade(0), "Windows shop level upgrade accepted");
                yield return new WaitForSeconds(2.2f);
                Check(Math.Abs(progress.CurrentGoldIncomePerSecond-3)<.03 && shop.GoldPerKg==10 && shop.Level==2,
                    "Updated Windows player displays total income 3 with level two shop");
                Destroy(saleObject); factory.enabled = true;
            }
            yield return new WaitForSecondsRealtime(.7f);
            Check(FindObjectsByType<ButcherVisual25D>().Length==2&&FindObjectsByType<SpriteRenderer>().Length==0,"Both 3D heroes and zero world sprites");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"battle_"+depth+".png"));yield return new WaitForEndOfFrame();
            var hook=hero.GetComponent<HookAbility>();hook.TryThrow(Vector2.up);Check(hero.GetComponent<HeroActionTiming25D>().HookPending,"Accepted animated hook cast");
            yield return new WaitForSeconds(.65f);
            Check(FindObjectsByType<HookChain25D>().Any(c=>c.ActiveLinks>0),"Native PhysX chain issued in Windows player");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"chain_"+depth+".png"));yield return new WaitForEndOfFrame();
            var pause=MatchPause.Instance;pause.HandleEscape();Check(pause.IsPaused&&Time.timeScale==0,"Pause freezes game");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"pause_"+depth+".png"));yield return new WaitForEndOfFrame();
            Check(pause.ReturnToMenu()&&!pause.ReturnToMenu(),"Return is accepted only once");
            yield return new WaitUntil(()=>SceneManager.GetActiveScene().name=="MainMenu");
            yield return new WaitForSecondsRealtime(.4f);
            Check(Time.timeScale==1&&SceneManager.sceneCount==1,"No residual physics scene or time scale");
        }
        MatchConfiguration.RememberDepth(previousDepth);
        File.AppendAllText(Path.Combine(output,"result.txt"),"HOOKWARS_WINDOWS_SMOKE_PASS: "+assertions+" assertions\n");Application.Quit(0);
    }
    void Update(){if(Time.realtimeSinceStartup>100&&!failed){failed=true;File.AppendAllText(Path.Combine(output,"result.txt"),"FAIL: timeout\n");Application.Quit(1);}}
#endif
}
