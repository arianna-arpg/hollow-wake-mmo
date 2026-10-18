using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI.Loading;

/// <summary>The same part recipe paints a spirit in loading, entry scenes, or any later presentation activity.</summary>
public static class SpiritCanvas
{
    private static Vector2[] Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Enumerable.Range(0,13)
        .Select(i => { var t=i/12f;return Mathf.Pow(1-t,3)*a+3*(1-t)*(1-t)*t*b+3*(1-t)*t*t*c+t*t*t*d; }).ToArray();
    private static Vector2[] Quadratic(Vector2 a, Vector2 b, Vector2 c) => Curve(a,a+(b-a)*2/3,c+(b-c)*2/3,c);
    public static void Draw(Control target, WispLook look, Vector2 position, float angle, float scale, double time, Action<Vector2, float, Color> paintGlow)
    {
        var painters=new Dictionary<string,Action<WispPart,float>> {
            ["disc"]=(part,r)=>{
                target.DrawCircle(Vector2.Zero,r,new Color(look.Palette["base"]));
                paintGlow(new(-r*.25f,-r*.25f),r*.7f,new Color(look.Palette["light"]) with { A=.5f });
                target.DrawArc(Vector2.Zero,r,0,Mathf.Tau,48,new Color(look.Palette["outline"]) with { A=.75f },1.6f,true);
            },
            ["halo"]=(part,r)=>{
                var glow=new Color(look.Palette["glow"]);
                target.DrawArc(Vector2.Zero,r*.8f,0,Mathf.Tau,48,glow with { A=.28f },Math.Max(3,r*.2f),true);
                target.DrawArc(Vector2.Zero,r*.8f,0,Mathf.Tau,48,glow with { A=.75f },Math.Max(1.2f,r*.06f),true);
            },
            ["eyes"]=(part,r)=>{
                var n=(int)part.Param("n",2);var size=(float)part.Param("size",.1);var glow=new Color(look.Palette["glow"]);
                for(var i=0;i<n;i++){
                    var a=n==1?0:(i/(double)(n-1)-.5)*part.Param("spread",.5)*2;
                    var at=new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*r*(float)part.Param("dist",.72);
                    paintGlow(at,r*size*2.6f,glow with { A=.65f });target.DrawCircle(at,r*size,glow);target.DrawCircle(at,r*size*.38f,Colors.White);
                }
            },
            ["wisps"]=(part,r)=>{
                var n=(int)part.Param("n",3);
                for(var i=0;i<n;i++){
                    var off=(i/(float)Math.Max(1,n-1)-.5f)*r*.9f;var sway=(float)Math.Sin(time*2.2+i*2.1)*r*.28f;
                    var len=r*(1+.25f*(float)Math.Sin(time*1.7+i));
                    target.DrawPolyline(Quadratic(new(-r*.3f,off),new(-r*.75f,off+sway*.5f),new(-r*.3f-len,off+sway)),
                        new Color(look.Palette["glow"]) with { A=.4f-i*.06f },Math.Max(1.5f,r*(.16f-i*.03f)),true);
                }
            },
        };
        foreach(var part in look.Parts){
            target.DrawSetTransform(position+new Vector2(part.X,part.Y).Rotated(angle)*look.Radius*scale,angle,new Vector2(scale,scale));
            if(painters.TryGetValue(part.Kind,out var paint))paint(part,look.Radius*part.Scale);
        }
        target.DrawSetTransform(Vector2.Zero);
    }
    private static double Hash(double a, double b)
    {
        unchecked { var h=(uint)((int)a*374761393+(int)b*668265263); h=(h^(h>>13))*1274126177; h^=h>>16; return h/4294967296.0; }
    }
    private static double Wrap(double v, double span) => (v % span + span) % span;
    public static void Abyss(Control target, Vector2 size, double time, double camX, double camY, Color color, Action<Vector2,float,Color> glow)
    {
        for (var i = 0; i < 4; i++)
        {
            var radius = 180 + Hash(i, 41) * 240;
            var x = Wrap(Hash(i, 43) * (size.X + radius * 2) + time * (1.5 + Hash(i, 45) * 2) - camX * .12, size.X + radius * 2) - radius;
            var y = Wrap(Hash(i, 47) * (size.Y + radius * 2) + Math.Sin(time * .05 + i * 2.1) * 30 - camY * .12, size.Y + radius * 2) - radius;
            glow(new((float)x, (float)y), (float)radius, color with { A = .035f });
        }
        foreach (var (count, parallax, moteSize, alpha, salt) in new[] { (26,.22,.9,.16,3), (18,.45,1.5,.24,101), (10,.78,2.2,.34,211) })
        for (var i = 0; i < count; i++)
        {
            var x = Wrap(Hash(i, salt) * (size.X + 40) + time * (2 + Hash(i, salt + 2) * 5) - camX * parallax, size.X + 40) - 20;
            var y = Wrap(Hash(i, salt + 4) * (size.Y + 40) + time * (1 + Hash(i, salt + 6) * 3)
                + Math.Sin(time * (.3 + Hash(i, salt + 8) * .4) + i) * 9 - camY * parallax, size.Y + 40) - 20;
            target.DrawCircle(new((float)x, (float)y), (float)(moteSize * (.8 + Hash(i, salt + 12) * .5)),
                color with { A = (float)(alpha * (.6 + .4 * Math.Sin(time * (.4 + Hash(i, salt + 10) * .8) + i * 1.7))) });
        }
    }
}
