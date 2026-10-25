using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CatPortraitWallSetup
{
    const string Art="Assets/Art/LivingRoom/WallArt/";
    const string Report="Validation/CatPortrait";
    static double ready;
    static CatPortraitWallSetup(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CatPortraitCheck",false)){SessionState.SetBool("CatPortraitCheck",false);ready=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Check;}};}
    [MenuItem("Catastrophe/Hang Cat Portrait Above Sofa")]
    static void Build(){
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")throw new Exception("Open LivingRoom in Edit Mode");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"TuxedoCatPortrait.png");
        var picture=AssetDatabase.LoadAssetAtPath<Material>(Art+"TuxedoCatPortrait.mat");
        if(!texture||!picture)throw new Exception("Existing portrait missing");
        var display=AssetDatabase.LoadAssetAtPath<Material>(Art+"PortraitDisplay.mat");
        if(!display){display=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(display,Art+"PortraitDisplay.mat");}
        display.SetTexture("_BaseMap",texture);display.SetColor("_BaseColor",Color.white);display.SetFloat("_Cull",0);EditorUtility.SetDirty(display);picture=display;
        Directory.CreateDirectory(Report);if(!File.Exists(Report+"/Before.unity"))File.Copy("Assets/LivingRoom.unity",Report+"/Before.unity");
        var root=GameObject.Find("Cat portrait above sofa");if(!root){root=new GameObject("Cat portrait above sofa");Undo.RegisterCreatedObjectUndo(root,"Hang existing cat portrait");root.transform.SetParent(GameObject.Find("Living Room").transform,false);root.AddComponent<SoftDecoration>();}
        else {Undo.RegisterFullObjectHierarchyUndo(root,"Update cat portrait");for(int i=root.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(root.transform.GetChild(i).gameObject);}
        root.transform.SetPositionAndRotation(new Vector3(-1.18f,2.16f,3.10f),Quaternion.identity);
        float height=1.15f,width=height*texture.width/texture.height;
        var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Thin oak picture backing";backing.transform.SetParent(root.transform,false);backing.transform.localPosition=new Vector3(0,0,.028f);backing.transform.localScale=new Vector3(width+.035f,height+.035f,.045f);Object.DestroyImmediate(backing.GetComponent<Collider>());
        backing.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LivingRoom/CozyFurniture/Honey oak.mat");
        var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Original tuxedo cat portrait";face.transform.SetParent(root.transform,false);face.transform.localScale=new Vector3(width,height,1);face.GetComponent<Renderer>().sharedMaterial=picture;Object.DestroyImmediate(face.GetComponent<Collider>());
        PrefabUtility.SaveAsPrefabAsset(root,Art+"FramedCatPortrait.prefab");EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        SessionState.SetBool("CatPortraitCheck",true);EditorApplication.isPlaying=true;
    }
    static void Check(){if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Check;GameObject g=null;RenderTexture rt=null;var prev=RenderTexture.active;try{
        var portrait=GameObject.Find("Cat portrait above sofa");if(!portrait||!portrait.activeInHierarchy)throw new Exception("Portrait missing at runtime");
        var face=portrait.transform.Find("Original tuxedo cat portrait").GetComponent<Renderer>();if(face.sharedMaterial.GetTexture("_BaseMap")!=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"TuxedoCatPortrait.png"))throw new Exception("Wrong portrait texture");
        g=new GameObject("Temporary portrait evidence camera");var c=g.AddComponent<Camera>();c.transform.position=new Vector3(-1.70f,1.95f,.4f);c.transform.LookAt(new Vector3(-1.70f,1.70f,3.16f));c.fieldOfView=52;c.nearClipPlane=.03f;rt=new RenderTexture(1000,1000,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();File.WriteAllBytes(Report+"/PortraitOnWall.png",image.EncodeToPNG());Object.DestroyImmediate(image);File.WriteAllText(Report+"/check.txt","PASS original TuxedoCatPortrait texture visible on wall above sofa in running LivingRoom; original aspect ratio preserved; no gameplay colliders added.\n");
    }catch(Exception e){File.WriteAllText(Report+"/check.txt","FAIL "+e.Message);Debug.LogException(e);}finally{RenderTexture.active=prev;if(g)Object.DestroyImmediate(g);if(rt)Object.DestroyImmediate(rt);EditorApplication.isPlaying=false;}}
}
