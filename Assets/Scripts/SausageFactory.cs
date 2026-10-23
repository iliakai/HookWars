using System;
using System.Collections.Generic;
using UnityEngine;

public class SausageFactory : GameBuilding
{
    public override Vector2Int Footprint=>new Vector2Int(2,2);
    public bool InDeliveryRange(Vector3 position)=>Vector2.Distance(position,transform.position)<=deliveryRadius+.5f;
    [SerializeField, Min(0.1f)] private float capacityKg = 12f;
    [SerializeField, Min(0.1f)] private float secondsPerKg = 2f;
    [SerializeField, Min(0)] private float productPerKg = 1f;
    [SerializeField, Min(0)] private float bonesPerKg = 0.5f;
    [SerializeField, Min(0)] private float experiencePerKg = 2f;
    [SerializeField, Min(0.1f)] private float deliveryRadius = 1.5f;
    [SerializeField, Min(1)] private int speedCost = 50, capacityCost = 50, experienceCost = 50;
    [SerializeField, Range(0.1f, 0.99f)] private float speedMultiplier = 0.8f;
    [SerializeField, Min(0.01f)] private float minimumSecondsPerKg = 0.1f;
    [SerializeField, Min(0.1f)] private float capacityIncrease = 12f;
    [SerializeField, Min(0)] private float experienceIncrease = 1f;
    [Header("Helper economy (temporary balance)")]
    [SerializeField] private ButcherHelper helperPrefab;
    [SerializeField,Min(1)] private int helperPrice=100,helperCapacityCost=50;
    [SerializeField,Min(.1f)] private float helperCapacityKg=10,helperCapacityIncrease=2;
    private readonly List<ButcherHelper> helpers=new List<ButcherHelper>();
    public float HelperCapacityKg=>helperCapacityKg;
    public int HelperPrice=>helperPrice;
    public int HelperCapacityCost=>helperCapacityCost;
    public int HelperCount{get{helpers.RemoveAll(h=>h==null||h.GetComponent<Health>().IsDead);return helpers.Count;}}
    public bool TryOrderHelper()
    {
        if(owner==null||helperPrefab==null||owner.Gold<helperPrice)return false;
        for(int attempt=0;attempt<48;attempt++)
        {
            Vector2 offset=UnityEngine.Random.insideUnitCircle.normalized*UnityEngine.Random.Range(1.5f,2.5f);
            Vector3 position=transform.position+(Vector3)offset;
            if(!ArenaLayout.IsInsideBank(position,transform.position.x>0,.27f)||!WorldCollision.FreeCircle(position,.27f))continue;
            if(!owner.TrySpendGold(helperPrice))return false;
            var helper=Instantiate(helperPrefab,position,Quaternion.identity,transform);helper.Bind(this,helperCapacityKg);helpers.Add(helper);return true;
        }
        return false;
    }
    private readonly Queue<double> queue = new Queue<double>();
    private double headRemaining;
    private double rawKg;
    public double RawKg => rawKg;
    public double CapacityKg => capacityKg;
    public float SecondsPerKg => secondsPerKg;
    public float ExperiencePerKg => experiencePerKg;
    public override BuildingKind Kind => BuildingKind.Factory;
    public override string DisplayName => "Колбасный цех";
    public override int UpgradeCount => 5;
    public override string Info => $"Сырьё: {rawKg:0.##} / {capacityKg:0.##} кг\n{secondsPerKg:0.##} с/кг; опыт: {experiencePerKg:0.##}/кг\nВыход: {productPerKg:0.##} кг и {bonesPerKg:0.##} костей/кг";
    public override bool CanBuild(PlayerProgression candidate) => candidate != null &&
        candidate.GetComponent<OwnerEconomy>() != null && candidate.GetComponent<OwnerEconomy>().Factory == null;
    public override bool Initialize(PlayerProgression candidate)
    {
        if (!base.Initialize(candidate)) return false;
        candidate.GetComponent<OwnerEconomy>().Factory = this;
        return true;
    }
    private void Start()
    {
        // Migrated scene instances may already have a serialized owner.
        if (owner != null && owner.GetComponent<OwnerEconomy>().Factory == null)
            owner.GetComponent<OwnerEconomy>().Factory = this;
    }
    private void Update()
    {
        if (owner == null||MatchPause.BlocksGameplay||Time.deltaTime<=0) return;
        CarcassCarrier carrier = owner.GetComponent<CarcassCarrier>();
        if (carrier != null && InDeliveryRange(owner.transform.position))
            carrier.DeliverTo(this);
        Advance(Time.deltaTime);
        if (carrier != null && InDeliveryRange(owner.transform.position))
            carrier.DeliverTo(this);
    }
    internal bool TryAcceptMass(double mass)
    {
        if (owner == null || !double.IsFinite(mass) || mass <= 0 || mass > capacityKg - rawKg + 1e-9) return false;
        queue.Enqueue(mass);
        rawKg += mass;
        owner.AddExperience(mass * experiencePerKg);
        return true;
    }
    public void Advance(double seconds)
    {
        if (owner == null || !double.IsFinite(seconds) || seconds <= 0) return;
        double budget = seconds / Math.Max(minimumSecondsPerKg, secondsPerKg);
        double processed = 0;
        while (budget > 0 && (headRemaining > 0 || queue.Count > 0))
        {
            if (headRemaining <= 0) headRemaining = queue.Dequeue();
            double part = Math.Min(budget, headRemaining);
            headRemaining -= part;
            budget -= part;
            processed += part;
        }
        rawKg = Math.Max(0, rawKg - processed);
        owner.GetComponent<OwnerEconomy>().AddOutput(processed * productPerKg, processed * bonesPerKg);
    }
    public override string UpgradeLabel(int index)
    {
        if(index==3)return $"Заказать помощника: {helperPrice} зол. (сейчас {HelperCount})";
        if(index==4)return $"Груз помощников {helperCapacityKg:0.#} → {helperCapacityKg+helperCapacityIncrease:0.#} кг: {helperCapacityCost} зол.";
        if (index == 0) return $"Обработка {secondsPerKg:0.##} → {Mathf.Max(minimumSecondsPerKg, secondsPerKg * speedMultiplier):0.##} с/кг: {speedCost} зол.";
        if (index == 1) return $"Вместимость +{capacityIncrease} кг: {capacityCost} зол.";
        return $"Опыт +{experienceIncrease}/кг: {experienceCost} зол.";
    }
    public override bool TryUpgrade(int index)
    {
        if(index==3)return TryOrderHelper();
        if(index==4)
        {
            if(owner==null||!owner.TrySpendGold(helperCapacityCost))return false;
            helperCapacityKg+=helperCapacityIncrease;helperCapacityCost=NextCost(helperCapacityCost);
            foreach(var helper in helpers)if(helper!=null)helper.GetComponent<CarcassCarrier>().SetHelperCapacity(helperCapacityKg);return true;
        }
        if (owner == null || index < 0 || index > 2) return false;
        if (index == 0 && secondsPerKg <= minimumSecondsPerKg) return false;
        int cost = index == 0 ? speedCost : index == 1 ? capacityCost : experienceCost;
        if (!owner.TrySpendGold(cost)) return false;
        if (index == 0) { secondsPerKg = Mathf.Max(minimumSecondsPerKg, secondsPerKg * speedMultiplier); speedCost = NextCost(cost); }
        if (index == 1) { capacityKg += capacityIncrease; capacityCost = NextCost(cost); }
        if (index == 2) { experiencePerKg += experienceIncrease; experienceCost = NextCost(cost); }
        return true;
    }
    private void OnDestroy()
    {
        if (owner != null && owner.GetComponent<OwnerEconomy>().Factory == this)
            owner.GetComponent<OwnerEconomy>().Factory = null;
    }
}
