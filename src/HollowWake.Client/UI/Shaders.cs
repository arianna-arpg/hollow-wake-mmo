using Godot;

namespace HollowWake.Client.UI;

/// <summary>
/// The interface's shaders: the orb's pool, veil and glass, and the surface of a pane (stone grain or
/// parchment fibre with a vignette). Colours and strengths are uniforms the skin sets from the theme.
/// </summary>
public static class Shaders
{
    private static Shader? _orb;
    private static Shader? _surface;

    public static Shader Orb => _orb ??= new Shader { Code = OrbCode };
    public static Shader Surface => _surface ??= new Shader { Code = SurfaceCode };

    private const string Noise = @"
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
    return v;
}
";

    private const string OrbCode = @"
shader_type canvas_item;
uniform float fill = 1.0;
uniform float reserved = 0.0;
uniform float veil = 0.0;
uniform float pulse = 0.0;
uniform vec4 fill_color = vec4(0.75, 0.15, 0.18, 1.0);
uniform vec4 deep_color = vec4(0.3, 0.04, 0.06, 1.0);
uniform vec4 veil_color = vec4(0.62, 0.86, 1.0, 1.0);
uniform vec4 reserved_color = vec4(0.03, 0.02, 0.04, 1.0);
uniform vec4 empty_color = vec4(0.05, 0.045, 0.04, 1.0);
uniform vec4 glass_color = vec4(1.0);
uniform float glass = 0.22;
uniform float phase = 0.0;
uniform float motion = 1.0;
" + Noise + @"
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float aa = max(fwidth(r) * 1.5, 0.004);
    float alpha = 1.0 - smoothstep(1.0 - aa, 1.0, r);
    float h = 1.0 - UV.y;
    float t = TIME * 0.6 * motion + phase;
    float calm = smoothstep(0.0, 0.04, fill) * smoothstep(1.0, 0.96, fill) * motion;
    float wave = (sin(UV.x * 9.0 + t * 1.7) * 0.012 + sin(UV.x * 17.0 - t * 2.3) * 0.006) * calm;
    float level = fill + wave;
    vec3 col = empty_color.rgb * (0.65 + 0.35 * (1.0 - h));
    if (h < level) {
        float depth = clamp((level - h) / max(level, 0.001), 0.0, 1.0);
        vec3 liquid = mix(fill_color.rgb, deep_color.rgb, clamp(depth * 0.65 + r * 0.4, 0.0, 1.0));
        float n = noise(vec2(UV.x * 5.0 + t * 0.15, h * 5.0 - t * 0.25));
        float n2 = noise(vec2(UV.x * 11.0 - t * 0.3, h * 9.0 + t * 0.2));
        liquid *= 0.8 + 0.3 * n + 0.1 * n2;
        float surface = 1.0 - smoothstep(0.0, 0.03, level - h);
        liquid = mix(liquid, fill_color.rgb * 1.45 + 0.06, surface * 0.55 * calm);
        col = liquid;
    }
    if (reserved > 0.0) {
        float edge = 1.0 - reserved;
        float cap = smoothstep(edge - 0.008, edge + 0.008, h);
        col = mix(col, reserved_color.rgb, cap * 0.8);
        col += vec3(0.22) * (1.0 - smoothstep(0.0, 0.012, abs(h - edge)));
    }
    if (veil > 0.0) {
        // The shield: a cool glass laid over the orb from the top. Over the pool it tints lightly; over the empty
        // glass it shows as frost, so a full shield veils the whole orb and a spent one leaves life bare.
        float top = 1.0 - veil;
        float v = smoothstep(top - 0.01, top + 0.01, h);
        float frost = noise(vec2(UV.x * 22.0 + t * 0.2, h * 6.0 - t * 0.3)) * noise(vec2(UV.x * 7.0 - t * 0.1, h * 19.0));
        float over_pool = h < level ? 0.22 : 0.5;
        col = mix(col, veil_color.rgb, v * (over_pool + frost * 0.25));
        col += veil_color.rgb * v * smoothstep(0.35, 0.9, frost) * 0.12;
        float rim = 1.0 - smoothstep(0.0, 0.016 + 0.02 * pulse, abs(h - top));
        col += veil_color.rgb * rim * (0.45 + 0.6 * pulse);
    }
    col *= 1.0 - smoothstep(0.42, 1.0, r) * 0.58;
    float hl = 1.0 - smoothstep(0.0, 0.5, length((p - vec2(-0.32, -0.44)) * vec2(1.0, 1.75)));
    col += glass_color.rgb * hl * glass;
    float rimlight = smoothstep(0.8, 0.98, r) * clamp(0.5 + 0.5 * p.y, 0.0, 1.0);
    col += glass_color.rgb * rimlight * glass * 0.55;
    COLOR = vec4(col, alpha);
}
";

    private const string SurfaceCode = @"
shader_type canvas_item;
uniform vec4 base_color = vec4(0.11, 0.1, 0.095, 1.0);
uniform vec4 light_color = vec4(0.17, 0.16, 0.145, 1.0);
uniform vec4 edge_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float grain = 0.15;
uniform float fibre = 0.0;
uniform float vignette = 0.4;
uniform float veins = 0.0;
uniform vec2 size = vec2(400.0, 400.0);
uniform float seed = 0.0;
uniform float opacity = 1.0;
" + Noise + @"
void fragment() {
    vec2 px = UV * size + vec2(seed * 97.0, seed * 61.0);
    float clouds = fbm(px / 90.0);
    vec3 col = mix(base_color.rgb, light_color.rgb, smoothstep(0.25, 0.85, clouds));
    float speck = noise(px / 1.7) * 0.6 + noise(px / 4.0) * 0.4;
    col *= 1.0 + (speck - 0.5) * grain;
    if (veins > 0.0) {
        // Fine dark seams in the stone: ridged noise thresholded to hairlines.
        float ridge = 1.0 - abs(fbm(px / 140.0 + vec2(5.2, 1.3)) * 2.0 - 1.0);
        col *= 1.0 - smoothstep(0.955, 0.992, ridge) * veins;
    }
    if (fibre > 0.0) {
        float f = noise(vec2(px.x / 46.0, px.y / 1.6)) * noise(vec2(px.x / 9.0, px.y / 3.0));
        col = mix(col, col * 0.86, smoothstep(0.32, 0.6, f) * fibre);
        float stain = fbm(px / 160.0 + 3.7);
        col = mix(col, col * vec3(0.92, 0.86, 0.74), smoothstep(0.55, 0.8, stain) * fibre * 0.6);
    }
    vec2 d = min(UV, 1.0 - UV) * size;
    float e = 1.0 - smoothstep(0.0, 34.0, min(d.x, d.y));
    col = mix(col, edge_color.rgb, e * vignette);
    COLOR = vec4(col, opacity);
}
";
}
