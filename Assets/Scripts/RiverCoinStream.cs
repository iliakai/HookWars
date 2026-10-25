using UnityEngine;

[DisallowMultipleComponent]
public sealed class RiverCoinStream : MonoBehaviour
{
    [SerializeField] private RiverCoin coinPrefab;
    [SerializeField] private ArenaPresentation25D presentation;
    private double nextSpawn;
    public double SecondsUntilSpawn => nextSpawn;
    public float FlowSpeed => presentation!=null?presentation.RiverFlowMetersPerSecond:.6f;
    private void Awake()=>ScheduleNext();
    private void ScheduleNext()=>nextSpawn=Random.Range(25f,35f);
    private void Update()=>Advance(Time.deltaTime);
    public void Advance(double seconds)
    {
        if(MatchPause.BlocksGameplay||Time.timeScale<=0||!double.IsFinite(seconds)||seconds<=0)return;
        nextSpawn-=seconds;
        if(nextSpawn>0)return;
        SpawnOne();ScheduleNext();
    }
    public RiverCoin SpawnOne()
    {
        if(coinPrefab==null)throw new System.InvalidOperationException("Assign the river coin prefab");
        float margin=coinPrefab.GetComponent<CircleCollider2D>().radius+.1f;
        var position=new Vector3(Random.Range(-ArenaLayout.RiverHalfWidth+margin,ArenaLayout.RiverHalfWidth-margin),ArenaLayout.FieldHeight*.5f+.35f);
        var coin=Instantiate(coinPrefab,position,Quaternion.identity,transform);
        coin.Initialize(Random.Range(25,76),FlowSpeed);return coin;
    }
}
