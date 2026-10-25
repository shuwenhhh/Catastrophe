using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class FixedTailReview
{
    const string Dir="Validation/FixedTail";
    static double ready;
    static FixedTailReview(){EditorApplication.update+=Poll;}
    [MenuItem("Catastrophe/Review Fixed Tail In Play")]
    public static void Run(){SessionState.SetBool("FixedTailReview",true);ready=EditorApplication.timeSinceStartup+3; if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Dir+"/install.request"))
        {File.Delete(Dir+"/install.request");SessionState.SetBool("FixedTailInstall",true);if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}}
        if(SessionState.GetBool("FixedTailInstall",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {SessionState.SetBool("FixedTailInstall",false);SoftTailKittenSetup.Install();Run();return;}
        if(!SessionState.GetBool("FixedTailReview",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
        var cat=UnityEngine.Object.FindFirstObjectByType<CatController>();if(!cat)return;
        SessionState.SetBool("FixedTailReview",false);
        try
        {
            Directory.CreateDirectory(Dir);
            DiningFurnitureClearance.TestInPlay();
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
            var anim=cat.GetComponentInChildren<Animator>();
            var rb=cat.GetComponent<Rigidbody>();var position=rb.position;var rotation=rb.rotation;
            var interpolation=rb.interpolation;rb.interpolation=RigidbodyInterpolation.None;
            // A clear floor lane for appearance comparisons, not inside furniture.
            rb.position=new Vector3(0,.225f,-2.55f);rb.rotation=Quaternion.identity;
            cat.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
            var mesh=new Mesh();skin.BakeMesh(mesh);
            File.WriteAllText(Dir+"/identity.txt",$"skin={skin.name} mesh={skin.sharedMesh.name} vertices={mesh.vertexCount} animator={anim.runtimeAnimatorController.name} swayComponents={cat.GetComponentsInChildren<SoftKittenTailMotion>().Length}\n");
            Capture(cat,"01-side",false);
            var modes=cat.GetComponentsInChildren<Renderer>().Select(r=>(r,r.shadowCastingMode)).ToArray();
            foreach(var x in modes)x.r.shadowCastingMode=ShadowCastingMode.Off;
            Capture(cat,"02-no-self-shadow",false);
            foreach(var x in modes)x.r.shadowCastingMode=x.shadowCastingMode;
            Capture(cat,"03-front",true);
            var root=anim.GetComponentsInChildren<Transform>().First(t=>t.name=="Root");
            var tailIndices=skin.sharedMesh.GetTriangles(1).Distinct().ToArray();
            Vector3[] baseline=null;float worst=0;int checkedPoses=0;var log=new System.Collections.Generic.List<string>();
            foreach(var clip in anim.runtimeAnimatorController.animationClips.Distinct())
            foreach(float phase in new[]{0f,.35f,.7f})
            {
                clip.SampleAnimation(anim.gameObject,clip.length*phase);skin.BakeMesh(mesh);
                var vertices=mesh.vertices;var tail=tailIndices.Select(i=>root.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]))).ToArray();
                if(baseline==null)baseline=tail;
                // Root motion may translate the whole pose; compare curve shape,
                // not its global offset during the push animation.
                float error=tail.Select((p,i)=>Vector3.Distance(p-tail[0],baseline[i]-baseline[0])).Max();worst=Mathf.Max(worst,error);checkedPoses++;
                log.Add($"{(error<.0001f?"PASS":"FAIL")} {clip.name} phase={phase} fixed tail error={error:F7}");
                if(phase==.35f)Capture(cat,"pose-"+clip.name.Replace('|','_').Replace('/','_'),false);
            }
            anim.Rebind();anim.Update(0);
            rb.position=position;rb.rotation=rotation;cat.transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
            rb.interpolation=interpolation;
            log.Add($"Fixed-tail checks: {checkedPoses}, worst root-relative shape error={worst:F7}; scene not saved. Passage results in Validation/DiningPassage/live.txt.");
            File.WriteAllLines(Dir+"/animation-checks.txt",log);
            UnityEngine.Object.DestroyImmediate(mesh);
            Debug.Log("FIXED_TAIL_REVIEW_CAPTURED");
        }catch(Exception e){Debug.LogException(e);}
    }
    static void Capture(CatController cat,string name,bool front)
    {
        var go=new GameObject("Disposable tail review camera");var camera=go.AddComponent<Camera>();
        var p=cat.transform.position+Vector3.up*.02f;
        go.transform.position=p+cat.transform.TransformDirection(front?new Vector3(.8f,.3f,1.5f):new Vector3(1.5f,.14f,0));
        go.transform.LookAt(p);camera.fieldOfView=35;camera.nearClipPlane=.02f;
        var rt=new RenderTexture(1200,900,24);var prior=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Dir+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);}
    }
}
