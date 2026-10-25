using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChairCompoundCollisionSetup
{
    static ChairCompoundCollisionSetup(){EditorApplication.playModeStateChanged+=RunAfterPlay;}
    const string Path="Assets/Models/Tripo/Prefabs/Tripo_Chair.prefab";
    const string Folder="Assets/Models/Tripo/ChairCollision";
    [MenuItem("Catastrophe/Prepare Separate Chair Collisions")]
    public static void Prepare()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before editing prefab");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Models/Tripo","ChairCollision");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var verts=new List<Vector3>();var tris=new List<int>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                int offset=verts.Count;var matrix=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                verts.AddRange(filter.sharedMesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)));
                tris.AddRange(filter.sharedMesh.triangles.Select(i=>i+offset));
            }
            var bounds=new Bounds(verts[0],Vector3.zero);foreach(var v in verts)bounds.Encapsulate(v);
            var histogram=new float[100];
            for(int i=0;i<tris.Count;i+=3)
            {
                Vector3 a=verts[tris[i]],b=verts[tris[i+1]],c=verts[tris[i+2]],n=Vector3.Cross(b-a,c-a);
                float y=((a+b+c)/3).y;int bin=Mathf.Clamp(Mathf.FloorToInt((y-bounds.min.y)/bounds.size.y*100),0,99);
                if(bin>=30&&bin<=65&&Mathf.Abs(n.normalized.y)>.85f)histogram[bin]+=n.magnitude;
            }
            // Choose the upper broad horizontal surface: the widest underside
            // or cross-brace can have more area than the actual seat cushion.
            float peakArea=histogram.Max();int peak=Array.FindLastIndex(histogram,area=>area>=peakArea*.15f&&area>0);
            if(peak<0)throw new Exception("Cannot locate seat surface");
            float seat=bounds.min.y+(peak+.5f)/100*bounds.size.y;
            var groups=Enumerable.Range(0,6).Select(_=>new List<int>()).ToArray();
            for(int i=0;i<tris.Count;i+=3)
            {
                Vector3 center=(verts[tris[i]]+verts[tris[i+1]]+verts[tris[i+2]])/3;
                int group=center.y>seat+bounds.size.y*.035f?5:center.y>seat-bounds.size.y*.08f?4:
                    (center.x>bounds.center.x?1:0)+(center.z>bounds.center.z?2:0);
                groups[group].AddRange(new[]{tris[i],tris[i+1],tris[i+2]});
            }
            if(groups.Any(g=>g.Count==0))throw new Exception("Chair partition missing a leg, seat or backrest");
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)UnityEngine.Object.DestroyImmediate(collider);
            var old=root.transform.Find("Chair surface collisions");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var parent=new GameObject("Chair surface collisions");parent.transform.SetParent(root.transform,false);
            for(int i=0;i<6;i++)
            {
                string name=i==4?"Seat":i==5?"Backrest":"Leg "+(i+1);
                var used=groups[i].Distinct().ToArray();var remap=new Dictionary<int,int>();for(int j=0;j<used.Length;j++)remap[used[j]]=j;
                var mesh=new Mesh{name="Chair "+name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(used.Select(j=>verts[j]).ToList());mesh.SetTriangles(groups[i].Select(j=>remap[j]).ToArray(),0);mesh.RecalculateBounds();
                string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var piece=new GameObject(name);piece.transform.SetParent(parent.transform,false);var collider=piece.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;collider.contactOffset=.003f;
            }
            PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Validation/ChairCollision");File.WriteAllText("Validation/ChairCollision/partition.txt",$"Chair bounds={bounds}; seat y={seat:F4}; triangles={tris.Count/3}; assigned={groups.Sum(g=>g.Count)/3}; partitions="+string.Join(",",groups.Select(g=>g.Count/3)));
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    [MenuItem("Catastrophe/Verify Chair Jump In Play Mode")]
    public static void Test()
    {
        if(!EditorApplication.isPlaying)
        {
            SessionState.SetBool("RunChairJumpOnce",true);EditorApplication.isPlaying=true;return;
        }
        var live=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).First(c=>c.name=="Seat"&&c.transform.parent.name=="Chair surface collisions");
        var liveChair=live.transform.parent.parent;
        var scene=SceneManager.CreateScene("Disposable chair physics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
        var chair=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));SceneManager.MoveGameObjectToScene(chair,scene);
        var cat=new GameObject("Disposable jump cat");SceneManager.MoveGameObjectToScene(cat,scene);
        var floor=new GameObject("Disposable floor");SceneManager.MoveGameObjectToScene(floor,scene);
        var report=new List<string>();
        try
        {
            chair.transform.rotation=Quaternion.identity;chair.transform.localScale=liveChair.lossyScale;chair.transform.position=Vector3.zero;
            var all=chair.GetComponentsInChildren<MeshCollider>();Bounds bounds=GeometryBounds(all[0]);foreach(var c in all)bounds.Encapsulate(GeometryBounds(c));
            chair.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            Physics.SyncTransforms();physics.Simulate(.0001f);
            var seat=all.First(c=>c.name=="Seat");var back=all.First(c=>c.name=="Backrest");
            if(chair.GetComponentsInChildren<BoxCollider>().Any(c=>c.enabled&&!c.isTrigger))throw new Exception("Broad chair box still enabled");
            report.Add("PASS four legs, seat and backrest use six separate mesh colliders; no broad box");
            var ground=floor.AddComponent<BoxCollider>();ground.size=new Vector3(10,.1f,10);floor.transform.position=new Vector3(0,-.05f,0);
            var rb=cat.AddComponent<Rigidbody>();rb.constraints=RigidbodyConstraints.FreezeRotation;rb.mass=1.5f;cat.AddComponent<CapsuleCollider>();var guard=cat.AddComponent<CatSolidCollision>();
            var probe=cat.AddComponent<ChairContactProbe>();
            Bounds seatBounds=GeometryBounds(seat),backBounds=GeometryBounds(back);
            var high=back.sharedMesh.vertices.Select(v=>back.transform.TransformPoint(v)).Where(v=>v.y>backBounds.center.y).ToArray();
            Vector3 highCenter=high.Aggregate(Vector3.zero,(sum,v)=>sum+v)/high.Length;
            Vector3 towardBack=highCenter-seatBounds.center;towardBack.y=0;
            Vector3 direction=Mathf.Abs(towardBack.x)>Mathf.Abs(towardBack.z)?Vector3.right*Mathf.Sign(towardBack.x):Vector3.forward*Mathf.Sign(towardBack.z);
            float extent=Mathf.Abs(direction.x)*seatBounds.extents.x+Mathf.Abs(direction.z)*seatBounds.extents.z;
            cat.transform.rotation=Quaternion.LookRotation(direction);
            var start=seatBounds.center-direction*(extent+.40f);start.y=.228f;cat.transform.position=start;Physics.SyncTransforms();physics.Simulate(.0001f);
            float maxHeight=0;bool landed=false;float seatTop=seatBounds.max.y;
            report.Add($"Actual seat bounds {seatBounds}; live enabled={live.enabled}; scale={liveChair.lossyScale}");
            report.Add($"Backrest bounds={backBounds}; direction={direction}");
            const float heldJumpHeight=1.40f;
            if(seatTop<.2f||seatTop>=heldJumpHeight)throw new Exception("Seat height is outside the held jump reach");
            rb.linearVelocity=Vector3.up*Mathf.Sqrt(2*9.81f*heldJumpHeight);
            for(int i=0;i<180;i++)
            {
                var velocity=rb.linearVelocity;float speed=i>55?.26f:.90f;velocity.x=direction.x*speed;velocity.z=direction.z*speed;
                if(i>110){velocity.x=0;velocity.z=0;}rb.linearVelocity=guard.LimitHorizontalVelocity(velocity,.01f);
                if(velocity.y<0)rb.AddForce(Physics.gravity*1.1f,ForceMode.Acceleration);
                physics.Simulate(.01f);maxHeight=Mathf.Max(maxHeight,rb.position.y-.225f);
                if(i>125&&Mathf.Abs(rb.linearVelocity.y)<.05f&&Mathf.Abs(rb.position.y-.225f-seatTop)<.04f){landed=true;break;}
            }
            report.Add($"Jump: seatTop={seatTop:F3}, apex feet={maxHeight:F3}, landing feet={rb.position.y-.225f:F3}, success={landed}; held jumpHeight={heldJumpHeight:F2}");
            report.AddRange(probe.Contacts.Values);
            // Verify the support surface itself, not just the final height.
            cat.transform.SetPositionAndRotation(rb.position,rb.rotation);
            Capture(cat,chair,scene,seatBounds.center);
            if(!landed)throw new Exception("Chair landing failed");
            if(!seat.Raycast(new Ray(rb.position+rb.rotation*new Vector3(.04f,0,0),Vector3.down),out var support,.30f))throw new Exception($"Cat is not supported by the seat: root={rb.position}, seat={seatBounds}, collider={seat.bounds}");
            report.Add($"PASS direct seat support ray at {support.point}");
            report.Add("PASS jump reaches seat with separated cat body/head/tail colliders");
        }
        catch(Exception e){report.Add("FAIL "+e.Message);Debug.LogException(e);}
        finally
        {
            UnityEngine.Object.DestroyImmediate(cat);UnityEngine.Object.DestroyImmediate(chair);UnityEngine.Object.DestroyImmediate(floor);SceneManager.UnloadSceneAsync(scene);
            File.WriteAllLines("Validation/ChairCollision/play.txt",report);
        }
    }
    static void Capture(GameObject cat,GameObject chair,Scene scene,Vector3 target)
    {
        var visual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("SmoothKitten3D/SmoothKitten3D"),cat.transform);
        visual.transform.localPosition=Vector3.down*.225f + (new Vector3(0,.063675f,0)+Vector3.up*.225f)*CatSolidCollision.SizeFactor;
        visual.transform.localScale=Vector3.one*(.65f*CatSolidCollision.SizeFactor);
        var animator=visual.GetComponentInChildren<Animator>();animator.enabled=false;
        var clip=animator.runtimeAnimatorController.animationClips.First(c=>c.name.Contains("Idle"));clip.SampleAnimation(animator.gameObject,0);
        var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();skin.updateWhenOffscreen=true;
        foreach(var t in cat.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        foreach(var t in chair.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        var cameraGo=new GameObject("Evidence camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var cam=cameraGo.AddComponent<Camera>();cam.transform.position=target+new Vector3(1.5f,1.0f,1.6f);cam.transform.LookAt(target+Vector3.up*.15f);cam.orthographic=true;cam.orthographicSize=.85f;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.8f,.85f,.9f);
        var lightGo=new GameObject("Evidence light");SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(40,-30,0);
        var rt=new RenderTexture(800,800,24);var tex=new Texture2D(800,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,800),0,0);tex.Apply();File.WriteAllBytes("Validation/ChairCollision/LandedOnSeat.png",tex.EncodeToPNG());}
        finally{RenderTexture.active=previous;foreach(var o in new UnityEngine.Object[]{cameraGo,lightGo,rt,tex})UnityEngine.Object.DestroyImmediate(o);}
    }
    static void RunAfterPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("RunChairJumpOnce",false))return;
        SessionState.SetBool("RunChairJumpOnce",false);
        EditorApplication.delayCall+=()=>{try{Test();}finally{EditorApplication.isPlaying=false;}};
    }
    static Bounds GeometryBounds(MeshCollider collider)
    {
        // Collision meshes keep the shared vertex buffer; bounds must use only
        // referenced triangles, not unused vertices from the other chair parts.
        var mesh=collider.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
        var bounds=new Bounds(collider.transform.TransformPoint(vertices[indices[0]]),Vector3.zero);
        foreach(int i in indices)bounds.Encapsulate(collider.transform.TransformPoint(vertices[i]));
        return bounds;
    }
}
