using System.Numerics;

namespace HE1ToSCU.Core;

/// <summary>HE1 composes rotation/translation independently of scale. A plain
/// matrix hierarchy adds shear when a scaled parent has a rotated child. Compensation
/// bones carry rigid transforms; skinned original bones carry model-space scale.
/// Separating them supports signed and zero scales without inverse-scale singularities.</summary>
public static class AnimationRig
{
    public static Pose Compose(Pose local, Pose parent) => new(
        parent.Translation + Vector3.Transform(local.Translation, parent.Rotation),
        Quaternion.Normalize(Quaternion.Concatenate(local.Rotation, parent.Rotation)),
        local.Scale * parent.Scale);

    public static Pose[] ModelPose(IReadOnlyList<Bone> bones, IReadOnlyList<Pose> locals)
    {
        var result = new Pose?[bones.Count];
        Pose Get(int i) => result[i] ?? (result[i] = bones[i].Parent < 0 ? locals[i] : Compose(locals[i], Get(bones[i].Parent)));
        return Enumerable.Range(0, bones.Count).Select(Get).ToArray();
    }

    public static (ModelAsset Model, List<AnimationClip> Clips, int Helpers) Prepare(
        ModelAsset model, List<AnimationClip> clips, CancellationToken token)
    {
        // Even uniform parent scale must not multiply HE1 child translations.
        bool compensate = clips.Any(c => c.Frames.Any(frame => frame.Any(p => Vector3.Distance(p.Scale, Vector3.One) > 1e-5f)));

        int count = model.Bones.Count;
        var rests = model.Bones.Select(b => Pose.FromMatrix(b.Rest)).ToArray();
        var bindMatrices = new Matrix4x4?[count];
        Matrix4x4 Bind(int i) => bindMatrices[i] ?? (bindMatrices[i] = model.Bones[i].Parent < 0 ? model.Bones[i].Rest : model.Bones[i].Rest * Bind(model.Bones[i].Parent)).Value;
        var bindPoses = Enumerable.Range(0, count).Select(i => Pose.FromMatrix(Bind(i))).ToArray();
        var bones = model.Bones.ToList();
        var helpers = new Dictionary<int, int>();
        var usedNames = model.Bones.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        var parents=model.Bones.Where(b=>b.Parent>=0).Select(b=>b.Parent).ToHashSet();
        if(compensate) foreach(int i in parents.Order()) {
            string name=model.Bones[i].Name+"_ScaleControl";while(!usedNames.Add(name))name+="_";
            helpers[i]=bones.Count;bones.Add(new Bone(name,-1,Matrix4x4.Identity));
        }
        Matrix4x4 RigidBind(int i) => (bindPoses[i] with {Scale=Vector3.One}).Matrix;
        if(compensate) for(int i=0;i<count;i++) {
            int parent=model.Bones[i].Parent;
            int rigidParent=parent<0?-1:helpers[parent];
            Matrix4x4 parentBind=parent<0?Matrix4x4.Identity:RigidBind(parent);
            if(helpers.TryGetValue(i,out int helper)) {
                bones[helper]=bones[helper] with {Parent=rigidParent,Rest=RigidBind(i)*Spatial.Inverse(parentBind)};
                bones[i]=bones[i] with {Parent=helper,Rest=Bind(i)*Spatial.Inverse(RigidBind(i))};
            } else bones[i]=bones[i] with {Parent=rigidParent,Rest=Bind(i)*Spatial.Inverse(parentBind)};
        }
        Spatial.ValidateSkeleton(bones);
        var converted = new List<AnimationClip>();
        foreach (var clip in clips)
        {
            Pose[] Bake(float time)
            {
                token.ThrowIfCancellationRequested();
                var local = rests.ToArray();
                var sample = AnimationSampling.Sample(clip,time);
                for (int t = 0; t < clip.TrackToBone.Length; t++) local[clip.TrackToBone[t]] = sample[t];
                if (!compensate) return local;
                var world = ModelPose(model.Bones, local);
                var frame = new Pose[bones.Count];
                for (int i = 0; i < count; i++)
                {
                    if(helpers.TryGetValue(i,out int helper)) {
                        frame[helper]=local[i] with {Scale=Vector3.One};
                        frame[i]=new Pose(Vector3.Zero,Quaternion.Identity,world[i].Scale);
                    } else frame[i]=local[i] with {Scale=world[i].Scale};
                }
                return frame;
            }
            var times = new List<float>(); var frames = new List<Pose[]>();
            Pose[] TrackPoses(Pose[] frame) => frame.Select((p,i)=>Pose.Relative(p,bones[i].Rest)).ToArray();
            bool Accurate(float time, Pose[] a, Pose[] b, float weight)
            {
                var expected = Bake(time);
                var expectedWorld = new Matrix4x4?[bones.Count]; var actualWorld = new Matrix4x4?[bones.Count];
                Matrix4x4 Expected(int i) => expectedWorld[i] ?? (expectedWorld[i] = bones[i].Parent<0 ? expected[i].Matrix : expected[i].Matrix*Expected(bones[i].Parent)).Value;
                Matrix4x4 Actual(int i) => actualWorld[i] ?? (actualWorld[i] = bones[i].Parent<0 ? AnimationSampling.Interpolate(a[i],b[i],weight).Matrix*bones[i].Rest : AnimationSampling.Interpolate(a[i],b[i],weight).Matrix*bones[i].Rest*Actual(bones[i].Parent)).Value;
                for(int i=0;i<count;i++) {
                    var x=Actual(i);var y=Expected(i);
                    if(Vector3.Distance(x.Translation,y.Translation)>.0005f ||
                       Vector3.Distance(new(x.M11,x.M12,x.M13),new(y.M11,y.M12,y.M13))>.0008f ||
                       Vector3.Distance(new(x.M21,x.M22,x.M23),new(y.M21,y.M22,y.M23))>.0008f ||
                       Vector3.Distance(new(x.M31,x.M32,x.M33),new(y.M31,y.M32,y.M33))>.0008f) return false;
                }
                return true;
            }
            void Interval(float start, Pose[] a, float end, Pose[] b, int depth)
            {
                // A block quantization seam may jump between adjacent float times.
                // Preserve both sides; there is no representable timestamp between them.
                if(MathF.BitIncrement(start)>=end) {times.Add(end);frames.Add(b);return;}
                var ta=TrackPoses(a);var tb=TrackPoses(b);
                if(new[]{.25f,.5f,.75f}.All(w=>Accurate(start+(end-start)*w,ta,tb,w))) {
                    times.Add(end);frames.Add(b);return;
                }
                if(depth>=10) throw new InvalidDataException($"{clip.Name}: subframe interpolation could not meet the pose error bound at {start:G9}s.");
                float middle=(start+end)*.5f;var m=Bake(middle);
                Interval(start,a,middle,m,depth+1);Interval(middle,m,end,b,depth+1);
            }
            var previous=Bake(0);times.Add(0);frames.Add(previous);
            var sourceTimes=Enumerable.Range(0,clip.Frames.Length).Select(f=>AnimationSampling.Time(clip,f))
                .Concat((clip.Discontinuities??[]).SelectMany(t=>new[]{MathF.BitDecrement(t),t})).Distinct().Order().ToArray();
            for(int f=1;f<sourceTimes.Length;f++) {
                float end=sourceTimes[f];var next=Bake(end);
                Interval(sourceTimes[f-1],previous,end,next,0); previous=next;
            }
            converted.Add(clip with { Frames = frames.ToArray(), Times = times.ToArray(), SourceSampler = null,
                TrackToBone = Enumerable.Range(0, bones.Count).ToArray() });
        }
        return (model with { Bones = bones, RigControls=helpers.ToDictionary(p=>p.Value,p=>p.Key) }, converted, helpers.Count);
    }
}
