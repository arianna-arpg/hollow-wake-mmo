import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO } from './bodyPlan';

// Twin of ScaledBipedFamilies.cs; all species choices live in appearance content.
export function scaledBiped(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", 0.4);
        const D = H * b.s("depth", 0.37);
        const snout = b.s("snout", 1);
        const crest = b.s("crest", 1);
        const tail = H * 0.60 * b.s("tail", 1);
        const hip = H * 0.39;
        const shoulder = H * 0.70;
        const hx = D * 0.28;
        const skin = b.c("skin", "#9d8053");
        const scute = b.c("scute", "#40594e");
        const belly = b.c("belly", "#cfbb88");
        const cloth = b.c("cloth", "#514d47");
        const bone = b.c("bone", "#e6d5ad");
        const accent = b.c("accent", "#c7a36a");
        const dark = b.c("dark", "#252c29");
        p.bone("hips", "root", V(0, hip, 0));
        p.bone("torso", "hips", V(0, hip, 0));
        p.bone("head", "torso", V(hx, H * 0.81, 0));
        p.bone("tail", "hips", V(-D * 0.25, hip, 0));
        p.add("torso", 'ellipsoid', V(D * 0.66, H * 0.40, W * 0.70), V(-D * 0.02, H * 0.59, 0), skin, { segments: 6 });
        p.add("hips", 'wedge', V(D * 0.71, H * 0.12, W * 0.77), V(0, hip, 0), cloth);
        // Broad ventral plates and a dorsal ridge read as scales at ordinary game distance.
        for (let i = 0; i < 4; i++)
        {
            p.add("torso", 'wedge', V(D * 0.12, H * 0.065, W * (0.39 - i * 0.035)), V(D * 0.29, H * (0.47 + i * 0.07), 0), belly);
            p.add("torso", 'wedge', V(D * 0.22, H * 0.09, W * 0.47), V(-D * 0.32, H * (0.48 + i * 0.07), 0), scute);
        }
        p.add("head", 'ellipsoid', V(D * 0.69, H * 0.24, W * 0.62), V(hx, H * 0.87, 0), skin, { segments: 6 });
        p.add("head", 'wedge', V(D * 0.70 * snout, H * 0.12, W * 0.48), V(hx + D * 0.38, H * 0.855, 0), skin);
        p.add("head", 'box', V(D * 0.55 * snout, H * 0.045, W * 0.42), V(hx + D * 0.36, H * 0.785, 0), belly);
        p.add("head", 'box', V(D * 0.56 * snout, H * 0.012, W * 0.43), V(hx + D * 0.36, H * 0.812, 0), dark);
        for (let i = 0; i < 3; i++)
            p.add("tail", 'ellipsoid', V(tail * 0.48, H * (0.17 - i * 0.045), W * (0.33 - i * 0.09)), V(-D * 0.25 - tail * (0.19 + i * 0.29), hip - H * (0.04 + i * 0.07), 0), i == 1 ? scute : skin, { segments: 6 });
        p.add("tail", 'cone', V(H * 0.06, tail * 0.33, H * 0.06), V(-D * 0.25 - tail, H * 0.20, 0), scute, { turn: V(0, 80, 0), segments: 5 });
        for (const side of [-1, 1])
        {
            const arm = side < 0 ? "arm_l" : "arm_r";
            const leg = side < 0 ? "leg_l" : "leg_r";
            const z = side * W * 0.37;
            const lz = side * W * 0.23;
            p.bone(arm, "torso", V(0, shoulder, z));
            p.bone(leg, "hips", V(0, hip, lz));
            p.add(arm, 'ellipsoid', V(D * 0.24, H * 0.25, W * 0.19), V(D * 0.06, H * 0.59, z), skin, { segments: 5 });
            p.add(arm, 'ellipsoid', V(D * 0.27, H * 0.11, W * 0.21), V(D * 0.23, H * 0.46, z), scute, { segments: 5 });
            p.add(leg, 'ellipsoid', V(D * 0.40, H * 0.26, W * 0.24), V(D * 0.08, H * 0.31, lz), skin, { segments: 6 });
            p.add(leg, 'cylinder', V(W * 0.13, H * 0.19, W * 0.13), V(-D * 0.11, H * 0.15, lz), scute, { turn: V(0, -22, 0), segments: 5 });
            p.add(leg, 'wedge', V(D * 0.55, H * 0.065, W * 0.27), V(D * 0.08, H * 0.0325, lz), skin);
            for (let toe = -1; toe <= 1; toe++)
                p.add(leg, 'cone', V(H * 0.027, H * 0.09, H * 0.027), V(D * 0.38, H * 0.035, lz + toe * W * 0.08), bone, { turn: V(0, -90, 0), segments: 4 });
            p.add("head", 'wedge', V(D * 0.30, H * 0.045, W * 0.14), V(hx + D * 0.11, H * 0.94, side * W * 0.26), scute);
            p.add("head", 'ellipsoid', V(D * 0.16, H * 0.035, W * 0.045), V(hx + D * 0.15, H * 0.914, side * W * 0.29), b.c("eye", "#f0d98a"), { segments: 5 });
            p.add("head", 'box', V(D * 0.018, H * 0.032, W * 0.05), V(hx + D * 0.17, H * 0.914, side * W * 0.30), dark);
            p.add("head", 'box', V(D * 0.07, H * 0.018, W * 0.04), V(hx + D * 0.62 * snout, H * 0.87, side * W * 0.16), dark);
            if (b.has("frill")) for (let i = 0; i < 3; i++)
                p.add("head", 'wedge', V(D * 0.12, H * 0.19 * crest, W * 0.30), V(hx - D * 0.22, H * (0.81 + i * 0.065), side * W * (0.27 + i * 0.04)), accent, { turn: V(side * (30 - i * 25), 0, 0) });
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm, at:V(D * 0.23, H * 0.43, z), turn:ZERO, size:H * b.s("gear", 0.85)});
        }
        if (b.has("short_crest") || b.has("crest")) for (let i = 0; i < 3; i++)
            p.add("head", 'wedge', V(D * 0.18, H * (b.has("short_crest") ? 0.07 : 0.17) * crest, W * 0.08), V(hx - D * 0.26 + i * D * 0.19, H * 1.00, 0), accent);
        if (b.has("sail")) p.add("torso", 'wedge', V(D * 0.39, H * 0.56, W * 0.08), V(-D * 0.41, H * 0.76, 0), accent);
        p.sockets.set('head', { bone: 'head', at: V(hx, H, 0), turn: ZERO, size: H * 0.24 });
        p.sockets.set('chest', { bone: 'torso', at: V(0, H * 0.59, 0), turn: ZERO, size: H });
        p.sockets.set('belt', { bone: 'hips', at: V(0, hip, 0), turn: ZERO, size: H });
        p.swings = true;
}
