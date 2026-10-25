#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
public sealed class ChairContactProbe : MonoBehaviour
{
    public readonly Dictionary<string,string> Contacts=new();
    void OnCollisionStay(Collision collision)
    {
        foreach(var c in collision.contacts)Contacts[c.thisCollider.name+" -> "+c.otherCollider.name]=$"{c.thisCollider.name} -> {c.otherCollider.name}: point={c.point}, normal={c.normal}";
    }
}
#endif
