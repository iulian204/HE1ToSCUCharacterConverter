using System.Numerics;

namespace HE1ToSCU.Core;

public static class AnimationSampling
{
    public static float Time(AnimationClip clip, int frame) => clip.Times?[frame] ??
        (clip.Frames.Length == 1 ? 0 : clip.Duration * frame / (clip.Frames.Length - 1));

    public static Pose Interpolate(Pose a, Pose b, float weight) => new(
        Vector3.Lerp(a.Translation, b.Translation, weight),
        Quaternion.Normalize(Quaternion.Slerp(a.Rotation, b.Rotation, weight)),
        Vector3.Lerp(a.Scale, b.Scale, weight));

    public static Pose[] Sample(AnimationClip clip, float time)
    {
        time = Math.Clamp(time, 0, clip.Duration);
        if (clip.SourceSampler != null) return clip.SourceSampler(time);
        if (clip.Frames.Length == 1) return clip.Frames[0];
        int low = 0, high = clip.Frames.Length - 1;
        while (high - low > 1) { int mid = (low + high) / 2; if (Time(clip, mid) <= time) low = mid; else high = mid; }
        float width = Time(clip, high) - Time(clip, low);
        float weight = width <= 0 ? 0 : Math.Clamp((time - Time(clip, low)) / width, 0, 1);
        return clip.Frames[low].Zip(clip.Frames[high], (a,b) => Interpolate(a,b,weight)).ToArray();
    }

    public static AnimationClip Complete(AnimationClip clip, IReadOnlyList<Pose> reference)
    {
        Pose[] Expand(Pose[] tracks) {
            var local = reference.ToArray();
            for (int t = 0; t < tracks.Length; t++) {
                int bone=clip.TrackToBone[t];var delta=tracks[t];var rest=reference[bone];
                local[bone]=clip.Blend switch {
                    AnimationBlend.Normal=>delta,
                    AnimationBlend.AdditiveDeprecated=>new Pose(delta.Translation+Vector3.Transform(rest.Translation,delta.Rotation),Quaternion.Normalize(Quaternion.Concatenate(rest.Rotation,delta.Rotation)),rest.Scale*delta.Scale),
                    AnimationBlend.Additive=>new Pose(rest.Translation+Vector3.Transform(delta.Translation,rest.Rotation),Quaternion.Normalize(Quaternion.Concatenate(delta.Rotation,rest.Rotation)),rest.Scale*delta.Scale),
                    _=>throw new NotSupportedException("Unsupported blend hint.")
                };
            }
            return local;
        }
        return clip with { Frames = clip.Frames.Select(Expand).ToArray(),
            TrackToBone = Enumerable.Range(0, reference.Count).ToArray(),
            SourceSampler = time => Expand(Sample(clip, time)), Blend=AnimationBlend.Normal,
            BakedReference=clip.Blend==AnimationBlend.Normal?null:$"{clip.Blend}: matching Havok reference pose" };
    }
}
