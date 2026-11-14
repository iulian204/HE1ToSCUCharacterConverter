shader_type spatial;
render_mode unshaded, ambient_light_disabled, blend_sub, cull_back, depth_draw_opaque;

uniform sampler2D diffuse_texture;
uniform int diffuse_uv_set : hint_range(0, 1) = 0;

uniform vec4 diffuse_color = vec4(1.0);
uniform float material_opacity = 1.0;

uniform vec2 uv0_offset;
uniform vec2 uv1_offset;

varying vec4 uv_coordinates_0_1;

void vertex()
{
	vec2 second_uv = UV2;
	
	
	uv_coordinates_0_1 = vec4(UV, second_uv) + vec4(uv0_offset, uv1_offset);
	
}

vec2 get_material_uv(int index)
{
	if (index == 0)
	{
		return uv_coordinates_0_1.xy;
	}
	if (index == 1)
	{
		return uv_coordinates_0_1.zw;
	}
	return vec2(0.0);
}

void fragment()
{
	vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
	vec3 radiance = min(diffuse.rgb * diffuse_color.rgb, vec3(4.0));
	ALBEDO = radiance * radiance;

	float material_alpha = min(diffuse.a * diffuse_color.a * material_opacity, 4.0);

	ALPHA = material_alpha;
}
