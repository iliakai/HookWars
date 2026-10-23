using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenuController : MonoBehaviour
{
    public const string MenuScene="MainMenu",BattleScene="Arena25D";
    private string number;
    private int sliderValue;
    private bool loading;
    private GUIStyle title,label,small,button,numberStyle;
    public string DepthText=>number;
    public bool Loading=>loading;
    public bool IsValid=>int.TryParse(number,out int depth)&&MatchConfiguration.Valid(depth);
    private void Awake(){Time.timeScale=1;sliderValue=MatchConfiguration.LastDepth;number=sliderValue.ToString();}
    public void SetDepthText(string text)
    {
        number=text;
        if(int.TryParse(number,out int depth)&&MatchConfiguration.Valid(depth))
        {sliderValue=depth;PlayerPrefs.SetInt(MatchConfiguration.SavedDepth,depth);}
    }
    public bool StartBattle()
    {
        if(loading||!int.TryParse(number,out int depth)||!MatchConfiguration.Prepare(depth))return false;
        loading=true;Time.timeScale=1;
        SceneManager.LoadSceneAsync(BattleScene,LoadSceneMode.Single);return true;
    }
    private void OnGUI()
    {
        title ??= new GUIStyle(GUI.skin.label){fontSize=52,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        label ??= new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleLeft};
        small ??= new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        button ??=new GUIStyle(GUI.skin.button){fontSize=22};
        numberStyle ??=new GUIStyle(GUI.skin.textField){fontSize=22,alignment=TextAnchor.MiddleCenter};
        Matrix4x4 previous=GUI.matrix;
        float scale=Mathf.Min(Screen.width/960f,Screen.height/640f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-960*scale)/2,(Screen.height-640*scale)/2,0),Quaternion.identity,Vector3.one*scale);
        GUI.Label(new Rect(100,48,760,80),"HookWars",title);
        GUI.Label(new Rect(180,132,600,36),"Бой на двух берегах",small);
        GUI.Box(new Rect(180,188,600,256),GUIContent.none);
        GUI.Label(new Rect(210,208,500,34),"Глубина каждого берега, м",label);
        float value=GUI.HorizontalSlider(new Rect(214,268,410,24),sliderValue,4,20);
        int integer=Mathf.RoundToInt(value);
        if(integer!=sliderValue)SetDepthText(integer.ToString());
        string edited=GUI.TextField(new Rect(654,255,90,38),number,8,numberStyle);
        if(edited!=number)SetDepthText(edited);
        GUI.Label(new Rect(210,310,540,36),$"Ширина реки {ArenaLayout.RiverWidth:0} м · длина вдоль реки {ArenaLayout.FieldHeight:0} м",small);
        GUI.Label(new Rect(210,350,540,36),IsValid?$"Общая ширина поля: 2 × {sliderValue} + {ArenaLayout.RiverWidth:0} = {2*sliderValue+ArenaLayout.RiverWidth:0} м":"Введите целое число от 4 до 20",small);
        GUI.enabled=IsValid&&!loading;
        if(GUI.Button(new Rect(270,466,420,54),loading?"Загрузка…":"Начать бой",button))StartBattle();
        GUI.enabled=!Application.isEditor&&!loading;
        if(GUI.Button(new Rect(350,540,260,44),"Выход",button))Application.Quit();
        GUI.enabled=true;GUI.matrix=previous;
    }
    private void OnDisable(){if(IsValid)PlayerPrefs.Save();}
}
