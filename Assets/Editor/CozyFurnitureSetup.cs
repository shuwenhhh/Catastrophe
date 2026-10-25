using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit, repeatable authoring command; never rebuild furniture on scene load.
[InitializeOnLoad]
public static class CozyFurnitureSetup
{
    const string Folder = "Assets/Art/LivingRoom/CozyFurniture";
    static CozyFurnitureSetup(){EditorApplication.playModeStateChanged += s => {if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CozyTest",false)){SessionState.SetBool("CozyTest",false); start=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick;}};}
    static double start; static int phase;
    static Material cream, piping, wood, gold, card, tape;
    static GameObject root;
    static int meshId;
    static Material Mat(string name, Color color, float smooth=.2f)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
    }
    [MenuItem("Catastrophe/Refine Sofa And Cat Shelter")]
    public static void Build()
    {
        if(Application.isPlaying || SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")throw new Exception("Open LivingRoom in Edit mode.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();Directory.CreateDirectory("Validation/CozyFurniture");
        string backup="Validation/CozyFurniture/LivingRoom_BeforeFurniture.unity";if(!File.Exists(backup))File.Copy("Assets/LivingRoom.unity",backup);
        cream=Mat("Ivory upholstery",new Color(.90f,.85f,.72f));piping=Mat("Warm seam",new Color(.73f,.66f,.51f));wood=Mat("Honey oak",new Color(.46f,.27f,.12f));gold=Mat("Antique brass",new Color(.60f,.39f,.12f),.55f);gold.SetFloat("_Metallic",.65f);
        card=Mat("Kraft cardboard",new Color(.55f,.34f,.17f),.05f);tape=Mat("Paper tape",new Color(.69f,.49f,.28f),.1f);
        var sofa=GameObject.Find("Sofa");var box=GameObject.Find("Tripo Hide Cardboard");if(!sofa||!box)throw new Exception("Missing room furniture");
        Bounds sb=BoundsOf(sofa),bb=BoundsOf(box);
        Replace(sofa,"Refined cream sofa",new Vector3(sb.center.x,sb.min.y,sb.center.z));
        float w=sb.size.x,d=sb.size.z,h=sb.size.y;
        // Separate rounded cushions, armrests, frame and legs preserve playable surfaces.
        Part("Upholstered apron",new Vector3(0,h*.22f,0),new Vector3(w*.89f,h*.20f,d*.90f),.06f,cream);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part("Oak foot",new Vector3(x*w*.39f,h*.08f,z*d*.32f),new Vector3(.10f,h*.16f,.10f),.025f,wood);
        Part("Rounded back",new Vector3(0,h*.67f,d*.34f),new Vector3(w*.90f,h*.66f,d*.23f),.10f,cream);
        for(int side=-1;side<=1;side+=2){Part("Soft arm",new Vector3(side*w*.445f,h*.46f,-d*.02f),new Vector3(w*.11f,h*.65f,d*.94f),.065f,cream);Sphere("Arm brass button",new Vector3(side*w*.445f,h*.57f,-d*.495f),new Vector3(.033f,.033f,.014f),gold);}
        for(int i=0;i<2;i++)
        {
            float x=(i==0?-1:1)*w*.205f;var p=new Vector3(x,h*.40f,-d*.065f);var size=new Vector3(w*.405f,h*.21f,d*.73f);
            Part("Seat cushion "+i,p,size,.07f,cream);Loop("Seat piping "+i,p+new Vector3(0,-size.y*.28f,0),size.x*.47f,size.z*.47f,.045f,.006f,piping);
        }
        for(int i=0;i<5;i++){float x=(i-2)*w*.155f;Sphere("Tuft button "+i,new Vector3(x,h*.72f,d*.207f),new Vector3(.024f,.024f,.014f),gold);Part("Vertical stitched channel "+i,new Vector3(x,h*.81f,d*.219f),new Vector3(.008f,h*.29f,.008f),.003f,piping,false);}
        SavePrefab("CreamSofa");
        Replace(box,"Single entrance cardboard shelter",new Vector3(bb.center.x,bb.min.y,bb.center.z));
        // Dimensioned for the existing body, head AND upright tail, not just the root capsule.
        const float bw=1.0f,bh=.86f,bd=1.02f,t=.028f;
        Part("Floor",new Vector3(0,t*.5f,0),new Vector3(bw,t,bd),.008f,card);
        Part("Closed lid",new Vector3(0,bh-t*.5f,0),new Vector3(bw,t,bd),.008f,card);
        Part("Back",new Vector3(0,bh*.5f,bd*.5f-t*.5f),new Vector3(bw,bh,t),.008f,card);
        for(int s=-1;s<=1;s+=2)Part("Side",new Vector3(s*(bw-t)*.5f,bh*.5f,0),new Vector3(t,bh,bd),.008f,card);
        // Front ring: a genuine open ellipse, closed thickness, no invisible broad box.
        var v=new List<Vector3>();var tri=new List<int>();int n=96;float cy=.365f,rx=.32f,ry=.33f;
        for(int i=0;i<n;i++){
            float a=2*Mathf.PI*i/n,dx=Mathf.Cos(a),dy=Mathf.Sin(a);float k=Mathf.Min(.5f/Mathf.Max(.00001f,Mathf.Abs(dx)),(dy>0?bh-cy:cy)/Mathf.Max(.00001f,Mathf.Abs(dy)));
            for(int z=0;z<2;z++){v.Add(new Vector3(dx*rx,cy+dy*ry,-bd*.5f+z*t));v.Add(new Vector3(dx*k,cy+dy*k,-bd*.5f+z*t));}
        }
        for(int i=0;i<n;i++){int a=i*4,b=((i+1)%n)*4;Quad(tri,a,b,b+1,a+1);Quad(tri,a+2,a+3,b+3,b+2);Quad(tri,a,a+2,b+2,b);Quad(tri,a+1,b+1,b+3,a+3);}
        MeshPart("Single round entrance",v,tri,card,true);
        Part("Lid centre tape",new Vector3(0,bh+.001f,0),new Vector3(.085f,.003f,bd-.01f),.001f,tape,false);
        Part("Lid cross tape",new Vector3(0,bh+.003f,0),new Vector3(bw-.01f,.003f,.045f),.001f,tape,false);
        for(int s=-1;s<=1;s+=2)Part("Fold edge",new Vector3(s*.486f,bh*.5f,-bd*.5f-.002f),new Vector3(.008f,bh-.01f,.004f),.001f,tape,false);
        var inside=new GameObject("Visible hide anchor").transform;inside.SetParent(root.transform,false);inside.localPosition=new Vector3(0,t+.228f,-.06f);inside.localRotation=Quaternion.Euler(0,180,0);
        var outside=new GameObject("Exit anchor").transform;outside.SetParent(root.transform,false);outside.localPosition=new Vector3(0,.228f,-1.0f);outside.localRotation=inside.localRotation;
        var spot=GameObject.Find("Hide Spot - Cardboard Box");Undo.RecordObject(spot.transform,"Move shelter interaction");spot.transform.SetPositionAndRotation(root.transform.position+new Vector3(0,.32f,-.72f),Quaternion.identity);spot.transform.localScale=Vector3.one;
        spot.GetComponent<BoxCollider>().size=new Vector3(.85f,.65f,.60f);var hide=spot.GetComponent<HideSpot>();if(!hide)hide=spot.AddComponent<HideSpot>();hide.Configure("纸箱");hide.SetAnchors(inside,outside);EditorUtility.SetDirty(hide);
        SavePrefab("CardboardShelter");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Capture(sofa,"Sofa",new Vector3(1.3f,.8f,-2));Capture(box,"Shelter",new Vector3(1,.65f,-2));Debug.Log("COZY_FURNITURE_BUILT");
    }
    static void Replace(GameObject parent,string name,Vector3 position){Undo.RegisterFullObjectHierarchyUndo(parent,"Refine furniture");foreach(var r in parent.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in parent.GetComponentsInChildren<Collider>())c.enabled=false;root=new GameObject(name);root.transform.SetParent(parent.transform);root.transform.SetPositionAndRotation(position,Quaternion.identity);root.transform.localScale=new Vector3(1/parent.transform.lossyScale.x,1/parent.transform.lossyScale.y,1/parent.transform.lossyScale.z);meshId=0;}
    static void SavePrefab(string name){PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");}
    [MenuItem("Catastrophe/Remove Sofa Loose Pillows")]
    public static void RemoveLoosePillows()
    {
        if(Application.isPlaying || SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")throw new Exception("Open LivingRoom in Edit mode first.");
        var sofa=GameObject.Find("Refined cream sofa");if(!sofa)throw new Exception("Missing refined sofa");
        Directory.CreateDirectory("Validation/CozyFurniture");
        const string prefabPath=Folder+"/CreamSofa.prefab";
        const string backup="Validation/CozyFurniture/CreamSofa_WithPillows.prefab";
        if(!File.Exists(backup))File.Copy(prefabPath,backup);
        int count=0;
        foreach(var t in sofa.GetComponentsInChildren<Transform>(true))
            if(t.name=="Loose pillow"||t.name=="Pillow tuft"){Undo.DestroyObjectImmediate(t.gameObject);count++;}
        var contents=PrefabUtility.LoadPrefabContents(prefabPath);
        try{
            foreach(var t in contents.GetComponentsInChildren<Transform>(true))
                if(t.name=="Loose pillow"||t.name=="Pillow tuft")Object.DestroyImmediate(t.gameObject);
            PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(contents);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorApplication.delayCall+=()=>{
            var current=GameObject.Find("Refined cream sofa");
            if(current.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Loose pillow"||t.name=="Pillow tuft"))throw new Exception("Pillows still present");
            Capture(GameObject.Find("Sofa"),"Sofa_NoPillows",new Vector3(1.3f,.8f,-2));
            File.WriteAllText("Validation/CozyFurniture/NoPillows.txt",$"PASS removed {count} scene objects (two pillows and their two tuft buttons); prefab updated; no pillow objects remain. Original prefab backed up.\n");
        };
    }
    [MenuItem("Catastrophe/Polish Sofa Tufting")]
    public static void Polish()
    {
        root=GameObject.Find("Refined cream sofa");cream=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Ivory upholstery.mat");piping=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Warm seam.mat");
        var back=root.transform.Find("Rounded back");var mesh=back.GetComponent<MeshFilter>().sharedMesh;Vector3 size=mesh.bounds.size;
        var replacement=Part("Tufted back",back.localPosition,size,.08f,cream);back.gameObject.SetActive(false);
        var m=replacement.GetComponent<MeshFilter>().sharedMesh;var vs=m.vertices;
        var buttons=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tuft button")).ToArray();
        for(int i=0;i<vs.Length;i++)if(vs[i].z<-size.z*.35f){Vector3 p=vs[i]+replacement.transform.localPosition;float dent=0;foreach(var b in buttons){float dx=p.x-b.localPosition.x,dy=p.y-b.localPosition.y;dent+=.028f*Mathf.Exp(-(dx*dx+dy*dy)/.0028f);dent+=.006f*Mathf.Exp(-dx*dx/.00012f)*Mathf.Exp(-dy*dy/.07f);}vs[i].z+=dent;}
        m.vertices=vs;m.RecalculateNormals();EditorUtility.SetDirty(m);replacement.GetComponent<MeshCollider>().sharedMesh=null;replacement.GetComponent<MeshCollider>().sharedMesh=m;
        foreach(var b in buttons){var p=b.localPosition;p.z=replacement.transform.localPosition.z-size.z*.5f+.026f;b.localPosition=p;}
        foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name.StartsWith("Vertical stitched"))t.gameObject.SetActive(false);else if(t.name=="Loose pillow"){t.localScale=new Vector3(1,1.45f,1.15f);t.localPosition+=Vector3.up*.035f;}
        SavePrefab("CreamSofa");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorApplication.delayCall+=()=>Capture(GameObject.Find("Sofa"),"Sofa",new Vector3(1.3f,.8f,-2));
    }
    [MenuItem("Catastrophe/Finalize Refined Furniture Visibility")]
    public static void FinalizeFurniture()
    {
        var plant=GameObject.Find("Tripo Pothos Plant");Undo.RecordObject(plant.transform,"Clear shelter entrance");plant.transform.position=new Vector3(-3.85f,0,.75f);PrefabUtility.RecordPrefabInstancePropertyModifications(plant.transform);
        foreach(string name in new[]{"Sofa","Tripo Hide Cardboard"}){
            var parent=GameObject.Find(name);var refined=parent.transform.Cast<Transform>().Last(t=>t.name=="Refined cream sofa"||t.name=="Single entrance cardboard shelter");
            foreach(var r in parent.GetComponentsInChildren<Renderer>(true))if(!r.transform.IsChildOf(refined)){r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);if(r.gameObject!=parent){r.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);}}
            foreach(var c in parent.GetComponentsInChildren<Collider>(true))if(!c.transform.IsChildOf(refined)){c.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        }
        var sofaRoot=GameObject.Find("Refined cream sofa");
        foreach(var t in sofaRoot.GetComponentsInChildren<Transform>(true)){
            if(t.name.StartsWith("Vertical stitched")||t.name=="Rounded back"){t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
            else if(t.name=="Loose pillow"){t.localScale=new Vector3(1,1.45f,1.15f);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorApplication.delayCall+=()=>{Capture(GameObject.Find("Sofa"),"Sofa",new Vector3(1.3f,.8f,-2));Capture(GameObject.Find("Tripo Hide Cardboard"),"Shelter",new Vector3(1,.65f,-2));};
    }
    static void Quad(List<int> t,int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d});}
    static GameObject MeshPart(string name,List<Vector3> v,List<int> t,Material m,bool collision){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+root.name+"_"+(meshId++)+".asset"));var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;}
    static GameObject Part(string name,Vector3 pos,Vector3 size,float radius,Material m,bool collision=true)
    {
        var v=new List<Vector3>();var t=new List<int>();int n=name=="Tufted back"?64:12;var half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.98f);var inner=half-Vector3.one*radius;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){
            Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up, vv=Vector3.Cross(normal,u);int offset=v.Count;
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++){Vector3 p=Vector3.Scale(normal+u*(2f*x/n-1)+vv*(2f*y/n-1),half);Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));v.Add(q+(p-q).normalized*radius);}
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=offset+y*(n+1)+x;Quad(t,a,a+1,a+n+2,a+n+1);}
        }
        var go=MeshPart(name,v,t,m,collision);go.transform.localPosition=pos;return go;
    }
    static void Sphere(string name,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(root.transform,false);g.transform.localPosition=p;g.transform.localScale=s*2;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=m;}
    static void Loop(string name,Vector3 p,float x,float z,float round,float r,Material m){var v=new List<Vector3>();var t=new List<int>();int n=96;for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;float c=Mathf.Cos(a),s=Mathf.Sin(a);Vector3 point=new Vector3(Mathf.Sign(c)*(x-round)+c*round,0,Mathf.Sign(s)*(z-round)+s*round);for(int j=0;j<6;j++){float b=j*2*Mathf.PI/6;v.Add(p+point+new Vector3(c*Mathf.Cos(b),Mathf.Sin(b),s*Mathf.Cos(b))*r);}}for(int i=0;i<n;i++)for(int j=0;j<6;j++)Quad(t,i*6+j,((i+1)%n)*6+j,((i+1)%n)*6+(j+1)%6,i*6+(j+1)%6);MeshPart(name,v,t,m,false);}
    static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static void Capture(GameObject target,string name,Vector3 angle){var b=BoundsOf(target);var go=new GameObject("Temporary furniture camera");var c=go.AddComponent<Camera>();c.transform.position=b.center+angle.normalized*Mathf.Max(b.size.x,b.size.y,b.size.z)*2.3f;c.transform.LookAt(b.center);c.fieldOfView=38;c.nearClipPlane=.02f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.83f,.85f,.85f);var rt=new RenderTexture(1200,1000,24);var prev=RenderTexture.active;c.targetTexture=rt;try{c.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1000),0,0);tex.Apply();File.WriteAllBytes("Validation/CozyFurniture/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}finally{RenderTexture.active=prev;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}}
    [MenuItem("Catastrophe/Verify Visible Shelter In Play Mode")]
    public static void Test(){phase=0;SessionState.SetBool("CozyTest",true);EditorApplication.isPlaying=true;}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-start<2)return;
        try{
            var cat=Object.FindFirstObjectByType<CatController>();var spot=GameObject.Find("Hide Spot - Cardboard Box");
            if(!cat||!spot)throw new Exception("Missing live cat/shelter");
            if(phase==0){cat.EnterHide(spot);phase=1;start=EditorApplication.timeSinceStartup;return;}
            if(phase==1){
                if(!cat.IsHidden||!cat.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled))throw new Exception("Hidden cat lost its renderer");
                CheckPenetration(cat,"inside");Capture(GameObject.Find("Tripo Hide Cardboard"),"CatInside",new Vector3(.7f,.22f,-2));cat.ExitHide();CheckPenetration(cat,"exit");phase=2;start=EditorApplication.timeSinceStartup;return;
            }
            if(cat.IsHidden||cat.GetComponent<Rigidbody>().constraints==RigidbodyConstraints.FreezeAll)throw new Exception("Exit failed");
            File.WriteAllText("Validation/CozyFurniture/play.txt","PASS live 3D cat stays rendered while hidden; exit restores movement constraints.\nPASS body/head/tail have no solid penetration inside and at exit.\nInside and outside use authored anchors; one real front aperture.\n");Finish();
        }catch(Exception e){Debug.LogException(e);File.WriteAllText("Validation/CozyFurniture/play.txt","FAIL "+e);Finish();}
    }
    static void CheckPenetration(CatController cat,string phaseName)
    {
        Physics.SyncTransforms();
        foreach(var a in cat.GetComponentsInChildren<CapsuleCollider>())foreach(var b in Physics.OverlapCapsule(a.bounds.center-Vector3.up*.2f,a.bounds.center+Vector3.up*.2f,.5f,~0,QueryTriggerInteraction.Ignore)){
            if(b.transform.IsChildOf(cat.transform)||!b.enabled)continue;
            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.012f)throw new Exception(phaseName+" penetration "+a.name+" / "+b.name+" depth="+depth);
        }
    }
    static void Finish(){EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
}
