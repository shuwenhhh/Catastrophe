using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RoomCollisionAudit
{
    static RoomCollisionAudit(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("RoomAudit",false)){SessionState.SetBool("RoomAudit",false);EditorApplication.delayCall+=Audit;}};}
    [MenuItem("Catastrophe/Collision/Audit Live Room")]
    public static void Audit()
    {
        if(!Application.isPlaying){SessionState.SetBool("RoomAudit",true);EditorApplication.isPlaying=true;return;}
        var lines=new List<string>();
        var cat=Object.FindFirstObjectByType<CatController>();
        lines.Add("Audit v2 Play="+Application.isPlaying+" cat="+cat);
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            lines.Add($"COL {c.name} parent={c.transform.parent?.name} enabled={c.enabled} trigger={c.isTrigger} rigid={c.attachedRigidbody?.name} bounds={c.bounds}");
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r is not ParticleSystemRenderer))
            if(!r.GetComponentsInParent<Collider>().Any(c=>c.enabled&&!c.isTrigger))
                lines.Add($"REVIEW {r.name} parent={r.transform.parent?.name} bounds={r.bounds}");
        if(cat)
        {
            var clearance=new List<string>();
            var fixture=new GameObject("Disposable table clearance cat");
            fixture.AddComponent<Rigidbody>().isKinematic=true;fixture.AddComponent<CapsuleCollider>();
            var protection=fixture.AddComponent<CatSolidCollision>();
            try
            {
                for(float z=-1.75f;z<.35f;z+=.1f)
                {
                    fixture.transform.SetPositionAndRotation(new Vector3(.55f,.228f,z),Quaternion.identity);
                    Physics.SyncTransforms();
                    foreach(var part in protection.Parts)
                    foreach(var other in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                        if(other.enabled&&!other.isTrigger&&!other.transform.IsChildOf(fixture.transform)&&!other.GetComponentInParent<CatController>()&&
                           Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out float depth)&&depth>.003f)
                            clearance.Add($"z={z:F2} {part.name} vs {other.name} depth={depth:F3} normal={normal}");
                }
            }
            finally{Object.DestroyImmediate(fixture);}
            Directory.CreateDirectory("Validation/RoomCollision");File.WriteAllLines("Validation/RoomCollision/table-clearance.txt",clearance);
            var guard=cat.GetComponent<CatSolidCollision>();var mesh=new Mesh();
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(mesh);
            var pts=mesh.vertices;int missed=0;float maximum=0;var bad=new List<(float d,string text)>();
            for(int i=0;i<pts.Length;i+=10)
            {
                var world=skin.transform.TransformPoint(pts[i]);
                float d=guard.Parts.Min(c=>Vector3.Distance(world,c.ClosestPoint(world)));
                if(d>.02f){missed++;bad.Add((d,$"outside local={cat.transform.InverseTransformPoint(world)} gap={d:F3}"));}maximum=Mathf.Max(maximum,d);
            }
            lines.Add($"MESH_ENVELOPE sampled={(pts.Length+9)/10} outside2cm={missed} maxGap={maximum}");lines.AddRange(bad.OrderByDescending(v=>v.d).Take(25).Select(v=>v.text));Object.DestroyImmediate(mesh);
        }
        Directory.CreateDirectory("Validation/RoomCollision");File.WriteAllLines("Validation/RoomCollision/audit.txt",lines);
        SweepRoom();
        CatSolidCollisionValidation.Run();
        ShelfJumpAudit.Run();
        Debug.Log("ROOM_COLLISION_AUDIT_DONE");
    }

    // Geometry-query regression, not a replacement for gameplay/animation QA.
    // Each real collider is copied away from the room, never saved or moved.
    static void SweepRoom()
    {
        var sources=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c=>c.enabled&&!c.isTrigger&&!c.GetComponentInParent<CatController>()).ToArray();
        var report=new List<string>();int blocked=0,failed=0,clear=0;
        Vector3 origin=new(2000,2000,2000);
        var fixture=new GameObject("Disposable room sweep cat"){hideFlags=HideFlags.HideAndDontSave};
        fixture.transform.position=origin;
        var rb=fixture.AddComponent<Rigidbody>();rb.useGravity=false;
        fixture.AddComponent<CapsuleCollider>();
        var guard=fixture.AddComponent<CatSolidCollision>();
        try
        {
            foreach(var source in sources)
            {
                var obstacle=new GameObject("Disposable "+source.name){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    var copy=(Collider)obstacle.AddComponent(source.GetType());EditorUtility.CopySerialized(source,copy);
                    obstacle.transform.SetPositionAndRotation(origin+source.transform.position-source.bounds.center,source.transform.rotation);
                    obstacle.transform.localScale=source.transform.lossyScale;
                    Physics.SyncTransforms();var bounds=copy.bounds;
                    foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                    foreach(float speed in new[]{1f,20f})
                    {
                        float extent=Mathf.Abs(direction.x)*bounds.extents.x+Mathf.Abs(direction.z)*bounds.extents.z;
                        Vector3 start=origin-direction*(extent+.65f);
                        Quaternion rotation=Quaternion.LookRotation(direction);
                        fixture.transform.SetPositionAndRotation(start,rotation);rb.position=start;rb.rotation=rotation;
                        Physics.SyncTransforms();bool stopped=false,overlap=false;
                        for(int step=0;step<100;step++)
                        {
                            var v=guard.LimitHorizontalVelocity(direction*speed,.02f);
                            if(v.magnitude<speed*.99f)stopped=true;
                            rb.position+=v*.02f;fixture.transform.position=rb.position;Physics.SyncTransforms();
                            if(!guard.CanOccupy(rb.position,rotation)){overlap=true;break;}
                            if(stopped&&v.sqrMagnitude<.00001f)break;
                        }
                        if(overlap){failed++;report.Add($"FAIL {source.name} direction={direction} speed={speed}");}
                        else if(stopped)blocked++;else clear++;
                    }
                }
                finally{Object.DestroyImmediate(obstacle);}
            }
        }
        finally{Object.DestroyImmediate(fixture);}
        report.Insert(0,$"Real room collider geometry: {sources.Length}; blocked approaches={blocked}; open/hollow approaches={clear}; overlaps={failed}. Query test only; does not certify animated mesh or jump landing.");
        File.WriteAllLines("Validation/RoomCollision/sweeps.txt",report);
    }
}
