import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO } from './bodyPlan';

// Paired with World/Logic/AnatomyFamilies.cs: the same anatomy on the same animated rigs.
    export function skeleton(b: BodyBuild): void { upright(b, false); }
    export function stitched(b: BodyBuild): void { upright(b, true); }

    function skull(b: BodyBuild, bone: string, x: number, y: number, z: number, size: number): void
    {
        const p = b.plan;
        const ivory = b.c("bone", "#d8d0b8");
        const hollow = b.c("hollow", "#28252e");
        p.add(bone, 'ellipsoid', V(size * 0.85, size * 0.72, size * 0.8), V(x, y + size * 0.62, z), ivory, { segments: 6 });
        p.add(bone, 'box', V(size * 0.62, size * 0.25, size * 0.62), V(x + size * 0.08, y + size * 0.16, z), ivory);
        for (const side of [-1, 1])
        {
            p.add(bone, 'box', V(size * 0.12, size * 0.25, size * 0.25), V(x + size * 0.37, y + size * 0.57, z + side * size * 0.21), hollow);
            p.add(bone, 'box', V(size * 0.035, size * 0.07, size * 0.07), V(x + size * 0.44, y + size * 0.56, z + side * size * 0.21), b.c("glow", "#c1a1c8"), { glow: true });
        }
        p.add(bone, 'wedge', V(size * 0.1, size * 0.19, size * 0.13), V(x + size * 0.43, y + size * 0.33, z), hollow);
        for (let i = -2; i <= 2; i++)
        {
            p.add(bone, 'box', V(size * 0.09, size * 0.09, size * 0.035), V(x + size * 0.41, y + size * 0.2, z + i * size * 0.095), hollow);
        }
    }

    function upright(b: BodyBuild, flesh: boolean): void
    {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const width = b.s("width", 1);
        const legs = H * 0.46 * b.s("legs", 1);
        const torso = H * 0.3 * b.s("torso", 1);
        const shoulders = H * 0.27 * width * b.s("shoulders", 1);
        const hips = H * 0.17 * width;
        const depth = H * 0.14 * width;
        const arms = H * 0.34 * b.s("arms", 1);
        const head = H * 0.17 * b.s("head", 1);
        const chest = legs + torso;
        const hunch = b.s("hunch", 0) * 28;
        const boneColor = b.c("bone", "#d8d0b8");
        const skin = b.c("skin", "#aa8475");
        const binding = b.c("binding", "#51453f");
        const cloth = b.c("cloth", "#4a4458");
        const accent = b.c("accent", "#b89a67");
        const thick = b.s("thickness", 1);
        p.bone("hips", "root", V(0, legs, 0));
        p.bone("torso", "hips", V(0, legs, 0), V(0, -hunch, 0));
        p.bone("head", "torso", V(0, chest + H * 0.025, 0), V(0, hunch * 0.5, 0));
        p.bone("arm_l", "torso", V(0, chest, -shoulders / 2));
        p.bone("arm_r", "torso", V(0, chest, shoulders / 2));
        p.bone("leg_l", "hips", V(0, legs, -hips / 2));
        p.bone("leg_r", "hips", V(0, legs, hips / 2));
        p.add("torso", 'cylinder', V(H * 0.035 * thick, torso, H * 0.035 * thick), V(-depth * 0.3, legs + torso / 2, 0), boneColor, { segments: 6 });
        p.add("hips", 'box', V(depth * 0.72, H * 0.055, hips), V(0, legs, 0), boneColor);
        const ribs = Math.max(2, Math.min(8, Math.trunc(b.s("ribs", 4))));
        for (let i = 0; i < ribs; i++)
        {
            const y = legs + torso * (0.25 + i * 0.65 / ribs);
            const span = shoulders * (0.7 + 0.15 * i / ribs);
            p.add("torso", 'box', V(depth * 0.25, H * 0.018 * Math.min(thick, 1.3), span), V(depth * 0.38, y, 0), boneColor);
            for (const side of [-1, 1])
            {
                p.add("torso", 'box', V(depth, H * 0.018 * Math.min(thick, 1.3), H * 0.022 * thick), V(0, y, side * span / 2), boneColor);
            }
        }
        p.add("torso", 'box', V(depth * 0.18, torso * 0.67, H * 0.035 * thick), V(depth * 0.47, legs + torso * 0.57, 0), boneColor);
        if (flesh)
        {
            p.add("torso", 'ellipsoid', V(depth * 1.7, torso * 1.15, shoulders * 1.1), V(-depth * 0.25, legs + torso * 0.45, 0), skin, { segments: 8 });
            p.add("hips", 'ellipsoid', V(depth * 1.2, H * 0.15, hips * 1.35), V(0, legs, 0), skin, { segments: 6 });
            // The stitches cross the front, and a diagonal burial harness binds the grafts.
            const stitches = Math.max(0, Math.min(9, Math.trunc(b.s("stitches", 5))));
            for (let i = 0; i < stitches; i++)
            {
                p.add("torso", 'box', V(H * 0.018, H * 0.014, shoulders * 0.23), V(depth * 0.62, legs + torso * (0.18 + i * 0.7 / Math.max(1, stitches)), shoulders * 0.12), binding, { turn: V(-25, 0, 0) });
            }
            if (b.has("harness")) p.add("torso", 'box', V(H * 0.023, torso * 1.05, H * 0.045), V(depth * 0.64, legs + torso * 0.48, 0), binding, { turn: V(24, 0, 0) });
        }
        skull(b, "head", 0, chest + H * 0.025, 0, head);
        for (const side of [-1, 1])
        {
            const armBone = side < 0 ? "arm_l" : "arm_r";
            const legBone = side < 0 ? "leg_l" : "leg_r";
            const z = side * shoulders / 2;
            const armScale = 1 + (side > 0 ? b.s("asymmetry", 0) : 0);
            const ar = H * (flesh ? 0.075 : 0.025) * thick * armScale;
            const lr = H * (flesh ? 0.07 : 0.03) * thick;
            const color = flesh ? skin : boneColor;
            p.add(armBone, 'cylinder', V(ar, arms * 0.45, ar), V(0, chest - arms * 0.24, z), color, { segments: 6 });
            p.add(armBone, 'ellipsoid', V(ar * 1.5, ar * 1.5, ar * 1.5), V(0, chest - arms * 0.5, z), boneColor, { segments: 6 });
            p.add(armBone, 'cylinder', V(ar * 0.9, arms * 0.43, ar), V(H * 0.01, chest - arms * 0.75, z), color, { segments: 6 });
            p.add(armBone, 'box', V(ar * 1.6, H * 0.06 * armScale, ar * 1.7), V(H * 0.02, chest - arms, z), color);
            if (!b.has("legless"))
            {
                for (let segment = 0; segment < 2; segment++)
                {
                    p.add(legBone, 'cylinder', V(lr, legs * 0.43, lr), V(0, legs * (0.25 + segment * 0.5), side * hips / 2), color, { segments: 6 });
                }
                p.add(legBone, 'ellipsoid', V(lr * 1.6, lr * 1.6, lr * 1.6), V(0, legs * 0.5, side * hips / 2), boneColor, { segments: 6 });
                p.add(legBone, 'box', V(H * 0.12, H * 0.035, lr * 1.8), V(H * 0.03, H * 0.02, side * hips / 2), color);
            }
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", { bone: armBone, at: V(H * 0.03, chest - arms, z), turn: ZERO, size: H });
            if (b.has("bound")) p.add(armBone, 'cylinder', V(ar * 1.5, H * 0.04, ar * 1.5), V(0, chest - arms * 0.28, z), binding, { segments: 6 });
            if (b.has("spikes")) p.add("torso", 'cone', V(H * 0.055, H * 0.2, H * 0.055), V(0, chest + H * 0.06, side * shoulders * 0.56), boneColor, { turn: V(side * 25, 0, 0), segments: 5 });
            if (b.has("mantle")) p.add("torso", 'wedge', V(depth * 0.4, torso * 1.2, shoulders * 0.45), V(-depth * 0.55, legs + torso * 0.4, side * shoulders * 0.32), cloth, { turn: V(side * 12, 0, 0) });
            if (b.has("yoke")) p.add("torso", 'cylinder', V(H * 0.035, H * 0.3, H * 0.035), V(-depth * 0.5, chest + H * 0.04, side * shoulders * 0.7), accent, { segments: 5 });
            if (b.has("crystals")) p.add("torso", 'cone', V(H * 0.08, H * 0.27, H * 0.08), V(-depth * 0.5, chest, side * shoulders * 0.65), accent, { turn: V(side * 35, 0, 0), segments: 4 });
            if (b.has("vials")) p.add("hips", 'cylinder', V(H * 0.06, H * 0.13, H * 0.06), V(0, legs, side * shoulders * 0.6), accent, { segments: 6 });
        }
        if (b.has("cowl")) p.add("head", 'wedge', V(head, head * 0.55, head * 1.3), V(-head * 0.2, chest + head * 0.9, 0), cloth);
        if (b.has("beak")) p.add("head", 'cone', V(head * 0.28, head * 0.65, head * 0.3), V(head * 0.65, chest + head * 0.4, 0), binding, { turn: V(0, -90, 0), segments: 4 });
        if (b.has("crown"))
        {
            for (let i = -1; i <= 1; i++) p.add("head", 'cone', V(head * 0.16, head * (i == 0 ? 0.8 : 0.55), head * 0.16), V(0, chest + head * 1.2, i * head * 0.38), accent, { segments: 4 });
        }
        if (b.has("graft_head")) skull(b, "torso", 0, chest - head * 0.2, shoulders * 0.42, head * 0.6);
        p.sockets.set("head", { bone: "head", at: V(0, chest + head, 0), turn: ZERO, size: head });
        p.sockets.set("face", { bone: "head", at: V(head * 0.45, chest + head * 0.5, 0), turn: ZERO, size: head });
        p.sockets.set("chest", { bone: "torso", at: V(0, legs + torso * 0.5, 0), turn: ZERO, size: H });
        p.sockets.set("belt", { bone: "hips", at: V(0, legs, 0), turn: ZERO, size: H });
        p.sockets.set("feet", { bone: "root", at: ZERO, turn: ZERO, size: H });
        p.sockets.set("back", { bone: "torso", at: V(-depth, chest, 0), turn: ZERO, size: H });
        p.sockets.set("off_hand", p.sockets.get("hand_l")!);
        p.height = chest + head * (b.has("crown") ? 1.6 : 1.05);
        p.width = shoulders + H * 0.12;
    }

    export function boneQuadruped(b: BodyBuild): void
    {
        const p = b.plan;
        p.rig = "quadruped";
        const H = b.height;
        const length = H * 1.2 * b.s("length", 1);
        const width = H * 0.38 * b.s("width", 1);
        const legs = H * 0.58;
        const ivory = b.c("bone", "#cfc8b8");
        p.bone("torso", "root", V(0, legs, 0));
        p.bone("head", "torso", V(length * 0.5, legs, 0));
        p.bone("tail", "torso", V(-length * 0.55, legs, 0));
        p.add("torso", 'box', V(length, H * 0.055, H * 0.055), V(0, legs + H * 0.28, 0), ivory);
        for (let i = 0; i < 5; i++)
        {
            const x = length * (-0.3 + i * 0.14);
            for (const side of [-1, 1])
            {
                p.add("torso", 'box', V(H * 0.035, H * 0.3, H * 0.035), V(x, legs + H * 0.12, side * width / 2), ivory, { turn: V(side * 18, 0, 0) });
            }
            p.add("torso", 'box', V(H * 0.035, H * 0.035, width), V(x, legs + H * 0.25, 0), ivory);
        }
        for (const end of [-1, 1])
        {
            for (const side of [-1, 1])
            {
                const name = "leg_" + (end > 0 ? "f" : "b") + (side < 0 ? "l" : "r");
                p.bone(name, "torso", V(end * length * 0.4, legs, side * width * 0.5));
                p.add(name, 'cylinder', V(H * 0.055, legs, H * 0.055), V(end * length * 0.4, legs / 2, side * width * 0.5), ivory, { segments: 5 });
                p.add(name, 'box', V(H * 0.19, H * 0.05, H * 0.12), V(end * length * 0.4 + H * 0.04, H * 0.025, side * width * 0.5), ivory);
            }
        }
        skull(b, "head", length * 0.57, legs - H * 0.08, 0, H * 0.34);
        p.add("head", 'box', V(H * 0.25, H * 0.11, H * 0.16), V(length * 0.76, legs + H * 0.02, 0), ivory);
        p.add("tail", 'cone', V(H * 0.05, H * 0.65, H * 0.05), V(-length * 0.72, legs + H * 0.16, 0), ivory, { turn: V(0, 65, 0), segments: 5 });
        p.height = H;
        p.width = length * 1.8;
    }
