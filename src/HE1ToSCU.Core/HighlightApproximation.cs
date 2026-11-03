namespace HE1ToSCU.Core;

public sealed record HighlightFit(float Strength,float Gloss,double Error);

/// <summary>Fit only the additional direct highlight with stock Godot 3 GTR1
/// clearcoat. Base GGX F0 (and its environment reflections) remains unchanged.
/// Clearcoat strength is an HDR coefficient, accepted by the stock float setter.</summary>
public static class HighlightApproximation
{
    public static double Lobe(double gloss,double mu)
    {
        double alpha=.1+(.001-.1)*gloss,a2=alpha*alpha,h=Math.Sqrt((1+mu)*.5);
        double d=(a2-1)/(Math.PI*Math.Log(a2)*(1+(a2-1)*h*h));
        double fresnel=.04+.96*Math.Pow(1-h,5),geometry=.25/(mu*.875+.125);
        return .25*d*fresnel*geometry*mu;
    }
    public static HighlightFit Fit(SpecularFit baseline,SpecularFit target,IReadOnlyList<SpecularSample> samples)
    {
        if(target.Specular<=baseline.Specular+1e-7f)return new HighlightFit(0,0,0);
        double[] edges=[0,1e-8,1e-6,1e-4,.01,.1,1];
        var unit=SpecularApproximation.Quadrature(24);
        var nodes=Enumerable.Range(1,edges.Length-1).SelectMany(i=>unit.Select(n=>(Mu:1-edges[i-1]-(edges[i]-edges[i-1])*n.Mu,Weight:(edges[i]-edges[i-1])*n.Weight))).ToArray();
        double count=samples.Sum(s=>(double)s.Count);
        var powers=samples.GroupBy(s=>baseline.Roughness[(s.Gloss<<8)|s.Mask]).Select(g=>(Roughness:g.Key/255.0,Weight:g.Sum(s=>(double)s.Count)/count)).ToArray();
        var wanted=nodes.Select(node=>powers.Sum(p=>p.Weight*(
            SpecularApproximation.GodotLobe(p.Roughness,target.Specular,node.Mu)-SpecularApproximation.GodotLobe(p.Roughness,baseline.Specular,node.Mu)+
            SpecularApproximation.GodotLobe(p.Roughness,target.Specular,node.Mu,true)-SpecularApproximation.GodotLobe(p.Roughness,baseline.Specular,node.Mu,true))*.5)).ToArray();
        double wantedPeak=powers.Sum(p=>p.Weight*(SpecularApproximation.GodotLobe(p.Roughness,target.Specular,1)-SpecularApproximation.GodotLobe(p.Roughness,baseline.Specular,1)));
        (double Strength,double Error) Evaluate(double gloss) {
            // Preserve the approved peak exactly, then fit the remaining lobe shape.
            double strength=wantedPeak/Lobe(gloss,1),error=0;
            for(int i=0;i<nodes.Length;i++){double delta=strength*Lobe(gloss,nodes[i].Mu)-wanted[i];error+=nodes[i].Weight*delta*delta;}
            return (strength,error);
        }
        int best=0;double bestError=double.PositiveInfinity;
        for(int i=0;i<=64;i++){double error=Evaluate(i/64.0).Error;if(error<bestError){best=i;bestError=error;}}
        double low=Math.Max(0,(best-1)/64.0),high=Math.Min(1,(best+1)/64.0);
        for(int i=0;i<24;i++) {
            double a=low+(high-low)/3,b=high-(high-low)/3;
            if(Evaluate(a).Error<Evaluate(b).Error)high=b;else low=a;
        }
        double gloss=(low+high)*.5;var result=Evaluate(gloss);
        return new HighlightFit((float)result.Strength,(float)gloss,result.Error);
    }
}
