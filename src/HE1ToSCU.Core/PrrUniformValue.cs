using System.Globalization;

namespace HE1ToSCU.Core;

/// <summary>One explicit Godot 3.1 Variant contract for text, expected values and runtime validation.</summary>
public sealed record PrrUniformValue(string VariantType, double[] Components, string Literal)
{
    public static string Number(double value)
    {
        if (!double.IsFinite(value) || !float.IsFinite((float)value)) throw new InvalidDataException("Non-finite/out-of-range shader value.");
        if(value==0 && BitConverter.SingleToInt32Bits((float)value)<0)return "-0.0";
        return ((float)value).ToString("G9", CultureInfo.InvariantCulture).Replace('E', 'e');
    }
    public static PrrUniformValue Convert(PrrUniform uniform, double[] row, string components = "xyzw", string storage = "float4")
    {
        if (uniform.ArrayDeclaration != null) throw new NotSupportedException("Godot 3.1 shader uniform arrays are unsupported.");
        if (storage=="bool4" && uniform.Type!="bool") throw new NotSupportedException("No verified bool4-to-numeric-vector binding.");
        int count = uniform.Type switch { "float" or "int" or "bool" => 1, "vec2" => 2, "vec3" => 3, "vec4" => 4, _ => 0 };
        if (count == 0) throw new NotSupportedException("No scalar/vector conversion for " + uniform.Type);
        if (components.Length < count || row.Length != 4) throw new InvalidDataException("Insufficient source components.");
        double[] values = components[..count].Select(c => row["xyzw".IndexOf(c)]).ToArray();
        if (values.Any(v => !double.IsFinite(v))) throw new InvalidDataException("Non-finite shader parameter.");
        if (uniform.Type == "int")
        {
            if (values[0] != Math.Truncate(values[0]) || values[0] < int.MinValue || values[0] > int.MaxValue)
                throw new InvalidDataException("Shader int requires an exact signed 32-bit integer.");
            return new("int", values, values[0].ToString("0", CultureInfo.InvariantCulture));
        }
        if (uniform.Type == "bool")
        {
            if (values[0] is not (0 or 1)) throw new InvalidDataException("Shader bool requires explicit 0 or 1.");
            return new("bool", values, values[0] == 1 ? "true" : "false");
        }
        if (storage == "int4" && values.Any(v => (double)(float)v != v)) throw new InvalidDataException("Source integer cannot be represented exactly by a Godot float uniform.");
        values = values.Select(v => { _ = Number(v); return (double)(float)v; }).ToArray();
        string kind = uniform.Type switch { "float" => "float", "vec2" => "Vector2", "vec3" => "Vector3", _ => uniform.IsColor ? "Color" : "Plane" };
        if (kind == "float")
        {
            string number = Number(values[0]);
            if (!number.Contains('.') && !number.Contains('e')) number += ".0";
            return new(kind, values, number);
        }
        return new(kind, values, kind + "( " + string.Join(", ", values.Select(Number)) + " )");
    }

    /// <summary>Only for a binding whose row-major matrix contract is independently established.</summary>
    public static PrrUniformValue MatrixRows(string type, double[][] rows)
    {
        int size = type switch { "mat2" => 2, "mat3" => 3, "mat4" => 4, _ => throw new NotSupportedException("Unsupported matrix type.") };
        if (rows.Length != size || rows.Any(r => r.Length < size)) throw new InvalidDataException("Matrix dimensions do not match the declared uniform.");
        double[] data = rows.SelectMany(r => r.Take(size)).ToArray();
        if (data.Any(v => !double.IsFinite(v) || (double)(float)v != v)) throw new InvalidDataException("Matrix has nonrepresentable values.");
        string kind;
        double[] serialized;
        if (size == 2) { kind = "Transform2D"; serialized = [data[0], data[2], data[1], data[3], 0, 0]; }
        else if (size == 3) { kind = "Basis"; serialized = data; }
        else
        {
            if (!data[12..].SequenceEqual(new double[] { 0, 0, 0, 1 }))
                throw new NotSupportedException("Godot 3.1 Transform cannot preserve a projective mat4 bottom row.");
            kind = "Transform";
            serialized = [data[0],data[1],data[2],data[4],data[5],data[6],data[8],data[9],data[10],data[3],data[7],data[11]];
        }
        return new(kind, data, kind + "( " + string.Join(", ", serialized.Select(Number)) + " )");
    }
}

public sealed class PrrAssignment
{
    public required string Name { get; init; }
    public required string UniformType { get; init; }
    public string? Hint { get; init; }
    public string? ShaderDefault { get; init; }
    public required string Origin { get; init; }
    public string? SourceName { get; init; }
    public required string Evidence { get; init; }
    public required string Confidence { get; init; }
    public required PrrUniformValue Expected { get; init; }
    public string? LegacyIssue => Origin=="HE1"&&Expected.VariantType=="Color"?"Color-hinted uniform was previously serialized as Plane":
        Expected.VariantType=="float"&&Expected.Literal.EndsWith(".0",StringComparison.Ordinal)?"Integer-form float literal previously loaded as an int Variant":null;
    public Dictionary<string, PrrReadback> Actual { get; } = new();
    public string RuntimeStatus => Actual.Values.Any(v=>v.Status!="verified")?"failed":
        Actual.ContainsKey("text")?(Actual.ContainsKey("binary")?"text_and_binary_verified":"text_verified"):"not_runtime_verified";
}
public sealed record PrrReadback(string Status, string VariantType, double[] Value, string? Reason = null);
public sealed record PrrParameterAudit(string SourceName, string Storage, double[][] RawSourceValue, string Classification,
    string Reason, string[] Destinations, string Evidence, string Confidence);
public sealed class PrrMappingResult
{
    public Dictionary<string,string> Values { get; } = new(StringComparer.Ordinal);
    public Dictionary<string,string> Mappings { get; } = new(StringComparer.Ordinal);
    public List<PrrAssignment> Assignments { get; } = [];
    public List<PrrParameterAudit> Parameters { get; } = [];
    public string[] Unmapped => Parameters.Where(p => p.Classification != "mapped").Select(p => p.SourceName).ToArray();
    public void Assign(PrrUniform uniform, PrrUniformValue value, string origin, string? source, string evidence, string confidence)
    {
        if (!Values.TryAdd(uniform.Name, value.Literal)) throw new InvalidDataException("Multiple source values target " + uniform.Name);
        Assignments.Add(new() { Name = uniform.Name, UniformType = uniform.Type, Hint = uniform.Hint, ShaderDefault = uniform.DefaultValue,
            Origin = origin, SourceName = source, Evidence = evidence, Confidence = confidence, Expected = value });
    }
}
