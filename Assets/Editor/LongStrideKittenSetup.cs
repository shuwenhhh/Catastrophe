using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LongStrideKittenSetup
{
    const string ModelPath = "Assets/Art/SmoothKitten3D/LongStride/SmoothKitten3D_LongStride.fbx";
    const string PrefabPath = "Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";
    const string ControllerPath = "Assets/Art/SmoothKitten3D/SmoothKitten3D.controller";

    [MenuItem("Catastrophe/Install Long Stride Kitten")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play before replacing the walk model.");
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Long-stride FBX has not imported.");
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        AnimationClip walk = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>().FirstOrDefault(c => c.name.EndsWith("Cat_Walk"));
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (model == null || walk == null || controller == null)
            throw new InvalidOperationException("Long-stride model, walk clip, or controller is missing.");
        var state = controller.layers.SelectMany(layer => layer.stateMachine.states)
            .Select(child => child.state).FirstOrDefault(s => s.name == "Walk");
        if (state == null) throw new InvalidOperationException("The Animator has no Walk state.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform oldModel = root.transform.Find("Rigged Model");
            if (oldModel == null) throw new InvalidOperationException("The existing model is missing from the prefab.");
            Transform oldBrows = oldModel.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "White eyebrow dots");
            Material catMaterial = oldModel.GetComponentInChildren<SkinnedMeshRenderer>(true)?.sharedMaterial;

            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
            replacement.name = "Rigged Model";
            replacement.transform.SetParent(root.transform, false);
            replacement.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            replacement.transform.localScale = Vector3.one;
            Transform newHead = replacement.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Head");
            if (oldBrows != null && newHead == null)
                throw new InvalidOperationException("The replacement model has no matching Head bone.");
            if (oldBrows != null) oldBrows.SetParent(newHead, false);
            foreach (SkinnedMeshRenderer skin in replacement.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                if (catMaterial != null)
                    skin.sharedMaterials = Enumerable.Repeat(catMaterial, skin.sharedMaterials.Length).ToArray();
            }
            foreach (Collider collider in replacement.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            Animator animator = replacement.GetComponent<Animator>();
            if (animator == null) animator = replacement.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Object.DestroyImmediate(oldModel.gameObject);
            state.motion = walk;
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"LONG_STRIDE_INSTALLED: model={ModelPath}, clip={walk.name}, prefab={PrefabPath}; original FBX preserved.");
    }
}
