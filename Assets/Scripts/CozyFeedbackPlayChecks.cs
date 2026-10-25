#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class CozyFeedbackPlayChecks
{
    public static IEnumerator Verify(CatController cat,Action<bool,string> check)
    {
        var manager=AudioManager.I;
        check(manager,"Cozy sound manager exists in actual game");if(!manager)yield break;
        string[] fields={"pawPush","itemThud","fragileBreak"};
        string[] names={"PawPush","ItemDrop","PorcelainDrop"};
        for(int n=0;n<fields.Length;n++)
        {
            var clip=(AudioClip)typeof(AudioManager).GetField(fields[n],BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
            check(clip&&clip.name.Contains(names[n]),"Live sound uses exported "+names[n]);
            if(!clip)continue;
            var samples=new float[clip.samples*clip.channels];clip.GetData(samples,0);float peak=0,energy=0;
            foreach(var s in samples){peak=Mathf.Max(peak,Mathf.Abs(s));energy+=s*s;}
            check(peak>.1f&&peak<=.51f&&energy>1&&Mathf.Abs(samples[0])<.001f&&Mathf.Abs(samples[samples.Length-1])<.001f,
                $"{names[n]} has headroom and click-free endpoints: peak={peak:F3}, duration={clip.length:F2}s");
        }
        // Disposable fixture, same player and Valuable collision/audio code.
        // Nothing is saved; the real room furniture is untouched.
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Disposable cozy audio floor";
        floor.transform.position=new Vector3(30,-.1f,30);floor.transform.localScale=new Vector3(4,.2f,4);
        var body=cat.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.position=new Vector3(30,.225f,30);body.rotation=Quaternion.identity;
        cat.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();
        var item=MakeItem(new Vector3(30,.08f,30.5f),false);
        yield return new WaitForSeconds(.3f);
        var source=manager.GetComponent<AudioSource>();source.Stop();
        bool pushed=cat.TryPushNearby();
        check(pushed&&source.isPlaying,"Successful actual paw contact triggers gentle push audio");
        yield return new WaitForSeconds(.6f);UnityEngine.Object.Destroy(item.gameObject);
        for(int n=0;n<2;n++)
        {
            source.Stop();var drop=MakeItem(new Vector3(31,.8f,30),n==1);bool scored=false;
            drop.OnDestroyed+=v=>scored=true;drop.ActivatePhysics();float deadline=Time.time+2;
            while(!scored&&Time.time<deadline)yield return null;
            check(scored&&source.isPlaying,(n==0?"Solid":"Porcelain")+" item falling triggers its cute drop sound");
            yield return new WaitForSeconds(.8f);UnityEngine.Object.Destroy(drop.gameObject);
        }
        UnityEngine.Object.Destroy(floor);
    }
    static Valuable MakeItem(Vector3 position,bool fragile)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Disposable sound-test item";
        g.transform.position=position;g.transform.localScale=Vector3.one*.16f;g.AddComponent<Rigidbody>();
        var item=g.AddComponent<Valuable>();item.fragile=fragile;item.noise=0;item.chaosValue=0;return item;
    }
}
#endif
