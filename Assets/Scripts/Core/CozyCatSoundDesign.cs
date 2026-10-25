using System;
using UnityEngine;

// Original, deterministic little Foley-like effects. No external recordings.
public static class CozyCatSoundDesign
{
    public const int SampleRate=44100;
    public enum Effect { PawPush, ItemDrop, PorcelainDrop }
    public static string Name(Effect effect)=>effect.ToString();
    public static AudioClip LoadOrCreate(Effect effect)
    {
        var saved=Resources.Load<AudioClip>("Audio/Cozy/"+Name(effect));
        if(saved)return saved;
        var data=Samples(effect);
        var clip=AudioClip.Create("Cozy - "+Name(effect),data.Length,1,SampleRate,false);
        clip.SetData(data,0);return clip;
    }
    public static float[] Samples(Effect effect)
    {
        float duration=effect==Effect.PawPush?.26f:effect==Effect.ItemDrop?.45f:.68f;
        var data=new float[Mathf.CeilToInt(duration*SampleRate)];
        uint seed=0x7135A9u;float noise=0,low=0,dc=0,peak=0;
        float noiseAlpha=1-Mathf.Exp(-2*Mathf.PI*850/SampleRate);
        float filterAlpha=1-Mathf.Exp(-2*Mathf.PI*2800/SampleRate);
        float dcAlpha=1-Mathf.Exp(-2*Mathf.PI*35/SampleRate);
        for(int i=0;i<data.Length;i++)
        {
            float t=i/(float)SampleRate;seed=seed*1664525u+1013904223u;
            float white=(seed>>8)/8388607.5f-1;noise+=noiseAlpha*(white-noise);
            float sample;
            if(effect==Effect.PawPush)
            {
                // A cushioned paw contact followed by a very quiet brushed slide.
                float contact=Envelope(t,0,.009f,27)*(.65f*Sweep(t,260,-210)+.09f*Sweep(t,510,-350));
                float brush=Envelope(t,.025f,.025f,18)*noise*.32f;
                sample=contact+brush;
            }
            else if(effect==Effect.ItemDrop)
            {
                // Round wooden "tok", a tiny softer bounce, no crack or sub-bass boom.
                sample=Envelope(t,0,.006f,20)*(.65f*Sweep(t,190,-140)+.14f*Sweep(t,460,-80)+noise*.15f);
                sample+=Envelope(t,.115f,.009f,32)*(.16f*Sweep(t-.115f,245,-160)+noise*.045f);
            }
            else
            {
                // Gentle porcelain clink: consonant, short partials rather than hiss.
                sample=Envelope(t,0,.008f,20)*(.36f*Sweep(t,235,-120)+noise*.09f);
                sample+=Chime(t,0,660,.20f)+Chime(t,.078f,880,.11f)+Chime(t,.155f,990,.065f);
            }
            low+=filterAlpha*(sample-low);dc+=dcAlpha*(low-dc);
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-t)/.055f));
            data[i]=(low-dc)*fade;peak=Mathf.Max(peak,Mathf.Abs(data[i]));
        }
        // Plenty of headroom, even before the quiet in-game mix.
        float desired=effect==Effect.PawPush?.43f:effect==Effect.ItemDrop?.50f:.44f;
        float gain=desired/Mathf.Max(.001f,peak);
        for(int i=0;i<data.Length;i++)data[i]*=gain;
        return data;
    }
    static float Sweep(float t,float hz,float slope)=>Mathf.Sin(2*Mathf.PI*(hz*t+.5f*slope*t*t));
    static float Envelope(float t,float delay,float attack,float decay)
    {
        t-=delay;if(t<0)return 0;
        return (1-Mathf.Exp(-t/attack))*Mathf.Exp(-t*decay);
    }
    static float Chime(float t,float delay,float hz,float level)
    {
        t-=delay;if(t<0)return 0;
        return Envelope(t,0,.005f,9)*level*(Sweep(t,hz,-8)+.24f*Sweep(t,hz*1.5f,-9)+.07f*Sweep(t,hz*2.02f,-10));
    }
}
