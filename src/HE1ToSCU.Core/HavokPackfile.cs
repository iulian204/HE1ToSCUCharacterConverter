namespace HE1ToSCU.Core;

/// <summary>Section/fixup resolver. Pointers in disk objects are resolved through local/global
/// relocation tables, never interpreted as process addresses.</summary>
internal sealed class HavokPackfile
{
    internal sealed record Section(int Start,int Local,int Global,int Virtual,int Exports,int End);
    public BinaryData Data { get; }
    public string Version { get; }
    public Dictionary<int,string> Objects { get; } = [];
    private readonly Dictionary<int,int> pointers = [];
    public HavokPackfile(byte[] bytes)
    {
        if(bytes.Length<64 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{0x57,0xE0,0xE0,0x57,0x10,0xC0,0xC0,0x10}))
            throw new NotSupportedException("Expected a Havok binary packfile or XML hkpackfile. Tagfiles are not supported.");
        Data=new BinaryData(bytes,bytes[17]==0);
        if(bytes[16]!=4)throw new NotSupportedException("Only 32-bit Havok packfile layouts are supported in this increment.");
        if(Data.I32(12)!=8) throw new NotSupportedException($"Packfile header version {Data.I32(12)} is not supported.");
        Version=Data.String(40);
        if(!Version.StartsWith("hk_2010.2.0",StringComparison.Ordinal))
            throw new NotSupportedException($"Binary layout {Version} is not supported. Current binary profile: Havok 2010.2.0 (Generations). XML skeletons/interleaved animations are also accepted.");
        int n=Data.Count(20,16);var sections=new List<Section>();
        for(int i=0;i<n;i++)
        {
            int h=64+i*48;Data.Check(h,48);
            var s=new Section(Data.Count(h+20,bytes.Length),Data.Count(h+24,bytes.Length),Data.Count(h+28,bytes.Length),Data.Count(h+32,bytes.Length),Data.Count(h+36,bytes.Length),Data.Count(h+44,bytes.Length));
            if(s.Local>s.Global||s.Global>s.Virtual||s.Virtual>s.Exports||s.Exports>s.End)throw new InvalidDataException("Invalid Havok section boundaries.");
            Data.Check(s.Start,s.End);sections.Add(s);
        }
        int Address(int section,int offset)
        {
            if(section<0||section>=n||offset<0||offset>=sections[section].End)throw new InvalidDataException("Havok fixup target is out of section bounds.");
            return checked(sections[section].Start+offset);
        }
        foreach(var s in sections)
        {
            for(int p=s.Start+s.Local;p+8<=s.Start+s.Global;p+=8)
            {
                int from=Data.I32(p),to=Data.I32(p+4);if(from==-1)continue;
                if(from<0||from>s.Local-4||to<0||to>=s.Local)throw new InvalidDataException("Invalid local Havok fixup.");
                pointers.Add(s.Start+from,s.Start+to);
            }
            for(int p=s.Start+s.Global;p+12<=s.Start+s.Virtual;p+=12)
            {
                int from=Data.I32(p);if(from==-1)continue;
                if(from<0||from>s.Local-4)throw new InvalidDataException("Invalid global Havok fixup.");
                pointers.Add(s.Start+from,Address(Data.I32(p+4),Data.I32(p+8)));
            }
            for(int p=s.Start+s.Virtual;p+12<=s.Start+s.Exports;p+=12)
            {
                int from=Data.I32(p);if(from==-1)continue;
                if(from<0||from>=s.Local)throw new InvalidDataException("Invalid virtual Havok fixup.");
                Objects.Add(s.Start+from,Data.String(Address(Data.I32(p+4),Data.I32(p+8))));
            }
        }
    }
    public int Pointer(int field,bool optional=false)=>pointers.TryGetValue(field,out int value)?value:optional?0:throw new InvalidDataException($"Missing Havok fixup at 0x{field:X}.");
    public (int Address,int Count) Array(int field,int stride,int max=5_000_000)
    {
        int count=Data.Count(field+4,max);
        int address=count==0?0:Pointer(field);
        Data.Check(address,checked(count*stride));return(address,count);
    }
}
