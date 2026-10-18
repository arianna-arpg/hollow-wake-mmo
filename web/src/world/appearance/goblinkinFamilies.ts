import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO } from './bodyPlan';

// Paired with World/Logic/GoblinkinFamilies.cs.
    export function goblinkin(b: BodyBuild): void
    {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", 0.5);
        const D = H * b.s("depth", 0.36);
        const legs = H * 0.36 * b.s("legs", 1);
        const chest = H * 0.7;
        const arms = H * 0.34 * b.s("arms", 1);
        const head = H * 0.22 * b.s("head", 1);
        const skin = b.c("skin", "#64834a");
        const cloth = b.c("cloth", "#654239");
        const dark = b.c("dark", "#302d2c");
        const metal = b.c("metal", "#787c78");
        const bone = b.c("bone", "#dfd5b3");
        const wood = b.c("wood", "#725b3d");
        const accent = b.c("accent", "#ba754b");
        p.bone("hips", "root", V(0, legs, 0));
        p.bone("torso", "hips", V(0, legs, 0));
        p.bone("head", "torso", V(D * 0.12, H * 0.78, 0));
        p.add("torso", 'ellipsoid', V(D * 0.72, chest - legs, W * 0.65), V(-D * 0.08, (chest + legs) / 2, 0), skin, { segments: 7 });
        p.add("hips", 'wedge', V(D * 0.65, H * 0.16, W * 0.58), V(0, legs, 0), cloth);
        p.add("hips", 'box', V(D * 0.72, H * 0.035, W * 0.61), V(0, legs + H * 0.055, 0), dark);
        p.add("head", 'ellipsoid', V(head * 0.9, head, head * 1.2), V(D * 0.1, H - head * 0.52, 0), skin, { segments: 6 });
        p.add("head", 'box', V(head * 0.9, head * 0.3 * b.s("jaw", 1), head * 0.95), V(D * 0.12 + head * 0.18, H - head * 0.9, 0), skin);
        p.add("head", 'wedge', V(head * 0.62, head * 0.38, head * 0.32), V(D * 0.12 + head * 0.5, H - head * 0.55, 0), skin, { turn: V(0, -12, 0) });
        p.add("head", 'box', V(head * 0.035, head * 0.045, head * 0.65), V(D * 0.12 + head * 0.64, H - head * 0.83, 0), dark);
        for (const side of [-1, 1])
        {
            const arm = side < 0 ? "arm_l" : "arm_r";
            const leg = side < 0 ? "leg_l" : "leg_r";
            const z = side * W * 0.39;
            p.bone(arm, "torso", V(0, chest, z));
            p.bone(leg, "hips", V(0, legs, side * W * 0.2));
            p.add(arm, 'ellipsoid', V(D * 0.38, arms * 0.92, W * 0.21), V(0, chest - arms * 0.43, z), skin, { segments: 6 });
            p.add(arm, 'box', V(D * 0.4, H * 0.075, W * 0.23), V(D * 0.08, chest - arms, z), skin);
            p.add(leg, 'cylinder', V(W * 0.18, legs * 0.86, W * 0.18), V(0, legs * 0.49, side * W * 0.2), skin, { segments: 6 });
            p.add(leg, 'box', V(D * 0.62, H * 0.05, W * 0.24), V(D * 0.14, H * 0.025, side * W * 0.2), dark);
            p.add("head", 'wedge', V(head * 0.3, head * 0.75, head * 0.75), V(D * 0.02, H - head * 0.39, side * head * 0.78), skin, { turn: V(side * 62, 0, 0) });
            p.add("head", 'box', V(head * 0.08, head * 0.16, head * 0.27), V(D * 0.1 + head * 0.43, H - head * 0.42, side * head * 0.29), dark, { turn: V(side * 15, 0, 0) });
            p.add("head", 'box', V(head * 0.09, head * 0.06, head * 0.12), V(D * 0.1 + head * 0.48, H - head * 0.43, side * head * 0.28), b.c("eye", "#e6c16b"));
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm, at:V(D * 0.1, chest - arms, z), turn:ZERO, size:H * Math.min(.55, (chest-arms)/H*1.5)});
            if (b.has("tusks")) p.add("head", 'cone', V(head * 0.16, head * 0.55, head * 0.16), V(D * 0.12 + head * 0.58, H - head * 0.7, side * head * 0.36), bone, { turn: V(0, -14, 0), segments: 5 });
            if (b.has("pauldrons")) p.add("torso", 'wedge', V(D * 0.7, H * 0.13, W * 0.32), V(0, chest, z), metal, { turn: V(side * 12, 0, 0) });
            if (b.has("stone_harness")) p.add("torso", 'box', V(D * 0.7, H * 0.2, W * 0.28), V(0, chest, z), b.c("stone", "#8d9185"), { turn: V(side * 18, 0, 0) });
            if (b.has("moss")) p.add("torso", 'cone', V(D * 0.65, H * 0.15, W * 0.3), V(-D * 0.16, chest + H * 0.03, z), b.c("moss", "#536f3e"), { segments: 5 });
            if (b.has("goggles")) p.add("head", 'cylinder', V(head * 0.32, head * 0.13, head * 0.32), V(D * 0.1 + head * 0.49, H - head * 0.4, side * head * 0.29), metal, { turn: V(0, 90, 0), segments: 8 });
        }
        if (b.has("armor")) p.add("torso", 'wedge', V(D * 0.2, (chest - legs) * 0.85, W * 0.62), V(D * 0.3, (chest + legs) / 2, 0), metal);
        if (b.has("warpaint")) p.add("torso", 'box', V(D * 0.03, H * 0.19, W * 0.09), V(D * 0.32, chest * 0.85, W * 0.12), accent, { turn: V(20, 0, 0) });
        if (b.has("robe")) p.add("hips", 'cone', V(W * 0.72, H * 0.5, W * 0.75), V(0, H * 0.3, 0), cloth, { segments: 7 });
        if (b.has("hat"))
        {
            p.add("head", 'cylinder', V(head * 1.8, H * 0.025, head * 1.8), V(0, H * 0.99, 0), cloth, { segments: 7 });
            p.add("head", 'cone', V(head * 1.2, H * 0.32, head * 1.2), V(-head * 0.16, H * 1.13, 0), cloth, { turn: V(0, 14, 0), segments: 6 });
        }
        if (b.has("crown"))
        {
            p.add("head", 'cylinder', V(head * 1.2, head * 0.15, head * 1.35), V(D * 0.1, H * 0.99, 0), accent, { segments: 6 });
            for (let i = -1; i <= 1; i++) p.add("head", 'cone', V(head * 0.18, head * (i == 0 ? 0.65 : 0.45), head * 0.18), V(head * 0.15, H + head * 0.22, i * head * 0.5), bone, { segments: 4 });
        }
        if (b.has("banner"))
        {
            p.add("torso", 'cylinder', V(H * 0.025, H * 0.8, H * 0.025), V(-D * 0.5, H * 0.9, W * 0.25), wood, { segments: 5 });
            p.add("torso", 'wedge', V(H * 0.025, H * 0.28, W * 0.52), V(-D * 0.5, H * 1.15, W * 0.4), accent);
        }
        if (b.has("sack")) p.add("torso", 'ellipsoid', V(D * 0.65, H * 0.3, W * 0.68), V(-D * 0.5, H * 0.55, 0), wood, { segments: 6 });
        if (b.has("keg"))
        {
            p.add("torso", 'cylinder', V(W * 0.68, H * 0.48, W * 0.68), V(-D * 0.4, H * 0.56, 0), wood, { segments: 8 });
            for (const y of [0.4, 0.7]) p.add("torso", 'cylinder', V(W * 0.72, H * 0.04, W * 0.72), V(-D * 0.4, H * y, 0), metal, { segments: 8 });
        }
        if (b.has("torch"))
        {
            p.add("arm_r", 'cylinder', V(H * 0.035, H * 0.32, H * 0.035), V(D * 0.15, chest - arms + H * 0.12, W * 0.39), wood, { segments: 5 });
            p.add("arm_r", 'cone', V(H * 0.1, H * 0.2, H * 0.1), V(D * 0.15, chest - arms + H * 0.36, W * 0.39), accent, { glow: true, segments: 5 });
        }
        if (b.has("sling"))
        {
            p.add("arm_r", 'box', V(H * 0.018, H * 0.35, H * 0.02), V(D * 0.12, chest - arms - H * 0.1, W * 0.39), dark);
            p.add("arm_r", 'ellipsoid', V(H * 0.12, H * 0.08, H * 0.1), V(D * 0.12, chest - arms - H * 0.28, W * 0.39), wood, { segments: 5 });
        }
        p.swings = true;
    }

    export function gnasher(b: BodyBuild): void { beast(b, false); }
    export function warbeast(b: BodyBuild): void { beast(b, true); }

    function beast(b: BodyBuild, longBody: boolean): void
    {
        const p = b.plan;
        p.rig = longBody ? "quadruped" : "biped";
        const H = b.height;
        const W = H * b.s("breadth", 0.9);
        const D = H * b.s("depth", 1.1);
        const skin = b.c("skin", "#a58155");
        const dark = b.c("dark", "#302d2c");
        const bone = b.c("bone", "#dfd5b3");
        p.bone("hips", "root", V(-D * 0.2, H * 0.4, 0));
        p.bone("torso", "hips", V(0, H * 0.55, 0));
        p.bone("head", "torso", V(D * 0.25, H * 0.65, 0));
        p.add("torso", 'ellipsoid', V(D * 0.8, H * 0.7, W * 0.95), V(-D * 0.06, H * 0.6, 0), skin, { segments: 7 });
        p.add("head", 'ellipsoid', V(D * (longBody ? 0.5 : 0.4), H * (longBody ? 0.35 : 0.5), W * (longBody ? 0.58 : 0.8)), V(D * 0.27, H * 0.67, 0), skin, { segments: 6 });
        p.add("head", 'box', V(D * 0.07, H * 0.26, W * 0.72), V(D * 0.48, H * 0.51, 0), dark);
        for (let i = -2; i <= 2; i += longBody ? 4 : 1)
        {
            p.add("head", 'cone', V(W * 0.1, H * 0.18, W * 0.1), V(D * 0.52, H * 0.48, i * W * 0.13), bone, { segments: 4 });
            p.add("head", 'cone', V(W * 0.09, H * 0.14, W * 0.09), V(D * 0.52, H * 0.59, i * W * 0.13), bone, { turn: V(180, 0, 0), segments: 4 });
        }
        for (const side of [-1, 1])
        {
            p.add("head", 'ellipsoid', V(D * 0.08, H * 0.14, W * 0.17), V(D * 0.44, H * 0.82, side * W * 0.25), b.c("eye", "#e6c16b"), { segments: 6 });
            if (longBody) p.add("head", 'cone', V(W * 0.22, H * 0.25, W * 0.18), V(D * 0.22, H * 0.99, side * W * 0.3), dark, { segments: 4 });
            const back = longBody ? (side < 0 ? "leg_bl" : "leg_br") : (side < 0 ? "leg_l" : "leg_r");
            p.bone(back, "hips", V(-D * 0.22, H * 0.4, side * W * 0.32));
            p.add(back, 'cylinder', V(W * 0.2, H * 0.4, W * 0.2), V(-D * 0.22, H * 0.23, side * W * 0.32), skin, { segments: 5 });
            p.add(back, 'box', V(D * 0.27, H * 0.07, W * 0.27), V(-D * 0.12, H * 0.04, side * W * 0.32), dark);
            if (longBody)
            {
                const front = side < 0 ? "leg_fl" : "leg_fr";
                p.bone(front, "torso", V(D * 0.2, H * 0.5, side * W * 0.3));
                p.add(front, 'cylinder', V(W * 0.18, H * 0.5, W * 0.18), V(D * 0.2, H * 0.26, side * W * 0.3), skin, { segments: 5 });
                p.add(front, 'box', V(D * 0.2, H * 0.07, W * 0.24), V(D * 0.26, H * 0.04, side * W * 0.3), dark);
            }
        }
        if (b.has("spines") || b.has("mane"))
        {
            for (let i = 0; i < 4; i++) p.add("torso", 'cone', V(D * 0.15, H * 0.22, W * 0.24), V(-D * 0.35 + i * D * 0.17, H * 0.94, 0), dark, { turn: V(0, 25, 0), segments: 4 });
        }
        if (longBody) p.add("hips", 'cone', V(W * 0.22, D * 0.34, W * 0.22), V(-D * 0.51, H * 0.55, 0), dark, {turn: V(0, 65, 0), segments: 5});
        p.swings = false;
    }
