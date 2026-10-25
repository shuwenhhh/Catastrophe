using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WindowNookSetup
{
    const string Folder="Assets/Art/LivingRoom/WindowNook";
    const string Reports="Validation/WindowNook";
    static Transform root;
    static int id,phase;
    static double ready;
    static Material cloth,wood,trim;
    static WindowNookSetup(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("WindowNookTest",false)){SessionState.SetBool("WindowNookTest",false);phase=0;ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=TestTick;}};}
    static Material Mat(string name,Color color,float smooth=.2f){var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;}
    [MenuItem("Catastrophe/Build Warm Window Nook")]
    public static void Build()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")throw new Exception("Open LivingRoom and stop Play first");
        Directory.CreateDirectory(Reports);Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        if(!File.Exists(Reports+"/LivingRoom_BeforeWindow.unity"))File.Copy("Assets/LivingRoom.unity",Reports+"/LivingRoom_BeforeWindow.unity");
        foreach(string name in new[]{"Tripo Curtain","Tripo Navy Sideboard"}){var g=GameObject.Find(name);if(g){Undo.RecordObject(g,"Preserve original window furniture");g.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(g);}}
        var old=GameObject.Find("Warm window nook");if(old)Undo.DestroyObjectImmediate(old);
        root=new GameObject("Warm window nook").transform;root.SetParent(GameObject.Find("Living Room").transform,false);root.position=Vector3.zero;root.rotation=Quaternion.identity;root.localScale=Vector3.one;id=0;
        cloth=Mat("Terracotta woven cloth",new Color(.58f,.17f,.105f),.08f);cloth.SetFloat("_Cull",0);
        trim=Mat("Curtain hem and ties",new Color(.43f,.105f,.066f),.12f);
        wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LivingRoom/Materials/Furniture_LightOak.mat");if(!wood)wood=Mat("Honey oak",new Color(.57f,.34f,.16f));
        var poleMat=Mat("Walnut rod",new Color(.29f,.14f,.065f));
        var rod=Primitive("Wooden curtain rod",PrimitiveType.Cylinder,new Vector3(1.15f,3.04f,2.92f),new Vector3(.055f,1.52f,.055f),poleMat);rod.transform.rotation=Quaternion.Euler(0,0,90);
        foreach(int side in new[]{-1,1}){
            Primitive("Rod finial",PrimitiveType.Sphere,new Vector3(1.15f+side*1.55f,3.04f,2.92f),Vector3.one*.105f,poleMat);
            Panel(side);
            for(int j=0;j<5;j++)Box("Fabric hanging tab",new Vector3(1.15f+side*1.03f+(j-2)*.16f,3.01f,2.89f),new Vector3(.07f,.14f,.055f),.025f,cloth,false);
        }
        // Separate top, shelf, rails and four legs leave a real under-table cavity.
        // Keep the front jump edge at z=2.345, but close the former fall-through
        // slot between the rear edge and the window wall (front face z=3.21).
        Box("Console oak top",new Vector3(1.12f,.72f,2.7725f),new Vector3(1.83f,.085f,.855f),.035f,wood);
        Box("Console lower shelf",new Vector3(1.12f,.19f,2.63f),new Vector3(1.66f,.045f,.45f),.016f,wood);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Console leg",new Vector3(1.12f+x*.79f,.35f,2.63f+z*.205f),new Vector3(.075f,.70f,.075f),.012f,wood);
        for(int z=-1;z<=1;z+=2)Box("Console apron",new Vector3(1.12f,.62f,2.63f+z*.21f),new Vector3(1.63f,.12f,.045f),.009f,wood);
        Prop("Tripo_PothosPlant","Window green plant",new Vector3(.90f,.7625f,2.66f),.47f,0);
        WindowPlantFinish.AddVase(root);
        Books(new Vector3(1.35f,.7625f,2.59f),4);
        Coffee(new Vector3(1.80f,.7625f,2.59f));
        Books(new Vector3(.90f,.214f,2.63f),3);
        var inside=Anchor("Curtain hide anchor",new Vector3(2.36f,.228f,2.97f),90);
        var outside=Anchor("Curtain exit anchor",new Vector3(2.50f,.228f,2.05f),180);
        var spot=GameObject.Find("Hide Spot - Behind Curtain");if(!spot)spot=new GameObject("Hide Spot - Behind Curtain");
        spot.transform.SetPositionAndRotation(new Vector3(2.48f,.4f,2.38f),Quaternion.identity);spot.transform.localScale=Vector3.one;
        var trigger=spot.GetComponent<BoxCollider>();if(!trigger)trigger=spot.AddComponent<BoxCollider>();trigger.center=Vector3.zero;trigger.size=new Vector3(.58f,.8f,.90f);trigger.isTrigger=true;
        var hide=spot.GetComponent<HideSpot>();if(!hide)hide=spot.AddComponent<HideSpot>();hide.Configure("窗帘后面");hide.SetAnchors(inside,outside);EditorUtility.SetDirty(hide);
        foreach(var r in spot.GetComponents<Renderer>())r.enabled=false;
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"/WarmWindowNook.prefab");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorApplication.delayCall+=Capture;Debug.Log("WINDOW_NOOK_BUILT");
    }
    [MenuItem("Catastrophe/Clear Sofa To Window Table Landing")]
    static void ClearSofaLanding()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")throw new Exception("Open LivingRoom and stop Play first");
        var sceneRoot=GameObject.Find("Warm window nook");
        if(!sceneRoot)throw new Exception("Warm window nook missing");
        MoveTableProps(sceneRoot.transform);
        var prefabPath=Folder+"/WarmWindowNook.prefab";
        var prefabRoot=PrefabUtility.LoadPrefabContents(prefabPath);
        try{MoveTableProps(prefabRoot.transform);PrefabUtility.SaveAsPrefabAsset(prefabRoot,prefabPath);}
        finally{PrefabUtility.UnloadPrefabContents(prefabRoot);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("SOFA_WINDOW_LANDING_CLEARED");
    }
    static void MoveTableProps(Transform nook)
    {
        foreach(var target in new[]{("Window green plant",.90f),("Window book stack - top",1.35f),("Window coffee cup",1.80f)})
        {
            var child=nook.Find(target.Item1);
            if(!child)throw new Exception("Window tabletop prop missing: "+target.Item1);
            Undo.RecordObject(child,"Clear sofa landing on window table");
            var p=child.position;p.x=target.Item2;child.position=p;
            EditorUtility.SetDirty(child);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
    }
    [MenuItem("Catastrophe/Window Nook/Close Table-Window Gap")]
    static void CloseTableWindowGap()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")
            throw new Exception("Open LivingRoom and stop Play first");
        var sceneNook=GameObject.Find("Warm window nook");
        if(!sceneNook)throw new Exception("Warm window nook missing");
        FitTableTop(sceneNook.transform,true);
        var prefabPath=Folder+"/WarmWindowNook.prefab";
        var prefabRoot=PrefabUtility.LoadPrefabContents(prefabPath);
        try{FitTableTop(prefabRoot.transform,false);PrefabUtility.SaveAsPrefabAsset(prefabRoot,prefabPath);}
        finally{PrefabUtility.UnloadPrefabContents(prefabRoot);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("WINDOW_TABLE_GAP_CLOSED: front jump edge unchanged; solid oak top reaches within 1 cm of the window wall.");
    }
    static void FitTableTop(Transform nook,bool undo)
    {
        var top=nook.Find("Console oak top");
        var mesh=top?top.GetComponent<MeshFilter>()?.sharedMesh:null;
        if(!top||!top.GetComponent<MeshCollider>()||!mesh)throw new Exception("Window tabletop or solid collider missing");
        if(undo)Undo.RecordObject(top,"Close window table fall-through gap");
        top.localPosition=new Vector3(1.12f,.72f,2.7725f);
        top.localScale=new Vector3(1f,1f,.855f/mesh.bounds.size.z);
        EditorUtility.SetDirty(top);
    }
    static Transform Anchor(string name,Vector3 p,float yaw){var t=new GameObject(name).transform;t.SetParent(root,false);t.position=p;t.rotation=Quaternion.Euler(0,yaw,0);return t;}
    static void Panel(int side)
    {
        var v=new List<Vector3>();var tris=new List<int>();const int nx=64,ny=96;
        for(int j=0;j<=ny;j++)for(int i=0;i<=nx;i++){
            float t=(float)j/ny,u=(float)i/nx,tie=Mathf.Exp(-Mathf.Pow((t-.39f)/.115f,2));
            float width=Mathf.Lerp(.58f,.84f,t)-.40f*tie;
            float x=1.15f+side*(1.10f+.12f*tie)+(u-.5f)*width;
            float y=.10f+t*2.88f+.025f*Mathf.Sin(u*Mathf.PI*8)*(1-t)*(1-t);
            float z=2.72f+.065f*Mathf.Cos(u*Mathf.PI*10)*(1-.65f*tie)+.06f*tie+.04f*Mathf.Sin(t*Mathf.PI);
            v.Add(new Vector3(x,y,z));
        }
        for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){int a=j*(nx+1)+i;Quad(tris,a,a+nx+1,a+nx+2,a+1);}
        var panel=MeshPart(side<0?"Left pleated curtain":"Right pleated curtain",v,tris,cloth,false);
        // Keep the sofa-to-console landing lane clear while the drape and its
        // collision bands remain aligned against the window behind the table.
        if(side<0) panel.transform.localPosition=new Vector3(0f,0f,.22f);
        AddCurtainCollisionBands(panel.transform);
        // Small wrap follows the gathered waist; the fabric is never a giant solid box.
        Box("Gathered cloth tie",new Vector3(1.15f+side*1.22f,1.23f,2.73f+(side < 0 ? .22f : 0f)),new Vector3(.33f,.115f,.13f),.046f,trim,false);
    }
    internal static int AddCurtainCollisionBands(Transform panel)
    {
        if(panel.Find("Curtain solid bands"))return panel.Find("Curtain solid bands").childCount;
        var mesh=panel.GetComponent<MeshFilter>()?.sharedMesh;
        if(!mesh)throw new Exception("Curtain mesh missing: "+panel.name);
        var group=new GameObject("Curtain solid bands").transform;
        group.SetParent(panel,false);
        var vertices=mesh.vertices;
        int count=0;
        for(float minY=.65f;minY<3.0f;minY+=.28f)
        {
            float maxY=Mathf.Min(3.0f,minY+.28f);
            bool found=false;Bounds bounds=default;
            foreach(var vertex in vertices)
            {
                if(vertex.y<minY-.025f||vertex.y>maxY+.025f)continue;
                if(!found){bounds=new Bounds(vertex,Vector3.zero);found=true;}
                else bounds.Encapsulate(vertex);
            }
            if(!found)continue;
            var piece=new GameObject("Curtain collision band "+(++count).ToString("00"));
            piece.transform.SetParent(group,false);
            var collider=piece.AddComponent<BoxCollider>();
            collider.center=new Vector3(bounds.center.x,(minY+maxY)*.5f,bounds.center.z);
            collider.size=new Vector3(bounds.size.x+.025f,maxY-minY+.018f,bounds.size.z+.07f);
        }
        return count;
    }
    static void Prop(string prefab,string name,Vector3 bottom,float height,float yaw)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tripo/Prefabs/"+prefab+".prefab");if(!asset)throw new Exception("Missing "+prefab);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.name=name;go.transform.SetParent(root,false);go.transform.rotation=Quaternion.Euler(0,yaw,0);
        Bounds b=BoundsOf(go);go.transform.localScale*=height/b.size.y;b=BoundsOf(go);go.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }
    static void Books(Vector3 bottom,int count)
    {
        var stack=new GameObject(count==4?"Window book stack - top":"Window book stack - lower").transform;
        stack.SetParent(root,false);stack.position=bottom;
        var paper=Mat("Ivory book pages",new Color(.88f,.82f,.67f),.05f);
        var colors=new[]{new Color(.20f,.37f,.32f),new Color(.58f,.22f,.15f),new Color(.20f,.32f,.46f),new Color(.73f,.53f,.24f)};
        for(int i=0;i<count;i++){
            float y=i*.048f;var cover=Mat("Book cover "+i,colors[i]);var center=bottom+new Vector3((i%2)*.015f,y,0);
            float width=.35f-i*.012f,depth=.26f-i*.008f;
            Box("Book pages",center+Vector3.up*.023f,new Vector3(width-.014f,.031f,depth-.008f),.003f,paper,false).transform.SetParent(stack,true);
            foreach(float dy in new[]{.003f,.043f})Box("Book cover",center+Vector3.up*dy,new Vector3(width,.006f,depth),.002f,cover,false).transform.SetParent(stack,true);
            Box("Bound spine",center+new Vector3(-width*.5f+.004f,.023f,0),new Vector3(.009f,.04f,depth),.003f,cover,false).transform.SetParent(stack,true);
        }
        FinalizePushable(stack,count==4?35:25,false);
    }
    static void Coffee(Vector3 p)
    {
        var cup=new GameObject("Window coffee cup").transform;cup.SetParent(root,false);cup.position=p;
        var ceramic=Mat("Cream glazed ceramic",new Color(.90f,.80f,.61f),.42f);
        var coffee=Mat("Coffee surface",new Color(.07f,.027f,.012f),.32f);
        // Revolved closed wall with a real open rim and recessed coffee surface.
        var profile=new[]{new Vector2(0,0),new Vector2(.060f,0),new Vector2(.070f,.012f),new Vector2(.081f,.185f),new Vector2(.080f,.197f),new Vector2(.070f,.197f),new Vector2(.069f,.18f),new Vector2(.060f,.025f),new Vector2(0,.025f)};
        var v=new List<Vector3>();var t=new List<int>();int n=64;
        for(int j=0;j<profile.Length;j++)for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;v.Add(p+new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x));}
        for(int j=0;j<profile.Length-1;j++)for(int i=0;i<n;i++){int a=j*n+i,b=j*n+(i+1)%n;Quad(t,a,a+n,b+n,b);}
        MeshPart("Open cream coffee cup",v,t,ceramic,false).transform.SetParent(cup,true);
        v.Clear();t.Clear();for(int i=0;i<64;i++){float a=i*2*Mathf.PI/64;for(int j=0;j<12;j++){float b=j*2*Mathf.PI/12;v.Add(p+new Vector3(.095f+(.043f+.012f*Mathf.Cos(b))*Mathf.Cos(a),.112f+(.058f+.012f*Mathf.Cos(b))*Mathf.Sin(a),.012f*Mathf.Sin(b)));}}
        for(int i=0;i<64;i++)for(int j=0;j<12;j++)Quad(t,i*12+j,((i+1)%64)*12+j,((i+1)%64)*12+(j+1)%12,i*12+(j+1)%12);
        MeshPart("Rounded cup handle",v,t,ceramic,false).transform.SetParent(cup,true);
        var surface=Primitive("Dark coffee",PrimitiveType.Cylinder,p+Vector3.up*.17f,new Vector3(.136f,.002f,.136f),coffee);
        Object.DestroyImmediate(surface.GetComponent<Collider>());surface.AddComponent<SoftDecoration>();surface.transform.SetParent(cup,true);
        FinalizePushable(cup,30,true);
    }
    internal static void FinalizePushable(Transform group,int score,bool fragile)
    {
        var renderers=group.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new Exception("Empty pushable prop: "+group.name);
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        var collider=group.gameObject.AddComponent<BoxCollider>();
        collider.center=group.InverseTransformPoint(bounds.center);
        collider.size=bounds.size;
        var body=group.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;
        var item=group.gameObject.AddComponent<Valuable>();item.itemName=group.name;
        item.chaosValue=score;item.weight=1f;item.noise=fragile?22f:14f;item.fragile=fragile;
    }
    static Bounds BoundsOf(GameObject go){var rr=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
    static GameObject Primitive(string name,PrimitiveType type,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(root,false);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    static void Quad(List<int> t,int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d});}
    static GameObject MeshPart(string name,List<Vector3> v,List<int> t,Material m,bool collision){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Mesh_"+(id++)+".asset"));var g=new GameObject(name);g.transform.SetParent(root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;else g.AddComponent<SoftDecoration>();return g;}
    static GameObject Box(string name,Vector3 p,Vector3 size,float r,Material m,bool collision=true){var v=new List<Vector3>();var tri=new List<int>();var h=size*.5f;r=Mathf.Min(r,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.98f);var inner=h-Vector3.one*r;const int n=10;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;var w=Vector3.Cross(normal,u);int start=v.Count;for(int y=0;y<=n;y++)for(int x=0;x<=n;x++){var a=Vector3.Scale(normal+u*(2f*x/n-1)+w*(2f*y/n-1),h);var b=new Vector3(Mathf.Clamp(a.x,-inner.x,inner.x),Mathf.Clamp(a.y,-inner.y,inner.y),Mathf.Clamp(a.z,-inner.z,inner.z));v.Add(b+(a-b).normalized*r);}for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=start+y*(n+1)+x;Quad(tri,a,a+1,a+n+2,a+n+1);}}
        var g=MeshPart(name,v,tri,m,collision);g.transform.position=p;return g;
    }
    [MenuItem("Catastrophe/Capture Window Nook")]
    public static void Capture(){Render("WindowNook",new Vector3(1.15f,1.65f,1.8f),new Vector3(1.15f,1.65f,2.7f),3.45f);}
    static void Render(string name,Vector3 p,Vector3 target,float size){Directory.CreateDirectory(Reports);var go=new GameObject("Window preview camera");var c=go.AddComponent<Camera>();c.transform.position=p;c.transform.LookAt(target);c.orthographic=true;c.orthographicSize=size*.5f;c.nearClipPlane=.02f;var rt=new RenderTexture(1200,1100,24);var prev=RenderTexture.active;c.targetTexture=rt;try{c.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1100),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}finally{c.targetTexture=null;RenderTexture.active=prev;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}}
    [MenuItem("Catastrophe/Verify Window Curtain Hide")]
    public static void Test(){if(Application.isPlaying)throw new Exception("Stop Play first");SessionState.SetBool("WindowNookTest",true);EditorApplication.isPlaying=true;}
    static void TestTick(){if(EditorApplication.timeSinceStartup<ready)return;try{
        var cat=Object.FindFirstObjectByType<CatController>();var spot=GameObject.Find("Hide Spot - Behind Curtain");if(!cat||!spot)throw new Exception("Missing live cat or curtain hide spot");var hide=spot.GetComponent<HideSpot>();var solid=cat.GetComponent<CatSolidCollision>();
        if(phase==0){Object.FindFirstObjectByType<GameFlowUI>().BeginGame();Physics.SyncTransforms();if(!solid.CanOccupy(hide.HideAnchor.position,hide.HideAnchor.rotation))throw new Exception("Hide anchor intersects solid scenery");if(!solid.CanOccupy(hide.ExitAnchor.position,hide.ExitAnchor.rotation))throw new Exception("Exit blocked");var rb=cat.GetComponent<Rigidbody>();rb.position=hide.ExitAnchor.position;rb.rotation=hide.ExitAnchor.rotation;cat.transform.SetPositionAndRotation(rb.position,rb.rotation);phase++;ready=EditorApplication.timeSinceStartup+1;return;}
        if(phase==1){var nearby=typeof(CatController).GetField("nearbyHideSpot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(cat);if(nearby!=hide)throw new Exception("Approaching curtain did not activate E interaction");for(int i=0;i<=20;i++){var p=Vector3.Lerp(hide.ExitAnchor.position,hide.HideAnchor.position,i/20f);if(!solid.CanOccupy(p,hide.HideAnchor.rotation))throw new Exception("Curtain access passage blocked at "+p);}cat.EnterHide(spot);phase++;ready=EditorApplication.timeSinceStartup+1;return;}
        if(phase==2){if(!cat.IsHidden||!cat.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled))throw new Exception("Cat not physically visible inside hide spot");if(Vector3.Distance(cat.transform.position,hide.HideAnchor.position)>.03f)throw new Exception("Cat not at anchor");Capture();Render("CatBehindCurtain",new Vector3(3.15f,.65f,.65f),new Vector3(2.34f,.4f,2.93f),1.5f);cat.ExitHide();phase++;ready=EditorApplication.timeSinceStartup+1;return;}
        if(cat.IsHidden||cat.GetComponent<Rigidbody>().constraints==RigidbodyConstraints.FreezeAll)throw new Exception("Exit failed");
        if(Object.FindObjectsByType<SoftDecoration>(FindObjectsSortMode.None).Any(d=>d.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger)))throw new Exception("Cloth got rigid collider");
        File.WriteAllText(Reports+"/check.txt","PASS runtime hide/exit anchors and 21 approach passage samples clear of body/head/tail collisions.\nPASS entering trigger activates the live cat's E hide target.\nPASS cat remains enabled and physically positioned behind right curtain; exit restores movement.\nPASS cloth excluded from automatic rigid furniture collision.\nSeparate console top, shelf and legs. Original curtain/cabinet preserved inactive; scene backup retained.\n");Finish();
    }catch(Exception e){Debug.LogException(e);File.WriteAllText(Reports+"/check.txt","FAIL "+e);Finish();}}
    static void Finish(){EditorApplication.update-=TestTick;EditorApplication.isPlaying=false;}
}
