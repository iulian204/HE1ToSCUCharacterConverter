using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Managed reader for standard big-endian Mirage skeletal model v2/v4/v5/v6.
/// Structures cross-checked against HedgeLib hl_hh_model.h and ModelConverter.</summary>
public sealed class He1ModelReader
{
    private BinaryData b = null!;
    private int origin;
    private int version;
    private CancellationToken token;
    private int Offset(int p) { int a = checked(origin + b.Count(p, int.MaxValue)); b.Check(a, 1); return a; }
    public ModelAsset Read(string path, CancellationToken cancellationToken = default,SourceGame game=SourceGame.SonicGenerations)
    {
        token = cancellationToken;
        b = new BinaryData(File.ReadAllBytes(path), true);
        b.Check(0, 24);
        if (b.U32(0) != b.Bytes.Length) throw new InvalidDataException("Not a standard HE1 Mirage file (sample-chunk/HE2 containers are not supported).");
        version = b.I32(4);
        if (version is not (2 or 4 or 5 or 6)) throw new NotSupportedException($"HE1 skeletal model version {version} is not supported.");
        // Select the actual serialized model version, including re-exported Unleashed
        // assets using a newer shared Mirage layout; the game selects shader/HKX semantics.
        origin = b.Count(12, b.Bytes.Length);
        int root = origin;
        b.Check(root, version == 4 ? 48 : version == 2 ? 44 : 32);
        int nodeField = root + (version == 2 ? 24 : version == 4 ? 32 : 16);
        int nodeCount = b.Count(nodeField, 4096);
        int nodes = Offset(nodeField+4), matrices = Offset(nodeField+8);
        b.Check(nodes, checked(nodeCount*4)); b.Check(matrices, checked(nodeCount*64));
        var bones = new List<Bone>();
        var globals = new Matrix4x4[nodeCount];
        var parents = new int[nodeCount];
        var names = new string[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            token.ThrowIfCancellationRequested();
            int n = Offset(nodes+i*4); b.Check(n,8);
            parents[i] = b.I32(n); names[i] = b.String(Offset(n+4));
            int m = matrices+i*64;
            var raw = new Matrix4x4(b.F32(m),b.F32(m+4),b.F32(m+8),b.F32(m+12),b.F32(m+16),b.F32(m+20),b.F32(m+24),b.F32(m+28),b.F32(m+32),b.F32(m+36),b.F32(m+40),b.F32(m+44),b.F32(m+48),b.F32(m+52),b.F32(m+56),b.F32(m+60));
            globals[i] = Spatial.Inverse(Matrix4x4.Transpose(raw));
        }
        for (int i = 0; i < nodeCount; i++)
        {
            int p = parents[i];
            if (p < -1 || p >= nodeCount) throw new InvalidDataException("Model bone parent is out of range.");
            bones.Add(new Bone(names[i], p, p < 0 ? globals[i] : globals[i] * Spatial.Inverse(globals[p])));
        }
        Spatial.ValidateSkeleton(bones);
        if(version>=4 && b.Count(root+(version==4?24:8),100_000)>0)
            throw new NotSupportedException("Model has morph targets; conversion is stopped to avoid losing blend shapes.");
        var surfaces = new List<Surface>();
        if (version is 2 or 4)
        {
            ReadSlot(root, "opaque", surfaces, nodeCount);
            ReadSlot(root+8, "transparent", surfaces, nodeCount);
            ReadSlot(root+16, "punch", surfaces, nodeCount);
        }
        else
        {
            int count = b.Count(root,100_000), groups = Offset(root+4);
            b.Check(groups,checked(count*4));
            for (int i = 0; i < count; i++)
            {
                int g = Offset(groups+i*4); b.Check(g,40);
                ReadSlot(g,"opaque",surfaces,nodeCount); ReadSlot(g+8,"transparent",surfaces,nodeCount); ReadSlot(g+16,"punch",surfaces,nodeCount);
                int specialCount = b.Count(g+24,4096);
                if (specialCount > 0)
                {
                    // raw_special_meshes: type names, pointers to counts, pointers to mesh tables.
                    int types = Offset(g+28), counts = Offset(g+32), meshes = Offset(g+36);
                    for (int j = 0; j < specialCount; j++)
                    {
                        string slot = b.String(Offset(types+j*4));
                        int n = b.Count(Offset(counts+j*4),100_000), table = Offset(meshes+j*4);
                        ReadMeshTable(n,table,slot,surfaces,nodeCount);
                    }
                }
            }
        }
        if (surfaces.Count == 0) throw new InvalidDataException("Model contains no mesh surfaces.");
        return new ModelAsset(Path.GetFileNameWithoutExtension(path),bones,surfaces);
    }
    private void ReadSlot(int p, string slot, List<Surface> result, int nodeCount)
    {
        int count=b.Count(p,100_000);
        if (count>0) ReadMeshTable(count,Offset(p+4),slot,result,nodeCount);
    }
    private void ReadMeshTable(int count, int table, string slot, List<Surface> result, int nodeCount)
    {
        b.Check(table,checked(count*4));
        for (int i=0;i<count;i++) { token.ThrowIfCancellationRequested(); result.Add(ReadMesh(Offset(table+i*4),slot,nodeCount)); }
    }
    private Surface ReadMesh(int m, string slot, int nodeCount)
    {
        b.Check(m,44);
        string material=b.String(Offset(m));
        int faceCount=b.Count(m+4), faces=Offset(m+8), vertexCount=b.Count(m+12), stride=b.Count(m+16,4096), vertices=Offset(m+20), declaration=Offset(m+24);
        if (stride==0 || vertexCount==0) throw new InvalidDataException("Empty mesh vertex buffer.");
        b.Check(vertices,checked(vertexCount*stride)); b.Check(faces,checked(faceCount*2));
        int paletteCount=b.Count(m+28,4096), palette=paletteCount>0 ? Offset(m+32) : 0;
        b.Check(palette,checked(paletteCount*(version==6?2:1)));
        var elements=new List<(int Offset,uint Format,byte Type,byte Index)>();
        for (int i=0;i<64;i++)
        {
            int d=declaration+i*12; b.Check(d,12);
            if (b.U32(d+4)==uint.MaxValue) break;
            if (b.U16(d)!=0) throw new NotSupportedException("Multi-stream HE1 vertex buffers are not supported.");
            elements.Add((b.U16(d+2),b.U32(d+4),b.U8(d+9),b.U8(d+10)));
            if(i==63) throw new InvalidDataException("Unterminated vertex declaration.");
        }
        if (!elements.Any(e=>e.Type==0 && e.Index==0)) throw new InvalidDataException("Mesh has no POSITION0.");
        var output=new List<Vertex>(vertexCount);
        bool[]? missingTangents = null;
        for(int i=0;i<vertexCount;i++)
        {
            if((i&1023)==0) token.ThrowIfCancellationRequested();
            Vector3 position=default,normal=Vector3.UnitY, tangent=Vector3.UnitX, binormal=Vector3.UnitZ;
            Vector2 uv=default,uv2=default; Vector4 color=Vector4.One, weights=default, indices=new(-1);
            bool hasWeights=false, hasIndices=false;
            foreach(var e in elements)
            {
                // Additional UV/color sets have no direct Godot 3 ArrayMesh channel.
                if (e.Index>0 && !(e.Type==5 && e.Index==1)) continue;
                if (e.Type is not (0 or 1 or 2 or 3 or 5 or 6 or 7 or 10)) continue;
                int size=FormatSize(e.Format);
                if(e.Offset>stride-size) throw new InvalidDataException("Vertex element exceeds vertex stride.");
                int address = vertices + i * stride + e.Offset;
                // Some HE1 meshes store this NaN triplet in TANGENT0. Treat only
                // that encoding as missing data and leave other float checks strict.
                if (e.Type == 6 && e.Format == 0x2A23B9 &&
                    b.U32(address) == 0x7FFFFFFF && b.U32(address + 4) == 0x7FFFFFFF &&
                    b.U32(address + 8) == 0x7FFFFFFF)
                {
                    (missingTangents ??= new bool[vertexCount])[i] = true;
                    continue;
                }
                var v=ReadElement(address,e.Format);
                var xyz=new Vector3(v.X,v.Y,v.Z);
                switch(e.Type)
                {
                    case 0:position=xyz;break; case 1:weights=v;hasWeights=true;break; case 2:indices=v;hasIndices=true;break;
                    case 3:normal=xyz;break; case 5:if(e.Index==0) uv=new(v.X,v.Y);else uv2=new(v.X,v.Y);break;
                    case 6:tangent=xyz;break; case 7:binormal=xyz;break; case 10:color=v;break;
                }
            }
            if(hasWeights!=hasIndices) throw new InvalidDataException("Mesh must provide both blend weights and blend indices.");
            int[] bones=new int[4]; float[] w=[weights.X,weights.Y,weights.Z,weights.W]; float[] ix=[indices.X,indices.Y,indices.Z,indices.W];
            for(int j=0;j<4;j++)
            {
                if(w[j]<0) throw new InvalidDataException("Negative skin weight.");
                if(w[j]==0) continue;
                int local=checked((int)ix[j]);
                if(local<0||local>=paletteCount) throw new InvalidDataException("Skin index exceeds mesh bone palette.");
                bones[j]=version==6?b.U16(palette+local*2):b.U8(palette+local);
                if(bones[j]>=nodeCount) throw new InvalidDataException("Mesh palette refers to a missing model bone.");
            }
            float sum=w.Sum();
            if(hasWeights && sum<=0) throw new InvalidDataException("Skinned vertex has no positive weights.");
            if(sum>0) weights/=sum;
            normal=Unit(normal,Vector3.UnitY); tangent=Unit(tangent-normal*Vector3.Dot(normal,tangent),Vector3.UnitX);
            float hand=Vector3.Dot(Vector3.Cross(normal,tangent),binormal)<0?-1:1;
            output.Add(new Vertex(position,normal,new Vector4(tangent,hand),uv,uv2,color,bones,weights));
        }
        var strip=Enumerable.Range(0,faceCount).Select(i=>(int)b.U16(faces+i*2)).ToArray();
        var triangles=DecodeStrip(strip,vertexCount);
        if (missingTangents is not null) RebuildTangents(output, triangles, missingTangents);
        return new Surface(material,slot,output,triangles);
    }
    private void RebuildTangents(List<Vertex> vertices, List<int> triangles, bool[] missing)
    {
        var tangents = new Vector3[vertices.Count];
        var bitangents = new Vector3[vertices.Count];
        for (int i = 0; i < triangles.Count; i += 3)
        {
            if (i % 3072 == 0) token.ThrowIfCancellationRequested();
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (!missing[a] && !missing[b] && !missing[c]) continue;
            Vector3 edge1 = vertices[b].Position - vertices[a].Position;
            Vector3 edge2 = vertices[c].Position - vertices[a].Position;
            Vector2 uv1 = vertices[b].UV - vertices[a].UV;
            Vector2 uv2 = vertices[c].UV - vertices[a].UV;
            float determinant = uv1.X * uv2.Y - uv1.Y * uv2.X;
            float area = Vector3.Cross(edge1, edge2).Length();
            if (MathF.Abs(determinant) <= 1e-12f || area == 0) continue;

            // Solve the UV derivatives and weight by triangle area before averaging.
            float weight = area / determinant;
            Vector3 tangent = (edge1 * uv2.Y - edge2 * uv1.Y) * weight;
            Vector3 bitangent = (edge2 * uv1.X - edge1 * uv2.X) * weight;
            Accumulate(a);
            Accumulate(b);
            Accumulate(c);
            void Accumulate(int vertex)
            {
                tangents[vertex] += tangent;
                bitangents[vertex] += bitangent;
            }
        }
        for (int i = 0; i < vertices.Count; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            if (!missing[i]) continue;
            Vertex vertex = vertices[i];
            Vector3 normal = vertex.Normal;
            Vector3 tangent = tangents[i] - normal * Vector3.Dot(normal, tangents[i]);
            float lengthSquared = tangent.LengthSquared();
            if (float.IsFinite(lengthSquared) && lengthSquared > 0)
                tangent /= MathF.Sqrt(lengthSquared);
            else
            {
                // Collapsed UVs cannot define a direction. Use a perpendicular axis.
                Vector3 axis = MathF.Abs(normal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                tangent = Vector3.Normalize(axis - normal * Vector3.Dot(normal, axis));
            }
            float handedness = Vector3.Dot(Vector3.Cross(normal, tangent), bitangents[i]) < 0 ? -1 : 1;
            vertices[i] = vertex with { Tangent = new Vector4(tangent, handedness) };
        }
    }
    private static Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>1e-12f?Vector3.Normalize(v):fallback;
    public static List<int> DecodeStrip(IEnumerable<int> strip,int vertexCount)
    {
        var result=new List<int>(); int a=-1,c=-1; bool odd=false;
        foreach(int d in strip)
        {
            if(d==65535){a=c=-1;odd=false;continue;}
            if(d<0||d>=vertexCount) throw new InvalidDataException("Triangle index exceeds vertex count.");
            if(a<0){a=d;continue;} if(c<0){c=d;continue;}
            // Preserve the source strip winding. Both this HE1 mesh and Godot use
            // clockwise front faces; a cross-product reversal exposes the back faces.
            if(a!=c&&c!=d&&d!=a) {result.Add(a);result.Add(odd?d:c);result.Add(odd?c:d);}
            a=c;c=d;odd=!odd;
        }
        return result;
    }
    private static int FormatSize(uint f)=>f switch
    {
        0x2C83A4=>4,0x2C23A5=>8,0x2A23B9=>12,0x1A23A6=>16,
        0x1A2286 or 0x1A2386 or 0x1A2086 or 0x1A2186 or 0x182886=>4,
        0x2C235F or 0x2C2359 or 0x2C2259 or 0x2C2159 or 0x2C2059=>4,
        0x1A2360 or 0x1A235A or 0x1A225A or 0x1A215A or 0x1A205A=>8,
        0x2A2187 or 0x2A2087 or 0x2A2190 or 0x2A2090=>4,
        _=>throw new NotSupportedException($"HE1 vertex format 0x{f:X8} is not supported.")
    };
    private Vector4 ReadElement(int p,uint f)
    {
        if(f is 0x2C83A4 or 0x2C23A5 or 0x2A23B9 or 0x1A23A6)
        {
            int n=FormatSize(f)/4;return new(b.F32(p),n>1?b.F32(p+4):0,n>2?b.F32(p+8):0,n>3?b.F32(p+12):0);
        }
        if(f is 0x1A2286 or 0x1A2386 or 0x1A2086 or 0x1A2186 or 0x182886)
        {
            // D3DCOLOR is stored as a big-endian packed ARGB integer.
            if(f==0x182886) {uint c=b.U32(p);return new Vector4((c>>16)&255,(c>>8)&255,c&255,(c>>24)&255)/255;}
            float C(int i)=>f==0x1A2186?Math.Max(-1,unchecked((sbyte)b.U8(p+i))/127f):f==0x1A2386?unchecked((sbyte)b.U8(p+i)):b.U8(p+i);
            var v=new Vector4(C(0),C(1),C(2),C(3)); return f==0x1A2086?v/255:v;
        }
        if(f is 0x2C235F or 0x1A2360)
        {
            float H(int i)
            {
                int address = p + i * 2;
                float value = (float)BitConverter.UInt16BitsToHalf(b.U16(address));
                return float.IsFinite(value) ? value : throw new InvalidDataException($"Non-finite half float at 0x{address:X}.");
            }
            return new(H(0),H(1),f==0x1A2360?H(2):0,f==0x1A2360?H(3):0);
        }
        if(f is 0x2C2359 or 0x2C2259 or 0x2C2159 or 0x2C2059 or 0x1A235A or 0x1A225A or 0x1A215A or 0x1A205A)
        {
            bool signed=f is 0x2C2359 or 0x2C2159 or 0x1A235A or 0x1A215A;
            bool norm=f is 0x2C2159 or 0x2C2059 or 0x1A215A or 0x1A205A;
            float C(int i)=>signed?(norm?Math.Max(-1,b.I16(p+i*2)/32767f):b.I16(p+i*2)):(norm?b.U16(p+i*2)/65535f:b.U16(p+i*2));
            return new(C(0),C(1),FormatSize(f)==8?C(2):0,FormatSize(f)==8?C(3):0);
        }
        uint packed=b.U32(p); bool hend=f is 0x2A2190 or 0x2A2090; bool snorm=f is 0x2A2187 or 0x2A2190;
        float Unpack(int shift,int bits)
        {
            int value=(int)((packed>>shift)&((1u<<bits)-1));
            if(!snorm)return value/(float)((1<<bits)-1);
            if((value&(1<<(bits-1)))!=0)value-=1<<bits;
            return Math.Max(-1,value/(float)((1<<(bits-1))-1));
        }
        return hend?new(Unpack(0,11),Unpack(11,11),Unpack(22,10),0):new(Unpack(0,10),Unpack(10,10),Unpack(20,10),0);
    }
}
