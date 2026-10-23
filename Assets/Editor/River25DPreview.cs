using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Captures actual rendered frames and checks flow direction/pause in the existing Editor.
[InitializeOnLoad]
public static class River25DPreview
{
    public const string Key = "HookWars.RiverPreview";
    static int phase;
    static double ready, deadline, resume;
    static float due, firstOffset, pausedOffset;
    static Renderer water;
    static River25DPreview() { if (SessionState.GetBool(Key, false)) Subscribe(); }
    [MenuItem("HookWars/25D/Preview river flow Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene(); EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); Subscribe();
    }
    static void Subscribe()
    {
        phase = 0; due = 0; ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 45;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static float Offset()
    {
        var block = new MaterialPropertyBlock(); water.GetPropertyBlock(block); return block.GetVector("_BaseMap_ST").w;
    }
    static void Capture(string label)
    {
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        ScreenCapture.CaptureScreenshot("docs/validation/014-river-" + label + ".png");
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < ready) return;
            if (phase == 99)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; }
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("River preview timeout");
            if (phase == 0) { EditorApplication.isPlaying = true; phase = 1; return; }
            if (!EditorApplication.isPlaying || Time.time < due) return;
            if (phase == 1)
            {
                foreach (var ai in Object.FindObjectsByType<EnemyHookAI>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyChase>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyMelee>()) ai.enabled = false;
                var view = Object.FindAnyObjectByType<ArenaPresentation25D>();
                var river = view.transform.Find("Generated 25D visuals/river");
                if (river == null) return;
                water = river.GetComponent<Renderer>();
                if (water.sharedMaterial.mainTexture == null) throw new InvalidOperationException("River texture not assigned");
                Capture("a"); firstOffset = Offset(); phase = 2; due = Time.time + 1; return;
            }
            if (phase == 2)
            {
                Capture("b"); float change = Mathf.Repeat(Offset() - firstOffset, 1);
                if (change < .1f || change > .3f) throw new InvalidOperationException("River texture did not advance at expected speed");
                var mesh = water.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.uv[1].y <= mesh.uv[0].y || mesh.vertices[1].z <= mesh.vertices[0].z)
                    throw new InvalidOperationException("River UV direction inverted");
                var camera = Camera.main;
                if (camera.WorldToScreenPoint(Vector3.back).y >= camera.WorldToScreenPoint(Vector3.zero).y)
                    throw new InvalidOperationException("River world flow does not project down the screen");
                due = Time.time + .15f; phase = 3; return;
            }
            if (phase == 3)
            {
                MatchPause.Instance.SetPaused(true); pausedOffset = Offset();
                resume = EditorApplication.timeSinceStartup + .4; phase = 4; return;
            }
            if (phase == 4)
            {
                if (EditorApplication.timeSinceStartup < resume) return;
                if (Mathf.Abs(Offset() - pausedOffset) > .00001f) throw new InvalidOperationException("River flows during pause");
                MatchPause.Instance.SetPaused(false);
                File.WriteAllText("docs/validation/014-river.txt", "PASS: actual Unity Play Mode frames captured; imported river texture assigned; UV offset advances; actual mesh V follows +Z; world -Z projects down; flow freezes during pause.\nFrames: 014-river-a.png and 014-river-b.png, approximately one game second apart.\n");
                SessionState.SetBool(Key + ".Passed", true); Debug.Log("HOOKWARS_RIVER_PREVIEW_PASS"); Stop();
            }
        }
        catch (Exception error) { Debug.LogError("HOOKWARS_RIVER_PREVIEW_FAIL: " + error); Stop(); }
    }
    static void Stop() { phase = 99; EditorApplication.isPlaying = false; }
}
