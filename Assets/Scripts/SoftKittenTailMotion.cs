using UnityEngine;
using System.Linq;
public sealed class SoftKittenTailMotion:MonoBehaviour
{
    Transform[] bones;Quaternion[] rest;
    void Awake(){bones=GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tail_")).OrderBy(t=>t.name).ToArray();rest=bones.Select(t=>t.localRotation).ToArray();}
    void LateUpdate(){for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i]*Quaternion.Euler(0,0,Mathf.Sin(Time.time*1.8f-i*.45f)*(1.5f+i*.8f));}
}
