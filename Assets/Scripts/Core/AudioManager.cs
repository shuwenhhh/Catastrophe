using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Optional replacement clips")]
    [SerializeField] private AudioClip itemThud;
    [SerializeField] private AudioClip fragileBreak;
    [SerializeField] private AudioClip pawPush;
    [SerializeField] private AudioClip ownerAlert;
    [SerializeField] private AudioClip hideSuccess;
    [SerializeField] private AudioClip footstep;
    [SerializeField] private AudioClip landing;
    [SerializeField] private AudioClip countdownTick;
    [SerializeField] private AudioSource backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float inspectionMusicLevel = .25f;
    private float musicBaseVolume;
    private bool inspectionMix;
    public bool HasCountdownClip => countdownTick != null;
    public bool InspectionMixActive => inspectionMix;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.75f;

    private AudioSource source;
    private AudioSource worldSource;
    private Transform catEmitter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        I = null;
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(this);
            return;
        }

        I = this;
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        worldSource = gameObject.AddComponent<AudioSource>();
        worldSource.playOnAwake = false;
        worldSource.spatialBlend = 1f;
        worldSource.rolloffMode = AudioRolloffMode.Logarithmic;
        worldSource.minDistance = 0.45f;
        worldSource.maxDistance = 8f;
        worldSource.dopplerLevel = 0f;
        CreatePlaceholderClips();
        if (backgroundMusic) musicBaseVolume = backgroundMusic.volume;
    }

    // Optional music can be assigned without ducking unrelated effects.
    public void SetBackgroundMusic(AudioSource music)
    {
        if (backgroundMusic) backgroundMusic.volume = musicBaseVolume;
        backgroundMusic = music;
        if (music) musicBaseVolume = music.volume;
    }
    public void SetInspectionMix(bool inspecting) => inspectionMix = inspecting;
    public void PlayCountdownTick() => Play(countdownTick, .22f, 1f);
    private void Update()
    {
        if (backgroundMusic)
            backgroundMusic.volume = Mathf.MoveTowards(backgroundMusic.volume,
                musicBaseVolume * (inspectionMix ? inspectionMusicLevel : 1f), Time.unscaledDeltaTime * 1.5f);
    }

    public void PlayItemThud() => Play(itemThud, 0.62f, Random.Range(0.97f, 1.03f));
    public void PlayFragileBreak() => Play(fragileBreak, 0.58f, Random.Range(0.98f, 1.02f));
    public void PlayPawPush() => Play(pawPush, 0.56f, 1f);
    public void PlayOwnerAlert() => Play(ownerAlert, 0.88f, 1f);
    public void PlayHideSuccess() => Play(hideSuccess, 0.76f, 1f);

    public void PlayFootstep()
    {
        PlayAtCat(footstep, 0.20f, Random.Range(0.92f, 1.08f));
    }

    public void PlayLanding()
    {
        PlayAtCat(landing, 0.38f, Random.Range(0.94f, 1.03f));
    }

    private void Play(AudioClip clip, float relativeVolume, float pitch)
    {
        if (clip == null || source == null)
            return;

        source.pitch = pitch;
        source.PlayOneShot(clip, effectsVolume * relativeVolume);
    }

    private void PlayAtCat(AudioClip clip, float relativeVolume, float pitch)
    {
        if (clip == null || worldSource == null)
            return;

        if (catEmitter == null)
        {
            CatController cat = FindAnyObjectByType<CatController>();
            if (cat != null)
                catEmitter = cat.transform;
        }
        if (catEmitter != null)
            worldSource.transform.position = catEmitter.position;

        worldSource.pitch = pitch;
        worldSource.PlayOneShot(clip, effectsVolume * relativeVolume);
    }

    private void CreatePlaceholderClips()
    {
        if(!itemThud)itemThud=CozyCatSoundDesign.LoadOrCreate(CozyCatSoundDesign.Effect.ItemDrop);
        if(!fragileBreak)fragileBreak=CozyCatSoundDesign.LoadOrCreate(CozyCatSoundDesign.Effect.PorcelainDrop);
        if(!pawPush)pawPush=CozyCatSoundDesign.LoadOrCreate(CozyCatSoundDesign.Effect.PawPush);
        countdownTick ??= CreateClip("Soft countdown pluck", .22f, (time, random) =>
        {
            float attack = Mathf.Clamp01(time / .012f);
            float release = Mathf.Clamp01((.22f - time) / .025f);
            return Mathf.Sin(time * 2f * Mathf.PI * 660f) * Mathf.Exp(-time * 22f) * attack * release * .25f;
        });
        ownerAlert ??= CreateClip("Placeholder - owner alert", 0.72f, (time, random) =>
        {
            float frequency = time < 0.34f ? 620f : 820f;
            float localTime = time < 0.34f ? time : time - 0.34f;
            return Mathf.Sin(time * 2f * Mathf.PI * frequency) * Mathf.Exp(-localTime * 3.2f) * 0.42f;
        });
        hideSuccess ??= CreateClip("Placeholder - hide success", 0.42f, (time, random) =>
        {
            float frequency = Mathf.Lerp(1350f, 340f, time / 0.42f);
            return (Mathf.Sin(time * 2f * Mathf.PI * frequency) * 0.28f + random * 0.13f) *
                   Mathf.Sin(Mathf.Clamp01(time / 0.42f) * Mathf.PI);
        });
        footstep ??= CreateClip("Placeholder - soft cat paw", 0.085f, (time, random) =>
        {
            float envelope = Mathf.Exp(-time * 42f);
            float pad = Mathf.Sin(time * 2f * Mathf.PI * 145f) * 0.22f;
            return (pad + random * 0.11f) * envelope;
        });
        landing ??= CreateClip("Placeholder - soft landing poof", 0.20f, (time, random) =>
        {
            float envelope = Mathf.Exp(-time * 19f);
            float body = Mathf.Sin(time * 2f * Mathf.PI * 92f) * 0.36f;
            return (body + random * 0.17f) * envelope;
        });
    }

    private delegate float SampleGenerator(float time, float random);

    private static AudioClip CreateClip(string name, float duration, SampleGenerator generator)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        uint state = 0x9E3779B9u;
        for (int index = 0; index < sampleCount; index++)
        {
            state = state * 1664525u + 1013904223u;
            float random = ((state >> 8) / 8388607.5f) - 1f;
            samples[index] = Mathf.Clamp(generator(index / (float)sampleRate, random), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (backgroundMusic) backgroundMusic.volume = musicBaseVolume;
        if (I == this)
            I = null;
    }
}
