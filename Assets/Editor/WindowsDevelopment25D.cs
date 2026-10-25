using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsDevelopment25D
{
    [MenuItem("HookWars/25D/Launch/Build Windows development")]
    public static void Build()=>Build("021");
    public static void Build(string revision)
    {
        if(revision!="009"&&revision!="010"&&revision!="012"&&revision!="013"&&revision!="014"&&revision!="015"&&revision!="016"&&revision!="017"&&revision!="019"&&revision!="020"&&revision!="021")throw new ArgumentException("Unknown build report revision");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before building");
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new InvalidOperationException("Windows module is unavailable");
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)throw new InvalidOperationException("Active profile is not Windows x64");
        var profile=BuildProfile.GetActiveBuildProfile();
        string[] scenes=(profile!=null&&profile.overrideGlobalScenes?profile.scenes:EditorBuildSettings.globalScenes).Where(s=>s.enabled).Select(s=>s.path).ToArray();
        if(!scenes.SequenceEqual(new[]{Launch25DSetup.MenuPath,Arena25DSetup.ScenePath}))throw new InvalidOperationException("Unexpected active build scene list");
        Directory.CreateDirectory("Builds/WindowsDevelopment");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,target=BuildTarget.StandaloneWindows64,locationPathName="Builds/WindowsDevelopment/HookWars.exe",options=BuildOptions.Development});
        Directory.CreateDirectory("docs/validation");
        File.WriteAllLines($"docs/validation/{revision}-windows-build-warnings.txt",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning).Select(m=>m.content).Distinct());
        File.WriteAllText($"docs/validation/{revision}-windows-build.txt",$"Unity {Application.unityVersion}\nProfile={(profile==null?"Windows platform profile (globalScenes)":profile.name)}\nScenes={string.Join(", ",scenes)}\nResult={report.summary.result}\nErrors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}; bytes={report.summary.totalSize}\nOutput=Builds/WindowsDevelopment/HookWars.exe\n");
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Windows build failed: "+report.summary.result);
        Debug.Log("HOOKWARS_WINDOWS_BUILD_PASS: "+report.summary.totalSize+" bytes");
    }
}
