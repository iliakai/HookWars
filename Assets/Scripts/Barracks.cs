using UnityEngine;

public sealed class Barracks : GameBuilding
{
    [SerializeField] private SausageWarrior warriorPrefab;
    [SerializeField,Min(0)] private int warriorGold=50,warriorBones=2;
    [SerializeField,Min(0)] private float warriorMeat=4;
    [SerializeField,Min(.1f)] private float productionSeconds=12;
    private WarriorStats upgrades,order;
    private bool hasOrder;
    private float progress;
    private float nextExitCheck;
    public const int ImprovementCost=1000;
    public override BuildingKind Kind=>BuildingKind.Barracks;
    public override string DisplayName=>"Барак";
    public override Vector2Int Footprint=>new Vector2Int(2,2);
    public override int UpgradeCount=>6;
    public bool AutoProduction {get;private set;}
    public bool HasOrder=>hasOrder;
    public float Progress=>hasOrder?Mathf.Clamp01(progress/productionSeconds):0;
    public WarriorStats Upgrades=>upgrades;
    public WarriorStats OrderStats=>order;
    public float DoorOpening=>hasOrder&&AutoProduction?Mathf.InverseLerp(.85f,1,Progress):0;
    public string UnitCost=>$"{warriorMeat:0.#} кг мяса + {warriorBones} кг костей + {warriorGold} золота";
    public override string Info=>$"Воин: {UnitCost}\n"+(hasOrder?$"{(AutoProduction?(Progress>=1?"Ожидает свободный выход":"Производство"):"Приостановлено")}: {Progress*100:0}%":AutoProduction?MissingResources():"Производство выключено");
    public bool SetProduction(bool enabled)
    {
        if(!CanManage())return false;
        AutoProduction=enabled;return true;
    }
    private bool CanManage()=>owner!=null&&isActiveAndEnabled&&!MatchPause.BlocksGameplay&&owner.GetComponent<Health>()?.IsDead!=true&&owner.GetComponent<HookVictim>()?.IsCaught!=true;
    public override bool CanBuild(PlayerProgression candidate)=>base.CanBuild(candidate)&&candidate.GetComponent<OwnerEconomy>()!=null;
    private string MissingResources()
    {
        if(owner==null)return "Нет владельца";
        var stock=owner.GetComponent<OwnerEconomy>();
        if(stock==null)return "Нет склада";
        string missing="";
        if(stock.ProductKg+1e-9<warriorMeat)missing+=" мясо";
        if(stock.BoneKg+1e-9<warriorBones)missing+=" кости";
        if(owner.Gold<warriorGold)missing+=" золото";
        return missing.Length>0?"Не хватает:"+missing:"Готов к производству";
    }
    private void Update()=>Advance(Time.deltaTime);
    public void Advance(float seconds)
    {
        if(!isActiveAndEnabled||MatchPause.BlocksGameplay||!AutoProduction||owner==null||seconds<=0||!float.IsFinite(seconds))return;
        if(!hasOrder)
        {
            if(warriorPrefab==null||!owner.GetComponent<OwnerEconomy>().TrySpendResources(warriorGold,warriorMeat,warriorBones))return;
            order=upgrades;progress=0;hasOrder=true;
        }
        progress=Mathf.Min(productionSeconds,progress+seconds);
        if(progress<productionSeconds||Time.time<nextExitCheck)return;
        nextExitCheck=Time.time+.2f;
        if(!TryFindExit(out var position))return;
        var warrior=Instantiate(warriorPrefab,position,Quaternion.identity);
        warrior.Bind(owner,order,transform.position.x>0);
        hasOrder=false;progress=0;
    }
    public bool TryFindExit(out Vector3 position)
    {
        position=default;
        Vector2 forward=-transform.up,right=transform.right;
        const float radius=SausageWarrior.BodyRadius;
        Physics2D.SyncTransforms();
        // Start outside the solid footprint; never spawn inside the closed building.
        Vector2 edge=(Vector2)transform.position+forward*(1+radius+.08f);
        foreach(float distance in new[]{0f,.35f,.7f})foreach(float side in new[]{0f,-.45f,.45f})
        {
            Vector2 candidate=edge+forward*distance+right*side;
            if(!ArenaLayout.IsInsideBank(candidate,transform.position.x>0,radius))continue;
            bool blocked=false;
            foreach(var hit in Physics2D.OverlapCircleAll(candidate,radius+.02f,WorldCollision.WalkingLayers))
                if(!hit.isTrigger){blocked=true;break;}
            if(blocked)continue;
            foreach(var hit in Physics2D.CircleCastAll(edge,radius,candidate-edge,Vector2.Distance(edge,candidate),WorldCollision.WalkingLayers))
                if(!hit.collider.isTrigger){blocked=true;break;}
            if(!blocked){position=candidate;return true;}
        }
        return false;
    }
    public override string UpgradeLabel(int index)
    {
        string[] names={"Здоровье +10 кг","Урон +2 кг","Радиус удара +0,1 м","Яд +1 кг/с на 10 с","Частота удара +10% от базы","Вероятность крита"};
        return index>=0&&index<6?$"{names[index]} · ур. {upgrades.Level(index)} · {ImprovementCost} золота":"";
    }
    public override bool TryUpgrade(int index)
    {
        var next=upgrades;
        if(!CanManage()||!next.Upgrade(index)||!owner.TrySpendGold(ImprovementCost))return false;
        upgrades=next;return true;
    }
}
