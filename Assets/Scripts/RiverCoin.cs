using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(HookVictim), typeof(CircleCollider2D))]
public sealed class RiverCoin : MonoBehaviour
{
    private int value;
    private float flowSpeed = .6f;
    private bool collected;
    public int Value => value;
    public float FlowSpeed => flowSpeed;
    public bool IsAvailable => isActiveAndEnabled && !collected && !GetComponent<HookVictim>().IsCaught;
    public void Initialize(int amount, float speed)
    {
        if(amount<25||amount>75||!float.IsFinite(speed)||speed<0)throw new System.ArgumentOutOfRangeException();
        value=amount;flowSpeed=speed;collected=false;
    }
    private void Update() => Advance(Time.deltaTime);
    public void Advance(double seconds)
    {
        if(!IsAvailable||MatchPause.BlocksGameplay||Time.timeScale<=0||!double.IsFinite(seconds)||seconds<=0)return;
        transform.position+=Vector3.down*(float)(flowSpeed*seconds);
        if(transform.position.y < -ArenaLayout.FieldHeight*.5f-.6f)Retire();
    }
    internal bool TryCollect(HookAbility collector)
    {
        if(collected||!isActiveAndEnabled||MatchPause.BlocksGameplay||collector==null||GetComponent<HookVictim>().Captor!=collector)return false;
        var owner=collector.GetComponent<PlayerProgression>();var health=collector.GetComponent<Health>();
        if(owner==null||health==null||health.IsDead||Vector2.Distance(transform.position,collector.transform.position)>1.5f)return false;
        collected=true;
        owner.AddGold(value);
        MatchHud25D.Instance?.ShowGoldGain(collector.transform.position,value);
        Retire();return true;
    }
    private void Retire(){gameObject.SetActive(false);Destroy(gameObject);}
}
