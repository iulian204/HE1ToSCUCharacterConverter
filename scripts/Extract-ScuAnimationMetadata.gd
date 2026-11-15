extends SceneTree

const ALIASES = {
    "hips":["Hips","Pelvis","Hip"], "head":["Head"],
    "handL":["Wrist_L","Hand_L","LeftHand","LHand"], "handR":["Wrist_R","Hand_R","RightHand","RHand"],
    "footL":["Foot_L","LeftFoot","LFoot"], "footR":["Foot_R","RightFoot","RFoot"]}
const GROUPS = ["hips","head","handL","handR","footL","footR"]

func from_rows(a):
    return Transform(Basis(Vector3(a[0],a[3],a[6]),Vector3(a[1],a[4],a[7]),Vector3(a[2],a[5],a[8])),Vector3(a[9],a[10],a[11]))

func global_pose(index, locals, parents, cache):
    if cache.has(index): return cache[index]
    var result = locals[index]
    if parents[index] >= 0: result = global_pose(parents[index],locals,parents,cache) * result
    cache[index] = result
    return result

func vector(v): return [v.x,v.y,v.z]

func _init():
    var f=File.new()
    f.open("res://request.json",File.READ)
    var request=parse_json(f.get_as_text())
    f.close()
    var result={"profiles":{},"boneAliases":ALIASES,"engine":Engine.get_version_info()}
    for profile in request.profiles:
        var bones=profile.primaryBones
        var names={}
        var parents=[]
        var rest=[]
        for i in range(bones.size()):
            names[bones[i].name]=i
            parents.append(int(bones[i].parent))
            rest.append(from_rows(bones[i].rest))
        var indices=[]
        var rest_cache={}
        var mask=[]
        for group in GROUPS:
            var index=-1
            for alias in ALIASES[group]:
                if names.has(alias):
                    index=names[alias]
                    break
            indices.append(index)
            mask.append(index>=0)
        var hip=global_pose(indices[0],rest,parents,rest_cache).origin
        var scale=1.0
        if indices[1]>=0 and indices[4]>=0 and indices[5]>=0:
            var head=global_pose(indices[1],rest,parents,rest_cache).origin
            var feet=(global_pose(indices[4],rest,parents,rest_cache).origin+global_pose(indices[5],rest,parents,rest_cache).origin)*0.5
            scale=max(head.distance_to(feet),0.000001)
        var animations=[]
        for entry in profile.animationFiles:
            var a=load(entry.diskPath)
            if not (a is Animation):
                animations.append({"path":entry.path,"name":entry.path.get_file().get_basename(),"eligible":false,"error":"Native Animation load failed"})
                continue
            var tracks=[]
            var matched={}
            var root_track=-1
            var key_times={}
            var maximum_keys=0
            for i in range(a.get_track_count()):
                var path=str(a.track_get_path(i))
                var count=a.track_get_key_count(i)
                tracks.append({"path":path,"type":a.track_get_type(i),"keyCount":count,"interpolation":a.track_get_interpolation_type(i),"enabled":a.track_is_enabled(i)})
                maximum_keys=max(maximum_keys,count)
                for k in range(count): key_times[a.track_get_key_time(i,k)]=true
                if a.track_get_type(i)!=Animation.TYPE_TRANSFORM or count==0 or not a.track_is_enabled(i): continue
                if path==".": root_track=i
                var pieces=path.split(":")
                var bone=pieces[1] if pieces.size()>1 else ""
                if path.begins_with(profile.skeletonNodeName+":") and names.has(bone): matched[names[bone]]=i
            var samples=[]
            var root_samples=[]
            for frame in range(17):
                var time=a.length*float(frame)/16.0
                var locals=rest.duplicate()
                for index in matched:
                    var key=a.transform_track_interpolate(matched[index],time)
                    locals[index]=rest[index]*Transform(Basis(key[1]).scaled(key[2]),key[0])
                var cache={}
                var hips=global_pose(indices[0],locals,parents,cache).origin
                var row=[]
                for g in range(indices.size()):
                    var position=Vector3()
                    if indices[g]>=0:
                        position=(global_pose(indices[g],locals,parents,cache).origin-(hip if g==0 else hips))/scale
                    row+=vector(position)
                samples.append(row)
                var travel=global_pose(0,locals,parents,cache).origin
                if root_track>=0:
                    var key=a.transform_track_interpolate(root_track,time)
                    travel=Transform(Basis(key[1]).scaled(key[2]),key[0]).xform(travel)
                root_samples.append(vector(travel/scale))
            var times=key_times.keys()
            times.sort()
            var deltas=[]
            for i in range(1,times.size()):
                if times[i]-times[i-1]>0.00001: deltas.append(times[i]-times[i-1])
            deltas.sort()
            var motion={"mask":mask,"samples":samples,"rootSamples":root_samples,"normalization":"rest head-to-feet distance; body points relative to animated hips; hips relative to rest hips","rootEvidence":"asset root-bone and optional object track; excludes gameplay-driven movement"}
            animations.append({"path":entry.path,"name":entry.path.get_file().get_basename(),"resourceName":a.resource_name,
                "duration":a.length,"loop":a.loop,"editorStep":a.step,"frameCount":null,"maximumTrackKeys":maximum_keys,
                "uniqueKeyTimes":times.size(),"medianKeyInterval":deltas[int(deltas.size()/2)] if deltas.size()>0 else null,
                "tracks":tracks,"eligible":matched.size()>0,"eligibilityReason":"Playable skeleton transform tracks" if matched.size()>0 else "No transform tracks for the playable skeleton; separate effect/resource rig",
                "motion":motion if matched.size()>0 else null})
        result.profiles[profile.id]=animations
        print("SCU_PROFILE ",profile.id," animations=",animations.size())
    f.open("res://decoded.json",File.WRITE)
    f.store_string(to_json(result))
    f.close()
    print("SCU_INVENTORY_EXTRACTED")
    quit()
