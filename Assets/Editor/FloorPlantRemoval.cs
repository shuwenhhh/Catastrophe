using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FloorPlantRemoval
{
    const string Report="Validation/FloorPlantRemoval";

    [MenuItem("Catastrophe/Remove Floor Plant Only")]
    public static void Apply()
    {
        if(Application.isPlaying||SceneManager.GetActiveScene().path!="Assets/LivingRoom.unity")
            throw new Exception("Open LivingRoom in Edit Mode first");
        var floorPlant=GameObject.Find("Tripo Pothos Plant");
        var windowPlant=GameObject.Find("Window green plant");
        var shelfPlant=GameObject.Find("Shelf Plant 02");
        if(!floorPlant||!windowPlant||!shelfPlant)
            throw new Exception("Expected floor, window and shelf plants must be present before the targeted edit");
        var renderers=floorPlant.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new Exception("Floor plant has no visible mesh");
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        // This imported prefab's pivot is centered, so part of its mesh sits below floor zero.
        if(bounds.center.y<-.15f||bounds.center.y>.45f)
            throw new Exception("The selected plant is not on the floor: "+bounds);
        Directory.CreateDirectory(Report);
        if(!File.Exists(Report+"/Before.unity"))File.Copy("Assets/LivingRoom.unity",Report+"/Before.unity");
        Undo.DestroyObjectImmediate(floorPlant);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(Report+"/check.txt",
            "Removed the floor-level scene instance 'Tripo Pothos Plant'.\n"+
            "Preserved 'Window green plant' and 'Shelf Plant 02' and the source plant prefab.\n");
    }
}
