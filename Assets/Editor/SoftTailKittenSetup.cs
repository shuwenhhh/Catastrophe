using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class SoftTailKittenSetup
{
    const string Model="Assets/Art/SmoothKitten3D/FixedCurl/SmoothKitten3D_FixedCurl.fbx";
    const string Prefab="Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";
    [MenuItem("Catastrophe/Install Soft Tail Kitten")]
    public static void Install()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first");
        AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType=ModelImporterAnimationType.Generic;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var old=root.transform.Find("Rigged Model");
            var material=old.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            var controller=old.GetComponentInChildren<Animator>().runtimeAnimatorController;
            var next=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),root.scene);
            next.name="Rigged Model";next.transform.SetParent(root.transform,false);
            next.transform.localPosition=old.localPosition;next.transform.localRotation=old.localRotation;next.transform.localScale=old.localScale;
            var brows=old.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="White eyebrow dots");
            var head=next.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
            if(brows)brows.SetParent(head,false);
            const string tailMatPath="Assets/Art/SmoothKitten3D/SoftTail/TailCharcoal.mat";
            var tailMat=AssetDatabase.LoadAssetAtPath<Material>(tailMatPath);
            if(!tailMat){tailMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));tailMat.SetColor("_BaseColor",new Color(.025f,.023f,.022f));tailMat.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(tailMat,tailMatPath);}
            foreach(var skin in next.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.sharedMaterials=Enumerable.Range(0,skin.sharedMaterials.Length).Select(i=>i==0?material:tailMat).ToArray();skin.updateWhenOffscreen=true;}
            var animator=next.GetComponent<Animator>();if(!animator)animator=next.AddComponent<Animator>();
            var baseController=controller is AnimatorOverrideController prior ? prior.runtimeAnimatorController : controller;
            var overrides=new AnimatorOverrideController(baseController);
            foreach(var clip in baseController.animationClips.Distinct())
            {
                var copy=UnityEngine.Object.Instantiate(clip);copy.name=clip.name;
                foreach(var binding in AnimationUtility.GetCurveBindings(copy))
                    if(binding.path.Contains("Tail_"))AnimationUtility.SetEditorCurve(copy,binding,null);
                string path="Assets/Art/SmoothKitten3D/SoftTail/"+clip.name.Replace('|','_').Replace('/','_')+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing){EditorUtility.CopySerialized(copy,existing);UnityEngine.Object.DestroyImmediate(copy);copy=existing;}
                else AssetDatabase.CreateAsset(copy,path);
                overrides[clip]=copy;
            }
            const string overridePath="Assets/Art/SmoothKitten3D/SoftTail/SoftTail.overrideController";
            var saved=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if(saved){EditorUtility.CopySerialized(overrides,saved);UnityEngine.Object.DestroyImmediate(overrides);overrides=saved;}
            else AssetDatabase.CreateAsset(overrides,overridePath);
            animator.runtimeAnimatorController=overrides;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            // Fixed curl is authored in the mesh and bound to Root, without sway.
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();File.WriteAllText("../ArtSource/SoftTailBackup/installed-v3.txt",DateTime.Now.ToString());
        RefinedJumpSetup.Preview();
        Debug.Log("SOFT_TAIL_INSTALLED");
    }
}
