using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Real mesh authoring: cushions, bound seams, a hollow cubby and wound sisal.
// The previous furniture is kept disabled, and no neighbouring objects move.
public static class StorybookCatTreeBuilder
{
    const string Folder = "Assets/Art/LivingRoom/StorybookCatTree";
    const string Evidence = "Validation/StorybookCatTree";
    static Transform root;
    static Material blush, cream, rope, piping, inset;
    static int sequence;

    [MenuItem("Catastrophe/Storybook Cat Tree/1 Build Soft Pink Tree")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/LivingRoom.unity") throw new Exception("Open LivingRoom in Edit Mode.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Evidence); AssetDatabase.Refresh();
        string backup = Evidence + "/BeforeSoftTree.unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Scene backup failed.");
        var old = GameObject.Find("Reference carpet cat tree");
        if (!old) throw new Exception("Current cat tree was not found.");
        var neighbours = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && !t.IsChildOf(old.transform)).ToDictionary(t => t, t => t.localToWorldMatrix.ToString("F6"));
        var parent = old.transform.parent; var position = old.transform.localPosition;
        var rotation = old.transform.localRotation; var scale = old.transform.localScale;
        Undo.RegisterFullObjectHierarchyUndo(old, "Preserve original cat tree");
        old.name = "Previous cat tree (preserved " + DateTime.Now.ToString("HHmmss") + ")"; old.SetActive(false);
        root = new GameObject("Reference carpet cat tree").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Soft illustrated cat tree");
        root.SetParent(parent, false); root.localPosition = position; root.localRotation = rotation; root.localScale = scale;
        sequence = 0;
        blush = Mat("Strawberry milk plush", "F3BDCD", .08f);
        cream = Mat("Vanilla cushion", "FFF0D8", .10f);
        rope = Mat("Natural oatmeal sisal", "D6B88C", .06f);
        piping = Mat("Rose bound seams", "DF94B0", .10f);
        inset = Mat("Soft pink cushion", "FFE0E6", .06f);

        RoundedBox("Soft rounded base", new Vector3(3.50f,.07f,-2.13f),new Vector3(1.35f,.14f,1.52f),.065f,blush,true);
        // A thin vanilla binding follows the rounded footprint, not a blocky second base.
        RoundedRectangleTube("Vanilla base binding",new Vector3(3.50f,.065f,-2.13f),.645f,.73f,.14f,.014f,cream);
        HollowCubby();
        Post(new Vector3(3.10f,.14f,-1.95f),1.08f);
        Bed("Lower landing bed",new Vector3(3.10f,1.16f,-1.95f),.36f,false);
        Post(new Vector3(3.73f,.14f,-1.64f),1.61f);
        Bed("Upper lookout bed",new Vector3(3.73f,1.69f,-1.64f),.46f,true);
        // A yarn toy hangs beside the route, not across the cubby entrance.
        var cord = new List<Vector3>();
        for(int i=0;i<=12;i++){float t=i/12f;cord.Add(new Vector3(3.02f+.018f*Mathf.Sin(t*Mathf.PI),1.10f-.27f*t,-1.64f));}
        Tube("Hanging cream cord",cord,.009f,cream,false);
        Sphere("Round yarn pompom",new Vector3(3.02f,.785f,-1.64f),new Vector3(.12f,.12f,.12f),inset);
        for(int k=0;k<6;k++)
        {
            var yarn=new List<Vector3>();float a=k*Mathf.PI/6;
            for(int j=0;j<=36;j++){float b=j*Mathf.PI*2/36;yarn.Add(new Vector3(3.02f,.785f,-1.64f)+new Vector3(Mathf.Cos(b)*Mathf.Cos(a),Mathf.Sin(b),Mathf.Cos(b)*Mathf.Sin(a))*.059f);}
            Tube("Pompom yarn strand",yarn,.003f,piping,false);
        }
        // Small raised paw applique on the front-right part of the base.
        Sphere("Paw pad",new Vector3(3.40f,.145f,-2.68f),new Vector3(.105f,.012f,.08f),inset);
        for(int i=0;i<3;i++)Sphere("Paw toe",new Vector3(3.35f+i*.048f,.146f,-2.745f),new Vector3(.036f,.012f,.043f),inset);
        var marker=root.gameObject.AddComponent<StorybookCatTree>();
        marker.insideAnchor=Anchor("Inside cat tree cubby",new Vector3(3.55f,.466f,-2.43f));
        marker.exitAnchor=Anchor("Outside cat tree cubby",new Vector3(2.91f,.371f,-2.43f));
        if(neighbours.Any(p=>!p.Key||p.Key.localToWorldMatrix.ToString("F6")!=p.Value))throw new Exception("A neighbouring object moved; abort save.");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,Folder+"/StorybookCatTree.prefab",InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var meshes=root.GetComponentsInChildren<MeshFilter>();
        File.WriteAllText(Evidence+"/authoring.txt",$"Saved soft pink tree. Prior tree kept disabled.\nUnchanged neighbours: {neighbours.Count}.\nMesh renderers: {meshes.Length}; vertices: {meshes.Sum(m=>m.sharedMesh.vertexCount)}; triangles: {meshes.Sum(m=>m.sharedMesh.triangles.Length/3)}.\nCubby floor 0.235, roof 1.105; perches 1.16 / 1.69. Hollow shell, no solid box over doorway.\n");
        Debug.Log("STORYBOOK_CAT_TREE_BUILT: hollow pink cubby, vanilla cushions, oatmeal posts, yarn toy.");
    }
    static Transform Anchor(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,270,0);return t;}
    static Material Mat(string name,string hex,float smooth)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        ColorUtility.TryParseHtmlString("#"+hex,out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",smooth);
        EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
    static void Quad(List<int> t,int a,int b,int c,int d,bool reverse=false){if(reverse)t.AddRange(new[]{a,c,b,a,d,c});else t.AddRange(new[]{a,b,c,a,c,d});}
    static GameObject Mesh(string name,List<Vector3> v,List<int> tri,Material mat,bool solid,List<Vector3> normals=null)
    {
        string path=Folder+"/Mesh_"+(sequence++).ToString("D2")+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.name=name;mesh.indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(v);mesh.SetTriangles(tri,0);if(normals!=null)mesh.SetNormals(normals);else mesh.RecalculateNormals();mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
        var g=new GameObject(name);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(solid){var c=g.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.contactOffset=.003f;}
        else g.AddComponent<SoftDecoration>();return g;
    }
    static void RoundedBox(string name,Vector3 p,Vector3 size,float radius,Material mat,bool solid)
    {
        var v=new List<Vector3>();var t=new List<int>();var normals=new List<Vector3>();const int count=12;var half=size*.5f;
        radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.98f);var inner=half-Vector3.one*radius;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;var w=Vector3.Cross(normal,u);int start=v.Count;
            for(int y=0;y<=count;y++)for(int x=0;x<=count;x++)
            {var point=Vector3.Scale(normal+u*(2f*x/count-1)+w*(2f*y/count-1),half);var q=new Vector3(Mathf.Clamp(point.x,-inner.x,inner.x),Mathf.Clamp(point.y,-inner.y,inner.y),Mathf.Clamp(point.z,-inner.z,inner.z));var n=(point-q).normalized;v.Add(p+q+n*radius);normals.Add(n);}
            for(int y=0;y<count;y++)for(int x=0;x<count;x++){int a=start+y*(count+1)+x;Quad(t,a,a+1,a+count+2,a+count+1);}
        }
        Mesh(name,v,t,mat,solid,normals);
    }
    static Vector2 Outline(float angle,float minY,float maxY,float halfZ,float radius)
    {
        // Ray-intersect a rounded rectangle; common angular samples allow an
        // outer wall, inner cavity and doorway to form a watertight shell.
        var center=new Vector2(.60f,0);var direction=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
        var boxCenter=new Vector2((minY+maxY)*.5f,0);var half=new Vector2((maxY-minY)*.5f,halfZ);
        float low=0,high=2;
        for(int i=0;i<24;i++){float mid=(low+high)*.5f;var q=center+direction*mid-boxCenter;q=new Vector2(Mathf.Abs(q.x),Mathf.Abs(q.y))-half+Vector2.one*radius;float sdf=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;if(sdf<=0)low=mid;else high=mid;}
        return center+direction*low;
    }
    static Vector2 Door(float angle)
    {
        var center=new Vector2(.60f,0);var d=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));float low=0,high=2;
        for(int i=0;i<24;i++){float mid=(low+high)*.5f;var p=center+d*mid;bool inside=p.x>=.235f&&Mathf.Abs(p.y)<=.32f&&(p.x<=.62f||(p.y*p.y+(p.x-.62f)*(p.x-.62f)<=.32f*.32f));if(inside)low=mid;else high=mid;}return center+d*low;
    }
    static void HollowCubby()
    {
        const int n=96;var v=new List<Vector3>();var t=new List<int>();
        // Outer edge rounds over 6 cm, with a flat, playable roof.
        var xs=new[]{3.185f,3.203f,3.245f,4.055f,4.097f,4.115f};var offsets=new[]{.06f,.018f,0f,0f,.018f,.06f};
        for(int j=0;j<xs.Length;j++)for(int i=0;i<n;i++){var p=Outline(i*Mathf.PI*2/n,.145f+offsets[j],1.105f-offsets[j],.443f-offsets[j],.17f);v.Add(new Vector3(xs[j],p.x,-2.43f+p.y));}
        for(int j=0;j<xs.Length-1;j++)for(int i=0;i<n;i++)Quad(t,j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i,true);
        int rear=v.Count;v.Add(new Vector3(4.115f,.60f,-2.43f));for(int i=0;i<n;i++)t.AddRange(new[]{rear,5*n+(i+1)%n,5*n+i});
        int opening=v.Count;for(int i=0;i<n;i++){var p=Door(i*Mathf.PI*2/n);v.Add(new Vector3(3.185f,p.x,-2.43f+p.y));}
        for(int i=0;i<n;i++)Quad(t,i,(i+1)%n,opening+(i+1)%n,opening+i);
        int cavity=v.Count;foreach(float x in new[]{3.26f,4.025f})for(int i=0;i<n;i++){var p=Outline(i*Mathf.PI*2/n,.235f,.985f,.353f,.08f);v.Add(new Vector3(x,p.x,-2.43f+p.y));}
        for(int i=0;i<n;i++){int b=(i+1)%n;Quad(t,opening+i,opening+b,cavity+b,cavity+i);Quad(t,cavity+i,cavity+b,cavity+n+b,cavity+n+i);}
        int back=v.Count;v.Add(new Vector3(4.025f,.60f,-2.43f));for(int i=0;i<n;i++)t.AddRange(new[]{back,cavity+n+i,cavity+n+(i+1)%n});
        Mesh("Rounded hollow cubby",v,t,blush,true);
        var binding=new List<Vector3>();for(int i=0;i<=n;i++){var p=Door(i*Mathf.PI*2/n);binding.Add(new Vector3(3.174f,p.x,-2.43f+p.y));}
        Tube("Vanilla doorway piping",binding,.019f,cream,false);
        RoundedBox("Little sleeping cushion",new Vector3(3.64f,.243f,-2.43f),new Vector3(.62f,.016f,.57f),.007f,inset,false);
        // The roof's low, padded centre is flush enough for the existing jump route.
        RoundedBox("Cubby roof cushion",new Vector3(3.65f,1.108f,-2.43f),new Vector3(.66f,.016f,.57f),.007f,inset,false);
    }
    static void Post(Vector3 bottom,float top)
    {
        Cylinder("Oatmeal scratching post",new Vector3(bottom.x,(bottom.y+top)*.5f,bottom.z),.078f,top-bottom.y,rope,true);
        var points=new List<Vector3>();int turns=Mathf.CeilToInt((top-bottom.y)/.018f);int count=turns*20;
        for(int i=0;i<=count;i++){float f=i/(float)count,a=f*turns*Mathf.PI*2;points.Add(new Vector3(bottom.x+Mathf.Cos(a)*.079f,Mathf.Lerp(bottom.y+.006f,top-.006f,f),bottom.z+Mathf.Sin(a)*.079f));}
        Tube("Fine wound sisal",points,.0045f,rope,false,6);
        Cylinder("Vanilla post cuff",bottom+Vector3.up*.025f,.092f,.05f,cream,false);
        Cylinder("Vanilla post cap",new Vector3(bottom.x,top-.02f,bottom.z),.092f,.04f,cream,false);
    }
    static void Bed(string name,Vector3 p,float radius,bool back)
    {
        Cylinder(name,p-Vector3.up*.045f,radius,.09f,blush,true);
        var edge=new List<Vector3>();for(int i=0;i<=96;i++){float a=i*Mathf.PI*2/96;edge.Add(p+new Vector3(Mathf.Cos(a)*(radius-.018f),-.018f,Mathf.Sin(a)*(radius-.018f)));}
        Tube("Plump pink cushion edge",edge,.037f,inset,false);
        Cylinder("Vanilla sleeping pad",p+Vector3.up*.002f,radius-.066f,.008f,cream,false);
        if(back)
        {
            // Back faces the wall (+X). The room-facing side and shelf route stay open.
            // One closed upholstered wall, rounded across its thickness and height.
            // Unlike stacked tubes this meets the bed and has sealed, soft ends.
            var v=new List<Vector3>();var t=new List<int>();var rim=new List<Vector3>();
            const int segments=40,section=36;
            for(int i=0;i<=segments;i++)
            {
                float a=Mathf.Lerp(-76,76,i/(float)segments)*Mathf.Deg2Rad;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float top=.10f+.17f*Mathf.Pow(Mathf.Cos(a),2),bottom=-.025f;
                float halfHeight=(top-bottom)*.5f,round=.026f;
                var center=p+radial*(radius-.042f)+Vector3.up*((top+bottom)*.5f);
                for(int k=0;k<4;k++)for(int j=0;j<=8;j++)
                {
                    float b=(k*90+j*90f/8)*Mathf.Deg2Rad;
                    float x=(k==0||k==3?1:-1)*(.055f-round)+Mathf.Cos(b)*round;
                    float y=(k<2?1:-1)*(halfHeight-round)+Mathf.Sin(b)*round;
                    v.Add(center+radial*x+Vector3.up*y);
                }
                rim.Add(p+radial*(radius-.042f)+Vector3.up*(top+.003f));
                if(i>0)for(int j=0;j<section;j++)Quad(t,(i-1)*section+j,(i-1)*section+(j+1)%section,i*section+(j+1)%section,i*section+j);
            }
            foreach(int end in new[]{0,segments})
            {
                int center=v.Count;var midpoint=Vector3.zero;
                for(int j=0;j<section;j++)midpoint+=v[end*section+j];v.Add(midpoint/section);
                for(int j=0;j<section;j++)if(end==0)t.AddRange(new[]{center,end*section+(j+1)%section,end*section+j});else t.AddRange(new[]{center,end*section+j,end*section+(j+1)%section});
            }
            Mesh("Rounded padded lookout backrest",v,t,blush,true);
            Tube("Vanilla backrest seam",rim,.010f,cream,false);
            Sphere("Seam soft end",rim[0],Vector3.one*.020f,cream);
            Sphere("Seam soft end",rim[rim.Count-1],Vector3.one*.020f,cream);
        }
    }
    static void Cylinder(string name,Vector3 p,float radius,float height,Material mat,bool solid)
    {
        var v=new List<Vector3>();var normals=new List<Vector3>();var t=new List<int>();const int n=64;
        for(int j=0;j<2;j++)for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v.Add(p+dir*radius+Vector3.up*((j-.5f)*height));normals.Add(dir);}
        for(int i=0;i<n;i++)Quad(t,i,i+1,n+2+i,n+1+i,true);
        foreach(int side in new[]{-1,1}){int c=v.Count;v.Add(p+Vector3.up*(side*height*.5f));normals.Add(Vector3.up*side);for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;v.Add(p+new Vector3(Mathf.Cos(a)*radius,side*height*.5f,Mathf.Sin(a)*radius));normals.Add(Vector3.up*side);}for(int i=0;i<n;i++)if(side>0)t.AddRange(new[]{c,c+i+2,c+i+1});else t.AddRange(new[]{c,c+i+1,c+i+2});}
        Mesh(name,v,t,mat,solid,normals);
    }
    static void Sphere(string name,Vector3 p,Vector3 size,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=size;
        Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;g.AddComponent<SoftDecoration>();
    }
    static void RoundedRectangleTube(string name,Vector3 p,float halfX,float halfZ,float corner,float radius,Material mat)
    {
        var points=new List<Vector3>();for(int k=0;k<4;k++)for(int i=0;i<=16;i++){float a=(k*90+i*90f/16)*Mathf.Deg2Rad;float x=(k==0||k==3?1:-1)*(halfX-corner);float z=(k<2?1:-1)*(halfZ-corner);points.Add(p+new Vector3(x+corner*Mathf.Cos(a),0,z+corner*Mathf.Sin(a)));}points.Add(points[0]);Tube(name,points,radius,mat,false);
    }
    static void Tube(string name,List<Vector3> points,float radius,Material mat,bool solid,int sides=8)
    {
        var v=new List<Vector3>();var t=new List<int>();var normals=new List<Vector3>();
        for(int i=0;i<points.Count;i++)
        {
            var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;
            var u=Vector3.Cross(tangent,Vector3.up).normalized;if(u.sqrMagnitude<.01f)u=Vector3.right;var w=Vector3.Cross(tangent,u).normalized;
            for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;var normal=Mathf.Cos(a)*u+Mathf.Sin(a)*w;v.Add(points[i]+radius*normal);normals.Add(normal);}
            if(i>0)for(int j=0;j<sides;j++)Quad(t,(i-1)*sides+j,(i-1)*sides+(j+1)%sides,i*sides+(j+1)%sides,i*sides+j);
        }
        Mesh(name,v,t,mat,solid,normals);
    }
}
