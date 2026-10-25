using UnityEngine;

// Antialiased, code-native control symbols matching the selected first-row design.
// Alpha masks sit on the existing buttons; hit areas and touch ownership do not change.
public static class CatActionIcons
{
    static readonly Vector2[] JumpToes={new(-.30f,-.11f),new(-.115f,.035f),new(.115f,.035f),new(.30f,-.11f)};
    static readonly Vector2[] PushToes={new(-.21f,.255f),new(-.06f,.16f),new(-.07f,.015f)};
    public static Texture2D Create(bool jump)
    {
        const int size=256;var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            var p=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1);
            float d=jump?Jump(p):Push(p);
            pixels[y*size+x]=new Color32(255,253,245,(byte)Mathf.RoundToInt(255*Mathf.Clamp01(.5f-d*size*.5f)));
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=jump?"Paw and upward arrow":"Paw pushing a block",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
    }
    static float Jump(Vector2 p)
    {
        float d=Ellipse(p,new Vector2(0,-.30f),new Vector2(.22f,.18f));
        d=Mathf.Min(d,Ellipse(p,new Vector2(-.13f,-.39f),new Vector2(.14f,.105f)));
        d=Mathf.Min(d,Ellipse(p,new Vector2(.13f,-.39f),new Vector2(.14f,.105f)));
        foreach(var center in JumpToes)
            d=Mathf.Min(d,Ellipse(p,center,new Vector2(.078f,.107f)));
        d=Mathf.Min(d,Segment(p,new Vector2(0,.28f),new Vector2(0,.60f),.055f));
        d=Mathf.Min(d,Segment(p,new Vector2(-.16f,.44f),new Vector2(0,.60f),.055f));
        return Mathf.Min(d,Segment(p,new Vector2(.16f,.44f),new Vector2(0,.60f),.055f));
    }
    static float Push(Vector2 p)
    {
        var q=p-new Vector2(-.40f,-.11f);float a=-25*Mathf.Deg2Rad;
        q=new Vector2(q.x*Mathf.Cos(a)-q.y*Mathf.Sin(a),q.x*Mathf.Sin(a)+q.y*Mathf.Cos(a));
        float paw=Box(q,Vector2.zero,new Vector2(.24f,.135f),.035f);
        paw=Mathf.Min(paw,Ellipse(p,new Vector2(-.23f,.06f),new Vector2(.19f,.18f)));
        foreach(var center in PushToes)
            paw=Mathf.Min(paw,Ellipse(p,center,new Vector2(.10f,.10f)));
        // Three toe pads and a central pad are knocked out to the orange below.
        float holes=Ellipse(p,new Vector2(-.25f,.025f),new Vector2(.062f,.078f));
        foreach(var center in PushToes)
            holes=Mathf.Min(holes,Ellipse(p,center,new Vector2(.044f,.044f)));
        float d=Mathf.Max(paw,-holes);
        d=Mathf.Min(d,Box(p,new Vector2(.32f,-.12f),new Vector2(.205f,.21f),.035f));
        d=Mathf.Min(d,Segment(p,new Vector2(.04f,.36f),new Vector2(.31f,.36f),.037f));
        d=Mathf.Min(d,Segment(p,new Vector2(.21f,.46f),new Vector2(.31f,.36f),.037f));
        return Mathf.Min(d,Segment(p,new Vector2(.21f,.26f),new Vector2(.31f,.36f),.037f));
    }
    static float Ellipse(Vector2 p,Vector2 c,Vector2 r)
    {var q=p-c;return (new Vector2(q.x/r.x,q.y/r.y).magnitude-1)*Mathf.Min(r.x,r.y);}
    static float Box(Vector2 p,Vector2 c,Vector2 half,float radius)
    {var q=p-c;q=new Vector2(Mathf.Abs(q.x),Mathf.Abs(q.y))-half+Vector2.one*radius;return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;}
    static float Segment(Vector2 p,Vector2 a,Vector2 b,float radius)
    {var d=b-a;return (p-a-d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)).magnitude-radius;}
}
