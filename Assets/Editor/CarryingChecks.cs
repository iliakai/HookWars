using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Uses real Unity objects/Physics2D in Play Mode, without changing production assemblies.
[InitializeOnLoad]
public static class CarryingChecks
{
    private const string PendingKey = "HookWars.CarryingChecks.Pending";
    private static readonly List<GameObject> created = new List<GameObject>();
    private static int assertions;
    private static double deadline;
    private static bool running;
    private static int phase;
    private static GameObject timedPlayer;
    private static GameObject timedEnemy;
    private static TrainingMatch timedMatch;
    private static Carcass timedPlayerCarcass;
    private static Carcass timedEnemyCarcass;
    private static double resumeAt;
    private static int waitFrame;
    private static double stableSince;

    static CarryingChecks()
    {
        if (SessionState.GetBool(PendingKey + ".Starting", false))
        {
            stableSince = EditorApplication.timeSinceStartup;
            EditorApplication.update += BeginWhenReady;
        }
        else if (SessionState.GetBool(PendingKey, false))
        {
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Tick;
        }
        if (SessionState.GetBool(PendingKey + ".Finishing", false))
            EditorApplication.update += WaitForEditModeExit;
    }

    [MenuItem("HookWars/Run carrying Play Mode checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Сначала остановите Play Mode.");
        CarryingSetup.Configure();
        byte[] before = System.IO.File.ReadAllBytes(CarryingSetup.ArenaPath);
        CarryingSetup.Configure();
        SessionState.SetBool(PendingKey + ".Idempotent", System.Linq.Enumerable.SequenceEqual(before,
            System.IO.File.ReadAllBytes(CarryingSetup.ArenaPath)));
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(PendingKey + ".Batch", Application.isBatchMode);
        SessionState.SetBool(PendingKey + ".Passed", false);
        SessionState.SetBool(PendingKey + ".Starting", true);
        stableSince = EditorApplication.timeSinceStartup;
        EditorApplication.update -= BeginWhenReady;
        EditorApplication.update += BeginWhenReady;
    }

    private static void BeginWhenReady()
    {
        // executeMethod runs before all editor startup reloads are finished.
        // Enter Play Mode only after imports and compilation have settled.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            stableSince = EditorApplication.timeSinceStartup;
            return;
        }
        if (EditorApplication.timeSinceStartup - stableSince < 3) return;
        EditorApplication.update -= BeginWhenReady;
        SessionState.SetBool(PendingKey + ".Starting", false);
        phase = 0;
        running = false;
        deadline = EditorApplication.timeSinceStartup + 30;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void WaitForEditModeExit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(PendingKey + ".Finishing", false);
        EditorApplication.update -= WaitForEditModeExit;
        if (SessionState.GetBool(PendingKey + ".Batch", false))
            EditorApplication.Exit(SessionState.GetBool(PendingKey + ".Passed", false) ? 0 : 1);
    }

    private static void Tick()
    {
        if (running) return;
        bool expired = EditorApplication.timeSinceStartup > deadline;
        // isPlaying becomes true before scene Awake; wait for the actual arena lifecycle.
        if (!expired && (!Application.isPlaying || Time.frameCount < 3 ||
            Time.deltaTime <= 0f || phase == 0 && ArenaLayout.Instance == null)) return;
        running = true;
        try
        {
            if (expired)
            {
                ArenaLayout[] arenas = Object.FindObjectsByType<ArenaLayout>(FindObjectsInactive.Include);
                string detail = $"playing={Application.isPlaying}, frame={Time.frameCount}, delta={Time.deltaTime}, arenas={arenas.Length}, rawNull={ReferenceEquals(ArenaLayout.Instance, null)}";
                foreach (ArenaLayout arena in arenas)
                    detail += $"; {arena.name}: enabled={arena.enabled}, active={arena.gameObject.activeInHierarchy}, depth={Get(arena, "appliedDepth")}";
                throw new TimeoutException("Unity Play Mode checks timed out: " + detail);
            }
            if (phase == 0)
            {
                Check(Time.deltaTime > 0f, "Play Mode provides a positive deltaTime");
                RunScenarios();
                ClearCreated();
                SetUpTimedRespawn();
                waitFrame = Time.frameCount + 2;
                phase = 1;
                return;
            }
            if (phase == 1)
            {
                if (Time.frameCount < waitFrame) return;
                // Start subscriptions have run in the player loop by this frame.
                timedPlayer.GetComponent<Health>().TakeDamage(1000);
                timedEnemy.GetComponent<Health>().TakeDamage(1000);
                timedPlayerCarcass = timedPlayer.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                timedEnemyCarcass = timedEnemy.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                created.Add(timedPlayerCarcass.gameObject);
                created.Add(timedEnemyCarcass.gameObject);
                Check(timedPlayer.GetComponent<Health>().IsDead && timedEnemy.GetComponent<Health>().IsDead,
                    "Both timed respawn subjects died");
                resumeAt = EditorApplication.timeSinceStartup + 0.8;
                phase = 2;
                return;
            }
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            Check(!timedPlayer.GetComponent<Health>().IsDead && !timedEnemy.GetComponent<Health>().IsDead,
                "PlayerRespawn and TrainingMatch coroutines really revived both heroes");
            Check(timedPlayerCarcass != null && timedEnemyCarcass != null &&
                timedPlayerCarcass.IsAvailable && timedEnemyCarcass.IsAvailable,
                "Real coroutine revival leaves both independent carcasses on the ground");
            Check((int)Get(timedMatch, "kills") == 1 && (int)Get(timedMatch, "deaths") == 1,
                "TrainingMatch still counts one kill and one death");
            Debug.Log($"HOOKWARS_CARRYING_PASS: {assertions} assertions in Unity Play Mode.");
            SessionState.SetBool(PendingKey + ".Passed", true);
        }
        catch (Exception exception)
        {
            Debug.LogError("HOOKWARS_CARRYING_FAIL: " + exception);
            phase = -1;
        }
        finally
        {
            if (phase == 2 && EditorApplication.timeSinceStartup >= resumeAt ||
                SessionState.GetBool(PendingKey + ".Passed", false) || phase < 0)
            {
                ClearCreated();
                EditorApplication.update -= Tick;
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(PendingKey + ".Finishing", true);
                EditorApplication.update += WaitForEditModeExit;
                EditorApplication.isPlaying = false;
            }
            running = false;
        }
    }

    private static void RunScenarios()
    {
        assertions = 0;
        Check(SessionState.GetBool(PendingKey + ".Idempotent", false), "Repeated setup leaves scene bytes unchanged");
        // Stop the real arena during isolated checks. These changes are never saved.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Scene isolated = SceneManager.CreateScene("CarryingChecks (unsaved)");
        SceneManager.SetActiveScene(isolated);
        Check(EditorBuildSettings.scenes[0].path == CarryingSetup.ArenaPath &&
            EditorBuildSettings.scenes[0].enabled, "Arena is the enabled starting scene");
        int layer = LayerMask.NameToLayer("Carcasses");
        Check(layer >= 0, "Carcass layer exists");
        Carcass prefab = AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath);
        Check(prefab != null, "Carcass prefab configured");

        GameObject hero = Hero("Carrier A", new Vector3(-4, 0), prefab);
        GameObject rival = Hero("Carrier B", new Vector3(-4, 4), prefab);
        CarcassCarrier carrier = hero.GetComponent<CarcassCarrier>();
        CarcassCarrier other = rival.GetComponent<CarcassCarrier>();
        Carcass first = Item(prefab, new Vector3(-3.5f, 0));
        Carcass second = Item(prefab, new Vector3(-3.5f, 0));
        Invoke(carrier, "Update"); // Actual overlap-based proximity pickup.
        Check(carrier.Count == 1 && carrier.CurrentMassKg == 6 && carrier.CapacityKg == 10,
            "Walking pickup: one 6 kg carcass fits in 10 kg");
        Carcass ground = first.IsStored ? second : first;
        Check(ground.IsAvailable && !carrier.TryCollect(ground), "Whole second carcass remains on ground");
        Carcass stored = first.IsStored ? first : second;
        Check(!other.TryCollect(stored), "A stored carcass cannot be taken twice");

        PlayerProgression progression = hero.GetComponent<PlayerProgression>();
        progression.AddExperience(progression.ExperienceToNextLevel);
        Check(progression.UpgradePoints == 1, "One level gives one point");
        Check(carrier.TryUpgradeCapacity(progression) && carrier.CapacityKg == 12 && progression.UpgradePoints == 0,
            "Upgrade 10 to 12 spends exactly one point");
        Check(!carrier.TryUpgradeCapacity(progression) && carrier.CapacityKg == 12, "No free repeated upgrade");
        Check(carrier.TryCollect(ground) && carrier.CurrentMassKg == 12 && carrier.Count == 2, "Two whole carcasses fit in 12 kg");
        Check(!other.TryUpgradeCapacity(progression), "Cannot spend another hero's point");

        hero.transform.position = Vector3.zero; // Die midway across the river.
        Health health = hero.GetComponent<Health>();
        health.TakeDamage(1000);
        Carcass own = hero.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        created.Add(own.gameObject);
        Check(carrier.Count == 0 && carrier.CurrentMassKg == 0, "Death drops all cargo");
        Check(own != first && own != second && own.MassKg == 6, "Own carcass is a separate 6 kg object");
        Check(first.IsAvailable && second.IsAvailable && OnBank(first.transform.position) && OnBank(second.transform.position),
            "Dropped cargo keeps both identities and is accessible on land");
        health.TakeDamage(1000);
        Check(hero.GetComponent<HeroCarcassSpawner>().CurrentCarcass == own, "Repeated damage creates no duplicate death carcass");
        Check(!carrier.TryCollect(own), "Dead hero cannot collect");
        Vector3 oldPosition = own.transform.position;
        hero.transform.position = new Vector3(-6, 0);
        health.Revive();
        Check(own != null && own.transform.position == oldPosition && carrier.CapacityKg == 12,
            "Revival preserves carcass position and upgraded capacity");

        // Reserve a carcass with one hook and reject both pickup and a second captor.
        HookAbility hook = hero.GetComponent<HookAbility>();
        HookAbility rivalHook = rival.GetComponent<HookAbility>();
        own.GetComponent<HookVictim>().TryCapture(hook);
        Check(!other.TryCollect(own) && !own.GetComponent<HookVictim>().TryCapture(rivalHook),
            "Captured carcass is reserved against pickup and another hook");
        own.GetComponent<HookVictim>().Release(hook);

        // Isolate remaining physics targets for deterministic direct and return casts.
        first.gameObject.SetActive(false);
        second.gameObject.SetActive(false);
        own.gameObject.SetActive(false);
        rival.transform.position = new Vector3(6, 6);
        hero.transform.position = new Vector3(-4, 0);
        Carcass direct = Item(prefab, new Vector3(-3, 0));
        Throw(hook);
        Invoke(hook, "FlyHook");
        Check(hook.IsPullingTarget(direct.transform) && direct.IsReserved, "Outgoing hook captures a ground carcass");
        Finish(hook);
        Check(direct.IsStored && carrier.Count == 1, "Landed carcass enters cargo");

        Carcass returning = Item(prefab, new Vector3(-2, 0));
        Throw(hook);
        hook.ActiveHook.position = new Vector3(0, 0);
        Check((bool)Invoke(hook, "TryCatchOnReturn", new Vector3(-4, 0), 10f), "Return cast detects a ground carcass");
        Check(hook.IsPullingTarget(returning.transform), "Return hook captures a ground carcass");
        hook.ActiveHook.position = Vector3.zero;
        returning.transform.position = Vector3.zero;
        hook.CancelHook();
        Check(returning.IsAvailable && OnBank(returning.transform.position) && carrier.Count == 1,
            "Cancel frees carcass on land without adding it to cargo");
        returning.gameObject.SetActive(false);

        // Lethal outgoing hook keeps the dead hero locked and lands its separate carcass.
        rival.transform.position = new Vector3(-2, 0);
        Set(hook, "targetLayers", (LayerMask)(1 << rival.layer));
        Set(hook, "damage", 1000f);
        Throw(hook);
        Invoke(hook, "FlyHook");
        Carcass lethal = rival.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        if (lethal != null) created.Add(lethal.gameObject);
        Check(rival.GetComponent<Health>().IsDead && hook.IsPullingTarget(rival.transform),
            "Lethal outgoing hit still pulls dead hero");
        Check(lethal != null && !carrier.TryCollect(lethal) && lethal.IsReserved, "Dead hero carcass is reserved throughout pull");
        Finish(hook);
        Check(lethal.IsStored && carrier.CurrentMassKg == 12, "Lethal hook lands and stores separate carcass");
        rival.GetComponent<Health>().Revive();
        Check(lethal.IsStored, "Source hero revival cannot delete stored carcass");

        // Return lethal hit with a full carrier leaves the intact carcass near the thrower.
        rival.transform.position = new Vector3(-2, 0);
        Throw(hook);
        hook.ActiveHook.position = Vector3.zero;
        Check((bool)Invoke(hook, "TryCatchOnReturn", new Vector3(-4, 0), 10f), "Return hook can deal lethal damage");
        Carcass overflow = rival.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        if (overflow != null) created.Add(overflow.gameObject);
        Check(overflow != null && hook.IsPullingTarget(rival.transform), "Lethal return hit continues pulling");
        Finish(hook);
        Check(overflow.IsAvailable && overflow.MassKg == 6 && OnBank(overflow.transform.position) && carrier.CurrentMassKg == 12,
            "Full cargo leaves whole lethal carcass on land, mass unchanged");
        // Its source can revive even when the independent carcass is hooked over water.
        overflow.GetComponent<HookVictim>().TryCapture(hook);
        overflow.transform.position = Vector3.zero;
        rival.GetComponent<Health>().Revive();
        Check(overflow.transform.position == Vector3.zero && overflow.IsReserved,
            "Revival cannot teleport or unlock an independently hooked carcass");
        overflow.GetComponent<HookVictim>().Release(hook);
        overflow.gameObject.SetActive(false);
        rival.transform.position = new Vector3(6, 6);

        Carcass interrupted = Item(prefab, new Vector3(-2, 0));
        Throw(hook);
        Invoke(hook, "FlyHook");
        interrupted.transform.position = Vector3.zero;
        hook.ActiveHook.position = Vector3.zero;
        // Simulates the existing death control path: PlayerRespawn disables the hook.
        hook.enabled = false;
        Check(interrupted.IsAvailable && OnBank(interrupted.transform.position), "OnDisable cancels pull safely");
        hero.transform.position = Vector3.zero;
        health.TakeDamage(1000);
        Carcass finalOwn = hero.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
        if (finalOwn != null) created.Add(finalOwn.gameObject);
        Check(direct.IsAvailable && lethal.IsAvailable && carrier.Count == 0 && OnBank(lethal.transform.position),
            "Second death drops both carried carcasses exactly once");
        Check(direct.MassKg + lethal.MassKg + finalOwn.MassKg == 18, "Drop plus own death conserves expected mass");

        // Live capture is still a hero, never cargo, and cancellation restores its bank.
        direct.gameObject.SetActive(false);
        lethal.gameObject.SetActive(false);
        interrupted.gameObject.SetActive(false);
        finalOwn.gameObject.SetActive(false);
        hero.transform.position = new Vector3(-4, 0);
        health.Revive();
        hook.enabled = true;
        rival.transform.position = new Vector3(-2, 0);
        Set(hook, "damage", 1f);
        Throw(hook);
        Invoke(hook, "FlyHook");
        Check(!rival.GetComponent<Health>().IsDead && hook.IsPullingTarget(rival.transform), "Living hero still gets hooked");
        Finish(hook);
        Check(carrier.Count == 0 && OnBank(rival.transform.position), "Living hero lands without becoming cargo");
        rival.transform.position = new Vector3(6, 6);
        CheckRegressions(hero, hook);
        CheckLethalCargo(prefab);
        EconomyChecks.CheckStage(SessionState.GetInt("HookWars.EconomyStage", 0));
    }

    private static void CheckLethalCargo(Carcass prefab)
    {
        // Outgoing/returning lethal hits, cancellation, and death during a live capture.
        for (int scenario = 0; scenario < 4; scenario++)
        {
            GameObject killer = Hero("Cargo killer " + scenario, new Vector3(-6, -6), prefab);
            GameObject victim = Hero("Cargo victim " + scenario, new Vector3(6, -6), prefab);
            CarcassCarrier receiver = killer.GetComponent<CarcassCarrier>();
            CarcassCarrier source = victim.GetComponent<CarcassCarrier>();
            Set(source, "capacityKg", 12f);
            Set(receiver, "capacityKg", scenario == 0 ? 18f : 10f);
            Carcass first = Item(prefab, victim.transform.position);
            Carcass second = Item(prefab, victim.transform.position);
            Check(source.TryCollect(first) && source.TryCollect(second), "Victim carries two whole carcasses: " + scenario);
            HookAbility hook = killer.GetComponent<HookAbility>();
            Set(hook, "targetLayers", (LayerMask)(1 << victim.layer));
            Set(hook, "damage", scenario == 3 ? 1f : 1000f);
            Throw(hook);
            if (scenario == 1)
            {
                hook.ActiveHook.position = new Vector3(8, -6);
                Check((bool)Invoke(hook, "TryCatchOnReturn", killer.transform.position, 30f), "Return hook catches loaded victim");
            }
            else
            {
                Set(hook, "speed", 20f / Time.deltaTime);
                Invoke(hook, "FlyHook");
            }
            if (scenario == 3) victim.GetComponent<Health>().TakeDamage(1000);
            Carcass own = victim.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
            created.Add(own.gameObject);
            Check(source.Count == 0 && hook.IsPullingTarget(victim.transform) && first.IsReserved && second.IsReserved,
                "Whole death cargo is dropped and reserved with captured hero: " + scenario);
            Check(!receiver.TryCollect(first) && !receiver.TryCollect(second), "No pickup while death cargo is in flight: " + scenario);
            victim.transform.position = new Vector3(0, -6);
            Invoke(first, "LateUpdate");
            Invoke(second, "LateUpdate");
            Check(first.transform.position == victim.transform.position && second.transform.position == victim.transform.position,
                "Both dropped carcasses travel with dead hero across river: " + scenario);

            if (scenario == 2)
            {
                hook.CancelHook();
                Check(first.IsAvailable && second.IsAvailable && own.IsAvailable &&
                    OnBank(first.transform.position) && OnBank(second.transform.position) &&
                    receiver.Count == 0, "Cancelled group stays available on land without pickup");
            }
            else
            {
                Finish(hook);
                if (scenario == 0)
                    Check(own.IsStored && first.IsStored && second.IsStored && receiver.CurrentMassKg == 18,
                        "Lethal hook collects own carcass plus entire cargo when 18 kg fits");
                else
                    Check(own.IsStored && first.IsAvailable && second.IsAvailable &&
                        OnBank(first.transform.position) && first.transform.position.x < 0 &&
                        OnBank(second.transform.position) && second.transform.position.x < 0 &&
                        receiver.CurrentMassKg == 6,
                        "Overflow cargo lands next to killer instead of remaining on victim bank: " + scenario);
            }
            Vector3 firstPosition = first.transform.position;
            Vector3 secondPosition = second.transform.position;
            victim.GetComponent<Health>().Revive();
            Check(first.transform.position == firstPosition && second.transform.position == secondPosition &&
                first.MassKg + second.MassKg + own.MassKg == 18,
                "Revival preserves positions and all 18 kg from loaded victim: " + scenario);
            killer.SetActive(false);
            victim.SetActive(false);
            first.gameObject.SetActive(false);
            second.gameObject.SetActive(false);
            own.gameObject.SetActive(false);
        }
    }

    private static void ClearCreated()
    {
        foreach (GameObject target in created) if (target != null) Object.DestroyImmediate(target);
        created.Clear();
    }

    private static void SetUpTimedRespawn()
    {
        Carcass prefab = AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath);
        timedPlayer = Hero("Timed player", new Vector3(-6, 0), prefab, true);
        timedEnemy = Hero("Timed enemy", new Vector3(6, 0), prefab);
        Set(timedPlayer.GetComponent<CarcassCarrier>(), "carcassLayers", (LayerMask)0);
        Set(timedEnemy.GetComponent<CarcassCarrier>(), "carcassLayers", (LayerMask)0);
        PlayerRespawn respawn = timedPlayer.AddComponent<PlayerRespawn>();
        Set(respawn, "respawnDelay", 0.1f);
        GameObject manager = new GameObject("Timed match");
        created.Add(manager);
        timedMatch = manager.AddComponent<TrainingMatch>();
        Set(timedMatch, "playerHealth", timedPlayer.GetComponent<Health>());
        Set(timedMatch, "enemyHealth", timedEnemy.GetComponent<Health>());
        Set(timedMatch, "playerHook", timedPlayer.GetComponent<PlayerHook>());
        Set(timedMatch, "respawnDelay", 0.1f);
    }

    private static void CheckRegressions(GameObject hero, HookAbility hook)
    {
        PlayerMovement movement = hero.AddComponent<PlayerMovement>();
        hero.transform.position = new Vector3(-4, 0);
        Invoke(movement, "Move", new Vector2(100000, 100000));
        Check(ArenaLayout.IsInsideBank(hero.transform.position, false), "WASD movement helper stays on original bank at field edge");
        foreach (int depth in new[] { 4, 16, 20 })
        {
            ArenaLayout arena = ArenaLayout.Instance;
            Set(arena, "appliedDepth", depth);
            Vector3 clamped = ArenaLayout.ClampToBank(new Vector3(1000, 1000), true);
            Check(ArenaLayout.IsInsideBank(clamped, true) && clamped.x <= 2 + depth - 0.5f,
                "Dynamic field clamp at depth " + depth);
        }
        Set(ArenaLayout.Instance, "appliedDepth", 16);
        hero.transform.position = new Vector3(-4, 0);
        Set(hook, "targetLayers", (LayerMask)0);
        Set(hook, "obstacleLayers", (LayerMask)LayerMask.GetMask("Mountains"));
        GameObject wall = new GameObject("Regression obstacle");
        created.Add(wall);
        wall.layer = LayerMask.NameToLayer("Mountains");
        wall.transform.position = new Vector3(-2, 0);
        wall.AddComponent<BoxCollider2D>().size = Vector2.one;

        Set(hook, "maxBounces", 0);
        Throw(hook);
        Invoke(hook, "FlyHook");
        Check(!hook.IsFlyingOut && hook.ChainPointCount == 2, "Obstacle without bounce budget returns hook");
        hook.CancelHook();
        Set(hook, "maxBounces", 1);
        Throw(hook);
        Invoke(hook, "FlyHook");
        Check(hook.IsFlyingOut && hook.ChainPointCount == 3, "Obstacle with bounce budget creates a chain bend");
        Invoke(hook, "FlyHook");
        Check(hook.ActiveHook.position.x < -2.5f, "Ricochet reflects outgoing movement");

        hook.ActiveHook.position = new Vector3(100, 100);
        Invoke(hook, "TrimToChainLimit");
        float chainLength = 0f;
        for (int i = 1; i < hook.ChainPointCount; i++)
            chainLength += Vector3.Distance(hook.GetChainPoint(i - 1), hook.GetChainPoint(i));
        Check(chainLength <= hook.Range + 0.001f, "Full bent chain is bounded by hook range");
        Set(hook, "speed", 100f / Time.deltaTime);
        Invoke(hook, "ReturnHook");
        Check(hook.ActiveHook == null, "Return traverses saved bends and clears hook");

        wall.SetActive(false);
        Set(hook, "maxBounces", 0);
        Throw(hook);
        SpriteRenderer sprite = hook.ActiveHook.GetComponent<SpriteRenderer>();
        float size = Mathf.Max(sprite.sprite.bounds.size.x, sprite.sprite.bounds.size.y) * hook.ActiveHook.localScale.x;
        Check(Mathf.Abs(size - 1.5f) < 0.001f, "Initial hook visual size is preserved");
        Quaternion rotation = hook.ActiveHook.rotation;
        Invoke(hook, "FlyHook");
        Invoke(hook.ActiveHook.GetComponent<HookVisual>(), "LateUpdate");
        Check(Quaternion.Angle(hook.ActiveHook.rotation, rotation) < 0.01f, "Hook visual follows the original outgoing orientation");
        hook.CancelHook();
    }

    private static GameObject Hero(string name, Vector3 position, Carcass prefab, bool playerHook = false)
    {
        GameObject hero = new GameObject(name);
        created.Add(hero);
        hero.layer = LayerMask.NameToLayer("PlayerTarget");
        hero.transform.position = position;
        hero.AddComponent<CircleCollider2D>().radius = 0.5f;
        hero.AddComponent<Health>();
        hero.AddComponent<HookVictim>();
        hero.AddComponent<PlayerProgression>();
        CarcassCarrier carrier = hero.AddComponent<CarcassCarrier>();
        Set(carrier, "carcassLayers", (LayerMask)LayerMask.GetMask("Carcasses"));
        HeroCarcassSpawner spawner = hero.AddComponent<HeroCarcassSpawner>();
        Set(spawner, "carcassPrefab", prefab);
        HookAbility hook = playerHook ? hero.AddComponent<PlayerHook>() : hero.AddComponent<HookAbility>();
        Set(hook, "hookPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hook.prefab").transform);
        Set(hook, "carcassLayers", (LayerMask)LayerMask.GetMask("Carcasses"));
        return hero;
    }

    private static Carcass Item(Carcass prefab, Vector3 position)
    {
        Carcass item = Object.Instantiate(prefab, position, Quaternion.identity);
        created.Add(item.gameObject);
        item.Initialize(6);
        return item;
    }

    private static void Throw(HookAbility hook)
    {
        Set(hook, "nextThrowTime", 0f);
        Set(hook, "speed", 3f / Time.deltaTime);
        hook.TryThrow(Vector2.right);
        Check(hook.ActiveHook != null, "Hook instantiated");
        created.Add(hook.ActiveHook.gameObject);
    }

    private static void Finish(HookAbility hook)
    {
        // Exercise actual route/landing logic with a deterministic full-frame budget.
        Set(hook, "pullSpeed", 100f / Time.deltaTime);
        Invoke(hook, "PullTarget");
        Check(hook.ActiveHook == null, "Pull completes and clears hook");
    }

    private static bool OnBank(Vector3 point) => ArenaLayout.IsInsideBank(point, point.x > 0);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
        Debug.Log("CARRYING_CHECK: " + message);
    }
    private static object Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static void Set(object target, string field, object value) =>
        Field(target.GetType(), field).SetValue(target, value);
    private static object Get(object target, string field) => Field(target.GetType(), field).GetValue(target);
    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) ?? Field(type.BaseType, name);
}
