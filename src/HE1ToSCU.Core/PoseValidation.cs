using System.Globalization;
using System.Numerics;

namespace HE1ToSCU.Core;

internal static class PoseValidation
{
    public static string GodotChecks(ModelAsset model,IReadOnlyList<AnimationClip> clips)
    {
        string Number(float v)=>v.ToString("G9",CultureInfo.InvariantCulture).Replace('E','e');
        var checks=new List<string>();
        foreach(var clip in clips)
        {
            int[] frames = clip.Name.Equals("sn_boost_loop", StringComparison.OrdinalIgnoreCase)
                ? Enumerable.Range(0, clip.Frames.Length).ToArray()
                : [0,(clip.Frames.Length-1)/2,Math.Max(0,clip.Frames.Length-2)];
            foreach(int frame in frames.Distinct())
            {
                var local=model.Bones.Select(b=>Spatial.Convert(b.Rest)).ToArray();
                for(int t=0;t<clip.TrackToBone.Length;t++)local[clip.TrackToBone[t]]=Spatial.Convert(clip.Frames[frame][t].Matrix);
                var globals=new Matrix4x4?[local.Length];
                Matrix4x4 Global(int bone)=>globals[bone]??(globals[bone]=model.Bones[bone].Parent<0?local[bone]:local[bone]*Global(model.Bones[bone].Parent)).Value;
                var xyz=Enumerable.Range(0,local.Length).Select(i=>{var g=Global(i);return $"Vector3({Number(g.M41)}, {Number(g.M42)}, {Number(g.M43)})";});
                string Vec(float x,float y,float z)=>$"Vector3({Number(x)}, {Number(y)}, {Number(z)})";
                var basis=Enumerable.Range(0,local.Length).Select(i=>{var g=Global(i);return $"Basis({Vec(g.M11,g.M12,g.M13)}, {Vec(g.M21,g.M22,g.M23)}, {Vec(g.M31,g.M32,g.M33)})";});
                float time=clip.Frames.Length==1?0:clip.Duration*frame/(clip.Frames.Length-1);
                string name=clip.Name.Replace("\\","\\\\").Replace("\"","\\\"");
                var motion = clip.RootMotion?.Sample(time) ?? new Pose(Vector3.Zero, Quaternion.Identity, Vector3.One);
                var root = Spatial.Convert(motion.Matrix);
                string rootTransform = $"Transform(Basis({Vec(root.M11,root.M12,root.M13)}, {Vec(root.M21,root.M22,root.M23)}, {Vec(root.M31,root.M32,root.M33)}), {Vec(root.M41,root.M42,root.M43)})";
                checks.Add($"[\"{name}\", {Number(time)}, [ {string.Join(", ",xyz)} ], [ {string.Join(", ",basis)} ], {rootTransform}]");
            }
        }
        return "["+string.Join(",\n",checks)+"]";
    }
}
