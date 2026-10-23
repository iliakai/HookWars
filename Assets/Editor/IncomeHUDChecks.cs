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
    static MeatShop second;
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
                { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; }
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
                Rate(2, "Passive income without shops");
                stock.AddOutput(100, 0); Shop(new Vector2(-6, -5)); Next(3); return;
            }
            if (phase == 3)
            {
                Rate(4.5, "Passive plus actual sales from one shop");
                second = Shop(new Vector2(-8, -5)); Next(4); return;
            }
            if (phase == 4)
            {
                Rate(7, "Two shops share stock and both contribute");
                Check(player.TrySpendGold(1), "Purchase accepted without changing income");
                Directory.CreateDirectory("Temp/IncomePreview");
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
                ScreenCapture.CaptureScreenshot("Temp/IncomePreview/income.png");
                Next(5); return;
            }
            if (phase == 5)
            {
                Rate(7, "Purchases do not reduce gross receipts");
                second.enabled = false; Next(6); return;
            }
            if (phase == 6)
            {
                Rate(4.5, "Disabled shop no longer contributes");
                stock.TakeProduct(10000); Next(7); return;
            }
            if (phase == 7)
            {
                Rate(2, "Empty stock contributes no theoretical shop income");
                stock.AddOutput(.1, 0); pausedGold = player.Gold; Next(8); return;
            }
            if (phase == 8)
            {
                double elapsed = 2.2 + Time.time - due;
                Check(Math.Abs((player.Gold - pausedGold) - (2 * elapsed + .5)) < .1,
                    "Limited stock yields only its actual .5 gold, plus passive income");
                Rate(2, "Exhausted stock returns to passive rate");
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
                Rate(2, "Resume excludes paused time from income sampling");
                var fresh = new GameObject("Fresh income session").AddComponent<PlayerProgression>();
                Check(fresh.Gold == 0 && fresh.CurrentGoldIncomePerSecond == 2, "Fresh owner starts with zero gold and default income");
                Object.Destroy(fresh.gameObject);
                File.WriteAllText("docs/validation/011-income.txt", $"PASS: {count} actual Unity Play Mode assertions\nPassive income; one/two real shops; shared/empty/limited stock; spending; disabled shop; pause/resume; fresh owner.\n");
                SessionState.SetBool(Key + ".Passed", true); Debug.Log("HOOKWARS_INCOME_PASS: " + count);
                Stop();
            }
        }
        catch (Exception error) { Debug.LogError("HOOKWARS_INCOME_FAIL: " + error); Stop(); }
    }
    static void Stop() { phase = 99; EditorApplication.isPlaying = false; }
}
