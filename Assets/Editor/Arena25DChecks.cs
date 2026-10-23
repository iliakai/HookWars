using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled calls on real Play Mode objects; not a manual keyboard/mouse pass.
[InitializeOnLoad]
public static class Arena25DChecks
{
    private const string Key = "HookWars.25D.Pending";
    private static double deadline, ready;
    private static int phase, assertions;
    static Arena25DChecks()
    {
        if (SessionState.GetBool(Key, false)) StartWaiting();
    }
    [MenuItem("HookWars/25D/Check Arena25D Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode.");
        assertions = 0; SessionState.SetInt(Key + ".Assertions", 0);
        SessionState.SetBool(Key + ".Finishing", false);
        string original = Hash(CarryingSetup.ArenaPath), build = Hash("ProjectSettings/EditorBuildSettings.asset");
        Arena25DSetup.Configure(); string scene = Hash(Arena25DSetup.ScenePath);
        Arena25DSetup.Configure();
        Check(scene == Hash(Arena25DSetup.ScenePath), "Setup leaves scene bytes unchanged on repeat");
        Check(original == Hash(CarryingSetup.ArenaPath) && build == Hash("ProjectSettings/EditorBuildSettings.asset"), "Arena and build scenes unchanged");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); StartWaiting();
    }
    private static string Hash(string path)
    {
        using (var hash = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
    }
    private static void StartWaiting()
    {
        assertions = SessionState.GetInt(Key + ".Assertions", 0);
        phase = SessionState.GetBool(Key + ".Finishing", false) ? 2 : 0;
        ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 90;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("25D checks timed out");
            if (phase == 0)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < ready) return;
                phase = 1; EditorApplication.isPaused = false; EditorApplication.isPlaying = true; return;
            }
            if (phase == 1)
            {
                if (!Application.isPlaying || Time.frameCount < 5 || ArenaLayout.Instance == null) return;
                Scenarios();
                Debug.Log($"HOOKWARS_25D_PASS: {assertions} assertions");
                SessionState.SetBool(Key + ".Passed", true); SessionState.SetBool(Key + ".Finishing", true);
                phase = 2; EditorApplication.isPlaying = false; return;
            }
            if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode) Finish();
        }
        catch (Exception error)
        {
            Debug.LogError("HOOKWARS_25D_FAIL: " + error); SessionState.SetBool(Key + ".Finishing", true);
            phase = 2; EditorApplication.isPlaying = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish();
        }
    }
    private static void Finish()
    {
        SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finishing", false); EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + ".Passed", false) ? 0 : 1);
    }
    private static void Scenarios()
    {
        Camera camera = Camera.main; var rig = camera.GetComponent<ArenaCamera25D>();
        var display = Object.FindAnyObjectByType<ArenaPresentation25D>();
        Check(rig != null && display != null, "25D presentation active");
        Check(Object.FindObjectsByType<Collider>().Length == 0 && Object.FindObjectsByType<Rigidbody>().Length == 0, "No parallel 3D physics");
        Check(Object.FindObjectsByType<SpriteRenderer>().All(s => s.forceRenderingOff), "Old sprites hidden");
        foreach (int depth in new[] { 4, 20 })
        {
            Set(ArenaLayout.Instance, "bankDepth", depth); Call(ArenaLayout.Instance, "Awake"); Call(display, "LateUpdate");
            Transform terrain = display.transform.Find("Generated 25D visuals");
            Check(Mathf.Abs(terrain.Find("left bank").localScale.x - depth) < 0.001f, "Bank depth matches layout");
            Check(Mathf.Abs(terrain.Find("river").localScale.x - ArenaLayout.RiverWidth) < 0.001f && Mathf.Abs(terrain.Find("left bank").localScale.z - ArenaLayout.FieldHeight) < 0.001f, "River and field dimensions match layout");
            foreach (float aspect in new[] { 0.75f, 16f / 9f, 21f / 9f })
                foreach (float tilt in new[] { 25f, 55f, 80f })
                {
                    camera.aspect = aspect; rig.Tilt = tilt; rig.Fit();
                    foreach (float x in new[] { -ArenaLayout.OuterEdge, ArenaLayout.OuterEdge })
                        foreach (float y in new[] { -10f, 10f })
                        {
                            Vector3 world = ArenaSpace25D.ToWorld(new Vector2(x, y)); Vector3 viewport = camera.WorldToViewportPoint(world);
                            Check(viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1 && viewport.z > 0, "Field corner framed");
                            Check(GamePointer.TryLogicalPoint(camera, camera.WorldToScreenPoint(world), out Vector3 logical) && Vector2.Distance(logical, new Vector2(x, y)) < 0.002f, "Ground ray round trip");
                        }
                }
        }
        Set(ArenaLayout.Instance, "bankDepth", 16); Call(ArenaLayout.Instance, "Awake"); camera.ResetAspect(); rig.Tilt = 55; rig.Fit();
        var player = Object.FindAnyObjectByType<PlayerMovement>(); Vector3 saved = player.transform.position;
        Vector3 origin = camera.WorldToScreenPoint(ArenaSpace25D.ToWorld(saved));
        Check(camera.WorldToScreenPoint(ArenaSpace25D.ToWorld((Vector2)saved + Vector2.up)).y > origin.y, "W projects up");
        Check(camera.WorldToScreenPoint(ArenaSpace25D.ToWorld((Vector2)saved + Vector2.right)).x > origin.x, "D projects right");
        Quaternion rotation = player.transform.rotation; Call(display, "LateUpdate");
        Check(player.transform.position == saved && player.transform.rotation == rotation, "Visual sync preserves logical transform");
        display.enabled = false;
        Check(Object.FindObjectsByType<SpriteRenderer>().All(s => !s.forceRenderingOff), "Disabling presentation restores original sprite visibility");
        Set(display, "terrain", null); Set(display, "views", null); Set(display, "hidden", null);
        Set(display, "palette", null); Set(display, "previewProperties", null);
        display.enabled = true; Call(display, "LateUpdate");
        Check(Object.FindObjectsByType<SpriteRenderer>().All(s => s.forceRenderingOff), "Lost runtime caches can be rebuilt without exceptions");
        Check(display.transform.Cast<Transform>().Count(t => t.name == "Generated 25D visuals" && t.gameObject.activeSelf) == 1, "Rebuilding presentation does not duplicate active visual roots");
        Check(GamePointer.OverInterface(new Vector2(30, Screen.height - 30)), "UI blocks world actions");
        var hook = player.GetComponent<PlayerHook>(); Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
        player.GetComponent<HeroActionTiming25D>()?.Advance(Time.time + HeroActionTiming25D.HookRelease + 0.001f);
        Check(hook.ActiveHook != null, "Hook spawned");
        var bends = (List<Vector3>)typeof(HookAbility).GetField("bends", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hook);
        bends.Add(saved + Vector3.right); hook.ActiveHook.position = saved + new Vector3(1, 2);
        Call(display, "LateUpdate");
        LineRenderer chain = display.GetComponentsInChildren<LineRenderer>().First(l => l.name == player.name + " chain 25D");
        Check(chain.positionCount == 3 && chain.positionCount == hook.ChainPointCount, "Chain includes logical bend");
        var butcher = display.GetComponentsInChildren<ButcherVisual25D>().FirstOrDefault(b => b.name == "Model" && b.transform.parent.name == player.name + " 25D");
        for (int i = 0; i < chain.positionCount; i++)
        {
            Vector3 expected = i == 0 && butcher != null ? butcher.Socket.position : ArenaSpace25D.ToWorld(hook.GetChainPoint(i), 0.65f);
            Check(Vector3.Distance(chain.GetPosition(i), expected) < 0.001f, "Chain point agrees with logic/socket");
        }
        Type state = typeof(HookAbility).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).FieldType;
        Set(hook, "state", Enum.Parse(state, "Returning")); Call(display, "LateUpdate");
        Check(!hook.IsFlyingOut && chain.positionCount == 3, "Return keeps bent route visible");
        hook.CancelHook(); Call(display, "LateUpdate"); Check(chain.positionCount == 0, "Chain hides after cancel");
        // Existing gameplay entry points produce new visual objects without 3D physics.
        var builder = player.GetComponent<PlayerBuilding>(); player.GetComponent<PlayerProgression>().AddGold(1000);
        builder.SelectBuilding(AssetDatabase.LoadAssetAtPath<GameBuilding>(EconomySetup.ShopPath));
        Vector3 cell = new Vector3(-8.5f, 3.5f); GameBuilding building = builder.TryBuildAt(cell);
        Check(building != null, "Existing cell placement succeeds"); Call(display, "LateUpdate");
        Transform visualRoot = display.transform.Find("Generated 25D visuals");
        Check(visualRoot.Find(building.name + " 25D").position == ArenaSpace25D.ToWorld(cell), "Spawned building aligns with logical cell");
        Physics2D.SyncTransforms();
        Vector2 click = camera.WorldToScreenPoint(ArenaSpace25D.ToWorld(cell));
        Check(GamePointer.TryLogicalPoint(camera, click, out Vector3 ground) &&
            Physics2D.OverlapPoint(ground, LayerMask.GetMask("Buildings"))?.GetComponent<GameBuilding>() == building, "Ground ray hits logical building cell");
        if (!GamePointer.OverInterface(click))
            Check(player.GetComponent<BuildingUpgradeMenu>().SelectAtScreenPoint(click), "Ground ray selects owned building");
        else Debug.LogWarning("25D_CHECK_SKIP: building menu click covered by HUD at current resolution; verify in a larger Game view.");
        Carcass item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath), saved + Vector3.up, Quaternion.identity);
        Call(display, "LateUpdate");
        Check(player.GetComponent<CarcassCarrier>().TryCollect(item), "Existing carcass pickup succeeds"); Call(display, "LateUpdate");
        Check(!visualRoot.Find(item.name + " 25D").gameObject.activeSelf && visualRoot.Find(player.name + " 25D/Cargo").gameObject.activeSelf, "Stored carcass hides and cargo marker appears");
        Health health = player.GetComponent<Health>(); health.TakeDamage(1000); Call(display, "LateUpdate");
        Carcass own = player.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        Check(!visualRoot.Find(player.name + " 25D").gameObject.activeSelf && own != null && item.gameObject.activeSelf, "Death hides hero and drops independent carcasses");
        health.Revive(); Call(display, "LateUpdate");
        Check(visualRoot.Find(player.name + " 25D").gameObject.activeSelf && own != null, "Controlled revival restores visual without deleting carcass");
    }
    private static void Set(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance); if (field != null) { field.SetValue(obj, value); return; } }
        throw new MissingFieldException(name);
    }
    private static void Call(object obj, string name) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, null);
    private static void Check(bool valid, string detail)
    {
        if (!valid) throw new InvalidOperationException(detail);
        assertions++; SessionState.SetInt(Key + ".Assertions", assertions); Debug.Log("25D_CHECK: " + detail);
    }
}
