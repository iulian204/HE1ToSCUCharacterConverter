namespace HE1ToSCU.Core;

internal static class PrrRuntimeValidation
{
    public const string Functions = """
        var prr_expected = {}
        var prr_readbacks = []
        var prr_stage = "text"
        func load_prr_expectations(stage):
            prr_stage = stage
            var file = File.new()
            if not file.file_exists("res://__prr-expected.json"):
                return
            if file.open("res://__prr-expected.json", File.READ) != OK:
                fail("cannot read independent PRR expectations")
                return
            prr_expected = parse_json(file.get_as_text())
            file.close()

        func save_prr_readbacks():
            if prr_expected.empty():
                return
            var file = File.new()
            if file.open("res://__prr-readback-" + prr_stage + ".json", File.WRITE) == OK:
                file.store_string(JSON.print(prr_readbacks))
                file.close()
            else:
                printerr("ERROR: PRR independent readback report could not be written")

        func prr_kind(value):
            match typeof(value):
                TYPE_BOOL: return "bool"
                TYPE_INT: return "int"
                TYPE_REAL: return "float"
                TYPE_VECTOR2: return "Vector2"
                TYPE_VECTOR3: return "Vector3"
                TYPE_PLANE: return "Plane"
                TYPE_COLOR: return "Color"
                TYPE_TRANSFORM2D: return "Transform2D"
                TYPE_BASIS: return "Basis"
                TYPE_TRANSFORM: return "Transform"
            if value is Texture:
                return "Texture"
            return "unsupported/nil"

        func prr_components(value):
            match typeof(value):
                TYPE_BOOL: return [1 if value else 0]
                TYPE_INT, TYPE_REAL: return [value]
                TYPE_VECTOR2: return [value.x, value.y]
                TYPE_VECTOR3: return [value.x, value.y, value.z]
                TYPE_PLANE: return [value.normal.x, value.normal.y, value.normal.z, value.d]
                TYPE_COLOR: return [value.r, value.g, value.b, value.a]
                TYPE_TRANSFORM2D: return [value.x.x, value.y.x, value.x.y, value.y.y]
                TYPE_BASIS: return [value.x.x,value.y.x,value.z.x,value.x.y,value.y.y,value.z.y,value.x.z,value.y.z,value.z.z]
                TYPE_TRANSFORM:
                    var b = value.basis
                    return [b.x.x,b.y.x,b.z.x,value.origin.x,b.x.y,b.y.y,b.z.y,value.origin.y,b.x.z,b.y.z,b.z.z,value.origin.z,0,0,0,1]
            return []

        func prr_record(material, name, kind, actual, ok, reason):
            prr_readbacks.append({"material":material.resource_name,"name":name,"kind":kind,
                "value":actual,"status":"verified" if ok else "failed","reason":reason})
            if not ok:
                save_prr_readbacks()
                fail("PRR " + prr_stage + " " + material.resource_name + "/" + name + ": " + reason)
            return ok

        func check_prr_material(material):
            if prr_expected.empty() or not (material is ShaderMaterial):
                return true
            if not prr_expected.has(material.resource_name):
                return prr_record(material,"(material)","missing",[],false,"No independently derived expectation set")
            var expected = prr_expected[material.resource_name]
            var properties = {}
            for property in material.get_property_list():
                if property.name.begins_with("shader_param/"):
                    properties[property.name.substr(13, property.name.length()-13)] = property.type
            var types = {"bool":TYPE_BOOL,"int":TYPE_INT,"float":TYPE_REAL,"Vector2":TYPE_VECTOR2,
                "Vector3":TYPE_VECTOR3,"Plane":TYPE_PLANE,"Color":TYPE_COLOR,
                "Transform2D":TYPE_TRANSFORM2D,"Basis":TYPE_BASIS,"Transform":TYPE_TRANSFORM}
            for entry in expected["values"]:
                var value = material.get_shader_param(entry.name)
                var kind = prr_kind(value)
                var actual = prr_components(value)
                var ok = properties.has(entry.name) and properties[entry.name] == types[entry.kind] and kind == entry.kind and actual.size() == entry.value.size()
                if ok:
                    for i in range(actual.size()):
                        if entry.kind == "int":
                            if actual[i] != int(entry.exactInteger):
                                ok = false
                        elif entry.kind == "bool":
                            if actual[i] != entry.value[i]:
                                ok = false
                        elif is_nan(float(actual[i])) or is_inf(float(actual[i])) or abs(float(actual[i])-float(entry.value[i])) > 0.000001 + 0.000002*abs(float(entry.value[i])):
                            ok = false
                if not prr_record(material,entry.name,kind,actual,ok,"" if ok else "Declared type, Variant type, or value differs from source-derived expectation"):
                    return false
            for entry in expected["textures"]:
                var value = material.get_shader_param(entry.name)
                var actual = []
                var ok = value is Texture
                if ok:
                    var image = value.get_data()
                    ok = image != null
                    if ok:
                        actual = [image.get_width(),image.get_height(),value.flags]
                        var file = File.new()
                        ok = file.open("res://" + entry.file,File.READ) == OK
                        if ok:
                            var pixels = file.get_buffer(file.get_len())
                            file.close()
                            ok = actual[0] == entry.width and actual[1] == entry.height and actual[2] == entry.flags and image.get_data() == pixels
                if not prr_record(material,entry.name,prr_kind(value),actual,ok,"" if ok else "Sampler, flags, dimensions or raw source-derived mip pixels differ"):
                    return false
            return true
        """;
}
