using System;
using UnityEngine;

public class MeatShop : GameBuilding
{
    [SerializeField, Min(0.01f)] private float saleKgPerSecond = 0.5f;
    [SerializeField, Min(0)] private float goldPerKg = 5f;
    [SerializeField, Min(0)] private float priceIncrease = 1f;
    [SerializeField, Min(1)] private int upgradeCost = 50;
    public float SaleKgPerSecond => saleKgPerSecond;
    public float GoldPerKg => goldPerKg;
    public float LastSaleTime { get; private set; } = float.NegativeInfinity;
    public override BuildingKind Kind => BuildingKind.Shop;
    public override string DisplayName => "Мясная лавка";
    public override string Info => $"Продажа: {saleKgPerSecond:0.##} кг/с\nЦена: {goldPerKg:0.##} золота/кг\nИспользует общий запас владельца";
    public override string UpgradeLabel(int index) => $"Цена +{priceIncrease} зол./кг: {upgradeCost} зол.";
    private void Update() => Advance(Time.deltaTime);
    public double Advance(double seconds)
    {
        if (owner == null || !double.IsFinite(seconds) || seconds <= 0) return 0;
        double sold = owner.GetComponent<OwnerEconomy>().TakeProduct(saleKgPerSecond * seconds);
        owner.AddGold(sold * goldPerKg);
        if (sold > 0) LastSaleTime = Time.time;
        return sold;
    }
    public override bool TryUpgrade(int index)
    {
        if (owner == null || index != 0 || !owner.TrySpendGold(upgradeCost)) return false;
        goldPerKg += priceIncrease;
        upgradeCost = NextCost(upgradeCost);
        return true;
    }
}
