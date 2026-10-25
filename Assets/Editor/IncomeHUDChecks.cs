using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class IncomeHUDChecks
{
    public const string Key = "HookWars.IncomeChecks";
    static int phase, count;
    static double deadline, ready;
    static float due;
    static PlayerProgression player;
    static OwnerEconomy stock;
    static MeatShop first, second;
    static double pausedGold, pausedUntil;

    static IncomeHUDChecks() { if (SessionState.GetBool(Key, false)) Subscribe(); }
    [MenuItem("HookWars/Economy/Check actual income HUD Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();
        EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Passed", false);
        Subscribe();
    }
    static void Subscribe()
    {
        phase = count = 0; due = 0;
        ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 90;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        count++; Debug.Log("INCOME_CHECK: " + message);
    }
    static void Rate(double expected, string message) => Check(Math.Abs(player.CurrentGoldIncomePerSecond - expected) < .03,
        message + ": expected " + expected + ", actual " + player.CurrentGoldIncomePerSecond);
    static MeatShop Shop(Vector2 at)
    {
        var shop = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MeatShop>(EconomySetup.ShopPath), at, Quaternion.identity);
        Check(shop.Initialize(player), "Real shop initialized for HUD owner");
        return shop;
    }
    static void Next(int value) { phase = value; due = Time.time + 2.2f; }
    static void Tick()
    {
        try
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < ready) return;
            if (phase == 99)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + ".Passed", false) ? 0 : 1);
                }
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Income checks timed out");
            if (phase == 0) { EditorApplication.isPlaying = true; phase = 1; return; }
            if (!EditorApplication.isPlaying || Time.time < due) return;
            if (phase == 1)
            {
                foreach (var ai in Object.FindObjectsByType<EnemyHookAI>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyChase>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyMelee>()) ai.enabled = false;
                player = Object.FindAnyObjectByType<PlayerMovement>().GetComponent<PlayerProgression>();
                stock = player.GetComponent<OwnerEconomy>(); Next(2); return;
            }
            if (phase == 2)
            {
                Rate(.5, "Passive income without shops");
                stock.AddOutput(12, 0); first = Shop(new Vector2(-6, -5)); Next(3); return;
            }
            if (phase == 3)
            {
                Rate(2.5, "Passive plus actual sales from one shop");
                second = Shop(new Vector2(-8, -5)); Next(4); return;
            }
            if (phase == 4)
            {
                Rate(4.5, "Two shops share stock and both contribute");
                Check(player.TrySpendGold(1), "Purchase accepted without changing income");
                Directory.CreateDirectory("Temp/IncomePreview");
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
                ScreenCapture.CaptureScreenshot("Temp/IncomePreview/income.png");
                Next(5); return;
            }
            if (phase == 5)
            {
                Rate(4.5, "Purchases do not reduce gross receipts");
                second.enabled = false; Next(6); return;
            }
            if (phase == 6)
            {
                Rate(2.5, "Disabled shop no longer contributes");
                stock.TakeProduct(10000); Next(7); return;
            }
            if (phase == 7)
            {
                Rate(.5, "Empty stock contributes no theoretical shop income");
                stock.AddOutput(.025, 0); pausedGold = player.Gold; Next(8); return;
            }
            if (phase == 8)
            {
                double elapsed = 2.2 + Time.time - due;
                Check(Math.Abs((player.Gold - pausedGold) - (.5 * elapsed + .5)) < .1,
                    "Limited stock yields only its actual .5 gold, plus passive income");
                Rate(.5, "Exhausted stock returns to passive rate");
                MatchPause.Instance.SetPaused(true); pausedGold = player.Gold;
                pausedUntil = EditorApplication.timeSinceStartup + .4; phase = 9; return;
            }
            if (phase == 9)
            {
                if (EditorApplication.timeSinceStartup < pausedUntil) return;
                Rate(0, "Pause displays zero income");
                Check(player.Gold == pausedGold, "No gold arrives while paused");
                MatchPause.Instance.SetPaused(false); Next(10); return;
            }
            if (phase == 10)
            {
                Rate(.5, "Resume excludes paused time from income sampling");
                var fresh = new GameObject("Fresh income session").AddComponent<PlayerProgression>();
                Check(fresh.Gold == 0 && fresh.CurrentGoldIncomePerSecond == .5, "Fresh owner starts with zero gold and default income");
                Object.Destroy(fresh.gameObject);
                StorageChecks();
                File.WriteAllText("docs/validation/022-income.txt", $"PASS: {count} actual Unity Play Mode assertions\nPassive .5/s; one/two real shops; shared/empty/limited stock; spending; disabled shop; pause/resume; fresh owner; repeated factory upgrades.\n");
                SessionState.SetBool(Key + ".Passed", true); Debug.Log("HOOKWARS_INCOME_PASS: " + count);
                Stop();
            }
        }
        catch (Exception error) { Debug.LogError("HOOKWARS_INCOME_FAIL: " + error); Stop(); }
    }
    static void Stop() { phase = 99; EditorApplication.isPlaying = false; }
    static void StorageChecks()
    {
        var root = new GameObject("Isolated production acceptance checks");
        try
        {
            var wallet = root.AddComponent<OwnerEconomy>();
            var owner = root.GetComponent<PlayerProgression>(); owner.enabled = false;
            var factory = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SausageFactory>(EconomySetup.FactoryPath), root.transform);
            factory.enabled = false;
            Check(factory.Initialize(owner), "Production fixture owns its own storage");
            Check((bool)EconomyChecks.Invoke(factory, "TryAcceptMass", 1d), "Factory accepts one kg raw meat");
            factory.Advance(2);
            Check(EconomyChecks.Near(wallet.ProductKg, .5) && EconomyChecks.Near(wallet.BoneKg, .5) && factory.RawKg == 0,
                "One kg raw becomes exactly .5 kg ready meat plus .5 kg bones");
            wallet.AddOutput(17.5, 0); double xp = owner.Experience;
            Check((bool)EconomyChecks.Invoke(factory, "TryAcceptMass", 1d) && owner.Experience == xp+2,
                "Eighteen kg ready stock no longer blocks raw acceptance and its XP");
            factory.Advance(1000);
            Check(wallet.ProductKg == 18.5 && wallet.BoneKg == 1, "Unlimited ready storage continues processing with conserved output");
            var shop = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MeatShop>(EconomySetup.ShopPath), root.transform);
            shop.enabled = false; shop.Initialize(owner);
            double gold = owner.Gold; shop.Advance(1);
            Check(Math.Abs(owner.Gold - gold - 2) < 1e-6 && Math.Abs(wallet.ProductKg - 18.4) < 1e-6,
                "Level one shop sells .1kg for 2 gold from unlimited ready storage");
            Check((bool)EconomyChecks.Invoke(factory, "TryAcceptMass", 2d), "Raw acceptance depends only on raw capacity");
            factory.Advance(1000);
            Check(Math.Abs(wallet.ProductKg - 19.4) < 1e-6 && factory.RawKg==0,
                "Processing can exceed the former finished storage capacity without blocking");
            Check(!factory.CanProcess, "Empty raw queue closes working visual state");
            wallet.TakeProduct(18); factory.Advance(1000);
            Check(factory.RawKg == 0 && Math.Abs(wallet.ProductKg - 1.4) < 1e-6 && Math.Abs(wallet.BoneKg - 2) < 1e-6,
                "Taking finished stock does not create extra product or bones");
            owner.AddGold(1000);
            Check(factory.TryUpgrade(1) && double.IsPositiveInfinity(factory.ProductCapacityKg) && factory.CapacityKg == 30,
                "Storage upgrade expands raw capacity while finished stock remains unlimited");
            Check(shop.TryUpgrade(0) && shop.Level == 2 && Math.Abs(shop.GoldPerSecond - 4) < 1e-6 && shop.GoldPerKg == 20,
                "Level two shop earns four gold/s at unchanged twenty gold/kg");
            wallet.TakeProduct(wallet.ProductKg);wallet.AddOutput(1.9, 0); gold = owner.Gold; shop.Advance(10);
            Check(Math.Abs(owner.Gold - gold - 38) < 1e-5, "Remaining 1.9kg finished meat sells for thirty-eight gold");
            Check(factory.TryUpgrade(1) && factory.CapacityKg == 48 && double.IsPositiveInfinity(factory.ProductCapacityKg),
                "Second capacity upgrade adds another eighteen raw kg without limiting finished stock");
            Check(factory.TryUpgrade(0) && EconomyChecks.Near(factory.SecondsPerKg, 1.4),
                "First processing upgrade reduces seconds per kg by thirty percent");
            Check(factory.TryUpgrade(0) && EconomyChecks.Near(factory.SecondsPerKg, .98),
                "Second processing upgrade compounds the thirty percent reduction");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
