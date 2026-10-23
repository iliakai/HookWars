using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ValidationPlayScope
{
    private const string Key="HookWars.ValidationPlayScope";
    static ValidationPlayScope(){EditorApplication.playModeStateChanged+=Changed;}
    public static void CurrentScene()
    {
        if(!SessionState.GetBool(Key,false))
        {
            SessionState.SetString(Key+".Path",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key,true);
        }
        EditorSceneManager.playModeStartScene=null;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        Restore();
    }
    public static void Restore()
    {
        if(!SessionState.GetBool(Key,false))return;
        string path=SessionState.GetString(Key+".Path","");
        EditorSceneManager.playModeStartScene=path==""?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.SetBool(Key,false);
    }
}
