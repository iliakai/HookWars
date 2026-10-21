using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EconomyChecks
{
    private static int assertions;
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
            Debug.Log($"HOOKWARS_ECONOMY_STAGE_{stage}_PASS: {assertions} assertions.");
        }
        finally { Object.DestroyImmediate(root); }
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
