using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// File requests run inside the editor already opened by Unity Hub. Never starts
// another editor, closes the editor, or runs the external butcher demo builder.
[InitializeOnLoad]
public static class HookWarsValidationBridge
{
    private const string DirectoryPath = "Temp/HookWarsAutomation";
    private const string Active = "HookWars.Automation.Active";
    [Serializable] private class Request { public string operation; }
    [Serializable] private class Result
    {
        public string operation, status, detail, scene;
        public bool playing;
    }
    private static double stableAfter;
    static HookWarsValidationBridge()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;
        stableAfter = EditorApplication.timeSinceStartup + 3;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Active, ""))) return;
        if (message.Contains("CHECK") || message.Contains("HOOKWARS") || type == LogType.Error || type == LogType.Exception)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(DirectoryPath + "/log.txt", type + ": " + message + "\n" +
                (type == LogType.Exception ? stack + "\n" : ""));
        }
    }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { stableAfter = EditorApplication.timeSinceStartup + 3; return; }
        if (EditorApplication.timeSinceStartup < stableAfter) return;
        string active = SessionState.GetString(Active, "");
        if (active != "")
        {
            if (EditorApplication.isPlaying)
            {
                Application.runInBackground = true;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            string key = active.StartsWith("chain-rock")?ChainRockChecks.Key:active.StartsWith("expansion-")?GameplayExpansionChecks.Key:active=="flight-audio"?HookFlightAudioChecks.Key:active=="river-preview"?River25DPreview.Key:active=="income"?IncomeHUDChecks.Key:active=="meat"?Meat25DChecks.Key:active.StartsWith("revision-") ? GameplayRevisionChecks.Key : active == "acceptance" ? MatchAcceptance25DChecks.Key : active == "launch" ? Launch25DChecks.Key : active == "chain" ? Chain25DChecks.Key : active == "animals" ? Animals25DChecks.Key : active == "world" ? World25DChecks.Key : active == "arena25d" ? "HookWars.25D.Pending" : active == "butcher" ? Butcher25DChecks.Key : "HookWars.CarryingChecks.Pending";
            if (SessionState.GetBool(key, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Application.runInBackground = SessionState.GetBool(Active + ".Background", false);
            Write(active, SessionState.GetBool(key + ".Passed", false) ? "passed" : "failed", "See log.txt");
            SessionState.SetString(Active, ""); return;
        }
        string path = DirectoryPath + "/request.json";
        if (!File.Exists(path)) return;
        try
        {
            Request request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            if (request.operation != "status" && EditorApplication.isPlayingOrWillChangePlaymode) return;
            // External edits must be imported before invoking a previously compiled
            // test class. Retain the request across any compilation/domain reload.
            string importToken = File.GetLastWriteTimeUtc(path).Ticks.ToString();
            if (request.operation != "status" && SessionState.GetString(Active + ".Imported", "") != importToken)
            {
                SessionState.SetString(Active + ".Imported", importToken);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                stableAfter = EditorApplication.timeSinceStartup + 3; return;
            }
            File.Delete(path);
            if (request.operation == "status") { Write("status", "ready", Application.dataPath); return; }
            if(request.operation=="expansion-meat-setup")
            {ExpansionAssetsSetup.ConfigureMeat();Write(request.operation,"passed","New rib steak configured");return;}
            if(request.operation=="expansion-anchor-setup")
            {ExpansionAssetsSetup.ConfigureAnchors();Write(request.operation,"passed","Five anchor prefab configured");return;}
            if(request.operation=="expansion-prune")
            {ExpansionAssetsSetup.PruneRetiredMeat();Write(request.operation,"passed","Retired meat dependency audit and GUID verification passed");return;}
            if(request.operation=="flight-audio-setup")
            {HookFlightAudioSetup.Configure();Write(request.operation,"passed","Hook flight audio configured");return;}
            if(request.operation=="grass-setup")
            {Grass25DSetup.Configure();Write(request.operation,"passed","4m grass texture configured");return;}
            if(request.operation=="river-setup")
            {River25DSetup.Configure();Write(request.operation,"passed","Flowing river configured");return;}
            if(request.operation=="meat-setup")
            {Meat25DSetup.Configure();Write(request.operation,"passed","Hovering meat with authored spin configured");return;}
            if(request.operation=="meat-build")
            {Write(request.operation,"running","Building updated Windows player");WindowsDevelopment25D.Build("010");Write(request.operation,"passed","See docs/validation/010-windows-build.txt");return;}
            if(request.operation=="meat-verify")
            {Legacy25DCleanup.Verify("docs/validation/010-assets.txt");Write(request.operation,"passed","Meat assets and GUIDs verified");return;}
            if(request.operation=="revision-setup")
            {GameplayRevisionSetup.Configure();Write(request.operation,"passed","Starting factories and permanent rocks configured");return;}
            if(request.operation=="revision-verify")
            {Legacy25DCleanup.Verify("docs/validation/009-assets.txt");Write(request.operation,"passed","No broken GUIDs or missing scripts");return;}
            if(request.operation=="windows-build")
            {
                Write(request.operation,"running","Building in the existing GUI Editor");
                WindowsDevelopment25D.Build();Write(request.operation,"passed","See docs/validation/021-windows-build.txt");return;
            }
            if (request.operation == "body-measure")
            {
                World25DDiagnostics.MeasureBody(); Write(request.operation, "passed", "See body-measurement.txt"); return;
            }
            if (request.operation == "buildings-setup")
            {
                WorldModels25DSetup.ConfigureBuildings(); Write(request.operation,"passed","Building models configured"); return;
            }
            if (request.operation == "animals-setup")
            {
                Animals25DSetup.Configure(); Write(request.operation,"passed","Animal models configured"); return;
            }
            if (request.operation == "chain-setup")
            {
                Chain25DSetup.Configure();Write(request.operation,"passed","Physical chain configured");return;
            }
            if (request.operation == "launch-setup")
            {
                Launch25DSetup.Configure();Write(request.operation,"passed","MainMenu and active build scenes configured");return;
            }
            if(request.operation=="cleanup-migrate")
            {
                Legacy25DCleanup.Migrate();Write(request.operation,"passed","Logical surfaces and 3D-only components migrated");return;
            }
            if(request.operation=="cleanup-audit"||request.operation=="cleanup-delete")
            {
                Legacy25DCleanup.Audit(request.operation=="cleanup-delete");Write(request.operation,"passed","See docs/validation/008-legacy-asset-audit.txt");return;
            }
            if(request.operation=="cleanup-verify")
            {
                Legacy25DCleanup.Migrate();Legacy25DCleanup.Verify();Write(request.operation,"passed","No obsolete world graphics or broken GUIDs");return;
            }
            if (request.operation == "butcher-inspect")
            {
                Butcher25DSetup.Inspect(); Write(request.operation, "passed", "See butcher-import.txt"); return;
            }
            if (request.operation == "butcher-setup")
            {
                Butcher25DSetup.Configure(); Write(request.operation, "passed", "Arena25D adapted butcher saved"); return;
            }
            if (!request.operation.StartsWith("chain-rock")&&!request.operation.StartsWith("expansion-")&&request.operation!="flight-audio"&&request.operation!="river-preview"&&request.operation!="income"&&request.operation!="meat"&&!request.operation.StartsWith("revision-") && request.operation != "acceptance" && request.operation != "launch" && request.operation != "chain" && request.operation != "animals" && request.operation != "world" && request.operation != "arena25d" && request.operation != "regressions" && request.operation != "butcher")
                throw new InvalidOperationException("Unsupported validation operation");
            SessionState.SetString(Active, request.operation);
            ValidationPlayScope.CurrentScene();
            SessionState.SetBool(Active + ".Background", Application.runInBackground);
            Write(request.operation, "running", "Running in existing Editor");
            if(request.operation.StartsWith("chain-rock"))ChainRockChecks.Run(request.operation=="chain-rock-before");
            else if(request.operation.StartsWith("expansion-"))GameplayExpansionChecks.Run(request.operation.Substring(10));
            else if(request.operation=="flight-audio")HookFlightAudioChecks.Run();
            else if(request.operation=="river-preview")River25DPreview.Run();
            else if(request.operation=="income")IncomeHUDChecks.Run();
            else if(request.operation=="meat")Meat25DChecks.Run();
            else if (request.operation.StartsWith("revision-")) GameplayRevisionChecks.Run(request.operation.Substring(9));
            else if (request.operation == "acceptance") MatchAcceptance25DChecks.Run();
            else if (request.operation == "launch") Launch25DChecks.Run();
            else if (request.operation == "chain") Chain25DChecks.Run();
            else if (request.operation == "animals") Animals25DChecks.Run();
            else if (request.operation == "world") World25DChecks.Run();
            else if (request.operation == "arena25d") Arena25DChecks.Run();
            else if (request.operation == "butcher") Butcher25DChecks.Run();
            else EconomyChecks.Run5();
        }
        catch (Exception error)
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode)ValidationPlayScope.Restore();
            Write(SessionState.GetString(Active, "request"), "failed", error.ToString());
            SessionState.SetString(Active, "");
        }
    }
    private static void Write(string operation, string status, string detail)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/result.json", JsonUtility.ToJson(new Result
        {
            operation = operation, status = status, detail = detail,
            scene = SceneManager.GetActiveScene().path, playing = EditorApplication.isPlaying
        }, true));
    }
}
