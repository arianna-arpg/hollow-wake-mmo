import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO } from './bodyPlan';

// Twin of OrcFamilies.cs. Shape values are content, never entity ids.
export function orc(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", 0.55);
        const D = H * b.s("depth", 0.38);
        const hip = H * 0.35;
        const shoulder = H * 0.77;
        const reach = H * 0.36 * b.s("arms", 1);
        const hx = D * 0.22 * b.s("hunch", 0.8);
        const muscle = b.s("muscle", 1);
        const skin = b.c("skin", "#6b7951");
        const dark = b.c("dark", "#292b29");
        const cloth = b.c("cloth", "#593a32");
        const metal = b.c("metal", "#676d6a");
        const bone = b.c("bone", "#d9cbaa");
        const wood = b.c("wood", "#72573b");
        const accent = b.c("accent", "#b96d44");
        p.bone("hips", "root", V(0, hip, 0));
        p.bone("torso", "hips", V(0, hip, 0));
        p.bone("head", "torso", V(hx, H * 0.83, 0));
        // The arch is authored directly, so posture does not accidentally change the rest height.
        p.add("torso", 'ellipsoid', V(D * 0.85, H * 0.49, W * 0.73), V(-D * 0.07, H * 0.60, 0), skin, { segments: 7 });
        p.add("torso", 'ellipsoid', V(D * 0.75, H * 0.30, W * 0.68), V(-D * 0.16, H * 0.77, 0), skin, { segments: 6 });
        p.add("torso", 'ellipsoid', V(D * 0.65, H * 0.18, W * 0.55), V(hx * 0.2, H * 0.83, 0), skin, { segments: 6 });
        p.add("hips", 'wedge', V(D * 0.74, H * 0.20, W * 0.60), V(0, hip, 0), cloth);
        p.add("hips", 'box', V(D * 0.78, H * 0.035, W * 0.63), V(0, hip + H * 0.065, 0), metal);
        p.add("head", 'ellipsoid', V(D * 0.48, H * 0.22, W * 0.38), V(hx, H * 0.89, 0), skin, { segments: 6 });
        p.add("head", 'wedge', V(D * 0.48, H * 0.10 * b.s("jaw", 1), W * 0.35), V(hx + D * 0.11, H * 0.82, 0), skin);
        p.add("head", 'box', V(D * 0.035, H * 0.018, W * 0.26), V(hx + D * 0.36, H * 0.835, 0), dark);
        p.add("head", 'wedge', V(D * 0.19, H * 0.07, W * 0.11), V(hx + D * 0.24, H * 0.90, 0), skin);
        for (const side of [-1, 1])
        {
            const arm = side < 0 ? "arm_l" : "arm_r";
            const leg = side < 0 ? "leg_l" : "leg_r";
            const z = side * W * 0.38;
            p.bone(arm, "torso", V(0, shoulder, z));
            p.bone(leg, "hips", V(0, hip, side * W * 0.22));
            p.add(arm, 'ellipsoid', V(D * 0.50, reach * 0.67, W * 0.24 * muscle), V(0, shoulder - reach * 0.23, z), skin, { segments: 6 });
            p.add(arm, 'ellipsoid', V(D * 0.42, reach * 0.56, W * 0.19 * muscle), V(D * 0.12, shoulder - reach * 0.70, z), skin, { segments: 6 });
            p.add(arm, 'box', V(D * 0.38, H * 0.09, W * 0.22), V(D * 0.16, shoulder - reach, z), skin);
            p.add(leg, 'ellipsoid', V(D * 0.42, hip * 0.82, W * 0.25), V(-D * 0.06, hip * 0.63, side * W * 0.22), skin, { segments: 6 });
            p.add(leg, 'box', V(D * 0.36, hip * 0.42, W * 0.19), V(D * 0.02, hip * 0.28, side * W * 0.22), dark);
            p.add(leg, 'box', V(D * 0.57, H * 0.055, W * 0.25), V(D * 0.13, H * 0.0275, side * W * 0.22), dark);
            p.add("head", 'wedge', V(D * 0.14, H * 0.09, W * 0.20), V(hx - D * 0.03, H * 0.90, side * W * 0.24), skin, { turn: V(side * 35, 0, 0) });
            p.add("head", 'box', V(D * 0.06, H * 0.035, W * 0.15), V(hx + D * 0.21, H * 0.935, side * W * 0.105), dark, { turn: V(side * 12, 0, 0) });
            p.add("head", 'box', V(D * 0.065, H * 0.018, W * 0.07), V(hx + D * 0.24, H * 0.914, side * W * 0.105), b.c("eye", "#d6b66c"));
            if (b.has("tusks")) p.add("head", 'cone', V(W * 0.055, H * 0.15, W * 0.055), V(hx + D * 0.33, H * 0.86, side * W * 0.15), bone, { turn: V(0, -13, 0), segments: 5 });
            if (b.has("pauldrons")) p.add("torso", 'wedge', V(D * 0.68, H * 0.12, W * 0.32), V(-D * 0.03, shoulder + H * 0.03, z), metal, { turn: V(side * 10, 0, 0) });
            if (b.has("bracers")) p.add(arm, 'cylinder', V(W * 0.23, reach * 0.25, W * 0.23), V(D * 0.1, shoulder - reach * 0.78, z), metal, { segments: 6 });
            if (b.has("horns")) p.add("head", 'cone', V(W * 0.10, H * 0.25, W * 0.10), V(hx - D * 0.1, H * 1.01, side * W * 0.20), bone, { turn: V(side * 35, 0, 0), segments: 5 });
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm, at:V(D * 0.16, shoulder - reach, z), turn:ZERO, size:H * 0.55});
        }
        if (b.has("armor")) p.add("torso", 'wedge', V(D * 0.15, H * 0.33, W * 0.69), V(D * 0.34, H * 0.62, 0), metal);
        if (b.has("warpaint")) p.add("torso", 'box', V(D * 0.025, H * 0.22, W * 0.10), V(D * 0.37, H * 0.64, W * 0.14), accent, { turn: V(20, 0, 0) });
        if (b.has("robe")) p.add("hips", 'cone', V(D * 0.85, H * 0.48, W * 0.80), V(0, H * 0.28, 0), cloth, { segments: 7 });
        if (b.has("visor")) p.add("head", 'wedge', V(D * 0.12, H * 0.10, W * 0.42), V(hx + D * 0.29, H * 0.87, 0), metal);
        if (b.has("mohawk")) for (let i = 0; i < 4; i++) p.add("head", 'cone', V(D * 0.13, H * 0.14, W * 0.05), V(hx - D * 0.22 + i * D * 0.12, H * 1.035, 0), accent, { segments: 4 });
        if (b.has("backspikes")) for (let i = -1; i <= 1; i++) p.add("torso", 'cone', V(W * 0.1, H * 0.30, W * 0.1), V(-D * 0.40, H * 0.90, i * W * 0.24), metal, { turn: V(0, 30, 0), segments: 4 });
        if (b.has("banner"))
        {
            p.add("torso", 'cylinder', V(H * 0.03, H * 0.8, H * 0.03), V(-D * 0.46, H * 0.95, W * 0.2), wood, { segments: 5 });
            p.add("torso", 'wedge', V(H * 0.02, H * 0.25, W * 0.55), V(-D * 0.46, H * 1.23, W * 0.43), accent);
        }
        if (b.has("quiver")) for (let i = -1; i <= 1; i++)
        {
            p.add("torso", 'cylinder', V(H * 0.02, H * 0.64, H * 0.02), V(-D * 0.48, H * 0.78, i * W * 0.12), wood, { segments: 5 });
            p.add("torso", 'cone', V(H * 0.07, H * 0.16, H * 0.07), V(-D * 0.48, H * 1.12, i * W * 0.12), metal, { segments: 4 });
        }
        if (b.has("rockpack")) for (let i = -1; i <= 1; i++) p.add("torso", 'ellipsoid', V(D * 0.5, H * 0.19, W * 0.30), V(-D * 0.58, H * 0.66, i * W * 0.24), metal, { segments: 5 });
        if (b.has("trophies")) for (let i = -1; i <= 1; i++) p.add("torso", 'ellipsoid', V(H * 0.07, H * 0.10, H * 0.08), V(D * 0.43, H * 0.72, i * W * 0.17), bone, { segments: 5 });
        if (b.has("chains")) for (let i = 0; i < 6; i++) p.add("hips", 'box', V(H * 0.04, H * 0.065, H * 0.045), V(D * 0.33, H * 0.39 - i * H * 0.045, W * 0.27), metal, { turn: V(i % 2 * 45, 0, 0) });
        if (b.has("drums"))
        {
            p.add("hips", 'cylinder', V(W * 0.55, H * 0.22, W * 0.55), V(D * 0.47, H * 0.44, 0), wood, { segments: 8 });
            p.add("hips", 'cylinder', V(W * 0.58, H * 0.02, W * 0.58), V(D * 0.47, H * 0.56, 0), bone, { segments: 8 });
        }
        if (b.has("censer")) p.add("torso", 'ellipsoid', V(H * 0.12, H * 0.17, H * 0.12), V(D * 0.42, H * 0.62, 0), accent, { glow: true, segments: 6 });
        p.swings = true;
}
