using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CozyFeedbackAuthoring
{
    [MenuItem("Catastrophe/Cozy Feedback/1 Export Gentle Sounds")]
    public static void Export()
    {
        const string folder="Assets/Resources/Audio/Cozy";
        Directory.CreateDirectory(folder);Directory.CreateDirectory("Validation/CozyFeedback");
        var mix=new float[CozyCatSoundDesign.SampleRate*5];int index=0;
        foreach(CozyCatSoundDesign.Effect effect in Enum.GetValues(typeof(CozyCatSoundDesign.Effect)))
        {
            var samples=CozyCatSoundDesign.Samples(effect);string path=folder+"/"+effect+".wav";
            WriteWav(path,samples);
            // Same quiet levels as the live game; leave space between each sound.
            float volume=.75f*(index==0?.56f:index==1?.62f:.58f);
            int offset=(int)((.3f+index*1.4f)*CozyCatSoundDesign.SampleRate);
            for(int i=0;i<samples.Length;i++)mix[offset+i]=samples[i]*volume;
            index++;
        }
        WriteWav("Validation/CozyFeedback/SoftPush-And-CuteDrops.wav",mix);
        AssetDatabase.Refresh();
        foreach(CozyCatSoundDesign.Effect effect in Enum.GetValues(typeof(CozyCatSoundDesign.Effect)))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(folder+"/"+effect+".wav");
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
        }
        Debug.Log("COZY_FEEDBACK_EXPORTED: gentle paw slide, wooden drop and porcelain clink; original WAV assets and listening preview saved.");
    }
    public static void WriteWav(string path,float[] samples)
    {
        using(var w=new BinaryWriter(File.Create(path)))
        {
            int bytes=samples.Length*2;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
            w.Write(CozyCatSoundDesign.SampleRate);w.Write(CozyCatSoundDesign.SampleRate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);
            foreach(float sample in samples)w.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
        }
    }
}
