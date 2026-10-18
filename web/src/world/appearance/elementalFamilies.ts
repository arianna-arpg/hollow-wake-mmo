import type { BodyBuild } from './bodyFamilies';
import { v3 as V } from './bodyPlan';

// Twin of ElementalFamilies.cs; presentation only.
function begin(b: BodyBuild): void
{
    const p = b.plan;
    p.rig = "wisp";
    p.height = b.height;
    p.bone("torso", "root", V(0, b.height * 0.45, 0));
    p.bone("head", "torso", V(0, b.height * 0.72, 0));
    p.bone("arm_l", "torso", V(0, b.height * 0.55, -b.height * b.s("breadth", 0.6) * 0.3));
    p.bone("arm_r", "torso", V(0, b.height * 0.55, b.height * b.s("breadth", 0.6) * 0.3));
}

function eyes(b: BodyBuild, x: number, y: number, spread: number): void
{
    for (const side of [-1, 1])
        b.plan.add("head", 'ellipsoid', V(b.height * 0.045, b.height * 0.05, b.height * 0.04), V(x, y, side * spread), b.c("core", "#fff0cf"), { glow: true, segments: 5 });
}

// An open horizontal loop made of contiguous flat-faceted segments.
function loop(b: BodyBuild, y: number, width: number, depth: number, thickness: number, phase: number, broken: boolean): void
{
    for (let i = 0; i < 12; i++)
    {
        if (broken && i % 4 == 0) continue;
        const a = (i / 12 * 360 + phase) * Math.PI / 180;
        b.plan.add("torso", 'ellipsoid', V(depth * 0.32, thickness, width * 0.11), V(Math.cos(a) * depth * 0.38, y, Math.sin(a) * width * 0.38), b.c("edge", "#bac8cd"), { turn: V(0, 0, -a * 180 / Math.PI - 90), segments: 5 });
    }
}

export function elementalFlame(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.6); const D = H * b.s("depth", 0.5);
    p.add("torso", 'ellipsoid', V(D * 0.78, H * 0.43, W * 0.70), V(0, H * 0.28, 0), b.c("body", "#813d28"), { segments: 7 });
    p.add("head", 'ellipsoid', V(D * 0.45, H * 0.30, W * 0.43), V(D * 0.12, H * 0.49, 0), b.c("core", "#fff0cf"), { glow: true, segments: 6 });
    const n = b.has("forks") ? 5 : 3;
    for (let i = 0; i < n; i++)
    {
        const z = (i - (n - 1) * 0.5) * W * 0.17;
        const h = H * (0.43 + (i % 2) * 0.16);
        p.add("torso", 'cone', V(D * 0.35, h, W * 0.28), V(-D * 0.12, H * 0.65, z), b.c("edge", "#ec8337"), { turn: V(0, -14, 0), glow: true, segments: 5 });
    }
    if (b.has("feet")) for (const side of [-1, 1])
        for (let i = 0; i < 2; i++) p.add("root", 'wedge', V(D * 0.30, H * 0.18, W * 0.24), V((i - 0.5) * D * 0.55, H * 0.09, side * W * 0.36), b.c("body", "#813d28"));
    if (b.has("streamers")) for (const side of [-1, 1])
        p.add(side < 0 ? "arm_l" : "arm_r", 'cone', V(D * 0.16, H * 0.60, W * 0.15), V(-D * 0.26, H * 0.48, side * W * 0.31), b.c("edge", "#ec8337"), { turn: V(0, 25, 0), glow: true, segments: 4 });
    eyes(b, D * 0.35, H * 0.42, W * 0.18);
}

export function elementalSurge(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.9); const D = H * b.s("depth", 0.7);
    p.add("root", 'ellipsoid', V(D, H * 0.15, W), V(0, H * 0.075, 0), b.c("body", "#3d7e97"), { segments: 8 });
    // Back wall and curling lip leave a hollow beneath the crest.
    p.add("torso", 'ellipsoid', V(D * 0.35, H * 0.47, W * 0.66), V(-D * 0.30, H * 0.30, 0), b.c("edge", "#a9d9df"), { segments: 7 });
    for (let i = 0; i < 5; i++)
    {
        const a = Math.PI - i * Math.PI / 4;
        p.add("torso", 'ellipsoid', V(D * 0.32, H * 0.25, W * (0.64 - i * 0.04)), V(Math.cos(a) * D * 0.36, H * (0.55 + Math.sin(a) * 0.28), 0), i == 4 ? b.c("core", "#e6faff") : b.c("edge", "#a9d9df"), { segments: 7 });
    }
    for (const side of [-1, 1])
        p.add(side < 0 ? "arm_l" : "arm_r", 'wedge', V(D * 0.38, H * 0.35, W * 0.18), V(-D * 0.12, H * 0.31, side * W * 0.34), b.c("body", "#3d7e97"), { turn: V(side * 20, 0, 0) });
    eyes(b, D * 0.48, H * 0.57, W * 0.12);
}

export function elementalShard(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.6); const D = H * b.s("depth", 0.5);
    p.add("torso", 'cone', V(D * 0.55, H * 0.72, W * 0.55), V(0, H * 0.46, 0), b.c("body", "#3d7e97"), { segments: 5 });
    const n = b.has("radial") ? 8 : 6;
    for (let i = 0; i < n; i++)
    {
        const a = i * Math.PI * 2 / n;
        const h = H * (b.has("radial") ? 0.34 : 0.48 + (i % 2) * 0.18);
        p.add("torso", 'cone', V(D * 0.22, h, W * 0.22), V(Math.cos(a) * D * 0.28, H * 0.52, Math.sin(a) * W * 0.28), b.c("edge", "#a9d9df"), { turn: V(Math.sin(a) * 30, -Math.cos(a) * 30, 0), segments: 4 });
        if (b.has("crown")) p.add("torso", 'cone', V(D * 0.14, H * 0.25, W * 0.14), V(Math.cos(a) * D * 0.20, H * 0.22, Math.sin(a) * W * 0.20), b.c("core", "#e6faff"), { turn: V(180, 0, 0), segments: 4 });
    }
    p.add("head", 'cone', V(D * 0.26, H * 0.45, W * 0.26), V(0, H * 0.77, 0), b.c("core", "#e6faff"), { segments: 4 });
    eyes(b, D * 0.24, H * 0.48, W * 0.13);
}

export function elementalVortex(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.6); const D = H * b.s("depth", 0.6);
    const broken = b.has("broken");
    for (let j = 0; j < 4; j++) loop(b, H * (0.13 + j * 0.22), W * (0.35 + j * 0.19), D * (0.35 + j * 0.19), H * 0.065, j * 27, broken);
    if (!broken) p.add("head", 'ellipsoid', V(D * 0.18, H * 0.25, W * 0.18), V(0, H * 0.58, 0), b.c("core", "#f2f2e4"), { glow: true, segments: 6 });
    for (const side of [-1, 1])
    {
        const bone = side < 0 ? "arm_l" : "arm_r";
        if (b.has("forks")) for (let i = 0; i < 2; i++)
            p.add(bone, 'wedge', V(D * 0.17, H * 0.20, W * 0.14), V(D * 0.13 * i, H * (0.78 + i * 0.09), side * W * (0.27 + i * 0.08)), b.c("core", "#f2f2e4"), { turn: V(side * 28, 0, 0), glow: true });
        if (b.has("ribbons")) p.add(bone, 'ellipsoid', V(D * 0.75, H * 0.055, W * 0.09), V(-D * 0.15, H * 0.50, side * W * 0.32), b.c("edge", "#a2b9d3"), { turn: V(0, 0, side * 18), segments: 5 });
        if (broken) p.add(bone, 'cone', V(D * 0.20, H * 0.40, W * 0.17), V(0, H * 0.70, side * W * 0.31), b.c("body", "#2b2634"), { turn: V(side * 15, 0, 0), segments: 3 });
    }
}

export function elementalMound(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 1.3); const D = H * b.s("depth", 1.7);
    p.rig = "crawler";
    p.bone("shell", "root", V(0, H * 0.45, 0));
    p.bone("claw_l", "shell", V(D * 0.25, H * 0.25, -W * 0.33));
    p.bone("claw_r", "shell", V(D * 0.25, H * 0.25, W * 0.33));
    p.add("shell", 'ellipsoid', V(D * 0.86, H * 0.70, W * 0.84), V(-D * 0.05, H * 0.38, 0), b.c("body", "#635d52"), { segments: 6 });
    for (const side of [-1, 1])
    {
        for (let i = 0; i < 3; i++) p.add("shell", 'ellipsoid', V(D * 0.32, H * 0.35, W * 0.28), V((i - 1) * D * 0.25, H * 0.20, side * W * 0.34), b.c("edge", "#a19380"), { segments: 5 });
        p.add(side < 0 ? "claw_l" : "claw_r", 'wedge', V(D * 0.30, H * 0.24, W * 0.20), V(D * 0.37, H * 0.22, side * W * 0.32), b.c("body", "#635d52"));
    }
    if (b.has("geode"))
    {
        p.add("shell", 'ellipsoid', V(D * 0.48, H * 0.30, W * 0.48), V(0, H * 0.65, 0), b.c("core", "#c4dce1"), { glow: true, segments: 6 });
        for (let i = 0; i < 8; i++)
        {
            const a = i * Math.PI / 4;
            p.add("shell", 'cone', V(D * 0.18, H * 0.40, W * 0.18), V(Math.cos(a) * D * 0.28, H * 0.72, Math.sin(a) * W * 0.28), b.c("edge", "#a19380"), { segments: 4 });
        }
    }
    if (b.has("lobes")) for (let i = 0; i < 4; i++)
        p.add("shell", 'ellipsoid', V(D * 0.28, H * (0.50 + i * 0.07), W * 0.70), V((i - 1.5) * D * 0.18, H * 0.51, 0), b.c("body", "#635d52"), { segments: 6 });
    eyes(b, D * 0.40, H * 0.36, W * 0.15);
}

export function elementalVeil(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.6); const D = H * b.s("depth", 0.5);
    const petals = b.has("petals");
    p.add("head", 'ellipsoid', V(D * 0.75, H * 0.28, W * 0.90), V(0, H * 0.78, 0), b.c("body", "#373249"), { segments: 7 });
    p.add("head", 'ellipsoid', V(D * 0.35, H * 0.16, W * 0.35), V(D * 0.20, H * 0.70, 0), b.c("core", "#bdb4d0"), { glow: true, segments: 6 });
    for (let i = 0; i < 7; i++)
    {
        const a = i * Math.PI * 2 / 7;
        const h = H * (0.37 + (i % 3) * 0.07);
        p.add("torso", petals ? 'ellipsoid' : 'wedge', V(D * 0.16, h, W * 0.19), V(Math.cos(a) * D * 0.30, H * 0.42, Math.sin(a) * W * 0.35), b.c("edge", "#766785"), { turn: V(Math.sin(a) * 12, 0, -Math.cos(a) * 12), segments: 5 });
        if (petals) p.add("torso", 'cylinder', V(D * 0.035, H * 0.25, W * 0.035), V(Math.cos(a) * D * 0.23, H * 0.18, Math.sin(a) * W * 0.25), b.c("body", "#576b45"), { segments: 4 });
    }
    eyes(b, D * 0.37, H * 0.74, W * 0.16);
}

export function elementalPrism(b: BodyBuild): void
{
    begin(b);
    const p = b.plan; const H = b.height; const W = H * b.s("breadth", 0.6); const D = H * b.s("depth", 0.5);
    const n = b.has("triune") ? 3 : 1;
    for (let i = 0; i < n; i++)
    {
        const z = (i - (n - 1) * 0.5) * W * 0.28;
        const y = H * (n == 1 ? 0.53 : 0.48 + (i % 2) * 0.18);
        const color = b.c(n == 1 ? "core" : i == 0 ? "core_left" : i == 1 ? "core_center" : "core_right", "#eee0f3");
        const bone = n == 1 ? "head" : i == 0 ? "arm_l" : i == 2 ? "arm_r" : "head";
        p.add(bone, 'cone', V(D * 0.26, H * 0.28, W * 0.24), V(0, y + H * 0.14, z), color, { glow: true, segments: 4 });
        p.add(bone, 'cone', V(D * 0.26, H * 0.28, W * 0.24), V(0, y - H * 0.14, z), color, { turn: V(180, 0, 0), glow: true, segments: 4 });
    }
    if (b.has("halo"))
    {
        loop(b, H * 0.52, W, D, H * 0.045, 0, false);
        for (const side of [-1, 1])
            p.add("torso", 'wedge', V(D * 0.15, H * 0.09, W * 0.48), V(0, H * 0.53, side * W * 0.30), b.c("edge", "#e6d39a"), { glow: true });
    }
    if (b.has("lattice") || b.has("triune"))
        for (const side of [-1, 1])
            for (let i = 0; i < 2; i++) p.add("torso", 'wedge', V(D * 0.14, H * 0.43, W * 0.15), V(0, H * (0.32 + i * 0.36), side * W * 0.30), b.c("edge", "#b18ebc"), { turn: V(side * (i == 0 ? -30 : 30), 0, 0) });
}
