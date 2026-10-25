using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RefinedJumpSetup
{
    const string Model = "Assets/Art/SmoothKitten3D/RefinedJump/RefinedJump.fbx";
    const string Controller = "Assets/Art/SmoothKitten3D/SmoothKitten3D.controller";
    public const string ClipPath = "Assets/Art/SmoothKitten3D/RefinedJump/Cat_Jump_Refined.anim";
    public const string Prefab = "Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";

    [MenuItem("Catastrophe/Jump/Install Refined Jump And Preview")]
    public static void Install()
    {
        if (Application.isPlaying) throw new Exception("Exit Play before installing the animation.");
        AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = false; c.loopPose = false;
            c.keepOriginalPositionY = c.keepOriginalPositionXZ = c.keepOriginalOrientation = true;
            c.lockRootHeightY = c.lockRootPositionXZ = c.lockRootRotation = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__"));
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (!clip) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, ClipPath); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = "Cat_Jump_Refined";
        // An armature-only FBX collapses the armature object at import. Rebind
        // its curves to the identical bone hierarchy beneath the existing mesh.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var animatorRoot = prefab.GetComponentInChildren<Animator>().transform;
        var rootBone = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "Root");
        string prefix = AnimationUtility.CalculateTransformPath(rootBone.parent, animatorRoot);
        clip.ClearCurves();
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var mapped = binding;
            mapped.path = string.IsNullOrEmpty(binding.path) ? prefix : prefix + "/" + binding.path;
            AnimationUtility.SetEditorCurve(clip, mapped, AnimationUtility.GetEditorCurve(source, binding));
        }
        clip.EnsureQuaternionContinuity();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (!controller.parameters.Any(p => p.name == "JumpPhase"))
            controller.AddParameter("JumpPhase", AnimatorControllerParameterType.Float);
        var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Jump").state;
        state.motion = clip;
        state.timeParameter = "JumpPhase"; state.timeParameterActive = true;
        foreach (var transition in state.transitions)
        {
            transition.hasExitTime = false;
            transition.duration = .06f;
            if (!transition.conditions.Any(c => c.parameter == "JumpPhase"))
                transition.AddCondition(AnimatorConditionMode.Greater, .99f, "JumpPhase");
        }
        EditorUtility.SetDirty(clip); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Preview();
        Debug.Log("REFINED_JUMP_INSTALLED");
    }

    public static void Preview()
    {
        const string folder = "Validation/RefinedJump";
        Directory.CreateDirectory(folder);
        var cat = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        cat.hideFlags = HideFlags.HideAndDontSave;
        cat.transform.position = new Vector3(200, 200, 200);
        var animator = cat.GetComponentInChildren<Animator>(); animator.enabled = false;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (animator.runtimeAnimatorController is AnimatorOverrideController overrides) clip = overrides[clip];
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var cameraObject = new GameObject("Jump preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = .55f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.76f,.80f,.84f);
        camera.cullingMask = 1 << 31;
        var lightObject = new GameObject("Jump preview light");
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = 1.4f; light.cullingMask = 1 << 31;
        light.transform.rotation = Quaternion.Euler(40,-30,0);
        var target = new RenderTexture(384,384,24);
        var atlas = new Texture2D(2304,768,TextureFormat.RGB24,false);
        var mesh = new Mesh();
        var frozen = new GameObject("Baked pose"); frozen.transform.SetParent(skin.transform,false);
        frozen.AddComponent<MeshFilter>().sharedMesh = mesh;
        frozen.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
        foreach (var t in cat.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
        var previous = RenderTexture.active;
        try
        {
            float[] phases = {0f,.10f,.23f,.48f,.79f,.90f};
            Vector3[] first = null; float deformation = 0;
            for(int i=0;i<phases.Length;i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * phases[i]);
                skin.BakeMesh(mesh);
                if(i==0) first=mesh.vertices;
                else deformation=Mathf.Max(deformation,mesh.vertices.Zip(first,(a,b)=>Vector3.Distance(a,b)).Max());
                skin.enabled=false;
                for(int view=0;view<2;view++)
                {
                    Vector3 center=cat.transform.position+new Vector3(0,0,0);
                    camera.transform.position=center+(view==0?new Vector3(.15f,.12f,2):new Vector3(2,.12f,.1f));
                    camera.transform.LookAt(center);
                    camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                    atlas.ReadPixels(new Rect(0,0,384,384),i*384,(1-view)*384);
                }
            }
            atlas.Apply(); File.WriteAllBytes(folder+"/jump-phases.png",atlas.EncodeToPNG());
            if(deformation < .025f) throw new Exception("Jump clip does not deform the runtime mesh: "+deformation);
            File.WriteAllText(folder+"/preview-check.txt",$"PASS runtime prefab mesh deformation={deformation:F5}m; six front/side phases; physical motion not baked into clip.\n");
        }
        finally
        {
            RenderTexture.active=previous; camera.targetTexture=null;
            foreach(var o in new UnityEngine.Object[]{cat,cameraObject,lightObject,target,atlas,mesh}) UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
