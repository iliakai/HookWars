using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HookFlightAudioSetup
{
    public const string ClipPath = FlightAudioMixSetup.OutputPath;
    [MenuItem("HookWars/25D/Setup hook flight sound")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        FlightAudioMixSetup.Build();
        AssetDatabase.ImportAsset(ClipPath);
        var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
        if (importer == null) throw new InvalidOperationException("Missing chain flight clip");
        importer.forceToMono = false; importer.loadInBackground = false;
        var samples = importer.defaultSampleSettings;
        samples.loadType = AudioClipLoadType.DecompressOnLoad;
        samples.compressionFormat = AudioCompressionFormat.PCM;
        samples.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        samples.preloadAudioData = true;
        importer.defaultSampleSettings = samples; importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        var scene = EditorSceneManager.OpenScene(Arena25DSetup.ScenePath);
        foreach (var hook in Object.FindObjectsByType<HookAbility>())
        {
            var source = hook.GetComponent<AudioSource>();
            if (source == null) source = hook.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.dopplerLevel = 0;
            var audio = hook.GetComponent<HookFlightAudio>();
            if (audio == null) audio = hook.gameObject.AddComponent<HookFlightAudio>();
            var data = new SerializedObject(audio);
            data.FindProperty("flightClip").objectReferenceValue = clip;
            data.FindProperty("pitchRange").vector2Value = new Vector2(.93f, 1.07f);
            data.FindProperty("volumeRange").vector2Value = new Vector2(.6f, .75f);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        var pcm = new float[clip.samples * clip.channels]; float peak = 0; double energy = 0;
        if (clip.GetData(pcm, 0)) foreach (float sample in pcm) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
        var mute = typeof(EditorUtility).GetProperty("audioMasterMute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        File.WriteAllText("docs/validation/016-audio-import.txt", $"Clip: {clip.name}\nDuration: {clip.length:F3}s; channels: {clip.channels}; frequency: {clip.frequency}Hz\nPCM, DecompressOnLoad, preload, no background loading; 2D sources on both logical heroes.\nDecoded peak: {peak:F6}; RMS: {Math.Sqrt(energy / pcm.Length):F6}\nEditor audioMasterMute: {mute?.GetValue(null)?.ToString() ?? "unavailable"}; AudioListener volume: {AudioListener.volume}; paused: {AudioListener.pause}\n");
        Debug.Log("HOOKWARS_FLIGHT_AUDIO_SETUP: " + clip.length + " seconds");
    }
}
