using System.Collections.Generic;
using UnityEngine;

// Game-time clock and one-second reward labels; no currency is awarded here.
[DisallowMultipleComponent]
public sealed class MatchHud25D : MonoBehaviour
{
    private struct Gain { public Vector3 position; public int amount; public double born; }
    private readonly List<Gain> gains=new List<Gain>();
    private GUIStyle clockStyle,gainStyle;
    private FogOfWar25D fog;
    public static MatchHud25D Instance {get;private set;}
    public static Rect ScreenRect=>new Rect(Screen.width*.5f-100,12,200,40);
    public double ElapsedSeconds {get;private set;}
    public int PopupCount=>gains.Count;
    public string ClockText=>FormatTime(ElapsedSeconds);
    public static string FormatTime(double seconds)
    {
        long whole=(long)System.Math.Max(0,seconds);
        return $"{whole/3600:00}:{whole/60%60:00}:{whole%60:00}";
    }
    private void Awake(){Instance=this;ElapsedSeconds=0;}
    private void Update()=>Advance(Time.deltaTime);
    public void Advance(double seconds)
    {
        if(MatchPause.BlocksGameplay||Time.timeScale<=0||!double.IsFinite(seconds)||seconds<=0)return;
        ElapsedSeconds+=seconds;
        gains.RemoveAll(g=>ElapsedSeconds-g.born>=1);
    }
    public void ShowGoldGain(Vector3 logicalPosition,int amount)
    {if(amount>0)gains.Add(new Gain{position=logicalPosition,amount=amount,born=ElapsedSeconds});}
    internal bool RewardVisible(Vector3 logicalPosition)
    {
        if(fog==null)fog=FindAnyObjectByType<FogOfWar25D>();
        return fog==null||fog.IsRevealed(logicalPosition);
    }
    private void OnGUI()
    {
        clockStyle ??= new GUIStyle(GUI.skin.label){fontSize=24,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        gainStyle ??= new GUIStyle(GUI.skin.label){fontSize=26,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        GUI.Box(ScreenRect,GUIContent.none);GUI.Label(ScreenRect,ClockText,clockStyle);
        var camera=Camera.main;if(camera==null)return;
        Color original=GUI.color;
        foreach(var gain in gains)
        {
            if(!RewardVisible(gain.position))continue;
            float progress=Mathf.Clamp01((float)(ElapsedSeconds-gain.born));
            Vector3 point=camera.WorldToScreenPoint(ArenaSpace25D.ToWorld(gain.position,1.35f));
            if(point.z<=0)continue;
            var rect=new Rect(point.x-70,Screen.height-point.y-progress*60,140,36);
            GUI.color=new Color(0,0,0,(1-progress)*.8f);GUI.Label(new Rect(rect.x+1,rect.y+2,rect.width,rect.height),"+"+gain.amount,gainStyle);
            GUI.color=new Color(1,.84f,.2f,1-progress);GUI.Label(rect,"+"+gain.amount,gainStyle);
        }
        GUI.color=original;
    }
    private void OnDestroy(){if(Instance==this)Instance=null;}
}
