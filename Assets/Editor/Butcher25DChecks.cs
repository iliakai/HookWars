using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Butcher25DChecks
{
    public const string Key = "HookWars.ButcherChecks";
    private static int phase, count;
    private static double deadline, ready;
    private static float due;
    private static GameObject subjects;
    private static Health player, target, enemy;
    private static PlayerMelee axe;
    private static PlayerHook hook;
    private static EnemyMelee enemyAxe;
    private static HeroActionTiming25D timing;
    private static ButcherVisual25D[] previewActors;
    private static int previewIndex;
    private static ButcherVisual25D preview;
    private static Health scenePlayer, sceneEnemy;
    private static HookAbility sceneHook;
    private static Carcass firstCargo, secondCargo, killedOwn, playerOwn;
    private static Vector3 firstLanded, secondLanded;
    private static int staleHits, playerLife;
    static Butcher25DChecks()
    {
        if (SessionState.GetBool(Key, false)) Wait();
    }
    [MenuItem("HookWars/25D/Butcher/Run integration checks")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode");
        count = 0; SessionState.SetInt(Key + ".Count", 0); SessionState.SetBool(Key + ".Finishing", false);
        string settings = Hash("ProjectSettings/EditorBuildSettings.asset") + Hash("ProjectSettings/QualitySettings.asset");
        Butcher25DSetup.Configure(); string first = Signature();
        Directory.CreateDirectory("Temp/ButcherIdempotence");
        foreach (string file in Directory.GetFiles(Butcher25DSetup.Adapted)) File.Copy(file, "Temp/ButcherIdempotence/" + Path.GetFileName(file), true);
        Butcher25DSetup.Configure();
        foreach (string file in Directory.GetFiles(Butcher25DSetup.Adapted))
            if (Hash(file) != Hash("Temp/ButcherIdempotence/" + Path.GetFileName(file))) Debug.LogError("BUTCHER_IDEMPOTENCE_CHANGED: " + file);
        Check(first == Signature(), "Repeated butcher setup preserves scene and integration asset bytes");
        Check(settings == Hash("ProjectSettings/EditorBuildSettings.asset") + Hash("ProjectSettings/QualitySettings.asset"), "Butcher setup preserves the active build scene list and quality settings");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); Wait();
    }
    private static string Hash(string path)
    { using (var hash = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))); }
    private static string Signature() => Hash(Arena25DSetup.ScenePath) + string.Join("", Directory.GetFiles(Butcher25DSetup.Adapted).OrderBy(p => p, StringComparer.Ordinal).Select(Hash));
    private static void Wait()
    {
        count = SessionState.GetInt(Key + ".Count", 0); phase = SessionState.GetBool(Key + ".Finishing", false) ? 99 : 0;
        ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 120;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Butcher checks timed out, frame=" + Time.frameCount);
            if (phase == 0)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < ready) return;
                phase = 1; EditorApplication.isPaused = false; EditorApplication.isPlaying = true; return;
            }
            if (phase == 1)
            {
                if (!Application.isPlaying || Time.frameCount < 5) return;
                StructureAndRolling(); CreateSubjects();
                Invoke(axe, "TryAttack");
                Check(timing.AxePending && target.HealthFraction == 1, "Accepted Player axe has no instant damage");
                int sequence = timing.AxeSequence; Invoke(axe, "TryAttack");
                Check(timing.AxeSequence == sequence, "Repeated attack during cooldown creates no second action");
                Check(Mathf.Abs(timing.AxeSpeed - 0.8f / 0.7f) < 0.001f, "Player 0.7s cooldown accelerates 0.8s clip without changing cooldown");
                due = timing.AxeStarted + HeroActionTiming25D.AxeImpact / timing.AxeSpeed + 0.04f; phase = 2; return;
            }
            if (phase == 2)
            {
                if (Time.time < due) return;
                Check(Mathf.Abs(target.HealthFraction - 0.8f) < 0.001f && !timing.AxePending, "Real Player axe applies exactly one 20 damage hit at impact");
                timing.Advance(Time.time + 50); Check(Mathf.Abs(target.HealthFraction - 0.8f) < 0.001f, "Repeated timing advancement cannot duplicate damage");
                enemyAxe.enabled = true; Set(enemyAxe, "readyAt", 0f); Invoke(enemyAxe, "LateUpdate");
                var enemyTiming = enemy.GetComponent<HeroActionTiming25D>();
                Check(enemyTiming.AxePending && player.HealthFraction == 1, "Accepted Enemy axe also defers damage");
                due = enemyTiming.AxeStarted + 0.44f; phase = 3; return;
            }
            if (phase == 3)
            {
                if (Time.time < due) return;
                Check(Mathf.Abs(player.HealthFraction - 0.85f) < 0.001f, "Real Enemy axe applies exactly one 15 damage hit");
                enemyAxe.enabled = false;
                Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
                Check(timing.HookPending && hook.ActiveHook == null, "Accepted cast has no projectile before release");
                int sequence = timing.HookSequence; float cooldown = (float)Field(hook, "nextThrowTime").GetValue(hook);
                hook.TryThrow(Vector2.up);
                Check(sequence == timing.HookSequence && timing.HookDirection == Vector2.right && cooldown == (float)Field(hook, "nextThrowTime").GetValue(hook), "Windup fixes direction and accounts cooldown exactly once");
                due = timing.HookStarted + HeroActionTiming25D.HookRelease + 0.03f; phase = 4; return;
            }
            if (phase == 4)
            {
                if (Time.time < due) return;
                Check(hook.ActiveHook != null && !timing.HookPending, "Real delayed release creates one logical hook");
                Transform sameHook = hook.ActiveHook; timing.Advance(Time.time + 50); hook.TryThrow(Vector2.up);
                Check(hook.ActiveHook == sameHook, "Repeated release/input cannot create second projectile");
                hook.CancelHook(); CancellationAndUI();
                previewActors = Object.FindObjectsByType<ButcherVisual25D>().Where(d => d.Owner.GetComponent<PlayerMovement>() != null || d.Owner.GetComponent<EnemyChase>() != null)
                    .OrderBy(d => d.Owner.GetComponent<PlayerMovement>() != null ? 0 : 1).ToArray();
                previewIndex = 0; BeginLivePreview(); return;
            }
            if (phase == 5)
            {
                if (Time.time < due) return;
                Capture(preview, "live_axe_" + (previewIndex == 0 ? "Player" : "Enemy"));
                HookAbility source = preview.Owner.GetComponent<HookAbility>(); Set(source, "nextThrowTime", 0f); source.TryThrow(Vector2.down);
                var action = preview.Owner.GetComponent<HeroActionTiming25D>();
                Check(action.HookPending, "Live model accepts cast after axe while retaining existing command rules");
                due = action.HookStarted + HeroActionTiming25D.HookRelease + 0.16f; phase = 6; return;
            }
            if (phase == 6)
            {
                if (Time.time < due) return;
                Check(preview.Owner.GetComponent<HookAbility>().ActiveHook != null, "Live model releases hook on real player-loop frames");
                Capture(preview, "live_hook_" + (previewIndex == 0 ? "Player" : "Enemy"));
                preview.Owner.GetComponent<HookAbility>().CancelHook();
                due = Time.time + 0.14f; phase = 7; return;
            }
            if (phase == 7)
            {
                if (Time.time < due) return;
                Capture(preview, "live_idle_" + (previewIndex == 0 ? "Player" : "Enemy"));
                if (++previewIndex < previewActors.Length) { BeginLivePreview(); return; }
                BeginLethalCargo(); phase = 8; return;
            }
            if (phase == 8)
            {
                if (sceneEnemy.IsDead && sceneHook.ActiveHook == null && !scenePlayer.GetComponent<HeroActionTiming25D>().HookPending)
                {
                    killedOwn = sceneEnemy.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                    var carrier = scenePlayer.GetComponent<CarcassCarrier>();
                    Check(killedOwn != null && killedOwn.IsHeroCarcass && killedOwn.IsAvailable&&Mathf.Abs(killedOwn.MassKg-sceneEnemy.MaximumKg)<.001f, "Animated lethal hook lands the intact overweight hero corpse");
                    Check(firstCargo.IsStored && secondCargo.IsAvailable && firstCargo.MassKg + secondCargo.MassKg + killedOwn.MassKg == 112&&carrier.CurrentMassKg==6, "Animated lethal hook preserves all 112kg and stores only the cargo that fits");
                    var display = Object.FindAnyObjectByType<ArenaPresentation25D>();
                    Check(Meat25DChecks.SafeVisual(display.VisualFor(firstCargo)), "Dropped meat has no combat scripts or events");
                    firstLanded = firstCargo.transform.position; secondLanded = secondCargo.transform.position;
                    CaptureAt(display.VisualFor(firstCargo).position + Vector3.up * 0.3f, "live_carcass");
                    phase = 9; return;
                }
                if (Time.time > due) throw new TimeoutException("Lethal animated hook did not complete");
                return;
            }
            if (phase == 9)
            {
                if (sceneEnemy.IsDead) return;
                due = Time.time + 0.06f; phase = 10; return;
            }
            if (phase == 10)
            {
                if (Time.time < due) return;
                Check(firstCargo.transform.position == firstLanded && secondCargo.transform.position == secondLanded && killedOwn != null, "Real Enemy respawn leaves delivered corpse identities and positions intact");
                var revived = Object.FindObjectsByType<ButcherVisual25D>().Single(d => d.Owner == sceneEnemy);
                Check(revived.gameObject.activeInHierarchy && !sceneEnemy.GetComponent<HeroActionTiming25D>().HookPending, "Enemy respawn restores visual and has no old cast");
                var actions = scenePlayer.GetComponent<HeroActionTiming25D>(); playerLife = actions.LifeSequence; staleHits = 0;
                sceneHook.CancelHook(); Check(actions.BeginAxe(0.7f, () => staleHits++), "Real Player death test starts pending axe");
                Set(sceneHook, "nextThrowTime", 0f); sceneHook.TryThrow(Vector2.right);
                Check(actions.HookPending, "Real Player death test starts pending cast");
                Set(scenePlayer.GetComponent<PlayerRespawn>(), "respawnDelay", 0.1f);
                scenePlayer.TakeDamage(1000); playerOwn = scenePlayer.GetComponent<HeroCarcassSpawner>().CurrentCarcass;
                due = Time.time + 0.5f; phase = 11; return;
            }
            if (phase == 11)
            {
                if (Time.time < due) return;
                var actions = scenePlayer.GetComponent<HeroActionTiming25D>();
                Check(!scenePlayer.IsDead && actions.LifeSequence > playerLife && !actions.AxePending && !actions.HookPending && sceneHook.ActiveHook == null && staleHits == 0, "Real PlayerRespawn restores life and cancels all previous-life delayed actions");
                Check(playerOwn != null && playerOwn.IsAvailable, "Real Player revival leaves its separate static carcass");
                var revived = Object.FindObjectsByType<ButcherVisual25D>().Single(d => d.Owner == scenePlayer);
                Check(revived.gameObject.activeInHierarchy && revived.Animator.applyRootMotion == false, "Revived Player restores animation and logical separation");
                Debug.Log("HOOKWARS_BUTCHER_PASS: " + count + " assertions in Unity Play Mode; live-frame previews and real respawns checked");
                SessionState.SetBool(Key + ".Passed", true); Stop(); return;
            }
            if (phase == 99 && !EditorApplication.isPlayingOrWillChangePlaymode)
            { SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finishing", false); EditorApplication.update -= Tick; }
        }
        catch (Exception error) { Debug.LogError("HOOKWARS_BUTCHER_FAIL: " + error); Stop(); }
    }
    private static void BeginLethalCargo()
    {
        scenePlayer = Object.FindAnyObjectByType<PlayerMovement>().GetComponent<Health>();
        sceneEnemy = Object.FindAnyObjectByType<EnemyChase>().GetComponent<Health>();
        scenePlayer.transform.position = new Vector3(-8, -5); sceneEnemy.transform.position = new Vector3(-6, -5);
        var carrier = sceneEnemy.GetComponent<CarcassCarrier>(); Set(carrier, "capacityKg", 12f);
        Set(scenePlayer.GetComponent<CarcassCarrier>(), "capacityKg", 10f);
        Carcass prefab = AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath);
        firstCargo = Object.Instantiate(prefab, sceneEnemy.transform.position, Quaternion.identity);
        secondCargo = Object.Instantiate(prefab, sceneEnemy.transform.position, Quaternion.identity);
        firstCargo.Initialize(6, sceneEnemy.GetComponent<HookVictim>()); secondCargo.Initialize(6, sceneEnemy.GetComponent<HookVictim>());
        Check(carrier.TryCollect(firstCargo) && carrier.TryCollect(secondCargo), "Real Enemy loads two whole carcasses before lethal hook");
        sceneHook = scenePlayer.GetComponent<PlayerHook>(); Set(sceneHook, "damage", 1000f); Set(sceneHook, "nextThrowTime", 0f);
        scenePlayer.GetComponent<HeroActionTiming25D>().CancelAll(); sceneEnemy.GetComponent<HeroActionTiming25D>().CancelAll();
        Physics2D.SyncTransforms(); sceneHook.TryThrow(Vector2.right);
        due = Time.time + 5;
    }
    private static void BeginLivePreview()
    {
        preview = previewActors[previewIndex];
        PlayerMovement movingPlayer = Object.FindAnyObjectByType<PlayerMovement>(); movingPlayer.enabled = false;
        PlayerAim aim = movingPlayer.GetComponentInChildren<PlayerAim>(); aim.enabled = false;
        Set(aim, "<AimDirection>k__BackingField", Vector2.down);
        if (previewIndex > 0) movingPlayer.transform.position = preview.Owner.transform.position + Vector3.down * 8;
        var action = preview.Owner.GetComponent<HeroActionTiming25D>(); action.CancelAll();
        Check(action.BeginAxe(previewIndex == 0 ? 0.7f : 1.1f, () => { }), "Live-frame axe preview accepted");
        due = action.AxeStarted + HeroActionTiming25D.AxeImpact / action.AxeSpeed + 0.03f; phase = 5;
    }
    private static void Stop()
    {
        if (subjects != null) Object.DestroyImmediate(subjects);
        SessionState.SetBool(Key + ".Finishing", true); phase = 99; EditorApplication.isPlaying = false;
    }
    private static void StructureAndRolling()
    {
        // Controlled combat fixtures; random map generation has its own checks.
        Object.FindAnyObjectByType<MatchMap25D>()?.MapRoot.gameObject.SetActive(false);
        foreach (EnemyChase ai in Object.FindObjectsByType<EnemyChase>()) ai.enabled = false;
        foreach (EnemyHookAI ai in Object.FindObjectsByType<EnemyHookAI>()) ai.enabled = false;
        foreach (EnemyMelee ai in Object.FindObjectsByType<EnemyMelee>()) ai.enabled = false;
        Check(Object.FindObjectsByType<Collider>().All(c=>c.gameObject.scene.name=="Arena25D chain physics"&&(c.gameObject.layer==24||c.gameObject.layer==25))&&
            Object.FindObjectsByType<Rigidbody>().All(b=>b.gameObject.scene.name=="Arena25D chain physics"),"All 3D physics is isolated to the chain world");
        Check(Object.FindObjectsByType<MonoBehaviour>().All(m => m == null || !new[] { "ButcherDemoInput", "ButcherAnimationDriver", "PhysicalHookChain" }.Contains(m.GetType().Name)), "No demo drivers attached");
        foreach (string name in Butcher25DSetup.ClipNames)
        {
            AnimationClip clip = Butcher25DSetup.Clip(name);
            float length = name == "Axe_Attack" ? 0.8f : name == "Hook_Cast" ? 0.6f : 1;
            Check(Mathf.Abs(clip.length - length) < 0.001f && clip.frameRate == 30 && clip.events.Length == 0, "Imported clip timing/events: " + name);
        }
        var drivers = Object.FindObjectsByType<ButcherVisual25D>(); Check(drivers.Length == 2, "Player and Enemy both have adapted model");
        foreach (ButcherVisual25D driver in drivers)
        {
            Transform view = driver.transform.parent;
            Check(driver.GetComponentsInChildren<Collider>(true).Length==0&&driver.GetComponentsInChildren<Rigidbody>(true).Length==0,"Butcher visual never introduces gameplay 3D physics");
            Vector3 savedPosition = view.position; Quaternion savedRotation = view.rotation;
            Check(!driver.Animator.applyRootMotion && driver.Animator.runtimeAnimatorController != null && driver.Animator.layerCount == 4, "Generic model has four animation layers and no root motion");
            Check(driver.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit")), "All butcher materials mapped to URP Lit");
            Check(driver.transform.Find("RigVisual/ButcherRig/Root/AxePivot/AxeHit") != null && driver.transform.Find("RigVisual/ButcherRig/Root/ChainArm/HookSocket") == driver.Socket, "Real weapon bone paths preserved");
            view.rotation = Quaternion.identity; driver.ResetPose(); driver.Tick(0.02f);
            Vector3 logicalPosition = driver.Owner.transform.position; Quaternion logicalRotation = driver.Owner.transform.rotation;
            Transform head = driver.transform.Find("RigVisual/ButcherRig/Root/Head"); Quaternion headRotation = head.rotation;
            Quaternion before = driver.SkinOrientation.rotation;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4; view.position += new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 0.05f;
                driver.Tick(0.02f);
                Check(driver.DirectionIndex == i, "Actual local travel chooses Roll octant " + i);
                Check(Mathf.Abs(driver.RollRate - 2.5f / (2f * Mathf.PI * driver.EffectiveRadius)) < 0.002f, "Roll rate follows distance and effective scaled radius");
                float step = Quaternion.Angle(before, driver.SkinOrientation.rotation);
                Check(step > 0.01f && step < 20, "Changing authored roll direction preserves continuous nonzero rotation");
                (driver.SkinOrientation.rotation * Quaternion.Inverse(before)).ToAngleAxis(out float degrees, out Vector3 axis);
                Vector3 rollAxis = Vector3.Cross(Vector3.up, new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)));
                Check(Vector3.Dot(axis.normalized, rollAxis.normalized) > 0.9f, "Authored clip rotates forward rather than backwards: octant=" + i + " delta=" + degrees + " axis=" + axis.ToString("F3") + " dot=" + Vector3.Dot(axis.normalized, rollAxis.normalized));
                Check(Quaternion.Angle(headRotation, head.rotation) < 15 && Vector3.Distance(driver.BodyRoll.localScale, Vector3.one) < 0.001f, "Head does not tumble and ball scale does not deform");
                before = driver.SkinOrientation.rotation;
                Capture(driver, "roll_" + i + "_" + view.name.Replace(" ", "_"));
            }
            driver.Tick(0.02f);
            Check(driver.RollRate == 0 && Quaternion.Angle(before, driver.SkinOrientation.rotation) < 0.03f, "No displacement freezes reached ball orientation");
            view.rotation = Quaternion.Euler(0, 90, 0); driver.Tick(0.02f);
            Check(Quaternion.Angle(before, driver.SkinOrientation.rotation) < 0.03f, "Aiming while stationary does not spin the ball");
            view.position += Vector3.right * 0.05f; driver.Tick(0.02f);
            Check(driver.DirectionIndex == 0, "Facing world +X and moving +X selects local N");
            view.position += Vector3.right * 20; driver.Tick(0.02f);
            Check(driver.RollRate == 0, "Teleport cannot cause extreme rolling");
            view.position = savedPosition; view.rotation = savedRotation; driver.ResetPose(); driver.Tick(0.02f);
            Check(driver.Owner.transform.position == logicalPosition && driver.Owner.transform.rotation == logicalRotation, "Animation never changes logical root/collider transform");
            var skin = (SkinnedMeshRenderer)Field(driver, "bodyMesh").GetValue(driver);
            int boneIndex = Array.IndexOf(skin.bones, driver.SkinOrientation);
            Matrix4x4 bind = skin.sharedMesh.bindposes[boneIndex];
            foreach (int octant in Enumerable.Range(0, 8))
            {
                for (int frame = 0; frame < 20; frame++)
                {
                    float angle = octant * Mathf.PI / 4;
                    view.position += new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle)) * .04f;
                    view.rotation = Quaternion.Euler(0, frame * 13, 0);
                    driver.Tick(.02f);
                    Vector3 center = driver.SkinOrientation.position;
                    Check(Mathf.Abs(center.y - view.position.y - driver.EffectiveRadius - .015f) < .0002f,
                        "Ball center stays at constant height through roll and independent aim");
                    float bottom = skin.sharedMesh.vertices.Min(v => driver.SkinOrientation.TransformPoint(bind.MultiplyPoint3x4(v)).y);
                    Check(bottom >= view.position.y + .014f && bottom < view.position.y + .022f,
                        "Derived spherical mesh maintains ground contact without per-frame correction");
                }
            }
            view.position = savedPosition; view.rotation = savedRotation; driver.ResetPose(); driver.Tick(.02f);
            ActionPictures(driver);
        }
        CheckWallStop(drivers.Single(d => d.Owner.GetComponent<PlayerMovement>() != null));
        GameObject corpse = AssetDatabase.LoadAssetAtPath<GameObject>(Meat25DSetup.HeroPrefab)??AssetDatabase.LoadAssetAtPath<GameObject>(Butcher25DSetup.CarcassPrefab);
        Check(Meat25DChecks.SafeVisual(corpse.transform)&&corpse.GetComponentsInChildren<Collider>(true).Length==0,"Corpse prefab contains visual animation only, without combat logic");
        Bounds projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Butcher25DSetup.HookPrefab).GetComponent<MeshFilter>().sharedMesh.bounds;
        Check(projectile.size.z > projectile.size.y && Mathf.Abs(projectile.size.z - 0.45f) < 0.001f, "HookProjectile FBX static adaptation points forward +Z at 0.45m");
    }
    private static void CheckWallStop(ButcherVisual25D driver)
    {
        PlayerMovement movement = driver.Owner.GetComponent<PlayerMovement>(); Vector3 saved = movement.transform.position;
        var wall = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BoneWall>(EconomySetup.WallPath), new Vector3(-6.5f, -5.5f), Quaternion.identity);
        var display = Object.FindAnyObjectByType<ArenaPresentation25D>();
        try
        {
            movement.transform.position = new Vector3(-7.5f, -5.5f); Physics2D.SyncTransforms();
            Invoke(display, "LateUpdate"); driver.ResetPose(); driver.Tick(0.02f);
            Vector3 before = movement.transform.position; Invoke(movement, "Move", Vector2.right); Invoke(display, "LateUpdate"); driver.Tick(0.02f);
            Check(Vector3.Distance(before, movement.transform.position) < 0.001f && driver.RollRate == 0, "Real Physics2D wall blocks movement and stops rolling despite movement command");
        }
        finally { Object.DestroyImmediate(wall.gameObject); movement.transform.position = saved; Physics2D.SyncTransforms(); Invoke(display, "LateUpdate"); driver.ResetPose(); driver.Tick(0.02f); }
    }
    private static void CreateSubjects()
    {
        subjects = new GameObject("Butcher integration subjects (unsaved)");
        player = Subject("Timed Player", new Vector3(-12, 8)); target = Subject("Timed target", new Vector3(-11, 8));
        var aim = new GameObject("Aim").AddComponent<PlayerAim>(); aim.transform.SetParent(player.transform, false); aim.enabled = false;
        hook = player.gameObject.AddComponent<PlayerHook>();
        Set(hook, "hookPrefab", AssetDatabase.LoadAssetAtPath<Transform>("Assets/Prefabs/Hook.prefab"));
        if (Field(hook, "hookPrefab").GetValue(hook) == null)
            Set(hook, "hookPrefab", Field(Object.FindObjectsByType<PlayerHook>().First(h => h != hook), "hookPrefab").GetValue(Object.FindObjectsByType<PlayerHook>().First(h => h != hook)));
        Set(hook, "playerAim", aim); Set(hook, "targetLayers", (LayerMask)0); Set(hook, "obstacleLayers", (LayerMask)0); Set(hook, "carcassLayers", (LayerMask)0); Set(hook, "speed", 0.1f);
        axe = player.gameObject.AddComponent<PlayerMelee>(); Set(axe, "playerAim", aim); Set(axe, "targetLayers", (LayerMask)1);
        timing = player.gameObject.AddComponent<HeroActionTiming25D>();
        enemy = Subject("Timed Enemy", new Vector3(-12, 6.8f)); enemy.gameObject.AddComponent<HookAbility>();
        enemy.gameObject.AddComponent<HeroActionTiming25D>(); enemyAxe = enemy.gameObject.AddComponent<EnemyMelee>();
        enemyAxe.enabled = false; Set(enemyAxe, "playerHealth", player); Set(enemyAxe, "playerHook", hook);
        Physics2D.SyncTransforms();
    }
    private static Health Subject(string name, Vector3 position)
    {
        var obj = new GameObject(name); obj.transform.SetParent(subjects.transform); obj.transform.position = position;
        obj.AddComponent<CircleCollider2D>(); var health = obj.AddComponent<Health>(); obj.AddComponent<HookVictim>(); return health;
    }
    private static void CancellationAndUI()
    {
        target.Revive(); Set(axe, "nextAttackTime", 0f); Invoke(axe, "TryAttack"); Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
        Check(timing.AxePending && timing.HookPending, "Existing admission rules allow independent pending arm actions");
        player.TakeDamage(1000); player.Revive(); timing.Advance(Time.time + 100);
        Check(!timing.AxePending && !timing.HookPending && hook.ActiveHook == null && target.HealthFraction == 1, "Death and revival cannot execute old delayed hit or throw");
        Set(axe, "nextAttackTime", 0f); Invoke(axe, "TryAttack"); Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
        var victim = player.GetComponent<HookVictim>(); HookAbility captor = enemy.GetComponent<HookAbility>(); victim.TryCapture(captor); victim.Release(captor);
        timing.Advance(Time.time + 100);
        Check(hook.ActiveHook == null && target.HealthFraction == 1, "Capture cancels actions even if released before the original deadline");
        Set(axe, "nextAttackTime", 0f); Invoke(axe, "TryAttack"); target.transform.position += Vector3.right * 8;
        Physics2D.SyncTransforms(); timing.Advance(Time.time + 100);
        Check(target.HealthFraction == 1, "Hit queries target availability and range at impact");
        Vector2 ui = new Vector2(30, Screen.height - 30);
        Check(GamePointer.OverInterface(ui), "UI screen position is blocked");
        int axeSequence = timing.AxeSequence, hookSequence = timing.HookSequence;
        Set(axe, "nextAttackTime", 0f); Set(hook, "nextThrowTime", 0f);
        axe.TryAttackAtScreenPoint(ui); hook.TryThrowAtScreenPoint(ui);
        Check(axeSequence == timing.AxeSequence && hookSequence == timing.HookSequence, "Actual click command handlers reject UI positions without actions");
    }
    private static void ActionPictures(ButcherVisual25D driver)
    {
        HeroActionTiming25D actions = driver.Owner.GetComponent<HeroActionTiming25D>();
        HookAbility ownedHook = driver.Owner.GetComponent<HookAbility>(); Transform view = driver.transform.parent;
        Quaternion facing = view.rotation; view.rotation = Quaternion.Euler(0, 180, 0); driver.ResetPose(); driver.Tick(0.02f);
        Capture(driver, "front_" + view.name.Replace(" ", "_"));
        Check(actions.BeginAxe(driver.Owner.GetComponent<PlayerMovement>() != null ? 0.7f : 1.1f, () => { }), "Visual axe starts from accepted action");
        Set(actions, "<AxeStarted>k__BackingField", Time.time - HeroActionTiming25D.AxeImpact / actions.AxeSpeed);
        driver.Tick(0.02f);
        Check(Mathf.Abs(Mathf.Repeat(driver.Animator.GetCurrentAnimatorStateInfo(2).normalizedTime, 1) - 0.5f) < 0.01f, "Axe animation evaluates impact pose at synchronized deadline");
        CheckActionPose(driver, "Axe_Attack", "AxePivot", 0.4f);
        Debug.Log("BUTCHER_POSE: axe impact blade bounds=" + driver.transform.Find("RigVisual/06_Axe_Rectangular_Blade").GetComponent<Renderer>().bounds + " pivot=" + driver.transform.Find("RigVisual/ButcherRig/Root/AxePivot").position);
        Capture(driver, "axe_impact_" + view.name.Replace(" ", "_"));
        actions.CancelAll(); driver.Tick(0.1f);
        Set(ownedHook, "nextThrowTime", 0f); ownedHook.TryThrow(Vector2.down);
        Check(actions.HookPending, "Model cast starts from accepted logical command");
        Set(actions, "<HookStarted>k__BackingField", Time.time - HeroActionTiming25D.HookRelease);
        actions.Advance(Time.time); driver.Tick(0.02f);
        CheckActionPose(driver, "Hook_Cast", "ChainArm", HeroActionTiming25D.HookRelease);
        Debug.Log("BUTCHER_POSE: hook socket=" + driver.Socket.position + " local=" + driver.Socket.localPosition + " rigScale=" + driver.transform.Find("RigVisual/ButcherRig").lossyScale);
        var presentation = Object.FindAnyObjectByType<ArenaPresentation25D>();
        Transform projectile = presentation.VisualFor(ownedHook.ActiveHook);
        Check(Vector3.Distance(projectile.position, driver.Socket.position) < 0.002f, "Released projectile starts visually at HookSocket without moving logical origin");
        Check(ownedHook.ActiveHook.position == ownedHook.transform.position, "Socket offset adds no logical range or hit-point offset");
        Check(!driver.transform.Find("RigVisual/04_Hook_Metal").GetComponent<Renderer>().enabled, "Resting hook hides while projectile active");
        Capture(driver, "hook_release_" + view.name.Replace(" ", "_"));
        ownedHook.ActiveHook.position += Vector3.down * 1.5f;
        presentation.UpdateHookPresentation(ownedHook, driver); Capture(driver, "hook_flight_" + view.name.Replace(" ", "_"));
        ownedHook.CancelHook(); driver.Tick(0.1f);
        Check(driver.transform.Find("RigVisual/04_Hook_Metal").GetComponent<Renderer>().enabled && !projectile.gameObject.activeSelf, "Cancel restores resting hook and hides flying visual immediately");
        view.rotation = facing; actions.CancelAll(); driver.ResetPose(); driver.Tick(0.02f);
    }
    private static void CheckActionPose(ButcherVisual25D driver, string clip, string bone, float time)
    {
        GameObject reference = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Butcher25DSetup.ModelPath));
        try
        {
            Butcher25DSetup.Clip(clip).SampleAnimation(reference, time);
            Transform expected = reference.GetComponentsInChildren<Transform>(true).Single(t => t.name == bone);
            Transform actual = driver.GetComponentsInChildren<Transform>(true).Single(t => t.name == bone);
            float error = Quaternion.Angle(expected.localRotation, actual.localRotation);
            Check(error < 0.3f, "Masked action reaches authored pose: " + clip + " error=" + error + " actual=" + actual.localEulerAngles + " expected=" + expected.localEulerAngles);
        }
        finally { Object.DestroyImmediate(reference); }
    }
    public static void Capture(ButcherVisual25D driver, string name)
        => CaptureAt(driver.transform.position + Vector3.up * 0.75f, name);
    public static void CaptureAt(Vector3 focus, string name, float framingSize=1.35f)
    {
        Camera camera = Camera.main; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float size = camera.orthographicSize, aspect = camera.aspect; RenderTexture old = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32); Texture2D image = null;
        try
        {
            camera.transform.position = focus + new Vector3(2.7f, 2.5f, -4.5f); camera.transform.LookAt(focus); camera.orthographicSize = framingSize; camera.aspect = 1;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply();
            Directory.CreateDirectory("Temp/ButcherPreview"); File.WriteAllBytes("Temp/ButcherPreview/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; camera.aspect = aspect;
            RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); if (image != null) Object.DestroyImmediate(image);
        }
    }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static void Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    private static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); count++; SessionState.SetInt(Key + ".Count", count); Debug.Log("BUTCHER_CHECK: " + message); }
}
