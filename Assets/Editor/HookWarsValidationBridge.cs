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
            string key = active == "launch" ? Launch25DChecks.Key : active == "chain" ? Chain25DChecks.Key : active == "animals" ? Animals25DChecks.Key : active == "world" ? World25DChecks.Key : active == "arena25d" ? "HookWars.25D.Pending" : active == "butcher" ? Butcher25DChecks.Key : "HookWars.CarryingChecks.Pending";
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
            if (request.operation == "butcher-inspect")
            {
                Butcher25DSetup.Inspect(); Write(request.operation, "passed", "See butcher-import.txt"); return;
            }
            if (request.operation == "butcher-setup")
            {
                Butcher25DSetup.Configure(); Write(request.operation, "passed", "Arena25D adapted butcher saved"); return;
            }
            if (request.operation != "launch" && request.operation != "chain" && request.operation != "animals" && request.operation != "world" && request.operation != "arena25d" && request.operation != "regressions" && request.operation != "butcher")
                throw new InvalidOperationException("Unsupported validation operation");
            SessionState.SetString(Active, request.operation);
            ValidationPlayScope.CurrentScene();
            SessionState.SetBool(Active + ".Background", Application.runInBackground);
            Write(request.operation, "running", "Running in existing Editor");
            if (request.operation == "launch") Launch25DChecks.Run();
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
