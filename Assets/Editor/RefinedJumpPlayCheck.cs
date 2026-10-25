using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RefinedJumpPlayCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static double ready;
    static CatController cat;
    static Animator animator;
    static Rigidbody body;
    static GameObject floor;
    static int stage;
    static float start, baseY, peak, shortPeak;
    static bool sawJump, sawTuck;
    static string report;
    static RefinedJumpPlayCheck()
    {
        EditorApplication.playModeStateChanged += s => {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("RefinedJumpCheck",false))
            {
                SessionState.SetBool("RefinedJumpCheck",false);
                ready=EditorApplication.timeSinceStartup+2; stage=0; cat=null;
                report=""; EditorApplication.update += Tick;
            }
        };
    }
    [MenuItem("Catastrophe/Jump/Test Short And Long Jump In Play")]
    public static void Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play before running jump check.");
        SessionState.SetBool("RefinedJumpCheck",true); EditorApplication.isPlaying=true;
    }
    static void Set(string name, object value) => typeof(CatController).GetField(name,Flags).SetValue(cat,value);
    static T Get<T>(string name) => (T)typeof(CatController).GetField(name,Flags).GetValue(cat);
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<ready) return;
        try
        {
            if(!cat)
            {
                UnityEngine.Object.FindAnyObjectByType<GameFlowUI>().BeginGame();
                cat=UnityEngine.Object.FindAnyObjectByType<CatController>();
                body=cat.GetComponent<Rigidbody>(); animator=cat.GetComponentInChildren<Animator>();
                if(!Get<bool>("hasRefinedJump") || !cat.GetComponentInChildren<SkinnedMeshRenderer>())
                    throw new Exception("Runtime cat is not using the refined 3D jump.");
                // A disposable platform avoids moving or saving any authored furniture.
                floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name="Disposable jump validation platform";
                floor.transform.position=new Vector3(30,-.1f,30); floor.transform.localScale=new Vector3(6,.2f,6);
                body.position=new Vector3(30,.25f,30); body.linearVelocity=Vector3.zero;
                Physics.SyncTransforms(); ready=EditorApplication.timeSinceStartup+.7; return;
            }
            if(stage==0 || stage==2)
            {
                baseY=body.position.y; peak=0; sawJump=sawTuck=false;
                Set("lastJumpPressedAt",Time.time); Set("lastGroundedAt",Time.time);
                Set("jumpHeld",true); Set("jumpCutPending",false);
                typeof(CatController).GetMethod("FixedUpdate",Flags).Invoke(cat,null);
                // Hold test bypasses only the release event; both tests use the
                // actual controller launch, physics, grounded query and Animator.
                if(stage==2) Set("jumpCutApplied",true);
                start=Time.time; stage++; return;
            }
            peak=Mathf.Max(peak,body.position.y-baseY);
            sawJump |= animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
            sawTuck |= animator.GetFloat("JumpPhase") >= .46f && animator.GetFloat("JumpPhase") < .80f;
            if(Time.time-start>4) throw new Exception("Jump or landing failed to finish.");
            if(Time.time-start<.4f || Get<bool>("isJumping")) return;
            if(Time.time-Get<float>("jumpLandingAt")<.35f) return;
            if(!sawJump || !sawTuck || !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                throw new Exception("Missing Jump/tuck/Idle transition.");
            report += $"PASS {(stage==1?"short":"long")} jump: rise={peak:F3}m; Jump, tuck, contact recovery, Idle; phase={animator.GetFloat("JumpPhase"):F2}.\n";
            if(stage==1)
            {
                shortPeak=peak; stage=2; ready=EditorApplication.timeSinceStartup+.3; return;
            }
            if(shortPeak<.35f || peak<1.1f || peak<shortPeak+.5f) throw new Exception("Short/long jump heights regressed.");
            KittenForelegValidation.VerifyPlay();
            report += "PASS existing walk planted-paw and Walk -> Idle regression check.\n";
            Finish();
        }
        catch(Exception e) { report += "FAIL "+e; Debug.LogException(e); Finish(); }
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;
        Directory.CreateDirectory("Validation/RefinedJump");
        File.WriteAllText("Validation/RefinedJump/play-check.txt",report);
        if(floor) UnityEngine.Object.DestroyImmediate(floor);
        EditorApplication.isPlaying=false;
    }
}
