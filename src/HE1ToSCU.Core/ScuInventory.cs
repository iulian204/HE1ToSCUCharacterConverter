using System.Text.Json;

namespace HE1ToSCU.Core;

public sealed class ScuInventory
{
    private static readonly Lazy<ScuInventory> Shared=new(()=>ReadBundled());
    public static ScuInventory Default=>Shared.Value;
    public int SchemaVersion {get;set;}
    public string InventoryVersion {get;set;}="";
    public int DescriptorVersion {get;set;}
    public ScuProfile[] Profiles {get;set;}=[];
    public JsonElement[] Files {get;set;}=[];
    public JsonElement[] References {get;set;}=[];
    public string[] Limitations {get;set;}=[];
    public static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
    private static ScuInventory ReadBundled()
    {
        using var stream=typeof(ScuInventory).Assembly.GetManifestResourceStream("SCU/sonic-inventory.json")??throw new InvalidDataException("Bundled SCU inventory is missing.");
        var result=JsonSerializer.Deserialize<ScuInventory>(stream,JsonOptions)??throw new InvalidDataException("Invalid bundled SCU inventory.");
        result.Validate();return result;
    }
    public ScuProfile Profile(bool superSonic)=>Profiles.Single(p=>p.Id==(superSonic?"SuperSonic":"Sonic"));
    public void Validate()
    {
        if(SchemaVersion!=1||DescriptorVersion!=1||string.IsNullOrWhiteSpace(InventoryVersion)||Profiles.Length!=2)
            throw new InvalidDataException("Unsupported SCU naming inventory schema.");
        if(!Profiles.Select(p=>p.Id).Order(StringComparer.Ordinal).SequenceEqual(new[]{"Sonic","SuperSonic"}))
            throw new InvalidDataException("SCU inventory must contain one Sonic and one SuperSonic profile.");
        foreach(var profile in Profiles)
        {
            ExportNames.ValidateStem(profile.SceneStem);
            ExportNames.ValidateStem(profile.MeshResourceName);
            if(profile.NativeSceneFile is{} native) ExportNames.ValidateStem(native);
            if(profile.Animations.Length==0||profile.Models.Length==0||string.IsNullOrWhiteSpace(profile.SceneStem)||string.IsNullOrWhiteSpace(profile.MeshResourceName))throw new InvalidDataException("Incomplete SCU naming inventory.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var animation in profile.Animations)
            {
                ExportNames.ValidateStem(animation.Name);
                if(!float.IsFinite(animation.Duration)||animation.Duration<=0 || animation.PlayerKey.IndexOfAny(['"','\r','\n',':','/','\\'])>=0)
                    throw new InvalidDataException("Invalid SCU animation metadata or player key.");
                if(!names.Add(animation.Name))throw new InvalidDataException("Duplicate SCU animation filename.");
                if(animation.Eligible&&!keys.Add(animation.PlayerKey))throw new InvalidDataException("Duplicate SCU internal animation identifier.");
                animation.Semantic=AnimationSemantics.Classify(animation.Name);
                animation.Motion?.Validate();
            }
        }
    }
}
public sealed class ScuProfile
{
    public string Id {get;set;}="";
    public string ResourceRoot {get;set;}="";
    public string SceneStem {get;set;}="";
    public string SceneResource {get;set;}="";
    public string RootNodeName {get;set;}="";
    public string SkeletonNodeName {get;set;}="";
    public string MeshNodeName {get;set;}="";
    public string MeshResourceName {get;set;}="";
    public string? NativeSceneFile {get;set;}
    public string? NativeScenePath {get;set;}
    public string? SceneRemapText {get;set;}
    public JsonElement[] Models {get;set;}=[];
    public JsonElement[] Skeletons {get;set;}=[];
    public Dictionary<string,string[]> BoneAliases {get;set;}=[];
    public ScuAnimation[] Animations {get;set;}=[];
}
public sealed class ScuAnimation
{
    public string Name {get;set;}="";
    public string Path {get;set;}="";
    public string ResourceName {get;set;}="";
    public string PlayerKey=>string.IsNullOrEmpty(ResourceName)?Name:ResourceName;
    public float Duration {get;set;}
    public bool Loop {get;set;}
    public bool Eligible {get;set;}
    public string EligibilityReason {get;set;}="";
    public string? Error {get;set;}
    public int MaximumTrackKeys {get;set;}
    public int UniqueKeyTimes {get;set;}
    public float? MedianKeyInterval {get;set;}
    public JsonElement[] Tracks {get;set;}=[];
    public MotionDescriptor? Motion {get;set;}
    public AnimationSemantic Semantic {get;set;}=AnimationSemantic.Empty;
}
public sealed record ModelExportIdentity(string SceneStem,string RootNodeName,string SkeletonNodeName,string MeshNodeName,string MeshResourceName,
    string? NativeSceneFile=null,string? SceneRemapText=null);
public sealed record AnimationExportIdentity(string FileStem,string ResourceName,string PlayerKey);
