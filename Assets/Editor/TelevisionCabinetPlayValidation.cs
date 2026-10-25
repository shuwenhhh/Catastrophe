using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TelevisionCabinetPlayValidation
{
    const string Flag = "TVConsolePlayValidation";
    const string FrontFlag = "TVConsoleFrontPushValidation";
    static readonly List<string> lines = new List<string>();
    static double next;
    static int phase, pushes, scores;
    static CatController cat;
    static CabinetOnlyTelevision gate;
    static BoxCollider top, tvCollider;
    static Valuable television;
    static Rigidbody tvBody;
    static float startingY;
    static double landingDeadline;
    static double fallDeadline;
    static bool frontPush;

    static TelevisionCabinetPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            frontPush = SessionState.GetBool(FrontFlag, false);
            next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Catastrophe/TV Cabinet/Verify Landing And TV Push In Play")]
    static void Run()
    {
        Start(false);
    }

    [MenuItem("Catastrophe/TV Cabinet/Verify Front TV Push In Play")]
    static void RunFront()
    {
        Start(true);
    }

    static void Start(bool front)
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before test.");
        lines.Clear(); phase = 0; pushes = 0; scores = 0;
        SessionState.SetBool(FrontFlag, front);
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (phase == 0)
            {
                cat = Object.FindFirstObjectByType<CatController>();
                var cabinet = GameObject.Find("Tripo Tv Cabinet");
                var topObject = GameObject.Find("TV cabinet - extended walkable top");
                var tv = GameObject.Find("Tripo Television");
                if (!cat || !cabinet || !topObject || !tv) throw new Exception("Missing live cat/cabinet/top/TV.");
                top = topObject.GetComponent<BoxCollider>();
                tvCollider = tv.GetComponent<BoxCollider>();
                television = tv.GetComponent<Valuable>();
                gate = tv.GetComponent<CabinetOnlyTelevision>();
                tvBody = tv.GetComponent<Rigidbody>();
                if (!top || !tvCollider || !television || !gate || !tvBody) throw new Exception("Missing solid top or television push components.");
                if (top.bounds.size.x < 2.9f || top.bounds.size.z < 1f) throw new Exception("TV cabinet top not enlarged enough for cat.");
                if (tvCollider.bounds.min.y < top.bounds.max.y - .005f || tvCollider.bounds.min.y > top.bounds.max.y + .05f)
                    throw new Exception("TV is not resting on cabinet top: TV min=" + tvCollider.bounds.min.y + ", top=" + top.bounds.max.y + ", TV=" + tvCollider.bounds + ", top=" + top.bounds);
                lines.Add("PASS cabinet top " + top.bounds.size + ", TV begins on top");
                Object.FindFirstObjectByType<GameFlowUI>()?.BeginGame();
                Valuable.AnyDestroyed += OnDestroyed;
                Rigidbody movingCat = cat.GetComponent<Rigidbody>();
                Vector3 ground = new Vector3(tvCollider.bounds.min.x - .36f, .225f, tvCollider.bounds.center.z);
                movingCat.position = ground;
                movingCat.rotation = Quaternion.Euler(0, 90, 0);
                Physics.SyncTransforms();
                if (gate.CanPush(cat) || cat.TryPushNearby()) throw new Exception("Cat can push TV from floor.");
                lines.Add("PASS TV cannot be pushed from floor");
                // Let the real cat rigidbody fall onto the platform and settle.
                movingCat.position = new Vector3(top.bounds.min.x + .36f, top.bounds.max.y + 1.0f, top.bounds.center.z + .28f);
                movingCat.rotation = Quaternion.Euler(0, 90, 0);
                movingCat.linearVelocity = Vector3.zero;
                lines.Add("Drop test start at " + movingCat.position + " surface=" + top.bounds);
                phase = 1; next = EditorApplication.timeSinceStartup + .5;
                landingDeadline = EditorApplication.timeSinceStartup + 7.0;
                return;
            }

            if (phase == 1)
            {
                float footY = cat.transform.position.y - .225f;
                if (Mathf.Abs(footY - top.bounds.max.y) > .12f)
                {
                    if (EditorApplication.timeSinceStartup < landingDeadline)
                    {
                        next = EditorApplication.timeSinceStartup + .3;
                        return;
                    }
                    throw new Exception("Cat did not land on top: position=" + cat.transform.position + " foot=" + footY + " top=" + top.bounds.max.y);
                }
                lines.Add("PASS live cat lands and stands on the cabinet at y=" + footY.ToString("F3"));
                cat.enabled = false;
                Rigidbody catBody = cat.GetComponent<Rigidbody>();
                catBody.isKinematic = true;
                // Leave room for the cat's head protection collider: the paw can
                // reach the TV without the body pinning it from the other side.
                Vector3 side = frontPush
                    ? new Vector3(tvCollider.bounds.center.x, top.bounds.max.y + .225f, tvCollider.bounds.min.z - .40f)
                    : new Vector3(tvCollider.bounds.min.x - .48f, top.bounds.max.y + .225f, tvCollider.bounds.center.z);
                cat.transform.SetPositionAndRotation(side, Quaternion.Euler(0, frontPush ? 0 : 90, 0));
                Physics.SyncTransforms();
                if (!gate.CanPush(cat)) throw new Exception("Cat standing on cabinet is not authorized to push TV.");
                lines.Add((frontPush ? "Front" : "Side") + " push cat=" + cat.transform.position + " facing=" + cat.transform.forward + " TV=" + tvBody.position);
                startingY = tvBody.worldCenterOfMass.y;
                phase = 2; next = EditorApplication.timeSinceStartup + .2;
                return;
            }

            if (phase == 2)
            {
                if (scores > 0)
                {
                    if (startingY - tvBody.worldCenterOfMass.y < .19f)
                        throw new Exception("TV scored before it fell from cabinet.");
                    lines.Add("PASS television fell and scored after " + pushes + " separate paw pushes");
                    Finish(true); return;
                }
                if (tvBody.worldCenterOfMass.y < startingY - .12f)
                {
                    lines.Add("TV has left the cabinet; waiting for floor impact and score");
                    phase = 3;
                    fallDeadline = EditorApplication.timeSinceStartup + 4.0;
                    next = EditorApplication.timeSinceStartup + .2;
                    return;
                }
                if (pushes >= 18) throw new Exception("TV did not fall after 18 pushes.");
                // A player follows the television along the cabinet between taps.
                // Keep the test cat at paw range instead of testing from its old spot.
                Vector3 nextPaw = frontPush
                    ? new Vector3(tvCollider.bounds.center.x, top.bounds.max.y + .225f, tvCollider.bounds.min.z - .40f)
                    : new Vector3(tvCollider.bounds.min.x - .48f, top.bounds.max.y + .225f, tvCollider.bounds.center.z);
                cat.transform.SetPositionAndRotation(nextPaw, Quaternion.Euler(0, frontPush ? 0 : 90, 0));
                Physics.SyncTransforms();
                if (!cat.TryPushNearby()) throw new Exception("Cabinet cat could not push TV at stroke " + (pushes + 1) +
                    ": cat=" + cat.transform.position + " gate=" + gate.CanPush(cat) +
                    " TV=" + tvBody.position + " collider=" + tvCollider.bounds +
                    " contacts=" + DescribeOverlaps());
                pushes++;
                if (scores != 0) throw new Exception("Scored while TV was still on the cabinet.");
                lines.Add("Push " + pushes + " accepted, TV=" + tvBody.position + " cat facing=" + cat.transform.forward);
                if (pushes <= 3) lines.Add("  TV physics velocity=" + tvBody.linearVelocity +
                    " constraints=" + tvBody.constraints + " contacts=" + DescribeOverlaps());
                next = EditorApplication.timeSinceStartup + 1.0;
            }
            if (phase == 3)
            {
                if (scores > 0)
                {
                    lines.Add("PASS television fell and scored after " + pushes + " separate paw pushes");
                    Finish(true); return;
                }
                if (EditorApplication.timeSinceStartup >= fallDeadline)
                    throw new Exception("TV left cabinet but did not score after reaching floor: " + tvBody.position);
                next = EditorApplication.timeSinceStartup + .2;
            }
        }
        catch (Exception e)
        {
            lines.Add("FAIL " + e);
            Debug.LogException(e);
            Finish(false);
        }
    }

    static void OnDestroyed(Valuable item) { if (item == television) scores++; }

    static string DescribeOverlaps()
    {
        var result = new List<string>();
        foreach (var other in Physics.OverlapBox(tvCollider.bounds.center,
                     tvCollider.bounds.extents + Vector3.one * .01f))
        {
            if (other.attachedRigidbody == tvBody || other.isTrigger) continue;
            if (Physics.ComputePenetration(tvCollider, tvCollider.transform.position,
                    tvCollider.transform.rotation, other, other.transform.position,
                    other.transform.rotation, out Vector3 direction, out float depth))
                result.Add(other.name + "(" + depth.ToString("F3") + "," + direction + ")");
        }
        return string.Join("; ", result);
    }

    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Valuable.AnyDestroyed -= OnDestroyed;
        lines.Add(passed ? "ALL PASS" : "VALIDATION FAILED");
        Directory.CreateDirectory("Validation/TVConsole");
        File.WriteAllLines("Validation/TVConsole/play-check.txt", lines);
        EditorApplication.isPlaying = false;
    }
}
