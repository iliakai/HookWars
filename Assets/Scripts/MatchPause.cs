using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-2000)]
public sealed class MatchPause : MonoBehaviour
{
    public static MatchPause Instance {get;private set;}
    public static bool BlocksGameplay=>Instance!=null&&(Instance.paused||Instance.leaving||Instance.MatchEnded);
    private bool paused,leaving;
    public bool IsPaused=>paused;
    public bool MatchEnded {get;private set;}
    public PlayerProgression DefeatedOwner {get;private set;}
    public void EndMatch(PlayerProgression defeated)
    {
        if(MatchEnded||defeated==null)return;
        MatchEnded=true;DefeatedOwner=defeated;
        foreach(var timing in FindObjectsByType<HeroActionTiming25D>())timing.CancelAll();
        foreach(var hook in FindObjectsByType<HookAbility>())hook.CancelHook();
        foreach(var chain in FindObjectsByType<HookChain25D>())chain.Clear();
        Time.timeScale=0;
    }
    private void Awake(){Instance=this;Time.timeScale=1;}
    private void Update(){if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)HandleEscape();}
    public void HandleEscape()
    {
        if(leaving||MatchEnded)return;
        if(paused){SetPaused(false);return;}
        var upgrades=FindAnyObjectByType<BuildingUpgradeMenu>();
        bool closed=upgrades!=null&&upgrades.CloseInterface();
        closed=(FindAnyObjectByType<HookUpgradeMenu>()?.CloseInterface()??false)||closed;
        if(!closed)SetPaused(true);
    }
    public void SetPaused(bool value){if(MatchEnded)return;paused=value;Time.timeScale=value?0:1;}
    public bool ReturnToMenu()
    {
        if(leaving)return false;
        leaving=true;
        foreach(var timing in FindObjectsByType<HeroActionTiming25D>())timing.CancelAll();
        foreach(var hook in FindObjectsByType<HookAbility>())hook.CancelHook();
        foreach(var chain in FindObjectsByType<HookChain25D>())chain.Clear();
        Time.timeScale=1;SceneManager.LoadSceneAsync(MainMenuController.MenuScene,LoadSceneMode.Single);return true;
    }
    private void OnGUI()
    {
        if(MatchEnded)
        {
            var result=new Rect((Screen.width-420)/2,(Screen.height-200)/2,420,200);
            GUI.Box(result,DefeatedOwner!=null&&DefeatedOwner.GetComponent<PlayerMovement>()!=null?"Поражение — ваш цех разрушен":"Победа — вражеский цех разрушен");
            if(GUI.Button(new Rect(result.x+45,result.y+80,330,50),"В главное меню"))ReturnToMenu();return;
        }
        if(!paused&&!leaving)return;
        GUI.Box(new Rect(0,0,Screen.width,Screen.height),GUIContent.none);
        var panel=new Rect((Screen.width-360)/2,(Screen.height-250)/2,360,250);
        GUI.Box(panel,"Пауза");GUI.enabled=!leaving;
        if(GUI.Button(new Rect(panel.x+30,panel.y+52,300,48),"Продолжить бой"))SetPaused(false);
        if(GUI.Button(new Rect(panel.x+30,panel.y+118,300,48),"В главное меню"))ReturnToMenu();
        GUI.Label(new Rect(panel.x+30,panel.y+190,300,34),"Esc — продолжить");GUI.enabled=true;
    }
    private void OnDestroy(){if(Instance==this)Instance=null;Time.timeScale=1;}
}
