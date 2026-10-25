using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class UniformShelfPass
{
    [MenuItem("Catastrophe/Reference Room/Inspect Current Shelf")]
    public static void Inspect()
    {
        var root=GameObject.Find("Tripo Shelving Unit");
        Directory.CreateDirectory("Validation/UniformShelf");
        File.WriteAllLines("Validation/UniformShelf/before.txt",root.GetComponentsInChildren<Renderer>(true).Select(r=>$"RENDER {r.name} enabled={r.enabled} bounds={r.bounds} mesh={r.GetComponent<MeshFilter>()?.sharedMesh?.name} mats={string.Join(",",r.sharedMaterials.Select(m=>m.name))}")
            .Concat(root.GetComponentsInChildren<Collider>(true).Select(c=>$"COLLIDER {c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} bounds={c.bounds}")));
        File.WriteAllLines("Validation/UniformShelf/nearby.txt",Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.bounds.center.x>2.7f&&r.bounds.size.z>.5f).Select(r=>$"{r.name} parent={r.transform.parent?.name} {r.bounds}"));
    }
    [MenuItem("Catastrophe/Reference Room/Make All Shelf Boards Equal")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play first");
        var root=GameObject.Find("Tripo Shelving Unit");
        Undo.RegisterFullObjectHierarchyUndo(root,"Equal shelf boards");
        var boards=root.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.StartsWith("Climbable Shelf Platform")).OrderBy(c=>c.bounds.max.y).ToArray();
        if(boards.Length!=4)throw new Exception("Expected four shelf platforms");
        // Preserve the current common collision footprint and tier heights.
        var footprint=boards[0].bounds;
        var source=root.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh && f.sharedMesh.subMeshCount==2);
        var original=source.GetComponent<Renderer>();
        string meshPath="Assets/Settings/LivingRoom/ReferenceShelf/UniformShelfFrame.asset";
        var frame=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(!frame)
        {
            frame=Object.Instantiate(source.sharedMesh);var tris=frame.GetTriangles(1);frame.subMeshCount=1;frame.SetTriangles(tris,0);frame.RecalculateBounds();
            AssetDatabase.CreateAsset(frame,meshPath);
        }
        var frameGo=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Uniform Shelf Frame");
        if(!frameGo){var g=new GameObject("Uniform Shelf Frame");frameGo=g.transform;frameGo.SetParent(source.transform,false);g.AddComponent<MeshFilter>().sharedMesh=frame;g.AddComponent<MeshRenderer>().sharedMaterial=original.sharedMaterials[1];}
        original.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(original);
        foreach(var board in boards)
        {
            var b=board.bounds;var center=new Vector3(footprint.center.x,b.center.y,footprint.center.z);
            board.transform.position+=center-b.center;
            // Geometry is generated from the collider's own local size/center,
            // so no invisible ledge or double slab can diverge from its surface.
            var visual=board.transform.Find("Matching Oak Board");
            if(!visual){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Matching Oak Board";Object.DestroyImmediate(g.GetComponent<Collider>());visual=g.transform;visual.SetParent(board.transform,false);}
            visual.localPosition=board.center;visual.localRotation=Quaternion.identity;visual.localScale=board.size;
            visual.GetComponent<Renderer>().sharedMaterial=original.sharedMaterials[0];
            foreach(var r in board.GetComponentsInChildren<Renderer>())if(r.transform!=visual)r.enabled=false;
        }
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Cat access step")||t.name.StartsWith("Cat access frame")))t.gameObject.SetActive(false);
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        ShelfJumpAudit.Run();Capture();
    }
    static void Capture()
    {
        var root=GameObject.Find("Tripo Shelving Unit");var boards=root.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Climbable Shelf Platform")).ToArray();
        var b=boards[0].bounds;foreach(var c in boards)b.Encapsulate(c.bounds);
        var go=new GameObject("Shelf check camera");var cam=go.AddComponent<Camera>();cam.transform.position=b.center+new Vector3(-3,1,-2.5f);cam.transform.LookAt(b.center);cam.fieldOfView=55;
        var rt=new RenderTexture(1200,900,24);var old=RenderTexture.active;cam.targetTexture=rt;
        try{cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();Directory.CreateDirectory("Validation/UniformShelf");File.WriteAllBytes("Validation/UniformShelf/after.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
