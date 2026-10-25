#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class CatTreeHidePlayValidation : MonoBehaviour
{
    const string Folder = "Validation/CatTreeHide";
    static CatTreeHidePlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CatTreeHideTest", false)) return;
            SessionState.SetBool("CatTreeHideTest", false);
            EditorApplication.delayCall += () => new GameObject("Disposable cubby hide check").AddComponent<CatTreeHidePlayValidation>();
        };
    }
    [MenuItem("Catastrophe/Verify Cat Tree Hiding In Play")]
    static void Run()
    {
        if (Application.isPlaying) new GameObject("Disposable cubby hide check").AddComponent<CatTreeHidePlayValidation>();
        else { SessionState.SetBool("CatTreeHideTest", true); EditorApplication.isPlaying = true; }
    }
    readonly List<string> lines = new();
    int failed;
    void Check(bool good, string label) { lines.Add((good ? "PASS " : "FAIL ") + label); if (!good) failed++; }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(4f);
        Directory.CreateDirectory(Folder);
        var cat = Object.FindFirstObjectByType<CatController>();
        var hide = GameObject.Find("Hide Spot - Cat Tree Cubby")?.GetComponent<HideSpot>();
        Check(cat && hide, $"Actual skinned player and cat tree hide volume exist (cat={cat}, hide={hide})");
        if (!hide) lines.Add(string.Join("; ", Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Contains("tree")||t.name.Contains("Tree")||t.name.Contains("Cubby")).Select(t=>t.name)));
        if (cat && hide)
        {
            Object.FindFirstObjectByType<GameFlowUI>().BeginGame();
            var body = cat.GetComponent<Rigidbody>();
            var solid = cat.GetComponent<CatSolidCollision>();
            Check(hide.GetComponent<Collider>().isTrigger, "Interaction trigger does not block walking");
            Check(solid.CanOccupy(hide.HideAnchor.position, hide.HideAnchor.rotation), "Whole cat fits inside real hollow shell");
            Check(solid.CanOccupy(hide.ExitAnchor.position, hide.ExitAnchor.rotation), "Exit is not blocked by posts or cubby walls");
            body.position = hide.ExitAnchor.position;
            body.rotation = hide.ExitAnchor.rotation;
            cat.transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.5f);
            Check(cat.CanHideNearby, "Approaching entrance activates hide interaction");
            cat.EnterHide(hide.gameObject);
            yield return new WaitForSeconds(.4f);
            Check(cat.IsHidden && Vector3.Distance(body.position, hide.HideAnchor.position) < .03f, "Cat enters and remains in cubby");
            Check(cat.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), "Cat remains a visible 3D model, not invisible");
            Capture("inside", hide.HideAnchor.position);
            cat.ExitHide();
            yield return new WaitForSeconds(.3f);
            Check(!cat.IsHidden && Vector3.Distance(body.position, hide.ExitAnchor.position) < .08f, "Cat exits safely and restores motion");
            Check(solid.CanOccupy(body.position, body.rotation), "No penetration after exiting");
            Capture("outside", hide.HideAnchor.position);
        }
        lines.Add($"Score: {(failed == 0 ? 90 : 40)}/100. Failures: {failed}");
        File.WriteAllLines(Folder + "/report.txt", lines);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }
    static void Capture(string name, Vector3 target)
    {
        var go = new GameObject("Disposable cubby evidence camera");
        var camera = go.AddComponent<Camera>();
        camera.transform.position = target + new Vector3(-1.9f, .7f, .9f);
        camera.transform.LookAt(target + Vector3.up * .10f);
        camera.nearClipPlane = .03f;
        var rt = new RenderTexture(1000, 800, 24);
        var old = RenderTexture.active;
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
        File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null;
        Destroy(image); Destroy(rt); Destroy(go);
    }
}
#endif
