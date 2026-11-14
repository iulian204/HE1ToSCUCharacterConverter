shader_type spatial;
render_mode ambient_light_disabled, blend_sub, cull_disabled, depth_draw_opaque;

uniform sampler2D diffuse_texture;
uniform int diffuse_uv_set : hint_range(0, 1) = 0;

uniform sampler2D refraction_texture;

uniform sampler2D mask_texture;
uniform int mask_uv_set : hint_range(0, 1) = 0;

uniform sampler2D noise_texture;

uniform vec4 diffuse_color = vec4(1.0);
uniform vec4 specular_color = vec4(1.0);
uniform vec4 power_gloss_level;
uniform vec2 sonic_fresnel_param = vec2(0.3, 1.0);
uniform vec4 falloff_color_near;
uniform vec4 falloff_color_edge;
uniform vec2 falloff_param;
uniform vec4 noise_scroll_scale;
uniform vec2 noise_power;
uniform vec2 emission_power;
uniform vec3 refraction_param;

uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.502;

uniform vec2 gi_uv_scale = vec2(1.0);
uniform vec2 gi_uv_offset = vec2(0.0);

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

varying vec3 edge_emission;
varying float refraction_gain;
varying float animation_time;

float compute_fresnel(float cos_theta)
{
	return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * (1.0 - sonic_fresnel_param.x) * sonic_fresnel_param.y + sonic_fresnel_param.x * sonic_fresnel_param.y;
}

varying vec4 uv_coordinates_0_1;

void vertex()
{
	bse_shadow = compute_shadow((WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz);
	vec2 source_uv1 = UV2;
	
	
	uv_coordinates_0_1 = vec4(UV, source_uv1 * gi_uv_scale + gi_uv_offset);

	vec3 position_world = (WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 normal_world = normalize((WORLD_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 eye_world = normalize(CAMERA_MATRIX[3].xyz - position_world);
	animation_time = TIME;
	float view_cosine = dot(normal_world, eye_world);
	float grazing = 1.0 - clamp(view_cosine, 0.0, 1.0);
	vertex_fresnel = compute_fresnel(view_cosine);
	float edge = min(pow(grazing, falloff_param.y), 1.0);
	edge_emission = mix(falloff_color_near.rgb, falloff_color_edge.rgb, edge) * falloff_param.x * edge;
	float view_depth = -(MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
	float distance_gate = clamp(view_depth - refraction_param.z, 0.0, 1.0);
	float view_gain = pow(abs(view_cosine), refraction_param.y);
	refraction_gain = refraction_param.x * distance_gate * view_gain * view_gain;
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

vec4 get_night_noise(vec2 uv)
{
	return texture(noise_texture, uv + animation_time * noise_scroll_scale.xy) +
		texture(noise_texture, uv + animation_time * noise_scroll_scale.zw);
}

float compute_night_blend(vec4 noise_value, vec2 mask)
{
	return (1.0 - mask.y) * clamp((noise_value.b + noise_value.a) * noise_power.y * refraction_gain, 0.0, 1.0);
}

void fragment()
{
	vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
	vec2 mask = texture(mask_texture, get_material_uv(mask_uv_set)).rg;
	vec2 screen_uv = FRAGCOORD.xy * (vec2(1.0) / VIEWPORT_SIZE);
	vec2 noise_uv = vec2(screen_uv.x, 1.0 - screen_uv.y) - 0.5 * (vec2(1.0) / VIEWPORT_SIZE);
	vec4 noise_value = get_night_noise(noise_uv);
	float refract_weight = compute_night_blend(noise_value, mask);
	vec3 base_color = diffuse.rgb * diffuse_color.rgb;
	ALBEDO = base_color * base_color * (1.0 - refract_weight);
	vec3 world_normal = (CAMERA_MATRIX * vec4(NORMAL.xyz, 0.0)).xyz;
	vec3 light_field = diffuse.rgb * get_light_field_color(world_normal);

	vec3 direction = mat3(CAMERA_MATRIX) * VIEW + noise_value.rgb * noise_power.x;
	float hemisphere = direction.z >= 0.0 ? 1.0 : 0.0;
	vec2 projected = vec2(mix(direction.x, -direction.x, hemisphere), -direction.y);
	vec2 refraction_uv = (projected / (1.0 + abs(direction.z)) + 1.0) * vec2(0.25, 0.5);
	refraction_uv.x += 0.5 * hemisphere;
	vec3 refracted = texture(refraction_texture, refraction_uv).rgb;
	EMISSION = mix(light_field * light_field, refracted * refracted, refract_weight);

	vec3 diffuse_emission = diffuse.rgb * mask.x * emission_power.x;
	vec3 rim = edge_emission * (1.0 - mask.y) * emission_power.y;
	EMISSION += diffuse_emission * diffuse_emission + rim * rim;

	float material_alpha = min(diffuse.a * diffuse_color.a, 4.0);

	if (material_alpha < alpha_cutoff)
	{
		discard;
	}

	{
		float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
		float specular_level = specular_gloss * 0.125;
		float cos_theta = dot(NORMAL, VIEW);
		cos_theta = pow(clamp(cos_theta, 0.0, 1.0), specular_gloss);

		vec4 fake_specular = vec4(1.0);
		fake_specular.rgb *= specular_color.rgb;
		fake_specular.rgb *= cos_theta;
		fake_specular.rgb *= specular_level * 0.2;
		fake_specular.rgb *= vertex_fresnel;
		fake_specular.rgb *= (clamp(dot(NORMAL, VIEW), 0.0, 1.0));

		fake_specular.rgb *= fake_specular.rgb;

		EMISSION += fake_specular.rgb * (1.0 - refract_weight);
	}
	ALPHA = 1.0;
}

void light()
{
	float renderer_specular_transfer = 100.0 * (0.4524 * 0.04 - 0.0024);
	vec3 source_light_color = LIGHT_COLOR / (3.14159265 * renderer_specular_transfer);
	float cos_theta = dot(LIGHT, NORMAL);
	float diffuse_cos_theta = clamp(cos_theta, 0.0, 1.0);
	float shadow = bse_shadow * bse_shadow;
	DIFFUSE_LIGHT += ALBEDO * diffuse_cos_theta * diffuse_cos_theta * LIGHT_COLOR * ATTENUATION * shadow;
	float specular_gloss = clamp(exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
	vec3 halfway_direction = normalize(LIGHT + VIEW);
	cos_theta = pow(clamp(dot(halfway_direction, NORMAL), 0.0, 1.0), specular_gloss);
	vec3 specular = specular_color.rgb * specular_gloss * 0.125;
	specular.rgb *= cos_theta;
	specular.rgb *= vertex_fresnel * diffuse_cos_theta;
	specular.rgb *= specular.rgb;
	vec2 screen_uv = FRAGCOORD.xy * (vec2(1.0) / VIEWPORT_SIZE);
	vec2 noise_uv = vec2(screen_uv.x, 1.0 - screen_uv.y) - 0.5 * (vec2(1.0) / VIEWPORT_SIZE);
	float reflection_mix = compute_night_blend(get_night_noise(noise_uv), texture(mask_texture, get_material_uv(mask_uv_set)).rg);
	SPECULAR_LIGHT += (specular * source_light_color * ATTENUATION * shadow * (1.0 - reflection_mix)) * 100.0;
}
