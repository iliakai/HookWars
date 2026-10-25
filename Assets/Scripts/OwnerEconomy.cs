using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerProgression))]
public class OwnerEconomy : MonoBehaviour
{
    private double productKg;
    private double bones;
    public double ProductKg => productKg;
    public double BoneKg => bones;
    public long Bones => (long)Math.Floor(bones + 1e-9);
    public double BoneRemainder => bones - Math.Floor(bones + 1e-9);
    public SausageFactory Factory { get; internal set; }
    // Main-thread transaction: no yield/events between validation and deductions.
    public bool TrySpendResources(int goldCost,double productCost,int boneCost)
    {
        if(goldCost<0||boneCost<0||!double.IsFinite(productCost)||productCost<0||
            productKg+1e-9<productCost||bones+1e-9<boneCost)return false;
        if(!GetComponent<PlayerProgression>().TrySpendGold(goldCost))return false;
        productKg=Math.Max(0,productKg-productCost);bones=Math.Max(0,bones-boneCost);return true;
    }
    public void AddOutput(double product, double boneUnits)
    {
        if (!double.IsFinite(product) || !double.IsFinite(boneUnits) || product < 0 || boneUnits < 0)
            throw new ArgumentOutOfRangeException();
        productKg += product;
        bones += boneUnits;
    }
    public double TakeProduct(double requested)
    {
        if (!double.IsFinite(requested) || requested <= 0) return 0;
        double actual = Math.Min(productKg, requested);
        productKg = Math.Max(0, productKg - actual);
        return actual;
    }
    public bool TrySpendBones(int amount)
    {
        if (amount < 0 || Bones < amount) return false;
        bones = Math.Max(0, bones - amount);
        return true;
    }
}
