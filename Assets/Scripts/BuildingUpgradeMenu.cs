using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingUpgradeMenu : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    private GameBuilding selected;
    private RiverBridge bridge;
    private Texture2D[] icons;
    public GameBuilding Selected=>selected;
    public RiverBridge SelectedBridge=>bridge;
    public bool SelectedThisFrame {get;private set;}
    public static Rect ScreenRect=>new Rect(Screen.width-432,Mathf.Max(20,Screen.height-600),420,588);
    private bool Compact=>selected is MeatShop||selected is AnimalFarm;
    public Rect ToolbarRect
    {
        get
        {
            if(selected==null||worldCamera==null)return new Rect(-1000,-1000,0,0);
            var display=FindAnyObjectByType<ArenaPresentation25D>();var view=display?.VisualFor(selected);
            var point=view!=null?view.position:ArenaSpace25D.ToWorld(selected.transform.position);
            var screen=worldCamera.WorldToScreenPoint(point+Vector3.up*1.65f);
            return new Rect(screen.x-54,Screen.height-screen.y-20,108,34);
        }
    }
    public bool Contains(Vector2 point)=>(selected!=null||bridge!=null)&&(Compact?ToolbarRect:ScreenRect).Contains(point);
    private void Awake(){if(worldCamera==null)worldCamera=Camera.main;}
    private bool CanAct=>!MatchPause.BlocksGameplay&&GetComponent<Health>()?.IsDead!=true&&GetComponent<HookVictim>()?.IsCaught!=true;
    public bool CloseInterface(){bool open=selected!=null||bridge!=null;selected=null;bridge=null;return open;}
    private void Update()
    {
        SelectedThisFrame=false;
        if(!CanAct){CloseInterface();return;}
        if(GetComponent<UnitCommander>()!=null)return;
        if(Mouse.current==null||worldCamera==null||GetComponent<PlayerBuilding>().IsBuilding)return;
        var mouse=Mouse.current.position.ReadValue();
        if(!Contains(new Vector2(mouse.x,Screen.height-mouse.y))&&Mouse.current.leftButton.wasPressedThisFrame&&!GetComponent<PlayerBuilding>().BlocksMouseInput)SelectAtScreenPoint(mouse);
    }
    public bool SelectAtScreenPoint(Vector2 screen)
    {
        if(!CanAct||GamePointer.OverInterface(screen)||GetComponent<PlayerBuilding>().IsBuilding)return false;
        if(!GamePointer.TryLogicalPoint(worldCamera,screen,out var point))return false;
        selected=null;bridge=null;
        foreach(var hit in Physics2D.OverlapPointAll(point,LayerMask.GetMask("Buildings")))
        {
            var candidate=hit.GetComponent<GameBuilding>();
            if(candidate!=null&&candidate.Owner==GetComponent<PlayerProgression>()){selected=candidate;break;}
            var crossing=hit.GetComponent<RiverBridge>();
            if(crossing!=null&&crossing.Owner==GetComponent<PlayerProgression>())bridge=crossing;
        }
        SelectedThisFrame=selected!=null||bridge!=null;return SelectedThisFrame;
    }
    private bool Icon(Rect rect,string label,Color color,bool available,string tooltip)
    {
        var old=GUI.color;bool enabled=GUI.enabled;GUI.color=color;GUI.enabled=available;
        bool clicked=GUI.Button(rect,new GUIContent(label,tooltip));GUI.enabled=enabled;GUI.color=old;return clicked;
    }
    private bool Picture(Rect rect,int kind,Color color,bool available,string tooltip)
    {
        if(icons==null)
        {
            icons=new Texture2D[4];
            for(int shape=0;shape<4;shape++)
            {
                var pixels=new Color32[24*24];
                for(int y=0;y<24;y++)for(int x=0;x<24;x++)
                {
                    bool ink=shape==0?(Mathf.Abs(x-12)<=2&&y>=3&&y<=14||y>=12&&y<=22&&Mathf.Abs(x-12)<=22-y):
                        shape==1?(x>=4&&x<=20&&y>=4&&y<=20&&(Mathf.Abs(x-y)<=1||Mathf.Abs(x+y-24)<=1)):
                        shape==2?(x>=6&&x<=18&&y>=6&&y<=18):(x>=6&&x<=20&&Mathf.Abs(y-12)<=(20-x)*.6f);
                    pixels[y*24+x]=ink?new Color32(255,255,255,255):new Color32(0,0,0,0);
                }
                icons[shape]=new Texture2D(24,24,TextureFormat.RGBA32,false){name="Building action icon",filterMode=FilterMode.Bilinear};icons[shape].SetPixels32(pixels);icons[shape].Apply();
            }
        }
        var previous=GUI.color;bool wasEnabled=GUI.enabled;GUI.enabled=available;
        bool pressed=GUI.Button(rect,new GUIContent("",tooltip));GUI.color=color;
        GUI.DrawTexture(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),icons[kind],ScaleMode.ScaleToFit);
        GUI.color=previous;GUI.enabled=wasEnabled;return pressed;
    }
    private void OnDestroy(){if(icons!=null)foreach(var icon in icons)if(icon!=null)Destroy(icon);}
    private void OnGUI()
    {
        if(!CanAct)return;
        if(bridge!=null)
        {
            var box=ScreenRect;GUI.Box(box,"Восстановление моста");
            GUI.Label(new Rect(box.x+14,box.y+36,390,60),$"Этап {bridge.Stage} / 3\n{bridge.NextCost}");
            if(Icon(new Rect(box.x+14,box.y+110,390,44),bridge.Complete?"Проход открыт":"Построить следующий этап",bridge.CanAfford?Color.green:Color.gray,bridge.CanAfford,"Ресурсы списываются сразу, мост открывается после третьего этапа"))bridge.TryBuildNext(GetComponent<PlayerProgression>());
            if(GUI.Button(new Rect(box.x+14,box.y+170,390,32),"Закрыть"))CloseInterface();return;
        }
        if(selected==null)return;
        if(Compact)
        {
            var rect=ToolbarRect;bool affordable=selected is MeatShop shop?shop.CanUpgrade:((AnimalFarm)selected).CanUpgrade;
            int price=selected is MeatShop sale?sale.UpgradeCost:((AnimalFarm)selected).UpgradeCost;
            if(Picture(new Rect(rect.x,rect.y,34,34),0,affordable?Color.green:Color.gray,affordable,$"Следующий уровень: {price} золота"))selected.TryUpgrade(0);
            if(Picture(new Rect(rect.x+37,rect.y,34,34),1,new Color(1,.35f,.3f),true,"Снести без возврата ресурсов"))
            {selected.Demolish(GetComponent<PlayerProgression>());CloseInterface();return;}
            if(Picture(new Rect(rect.x+74,rect.y,34,34),selected.IsOperating?2:3,selected.IsOperating?Color.green:Color.red,true,selected.IsOperating?"Приостановить":"Продолжить"))
                selected.SetOperating(GetComponent<PlayerProgression>(),!selected.IsOperating);
            if(!string.IsNullOrEmpty(GUI.tooltip))GUI.Box(new Rect(rect.x-60,rect.y+38,230,28),GUI.tooltip);
            return;
        }
        var panel=ScreenRect;GUI.Box(panel,selected.DisplayName);
        GUI.Label(new Rect(panel.x+12,panel.y+24,396,86),selected.Info+$"\nHP: {selected.CurrentHP:0}/{selected.MaximumHP:0}");
        for(int i=0;i<selected.UpgradeCount;i++)
            if(GUI.Button(new Rect(panel.x+12,panel.y+116+i*39,396,35),selected.UpgradeLabel(i)))selected.TryUpgrade(i);
        if(selected is Barracks barracks)
        {
            var production=new Rect(panel.x+12,panel.y+356,396,44);
            GUI.Box(production,$"Воин: ЛКМ — в очередь · ПКМ — конвейер\nОчередь: {barracks.QueueCount} · Конвейер: {(barracks.AutoProduction?"ВКЛ":"ВЫКЛ")}",GUI.skin.button);
            var evt=Event.current;
            if(evt.type==EventType.MouseDown&&production.Contains(evt.mousePosition))
            {if(evt.button==0)barracks.TryQueue();if(evt.button==1)barracks.SetProduction(!barracks.AutoProduction);evt.Use();}
            bool ready=RiverBridge.OwnComplete(barracks.Owner);
            if(Icon(new Rect(panel.x+12,panel.y+406,396,34),ready?$"Отправка на другой берег: {(barracks.SendToEnemy?"ВКЛ":"ВЫКЛ")}":"Для отправки восстановите свой мост",ready?Color.white:Color.gray,ready,"Ручные приказы имеют приоритет"))
                barracks.SetSending(!barracks.SendToEnemy);
            var stats=barracks.Upgrades;
            GUI.Label(new Rect(panel.x+12,panel.y+448,396,72),$"Следующие: {stats.Health:0} кг · урон {stats.Damage:0} · радиус {stats.Range:0.0} м\nУдар {stats.AttackPeriod:0.###} с · яд {stats.Poison:0} кг/с × 10 с\nКрит {stats.CriticalChance*100:0.##}% ×2");
        }
        if(GUI.Button(new Rect(panel.x+320,panel.y+panel.height-42,88,30),"Закрыть"))CloseInterface();
        if(selected!=null&&selected.Kind!=BuildingKind.Factory&&GUI.Button(new Rect(panel.x+12,panel.y+panel.height-42,300,30),"Снести без возврата"))
        {selected.Demolish(GetComponent<PlayerProgression>());CloseInterface();}
    }
}
