using System.Numerics;

namespace HE1ToSCU.Core;

public sealed record He1Texture(string Name, string Type, int UvIndex, int WrapU, int WrapV, string? SourcePath);
public sealed record He1Parameter(string Name, string Storage, double[][] Values, string? RawName = null, byte[]? Flags = null);
public sealed record He1Material(string Name, string Shader, float AlphaThreshold, bool DoubleSided, bool Additive,
    IReadOnlyDictionary<string, Vector4[]> Parameters, IReadOnlyList<He1Texture> Textures)
{
    public IReadOnlyList<He1Parameter> NativeParameters { get; init; } = [];
    public IReadOnlyDictionary<string, He1Parameter> FloatMetadata { get; init; } = new Dictionary<string, He1Parameter>();
    public int? MaterialVersion { get; init; }
    public string? SourcePath { get; init; }
    public string? SubShader { get; init; }
    public byte UnknownFlag { get; init; }
    public byte? AlphaThresholdByte { get; init; }
    public IEnumerable<He1Parameter> AllParameters => Parameters.Select(p => new He1Parameter(p.Key, "float4",
        p.Value.Select(v => new double[] { v.X, v.Y, v.Z, v.W }).ToArray(),
        FloatMetadata.GetValueOrDefault(p.Key)?.RawName, FloatMetadata.GetValueOrDefault(p.Key)?.Flags)).Concat(NativeParameters);
    public Vector4 Parameter(string name, Vector4 fallback) => Parameters.TryGetValue(name, out var values) && values.Length > 0 ? values[0] : fallback;
}

/// <summary>Standard Mirage material v1/v3, with v1 texset/texture indirection.
/// Layouts: HedgeLib hl_hh_material.h and hl_hh_texture.h.</summary>
public sealed class He1MaterialReader
{
    private sealed class Mirage
    {
        public BinaryData Data { get; }
        public int Origin { get; }
        public int Version { get; }
        public Mirage(string path)
        {
            Data = new BinaryData(File.ReadAllBytes(path), true); Data.Check(0, 24);
            if (Data.U32(0) != Data.Bytes.Length) throw new NotSupportedException($"Material dependency is not a standard Mirage file: {Path.GetFileName(path)}.");
            Origin = Data.Count(12, Data.Bytes.Length); Version = Data.I32(4);
        }
        public int Pointer(int field) { int p = checked(Origin + Data.Count(field, int.MaxValue)); Data.Check(p, 1); return p; }
    }

    public He1Material Read(string path,SourceGame game=SourceGame.SonicGenerations)
    {
        var file = new Mirage(path); var b = file.Data; int root = file.Origin;
        if (file.Version is not (1 or 3)) throw new NotSupportedException($"HE1 material version {file.Version} is not supported: {Path.GetFileName(path)}.");
        b.Check(root, 36);
        var parameters = new Dictionary<string, Vector4[]>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, He1Parameter>(StringComparer.OrdinalIgnoreCase);
        var native = new List<He1Parameter>();
        int count = b.U8(root + 20), table = count > 0 ? file.Pointer(root + 24) : 0;
        b.Check(table, count * 4);
        for (int i = 0; i < count; i++)
        {
            if (b.U32(table + i * 4) == 0) { native.Add(new($"<null float4[{i}]>","float4",[])); continue; }
            int p = file.Pointer(table + i * 4); b.Check(p, 12);
            int n = b.U8(p + 2);
            string rawName = b.U32(p+4)==0?"":b.String(file.Pointer(p + 4)), name = rawName.Trim();
            if (n == 0)
            {
                native.Add(new(name.Length==0?$"<unnamed float4[{i}]>":name,"float4",[],rawName,[b.U8(p),b.U8(p+1),b.U8(p+3)]));
                continue;
            }
            int values = file.Pointer(p + 8); b.Check(values, n * 16);
            if (name.Length == 0)
            {
                native.Add(new($"<unnamed float4[{i}]>", "float4", Enumerable.Range(0,n).Select(j =>
                    Enumerable.Range(0,4).Select(k => (double)b.F32(values+j*16+k*4)).ToArray()).ToArray(), rawName,
                    [b.U8(p),b.U8(p+1),b.U8(p+3)]));
                continue;
            }
            if (parameters.TryGetValue(name, out var previous))
                native.Add(new(name, "float4", previous.Select(v => new double[] {v.X,v.Y,v.Z,v.W}).ToArray(),
                    metadata[name].RawName, metadata[name].Flags));
            parameters[name] = Enumerable.Range(0, n).Select(j => { int v = values + j * 16; return new Vector4(b.F32(v), b.F32(v + 4), b.F32(v + 8), b.F32(v + 12)); }).ToArray();
            metadata[name] = new(name, "float4", [], rawName, [b.U8(p), b.U8(p+1), b.U8(p+3)]);
        }
        // Mirage stores signed int4 and byte-sized bool4 arrays in separate tables, not as IEEE floats.
        foreach (var (countOffset, pointerOffset, storage, stride) in new[] { (21,28,"int4",16), (22,32,"bool4",4) })
        {
            int nParams = b.U8(root + countOffset), entries = nParams > 0 ? file.Pointer(root + pointerOffset) : 0;
            b.Check(entries, nParams * 4);
            for (int i = 0; i < nParams; i++)
            {
                if(b.U32(entries+i*4)==0){native.Add(new($"<null {storage}[{i}]>",storage,[]));continue;}
                int p = file.Pointer(entries + i * 4); b.Check(p, 12);
                string rawName = b.U32(p+4)==0?"":b.String(file.Pointer(p + 4)), name = rawName.Trim();
                if(name.Length==0)name=$"<unnamed {storage}[{i}]>";
                int n = b.U8(p + 2), values = n == 0 ? 0 : file.Pointer(p + 8); b.Check(values, n * stride);
                var rows = Enumerable.Range(0, n).Select(j => Enumerable.Range(0,4).Select(k => storage == "int4" ?
                    (double)b.I32(values + j * stride + k * 4) : b.U8(values + j * stride + k)).ToArray()).ToArray();
                native.Add(new(name, storage, rows, rawName, [b.U8(p), b.U8(p+1), b.U8(p+3)]));
            }
        }
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var textures = new List<He1Texture>();
        He1Texture Texture(Mirage owner, int p)
        {
            var d = owner.Data; d.Check(p, 12);
            string name = d.String(owner.Pointer(p)), type = d.String(owner.Pointer(p + 8));
            string fileName = Path.GetFileName(name.Replace('\\', '/'));
            if (Path.GetExtension(fileName).Length == 0) fileName += ".dds";
            string? resolved = Find(folder, fileName);
            return new He1Texture(name, type, d.U8(p + 4), d.U8(p + 5), d.U8(p + 6), resolved);
        }
        if (file.Version == 3)
        {
            int n = b.U8(root + 23), entries = n > 0 ? file.Pointer(root + 12) : 0;
            b.Check(entries, n * 4);
            for (int i = 0; i < n; i++) if (b.U32(entries + i * 4) != 0) textures.Add(Texture(file, file.Pointer(entries + i * 4)));
        }
        else
        {
            string texsetName = b.String(file.Pointer(root + 8));
            string? texsetPath = Find(folder, texsetName + ".texset");
            if (texsetPath == null) throw new FileNotFoundException($"Material texset is missing: {texsetName}.texset");
            var set = new Mirage(texsetPath); int n = set.Data.Count(set.Origin, 4096), entries = n > 0 ? set.Pointer(set.Origin + 4) : 0;
            set.Data.Check(entries, n * 4);
            for (int i = 0; i < n; i++)
            {
                string entry = set.Data.String(set.Pointer(entries + i * 4));
                string? entryPath = Find(folder, entry + ".texture");
                if (entryPath == null) throw new FileNotFoundException($"Material texture entry is missing: {entry}.texture");
                var texture = new Mirage(entryPath); textures.Add(Texture(texture, texture.Origin));
            }
        }
        return new He1Material(Path.GetFileNameWithoutExtension(path), b.String(file.Pointer(root)), b.U8(root + 16) / 255f,
            b.U8(root + 17) != 0, b.U8(root + 18) != 0, parameters, textures)
            { NativeParameters = native, FloatMetadata = metadata, MaterialVersion = file.Version, SourcePath=Path.GetFullPath(path),
                SubShader=b.U32(root+4)==0?null:b.String(file.Pointer(root+4)),UnknownFlag=b.U8(root+19),AlphaThresholdByte=b.U8(root+16) };
    }

    internal static string? Find(string folder, string name)
    {
        string safeName = Path.GetFileName(name.Replace('\\', '/'));
        foreach (string directory in new[] { folder, Path.Combine(folder, "textures"), Path.Combine(folder, "images") })
        {
            if (!Directory.Exists(directory)) continue;
            string direct = Path.Combine(directory, safeName);
            if (File.Exists(direct)) return direct;
            string? match = Directory.EnumerateFiles(directory).FirstOrDefault(p => Path.GetFileName(p).Equals(safeName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return null;
    }
}
