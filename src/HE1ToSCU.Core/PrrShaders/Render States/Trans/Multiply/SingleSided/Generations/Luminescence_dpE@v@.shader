shader_type spatial;
render_mode ambient_light_disabled, blend_mul, cull_back, depth_draw_opaque;

uniform sampler2D diffuse_texture;
uniform int diffuse_uv_set : hint_range(0, 1) = 0;

uniform sampler2D gloss_texture;
uniform int gloss_uv_set : hint_range(0, 1) = 0;

uniform sampler2D emission_texture;
uniform int emission_uv_set : hint_range(0, 1) = 0;

uniform vec4 diffuse_color = vec4(1.0);
uniform vec4 ambient_light_color;
uniform vec4 specular_color = vec4(1.0);
uniform vec4 power_gloss_level;
uniform float material_opacity = 1.0;

uniform vec2 uv0_offset;
uniform vec2 uv1_offset;

uniform sampler2D gi_atlas : hint_white;
uniform vec2 gi_uv_scale = vec2(1.0);
uniform vec2 gi_uv_offset = vec2(0.0);
uniform vec3 gi_rgb_scale = vec3(1.0);
uniform float gi_saturation = 1.0;

uniform float inner_shadow_amount : hint_range(0.0, 1.0, 0.05) = 1.0;
uniform float outer_shadow_amount : hint_range(0.0, 1.0, 0.05) = 0.0;
uniform vec3 inner_min = vec3(-1.0);
uniform vec3 inner_max = vec3(1.0);
uniform sampler2D shadow_texture;
uniform mat4 region_transform;

varying vec2 gi_uv;

varying vec4 uv_coordinates_0_1;

vec3 getq(vec3 p, vec3 b, vec3 bound)
{
	vec3 h = 2.0 * (vec3(1.0) - abs(bound));
	vec3 q = p - b;
	return q / max(h, vec3(0.000001));
}

float sdBox(vec3 p, vec3 bound_min, vec3 bound_max, sampler2D tex)
{
	vec3 imin = clamp(inner_min, -1.0, 0.0);
	vec3 imax = clamp(inner_max, 0.0, 1.0);
	
	vec2 uv = clamp(0.25 * (p.xz + 2.0), 0.0, 1.0);
	float box_mask = 1.0 - texture(tex, uv).r;
	
	p = p - imax - imin;
	vec3 b = imax - imin;
	vec3 q = max(getq(p, b, imax), getq(-p, b, imin));
	
	float box = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
	box = max(box, box_mask);
	
	return box;
}

float compute_shadow(in vec3 p)
{
	vec3 region_axis_length = vec3(length(region_transform[0]), length(region_transform[1]), length(region_transform[2]));
	if (min(region_axis_length.x, min(region_axis_length.y, region_axis_length.z)) == 0.0)
	{
		return 1.0;
	}
	vec3 region_scale = 1.0 / region_axis_length;
	region_scale = 2.0 * region_scale * region_scale;
	p = (p - region_transform[3].xyz) * mat3(region_transform);
	p *= region_scale;

	return 1.0 - mix(inner_shadow_amount, outer_shadow_amount, clamp(sdBox(p, inner_min, inner_max, shadow_texture), 0.0, 1.0));
}

varying float bse_shadow;
varying vec4 vColor;

varying float vertex_fresnel;

float compute_fresnel(float cos_theta)
{
	return pow(1.0 - clamp(cos_theta, 0.0, 1.0), 5.0) * 0.6 + 0.4;
}

void vertex()
{
	bse_shadow = compute_shadow((WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz);
	vec2 source_uv1 = UV2;
	// Extended layout stores the two auxiliary texels per vertex in a float texture.
	
	
	
	gi_uv = source_uv1 * gi_uv_scale + gi_uv_offset;
	uv_coordinates_0_1 = vec4(UV + uv0_offset, gi_uv + uv1_offset);
	gi_uv = uv_coordinates_0_1.zw;
	vColor = COLOR;

	vec3 position_world = (WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 normal_world = normalize((WORLD_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 eye_world = normalize(CAMERA_MATRIX[3].xyz - position_world);
	vertex_fresnel = compute_fresnel(dot(normal_world, eye_world));
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

float compute_direct_shadow()
{
	return bse_shadow * texture(gi_atlas, gi_uv).a;
}

void fragment()
{
	vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));

	float fade = 1.0;
	vec3 base_color = diffuse_color.rgb * mix(vec3(1.0), diffuse.rgb, fade);
	vec3 material_color = vColor.rgb * vec3(1.0);
	ALBEDO = base_color * material_color;
	ALBEDO *= ALBEDO;
	vec3 baked = texture(gi_atlas, gi_uv).rgb;
	baked *= baked;
	float gray = dot(baked, vec3(0.298900008, 0.586600006, 0.114399999));
	vec3 light_field = clamp(mix(vec3(gray), baked, gi_saturation), 0.0, 1.0) * gi_rgb_scale;
	light_field *= base_color * mix(1.0, 1.0, bse_shadow) * material_color;
	vec3 emission = texture(emission_texture, get_material_uv(emission_uv_set)).rgb * ambient_light_color.rgb * fade * vec3(1.0) * vColor.rgb;
	EMISSION = vec3(0.0);
	EMISSION += light_field * light_field;
	EMISSION += emission * emission;

	float material_alpha = min(diffuse.a * diffuse_color.a * material_opacity * 1.0 * vColor.a, 4.0);

	EMISSION += 2.0 * light_field * emission;

	{
		float specular_gloss_map = texture(gloss_texture, get_material_uv(gloss_uv_set)).r;
		float specular_gloss = clamp(specular_gloss_map * power_gloss_level.y * 500.0, 1.0, 1024.0);
		float specular_level = specular_gloss_map * power_gloss_level.z * 5.0;
		float cos_theta = dot(NORMAL, VIEW);
		cos_theta = pow(clamp(cos_theta, 0.0, 1.0), specular_gloss);

		vec4 fake_specular = vec4(vec3(1.0), 1.0);
		fake_specular.rgb *= specular_color.rgb;
		fake_specular.rgb *= cos_theta;
		fake_specular.rgb *= specular_level * 0.8;
		fake_specular.rgb *= vertex_fresnel;
		fake_specular.rgb *= (vColor.rgb * vec3(1.0));

		fake_specular.rgb *= fake_specular.rgb;

		EMISSION += fake_specular.rgb;
	}
	ALPHA = material_alpha;
}

void light()
{
	float renderer_specular_transfer = 100.0 * (0.4524 * 0.04 - 0.0024);
	vec3 source_light_color = LIGHT_COLOR / (3.14159265 * renderer_specular_transfer);
	float cos_theta = dot(LIGHT, NORMAL);
	cos_theta = clamp(cos_theta, 0.0, 1.0);
	cos_theta *= cos_theta;
	float shadow = compute_direct_shadow();
	DIFFUSE_LIGHT += (cos_theta * ALBEDO * LIGHT_COLOR * ATTENUATION * (shadow * shadow));

	float specular_gloss_map = texture(gloss_texture, get_material_uv(gloss_uv_set)).r;
	float specular_gloss = clamp(specular_gloss_map * 500.0 * power_gloss_level.y, 1.0, 1024.0);
	float specular_level = specular_gloss_map * 5.0 * power_gloss_level.z;
	vec3 halfway_direction = normalize(LIGHT + VIEW);
	cos_theta = pow(clamp(dot(halfway_direction, NORMAL), 0.0, 1.0), specular_gloss);
	vec3 specular = vec3(1.0) * specular_color.rgb;
	specular.rgb *= cos_theta;
	specular.rgb *= specular_level;
	specular.rgb *= vertex_fresnel;
	specular.rgb *= vColor.rgb * vec3(1.0);
	specular.rgb *= specular.rgb;
	SPECULAR_LIGHT += (specular.rgb * source_light_color * ATTENUATION * (shadow * shadow)) * 100.0;
}
