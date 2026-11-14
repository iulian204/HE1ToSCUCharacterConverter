namespace HE1ToSCU.Core;

internal static class MaterialResourceValidation
{
    // Snapshots use values and image bytes, never filesystem paths. A second engine process checks the binary output.
    public const string Functions = PrrRuntimeValidation.Functions + "\n" + """
        var material_snapshots = {}
        func material_snapshot(material):
            if material is SpatialMaterial:
                return ["SpatialMaterial", material.resource_name]
            if not (material is ShaderMaterial) or material.shader == null:
                return []
            var values = []
            var names = []
            for property in material.get_property_list():
                if property.name.begins_with("shader_param/"):
                    names.append(property.name)
            names.sort()
            for name in names:
                var value = material.get(name)
                if value is Texture:
                    var image = value.get_data()
                    if image == null:
                        return []
                    value = ["Texture", image.get_width(), image.get_height(), image.get_format(),
                        value.flags, image.get_data()]
                values.append([name, value])
            return ["ShaderMaterial", material.resource_name, material.shader.code, values]

        func check_material(material, key):
            if not check_prr_material(material):
                return false
            var snapshot = material_snapshot(material)
            if snapshot.empty():
                fail("invalid ShaderMaterial or sampler: " + key)
                return false
            if not material_snapshots.has(key):
                fail("missing material snapshot: " + key)
                return false
            if var2bytes(snapshot) != var2bytes(material_snapshots[key]):
                fail("binary material/shader/parameter/texture mismatch: " + key)
                return false
            return true

        func clear_material_paths(material):
            material.resource_path = ""
            if material is ShaderMaterial:
                material.shader.resource_path = ""
                for property in material.get_property_list():
                    if property.name.begins_with("shader_param/"):
                        var texture = material.get(property.name)
                        if texture is Texture:
                            texture.resource_path = ""
            elif material is SpatialMaterial:
                for slot in range(SpatialMaterial.TEXTURE_MAX):
                    var texture = material.get_texture(slot)
                    if texture != null:
                        texture.resource_path = ""
        """;
}
