shader_type spatial;
render_mode ambient_light_disabled, blend_mix, cull_disabled, depth_draw_opaque;

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

uniform float luminance_range;

uniform vec2 uv0_offset;
uniform vec2 uv1_offset;

uniform bool use_gi_atlas = false;
uniform sampler2D gi_atlas : hint_white;
uniform vec2 gi_uv_scale = vec2(1.0);
uniform vec2 gi_uv_offset = vec2(0.0);
uniform vec3 gi_rgb_scale = vec3(1.0);
uniform float gi_saturation = 1.0;

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

uniform float environment_max_lod = 0.0;

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

varying float vertex_fresnel;

float compute_fresnel(float cos_theta)
{
	return pow(clamp(1.0 - cos_theta, 0.0, 1.0), 5.0) * 0.7 + 0.3;
}

void vertex()
{
	BINORMAL = -BINORMAL;
	bse_shadow = compute_shadow((WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz);

	vec3 position_world = (WORLD_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 normal_world = normalize((WORLD_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 eye_world = normalize(CAMERA_MATRIX[3].xyz - position_world);
	vertex_fresnel = compute_fresnel(dot(normal_world, eye_world));

	vec2 second_uv = UV2;
	
	
	gi_uv = second_uv * gi_uv_scale + gi_uv_offset;
	uv_coordinates_0_1 = vec4(UV, gi_uv) + vec4(uv0_offset, uv1_offset);
	
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

vec3 decode_normal_map(vec4 normal_map)
{
	normal_map.x *= normal_map.w;
	normal_map.xy = normal_map.xy * 2.0 - 1.0;
	normal_map.z = sqrt(abs(1.0 - dot(normal_map.xy, normal_map.xy)));
	return normal_map.xyz;
}
float compute_direct_shadow()
{
	if (!use_gi_atlas)
	{
		return bse_shadow;
	}
	return bse_shadow * mix(1.0, 1.0, texture(gi_atlas, gi_uv).a);
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

vec4 sample_environment_cross_level(vec2 face, vec2 local_uv, vec2 face_size, float level)
{
	vec2 half_texel = 0.5 / max(floor(face_size / exp2(level)), vec2(1.0));
	vec2 bounded_uv = clamp(local_uv, half_texel, vec2(1.0) - half_texel);
	return textureLod(environment_texture, (face + bounded_uv) / vec2(4.0, 3.0), level);
}

vec4 sample_environment_cross(vec3 direction)
{
	vec3 direction_dx = dFdx(direction);
	vec3 direction_dy = dFdy(direction);
	vec3 magnitude = abs(direction);
	vec2 face;
	vec3 major_axis;
	vec3 u_axis;
	vec3 v_axis;
	if (magnitude.x > magnitude.y && magnitude.x > magnitude.z)
	{
		v_axis = vec3(0.0, -1.0, 0.0);
		if (direction.x >= 0.0)
		{
			face = vec2(2.0, 1.0);
			major_axis = vec3(1.0, 0.0, 0.0);
			u_axis = vec3(0.0, 0.0, -1.0);
		}
		else
		{
			face = vec2(0.0, 1.0);
			major_axis = vec3(-1.0, 0.0, 0.0);
			u_axis = vec3(0.0, 0.0, 1.0);
		}
	}
	else
	{
		if (magnitude.y > magnitude.z)
		{
			u_axis = vec3(1.0, 0.0, 0.0);
			if (direction.y >= 0.0)
			{
				face = vec2(1.0, 0.0);
				major_axis = vec3(0.0, 1.0, 0.0);
				v_axis = vec3(0.0, 0.0, 1.0);
			}
			else
			{
				face = vec2(1.0, 2.0);
				major_axis = vec3(0.0, -1.0, 0.0);
				v_axis = vec3(0.0, 0.0, -1.0);
			}
		}
		else
		{
			v_axis = vec3(0.0, -1.0, 0.0);
			if (direction.z >= 0.0)
			{
				face = vec2(1.0, 1.0);
				major_axis = vec3(0.0, 0.0, 1.0);
				u_axis = vec3(1.0, 0.0, 0.0);
			}
			else
			{
				face = vec2(3.0, 1.0);
				major_axis = vec3(0.0, 0.0, -1.0);
				u_axis = vec3(-1.0, 0.0, 0.0);
			}
		}
	}
	float major = max(dot(direction, major_axis), 0.000001);
	vec2 planar = vec2(dot(direction, u_axis), dot(direction, v_axis));
	vec2 local_uv = planar / major * 0.5 + 0.5;
	vec2 face_size = vec2(textureSize(environment_texture, 0)) / vec2(4.0, 3.0);
	vec2 planar_dx = vec2(dot(direction_dx, u_axis), dot(direction_dx, v_axis));
	vec2 planar_dy = vec2(dot(direction_dy, u_axis), dot(direction_dy, v_axis));
	vec2 uv_dx = (planar_dx * major - planar * dot(direction_dx, major_axis)) * (0.5 / (major * major));
	vec2 uv_dy = (planar_dy * major - planar * dot(direction_dy, major_axis)) * (0.5 / (major * major));
	float footprint = max(length(uv_dx * face_size), length(uv_dy * face_size));
	float maximum_level = max(floor(min(environment_max_lod, log2(min(face_size.x, face_size.y)))), 0.0);
	float level = clamp(log2(max(footprint, 1.0)), 0.0, maximum_level);
	float lower_level = floor(level);
	float upper_level = min(lower_level + 1.0, maximum_level);
	vec4 lower_sample = sample_environment_cross_level(face, local_uv, face_size, lower_level);
	if (level == lower_level)
	{
		return lower_sample;
	}
	vec4 upper_sample = sample_environment_cross_level(face, local_uv, face_size, upper_level);
	return mix(lower_sample, upper_sample, level - lower_level);
}

void fragment()
{
	vec3 normal_map = decode_normal_map(texture(normal_texture, get_material_uv(normal_uv_set)));
	NORMAL = normalize(
		normal_map.x * normalize(TANGENT) +
		normal_map.y * normalize(BINORMAL) +
		normal_map.z * normalize(NORMAL));
	vec4 diffuse = texture(diffuse_texture, get_material_uv(diffuse_uv_set));
	vec3 base_color = diffuse.rgb;
	ALBEDO = base_color * diffuse_color.rgb;
	ALBEDO *= ALBEDO;
	vec3 light_field;
	if (use_gi_atlas)
	{
		vec3 baked = texture(gi_atlas, gi_uv).rgb;
		baked *= baked;
		float gray = dot(baked, vec3(0.298900008, 0.586600006, 0.114399999));
		light_field = clamp(mix(vec3(gray), baked, gi_saturation), 0.0, 1.0) * gi_rgb_scale * ambient_light_color.rgb * base_color;
	}
	else
	{
		vec3 world_normal = (CAMERA_MATRIX * vec4(NORMAL.xyz, 0.0)).xyz;
		light_field = get_light_field_color(world_normal) * base_color;
	}
	EMISSION = vec3(0.0);
	EMISSION += light_field * light_field;
	vec3 reflection_direction = mat3(CAMERA_MATRIX) * reflect(-VIEW, NORMAL);

	vec4 environment_map = sample_environment_cross(reflection_direction);
	environment_map.rgb += environment_map.rgb * environment_map.a * luminance_range;
	vec3 reflected = environment_map.rgb * texture(gloss_texture, get_material_uv(gloss_uv_set)).r * vertex_fresnel * (0.3 + 0.7 * compute_direct_shadow());
	EMISSION += reflected * reflected;

	

	{
		float specular_gloss = clamp(texture(gloss_texture, get_material_uv(gloss_uv_set)).r * exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
		float specular_level = specular_gloss * 0.125;
		float cos_theta = dot(NORMAL, VIEW);
		cos_theta = pow(clamp(cos_theta, 0.0, 1.0), specular_gloss);

		vec4 fake_specular = vec4(vec3(1.0), 1.0);
		fake_specular.rgb *= specular_color.rgb;
		fake_specular.rgb *= cos_theta;
		fake_specular.rgb *= specular_level * 0.2;
		fake_specular.rgb *= vertex_fresnel;
		fake_specular.rgb *= (clamp(dot(NORMAL, VIEW), 0.0, 1.0));

		fake_specular.rgb *= fake_specular.rgb;

		EMISSION += fake_specular.rgb;
	}

	EMISSION += 2.0 * light_field * reflected;
}

void light()
{
	float renderer_specular_transfer = 100.0 * (0.4524 * 0.04 - 0.0024);
	vec3 source_light_color = LIGHT_COLOR / (3.14159265 * renderer_specular_transfer);
	float diffuse_cos_theta = clamp(dot(LIGHT, NORMAL), 0.0, 1.0);
	float shadow = compute_direct_shadow();
	DIFFUSE_LIGHT += ALBEDO * diffuse_cos_theta * diffuse_cos_theta * LIGHT_COLOR * ATTENUATION * shadow * shadow;
	float specular_gloss_map = texture(gloss_texture, get_material_uv(gloss_uv_set)).r;
	float specular_gloss = clamp(specular_gloss_map * exp2(power_gloss_level.y * 10.0 + 2.0), 1.0, 512.0);
	float specular_level = specular_gloss * 0.125;
	vec3 halfway_direction = normalize(LIGHT + VIEW);
	float cos_theta = pow(clamp(dot(halfway_direction, NORMAL), 0.0, 1.0), specular_gloss);
	vec4 specular = vec4(specular_color.rgb, 1.0);
	specular.rgb *= cos_theta;
	specular.rgb *= specular_level;
	specular.rgb *= vertex_fresnel * diffuse_cos_theta;
	specular.rgb *= specular.rgb;
	SPECULAR_LIGHT += (specular.rgb * source_light_color * ATTENUATION * shadow * shadow) * 100.0;
}
