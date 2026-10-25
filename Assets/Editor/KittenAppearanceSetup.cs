using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class KittenAppearanceSetup
{
    const string Folder="Assets/Art/SmoothKitten3D/";
    const string Prefab="Assets/Resources/SmoothKitten3D/SmoothKitten3D.prefab";
    [MenuItem("Catastrophe/Apply Black White Kitten and Eyebrows")]
    public static void Apply()
    {
        Shader shader=Shader.Find("Catastrophe/Kitten Black White");
        if(!shader || ShaderUtil.ShaderHasError(shader))throw new Exception("Kitten shader is missing or has compilation errors.");
        string matPath=Folder+"Kitten_BlackWhite.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
        material.shader=shader;
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"SmoothKitten3D_BaseColor.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(material);
        var white=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Kitten_WhiteBrows.mat");
        if(!white){white=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(white,Folder+"Kitten_WhiteBrows.mat");}
        white.SetColor("_BaseColor",new Color(.94f,.94f,.90f));white.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(white);
        GameObject root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {var materials=skin.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;skin.sharedMaterials=materials;}
            Transform head=Find(root.transform,"Head");
            if(!head)throw new Exception("Head bone missing");
            var oldBrows=Find(root.transform,"White eyebrow tufts");
            if(oldBrows)UnityEngine.Object.DestroyImmediate(oldBrows.gameObject);
            oldBrows=Find(root.transform,"White eyebrow dots");
            if(oldBrows)UnityEngine.Object.DestroyImmediate(oldBrows.gameObject);
            {
                var group=new GameObject("White eyebrow dots");group.transform.SetParent(head,false);
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                var baked=new Mesh();skin.BakeMesh(baked);
                var probe=new GameObject("Temporary brow surface probe");
                probe.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);probe.transform.localScale=skin.transform.lossyScale;
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=baked;
                try
                {
                    for(int side=-1;side<=1;side+=2)
                    for(int dot=-1;dot<=1;dot++)
                    {
                        // Three softly overlapping dots read as one small oval eyebrow mark.
                        float centerX=.055f+side*.094f;
                        float x=centerX+dot*.0072f;
                        float y=.105f+(dot==0?.0015f:0f);
                        Ray ray=new Ray(root.transform.TransformPoint(new Vector3(x,y,2)),root.transform.TransformDirection(Vector3.back));
                        if(!collider.Raycast(ray,out var hit,4))throw new Exception("Brow dot missed face surface");
                        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=$"Brow {(side<0?"L":"R")} dot {dot+2}";
                        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                        go.transform.SetParent(group.transform,true);
                        go.transform.SetPositionAndRotation(hit.point+hit.normal*.003f,Quaternion.LookRotation(hit.normal,root.transform.up));
                        go.transform.localScale=new Vector3(.013f,.009f,.004f);
                        go.GetComponent<MeshRenderer>().sharedMaterial=white;
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(probe);UnityEngine.Object.DestroyImmediate(baked);}
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssetIfDirty(material);
        AssetDatabase.SaveAssetIfDirty(white);
        RenderPreview();
        Debug.Log("KITTEN_BLACK_WHITE_APPLIED: original texture retained; two three-dot white eyebrow marks bound to Head.");
    }
    static void RenderPreview()
    {
        var fixture=new GameObject("Temporary kitten preview"){hideFlags=HideFlags.HideAndDontSave};
        var meshes=new System.Collections.Generic.List<Mesh>();
        RenderTexture rt=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            fixture.transform.position=new Vector3(1800,1800,1800);
            var cat=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),fixture.transform);
            foreach(var skin in cat.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                var frozen=new GameObject("Frozen preview mesh");frozen.transform.SetParent(skin.transform,false);
                frozen.AddComponent<MeshFilter>().sharedMesh=mesh;frozen.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
            }
            foreach(var t in cat.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var lightObj=new GameObject("Preview soft light");lightObj.transform.SetParent(fixture.transform,false);
            lightObj.transform.rotation=Quaternion.Euler(35,-25,0);var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.cullingMask=1<<31;
            var camObj=new GameObject("Preview camera");camObj.transform.SetParent(fixture.transform,false);
            camObj.transform.localPosition=new Vector3(.12f,.10f,2f);camObj.transform.LookAt(fixture.transform.position);
            var cam=camObj.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=.56f;cam.cullingMask=1<<31;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.83f,.87f,.91f);
            rt=new RenderTexture(768,768,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
            Directory.CreateDirectory("Validation/SmoothKitten3D");File.WriteAllBytes("Validation/SmoothKitten3D/BlackWhite_Eyebrows.png",image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(fixture);
            foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);
            if(rt)UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static Transform Find(Transform t,string name)
    {if(t.name==name)return t;foreach(Transform c in t){var f=Find(c,name);if(f)return f;}return null;}
}
