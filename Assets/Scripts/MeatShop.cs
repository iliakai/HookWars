using System;
using UnityEngine;

public class MeatShop : GameBuilding
{
    [SerializeField, Min(0.001f)] private float saleKgPerSecond = 0.1f;
    [SerializeField, Min(0)] private float goldPerKg = 20f;
    [SerializeField, Min(0)] private float saleSpeedIncrease = 0.1f;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(1)] private int upgradeCost = 50;
    public float SaleKgPerSecond => saleKgPerSecond;
    public float GoldPerKg => goldPerKg;
    public int Level => level;
    public int UpgradeCost=>upgradeCost;
    public bool CanUpgrade=>owner!=null&&owner.Gold>=upgradeCost;
    public double GoldPerSecond => (double)saleKgPerSecond * goldPerKg;
    public float LastSaleTime { get; private set; } = float.NegativeInfinity;
    public override BuildingKind Kind => BuildingKind.Shop;
    public override string DisplayName => "Мясная лавка";
    public override string Info => $"Уровень: {level}\nПродажа: {saleKgPerSecond:0.##} кг/с, доход до {GoldPerSecond:0.#} зол./с\nЦена: {goldPerKg:0.##} золота/кг\nПродаёт готовое мясо со склада цеха";
    public override string UpgradeLabel(int index) => $"Уровень {level} → {level+1}: +0,1 кг/с, +5 зол./кг за {upgradeCost} зол.";
    private void Update() => Advance(Time.deltaTime);
    public double Advance(double seconds)
    {
        if (owner == null || !IsOperating || MatchPause.BlocksGameplay || !double.IsFinite(seconds) || seconds <= 0) return 0;
        double sold = owner.GetComponent<OwnerEconomy>().TakeProduct(saleKgPerSecond * seconds);
        owner.AddGold(sold * goldPerKg);
        if (sold > 0) LastSaleTime = Time.time;
        return sold;
    }
    public override bool TryUpgrade(int index)
    {
        if (owner == null || index != 0 || !owner.TrySpendGold(upgradeCost)) return false;
        saleKgPerSecond += saleSpeedIncrease;
        goldPerKg += 5;
        level++;
        upgradeCost = NextCost(upgradeCost);
        return true;
    }
}
