using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EconomyChecks
{
    private static int assertions;
    [MenuItem("HookWars/Economy/Check all stages")]
    public static void Run5()
    {
        EconomySetup.Stage5();
        string before = SetupSignature();
        EconomySetup.Stage5();
        if (before != SetupSignature()) throw new InvalidOperationException("Repeated full economy setup changed saved assets");
        Debug.Log("ECONOMY_SETUP_IDEMPOTENT: scene and all prefabs/animations unchanged after repeat");
        SessionState.SetInt("HookWars.EconomyStage", 5);
        CarryingChecks.Run();
    }
    private static string SetupSignature()
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
        using (var bytes = new System.IO.MemoryStream())
        {
            foreach (string path in new[] { CarryingSetup.ArenaPath, EconomySetup.FactoryPath, EconomySetup.ShopPath,
                EconomySetup.FarmPath, EconomySetup.WallPath, "Assets/Prefabs/sheep.prefab", "Assets/Prefabs/cow.prefab", "Assets/Prefabs/pig.prefab" })
            {
                byte[] file = System.IO.File.ReadAllBytes(path); bytes.Write(file, 0, file.Length);
            }
            return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
        }
    }
    [MenuItem("HookWars/Economy/Check stage 4")]
    public static void Run4()
    {
        EconomySetup.Stage4();
        SessionState.SetInt("HookWars.EconomyStage", 4);
        CarryingChecks.Run();
    }
    [MenuItem("HookWars/Economy/Check stage 3")]
    public static void Run3()
    {
        EconomySetup.Stage3();
        SessionState.SetInt("HookWars.EconomyStage", 3);
        CarryingChecks.Run();
    }
    [MenuItem("HookWars/Economy/Check stage 2")]
    public static void Run2()
    {
        EconomySetup.Stage2();
        SessionState.SetInt("HookWars.EconomyStage", 2);
        CarryingChecks.Run();
    }
    public static void CheckStage(int stage)
    {
        if (stage < 2) return;
        assertions = 0;
        GameObject root = new GameObject("Economy checks (unsaved)");
        try
        {
            GameObject hero = new GameObject("Economy owner"); hero.transform.SetParent(root.transform);
            hero.transform.position = new Vector3(-8, 8);
            hero.AddComponent<Health>(); hero.AddComponent<HookVictim>();
            CarcassCarrier carrier = hero.AddComponent<CarcassCarrier>();
            Set(carrier, "capacityKg", 18f);
            OwnerEconomy wallet = hero.AddComponent<OwnerEconomy>();
            PlayerProgression owner = hero.GetComponent<PlayerProgression>();
            Check(owner.Gold == 0 && owner.ExperiencePerSecond == 0 && owner.GoldPerSecond == 2, "Target starting income");
            SausageFactory prefab = AssetDatabase.LoadAssetAtPath<SausageFactory>(EconomySetup.FactoryPath);
            SausageFactory factory = Object.Instantiate(prefab, new Vector3(-10, 8), Quaternion.identity, root.transform);
            Check(factory.Initialize(owner) && !prefab.CanBuild(owner), "One factory per owner");
            for (int i = 0; i < 3; i++)
            {
                Carcass carcass = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath));
                carcass.transform.SetParent(root.transform);
                Check(carrier.TryCollect(carcass), "Load full 18 kg");
            }
            Check(carrier.DeliverTo(factory) == 12 && factory.RawKg == 12 && carrier.CurrentMassKg == 6,
                "Whole carcass delivery conserves mass at full capacity");
            Check(owner.Level == 2 && Math.Abs(owner.Experience - 4) < 1e-8, "Experience awarded only for accepted 12 kg");
            Check(carrier.DeliverTo(factory) == 0 && factory.RawKg == 12 && owner.Experience == 4, "Full factory leaves cargo and grants no extra experience");
            factory.Advance(12);
            Check(Near(factory.RawKg, 6) && Near(wallet.ProductKg, 6) && wallet.Bones == 3, "Processing kg and bone coefficients");
            hero.transform.position = factory.transform.position + Vector3.right;
            typeof(SausageFactory).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(factory, null);
            Check(carrier.Count == 0 && factory.RawKg > 11 && factory.RawKg <= 12, "Automatic retry while near after capacity is freed");
            double xp = owner.Experience;
            factory.Advance(24);
            Check(Near(wallet.ProductKg, 18) && wallet.Bones == 9 && factory.RawKg == 0 && owner.Experience == xp, "No processing XP; full mass balance");
            for (int i = 0; i < 10; i++) wallet.AddOutput(0, 0.05);
            Check(wallet.Bones == 9 && Near(wallet.BoneRemainder, 0.5), "Fractional bone output retained");
            GameObject other = new GameObject("Other owner"); other.transform.SetParent(root.transform);
            Check(other.AddComponent<OwnerEconomy>().ProductKg == 0, "Owner resources isolated");
            owner.AddGold(1000);
            double beforeGold = owner.Gold;
            PlayerBuilding builder = hero.AddComponent<PlayerBuilding>();
            builder.SelectBuilding(prefab);
            Check(builder.TryBuildAt(new Vector3(-12.5f, 8.5f)) == null && owner.Gold == beforeGold,
                "Repeated factory construction does not spend money");
            Check(factory.TryUpgrade(0) && factory.TryUpgrade(1) && factory.TryUpgrade(2) &&
                Near(factory.CapacityKg, 24) && Near(factory.SecondsPerKg, 1.6) && factory.ExperiencePerKg == 3,
                "Three independent upgrades");
            Carcass future = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Carcass>(CarryingSetup.PrefabPath));
            Check(carrier.TryCollect(future) && carrier.DeliverTo(factory) == 6 && owner.Level == 3 && Near(owner.Experience, 9),
                "New experience coefficient applies only to future delivery");
            factory.Advance(factory.SecondsPerKg / 2.0);
            double partial = factory.RawKg;
            Check(factory.TryUpgrade(0) && Near(factory.RawKg, partial), "Speed upgrade preserves partially processed carcass");
            double productBefore = wallet.ProductKg;
            factory.Advance(factory.SecondsPerKg / 2.0);
            Check(Near(wallet.ProductKg - productBefore, 0.5), "New processing time applies without resetting progress");
            hero.GetComponent<Health>().TakeDamage(1000);
            hero.GetComponent<Health>().Revive();
            Check(Near(wallet.ProductKg, 19) && wallet.Bones == 10 && owner.Gold == beforeGold - 225,
                "Death preserves owner and building resources");
            if (stage >= 3) CheckShops(root);
            if (stage >= 4) CheckAnimals(root);
            if (stage >= 5) CheckWalls(root);
            Debug.Log($"HOOKWARS_ECONOMY_STAGE_{stage}_PASS: {assertions} assertions.");
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static void CheckWalls(GameObject root)
    {
        GameObject defender = new GameObject("Wall owner"); defender.transform.SetParent(root.transform);
        defender.AddComponent<OwnerEconomy>().AddOutput(0, 100);
        PlayerProgression owner = defender.GetComponent<PlayerProgression>(); owner.AddGold(1000);
        GameObject attacker = new GameObject("Wall attacker"); attacker.transform.SetParent(root.transform);
        attacker.transform.position = new Vector3(-6, -2);
        attacker.AddComponent<Health>(); attacker.AddComponent<HookVictim>(); attacker.AddComponent<OwnerEconomy>();
        PlayerProgression enemy = attacker.GetComponent<PlayerProgression>();
        HookAbility hook = attacker.AddComponent<HookAbility>();
        Set(hook, "hookPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hook.prefab").transform);
        Set(hook, "obstacleLayers", (LayerMask)LayerMask.GetMask("Buildings"));
        Set(hook, "speed", 4f / Time.deltaTime); Set(hook, "damage", 40f);
        BoneWall prefab = AssetDatabase.LoadAssetAtPath<BoneWall>(EconomySetup.WallPath);
        for (int mode = 0; mode < 5; mode++)
        {
            BoneWall wall = Object.Instantiate(prefab, new Vector3(-4, -2), Quaternion.identity, root.transform);
            wall.Initialize(mode == 4 ? enemy : owner);
            Set(hook, "maxBounces", mode == 0 || mode == 2 ? 0 : 1);
            Set(hook, "damage", mode == 2 || mode == 3 ? 1000f : 40f);
            Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
            Invoke(hook, "FlyHook");
            if (mode == 0)
                Check(wall.CurrentHealth == 60 && !hook.IsFlyingOut, "Surviving enemy wall takes damage then returns hook without bounces");
            if (mode == 1)
            {
                Check(wall.CurrentHealth == 60 && hook.IsFlyingOut && hook.ChainPointCount == 3,
                    "Surviving enemy wall takes damage before ricochet");
                Invoke(hook, "FlyHook");
                Check(wall.CurrentHealth == 60, "Numerical repeated contact does not multiply wall damage");
            }
            if (mode == 2 || mode == 3)
            {
                Check(wall.IsDestroyed && !wall.GetComponent<Collider2D>().enabled && hook.IsFlyingOut && hook.ChainPointCount == 2,
                    "Destroyed wall releases cell and does not reflect: " + mode);
                int remainingBounces = (int)typeof(HookAbility).GetField("bouncesLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(hook);
                Check(remainingBounces == (mode == 2 ? 0 : 1), "Destroyed wall does not consume bounce budget");
                Invoke(hook, "FlyHook");
                Check(hook.ActiveHook.position.x > -4, "Hook continues through destroyed wall");
            }
            if (mode == 4) Check(wall.CurrentHealth == 100 && hook.IsFlyingOut && hook.ChainPointCount == 3,
                "Friendly wall collides and reflects without damage");
            hook.CancelHook(); wall.gameObject.SetActive(false);
        }
        BoneWall damaged = Object.Instantiate(prefab, new Vector3(-4, -2), Quaternion.identity, root.transform); damaged.Initialize(owner);
        damaged.TakeHookDamage(enemy, 40);
        long bonesBefore = defender.GetComponent<OwnerEconomy>().Bones;
        double goldBefore = owner.Gold;
        Check(damaged.TryUpgrade(0) && damaged.MaxHealth == 150 && damaged.CurrentHealth == 110 &&
            defender.GetComponent<OwnerEconomy>().Bones == bonesBefore - 5 && owner.Gold == goldBefore,
            "Wall upgrade spends bones only and preserves previously received damage");
        Check(!WorldCollision.CanMove(attacker.transform, new Vector3(-2, -2), 0.5f), "Wall blocks hero walking");
        attacker.transform.position = damaged.transform.position;
        Check(WorldCollision.CanMove(attacker.transform, attacker.transform.position + Vector3.left, 0.5f),
            "A hero can escape a preexisting overlap after hook landing");
        attacker.transform.position = new Vector3(-6, -2);
        damaged.TakeHookDamage(enemy, 1000);
        Check(WorldCollision.CanMove(attacker.transform, new Vector3(-2, -2), 0.5f), "Destroyed wall releases walking path immediately");
        CheckPlacement(root, prefab);
    }
    private static void CheckPlacement(GameObject root, BoneWall prefab)
    {
        GameObject hero = new GameObject("Builder"); hero.transform.SetParent(root.transform);
        hero.transform.position = new Vector3(-4, -8);
        hero.AddComponent<Health>(); hero.AddComponent<HookVictim>();
        OwnerEconomy wallet = hero.AddComponent<OwnerEconomy>(); wallet.AddOutput(0, 100);
        PlayerProgression owner = hero.GetComponent<PlayerProgression>(); owner.AddGold(1000);
        PlayerBuilding builder = hero.AddComponent<PlayerBuilding>();
        foreach (int depth in new[] { 4, 20 })
        {
            Set(ArenaLayout.Instance, "appliedDepth", depth);
            float edge = ArenaLayout.OuterEdge;
            GameObject mountain = new GameObject("Test mountain"); mountain.transform.SetParent(root.transform);
            mountain.layer = LayerMask.NameToLayer("Mountains"); mountain.transform.position = new Vector3(-edge - 0.5f, 0);
            mountain.AddComponent<BoxCollider2D>().size = new Vector2(1, 20);
            Vector3 cell = new Vector3(-edge + 0.5f, -6.5f);
            builder.SelectBuilding(prefab);
            if (!builder.CanPlace(cell))
                foreach (Collider2D hit in Physics2D.OverlapBoxAll(cell, Vector2.one * 0.9998f, 0))
                    Debug.Log("PLACEMENT_BLOCKER: " + hit.name + " " + hit.bounds);
            Check(builder.CanPlace(cell), "Extreme bank cell touching mountain is buildable at depth " + depth);
            double gold = owner.Gold; long bones = wallet.Bones;
            GameBuilding built = builder.TryBuildAt(cell);
            Check(built != null && wallet.Bones == bones - 5 && owner.Gold == gold, "Wall construction spends only bones");
            built.transform.SetParent(root.transform);
            builder.SelectBuilding(prefab);
            Check(!builder.CanPlace(cell) && builder.TryBuildAt(cell) == null && wallet.Bones == bones - 5,
                "Occupied cell rejects overlapping build without payment");
            Check(!builder.CanPlace(new Vector3(0.5f, 0.5f)) && !builder.CanPlace(new Vector3(4.5f, 0.5f)) &&
                !builder.CanPlace(new Vector3(-edge - 0.5f, 0.5f)), "River, other bank and outside field reject construction");
            GameObject enemy = new GameObject("Enemy", typeof(PlayerProgression)); enemy.transform.SetParent(root.transform);
            ((BoneWall)built).TakeHookDamage(enemy.GetComponent<PlayerProgression>(), 1000);
            Check(builder.CanPlace(cell), "Destroyed wall cell becomes buildable immediately");
            Check(!builder.CanPlace(cell + Vector3.right * 0.1f), "Building API rejects positions between grid cells");
            mountain.SetActive(false);
        }
        Set(ArenaLayout.Instance, "appliedDepth", 16);
        foreach (string path in new[] { EconomySetup.FactoryPath, EconomySetup.ShopPath, EconomySetup.FarmPath, EconomySetup.WallPath })
        {
            GameObject building = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int kind=path==EconomySetup.FactoryPath?0:path==EconomySetup.ShopPath?1:path==EconomySetup.FarmPath?2:3;
            var visual=AssetDatabase.LoadAssetAtPath<GameObject>(WorldModels25DSetup.Output+"/"+WorldModels25DSetup.Names[kind]+".prefab");
            Check(building.transform.localScale == Vector3.one && building.GetComponent<BoxCollider2D>().size == Vector2.one &&
                visual!=null && visual.GetComponentsInChildren<Renderer>(true).Length>0,
                "One-metre logical cell has a ready 3D visual asset: " + building.name);
        }
    }
    private static void CheckAnimals(GameObject root)
    {
        GameObject ownerRoot = new GameObject("Farm owner"); ownerRoot.transform.SetParent(root.transform);
        OwnerEconomy wallet = ownerRoot.AddComponent<OwnerEconomy>();
        PlayerProgression owner = wallet.GetComponent<PlayerProgression>();
        AnimalFarm prefab = AssetDatabase.LoadAssetAtPath<AnimalFarm>(EconomySetup.FarmPath);
        AnimalFarm a = Object.Instantiate(prefab, new Vector3(-12, -8), Quaternion.identity, root.transform);
        AnimalFarm b = Object.Instantiate(prefab, new Vector3(14, -8), Quaternion.identity, root.transform);
        a.Initialize(owner); b.Initialize(owner);
        Check(a.Advance(14) == null && b.Advance(14) == null, "Independent farm timers wait for interval");
        Animal first = a.Advance(1), second = b.Advance(1);
        Check(first != null && second != null && first != second, "Each farm spawns independently");
        first.transform.SetParent(root.transform); second.transform.SetParent(root.transform);
        Check(ArenaLayout.IsInsideBank(first.transform.position, first.transform.position.x > 0, first.Radius) &&
            ArenaLayout.IsInsideBank(second.transform.position, second.transform.position.x > 0, second.Radius), "Animal spawn on dry dynamic banks");
        Check(first.GetComponent<Health>() == null && second.GetComponent<Health>() == null, "No numeric Health on animals");
        float mass = first.MassKg;
        Carcass carcass = first.Kill(); carcass.transform.SetParent(root.transform);
        Check(first.Kill() == carcass && carcass.MassKg == mass && !first.gameObject.activeSelf,
            "Animal death gives exactly one carcass and stops walking animation");
        second.gameObject.SetActive(false);
        carcass.gameObject.SetActive(false);
        foreach (string species in Animals25DSetup.Names)
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(Animals25DSetup.Root+"/Models/"+species+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var walk=clips.Single(c=>Butcher25DSetup.Matches(c.name,"Walk"));
            var graze=clips.Single(c=>Butcher25DSetup.Matches(c.name,"Graze"));
            Check(Mathf.Abs(walk.length-(species=="Cow"?1.2f:.9f))<.002f&&walk.isLooping,"Imported Walk timing and looping: "+species);
            Check(Mathf.Abs(graze.length-6.4f)<.002f&&graze.isLooping,"Imported Graze timing and looping: "+species);
        }
        GameObject killer = new GameObject("Animal hook owner"); killer.transform.SetParent(root.transform);
        killer.transform.position = new Vector3(-6, -6);
        killer.AddComponent<Health>(); killer.AddComponent<HookVictim>(); CarcassCarrier carrier = killer.AddComponent<CarcassCarrier>();
        HookAbility hook = killer.AddComponent<HookAbility>();
        Set(hook, "hookPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hook.prefab").transform);
        Set(hook, "targetLayers", (LayerMask)LayerMask.GetMask("Animals"));
        Set(hook, "speed", 4f / Time.deltaTime);
        for (int mode = 0; mode < 2; mode++)
        {
            Animal animal = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/sheep.prefab"), new Vector3(-4, -6), Quaternion.identity, root.transform);
            Set(hook, "nextThrowTime", 0f); hook.TryThrow(Vector2.right);
            if (mode == 0) Invoke(hook, "FlyHook");
            else { hook.ActiveHook.position = new Vector3(-2, -6); Invoke(hook, "TryCatchOnReturn", killer.transform.position, 10f); }
            Carcass caught = (Carcass)typeof(Animal).GetField("carcass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(animal);
            Check(caught != null && hook.IsPullingTarget(caught.transform) && caught.IsReserved, "Animal lethal hook continues pulling its carcass: " + mode);
            Set(hook, "pullSpeed", 100f / Time.deltaTime); Invoke(hook, "PullTarget");
            Check(caught.IsStored && carrier.Count == mode + 1, "Animal carcass uses normal pickup: " + mode);
        }
        owner.AddGold(1000);
        Check(a.TryUpgrade(0) && a.SpawnInterval == 12 && b.SpawnInterval == 15, "Farm upgrade is local with positive minimum");
        GameObject obstacle = new GameObject("Animal obstacle"); obstacle.transform.SetParent(root.transform);
        obstacle.layer = LayerMask.NameToLayer("Buildings"); obstacle.transform.position = new Vector3(-4, 6);
        obstacle.AddComponent<BoxCollider2D>().size = Vector2.one;
        Animal walker = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/sheep.prefab"), new Vector3(-6, 6), Quaternion.identity, root.transform);
        Check(!WorldCollision.CanMove(walker.transform, new Vector3(-2, 6), walker.Radius), "Animal movement sweep cannot cross buildings or walls");
        walker.transform.position = new Vector3(-2.4f, 0);
        Set(walker, "direction", Vector2.right); Set(walker, "nextDirection", float.MaxValue);
        Set(walker, "moveSpeed", 1f / Time.deltaTime);
        Invoke(walker, "Update");
        Check(ArenaLayout.IsInsideBank(walker.transform.position, false, walker.Radius) && walker.VoluntaryVelocity == Vector2.zero,
            "Animal update rejects crossing the river and stops animation");
        UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
        UnityEngine.Random.InitState(1021);
        foreach (int depth in new[] { 4, 20 })
        {
            Set(ArenaLayout.Instance, "appliedDepth", depth);
            a.transform.position=new Vector3(-(2+depth*.5f),-8);b.transform.position=new Vector3(2+depth*.5f,-8);
            bool left = false, right = false;
            for (int i = 0; i < 24; i++)
            {
                bool useRight = i % 2 != 0;
                owner.transform.position = new Vector3(useRight ? -4 : 4, 0);
                Animal spawned = (useRight ? b : a).TrySpawn();
                Check(spawned != null && ArenaLayout.IsInsideBank(spawned.transform.position, spawned.transform.position.x > 0, spawned.Radius),
                    "Free dry spawn at bank depth " + depth);
                left |= spawned.transform.position.x < 0; right |= spawned.transform.position.x > 0;
                Check((spawned.transform.position.x > 0) == useRight, "Farm bank ignores transported owner at depth " + depth);
                spawned.transform.SetParent(root.transform);
                spawned.gameObject.SetActive(false);
            }
            Check(left && right, "Independent left and right farms use their own banks at depth " + depth);
        }
        Set(ArenaLayout.Instance, "appliedDepth", 16); UnityEngine.Random.state = randomBefore;
        owner.AddGold(100000);
        for (int i = 0; i < 20; i++) a.TryUpgrade(0);
        Check(a.SpawnInterval == 3 && !a.TryUpgrade(0), "Farm interval has positive lower bound");
        GameObject cover = new GameObject("Full map"); cover.transform.SetParent(root.transform);
        cover.AddComponent<BoxCollider2D>().size = new Vector2(100, 100);
        Check(a.TrySpawn() == null, "Full map returns after bounded spawn attempts");
        cover.SetActive(false);
        var leftCover = new GameObject("Only left bank occupied"); leftCover.transform.SetParent(root.transform);
        leftCover.transform.position = new Vector3(-12,0);
        leftCover.AddComponent<BoxCollider2D>().size = new Vector2(20,20);
        Animal rightSpawn=b.TrySpawn();if(rightSpawn!=null){rightSpawn.transform.SetParent(root.transform);rightSpawn.gameObject.SetActive(false);}
        Check(a.TrySpawn()==null&&rightSpawn!=null,"Blocked left farm never falls back to free right bank");
        leftCover.SetActive(false);
        Animal overdue = a.Advance(10000); if(overdue!=null) { overdue.transform.SetParent(root.transform); overdue.gameObject.SetActive(false); }
        Check(overdue != null && a.Advance(.01) == null,"Long overdue farm starts a fresh interval without burst queue");
        GameObject fighter = new GameObject("Animal axe attacker"); fighter.transform.SetParent(root.transform);
        fighter.transform.position = new Vector3(-12, 2);
        GameObject aimRoot = new GameObject("Aim"); aimRoot.transform.SetParent(fighter.transform, false);
        PlayerAim aim = aimRoot.AddComponent<PlayerAim>();
        PlayerMelee axe = fighter.AddComponent<PlayerMelee>();
        Set(axe, "playerAim", aim); Set(axe, "targetLayers", (LayerMask)LayerMask.GetMask("Animals"));
        Animal axeVictim = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Animal>("Assets/Prefabs/cow.prefab"),
            fighter.transform.position + Vector3.right, Quaternion.identity, root.transform);
        Invoke(axe, "TryAttack");
        Carcass axeCarcass = (Carcass)typeof(Animal).GetField("carcass", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(axeVictim);
        Check(axeVictim.IsDead && axeCarcass != null && axeCarcass.MassKg == 10, "One ordinary axe hit kills cow without numeric Health");
        axeCarcass.transform.SetParent(root.transform);
        axeCarcass.gameObject.SetActive(false);
    }
    public static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, args);
    private static void CheckShops(GameObject root)
    {
        GameObject first = new GameObject("Shop owner A"); first.transform.SetParent(root.transform);
        GameObject second = new GameObject("Shop owner B"); second.transform.SetParent(root.transform);
        OwnerEconomy stockA = first.AddComponent<OwnerEconomy>(), stockB = second.AddComponent<OwnerEconomy>();
        PlayerProgression a = first.GetComponent<PlayerProgression>(), b = second.GetComponent<PlayerProgression>();
        MeatShop prefab = AssetDatabase.LoadAssetAtPath<MeatShop>(EconomySetup.ShopPath);
        MeatShop shopA = Object.Instantiate(prefab, root.transform), shopB = Object.Instantiate(prefab, root.transform);
        shopA.Initialize(a); shopB.Initialize(a);
        stockA.AddOutput(1, 0);
        Check(Near(shopA.Advance(2) + shopB.Advance(2), 1) && Near(a.Gold, 5) && stockA.ProductKg == 0,
            "Two shops cannot sell one stock twice");
        Check(shopA.Advance(100) == 0 && a.Gold == 5 && a.Experience == 0, "Empty stock grants no income or experience");
        MeatShop comparison = Object.Instantiate(prefab, root.transform); comparison.Initialize(b);
        stockA.AddOutput(2.5, 0); stockB.AddOutput(2.5, 0);
        double gold = a.Gold;
        for (int i = 0; i < 100; i++) shopA.Advance(0.05);
        comparison.Advance(5);
        Check(Near(a.Gold - gold, b.Gold) && Near(stockA.ProductKg, stockB.ProductKg), "Sales preserve fractional results across time steps");
        a.AddGold(100);
        Check(shopA.TryUpgrade(0) && shopA.SaleKgPerSecond == 0.5f && shopA.GoldPerKg == 6,
            "Local price upgrade keeps sale speed unchanged");
        Check(shopB.GoldPerKg == 5 && comparison.GoldPerKg == 5, "Other shop prices and owners remain independent");

    }
    public static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Economy: " + message);
        assertions++; Debug.Log("ECONOMY_CHECK: " + message);
    }
    public static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
    public static void Set(object target, string field, object value)
    {
        Type type = target.GetType();
        System.Reflection.FieldInfo found;
        while ((found = type.GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)) == null) type = type.BaseType;
        found.SetValue(target, value);
    }
}
