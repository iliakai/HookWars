using UnityEngine;
using UnityEngine.InputSystem;

// Retains the original component GUID, now owns the complete development window.
public class HookUpgradeMenu : MonoBehaviour
{
    public enum Skill { HookRange,HookDamage,HookWidth,HookSpeed,HookCooldown,AxeDamage,AxeSize,Movement,Regeneration,Capacity }
    [SerializeField] private PlayerProgression progression;
    [SerializeField] private HookAbility hook;
    private static HookUpgradeMenu instance;
    private bool open;
    private int closedFrame=-1;
    public bool IsOpen=>open;
    public static bool BlocksWorldInput=>instance!=null&&(instance.open||instance.closedFrame==Time.frameCount);
    public static Rect ScreenRect=>new Rect(Screen.width-190,12,178,46);
    private void Awake(){instance=this;if(progression==null)progression=GetComponent<PlayerProgression>();if(hook==null)hook=GetComponent<HookAbility>();}
    private void Update()
    {
        if(MatchPause.BlocksGameplay)return;
        if(Keyboard.current?.tabKey.wasPressedThisFrame==true)Toggle();
        if(progression.GetComponent<Health>()?.IsDead==true||progression.GetComponent<HookVictim>()?.IsCaught==true)CloseInterface();
    }
    public void Toggle()
    {
        if(open){CloseInterface();return;}
        if(MatchPause.BlocksGameplay||progression.GetComponent<Health>().IsDead||progression.GetComponent<HookVictim>().IsCaught)return;
        progression.GetComponent<PlayerBuilding>()?.CloseInterface();progression.GetComponent<BuildingUpgradeMenu>()?.CloseInterface();open=true;
    }
    public bool CloseInterface(){bool had=open;if(had)closedFrame=Time.frameCount;open=false;return had;}
    public bool TryBuy(Skill skill)
    {
        if(MatchPause.BlocksGameplay||progression==null||progression.UpgradePoints<=0||progression.GetComponent<Health>().IsDead||progression.GetComponent<HookVictim>().IsCaught)return false;
        bool applied;
        if((int)skill<=4)
        {
            applied=hook.TryUpgrade((HookAbility.UpgradeStat)(int)skill);
            if(applied)progression.TrySpendUpgradePoint();
        }
        else switch(skill)
        {
            case Skill.AxeDamage: applied=progression.GetComponent<PlayerMelee>().TryUpgrade(false,progression);break;
            case Skill.AxeSize: applied=progression.GetComponent<PlayerMelee>().TryUpgrade(true,progression);break;
            case Skill.Movement: applied=progression.GetComponent<PlayerMovement>().TryUpgradeSpeed(progression);break;
            case Skill.Regeneration: applied=progression.GetComponent<Health>().TryUpgradeRegeneration(progression);break;
            case Skill.Capacity: applied=progression.GetComponent<CarcassCarrier>().TryUpgradeCapacity(progression);break;
            default:return false;
        }
        return applied;
    }
    public bool CanBuy(Skill skill)=>progression.UpgradePoints>0&&
        ((int)skill>4||(hook.ActiveHook==null&&!hook.IsPreparingThrow&&(skill!=Skill.HookCooldown||hook.Cooldown>HookAbility.MinimumCooldown)))&&
        ((skill!=Skill.AxeDamage&&skill!=Skill.AxeSize)||progression.GetComponent<HeroActionTiming25D>()?.AxeAnimating!=true);
    public string Value(Skill skill)
    {
        var axe=progression.GetComponent<PlayerMelee>();var hp=progression.GetComponent<Health>();
        switch(skill)
        {
            case Skill.HookRange:return $"Дальность крюка\n{hook.Range:0.##} м   (+1 м)";
            case Skill.HookDamage:return $"Урон крюка\n{hook.Damage:0.##} кг   (+10)";
            case Skill.HookWidth:return $"Ширина крюка\n{hook.Width:0.##} м   (+0,1 м)";
            case Skill.HookSpeed:return $"Скорость крюка\n{hook.Speed:0.##} м/с   (+1)";
            case Skill.HookCooldown:return $"Перезарядка крюка\n{hook.Cooldown:0.##} с   (−0,25 с)";
            case Skill.AxeDamage:return $"Урон топора\n{axe.Damage:0.##} кг   (+5)";
            case Skill.AxeSize:return $"Размах / размер топора\n{axe.AttackRange:0.##} м   (+0,2 м)";
            case Skill.Movement:return $"Скорость героя\n{progression.GetComponent<PlayerMovement>().MoveSpeed:0.##} м/с   (+0,25)";
            case Skill.Regeneration:return $"Регенерация\n{hp.RegenerationKgPerSecond:0.##} кг/с   (+0,1)";
            default:return $"Переноска\n{progression.GetComponent<CarcassCarrier>().CapacityKg:0.##} кг   (+2 кг)";
        }
    }
    private void OnGUI()
    {
        if(MatchPause.BlocksGameplay)return;
        if(GUI.Button(ScreenRect,$"Развитие [Tab] · {progression.UpgradePoints}"))Toggle();
        if(!open)return;
        GUI.Box(new Rect(0,0,Screen.width,Screen.height),GUIContent.none);
        Matrix4x4 old=GUI.matrix;float scale=Mathf.Min(Screen.width/960f,Screen.height/700f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-960*scale)/2,(Screen.height-700*scale)/2),Quaternion.identity,Vector3.one*scale);
        GUI.Box(new Rect(55,28,850,640),$"Развитие героя · уровень {progression.Level} · очки: {progression.UpgradePoints}");
        for(int i=0;i<10;i++)
        {
            float x=80+(i%2)*410,y=74+(i/2)*103;
            GUI.Box(new Rect(x,y,390,94),GUIContent.none);GUI.Label(new Rect(x+14,y+15,306,64),Value((Skill)i));
            GUI.enabled=CanBuy((Skill)i);
            if(GUI.Button(new Rect(x+322,y+24,52,45),"+"))TryBuy((Skill)i);GUI.enabled=true;
        }
        GUI.Label(new Rect(80,605,650,34),"Каждый «+» стоит 1 очко уровня. Количество отскоков не ограничено.");
        if(GUI.Button(new Rect(752,612,128,38),"Закрыть / Esc"))CloseInterface();GUI.matrix=old;
    }
    private void OnDisable(){open=false;if(instance==this)instance=null;}
    private void OnEnable(){instance=this;}
}
