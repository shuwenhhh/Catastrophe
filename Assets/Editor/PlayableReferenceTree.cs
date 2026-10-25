using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class PlayableReferenceTree
{
    const string Folder="Assets/Art/LivingRoom/ReferenceCatTree";
    static Transform root;static Material cream,rope;static int meshId;
    [MenuItem("Catastrophe/Build Playable Reference Tree")]
    public static void Build(){if(Application.isPlaying)throw new Exception("Stop Play first");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();meshId=0;
        var old=GameObject.Find("Tripo Cat Tree");if(old){Undo.RecordObject(old,"Preserve original cat tree");old.SetActive(false);}
        var steps=GameObject.Find("Cat tree access perches");if(steps)steps.SetActive(false);
        var existing=GameObject.Find("Reference carpet cat tree");if(existing)Undo.DestroyObjectImmediate(existing);
        root=new GameObject("Reference carpet cat tree").transform;root.SetParent(GameObject.Find("Living Room").transform,false);
        cream=Mat("Oatmeal carpet",new Color(.91f,.75f,.80f));rope=Mat("Natural sisal",new Color(.84f,.62f,.70f));
        Box("Soft rounded base",new Vector3(3.50f,.07f,-2.13f),new Vector3(1.35f,.14f,1.52f),.05f);
        Box("Cubby floor",new Vector3(3.65f,.19f,-2.43f),new Vector3(.83f,.09f,.77f),.035f);
        Box("Cubby roof",new Vector3(3.65f,.625f,-2.43f),new Vector3(.83f,.09f,.77f),.035f);
        Box("Cubby rear wall",new Vector3(4.02f,.40f,-2.43f),new Vector3(.085f,.43f,.74f),.035f);
        foreach(float z in new[]{-2.77f,-2.09f})Box("Cubby side",new Vector3(3.65f,.40f,z),new Vector3(.76f,.43f,.085f),.035f);
        // Front opening looks into the room (-X); real hollow interior, not a painted doorway.
        foreach(float z in new[]{-2.73f,-2.13f})Box("Door jamb",new Vector3(3.27f,.39f,z),new Vector3(.085f,.40f,.22f),.025f);
        Box("Door upper frame",new Vector3(3.27f,.59f,-2.43f),new Vector3(.085f,.07f,.77f),.02f);
        Arch();
        Post(new Vector3(3.10f,.14f,-1.95f),1.08f);
        Bed("Lower landing bed",new Vector3(3.10f,1.16f,-1.95f),.36f,false);
        Post(new Vector3(3.73f,.14f,-1.64f),1.61f);
        Bed("Upper lookout bed",new Vector3(3.73f,1.69f,-1.64f),.35f,true);
        // Leave a walkable gap beside the display shelf's front metal upright.
        root.localPosition=new Vector3(-.8f,0,0);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/ReferenceCatTree.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }
    static Material Mat(string n,Color c){var path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(m);return m;}
    static void Quad(List<int> t,int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d});}
    static GameObject Mesh(string n,List<Vector3> v,List<int> t,Material m,bool solid){var mesh=new Mesh{name=n};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Folder+"/"+n.Replace(" ","_")+"_"+(meshId++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){saved.Clear();saved.SetVertices(v);saved.SetTriangles(t,0);saved.RecalculateNormals();saved.RecalculateBounds();saved.UploadMeshData(false);Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);var g=new GameObject(n);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(solid)g.AddComponent<MeshCollider>().sharedMesh=mesh;else g.AddComponent<SoftDecoration>();return g;}
    static void Box(string n,Vector3 pos,Vector3 size,float radius){var v=new List<Vector3>();var t=new List<int>();int count=10;var half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.98f);var inner=half-Vector3.one*radius;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up,w=Vector3.Cross(normal,u);int offset=v.Count;
            for(int y=0;y<=count;y++)for(int x=0;x<=count;x++){Vector3 p=Vector3.Scale(normal+u*(2f*x/count-1)+w*(2f*y/count-1),half);Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));v.Add(pos+q+(p-q).normalized*radius);}
            for(int y=0;y<count;y++)for(int x=0;x<count;x++){int a=offset+y*(count+1)+x;Quad(t,a,a+1,a+count+2,a+count+1);}}
        Mesh(n,v,t,cream,true);}
    static void Arch(){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=32;i++){float a=Mathf.PI*i/32;foreach(float x in new[]{3.225f,3.31f})foreach(float r in new[]{.20f,.30f})v.Add(new Vector3(x,.36f+Mathf.Sin(a)*r,-2.43f+Mathf.Cos(a)*r));}
        for(int i=0;i<32;i++){int a=i*4,b=a+4;Quad(t,a,b,b+1,a+1);Quad(t,a+2,a+3,b+3,b+2);Quad(t,a,a+2,b+2,b);Quad(t,a+1,b+1,b+3,a+3);}Mesh("Arched cubby entrance",v,t,cream,true);}
    static void Post(Vector3 bottom,float top){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Sisal scratching post";g.transform.SetParent(root,false);g.transform.position=new Vector3(bottom.x,(bottom.y+top)*.5f,bottom.z);g.transform.localScale=new Vector3(.16f,(top-bottom.y)*.5f,.16f);g.GetComponent<Renderer>().sharedMaterial=rope;
        var points=new List<Vector3>();int n=Mathf.CeilToInt((top-bottom.y)/.023f)*24;for(int i=0;i<=n;i++){float y=Mathf.Lerp(bottom.y,top,(float)i/n),a=(y-bottom.y)/.023f*Mathf.PI*2;points.Add(new Vector3(bottom.x+Mathf.Cos(a)*.082f,y,bottom.z+Mathf.Sin(a)*.082f));}Tube("Wound sisal rope",points,.007f,rope,false);}
    static void Bed(string name,Vector3 p,float radius,bool rim){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(root,false);g.transform.position=p-Vector3.up*.04f;g.transform.localScale=new Vector3(radius*2,.04f,radius*2);g.GetComponent<Renderer>().sharedMaterial=cream;Object.DestroyImmediate(g.GetComponent<Collider>());g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;
        var points=new List<Vector3>();for(int i=0;i<=72;i++){float a=i/72f*Mathf.PI*2;points.Add(p+new Vector3(Mathf.Cos(a)*(radius-.015f),-.01f,Mathf.Sin(a)*(radius-.015f)));}Tube("Soft bound edge",points,.025f,cream,false);
        if(rim){points.Clear();for(int i=0;i<=48;i++){float a=(-70f+i/48f*220f)*Mathf.Deg2Rad;points.Add(p+new Vector3(Mathf.Cos(a)*(radius-.03f),.055f,Mathf.Sin(a)*(radius-.03f)));}Tube("Low curved bed bolster",points,.045f,cream,true);}}
    static void Tube(string n,List<Vector3> points,float radius,Material m,bool solid){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<points.Count;i++){var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;var u=Vector3.Cross(tangent,Vector3.up).normalized;if(u.sqrMagnitude<.01f)u=Vector3.right;var w=Vector3.Cross(tangent,u);for(int j=0;j<8;j++){float a=j*Mathf.PI*.25f;v.Add(points[i]+radius*(Mathf.Cos(a)*u+Mathf.Sin(a)*w));}if(i>0)for(int j=0;j<8;j++)Quad(t,(i-1)*8+j,(i-1)*8+(j+1)%8,i*8+(j+1)%8,i*8+j);}Mesh(n,v,t,m,solid);}
}
