using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent,RequireComponent(typeof(Health))]
public sealed class WarriorPoison : MonoBehaviour
{
    private sealed class Dose {public Health source;public float damage;public double expires;}
    private readonly List<Dose> doses=new List<Dose>();
    private Health health;
    private double clock,nextTick;
    public int ActiveDoses=>doses.Count;
    private void Awake(){health=GetComponent<Health>();health.Died+=Clear;health.Revived+=Clear;}
    public static void Apply(Health target,Health source,float damage)
    {
        if(target==null||source==null||target.IsDead||damage<=0||!float.IsFinite(damage))return;
        var effect=target.GetComponent<WarriorPoison>()??target.gameObject.AddComponent<WarriorPoison>();
        if(effect.doses.Count==0)effect.nextTick=effect.clock+1;
        var dose=effect.doses.Find(d=>d.source==source);
        if(dose==null){dose=new Dose{source=source};effect.doses.Add(dose);}
        dose.damage=damage;dose.expires=effect.clock+10;
    }
    private void Update()=>Advance(Time.deltaTime);
    public void Advance(float seconds)
    {
        if(MatchPause.BlocksGameplay||health.IsDead||seconds<=0||!float.IsFinite(seconds)||doses.Count==0)return;
        clock+=seconds;
        while(doses.Count>0&&nextTick<=clock+1e-6)
        {
            double tick=nextTick;nextTick+=1;
            doses.RemoveAll(d=>d.expires+1e-6<tick);
            Dose strongest=null;foreach(var dose in doses)if(strongest==null||dose.damage>strongest.damage)strongest=dose;
            if(strongest==null)break;
            health.TakeDamage(strongest.damage,strongest.source);
            if(health.IsDead)break;
        }
        doses.RemoveAll(d=>d.expires<=clock+1e-6);
    }
    private void Clear(Health _)=>doses.Clear();
    private void OnDisable()=>doses.Clear();
    private void OnDestroy(){if(health!=null){health.Died-=Clear;health.Revived-=Clear;}}
}
