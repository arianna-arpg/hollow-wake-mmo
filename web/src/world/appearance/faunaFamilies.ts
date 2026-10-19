import type { BodyBuild } from './bodyFamilies';
import { v3 as V } from './bodyPlan';

// Twin of FaunaFamilies.cs, composing existing animation rigs.
export function faunaQuadruped(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "quadruped";
        const H = b.height;
        const W = H * b.s("breadth", 0.6);
        const D = H * b.s("depth", 1.4);
        const legs = H * b.s("legs", 0.4);
        const head = H * b.s("head", 0.22);
        const torso = (H * 0.82 - legs) * 0.9;
        const tail = D * b.s("tail", 0.2) * 0.55;
        const length = D * (0.72 - b.s("tail", 0.2) * 0.4);
        const mid = -D * 0.04 + tail * 0.2;
        const y = legs + torso * 0.44;
        const skin = b.c("hide", "#9b7955");
        const belly = b.c("belly", "#c7bba0");
        const dark = b.c("dark", "#36352f");
        const horn = b.c("horn", "#d0c7ab");
        p.bone("hips", "root", V(mid - length * 0.3, legs, 0));
        p.bone("torso", "hips", V(mid, y, 0));
        p.bone("neck", "torso", V(D * 0.22, y, 0));
        p.bone("head", "neck", V(D * 0.31, H - head, 0));
        p.bone("tail", "hips", V(mid - length * 0.45, y, 0));
        p.add("torso", 'ellipsoid', V(length, torso, W * 0.90), V(mid, y, 0), skin, { segments: 8 });
        p.add("hips", 'ellipsoid', V(length * 0.45, torso * b.s("rump", 1), W * 0.87), V(mid - length * 0.29, y, 0), skin, { segments: 7 });
        const neck = Math.max(H * 0.08, H - head * 0.65 - y);
        p.add("neck", 'ellipsoid', V(D * 0.20, neck, W * 0.52), V(D * 0.23, y + neck * 0.45, 0), skin, { segments: 6 });
        p.add("head", 'ellipsoid', V(D * 0.23, head, W * 0.61), V(D * 0.32, H - head * 0.5, 0), skin, { segments: 7 });
        p.add("head", 'ellipsoid', V(D * b.s("snout", 0.16), head * 0.55, W * 0.39), V(D * 0.40, H - head * 0.72, 0), skin, { segments: 6 });
        p.add("head", 'box', V(D * 0.035, head * 0.22, W * 0.27), V(D * (0.4 + b.s("snout", 0.16) * 0.48), H - head * 0.70, 0), dark);
        for (const side of [-1, 1])
        {
            const back = side < 0 ? "leg_bl" : "leg_br";
            const front = side < 0 ? "leg_fl" : "leg_fr";
            const z = side * W * 0.31;
            const bx = mid - length * 0.30;
            const fx = mid + length * 0.29;
            p.bone(back, "hips", V(bx, legs, z));
            p.bone(front, "torso", V(fx, legs, z));
            p.add(back, 'ellipsoid', V(W * 0.34, legs * 0.70, W * 0.29), V(bx - D * 0.025, legs * 0.73, z), skin, { segments: 6 });
            p.add(back, 'cylinder', V(W * 0.13, legs * 0.68, W * 0.13), V(bx + D * 0.025, legs * 0.36, z), skin, { segments: 5 });
            p.add(front, 'cylinder', V(W * 0.17, legs * 0.96, W * 0.17), V(fx, legs * 0.51, z), skin, { segments: 6 });
            p.add(back, 'box', V(D * 0.13, H * 0.04, W * 0.24), V(bx + D * 0.045, H * 0.02, z), b.has("hooves") ? dark : skin);
            p.add(front, 'box', V(D * 0.12, H * 0.04, W * 0.22), V(fx + D * 0.035, H * 0.02, z), b.has("hooves") ? dark : skin);
            const ears = H * b.s("ears", 0.2);
            if (ears > 0)
            {
                p.add("head", b.has("round_ears") ? 'ellipsoid' : 'cone', V(head * 0.28, ears, W * 0.22), V(D * 0.26, H + ears * 0.20, side * W * 0.24), skin, { turn: V(side * 16, 0, 0), segments: 5 });
                if (b.has("long_ears")) p.add("head", 'ellipsoid', V(head * 0.11, ears * 0.70, W * 0.10), V(D * 0.29, H + ears * 0.2, side * W * 0.24), belly, { segments: 5 });
                if (b.has("tufts")) p.add("head", 'cone', V(head * 0.13, ears * 0.60, W * 0.10), V(D * 0.26, H + ears * 0.77, side * W * 0.24), dark, { segments: 4 });
            }
            p.add("head", 'ellipsoid', V(head * 0.16, head * 0.17, W * 0.07), V(D * 0.38, H - head * 0.30, side * W * 0.28), b.c("eye", "#1c2020"), { segments: 6 });
            if (b.has("frog"))
            {
                p.add("head", 'ellipsoid', V(head * 0.6, head * 0.6, W * 0.27), V(D * 0.31, H - head * 0.13, side * W * 0.28), belly, { segments: 6 });
                p.add("head", 'ellipsoid', V(head * 0.23, head * 0.28, W * 0.15), V(D * 0.36, H - head * 0.05, side * W * 0.32), dark, { segments: 6 });
            }
            if (b.has("horns")) p.add("head", 'cone', V(W * 0.17, H * 0.37, W * 0.17), V(D * 0.21, H * 1.08, side * W * 0.37), horn, { turn: V(side * 42, 38, 0), segments: 6 });
            if (b.has("swept_horns"))
            {
                p.add("head", 'cylinder', V(W * 0.16, H * 0.30, W * 0.16), V(D * 0.19, H * 1.10, side * W * 0.31), horn, { turn: V(side * 12, 24, 0), segments: 6 });
                p.add("head", 'cone', V(W * 0.14, H * 0.34, W * 0.14), V(D * 0.07, H * 1.30, side * W * 0.34), horn, { turn: V(side * 12, 68, 0), segments: 6 });
            }
            if (b.has("antlers"))
            {
                p.add("head", 'cylinder', V(H * 0.025, H * 0.38, H * 0.025), V(D * 0.24, H * 1.14, side * W * 0.40), horn, { turn: V(side * 25, 0, 0), segments: 5 });
                for (let i = 0; i < 3; i++) p.add("head", 'cone', V(H * 0.025, H * 0.15, H * 0.025), V(D * 0.24 + i * H * 0.025, H * (1.04 + i * 0.11), side * W * (0.44 + i * 0.09)), horn, { turn: V(side * 55, 20, 0), segments: 4 });
            }
            if (b.has("tusks")) p.add("head", 'cone', V(W * 0.06, H * 0.23, W * 0.06), V(D * 0.47, H - head * 0.70, side * W * 0.23), horn, { turn: V(side * 10, -25, 0), segments: 5 });
            if (b.has("spots")) for (let i = 0; i < 4; i++) p.add("torso", 'ellipsoid', V(D * 0.045, torso * 0.12, W * 0.025), V(mid - length * 0.3 + i * length * 0.18, y + torso * 0.08, side * W * 0.445), belly, { segments: 5 });
            if (b.has("stripe")) p.add("torso", 'box', V(length * 0.9, torso * 0.12, W * 0.025), V(mid, y + torso * 0.25, side * W * 0.40), belly);
        }
        if (tail > 0)
        {
            const thick = b.has("brush_tail") ? W * 0.37 : W * 0.14;
            p.add("tail", 'ellipsoid', V(tail, thick, thick), V(mid - length * 0.45 - tail * 0.39, y, 0), b.has("naked_tail") ? belly : skin, { segments: 6 });
            if (b.has("brush_tail")) p.add("tail", 'ellipsoid', V(tail * 0.30, thick * 0.8, thick * 0.8), V(mid - length * 0.45 - tail * 0.76, y, 0), belly, { segments: 5 });
        }
        if (b.has("ruff")) p.add("neck", 'ellipsoid', V(D * 0.25, torso * 0.83, W * 0.83), V(D * 0.19, y + torso * 0.20, 0), skin, { segments: 5 });
        if (b.has("hump")) p.add("torso", 'ellipsoid', V(length * 0.42, torso * 0.70, W * 0.83), V(mid + length * 0.20, y + torso * 0.30, 0), skin, { segments: 6 });
        if (b.has("bib")) p.add("neck", 'ellipsoid', V(D * 0.06, neck * 0.64, W * 0.37), V(D * 0.32, y + neck * 0.32, 0), belly, { segments: 5 });
        if (b.has("beard")) p.add("head", 'cone', V(W * 0.20, H * 0.20, W * 0.20), V(D * 0.36, H - head * 1.27, 0), dark, { turn: V(180, 0, 0), segments: 5 });
        if (b.has("ridge") || b.has("spines")) for (let i = 0; i < 6; i++) p.add("torso", 'cone', V(D * 0.10, H * 0.18, W * 0.38), V(mid - length * 0.36 + i * length * 0.14, y + torso * 0.48, 0), dark, { segments: 4 });
        if (b.has("spines"))
        {
            p.add("torso", 'ellipsoid', V(length * 1.10, torso * 1.20, W * 0.98), V(mid - length * 0.10, y, 0), dark, { segments: 8 });
            for (const side of [-1, 1])
                for (let i = 0; i < 5; i++) p.add("torso", 'cone', V(D * 0.08, H * 0.19, W * 0.18), V(mid - length * 0.38 + i * length * 0.17, y + torso * 0.37, side * W * 0.29), horn, { turn: V(side * 30, -20, 0), segments: 4 });
        }
        if (b.has("shell"))
        {
            p.add("torso", 'ellipsoid', V(length * 1.12, torso * 1.15, W), V(mid, y + torso * 0.09, 0), dark, { segments: 8 });
            for (let i = -1; i <= 1; i++) p.add("torso", 'ellipsoid', V(length * 0.28, torso * 0.20, W * 0.53), V(mid + i * length * 0.25, y + torso * 0.60, 0), skin, { segments: 5 });
        }
        p.swings = false;
}

export function faunaBird(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", 0.75);
        const D = H * b.s("depth", 1);
        const legs = H * b.s("legs", 0.25);
        const skin = b.c("hide", "#a68d61");
        const dark = b.c("dark", "#36352f");
        p.bone("hips", "root", V(-D * 0.08, legs, 0));
        p.bone("torso", "hips", V(0, H * 0.52, 0));
        p.bone("head", "torso", V(D * 0.22, H * 0.76, 0));
        p.add("torso", 'ellipsoid', V(D * 0.72, H * 0.58, W * 0.8), V(-D * 0.02, H * 0.52, 0), skin, { segments: 7 });
        p.add("head", 'ellipsoid', V(D * 0.40, H * 0.30, W * 0.56), V(D * 0.22, H * 0.85, 0), skin, { segments: 6 });
        p.add("head", 'wedge', V(D * b.s("beak", 0.2), H * 0.09, W * 0.18), V(D * 0.45, H * 0.83, 0), dark);
        for (let i = -2; i <= 2; i++) p.add("torso", 'wedge', V(D * b.s("tail", 0.3), H * (b.has("fan_tail") ? 0.48 : 0.10), W * 0.15), V(-D * 0.40, H * (b.has("fan_tail") ? 0.72 : 0.51), i * W * (b.has("fan_tail") ? 0.18 : 0.10)), dark, { turn: V(i * 15, 25, 0) });
        for (const side of [-1, 1])
        {
            const leg = side < 0 ? "leg_l" : "leg_r";
            const wing = side < 0 ? "arm_l" : "arm_r";
            p.bone(leg, "hips", V(-D * 0.02, legs, side * W * 0.23));
            p.bone(wing, "torso", V(D * 0.02, H * 0.64, side * W * 0.36));
            p.add(leg, 'cylinder', V(W * 0.055, legs, W * 0.055), V(-D * 0.02, legs * 0.52, side * W * 0.23), dark, { segments: 5 });
            p.add(leg, 'wedge', V(D * 0.20, H * 0.035, W * 0.18), V(D * 0.04, H * 0.0175, side * W * 0.23), dark);
            p.add(wing, 'wedge', V(D * 0.61, H * 0.32, W * 0.15), V(-D * 0.06, H * 0.53, side * W * 0.39), dark);
            p.add("head", 'ellipsoid', V(H * 0.06, H * 0.065, H * 0.035), V(D * 0.31, H * 0.89, side * W * 0.27), b.c("eye", "#1c2020"), { segments: 5 });
            if (b.has("spots")) for (let i = 0; i < 3; i++) p.add(wing, 'box', V(D * 0.06, H * 0.06, W * 0.015), V(-D * 0.23 + i * D * 0.14, H * 0.57, side * W * 0.47), b.c("belly", "#c7bba0"));
        }
        if (b.has("crest")) p.add("head", 'cone', V(W * 0.14, H * 0.26, W * 0.13), V(D * 0.22, H * 1.06, 0), b.c("accent", "#b85e43"), { turn: V(0, -22, 0), segments: 5 });
        if (b.has("ruff")) p.add("head", 'cone', V(W * 0.51, H * 0.25, W * 0.52), V(D * 0.12, H * 0.72, 0), dark, { turn: V(180, 0, 0), segments: 5 });
        p.swings = false;
}

export function faunaCrab(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "crawler";
        const H = b.height;
        const W = H * b.s("breadth", 1.7);
        const D = H * b.s("depth", 1.3);
        const skin = b.c("hide", "#bc8c65");
        const dark = b.c("dark", "#36352f");
        p.bone("shell", "root", V(0, H * 0.52, 0));
        p.add("shell", 'ellipsoid', V(D * 0.68, H * 0.50, W * 0.63), V(-D * 0.06, H * 0.53, 0), skin, { segments: 8 });
        for (const side of [-1, 1])
        {
            const claw = side < 0 ? "claw_l" : "claw_r";
            const scale = b.s("claw", 1) * (b.has("fiddler") && side > 0 ? 1.45 : 1);
            p.bone(claw, "shell", V(D * 0.20, H * 0.40, side * W * 0.24));
            p.add(claw, 'cylinder', V(W * 0.10, D * 0.30, W * 0.10), V(D * 0.29, H * 0.40, side * W * 0.30), skin, { turn: V(0, 75, 0), segments: 5 });
            p.add(claw, 'ellipsoid', V(D * 0.28 * scale, H * 0.23 * scale, W * 0.14 * scale), V(D * 0.43, H * 0.43, side * W * 0.34), skin, { segments: 6 });
            for (let jaw = -1; jaw <= 1; jaw += 2) p.add(claw, 'cone', V(W * 0.07, D * 0.22 * scale, W * 0.07), V(D * 0.55, H * 0.43, side * W * 0.34 + jaw * W * 0.045), dark, { turn: V(0, 90, 0), segments: 4 });
            p.add("shell", 'cylinder', V(W * 0.035, H * 0.30, W * 0.035), V(D * 0.18, H * 0.80, side * W * 0.16), skin, { segments: 5 });
            p.add("shell", 'ellipsoid', V(W * 0.08, H * 0.12, W * 0.08), V(D * 0.18, H * 0.96, side * W * 0.16), b.c("eye", "#1c2020"), { segments: 6 });
            for (let i = 0; i < 3; i++)
            {
                p.add("shell", 'cylinder', V(W * 0.045, W * 0.32, W * 0.045), V(-D * 0.26 + i * D * 0.18, H * 0.34, side * W * 0.34), skin, { turn: V(side * 60, 0, 0), segments: 4 });
                p.add("shell", 'cone', V(W * 0.05, H * 0.28, W * 0.05), V(-D * 0.26 + i * D * 0.18, H * 0.14, side * W * 0.46), dark, { turn: V(180, 0, 0), segments: 4 });
            }
        }
        p.swings = false;
}
