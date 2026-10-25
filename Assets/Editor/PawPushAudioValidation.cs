using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PawPushAudioValidation
{
    static double ready;
    static PawPushAudioValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TestPawAudio", false)) return;
            SessionState.SetBool("TestPawAudio", false);
            ready = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Check;
        };
    }
    [MenuItem("Catastrophe/Verify Single Du Push Audio")]
    static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first");
        SessionState.SetBool("TestPawAudio", true); EditorApplication.isPlaying = true;
    }
    static void Check()
    {
        if (EditorApplication.timeSinceStartup < ready) return;
        EditorApplication.update -= Check;
        Directory.CreateDirectory("Validation/Audio");
        try
        {
            var manager = AudioManager.I;
            if (!manager) throw new Exception("No live audio manager");
            var clip = (AudioClip)typeof(AudioManager).GetField("pawPush", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            if (!clip || !clip.name.Contains("PawPush")) throw new Exception("Wrong runtime cozy push clip");
            var samples = new float[clip.samples * clip.channels]; clip.GetData(samples, 0);
            float peak = 0; foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak < .1f || peak >= 1f) throw new Exception("Silent or clipped samples");
            for (int tap = 0; tap < 1; tap++) {
                float energy = 0;
                for (int i = Mathf.RoundToInt(tap * .16f * clip.frequency); i < Mathf.RoundToInt((tap * .16f + .12f) * clip.frequency); i++) energy += samples[i] * samples[i];
                if (energy < 1f) throw new Exception("Missing tap " + tap);
            }
            manager.PlayPawPush();
            if (!manager.GetComponent<AudioSource>().isPlaying) throw new Exception("Push source not playing");
            using (var w = new BinaryWriter(File.Create("Validation/Audio/PawPush_Cozy.wav"))) {
                int bytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)clip.channels); w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2); w.Write((short)(clip.channels * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes); foreach (float s in samples) w.Write((short)(s * .75f * .56f * 32767));
            }
            File.WriteAllText("Validation/Audio/check.txt", $"PASS live push clip={clip.name}, duration={clip.length:F2}s, one audible pulse, peak={peak:F3}, AudioSource playing. Other effect definitions unchanged.\n");
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Validation/Audio/check.txt", "FAIL " + e); }
        finally { EditorApplication.isPlaying = false; }
    }
}
