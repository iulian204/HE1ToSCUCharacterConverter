shader_type spatial;
render_mode ambient_light_disabled, blend_mul, cull_back, depth_draw_opaque;

uniform sampler2D diffuse_texture;
uniform int diffuse_uv_set : hint_range(0, 1) = 0;

uniform sampler2D gloss_texture;
uniform int gloss_uv_set : hint_range(0, 1) = 0;

uniform sampler2D normal_texture;
uniform int normal_uv_set : hint_range(0, 1) = 0;

uniform sampler2D environment_texture;

uniform vec4 diffuse_color = vec4(1.0);
uniform vec4 specular_color = vec4(1.0);
uniform vec4 power_gloss_level;

uniform vec3 eye_highlight_position;
uniform vec4 eye_highlight_color;
uniform float luminance_range;

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

varying vec4 vColor;

varying float vertex_fresnel;

float compute_fresnel(float cos_theta)
{
	return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * 0.7 + 0.3;
}

varying vec3 eye_light_view;
varying vec3 eye_tangent_view;
varying vec3 eye_binormal_view;

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

	vColor = COLOR;

	eye_light_view = normalize(mat3(MODELVIEW_MATRIX) * (VERTEX - eye_highlight_position));
	eye_tangent_view = normalize(mat3(MODELVIEW_MATRIX) * TANGENT);
	eye_binormal_view = normalize(mat3(MODELVIEW_MATRIX) * BINORMAL);
	vec3 position_world = (WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 normal_world = normalize((WORLD_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 eye_world = normalize(CAMERA_MATRIX[3].xyz - position_world);
	vertex_fresnel = compute_fresnel(dot(normal_world, eye_world));

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

// RG carries the authored eye highlight normal. BA carries the second
// surface lobe. Neither pair multiplies its X component by alpha.
vec3 decode_eye_normal_map(vec2 channels, vec3 normal, vec3 tangent_view, vec3 binormal_view)
{
	vec2 xy = channels * 2.0 - 1.0;
	float z = sqrt(abs(1.0 - dot(xy, xy)));
	return tangent_view * xy.x + binormal_view * xy.y + normal * z;
}

vec2 compute_eye_specular_gloss(vec2 channels)
{
	return clamp(channels * (500.0 * power_gloss_level.y), vec2(1.0), vec2(1024.0));
}

void fragment()
{
	vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
	vec2 specular_gloss_map = texture(gloss_texture, get_material_uv(gloss_uv_set)).rg;
	vec2 specular_gloss = compute_eye_specular_gloss(specular_gloss_map);
	vec3 highlight_normal = decode_eye_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)).rg, NORMAL, normalize(TANGENT), normalize(BINORMAL));
	vec3 highlight_halfway_direction = normalize(normalize(eye_light_view) + VIEW);
	float highlight = pow(clamp(dot(highlight_normal, highlight_halfway_direction), 0.0, 1.0), specular_gloss.x);
	vec3 eye_highlight = eye_highlight_color.rgb * (5.0 * power_gloss_level.z * specular_gloss_map.x * highlight);
	eye_highlight *= vertex_fresnel;
	EMISSION += (eye_highlight * eye_highlight) * vColor.rgb * vec3(1.0);

	vec4 environment_map = texture(environment_texture, NORMAL.xy * vec2(0.5, -0.5) + 0.5);
	environment_map.rgb += environment_map.rgb * environment_map.a * luminance_range;
	float reflection_shadow = mix(1.0 * 1.0,
		1.0, bse_shadow);
	vec3 reflection = environment_map.rgb * reflection_shadow * vertex_fresnel * vColor.a;
	EMISSION += (reflection * reflection) * vColor.rgb * vec3(1.0);
	vec3 base_color = diffuse.rgb * diffuse_color.rgb;
	ALBEDO = vColor.rgb * base_color * base_color * vec3(1.0);
	vec3 world_normal = mat3(CAMERA_MATRIX) * NORMAL;
	vec3 light_field = base_color * get_light_field_color(world_normal);
	EMISSION += light_field * light_field * vColor.rgb * vec3(1.0);

	

	{
		float outer_specular_gloss = specular_gloss.x;
		float outer_specular_level = 5.0 * power_gloss_level.z * specular_gloss_map.x;
		float outer_cos_theta = dot(NORMAL, VIEW);
		outer_cos_theta = pow(clamp(outer_cos_theta, 0.0, 1.0), outer_specular_gloss);

		vec4 outer_fake_specular = vec4(1.0);
		outer_fake_specular.rgb *= specular_color.rgb;
		outer_fake_specular.rgb *= outer_cos_theta;
		outer_fake_specular.rgb *= outer_specular_level * 0.8;
		outer_fake_specular.rgb *= vertex_fresnel;

		outer_fake_specular.rgb *= outer_fake_specular.rgb;

		EMISSION += outer_fake_specular.rgb * (vColor.rgb * vec3(1.0));
	}

	{
		vec3 iris_normal = decode_eye_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)).ba, NORMAL, normalize(TANGENT), normalize(BINORMAL));
		float iris_specular_gloss = specular_gloss.y;
		float iris_specular_level = 5.0 * power_gloss_level.z * specular_gloss_map.y;
		float iris_cos_theta = dot(iris_normal, VIEW);
		iris_cos_theta = pow(clamp(iris_cos_theta, 0.0, 1.0), iris_specular_gloss);

		vec4 iris_fake_specular = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
		iris_fake_specular.rgb *= specular_color.rgb;
		iris_fake_specular.rgb *= iris_cos_theta;
		iris_fake_specular.rgb *= iris_specular_level * 0.8;
		iris_fake_specular.rgb *= compute_fresnel(dot(iris_normal, VIEW));

		iris_fake_specular.rgb *= iris_fake_specular.rgb;

		EMISSION += iris_fake_specular.rgb * (vColor.rgb * vec3(1.0));
	}

	EMISSION += 2.0 * eye_highlight * reflection * vColor.rgb * vec3(1.0) +
		2.0 * eye_highlight * light_field * vColor.rgb * vec3(1.0) +
		2.0 * reflection * light_field * vColor.rgb * vec3(1.0);
	ALPHA = 1.0;
}

void light()
{
	float renderer_specular_transfer = 100.0 * (0.4524 * 0.04 - 0.0024);
	vec3 source_light_color = LIGHT_COLOR / (3.14159265 * renderer_specular_transfer);
	float lambert = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float shadow = bse_shadow * bse_shadow;
	DIFFUSE_LIGHT += ALBEDO * (lambert * lambert) * LIGHT_COLOR * ATTENUATION * shadow;
	vec2 specular_gloss_map = texture(gloss_texture, get_material_uv(gloss_uv_set)).rg;
	vec2 specular_gloss = compute_eye_specular_gloss(specular_gloss_map);
	vec3 halfway_direction = normalize(LIGHT + VIEW);
	vec3 iris_normal = decode_eye_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)).ba, NORMAL,
		normalize(eye_tangent_view), normalize(eye_binormal_view));
	float outer_lobe = pow(clamp(dot(NORMAL, halfway_direction), 0.0, 1.0), specular_gloss.x) * specular_gloss_map.x;
	float iris_lobe = pow(clamp(dot(iris_normal, halfway_direction), 0.0, 1.0), specular_gloss.y) * specular_gloss_map.y;
	vec3 eye_diffuse = (texture(diffuse_texture, get_material_uv(diffuse_uv_set))).rgb;
	vec3 gain = specular_color.rgb * (5.0 * power_gloss_level.z);
	vec3 outer_specular = gain * outer_lobe;
	vec3 iris_specular = gain * iris_lobe * eye_diffuse;
	outer_specular *= vertex_fresnel;
	iris_specular *= compute_fresnel(dot(iris_normal, VIEW));
	SPECULAR_LIGHT += (vColor.rgb * vec3(1.0) * (outer_specular * outer_specular +
		iris_specular * iris_specular) *
		source_light_color * ATTENUATION * shadow) * 100.0;
}
