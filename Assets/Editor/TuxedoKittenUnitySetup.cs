using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class TuxedoKittenUnitySetup
{
    private const string Folder = "Assets/Art/TuxedoKitten";
    private const string FbxPath = Folder + "/TuxedoKitten_Rigged.fbx";
    private const string BaseColorPath = Folder + "/TuxedoKitten_BaseColor.png";
    private const string MaterialPath = Folder + "/TuxedoKitten_URP.mat";
    private const string ControllerPath = Folder + "/TuxedoKitten.controller";
    private const string PrefabFolder = "Assets/Resources/TuxedoKitten";
    private const string PrefabPath = PrefabFolder + "/TuxedoKitten.prefab";

    [MenuItem("Catastrophe/Build Tuxedo Kitten Character")]
    public static void BuildCharacter()
    {
        ConfigureTexture();
        ConfigureModelImporter();
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();

        AnimationClip idle = FindClip(clips, "Cat_Idle");
        AnimationClip walk = FindClip(clips, "Cat_Walk");
        AnimationClip push = FindClip(clips, "Cat_Push_Right");
        AnimationClip jump = FindClip(clips, "Cat_Jump");
        if (idle == null || walk == null || push == null || jump == null)
            throw new InvalidOperationException("The FBX did not import all four kitten animation clips: " +
                                                string.Join(", ", clips.Select(c => c.name)));

        Material material = CreateOrUpdateMaterial();
        AnimatorController controller = CreateController(idle, walk, push, jump);
        CreatePrefab(material, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"TUXEDO_KITTEN_SETUP_COMPLETE clips=[{string.Join(", ", clips.Select(c => c.name))}] " +
                  $"prefab={PrefabPath}");
    }

    private static void ConfigureTexture()
    {
        if (AssetImporter.GetAtPath(BaseColorPath) is TextureImporter importer)
        {
            importer.sRGBTexture = true;
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureModelImporter()
    {
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
            throw new InvalidOperationException("Could not find the kitten FBX importer.");

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importConstraints = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.isReadable = false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = clip.name.Contains("Idle", StringComparison.OrdinalIgnoreCase) ||
                            clip.name.Contains("Walk", StringComparison.OrdinalIgnoreCase);
            clip.loopPose = clip.loopTime;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionXZ = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = true;
            clip.lockRootPositionXZ = true;
            clip.lockRootHeightY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string expected)
    {
        return clips.FirstOrDefault(clip => clip.name.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static Material CreateOrUpdateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader) { name = "TuxedoKitten_URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        material.SetTexture("_BaseMap", baseColor);
        material.SetColor("_BaseColor", Color.white);
        // Smooth painted-vinyl response: explicitly avoid fuzzy/fur shading.
        material.SetFloat("_Smoothness", 0.42f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AnimatorController CreateController(AnimationClip idle, AnimationClip walk,
        AnimationClip push, AnimationClip jump)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Push", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        AnimatorState walkState = machine.AddState("Walk");
        AnimatorState pushState = machine.AddState("Push Right Paw");
        AnimatorState jumpState = machine.AddState("Jump");
        idleState.motion = idle;
        walkState.motion = walk;
        pushState.motion = push;
        jumpState.motion = jump;
        machine.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.12f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.12f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition anyPush = machine.AddAnyStateTransition(pushState);
        anyPush.hasExitTime = false;
        anyPush.duration = 0.05f;
        anyPush.canTransitionToSelf = false;
        anyPush.AddCondition(AnimatorConditionMode.If, 0f, "Push");
        AnimatorStateTransition pushDone = pushState.AddTransition(idleState);
        pushDone.hasExitTime = true;
        pushDone.exitTime = 0.94f;
        pushDone.duration = 0.08f;

        AnimatorStateTransition anyJump = machine.AddAnyStateTransition(jumpState);
        anyJump.hasExitTime = false;
        anyJump.duration = 0.04f;
        anyJump.canTransitionToSelf = false;
        anyJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        AnimatorStateTransition jumpDone = jumpState.AddTransition(idleState);
        jumpDone.hasExitTime = true;
        jumpDone.exitTime = 0.92f;
        jumpDone.duration = 0.10f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void CreatePrefab(Material material, RuntimeAnimatorController controller)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "TuxedoKitten");

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
            throw new InvalidOperationException("The kitten FBX model could not be loaded.");

        GameObject root = new("TuxedoKitten");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = "Rigged Model";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
                materials[index] = material;
            renderer.sharedMaterials = materials;
        }

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
            animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
