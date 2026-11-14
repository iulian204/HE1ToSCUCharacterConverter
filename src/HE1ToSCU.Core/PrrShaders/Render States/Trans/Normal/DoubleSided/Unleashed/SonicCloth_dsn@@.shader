shader_type spatial;
render_mode ambient_light_disabled, blend_mix, cull_disabled, depth_draw_opaque;

uniform sampler2D diffuse_texture;
uniform int diffuse_uv_set : hint_range(0, 1) = 0;

uniform sampler2D specular_texture;
uniform int specular_uv_set : hint_range(0, 1) = 0;

uniform sampler2D normal_texture;
uniform int normal_uv_set : hint_range(0, 1) = 0;

uniform vec4 diffuse_color = vec4(1.0);
uniform vec4 specular_color = vec4(1.0);
uniform vec4 power_gloss_level;
uniform vec3 cloth_transmission;

uniform vec2 uv0_offset;
uniform vec2 uv1_offset;

uniform vec4 sky_color : hint_color;
uniform vec4 ground_color : hint_color;
uniform vec4 ambient_light_color : hint_color;
uniform sampler2D sky_panorama;
uniform float ambient_light_sky_contribution;

uniform float inner_shadow_amount : hint_range(0.0, 1.0, 0.05) = 1.0;
uniform float outer_shadow_amount : hint_range(0.0, 1.0, 0.05) = 0.0;
uniform vec3 inner_min = vec3(-1.0);
uniform vec3 inner_max = vec3(1.0);
uniform sampler2D shadow_texture;
uniform mat4 region_transform;

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

varying float vertex_fresnel;
varying float cloth_view_falloff;

float compute_fresnel(float cos_theta)
{
	return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * 0.963845909 + 0.0361540653;
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

void vertex()
{
	BINORMAL = -BINORMAL;
	bse_shadow = compute_shadow((WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz);
	UV = (0 == 1 ? UV2 : UV) + uv0_offset;

	vec3 position_world = (WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 normal_world = normalize((WORLD_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 eye_world = normalize(CAMERA_MATRIX[3].xyz - position_world);
	float view_cosine = abs(dot(normal_world, eye_world));
	vertex_fresnel = compute_fresnel(view_cosine);
	cloth_view_falloff = 1.0 - clamp(view_cosine, 0.0, 1.0);
	cloth_view_falloff *= cloth_view_falloff;

	vec2 texture_uv1 = UV2;
	
	
	uv_coordinates_0_1 = vec4(UV, texture_uv1 + uv1_offset);
	
}

vec3 get_light_field_color(vec3 normal)
{

    vec4 sky_tex1 = texture(sky_panorama, vec2(0.25, 0.25));
    vec4 sky_tex2 = texture(sky_panorama, vec2(0.75, 0.25));
    vec4 sky_tex3 = texture(sky_panorama, vec2(0.25, 0.75));
    vec4 sky_tex4 = texture(sky_panorama, vec2(0.75, 0.75));
    vec4 sky_tex5 = texture(sky_panorama, vec2(0.5, 0.5));
    vec4 avg_sky_tex = (sky_tex1 + sky_tex2 + sky_tex3 + sky_tex4 + sky_tex5) / 5.0;
    float avg_brightness = dot(avg_sky_tex.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 normalized_sky_tex_rgb = avg_sky_tex.rgb / max(length(avg_sky_tex.rgb), 0.000001);
    vec3 avg_color_with_hue_and_saturation = avg_brightness * normalized_sky_tex_rgb;
    vec4 final_avg_sky_tex = vec4(avg_color_with_hue_and_saturation, avg_sky_tex.a);
    float factor = smoothstep(0.0, 1.0, normal.y * 0.5 + 0.5);
    vec3 final_ambient_color = ambient_light_color.rgb * (1.0 - ambient_light_sky_contribution) + final_avg_sky_tex.rgb * ambient_light_sky_contribution;
    vec3 up_color = mix(sky_color.rgb, final_ambient_color, ambient_light_sky_contribution);
	vec3 down_color = mix(ground_color.rgb, final_ambient_color, ambient_light_sky_contribution);
    float min_brightness = 0.5;
    vec3 initial_color = mix(down_color.rgb, up_color.rgb, factor);
    float brightness = dot(initial_color, vec3(0.2126, 0.7152, 0.0722));

    if (brightness < min_brightness) {
        initial_color *= min_brightness / (brightness + 0.0001);
    }

    return clamp(initial_color,0.0,0.8);
}

vec3 get_rim_light_color(vec3 view, vec3 normal)
{
	return get_light_field_color(reflect(-view, normal));
}

vec3 decode_normal_map(vec4 normal_map)
{
	normal_map.x *= normal_map.w;
	normal_map.xy = normal_map.xy * 2.0 - 1.0;
	normal_map.z = sqrt(abs(1.0 - dot(normal_map.xy, normal_map.xy)));
	return normal_map.xyz;
}

void fragment()
{
	vec4 diffuse_sample = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
	vec4 diffuse = diffuse_sample * diffuse_color;

	float material_alpha = min(diffuse.a, 4.0);

	ALBEDO = diffuse.rgb * diffuse.rgb;

	vec4 normal_map = texture(normal_texture, get_material_uv(normal_uv_set));
	normal_map.xyz = decode_normal_map(normal_map);

	NORMAL = normalize(
		normal_map.x * TANGENT +
		normal_map.y * BINORMAL +
		normal_map.z * NORMAL);

	vec3 world_normal = (CAMERA_MATRIX * vec4(NORMAL.xyz, 0.0)).xyz;
	vec3 world_view = (CAMERA_MATRIX * vec4(VIEW.xyz, 0.0)).xyz;

	vec3 light_field = diffuse_sample.rgb;
	light_field *= get_light_field_color(world_normal);
	EMISSION += light_field * light_field;

	vec3 transmitted_field = get_rim_light_color(world_view, world_normal) *
		(cloth_transmission.x + cloth_transmission.y) * cloth_view_falloff;
	EMISSION += transmitted_field * transmitted_field;

	{
		float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
		float specular_level = specular_gloss * 0.125;
		float cos_theta = dot(NORMAL, VIEW);
		cos_theta = pow(clamp(cos_theta, 0.0, 1.0), specular_gloss);

		vec4 fake_specular = texture(specular_texture, get_material_uv(specular_uv_set));
		fake_specular.rgb *= specular_color.rgb;
		fake_specular.rgb *= cos_theta;
		fake_specular.rgb *= specular_level * 0.2;
		fake_specular.rgb *= vertex_fresnel;
		fake_specular.rgb *= (clamp(dot(NORMAL, VIEW), 0.0, 1.0));

		fake_specular.rgb *= fake_specular.rgb;

		EMISSION += fake_specular.rgb;
	}

	EMISSION += 2.0 * light_field * transmitted_field;
	ALPHA = material_alpha;
}

void light()
{
	float renderer_specular_transfer = 100.0 * (0.4524 * 0.04 - 0.0024);
	vec3 source_light_color = LIGHT_COLOR / (3.14159265 * renderer_specular_transfer);
	float cos_theta = dot(LIGHT, NORMAL);
	float diffuse_cos_theta = clamp(cos_theta, 0.0, 1.0);
	float back_cos_theta = clamp(-cos_theta, 0.0, 1.0);
	float shadow = bse_shadow * bse_shadow;
	float transmission = cloth_transmission.z * back_cos_theta;
	DIFFUSE_LIGHT += ALBEDO * (diffuse_cos_theta * diffuse_cos_theta + transmission * transmission) *
		LIGHT_COLOR * ATTENUATION * shadow;
	vec4 specular = texture(specular_texture, get_material_uv(specular_uv_set));
	float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
	float specular_level = specular_gloss * 0.125;
	vec3 halfway_direction = normalize(LIGHT + VIEW);
	cos_theta = pow(clamp(dot(halfway_direction, NORMAL), 0.0, 1.0), specular_gloss);
	specular.rgb *= specular_color.rgb;
	specular.rgb *= cos_theta;
	specular.rgb *= specular_level;
	specular.rgb *= vertex_fresnel * diffuse_cos_theta;
	specular.rgb *= specular.rgb;
	SPECULAR_LIGHT += (specular.rgb * source_light_color * ATTENUATION * shadow) * 100.0;
}
