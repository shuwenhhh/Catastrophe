using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KittenForelegValidation
{
    const string Model="Assets/Art/SmoothKitten3D/LongStride/SmoothKitten3D_LongStride.fbx";
    const string Prefab="Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";
    const string Output="Validation/SmoothKitten3D/Forelegs";
    [MenuItem("Catastrophe/Verify Repaired Forelegs In Play Mode")]
    public static void VerifyPlay()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");
        var live=UnityEngine.Object.FindAnyObjectByType<CatController>();
        if(!live)throw new Exception("No runtime player");
        var liveAnimator=live.GetComponentInChildren<Animator>();
        if(!liveAnimator||!liveAnimator.GetComponentInChildren<SkinnedMeshRenderer>()||liveAnimator.GetComponentInChildren<SpriteRenderer>())throw new Exception("Runtime player does not use the skinned kitten");
        Bone(live.gameObject,"Front_R_Paw");Bone(live.gameObject,"Front_L_Paw");
        var fixture=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));fixture.name="Disposable runtime gait check";
        try
        {
            fixture.transform.position=new Vector3(200,200,200);fixture.transform.localScale=Vector3.one*.65f;
            var animator=fixture.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.SetBool("Grounded",true);animator.SetFloat("Speed",1);animator.SetFloat("WalkRate",4);
            animator.Play("Walk",0,0);animator.Update(0);
            var paw=Bone(fixture,"Front_R_Paw");var start=paw.position;float slip=0;
            float contactSpeed=.20f/.64f*.65f*(4f/2f);
            for(int i=0;i<24;i++){fixture.transform.position+=Vector3.forward*(contactSpeed/96f);animator.Update(1f/96f);slip=Mathf.Max(slip,Vector3.Distance(start,paw.position));}
            if(slip>.004f)throw new Exception($"Runtime contact slip {slip}");
            animator.SetFloat("Speed",0);animator.Update(.2f);animator.Update(.2f);
            if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))throw new Exception("Failed to return to Idle");
            File.WriteAllText(Output+"/play-checks.txt",$"PASS runtime player: {live.name}; Animator: {liveAnimator.name}; front wrist bones present; skinned 3D mesh.\nPASS moving fixture at {contactSpeed:F3}m/s, WalkRate=4; planted paw world slip={slip:F6}m.\nPASS Walk -> Idle transition.\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(fixture);}
    }
    [MenuItem("Catastrophe/Repair Front Legs - Import and Verify")]
    public static void Run()
    {
        AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips){clip.loopTime=clip.name.Contains("Walk")||clip.name.Contains("Idle");clip.loopPose=false;clip.keepOriginalPositionY=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalOrientation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var assets=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/SmoothKitten3D/SmoothKitten3D.controller");
        if(!controller.parameters.Any(p=>p.name=="WalkRate"))controller.AddParameter("WalkRate",AnimatorControllerParameterType.Float);
        foreach(var child in controller.layers[0].stateMachine.states)
        {
            var state=child.state; string key=state.name=="Push Right Paw"?"Cat_Push_Right":"Cat_"+state.name;
            var motion=assets.FirstOrDefault(c=>c.name.Contains(key));if(motion)state.motion=motion;
            if(state.name=="Walk"){state.speed=1;state.speedParameter="WalkRate";state.speedParameterActive=true;}
        }
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
        KittenAppearanceSetup.Apply();
        Validate(assets.First(c=>c.name.Contains("Cat_Walk")));
    }
    static Transform Bone(GameObject cat,string name)=>cat.GetComponentsInChildren<Transform>().First(t=>t.name==name);
    static void Validate(AnimationClip walk)
    {
        Directory.CreateDirectory(Output);
        var fixture=new GameObject("Disposable foreleg validation"){hideFlags=HideFlags.HideAndDontSave};
        var cat=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),fixture.transform);
        var animator=cat.GetComponentInChildren<Animator>();animator.enabled=false;
        var lines=new List<string>();
        try
        {
            foreach(string side in new[]{"R","L"})
            {
                var paw=Bone(cat,"Front_"+side+"_Paw");float min=999,max=-999,error=0;Vector3 first=Vector3.zero;
                for(int i=0;i<=24;i++)
                {
                    float phase=i/48f+(side=="L"?.5f:0);phase%=1;
                    walk.SampleAnimation(animator.gameObject,walk.length*phase);
                    var p=paw.position;if(i==0)first=p;
                    min=Mathf.Min(min,p.y);max=Mathf.Max(max,p.y);
                    // The paw travels 0.20 model metres over the first 64% of the cycle.
                    error=Mathf.Max(error,Mathf.Abs((p.z-first.z)+.20f*(i/48f)/.64f));
                }
                lines.Add($"{side}: stance height drift={max-min:F6}m; compensated forward slip={error:F6}m");
                if(max-min>.003f||error>.004f)throw new Exception("Planted paw validation failed: "+lines.Last());
            }
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
            var a=new Mesh();var b=new Mesh();walk.SampleAnimation(animator.gameObject,0);skin.BakeMesh(a);
            walk.SampleAnimation(animator.gameObject,walk.length*.8f);skin.BakeMesh(b);
            float deformation=a.vertices.Zip(b.vertices,(x,y)=>Vector3.Distance(x,y)).Max();
            UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
            if(deformation<.015f)throw new Exception("Mesh failed to deform");
            lines.Add($"Visible mesh deformation={deformation:F5}m; Animator={animator.name}; duration={walk.length:F3}s");
            foreach(float phase in new[]{0f,.25f,.65f,.80f,.95f})
            {walk.SampleAnimation(animator.gameObject,walk.length*phase);Render(cat,fixture,new Vector3(.15f,.06f,2),$"front_{phase:F2}.png");Render(cat,fixture,new Vector3(2,.02f,.15f),$"side_{phase:F2}.png");}
            File.WriteAllLines(Output+"/checks.txt",lines);Debug.Log("FORELEG_VALIDATION_PASS "+string.Join("; ",lines));
        }
        catch(Exception e){File.WriteAllText(Output+"/checks.txt",e.ToString()+"\n"+string.Join("\n",lines));throw;}
        finally{UnityEngine.Object.DestroyImmediate(fixture);}
    }
    static void Render(GameObject cat,GameObject fixture,Vector3 view,string filename)
    {
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh);
        var frozen=new GameObject("Pose");frozen.transform.SetParent(skin.transform,false);frozen.AddComponent<MeshFilter>().sharedMesh=mesh;frozen.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
        foreach(var t in cat.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        var lightGo=new GameObject("Key");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.cullingMask=1<<31;lightGo.transform.rotation=Quaternion.Euler(35,-25,0);
        var cameraGo=new GameObject("Preview");var camera=cameraGo.AddComponent<Camera>();camera.transform.position=view;camera.transform.LookAt(Vector3.zero);camera.orthographic=true;camera.orthographicSize=.55f;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.83f,.87f,.91f);
        var rt=new RenderTexture(640,640,24);var previous=RenderTexture.active;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+filename,tex.EncodeToPNG());
        RenderTexture.active=previous;skin.enabled=true;foreach(var o in new UnityEngine.Object[]{frozen,mesh,lightGo,cameraGo,rt,tex})UnityEngine.Object.DestroyImmediate(o);
    }
}
