using System;
using UnityEngine;

// A value snapshot: a purchased upgrade never changes an existing order or soldier.
[Serializable]
public struct WarriorStats
{
    public int healthLevel, damageLevel, areaLevel, poisonLevel, speedLevel, criticalLevel;
    public float Health => 40f + 10f * healthLevel;
    public float Damage => 5f + 2f * damageLevel;
    public float Range => .9f + .1f * areaLevel;
    public float Poison => poisonLevel;
    public float AttackPeriod => 1.2f / (1f + .1f * speedLevel);
    public double CriticalChance => 1d - .9d * Math.Pow(.98d, criticalLevel);
    public float BodyScale => 1f + .15f * healthLevel;
    public float ClubWidth => 1f + .24f * damageLevel;
    public float ClubLength => 1f + .3f * areaLevel / .9f;
    public Color BodyColor => Color.Lerp(new Color(1f,.72f,.73f),new Color(.20f,.065f,.025f),3f*criticalLevel/(3f*criticalLevel+5f));
    public Color ClubColor => Color.Lerp(new Color(1f,.96f,.83f),new Color(.48f,1f,.015f),3f*poisonLevel/(3f*poisonLevel+5f));
    public int Level(int index) => index switch {0=>healthLevel,1=>damageLevel,2=>areaLevel,3=>poisonLevel,4=>speedLevel,5=>criticalLevel,_=>-1};
    public bool Upgrade(int index)
    {
        if(Level(index)<0 || Level(index)==int.MaxValue)return false;
        switch(index){case 0:healthLevel++;break;case 1:damageLevel++;break;case 2:areaLevel++;break;case 3:poisonLevel++;break;case 4:speedLevel++;break;case 5:criticalLevel++;break;}
        return true;
    }
}

public static class CombatOwnership
{
    public static PlayerProgression OwnerOf(Health health)
    {
        if(health==null)return null;
        return health.GetComponent<PlayerProgression>() ?? health.GetComponent<SausageWarrior>()?.Owner ?? health.GetComponent<ButcherHelper>()?.Factory?.Owner;
    }
    public static bool Enemies(Health a,Health b)
    {
        var first=OwnerOf(a);var second=OwnerOf(b);
        return first!=null && second!=null && first!=second;
    }
}
