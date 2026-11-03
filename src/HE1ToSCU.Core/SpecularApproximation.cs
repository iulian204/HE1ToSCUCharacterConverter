namespace HE1ToSCU.Core;

public readonly record struct SpecularSample(byte Gloss, byte Mask, int Count);
public sealed record SpecularFit(float Specular, IReadOnlyDictionary<int,byte> Roughness, double InitialError, double Error);

/// <summary>Fit HE1's unnormalized Blinn lobe to the stock Godot 3 GGX lobe.
/// Roughness matches peaks of normalized Blinn and GGX normal distributions. With that fixed,
/// scalar F0 minimizes integrated squared highlight error over the source textures.
/// The fit uses a normal-facing viewer and a hemisphere of light directions, with
/// equal weight for the upstream GLES2/GLES3 geometry terms. No shader changes.</summary>
public static class SpecularApproximation
{
    // Deliberately use a single material lobe. Camera/eye-light lobes from the
    // reference shaders belong to the source lighting setup and are not baked
    // into a reusable material or reconstructed as an extra material response.
    public static float Power(float level,float gloss=1,SourceGame game=SourceGame.SonicGenerations) => game==SourceGame.SonicUnleashed ?
        Math.Clamp(gloss==0?0:gloss*MathF.Pow(2,level*10+2),1,512) : Math.Clamp(level*500*gloss,1,1024);
    public static float Brightness(float level,float gloss=1) => Math.Max(0,level*5*gloss);
    // D_Blinn(1)=(n+2)/(2*pi), D_GGX(1)=1/(pi*r^4).
    // Matching these normalized peaks avoids the overly broad GGX tail produced
    // by half-width matching, while intensity is calibrated independently.
    public static float PeakRoughness(float power) => MathF.Pow(2/(Math.Clamp(power,1,1024)+2),.25f);
    // V=N, N.L=mu, N.H=L.H=sqrt((1+mu)/2), metallic=0.
    public static double GodotLobe(double roughness,double specular,double mu,bool gles2=false)
    {
        double alpha=roughness*roughness, a2=alpha*alpha, h=Math.Sqrt((1+mu)*.5);
        double d=a2/(Math.PI*Math.Pow(1+(a2-1)*h*h,2));
        double g=gles2 ? .5/(2*mu*(1-alpha)+(mu+1)*alpha) : .25/(mu*(1-.5*alpha)+.5*alpha);
        double fresnel=Math.Pow(1-h,5), f=fresnel+(1-fresnel)*.16*specular*specular;
        return mu*d*g*f;
    }
    internal static (double Mu,double Weight)[] Quadrature(int count)
    {
        var nodes=new (double,double)[count];
        for(int i=0;i<count;i++) {
            double z=Math.Cos(Math.PI*(i+.75)/(count+.5)),derivative=0;
            for(int iteration=0;iteration<30;iteration++) {
                double p0=1,p1=z;
                for(int k=2;k<=count;k++) {double p=((2*k-1)*z*p1-(k-1)*p0)/k;p0=p1;p1=p;}
                derivative=count*(z*p1-p0)/(z*z-1);double next=z-p1/derivative;
                if(Math.Abs(next-z)<1e-15){z=next;break;}z=next;
            }
            nodes[i]=((z+1)*.5,1/((1-z*z)*derivative*derivative));
        }
        return nodes;
    }
    public static SpecularFit Fit(float powerLevel,float brightnessLevel,float tint,IReadOnlyList<SpecularSample> samples,SourceGame game=SourceGame.SonicGenerations,float maximumSpecular=1,float intensityScale=1)
    {
        if(!float.IsFinite(powerLevel)||!float.IsFinite(brightnessLevel)||!float.IsFinite(tint)) throw new InvalidDataException("Non-finite specular parameters.");
        if(!float.IsFinite(maximumSpecular)||maximumSpecular<=0||maximumSpecular>2.5f||!float.IsFinite(intensityScale)||intensityScale<0) throw new ArgumentOutOfRangeException(nameof(maximumSpecular));
        // Resolve even the sharpest representable RGBA8 roughness lobe. Uniform
        // angular sampling misses its peak and can incorrectly hide dark texels
        // behind near-zero roughness, causing bright pinpricks at other views.
        double[] edges=[0,1e-10,1e-8,1e-6,1e-4,.01,.1,1];
        var unit=Quadrature(24);
        var nodes=Enumerable.Range(1,edges.Length-1).SelectMany(i=>unit.Select(n=>(Mu:1-edges[i-1]-(edges[i]-edges[i-1])*n.Mu,Weight:(edges[i]-edges[i-1])*n.Weight))).ToArray();
        var aa=new double[256];var ab=new double[256];var bb=new double[256];
        var crossA=new Dictionary<byte,double[]>();var crossB=new Dictionary<byte,double[]>();var norms=new Dictionary<byte,double>();
        var glosss=samples.Select(s=>s.Gloss).Distinct().ToArray();
        foreach(byte gloss in glosss) {
            crossA[gloss]=new double[256];crossB[gloss]=new double[256];
            double n=Power(powerLevel,gloss/255f,game);norms[gloss]=2*(1-Math.Pow(.5,n+1))/(n+1);
        }
        for(int r=1;r<256;r++) foreach(var node in nodes) foreach(bool gles2 in new[]{false,true}) {
            double b=GodotLobe(r/255.0,0,node.Mu,gles2),a=GodotLobe(r/255.0,1,node.Mu,gles2)-b,w=node.Weight*.5;
            aa[r]+=w*a*a;ab[r]+=w*a*b;bb[r]+=w*b*b;
            foreach(byte gloss in glosss) {
                double target=Math.Pow((1+node.Mu)*.5,Power(powerLevel,gloss/255f,game)*.5);
                crossA[gloss][r]+=w*a*target;crossB[gloss][r]+=w*b*target;
            }
        }
        double Target(SpecularSample s)=>(game==SourceGame.SonicUnleashed ? Power(powerLevel,s.Gloss/255f,game)*.125f : Brightness(brightnessLevel,s.Gloss/255f))*Math.Max(0,tint)*s.Mask/255*intensityScale;
        int Key(SpecularSample s)=>(s.Gloss<<8)|s.Mask;
        var selected=samples.ToDictionary(Key,s=>(byte)Math.Clamp((int)Math.Round(PeakRoughness(Power(powerLevel,s.Gloss/255f,game))*255),1,255));
        double SolveScalar() {
            double numerator=0,denominator=0;
            foreach(var s in samples) {int r=selected[Key(s)];numerator+=s.Count*(Target(s)*crossA[s.Gloss][r]-ab[r]);denominator+=s.Count*aa[r];}
            return denominator==0?0:Math.Clamp(numerator/denominator,0,maximumSpecular*maximumSpecular);
        }
        double Error(SpecularSample s,int r,double f0Scale) => aa[r]*f0Scale*f0Scale+2*ab[r]*f0Scale+bb[r]-2*Target(s)*(crossA[s.Gloss][r]*f0Scale+crossB[s.Gloss][r])+Target(s)*Target(s)*norms[s.Gloss];
        double scalar=SolveScalar();
        double averageBrightness=samples.Sum(s=>s.Count*Target(s))/samples.Sum(s=>(double)s.Count);
        double previousScalar=Math.Pow(Math.Clamp(averageBrightness,0,maximumSpecular),2);
        double initial=samples.Sum(s=>s.Count*Error(s,selected[Key(s)],previousScalar));
        double total=samples.Sum(s=>s.Count*Error(s,selected[Key(s)],scalar));
        return new SpecularFit((float)Math.Sqrt(scalar),selected,initial,Math.Max(0,total));
    }
}
