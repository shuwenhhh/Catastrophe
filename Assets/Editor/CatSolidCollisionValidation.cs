using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CatSolidCollisionValidation
{
    static CatSolidCollisionValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool("CatSolidCollisionCheckedV2", false)) return;
            SessionState.SetBool("CatSolidCollisionCheckedV2", true);
            Run();
        };
    }

    [MenuItem("Catastrophe/Validate Cat Solid Collision")]
    public static void Run()
    {
        GameObject cat = null, wall = null;
        var lines = new List<string>();
        try
        {
            // Far from the authored room. No scene loading, saving or simulation
            // of the user's game: only disposable query fixtures.
            Vector3 origin = new Vector3(1000, 1000, 1000);
            cat = new GameObject("Collision query fixture") { hideFlags = HideFlags.HideAndDontSave };
            cat.transform.position = origin;
            var rb = cat.AddComponent<Rigidbody>(); rb.useGravity = false;
            var guard = cat.AddComponent<CatSolidCollision>(); guard.Initialize();
            wall = new GameObject("Thin table leg fixture") { hideFlags = HideFlags.HideAndDontSave };
            var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(.04f, 2, 2);
            wall.transform.position = origin + Vector3.right;
            Physics.SyncTransforms();
            Check(guard.CanOccupy(origin,Quaternion.identity),"clear position allowed",lines);
            Check(!guard.CanOccupy(wall.transform.position,Quaternion.identity),"solid overlap rejected",lines);
            wall.transform.position = origin + new Vector3(.035f,.37f,-.2f);
            box.size = Vector3.one * .04f;
            Physics.SyncTransforms();
            Check(!guard.CanOccupy(origin,Quaternion.identity),"raised tail cannot intersect a post",lines);
            box.size = new Vector3(.04f,2,2);
            wall.transform.position = origin + Vector3.right;
            Physics.SyncTransforms();
            // Repeated steps, including an artificially high movement speed.
            foreach(float speed in new[]{.65f,20f})
            {
                rb.position=origin;cat.transform.position=origin;Physics.SyncTransforms();
                for(int i=0;i<160;i++)
                {
                    Vector3 v=guard.LimitHorizontalVelocity(Vector3.right*speed,.02f);
                    rb.position += v*.02f;
                    cat.transform.position=rb.position;
                    Physics.SyncTransforms();
                }
                Check(rb.position.x < wall.transform.position.x && guard.CanOccupy(rb.position,Quaternion.identity),"thin-leg sweep stops speed="+speed,lines);
            }
            box.size=new Vector3(.04f,2,2);wall.transform.position=origin+Vector3.right*.30f;
            rb.position=origin;cat.transform.position=origin;Physics.SyncTransforms();
            Check(guard.CanOccupy(origin,Quaternion.identity),"parallel pose fits beside obstacle",lines);
            Check(!guard.CanOccupy(origin,Quaternion.Euler(0,90,0)),"nose/tail turn into obstacle rejected",lines);
            box.isTrigger=true;Physics.SyncTransforms();
            Check(guard.CanOccupy(wall.transform.position,Quaternion.identity),"interaction triggers do not block",lines);
            Check(rb.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"continuous collision enabled for jump/fall",lines);
            CheckTable(lines);
            lines.Add("PASS: no authored room or scene file modified.");
            Debug.Log("CAT_SOLID_COLLISION_VALIDATION_PASSED");
        }
        catch(Exception e){lines.Add("FAIL "+e);Debug.LogException(e);}
        finally
        {
            if(cat) UnityEngine.Object.DestroyImmediate(cat);
            if(wall) UnityEngine.Object.DestroyImmediate(wall);
            Directory.CreateDirectory("Validation");
            File.WriteAllLines("Validation/CatSolidCollision.txt",lines);
        }
    }
    static void Check(bool pass,string label,List<string> lines)
    {if(!pass)throw new InvalidOperationException(label);lines.Add("PASS "+label);}

    static void CheckTable(List<string> lines)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tripo/Prefabs/Tripo_Table.prefab");
        GameObject root=new GameObject("Furniture collision test") { hideFlags=HideFlags.HideAndDontSave };
        try
        {
            root.transform.position=new Vector3(1200,1200,1200);
            var table=UnityEngine.Object.Instantiate(prefab,root.transform);table.name="Table";
            int added=CatSolidCollision.EnsureFurnitureCollisions(root);
            Check(added>0,"table receives solid mesh collision including legs",lines);
            Physics.SyncTransforms();
            bool hitLeg=false;
            foreach(var mesh in table.GetComponentsInChildren<MeshCollider>())
            {
                Bounds b=mesh.bounds;
                for(int i=0;i<=40;i++)
                {
                    Vector3 from=new Vector3(b.min.x-.1f,b.min.y+b.size.y*.2f,Mathf.Lerp(b.min.z,b.max.z,i/40f));
                    if(mesh.Raycast(new Ray(from,Vector3.right),out _,b.size.x+.2f)) hitLeg=true;
                }
            }
            Check(hitLeg,"ray below tabletop hits actual table-leg geometry",lines);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
