using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CatTreeRouteSetup
{
    const string Dir="Validation/CatTreeRoute";
    static CatTreeRouteSetup(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TestTreeRoute",false)){SessionState.SetBool("TestTreeRoute",false);EditorApplication.delayCall+=()=>{try{Test();}finally{EditorApplication.isPlaying=false;}};}};}
    [MenuItem("Catastrophe/Build Reachable Cat Tree Route")]
    public static void Build(){if(Application.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Dir);if(!File.Exists(Dir+"/LivingRoom_BeforeRoute.unity"))File.Copy("Assets/LivingRoom.unity",Dir+"/LivingRoom_BeforeRoute.unity");
        var tree=GameObject.Find("Tripo Cat Tree");Undo.RegisterFullObjectHierarchyUndo(tree,"Bring cat tree closer and expose actual platforms");var r=tree.GetComponentInChildren<Renderer>();var center=r.bounds.center;
        // Preserve the original cubby entrance orientation; translate only toward the shelf.
        tree.transform.rotation=Quaternion.Euler(0,90,0);var bounds=r.bounds;
        tree.transform.position+=new Vector3(3.68f,0,-2.03f)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        foreach(var c in tree.GetComponentsInChildren<BoxCollider>()){c.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        foreach(var f in tree.GetComponentsInChildren<MeshFilter>()){var c=f.GetComponent<MeshCollider>();if(!c)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=false;c.enabled=true;c.contactOffset=.003f;}
        PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);
        var old=tree.transform.Find("Reachable route markers");if(old)Object.DestroyImmediate(old.gameObject);var markers=new GameObject("Reachable route markers");markers.transform.SetParent(tree.transform,false);
        var parent=GameObject.Find("Cat tree access perches");if(parent)Undo.DestroyObjectImmediate(parent);parent=new GameObject("Cat tree access perches");parent.transform.SetParent(GameObject.Find("Living Room").transform,false);
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LivingRoom/CozyFurniture/Ivory upholstery.mat");
        foreach(var p in new[]{new Vector3(2.90f,.38f,-2.60f),new Vector3(2.90f,.76f,-2.00f),new Vector3(2.90f,1.14f,-1.40f),new Vector3(2.90f,1.52f,-.80f),new Vector3(2.90f,1.90f,-.20f)}){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Carpeted access perch "+p.y;g.transform.SetParent(parent.transform);g.transform.position=p-Vector3.up*.035f;g.transform.localScale=new Vector3(.59f,.035f,.59f);g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;
            var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.name="Access perch support";post.transform.SetParent(parent.transform);post.transform.position=new Vector3(p.x,(p.y-.07f)*.5f,p.z);post.transform.localScale=new Vector3(.075f,(p.y-.07f)*.5f,.075f);post.GetComponent<Renderer>().sharedMaterial=mat;}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorApplication.delayCall+=Inspect;
    }
    [MenuItem("Catastrophe/Verify Cat Tree Route In Play")]
    public static void Run(){if(Application.isPlaying)throw new Exception("Stop Play first");SessionState.SetBool("TestTreeRoute",true);EditorApplication.isPlaying=true;}
    static void Test(){var lines=new List<string>();Scene source=SceneManager.GetActiveScene(),test=SceneManager.CreateScene("Temporary cat tree physics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));try{
        // Copy all enabled scene obstacles, including actual triangle collisions, not just boxes.
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)){if(c.gameObject.scene!=source||!c.enabled||c.isTrigger||c.GetComponentInParent<CatController>())continue;var g=new GameObject(c.name);SceneManager.MoveGameObjectToScene(g,test);g.transform.SetPositionAndRotation(c.transform.position,c.transform.rotation);g.transform.localScale=c.transform.lossyScale;Collider copy=null;if(c is MeshCollider mc){var a=g.AddComponent<MeshCollider>();a.sharedMesh=mc.sharedMesh;a.convex=mc.convex;copy=a;}else if(c is BoxCollider bc){var a=g.AddComponent<BoxCollider>();a.center=bc.center;a.size=bc.size;copy=a;}else if(c is CapsuleCollider cc){var a=g.AddComponent<CapsuleCollider>();a.center=cc.center;a.radius=cc.radius;a.height=cc.height;a.direction=cc.direction;copy=a;}else if(c is SphereCollider sc){var a=g.AddComponent<SphereCollider>();a.center=sc.center;a.radius=sc.radius;copy=a;}if(copy)copy.contactOffset=.003f;}
        var live=Object.FindFirstObjectByType<CatController>();var player=new GameObject("Actual cat collision envelope");SceneManager.MoveGameObjectToScene(player,test);var capsule=player.AddComponent<CapsuleCollider>();var rb=player.AddComponent<Rigidbody>();rb.mass=live.GetComponent<Rigidbody>().mass;rb.constraints=RigidbodyConstraints.FreezeRotation;rb.linearDamping=live.GetComponent<Rigidbody>().linearDamping;var guard=player.AddComponent<CatSolidCollision>();guard.Initialize();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        float jumpHeight=(float)typeof(CatController).GetField("jumpHeight",flags).GetValue(live),airSpeed=(float)typeof(CatController).GetField("jumpMoveSpeed",flags).GetValue(live),walkSpeed=(float)typeof(CatController).GetField("moveSpeed",flags).GetValue(live);
        lines.Add($"Runtime player={live.name}; skin={live.GetComponentInChildren<SkinnedMeshRenderer>().name}; Animator={live.GetComponentInChildren<Animator>().name}; jump={jumpHeight}; airSpeed={airSpeed}; walkSpeed={walkSpeed}");
        var physics=test.GetPhysicsScene();var waypoints=new[]{new Vector3(3.9f,0,-2.65f),new Vector3(3.90f,.79f,-2.01f),new Vector3(3.39f,1.48f,-1.79f),new Vector3(3.60f,2.09f,-.85f)};
        rb.position=waypoints[0]+Vector3.up*.228f;Physics.SyncTransforms();for(int i=0;i<20;i++)physics.Simulate(.01f);
        for(int k=1;k<waypoints.Length;k++){var target=waypoints[k];var direction=target-(rb.position-Vector3.up*.225f);direction.y=0;var takeoff=rb.position-direction.normalized*(k==1?.12f:0f);for(int j=0;j<60;j++){var delta=takeoff-rb.position;delta.y=0;var vel=rb.linearVelocity;var h=Vector3.ClampMagnitude(delta/.01f,walkSpeed);vel.x=h.x;vel.z=h.z;rb.linearVelocity=guard.LimitHorizontalVelocity(vel,.01f);physics.Simulate(.01f);}rb.rotation=Quaternion.LookRotation(direction);rb.angularVelocity=Vector3.zero;rb.linearVelocity=Vector3.up*Mathf.Sqrt(2*9.81f*jumpHeight);float apex=0;
            for(int i=0;i<140;i++){var delta=target-(rb.position-Vector3.up*.225f);delta.y=0;var velocity=rb.linearVelocity;bool landed=i>20&&Mathf.Abs(rb.position.y-.225f-target.y)<.04f&&Mathf.Abs(velocity.y)<.05f;var h=Vector3.ClampMagnitude(delta/.01f,landed?walkSpeed:airSpeed);velocity.x=h.x;velocity.z=h.z;rb.linearVelocity=guard.LimitHorizontalVelocity(velocity,.01f);if(velocity.y<0)rb.AddForce(Physics.gravity*1.1f,ForceMode.Acceleration);physics.Simulate(.01f);apex=Mathf.Max(apex,rb.position.y-.225f);}
            var foot=rb.position-Vector3.up*.225f;float error=Vector3.Distance(foot,target);lines.Add($"Jump {k}: target={target} feet={foot} apex={apex:F3} error={error:F3}");if(error>.10f)throw new Exception("Could not land route segment "+k);}
        lines.Add("PASS ground -> original cubby -> upper bed -> shelf, actual body/head/tail envelope and full scene collisions.");
    }catch(Exception e){lines.Add("FAIL "+e.Message);Debug.LogException(e);}finally{Directory.CreateDirectory(Dir);File.WriteAllLines(Dir+"/jumps.txt",lines);SceneManager.UnloadSceneAsync(test);}}
    [MenuItem("Catastrophe/Inspect Cat Tree Route")]
    public static void Inspect(){Directory.CreateDirectory(Dir);var lines=new List<string>();foreach(string name in new[]{"Tripo Cat Tree","Tripo Shelving Unit"}){var go=GameObject.Find(name);lines.Add(name+" transform="+go.transform.position);foreach(var r in go.GetComponentsInChildren<Renderer>())lines.Add("Renderer "+r.name+" "+r.bounds);foreach(var c in go.GetComponentsInChildren<Collider>())lines.Add("Collider "+c.name+" "+c.GetType().Name+" enabled="+c.enabled+" "+c.bounds);
        var bins=new Dictionary<int,(float area,Vector3 sum)>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var m=f.sharedMesh;var vv=m.vertices;var tt=m.triangles;for(int i=0;i<tt.Length;i+=3){var a=f.transform.TransformPoint(vv[tt[i]]);var b=f.transform.TransformPoint(vv[tt[i+1]]);var c=f.transform.TransformPoint(vv[tt[i+2]]);var n=Vector3.Cross(b-a,c-a);if(n.normalized.y<.8f)continue;float area=n.magnitude*.5f;var p=(a+b+c)/3;int bin=Mathf.RoundToInt(p.y*20);var prior=bins.ContainsKey(bin)?bins[bin]:(0f,Vector3.zero);bins[bin]=(prior.Item1+area,prior.Item2+p*area);}}foreach(var pair in bins.Where(p=>p.Value.area>.02f).OrderBy(p=>p.Key))lines.Add($"Surface y={pair.Key/20f:F2} area={pair.Value.area:F3} center={pair.Value.sum/pair.Value.area}");}
        File.WriteAllLines(Dir+"/inspect.txt",lines);Capture();}
    [MenuItem("Catastrophe/Capture Cat Tree Route")]
    public static void Capture(){Directory.CreateDirectory(Dir);var g=new GameObject("Route evidence camera");var c=g.AddComponent<Camera>();c.transform.position=new Vector3(1.4f,2.3f,-1.4f);c.transform.LookAt(new Vector3(3.65f,1.05f,-1.2f));c.orthographic=true;c.orthographicSize=1.65f;c.nearClipPlane=.03f;var rt=new RenderTexture(1200,1000,24);var old=RenderTexture.active;c.targetTexture=rt;try{c.Render();RenderTexture.active=rt;var t=new Texture2D(1200,1000,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,1000),0,0);t.Apply();File.WriteAllBytes(Dir+"/Route.png",t.EncodeToPNG());Object.DestroyImmediate(t);}finally{c.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(g);}}
}
