using System.Collections;
using UnityEngine;

/// <summary>
/// Adds the non-destructive "cute pass" on top of the animated kitten rig.
/// Bone proportions and secondary motion remain runtime-editable and do not
/// alter the source FBX or its animation clips.
/// </summary>
public sealed class CuteCatVisual : MonoBehaviour
{
    [Header("Cute proportions")]
    [SerializeField] private float headScale = 1.28f;
    [SerializeField] private float lowerLegScale = 0.80f;

    [Header("Secondary motion")]
    [SerializeField] private float squashDuration = 0.15f;
    [SerializeField] private float tailSwayDegrees = 9f;
    [SerializeField] private Vector2 blinkInterval = new Vector2(2f, 4f);

    private Transform visualRoot;
    private Transform head;
    private Transform[] lowerLegs;
    private Transform[] tailBones;
    private Transform[] earBones;
    private Vector3 visualBaseScale;
    private Vector3 headBaseScale;
    private Vector3[] legBaseScales;
    private float nextBlinkAt;
    private float blinkUntil;
    private float nextEarTwitchAt;
    private float earTwitchUntil;
    private float alertShakeUntil;
    private Coroutine squashRoutine;
    private NoiseManager observedNoise;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        visualRoot = FindDeep(transform, "CartoonShell");
        if (visualRoot == null)
            visualRoot = animator != null ? animator.transform.parent : transform.GetChild(0);
        visualBaseScale = visualRoot.localScale;
        head = FindDeep(transform, "CuteHead");
        if (head == null)
            head = FindDeep(transform, "Head");
        lowerLegs = FindMany("Front_R_Lower", "Front_L_Lower", "Back_R_Lower", "Back_L_Lower");
        tailBones = FindMany("Tail_01", "Tail_02", "Tail_03");
        earBones = FindNameContains("Ear");
        if (head != null)
        {
            headBaseScale = head.localScale;
            head.localScale = headBaseScale * headScale;
        }
        legBaseScales = new Vector3[lowerLegs.Length];
        for (int index = 0; index < lowerLegs.Length; index++)
        {
            legBaseScales[index] = lowerLegs[index].localScale;
            lowerLegs[index].localScale = Vector3.Scale(legBaseScales[index], new Vector3(1f, lowerLegScale, 1f));
        }
        ScheduleBlink();
        nextEarTwitchAt = Time.time + Random.Range(3f, 6f);
    }

    private void Start()
    {
        BindNoiseManager();
    }

    private void BindNoiseManager()
    {
        NoiseManager manager = NoiseManager.I != null ? NoiseManager.I : FindAnyObjectByType<NoiseManager>();
        if (manager == observedNoise)
            return;
        if (observedNoise != null)
            observedNoise.OnThresholdExceeded -= PlayAlertShake;
        observedNoise = manager;
        if (observedNoise != null)
            observedNoise.OnThresholdExceeded += PlayAlertShake;
    }

    public void PlayJumpStretch()
    {
        StartSquash(new Vector3(0.88f, 1.16f, 0.88f));
    }

    public void PlayLandingSquash()
    {
        StartSquash(new Vector3(1.14f, 0.78f, 1.14f));
    }

    public void PlayAlertShake()
    {
        alertShakeUntil = Time.time + 0.28f;
    }

    private void StartSquash(Vector3 target)
    {
        if (squashRoutine != null)
            StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(SquashAndRecover(target));
    }

    private IEnumerator SquashAndRecover(Vector3 target)
    {
        float half = squashDuration * 0.5f;
        for (float time = 0f; time < half; time += Time.deltaTime)
        {
            visualRoot.localScale = Vector3.Scale(visualBaseScale,
                Vector3.Lerp(Vector3.one, target, time / Mathf.Max(0.001f, half)));
            yield return null;
        }
        for (float time = 0f; time < half; time += Time.deltaTime)
        {
            float eased = 1f - Mathf.Pow(1f - time / Mathf.Max(0.001f, half), 2f);
            visualRoot.localScale = Vector3.Scale(visualBaseScale, Vector3.Lerp(target, Vector3.one, eased));
            yield return null;
        }
        visualRoot.localScale = visualBaseScale;
        squashRoutine = null;
    }

    private void LateUpdate()
    {
        if (observedNoise == null)
            BindNoiseManager();

        float idleAmount = GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < 0.02f ? 1f : 0.35f;
        float sway = Mathf.Sin(Time.time * 2.4f) * tailSwayDegrees * idleAmount;
        for (int index = 0; index < tailBones.Length; index++)
            tailBones[index].localRotation *= Quaternion.Euler(0f, sway * (0.45f + index * 0.25f), 0f);

        if (Time.time >= nextEarTwitchAt)
        {
            earTwitchUntil = Time.time + 0.16f;
            nextEarTwitchAt = Time.time + Random.Range(3f, 6f);
        }
        if (Time.time < earTwitchUntil)
        {
            float twitch = Mathf.Sin((earTwitchUntil - Time.time) * 65f) * 6f;
            foreach (Transform ear in earBones)
                ear.localRotation *= Quaternion.Euler(0f, 0f, twitch);
        }

        if (Time.time >= nextBlinkAt)
        {
            blinkUntil = Time.time + 0.11f;
            ScheduleBlink();
        }
        // The rig has modeled eyelids rather than blend shapes. A very brief,
        // subtle head compression reads as a blink at gameplay scale without
        // introducing fuzzy overlay sprites.
        if (head != null)
        {
            float blink = Time.time < blinkUntil ? 0.92f : 1f;
            head.localScale = Vector3.Scale(headBaseScale * headScale, new Vector3(1f, 1f, blink));
        }

        if (Time.time < alertShakeUntil)
        {
            float shake = Mathf.Sin(Time.time * 85f) * 0.035f;
            visualRoot.localPosition = new Vector3(shake, visualRoot.localPosition.y, 0f);
        }
        else
        {
            Vector3 position = visualRoot.localPosition;
            position.x = 0f;
            position.z = 0f;
            visualRoot.localPosition = position;
        }
    }

    private void ScheduleBlink()
    {
        nextBlinkAt = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
    }

    private Transform[] FindMany(params string[] names)
    {
        System.Collections.Generic.List<Transform> found = new();
        foreach (string name in names)
        {
            Transform item = FindDeep(transform, name);
            if (item != null)
                found.Add(item);
        }
        return found.ToArray();
    }

    private Transform[] FindNameContains(string fragment)
    {
        System.Collections.Generic.List<Transform> found = new();
        foreach (Transform item in GetComponentsInChildren<Transform>(true))
            if (item.name.Contains(fragment, System.StringComparison.OrdinalIgnoreCase))
                found.Add(item);
        return found.ToArray();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            if (item.name == name)
                return item;
        return null;
    }

    private void OnDestroy()
    {
        if (observedNoise != null)
            observedNoise.OnThresholdExceeded -= PlayAlertShake;
    }
}
