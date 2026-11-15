using System.Text.RegularExpressions;

namespace HE1ToSCU.Core;

public sealed record AnimationSemantic(string Normalized,string Action,string Phase,string Direction,string Context,string Form,string[] Tokens)
{
    public static readonly AnimationSemantic Empty=new("","unknown","unspecified","","","unknown",[]);
}
public static class AnimationSemantics
{
    // Exact observed compounds and ordinary terminology; dash/boost/jet remain distinct actions.
    private static readonly Dictionary<string,string> Words=new(StringComparer.OrdinalIgnoreCase)
    {
        ["standing"]="idle",["standstill"]="idle",["wait"]="idle",["running"]="run",["walking"]="walk",
        ["leap"]="jump",["falling"]="fall",["landing"]="land",["dead"]="death",["die"]="death",
        ["hurt"]="damage",["rail"]="grind",["stomping"]="stomp",["skydiving"]="dive",["lightdash"]="ringdash",
        ["movestop"]="brake",["turbo"]="boost",["drowning"]="drown",["drow"]="drown",["sliding"]="slide",
        ["delux"]="deluxe",["swich"]="switch",["grindswich"]="grind switch",["grindmove"]="grind move",
        ["grindquickjump"]="grind jump quick",["grindsquat"]="grind squat",["grindstandlong"]="grind stand long",
        ["grindstand"]="grind stand",["walkfast"]="walk fast",["reactionplate"]="plate",["jumpstand"]="jump stand",
        ["springjump"]="spring jump",["selectjump"]="select jump",["polespin"]="pole spin",["bobsleigh"]="vehicle bobsleigh",
        ["coaster"]="vehicle coaster",["quickstep"]="quick step",["airboost"]="air boost",["dashring"]="dash ring",
        ["loopstart"]="loop start",["standup"]="recover"
    };
    private static readonly string[] Priority=["grind","vehicle","select","spring","ringdash","pole","pulley","box","water","drown","wall","dive","drift","brake","damage","death","recover","homing","stomp","slide","squat","trick","result","change","ph","save","air","balloon","ball","boost","jet","dash","run","jog","walk","jump","fall","land","idle","float","crawl","freeze","stumble","hurdle","plate","rocket","ladder","clash"];
    public static AnimationSemantic Classify(string name)
    {
        string form=name.StartsWith("ssn_",StringComparison.OrdinalIgnoreCase)||name.StartsWith("super",StringComparison.OrdinalIgnoreCase)?"super":
            Regex.IsMatch(name,@"^(sn|sonic|sns)_",RegexOptions.IgnoreCase)?"normal":"unknown";
        string value=Regex.Replace(name,@"([a-z0-9])([A-Z])","$1_$2");
        // Uppercase direction/phase clusters in Unleashed: LE, RS, FE, DS, etc.
        value=Regex.Replace(value,@"(?<=_)([LRFBUDEMS]{2,3})(?=_|$)",m=>string.Join("_",m.Value.ToCharArray()));
        value=Regex.Replace(value,@"(?<![a-z])([DFULR])(?=loop)","$1_",RegexOptions.IgnoreCase);
        value=Regex.Replace(value.ToLowerInvariant(),@"^(sn|ssn|sns|sonic)_","");
        value=Regex.Replace(value,@"([a-z])([0-9]+)","$1_$2");
        var tokens=Regex.Split(value,@"[^a-z0-9]+").Where(t=>t.Length>0).SelectMany(t=>(Words.GetValueOrDefault(t)??t).Split(' ')).ToArray();
        string phase=tokens.Contains("loop")?"loop":tokens.LastOrDefault() is "s" or "start"?"start":tokens.LastOrDefault() is "e" or "end"?"end":"unspecified";
        var directions=tokens.Where(t=>t is "l" or "r" or "left" or "right" or "f" or "b" or "u" or "d")
            .Select(t=>t switch{"l"=>"left","r"=>"right","f"=>"forward","b"=>"back","u"=>"up","d"=>"down",_=>t}).ToArray();
        var normalized=tokens.Where((t,i)=>!((t is "s" or "e")&&i==tokens.Length-1)).ToArray();
        string action=Priority.FirstOrDefault(tokens.Contains)??"unknown";
        if(tokens.FirstOrDefault()=="start"||action=="unknown"&&tokens.Contains("start"))action="start";
        if(action=="air"&&tokens.Contains("boost"))action="airboost";
        if(action=="dash"&&tokens.Contains("ring"))action="dashring";
        string context=string.Join("/",tokens.Where(t=>t is "air" or "ground" or "water" or "fast" or "slow" or "tired" or "electric" or "bomb" or "near" or "far" or "fv" or "sv"));
        return new(string.Join("_",normalized),action,phase,string.Join("/",directions),context,form,tokens);
    }
    public static double TokenSimilarity(AnimationSemantic a,AnimationSemantic b)
    {
        var left=a.Tokens.ToHashSet(StringComparer.Ordinal);var right=b.Tokens.ToHashSet(StringComparer.Ordinal);
        return left.Count+right.Count==0?0:left.Intersect(right).Count()/(double)left.Union(right).Count();
    }
}
