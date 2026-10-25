using System;
using System.IO;
using UnityEngine;

public static class MatchConfiguration
{
    public const int DefaultDepth=16,MinimumDepth=12,MaximumDepth=36;
    public const string SavedDepth="HookWars.BankDepth";
    private static int? nextDepth;
    private static int? rememberedDepth;
    private static string PreferenceFile=>string.IsNullOrWhiteSpace(Application.persistentDataPath)?null:Path.Combine(Application.persistentDataPath,"bank-depth.txt");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime(){nextDepth=null;rememberedDepth=null;Time.timeScale=1;}
    public static bool Valid(int value)=>value>=MinimumDepth&&value<=MaximumDepth;
    public static bool Prepare(int depth)
    {
        if(!Valid(depth))return false;
        nextDepth=depth;RememberDepth(depth);return true;
    }
    public static int ConsumeDepth(int directSceneDefault)
    {
        int depth=nextDepth??directSceneDefault;nextDepth=null;
        return Mathf.Clamp(depth,MinimumDepth,MaximumDepth);
    }
    public static void RememberDepth(int depth)
    {
        if(!Valid(depth))throw new ArgumentOutOfRangeException(nameof(depth));
        rememberedDepth=depth;
        string path=PreferenceFile;
        if(path==null){Debug.LogWarning("Persistent data directory is unavailable; bank depth is remembered for this process only.");return;}
        try{Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(path,depth.ToString(System.Globalization.CultureInfo.InvariantCulture));}
        catch(Exception error) when(error is IOException||error is UnauthorizedAccessException)
        {Debug.LogWarning("Cannot persist bank depth; current choice remains valid for this session: "+error.Message);}
    }
    public static int LastDepth
    {
        get
        {
            if(rememberedDepth.HasValue)return rememberedDepth.Value;
            try
            {
                if(File.Exists(PreferenceFile))return int.TryParse(File.ReadAllText(PreferenceFile),out int stored)&&Valid(stored)?stored:DefaultDepth;
                // Read-only migration of an earlier editor/player preference.
                int legacy=PlayerPrefs.GetInt(SavedDepth,DefaultDepth);return Valid(legacy)?legacy:DefaultDepth;
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is PlayerPrefsException)
            {return DefaultDepth;}
        }
    }
}
