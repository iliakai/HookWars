using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerProgression))]
public class OwnerEconomy : MonoBehaviour
{
    private double productKg;
    private double bones;
    public double ProductKg => productKg;
    public long Bones => (long)Math.Floor(bones + 1e-9);
    public double BoneRemainder => bones - Math.Floor(bones + 1e-9);
    public SausageFactory Factory { get; internal set; }
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
