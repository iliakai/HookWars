using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HookFlightAudioChecks
{
    public const string Key = "HookWars.FlightAudioChecks";
    static int phase, count;
    static double ready, deadline, resume;
    static float due, outputPeak;
    static HookAbility[] hooks;
    static HookFlightAudio[] sounds;
    static AudioSource[] sources;
    static HookFlightAudioChecks() { if (SessionState.GetBool(Key, false)) Subscribe(); }
    [MenuItem("HookWars/25D/Check hook flight sound Play Mode")]
    public static void Run()
    {
        ValidationPlayScope.CurrentScene(); EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Passed", false); Subscribe();
    }
    static void Subscribe()
    {
        phase = count = 0; due = outputPeak = 0; ready = EditorApplication.timeSinceStartup + 3; deadline = ready + 60;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); count++; Debug.Log("FLIGHT_AUDIO_CHECK: " + message); }
    static void Ready(HookAbility hook)
    { hook.CancelHook(); EconomyChecks.Set(hook, "nextThrowTime", 0f); }
    static void ReleaseNow(HookAbility hook)
    { Ready(hook); hook.TryThrow(Vector2.up); hook.GetComponent<HeroActionTiming25D>().Advance(Time.time + HeroActionTiming25D.HookRelease + .01f); }
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
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Flight audio timeout");
            if (phase == 0) { EditorApplication.isPlaying = true; phase = 1; return; }
            if (!EditorApplication.isPlaying || Time.time < due) return;
            if (phase == 1)
            {
                foreach (var ai in Object.FindObjectsByType<EnemyHookAI>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyChase>()) ai.enabled = false;
                foreach (var ai in Object.FindObjectsByType<EnemyMelee>()) ai.enabled = false;
                hooks = Object.FindObjectsByType<HookAbility>().Where(h => h.GetComponent<HeroActionTiming25D>() != null).ToArray();
                sounds = hooks.Select(h => h.GetComponent<HookFlightAudio>()).ToArray(); sources = hooks.Select(h => h.GetComponent<AudioSource>()).ToArray();
                Check(hooks.Length == 2 && sounds.All(s => s != null), "Player and Enemy both have flight audio");
                Check(Object.FindObjectsByType<AudioListener>().Length == 1, "Exactly one listener");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HookFlightAudioSetup.ClipPath);
                var pcm = new float[clip.samples * clip.channels];
                Check(clip.GetData(pcm, 0) && pcm.Max(v => Mathf.Abs(v)) > .001f, "Imported clip decodes to non-silent PCM");
                for (int i = 0; i < hooks.Length; i++)
                {
                    var hook = hooks[i]; var timing = hook.GetComponent<HeroActionTiming25D>();
                    Check(hook.GetComponents<AudioSource>().Length == 1 && !sources[i].playOnAwake && sources[i].spatialBlend == 0,
                        "One non-autoplay 2D source, audible independently of elevated camera: " + i);
                    Ready(hook); hook.TryThrow(Vector2.zero);
                    Check(sounds[i].PlaybackCount == 0, "Rejected zero-direction command is silent: " + i);
                    hook.TryThrow(Vector2.up); hook.TryThrow(Vector2.up);
                    Check(timing.HookPending && sounds[i].PlaybackCount == 0, "Windup and repeated input make no premature sound: " + i);
                    hook.GetComponent<Health>().TakeDamage(100000); timing.Advance(Time.time + 1); hook.GetComponent<Health>().Revive();
                    // This fixture revives immediately instead of waiting for the normal
                    // respawn coroutine, which normally restores the player's controls.
                    hook.GetComponent<PlayerRespawn>()?.StopAllCoroutines();
                    Object.FindAnyObjectByType<TrainingMatch>()?.StopAllCoroutines();
                    hook.enabled = true;
                    Check(sounds[i].PlaybackCount == 0, "Death before release stays silent: " + i);
                    Ready(hook); hook.TryThrow(Vector2.up);
                    var victim = hook.GetComponent<HookVictim>(); victim.TryCapture(hooks[1-i]); timing.Advance(Time.time + 1); victim.Release(hooks[1-i]);
                    Check(sounds[i].PlaybackCount == 0, "Capture before release stays silent: " + i);
                    // Remove test deaths' corpses from the muzzle so this audio
                    // playback check gets an unobstructed flight rather than instant pickup.
                    foreach (var corpse in Object.FindObjectsByType<Carcass>()) Object.DestroyImmediate(corpse.gameObject);
                    Ready(hook); hook.TryThrow(Vector2.up); hook.TryThrow(Vector2.up);
                }
                phase = 2; due = Time.time + .33f; return;
            }
            if (phase == 2)
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    Check(hooks[i].ActiveHook != null && sounds[i].PlaybackCount == 1,
                        $"Exactly one sound at actual animated release: {i}, count={sounds[i].PlaybackCount}, active={hooks[i].ActiveHook!=null}, pending={hooks[i].IsPreparingThrow}, enabled={hooks[i].enabled}");
                    Check(sources[i].isPlaying && sources[i].pitch >= .93f && sources[i].pitch <= 1.07f && sources[i].volume >= .6f && sources[i].volume <= .75f,
                        "Real audio voice playing with bounded pitch and volume: " + i);
                    var samples = new float[1024]; sources[i].GetOutputData(samples, 0);
                    outputPeak = Mathf.Max(outputPeak, samples.Max(v => Mathf.Abs(v)));
                }
                MatchPause.Instance.SetPaused(true); resume = EditorApplication.timeSinceStartup + .25; phase = 3; return;
            }
            if (phase == 3)
            {
                if (EditorApplication.timeSinceStartup < resume) return;
                Check(sources.All(s => !s.isPlaying), "Match pause pauses flight sounds");
                MatchPause.Instance.SetPaused(false); phase = 4; due = Time.time + .06f; return;
            }
            if (phase == 4)
            {
                Check(sources.All(s => s.isPlaying), "Resume continues paused sounds");
                for (int i = 0; i < hooks.Length; i++)
                {
                    float pitch = sources[i].pitch; bool changed = false;
                    string rng = JsonUtility.ToJson(UnityEngine.Random.state);
                    for (int j = 0; j < 6; j++) { ReleaseNow(hooks[i]); changed |= Mathf.Abs(pitch - sources[i].pitch) > .00001f; }
                    Check(changed && sounds[i].PlaybackCount == 7, "Repeated releases vary naturally and never double-trigger: " + i);
                    Check(rng == JsonUtility.ToJson(UnityEngine.Random.state), "Audio variation leaves gameplay random state untouched: " + i);
                    hooks[i].CancelHook();
                }
                phase = 5; due = Time.time + .06f; return;
            }
            if (phase == 5)
            {
                Check(sources.All(s => !s.isPlaying), "Cancelled/returned hook leaves no playing voice");
                sounds[0].enabled = false; ReleaseNow(hooks[0]);
                Check(sounds[0].PlaybackCount == 7 && !sources[0].isPlaying, "Disabling audio unsubscribes and stops its source");
                sounds[0].enabled = true; ReleaseNow(hooks[0]);
                Check(sounds[0].PlaybackCount == 8, "Re-enable subscribes exactly once");
                File.WriteAllText("docs/validation/015-flight-audio.txt", $"PASS: {count} actual Unity Play Mode assertions\nBoth heroes; decoded PCM; one 2D source each; rejected/windup/death/capture silence; release exactly once; real playback; variation and independent RNG; pause/resume; cancel; disable/re-enable.\nMeasured AudioSource output peak: {outputPeak:F6}\nSpeaker/headphone listening and subjective volume balance not performed by the agent.\n");
                SessionState.SetBool(Key + ".Passed", true); Debug.Log("HOOKWARS_FLIGHT_AUDIO_PASS: " + count); Stop();
            }
        }
        catch (Exception error) { Debug.LogError("HOOKWARS_FLIGHT_AUDIO_FAIL: " + error); Stop(); }
    }
    static void Stop() { phase = 99; EditorApplication.isPlaying = false; }
}
