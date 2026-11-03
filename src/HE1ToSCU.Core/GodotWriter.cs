using System.Globalization;
using System.Numerics;
using System.Text;

namespace HE1ToSCU.Core;

public static class GodotWriter
{
    private static string F(float f)=>float.IsFinite(f)?(f==0?"0":f.ToString("G9",CultureInfo.InvariantCulture).Replace('E','e')):throw new InvalidDataException("Non-finite Godot resource value.");
    private static string V(Vector3 v)=>$"{F(v.X)}, {F(v.Y)}, {F(v.Z)}";
    private static string Q(Quaternion v)=>$"{F(v.X)}, {F(v.Y)}, {F(v.Z)}, {F(v.W)}";
    private static string Quote(string s)=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";
    // VariantParser's text Transform constructor accepts basis ROWS (unlike GDScript's three axis vectors).
    private static string Transform(Matrix4x4 m)=>$"Transform( {F(m.M11)}, {F(m.M21)}, {F(m.M31)}, {F(m.M12)}, {F(m.M22)}, {F(m.M32)}, {F(m.M13)}, {F(m.M23)}, {F(m.M33)}, {F(m.M41)}, {F(m.M42)}, {F(m.M43)} )";
    private static void Save(string path,StringBuilder text)=>File.WriteAllText(path,text.ToString(),new UTF8Encoding(false));
    public static void Write(ModelAsset model,IReadOnlyList<AnimationClip> clips,string directory,CancellationToken token,Action<string>? log = null)
    {
        ExportNames.Validate(model, clips);
        Directory.CreateDirectory(directory);
        var materials=model.Surfaces.Select(s=>(s.Material,s.Slot)).Distinct().ToArray();
        var materialFiles = new SpatialMaterialWriter().Write(model, directory, log, token);
        var mesh=new StringBuilder($"[gd_resource type=\"ArrayMesh\" load_steps={materials.Length+1} format=2]\n\n");
        for(int i=0;i<materials.Length;i++)
        {
            mesh.AppendLine($"[ext_resource path={Quote(materialFiles[materials[i]])} type=\"Material\" id={i+1}]");
        }
        mesh.AppendLine("[resource]");mesh.AppendLine($"resource_name = {Quote(model.Name)}");
        for(int i=0;i<model.Surfaces.Count;i++)
        {
            token.ThrowIfCancellationRequested();var s=model.Surfaces[i];
            string[] arrays=
            [
                "PoolVector3Array( "+string.Join(", ",s.Vertices.Select(v=>V(v.Position*Spatial.Scale)))+" )",
                "PoolVector3Array( "+string.Join(", ",s.Vertices.Select(v=>V(v.Normal)))+" )",
                "PoolRealArray( "+string.Join(", ",s.Vertices.Select(v=>$"{F(v.Tangent.X)}, {F(v.Tangent.Y)}, {F(v.Tangent.Z)}, {F(v.Tangent.W)}"))+" )",
                "PoolColorArray( "+string.Join(", ",s.Vertices.Select(v=>$"{F(v.Color.X)}, {F(v.Color.Y)}, {F(v.Color.Z)}, {F(v.Color.W)}"))+" )",
                "PoolVector2Array( "+string.Join(", ",s.Vertices.Select(v=>$"{F(v.UV.X)}, {F(v.UV.Y)}"))+" )",
                "PoolVector2Array( "+string.Join(", ",s.Vertices.Select(v=>$"{F(v.UV2.X)}, {F(v.UV2.Y)}"))+" )",
                s.Vertices.Any(v=>v.Weights!=Vector4.Zero)?"PoolIntArray( "+string.Join(", ",s.Vertices.SelectMany(v=>v.Bones))+" )":"null",
                s.Vertices.Any(v=>v.Weights!=Vector4.Zero)?"PoolRealArray( "+string.Join(", ",s.Vertices.Select(v=>$"{F(v.Weights.X)}, {F(v.Weights.Y)}, {F(v.Weights.Z)}, {F(v.Weights.W)}"))+" )":"null",
                "PoolIntArray( "+string.Join(", ",s.Indices)+" )"
            ];
            int material=Array.IndexOf(materials,(s.Material,s.Slot))+1;
            mesh.AppendLine($"surfaces/{i} = {{\n\"primitive\": 4,\n\"arrays\": [ {string.Join(",\n",arrays)} ],\n\"morph_arrays\": [],\n\"material\": ExtResource( {material} ),\n\"name\": {Quote(s.Material)}\n}}");
        }
        Save(Path.Combine(directory,ExportNames.MeshText(model)),mesh);
        var scene=new StringBuilder($"[gd_scene load_steps={clips.Count+2} format=2]\n\n[ext_resource path={Quote(ExportNames.MeshText(model))} type=\"ArrayMesh\" id=1]\n");
        for(int i=0;i<clips.Count;i++)
        {
            string file=ExportNames.AnimationText(clips[i]);
            scene.AppendLine($"[ext_resource path={Quote(file)} type=\"Animation\" id={i+2}]");
            WriteAnimation(clips[i],model.Bones,Path.Combine(directory,file),token);
        }
        scene.AppendLine($"\n[node name={Quote(model.Name)} type=\"Spatial\"]\n\n[node name=\"Skeleton\" type=\"Skeleton\" parent=\".\"]");
        // Declare all bones before assigning parents; compensation parents may have higher indexes.
        for(int i=0;i<model.Bones.Count;i++) scene.AppendLine($"bones/{i}/name = {Quote(model.Bones[i].Name)}");
        for(int i=0;i<model.Bones.Count;i++)
        {
            var bone=model.Bones[i];
            scene.AppendLine($"bones/{i}/parent = {bone.Parent}\nbones/{i}/rest = {Transform(Spatial.Convert(bone.Rest))}\nbones/{i}/enabled = true\nbones/{i}/bound_children = []");
        }
        scene.AppendLine("\n[node name=\"Mesh\" type=\"MeshInstance\" parent=\"Skeleton\"]\nmesh = ExtResource( 1 )\nskeleton = NodePath(\"..\")\ncast_shadow = 0\ncast_character_shadow = true");
        if(clips.Count>0)
        {
            scene.AppendLine("\n[node name=\"AnimationPlayer\" type=\"AnimationPlayer\" parent=\".\"]\nroot_node = NodePath(\"..\")");
            for(int i=0;i<clips.Count;i++)scene.AppendLine($"anims/{clips[i].Name} = ExtResource( {i+2} )");
        }
        Save(Path.Combine(directory,ExportNames.SceneText(model)),scene);
    }
    private static void WriteAnimation(AnimationClip clip,IReadOnlyList<Bone> bones,string path,CancellationToken token)
    {
        var text=new StringBuilder("[gd_resource type=\"Animation\" format=2]\n\n[resource]\n");
        text.AppendLine($"resource_name = {Quote(clip.Name)}\nlength = {F(clip.Duration)}\nloop = {(clip.Name.Contains("loop",StringComparison.OrdinalIgnoreCase)?"true":"false")}");
        for(int t=0;t<clip.TrackToBone.Length;t++)
        {
            token.ThrowIfCancellationRequested();int bone=clip.TrackToBone[t];
            if(bone<0||bone>=bones.Count)throw new InvalidDataException("Animation refers to a missing export bone.");
            text.AppendLine($"tracks/{t}/type = \"transform\"\ntracks/{t}/path = NodePath({Quote("Skeleton:"+bones[bone].Name)})\ntracks/{t}/interp = 1\ntracks/{t}/loop_wrap = true\ntracks/{t}/imported = true\ntracks/{t}/enabled = true");
            text.Append($"tracks/{t}/keys = PoolRealArray( ");
            var inverseRest=Spatial.Inverse(Spatial.Convert(bones[bone].Rest));Quaternion previous=Quaternion.Identity;
            for(int f=0;f<clip.Frames.Length;f++)
            {
                // Godot 3 Skeleton applies rest * pose (column vectors). System.Numerics uses row vectors.
                var pose=Pose.FromMatrix(Spatial.Convert(clip.Frames[f][t].Matrix)*inverseRest);
                var q=pose.Rotation;if(f>0&&Quaternion.Dot(q,previous)<0)q=-q;previous=q;
                float time=clip.Frames.Length==1?0:clip.Duration*f/(clip.Frames.Length-1);
                if(f>0)text.Append(", ");
                text.Append($"{F(time)}, 1, {V(pose.Translation)}, {Q(q)}, {V(pose.Scale)}");
            }
            text.AppendLine(" )");
        }
        // The model Spatial carries extracted motion independently of the skeleton tracks.
        // An identity track on in-place clips also clears motion when switching animations.
        int rootTrack = clip.TrackToBone.Length;
        text.AppendLine($"tracks/{rootTrack}/type = \"transform\"\ntracks/{rootTrack}/path = NodePath(\".\")\ntracks/{rootTrack}/interp = 1\ntracks/{rootTrack}/loop_wrap = true\ntracks/{rootTrack}/imported = true\ntracks/{rootTrack}/enabled = true");
        text.Append($"tracks/{rootTrack}/keys = PoolRealArray( ");
        IEnumerable<(float Time, Pose Pose)> rootKeys = clip.RootMotion?.Keys() ?? [(0, new Pose(Vector3.Zero, Quaternion.Identity, Vector3.One))];
        bool first = true; Quaternion previousRoot = Quaternion.Identity;
        foreach (var (time, pose) in rootKeys)
        {
            token.ThrowIfCancellationRequested();
            var rotation = pose.Rotation;
            if (!first && Quaternion.Dot(previousRoot, rotation) < 0) rotation = -rotation;
            if (!first) text.Append(", ");
            text.Append($"{F(time)}, 1, {V(pose.Translation * Spatial.Scale)}, {Q(rotation)}, {V(pose.Scale)}");
            first = false; previousRoot = rotation;
        }
        text.AppendLine(" )");
        Save(path,text);
    }
}
