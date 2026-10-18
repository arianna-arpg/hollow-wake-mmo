using Godot;

namespace HollowWake.Client.World.Appearance;

/// <summary>
/// The materials every built body shares: a lit painted surface with per-instance flash and fade, an unlit
/// one for parts that read as light, and the card material for baked directional cards. One material per
/// kind for every body keeps the draw calls sortable and the shader count at three.
/// </summary>
public static class BodyShaders
{
    private static ShaderMaterial? _painted;
    private static ShaderMaterial? _glow;
    private static ShaderMaterial? _card;
    private static ShaderMaterial? _mark;

    /// <summary>Lit facets in vertex colour, with instance uniforms flash (toward the flash colour) and fade.</summary>
    public static ShaderMaterial Painted => _painted ??= new ShaderMaterial { Shader = new Shader { Code = PaintedCode } };

    /// <summary>Unlit vertex colour for glowing parts, with the same instance uniforms.</summary>
    public static ShaderMaterial Glow => _glow ??= new ShaderMaterial { Shader = new Shader { Code = GlowCode } };

    /// <summary>A baked card: alpha-tested, depth-written, lit by the sky only, with the cell chosen per instance.</summary>
    public static ShaderMaterial Card => _card ??= new ShaderMaterial { Shader = new Shader { Code = CardCode } };

    /// <summary>The ground contact mark under a body: a soft disc with an edge grammar per relation.</summary>
    public static ShaderMaterial Mark => _mark ??= new ShaderMaterial { Shader = new Shader { Code = MarkCode } };

    private const string PaintedCode = @"
shader_type spatial;
render_mode cull_back, depth_draw_opaque;
instance uniform float flash = 0.0;
instance uniform float fade = 1.0;
uniform vec4 flash_color : source_color = vec4(1.0, 0.94, 0.82, 1.0);
void fragment() {
    vec3 col = COLOR.rgb;
    ALBEDO = mix(col, flash_color.rgb, flash);
    EMISSION = flash_color.rgb * flash * 0.5;
    ROUGHNESS = 0.92;
    METALLIC = 0.0;
    SPECULAR = 0.25;
    ALPHA = fade;
}
";

    private const string GlowCode = @"
shader_type spatial;
render_mode cull_back, unshaded, depth_draw_opaque;
instance uniform float flash = 0.0;
instance uniform float fade = 1.0;
uniform vec4 flash_color : source_color = vec4(1.0, 0.94, 0.82, 1.0);
void fragment() {
    ALBEDO = mix(COLOR.rgb, flash_color.rgb, flash * 0.5);
    ALPHA = fade;
}
";

    private const string CardCode = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, alpha_to_coverage;
uniform sampler2D atlas : source_color, filter_linear_mipmap;
uniform float cells = 8.0;
instance uniform float cell = 0.0;
instance uniform float flash = 0.0;
instance uniform float fade = 1.0;
uniform vec4 flash_color : source_color = vec4(1.0, 0.94, 0.82, 1.0);
void fragment() {
    vec2 uv = vec2((UV.x + cell) / cells, UV.y);
    vec4 tex = texture(atlas, uv);
    if (tex.a < 0.5) { discard; }
    ALBEDO = mix(tex.rgb, flash_color.rgb, flash);
    EMISSION = flash_color.rgb * flash * 0.5;
    ROUGHNESS = 0.95;
    SPECULAR = 0.1;
    ALPHA = tex.a * fade;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
}
";

    private const string MarkCode = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;
uniform vec4 color : source_color = vec4(0.08, 0.09, 0.1, 1.0);
uniform vec4 edge_color : source_color = vec4(0.92, 0.9, 0.84, 1.0);
uniform float opacity = 0.35;
// 0 plain, 1 teeth (inward, hostile), 2 ticks (spaced squares, supportive)
instance uniform float style = 0.0;
instance uniform float fade = 1.0;
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float aa = fwidth(r) * 1.5;
    float disc = 1.0 - smoothstep(0.78 - aa, 0.9, r);
    float a = disc * opacity * (1.0 - r * 0.35);
    float ring = smoothstep(0.86, 0.9, r) * (1.0 - smoothstep(0.98, 1.0, r));
    float angle = atan(p.y, p.x);
    float mark = 0.0;
    if (style > 0.5 && style < 1.5) {
        float tooth = abs(fract(angle / 6.2831853 * 12.0) - 0.5) * 2.0;
        float inward = 1.0 - smoothstep(0.78, 0.98, r);
        mark = ring * step(tooth, 0.55) + (1.0 - smoothstep(0.72, 0.9, r)) * step(0.8, r) * step(tooth, 0.3) * inward;
    } else if (style >= 1.5) {
        float tick = step(0.7, abs(fract(angle / 6.2831853 * 8.0) - 0.5) * 2.0);
        mark = ring * tick;
    }
    vec3 col = mix(color.rgb, edge_color.rgb, mark);
    ALBEDO = col;
    ALPHA = max(a, mark * 0.9) * fade;
}
";
}
