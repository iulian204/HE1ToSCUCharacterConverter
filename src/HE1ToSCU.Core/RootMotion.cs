using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>Havok default reference-frame samples store translation XYZ and an unwrapped
/// rotation angle in W about Up. Motion is independent of the skeleton's local tracks.</summary>
public sealed class RootMotion
{
    public Vector3 Up { get; }
    public Vector3 Forward { get; }
    public float Duration { get; }
    public IReadOnlyList<Vector4> Samples { get; }

    public RootMotion(Vector3 up, Vector3 forward, float duration, IReadOnlyList<Vector4> samples)
    {
        if (!float.IsFinite(duration) || duration <= 0 || samples.Count == 0 ||
            !float.IsFinite(up.LengthSquared()) || up.LengthSquared() < 1e-8f ||
            !float.IsFinite(forward.LengthSquared()) || forward.LengthSquared() < 1e-8f ||
            samples.Any(s => !float.IsFinite(s.X) || !float.IsFinite(s.Y) || !float.IsFinite(s.Z) || !float.IsFinite(s.W)))
            throw new InvalidDataException("Invalid Havok reference-frame duration, axes or samples.");
        Up = Vector3.Normalize(up);
        Forward = Vector3.Normalize(forward);
        Duration = duration;
        Samples = samples.ToArray();
    }

    public Pose Sample(float time)
    {
        float frame = Math.Clamp(time / Duration, 0, 1) * (Samples.Count - 1);
        int first = (int)frame, last = Math.Min(first + 1, Samples.Count - 1);
        return ToPose(Vector4.Lerp(Samples[first], Samples[last], frame - first));
    }

    private Pose ToPose(Vector4 sample) => new(new(sample.X, sample.Y, sample.Z),
        Quaternion.CreateFromAxisAngle(Up, sample.W), Vector3.One);

    public IEnumerable<(float Time, Pose Pose)> Keys()
    {
        yield return (0, ToPose(Samples[0]));
        for (int i = 1; i < Samples.Count; i++)
        {
            // Keep complete turns even when two sparse samples differ by more than pi.
            double subdivisions = Math.Ceiling(Math.Abs((double)Samples[i].W - Samples[i - 1].W) / (Math.PI / 2));
            if (subdivisions > 100_000) throw new InvalidDataException("Excessive root-motion angular range.");
            int steps = Math.Max(1, (int)subdivisions);
            for (int step = 1; step <= steps; step++)
            {
                float fraction = step / (float)steps;
                yield return (Duration * ((i - 1 + fraction) / (Samples.Count - 1)),
                    ToPose(Vector4.Lerp(Samples[i - 1], Samples[i], fraction)));
            }
        }
    }
}
