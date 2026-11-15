using System.Numerics;
using System.Text;

namespace HE1ToSCU.Core;

internal static class PoseValidation
{
    // Independent oracle: rigid matrices without inherited scale; accumulate scale separately.
    public static Matrix4x4[] SourceGlobals(ModelAsset source, AnimationClip clip, float time,bool includeScale=true)
    {
        var locals=source.Bones.Select(b=>Pose.FromMatrix(b.Rest)).ToArray();
        var sample=AnimationSampling.Sample(clip,time);
        for(int t=0;t<sample.Length;t++) locals[clip.TrackToBone[t]]=sample[t];
        var rigid=new Matrix4x4?[locals.Length];var scales=new Vector3?[locals.Length];
        Matrix4x4 Rigid(int i) => rigid[i] ?? (rigid[i]=source.Bones[i].Parent<0 ?
            Matrix4x4.CreateFromQuaternion(locals[i].Rotation)*Matrix4x4.CreateTranslation(locals[i].Translation) :
            Matrix4x4.CreateFromQuaternion(locals[i].Rotation)*Matrix4x4.CreateTranslation(locals[i].Translation)*Rigid(source.Bones[i].Parent)).Value;
        Vector3 Scale(int i) => scales[i] ?? (scales[i]=source.Bones[i].Parent<0 ? locals[i].Scale : locals[i].Scale*Scale(source.Bones[i].Parent)).Value;
        return Enumerable.Range(0,locals.Length).Select(i=>includeScale?Matrix4x4.CreateScale(Scale(i))*Rigid(i):Rigid(i)).ToArray();
    }

    public static int Write(string path,ModelAsset target,ModelAsset source,IReadOnlyList<AnimationClip> clips,IReadOnlyList<AnimationClip> originals,CancellationToken token)
    {
        using var writer=new BinaryWriter(File.Create(path),Encoding.UTF8);
        // Reused target resources are key-for-key checked against this representative in Acorn.
        var seen=new HashSet<string>(StringComparer.Ordinal);
        var indices=Enumerable.Range(0,clips.Count).Where(i=>clips[i].ExportIdentity==null||seen.Add(clips[i].Name)).ToArray();
        writer.Write(target.Bones.Count);writer.Write(indices.Length);int checks=0;
        void Transform(Matrix4x4 m) {
            writer.Write(m.M11);writer.Write(m.M12);writer.Write(m.M13);
            writer.Write(m.M21);writer.Write(m.M22);writer.Write(m.M23);
            writer.Write(m.M31);writer.Write(m.M32);writer.Write(m.M33);
            writer.Write(m.M41);writer.Write(m.M42);writer.Write(m.M43);
        }
        foreach(int c in indices) {
            var clip=clips[c];var original=originals[c];
            var times=Enumerable.Range(0,original.Frames.Length).Select(i=>AnimationSampling.Time(original,i))
                .Concat(Enumerable.Range(0,clip.Frames.Length).Select(i=>AnimationSampling.Time(clip,i))).Distinct().Order().ToArray();
            var cases=times.Select(t=>(Time:t,Advance:0f,Loop:false,Expected:t)).ToList();
            for(int i=1;i<times.Length;i++) cases.Add(((times[i-1]+times[i])*.5f,0,false,(times[i-1]+times[i])*.5f));
            if(clip.Name.Contains("loop",StringComparison.OrdinalIgnoreCase)) {
                float step=Math.Min(clip.Duration*.1f,1f/120);
                cases.Add((clip.Duration-step,step*2,true,step));
            }
            byte[] name=Encoding.UTF8.GetBytes(ExportNames.AnimationName(clip));writer.Write(name.Length);writer.Write(name);writer.Write(cases.Count);
            foreach(var entry in cases) {
                token.ThrowIfCancellationRequested();writer.Write(entry.Time);writer.Write(entry.Advance);writer.Write(entry.Loop?1:0);
                Transform(Spatial.Convert((original.RootMotion?.Sample(entry.Expected) ?? new Pose(Vector3.Zero,Quaternion.Identity,Vector3.One)).Matrix));
                var globals=SourceGlobals(source,original,entry.Expected);
                var rigidGlobals=SourceGlobals(source,original,entry.Expected,false);
                for(int i=0;i<target.Bones.Count;i++) {
                    Matrix4x4 matrix;
                    if(i<source.Bones.Count) matrix=globals[i];
                    else {
                        matrix=rigidGlobals[target.RigControls![i]];
                    }
                    Transform(Spatial.Convert(matrix));
                }
                checks++;
            }
        }
        return checks;
    }

    public const string GodotFunctions = """
        func read_transform(file):
            var x = Vector3(file.get_float(), file.get_float(), file.get_float())
            var y = Vector3(file.get_float(), file.get_float(), file.get_float())
            var z = Vector3(file.get_float(), file.get_float(), file.get_float())
            var origin = Vector3(file.get_float(), file.get_float(), file.get_float())
            return Transform(Basis(x,y,z), origin)
        func check_poses(instance, skeleton):
            var file = File.new()
            if file.open("res://__poses.bin", File.READ) != OK:
                fail("missing source pose reference")
                return false
            var bones = file.get_32()
            var clips = file.get_32()
            var player = instance.get_node("AnimationPlayer")
            var checks = 0
            var max_position_error = 0.0
            var max_basis_error = 0.0
            for clip_index in range(clips):
                var name = file.get_buffer(file.get_32()).get_string_from_utf8()
                var count = file.get_32()
                var animation = player.get_animation(name)
                var original_loop = animation.loop
                for sample in range(count):
                    var time = file.get_float()
                    var advance = file.get_float()
                    animation.loop = file.get_32() != 0
                    player.play(name)
                    player.seek(time, true)
                    if advance > 0:
                        player.advance(advance)
                    var expected_root = read_transform(file)
                    var actual_root = instance.transform
                    if actual_root.origin.distance_to(expected_root.origin) > 0.02:
                        fail("root translation " + name + " at " + str(time))
                        return false
                    for axis in range(3):
                        if actual_root.basis[axis].distance_to(expected_root.basis[axis]) > 0.003:
                            fail("root heading " + name + " at " + str(time))
                            return false
                    for bone in range(bones):
                        var expected = read_transform(file)
                        var actual = skeleton.get_bone_global_pose(bone)
                        max_position_error = max(max_position_error, actual.origin.distance_to(expected.origin))
                        if is_nan(actual.origin.length()) or is_inf(actual.origin.length()) or actual.origin.distance_to(expected.origin) > 0.02:
                            fail("bone position " + name + " at " + str(time) + " bone=" + skeleton.get_bone_name(bone) + " actual=" + str(actual.origin) + " expected=" + str(expected.origin))
                            return false
                        for axis in range(3):
                            max_basis_error = max(max_basis_error, actual.basis[axis].distance_to(expected.basis[axis]))
                            if is_nan(actual.basis[axis].length()) or actual.basis[axis].distance_to(expected.basis[axis]) > 0.003:
                                fail("bone basis " + name + " at " + str(time) + " bone=" + skeleton.get_bone_name(bone))
                                return false
                    checks += 1
                    if checks % 128 == 0:
                        yield(self, "idle_frame")
                animation.loop = original_loop
            file.close()
            print("SOURCE_POSES_VALIDATED samples=" + str(checks) + " max_position_error=" + str(max_position_error) + " max_basis_error=" + str(max_basis_error))
            return true
        """;
}
