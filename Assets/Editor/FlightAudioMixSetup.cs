using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FlightAudioMixSetup
{
    public const string StartPath="Assets/Audio/ChainFlightStart.mp3",EndPath="Assets/Audio/ChainFlight.mp3";
    public const string OutputPath="Assets/Audio/ChainFlightMix.wav";
    public static void Build()
    {
        var first=Read(StartPath);var last=Read(EndPath);
        const int frequency=44100;
        float prefix=Mathf.Min(.70f,first.clip.length),suffix=Mathf.Min(1.10f,last.clip.length),fade=Mathf.Min(.12f,Mathf.Min(prefix,suffix));
        int a=Mathf.RoundToInt(prefix*frequency),b=Mathf.RoundToInt(suffix*frequency),overlap=Mathf.RoundToInt(fade*frequency);
        var mixed=new float[a+b-overlap];
        for(int i=0;i<a;i++)mixed[i]=Sample(first.clip,first.pcm,(double)i/frequency);
        for(int i=0;i<b;i++)
        {
            float sample=Sample(last.clip,last.pcm,last.clip.length-suffix+(double)i/frequency);
            int index=a-overlap+i;float blend=i<overlap?(float)i/Mathf.Max(1,overlap-1):1;
            mixed[index]=i<overlap?Mathf.Lerp(mixed[index],sample,blend):sample;
        }
        float peak=0;foreach(float sample in mixed)peak=Mathf.Max(peak,Mathf.Abs(sample));
        if(peak<.001f)throw new InvalidOperationException("Flight mix is silent");float gain=.9f/peak;
        using(var file=new BinaryWriter(File.Create(OutputPath)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+mixed.Length*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(frequency);file.Write(frequency*2);file.Write((short)2);file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(mixed.Length*2);
            foreach(float sample in mixed)file.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample*gain,-1,1)*32767));
        }
        AssetDatabase.ImportAsset(OutputPath,ImportAssetOptions.ForceUpdate);
        File.WriteAllText("docs/validation/016-audio-mix.txt",$"Source start: 1ce83086b51f4fb ({first.clip.length:F3}s, {first.clip.frequency}Hz, {first.clip.channels} channels)\nSource end: 3c40cf014c0b451 ({last.clip.length:F3}s, {last.clip.frequency}Hz, {last.clip.channels} channels)\nFirst {prefix:F2}s of start + last {suffix:F2}s of end, crossfade {fade:F2}s\nMix duration={(double)mixed.Length/frequency:F3}s; PCM16 mono 44100Hz, peak normalized to .9. Existing pitch/volume variation retained.\n");
    }
    static (AudioClip clip,float[] pcm) Read(string path)
    {
        AssetDatabase.ImportAsset(path);var importer=(AudioImporter)AssetImporter.GetAtPath(path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;
        importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var pcm=new float[clip.samples*clip.channels];
        if(!clip.GetData(pcm,0))throw new InvalidOperationException("Cannot decode "+path);return(clip,pcm);
    }
    static float Sample(AudioClip clip,float[] pcm,double seconds)
    {
        double frame=Math.Max(0,Math.Min(clip.samples-1,seconds*clip.frequency));int a=(int)frame,b=Math.Min(clip.samples-1,a+1);float value=0;
        for(int channel=0;channel<clip.channels;channel++)value+=Mathf.Lerp(pcm[a*clip.channels+channel],pcm[b*clip.channels+channel],(float)(frame-a));
        return value/clip.channels;
    }
}
