using System.Linq;
using UnityEditor;
using UnityEngine;

// Current meat checks use the static rib steak and actual world integration.
public static class Meat25DChecks
{
    public const string Key=GameplayExpansionChecks.Key;
    [MenuItem("HookWars/25D/Meat/Check spinning drops Play Mode")]
    public static void Run()=>GameplayExpansionChecks.Run("world");
    public static bool SafeVisual(Transform view)=>view!=null&&view.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&
        view.GetComponentsInChildren<Animator>(true).All(a=>!a.applyRootMotion&&a.runtimeAnimatorController!=null&&
            a.runtimeAnimatorController.animationClips.All(c=>Butcher25DSetup.Matches(c.name,"Meat_Spin")&&c.events.Length==0));
}
