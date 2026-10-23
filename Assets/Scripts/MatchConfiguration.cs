using UnityEngine;

public static class MatchConfiguration
{
    public const int DefaultDepth=16,MinimumDepth=4,MaximumDepth=20;
    public const string SavedDepth="HookWars.BankDepth";
    private static int? nextDepth;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime(){nextDepth=null;Time.timeScale=1;}
    public static bool Valid(int value)=>value>=MinimumDepth&&value<=MaximumDepth;
    public static bool Prepare(int depth)
    {
        if(!Valid(depth))return false;
        nextDepth=depth;PlayerPrefs.SetInt(SavedDepth,depth);PlayerPrefs.Save();return true;
    }
    public static int ConsumeDepth(int directSceneDefault)
    {
        int depth=nextDepth??directSceneDefault;nextDepth=null;
        return Mathf.Clamp(depth,MinimumDepth,MaximumDepth);
    }
    public static int LastDepth=>Mathf.Clamp(PlayerPrefs.GetInt(SavedDepth,DefaultDepth),MinimumDepth,MaximumDepth);
}
