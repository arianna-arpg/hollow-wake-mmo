import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO } from './bodyPlan';

// Twin of KinFamilies.cs. Anatomy is selected by content, never entity ids.
export function gnoll(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", .45);
        const D = H * b.s("depth", .45);
        const muscle = b.s("muscle", 1);
        const hx = D * .12 * b.s("hunch", 1);
        const snout = b.s("snout", 1);
        const hide = b.c("hide", "#ae8951");
        const dark = b.c("dark", "#332e28");
        const bone = b.c("bone", "#ddd0aa");
        const cloth = b.c("cloth", "#554239");
        const metal = b.c("metal", "#777b78");
        const belly = b.c("belly", "#c9b894");
        const accent = b.c("accent", "#bd6848");
        p.bone("hips", "root", V(-D * .12, H * .40, 0));
        p.bone("torso", "hips", V(-D * .12, H * .40, 0));
        p.bone("head", "torso", V(hx, H * .78, 0));
        p.bone("tail", "hips", V(-D * .33, H * .40, 0));
        // High scapula and forward low skull make the posture legible from both cameras.
        p.add("torso", 'ellipsoid', V(D * .59, H * .43, W * .61), V(-D * .11, H * .63, 0), hide, { turn: V(0, 0, -15), segments: 7 });
        p.add("torso", 'ellipsoid', V(D * .59, H * .27, W * .72), V(-D * .17, H * .80, 0), hide, { segments: 6 });
        p.add("torso", 'ellipsoid', V(D * .15, H * .30, W * .40), V(D * .19, H * .62, 0), belly, { segments: 6 });
        p.add("hips", 'wedge', V(D * .47, H * .16, W * .53), V(-D * .10, H * .41, 0), cloth);
        p.add("tail", 'ellipsoid', V(D * .50, H * .065, W * .12), V(-D * .54, H * .36, 0), hide, { turn: V(0, 0, 22), segments: 5 });
        p.add("tail", 'cone', V(D * .22, H * .09, W * .16), V(-D * .76, H * .30, 0), dark, { turn: V(0, 0, 110), segments: 5 });
        p.add("head", 'ellipsoid', V(D * .42, H * .21, W * .39), V(hx, H * .80, 0), hide, { segments: 6 });
        p.add("head", 'wedge', V(D * .40 * snout, H * .095, W * .29), V(hx + D * .20, H * .77, 0), hide, { turn: V(0, 0, -8) });
        p.add("head", 'wedge', V(D * .35 * snout, H * .045, W * .25), V(hx + D * .20, H * .715, 0), belly, { turn: V(0, 0, -3) });
        p.add("head", 'box', V(D * .35 * snout, H * .025, W * .26), V(hx + D * .21, H * .743, 0), dark);
        p.add("head", 'ellipsoid', V(D * .10, H * .05, W * .22), V(hx + D * .40 * snout, H * .78, 0), dark, { segments: 5 });
        for (const side of [-1, 1])
        {
            const z = side * W * .36;
            const arm = side < 0 ? "arm_l" : "arm_r";
            const leg = side < 0 ? "leg_l" : "leg_r";
            p.bone(arm, "torso", V(-D * .03, H * .76, z));
            p.bone(leg, "hips", V(-D * .12, H * .40, side * W * .20));
            p.add(arm, 'ellipsoid', V(D * .30, H * .27, W * .20 * muscle), V(-D * .01, H * .64, z), hide, { segments: 6 });
            p.add(arm, 'ellipsoid', V(D * .23, H * .24, W * .15 * muscle), V(D * .08, H * .46, z), hide, { turn: V(0, 0, -15), segments: 6 });
            p.add(arm, 'ellipsoid', V(D * .26, H * .07, W * .21), V(D * .28, H * .34, z), dark, { segments: 5 });
            p.add(leg, 'ellipsoid', V(D * .30, H * .24, W * .26), V(-D * .04, H * .30, side * W * .20), hide, { turn: V(0, 0, -20), segments: 6 });
            p.add(leg, 'ellipsoid', V(D * .16, H * .20, W * .15), V(-D * .11, H * .15, side * W * .20), hide, { turn: V(0, 0, 20), segments: 5 });
            p.add(leg, 'ellipsoid', V(D * .36, H * .075, W * .28), V(D * .02, H * .0375, side * W * .20), dark, { segments: 6 });
            for (let toe = -1; toe <= 1; toe++) p.add(leg, 'cone', V(W * .045, D * .12, W * .045), V(D * .22, H * .025, side * W * .20 + toe * W * .07), bone, { turn: V(0, 0, -90), segments: 4 });
            p.add("head", 'ellipsoid', V(D * .07, H * .045, W * .06), V(hx + D * .15, H * .837, side * W * .165), b.c("eye", "#edba64"), { segments: 5 });
            p.add("head", 'wedge', V(D * .19, H * .035, W * .07), V(hx + D * .10, H * .867, side * W * .17), dark, { turn: V(side * 18, 0, -9) });
            if (b.has("pointed_ears"))
            {
                p.add("head", 'cone', V(D * .15, H * .19, W * .18), V(hx - D * .05, H * .935, side * W * .18), hide, { turn: V(side * 17, 0, 10), segments: 4 });
            }
            else
            {
                p.add("head", 'ellipsoid', V(D * .12, H * .115, W * .19), V(hx - D * .06, H * .92, side * W * .19), hide, { segments: 6 });
                p.add("head", 'ellipsoid', V(D * .025, H * .067, W * .12), V(hx + D * .01, H * .923, side * W * .19), dark, { segments: 5 });
            }
            for (let tooth = 0; tooth < 4; tooth++) p.add("head", 'cone', V(D * .045, H * (tooth == 3 ? .055 : .027), W * .038), V(hx + D * (.04 + tooth * .09), H * .737, side * W * .135), bone, { turn: V(0, 0, 180), segments: 4 });
            if (b.has("spots")) for (let i = 0; i < 4; i++) p.add("torso", 'ellipsoid', V(D * .12, H * .055, W * .025), V(-D * .22 + i % 2 * D * .20, H * (.65 + i / 2 * .075), side * W * .32), dark, { segments: 5 });
            if (b.has("bracers")) p.add(arm, 'cylinder', V(W * .20, H * .09, W * .20), V(D * .10, H * .41, z), metal, { segments: 6 });
            if (b.has("armor")) p.add("torso", 'wedge', V(D * .48, H * .10, W * .30), V(-D * .06, H * .79, z), metal);
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm, at:V(D * .28, H * .34, z), turn:ZERO, size:H * b.s("grip", .95)});
        }
        if (b.has("ridge")) for (let i = 0; i < 5; i++) p.add("torso", 'cone', V(D * .17, H * (.10 + i * .012), W * .15), V(-D * .35 + i * D * .055, H * (.66 + i * .06), 0), dark, { turn: V(0, 0, 30), segments: 4 });
        if (b.has("throat")) p.add("head", 'ellipsoid', V(D * .28, H * .20, W * .36), V(D * .22, H * .70, 0), belly, { segments: 6 });
        if (b.has("apron")) p.add("hips", 'wedge', V(D * .06, H * .28, W * .45), V(D * .15, H * .35, 0), accent);
        if (b.has("scars")) for (let i = 0; i < 3; i++) p.add("torso", 'box', V(D * .025, H * .08, W * .025), V(D * .22, H * .69, W * (.05 + i * .08)), accent, { turn: V(25, 0, 0) });
        kinAdornments(b);
        p.swings = true;
}

export function beastkin(b: BodyBuild): void {
        const p = b.plan;
        p.rig = "biped";
        const H = b.height;
        const W = H * b.s("breadth", .48);
        const D = H * b.s("depth", .38);
        const hip = H * b.s("legs", .50);
        const muscle = b.s("muscle", 1);
        const horn = b.s("horns", 1);
        const bull = b.has("bull");
        const hide = b.c("hide", "#95714b");
        const belly = b.c("belly", "#c9b894");
        const dark = b.c("dark", "#332e28");
        const bone = b.c("bone", "#ddd0aa");
        const metal = b.c("metal", "#777b78");
        p.bone("hips", "root", V(0, hip, 0));
        p.bone("torso", "hips", V(0, hip, 0));
        p.bone("head", "torso", V(D * .06, H * .82, 0));
        p.add("torso", 'ellipsoid', V(D * .70, H * .35, W * .71), V(-D * .05, H * .66, 0), hide, { segments: 7 });
        p.add("torso", 'ellipsoid', V(D * .15, H * .23, W * .51), V(D * .30, H * .68, 0), belly, { segments: 6 });
        p.add("hips", 'ellipsoid', V(D * .60, H * .15, W * .51), V(0, hip, 0), dark, { segments: 6 });
        p.add("hips", 'wedge', V(D * .10, H * .18, W * .27), V(D * .31, hip - H * .05, 0), b.c("cloth", "#554239"));
        p.add("head", 'ellipsoid', V(D * (bull ? .50 : .35), H * .20, W * (bull ? .43 : .32)), V(D * .09, H * .86, 0), hide, { segments: 6 });
        p.add("head", 'ellipsoid', V(D * .35, H * (bull ? .09 : .13), W * (bull ? .38 : .22)), V(D * .32, H * .81, 0), belly, { segments: 6 });
        p.add("head", 'ellipsoid', V(D * .06, H * .04, W * (bull ? .30 : .14)), V(D * .51, H * .81, 0), dark, { segments: 5 });
        for (const side of [-1, 1])
        {
            const z = side * W * .36;
            const arm = side < 0 ? "arm_l" : "arm_r";
            const leg = side < 0 ? "leg_l" : "leg_r";
            p.bone(arm, "torso", V(0, H * .77, z));
            p.bone(leg, "hips", V(0, hip, side * W * .22));
            p.add(arm, 'ellipsoid', V(D * .34, H * .23, W * .20 * muscle), V(0, H * .66, z), hide, { segments: 6 });
            p.add(arm, 'ellipsoid', V(D * .28, H * .20, W * .16 * muscle), V(D * .09, H * .50, z), hide, { turn: V(0, 0, -12), segments: 6 });
            p.add(arm, 'box', V(D * .25, H * .07, W * .20), V(D * .28, H * .39, z), dark);
            // Thigh, backward shin/hock, then forward pastern: a readable ungulate leg.
            p.add(leg, 'ellipsoid', V(D * .36, hip * .49, W * .27), V(D * .06, hip * .80, side * W * .22), hide, { turn: V(0, 0, -16), segments: 6 });
            p.add(leg, 'ellipsoid', V(D * .19, hip * .45, W * .17), V(-D * .04, hip * .49, side * W * .22), dark, { turn: V(0, 0, 28), segments: 5 });
            p.add(leg, 'ellipsoid', V(D * .22, hip * .14, W * .19), V(-D * .17, hip * .30, side * W * .22), hide, { segments: 5 });
            p.add(leg, 'ellipsoid', V(D * .14, hip * .30, W * .13), V(-D * .08, hip * .18, side * W * .22), dark, { turn: V(0, 0, -25), segments: 5 });
            for (const toe of [-1, 1]) p.add(leg, 'wedge', V(D * .34, H * .07, W * .11), V(D * .06, H * .035, side * W * .22 + toe * W * .061), b.c("hoof", "#4a4540"));
            p.add("head", 'ellipsoid', V(D * .12, H * .05, W * .26), V(-D * .02, H * .875, side * W * .26), hide, { turn: V(side * 15, 0, 0), segments: 5 });
            p.add("head", 'ellipsoid', V(D * .04, H * .025, W * .065), V(D * .25, H * .885, side * W * .15), b.c("eye", "#edba64"), { segments: 5 });
            p.add("head", 'wedge', V(D * .16, H * .035, W * .08), V(D * .18, H * .91, side * W * .15), dark, { turn: V(side * 10, 0, 0) });
            if (b.has("bull_horns"))
            {
                p.add("head", 'cylinder', V(W * .10, W * .30, W * .10), V(-D * .03, H * .94, side * W * .29), bone, { turn: V(side * 70, 0, 0), taper: .65, segments: 6 });
                p.add("head", 'cone', V(W * .075, H * .16, W * .075), V(-D * .03, H * 1.01, side * W * .43), bone, { turn: V(side * 20, 0, -15), segments: 5 });
            }
            if (b.has("swept_horns")) p.add("head", 'cone', V(W * .10, H * .27 * horn, W * .10), V(-D * .09, H * 1.02, side * W * .15), bone, { turn: V(side * 12, 0, 28), segments: 6 });
            if (b.has("ram_horns"))
            {
                p.add("head", 'ellipsoid', V(D * .43 * horn, H * .10 * horn, W * .14), V(-D * .04, H * .985, side * W * .25), bone, { segments: 6 });
                p.add("head", 'ellipsoid', V(D * .14 * horn, H * .18 * horn, W * .14), V(-D * .23 * horn, H * .92, side * W * .28), bone, { turn: V(0, 0, -12), segments: 6 });
                p.add("head", 'ellipsoid', V(D * .35 * horn, H * .07 * horn, W * .12), V(-D * .09, H * .84, side * W * .28), bone, { turn: V(0, 0, 15), segments: 6 });
                p.add("head", 'cone', V(W * .095, H * .12 * horn, W * .095), V(D * .08, H * .87, side * W * .28), bone, { turn: V(0, 0, -25), segments: 5 });
            }
            if (b.has("armor")) p.add("torso", 'wedge', V(D * .44, H * .10, W * .30), V(0, H * .79, z), metal);
            if (b.has("bracers")) p.add(arm, 'cylinder', V(W * .21, H * .10, W * .21), V(D * .09, H * .46, z), metal, { segments: 6 });
            p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm, at:V(D * .28, H * .39, z), turn:ZERO, size:H * b.s("grip", .95)});
        }
        if (b.has("beard") || bull) p.add("head", 'cone', V(D * .17, H * .18, W * .24), V(D * .20, H * .73, 0), dark, { turn: V(0, 0, 180), segments: 5 });
        if (b.has("armor")) p.add("torso", 'wedge', V(D * .10, H * .24, W * .57), V(D * .32, H * .66, 0), metal);
        if (b.has("warpaint")) for (let i = -1; i <= 1; i++) p.add("torso", 'box', V(D * .025, H * .15, W * .05), V(D * .39, H * .68, i * W * .16), b.c("accent", "#7d9e95"), { turn: V(20, 0, 0) });
        kinAdornments(b);
        p.swings = true;
}

function kinAdornments(b: BodyBuild): void {
        const p = b.plan;
        const H = b.height;
        const W = H * b.s("breadth", .48);
        const D = H * b.s("depth", .40);
        const bone = b.c("bone", "#ddd0aa");
        const cloth = b.c("cloth", "#554239");
        const wood = b.c("wood", "#72583d");
        const metal = b.c("metal", "#777b78");
        if (b.has("mantle")) p.add("torso", 'wedge', V(D * .12, H * .37, W * .76), V(-D * .40, H * .62, 0), cloth);
        if (b.has("pack")) p.add("torso", 'box', V(D * .30, H * .24, W * .48), V(-D * .43, H * .63, 0), cloth);
        if (b.has("trophies")) for (let i = -1; i <= 1; i++)
        {
            p.add("torso", 'ellipsoid', V(H * .065, H * .075, H * .065), V(D * .36, H * .65 + i % 2 * H * .03, i * W * .16), bone, { segments: 5 });
            p.add("torso", 'cone', V(H * .025, H * .07, H * .025), V(D * .38, H * .60 + i % 2 * H * .03, i * W * .16), bone, { turn: V(0, 0, 180), segments: 4 });
        }
        if (b.has("quiver")) for (let i = -1; i <= 1; i++)
        {
            p.add("torso", 'cylinder', V(H * .014, H * .48, H * .014), V(-D * .43, H * .72, i * W * .09), wood, { segments: 5 });
            p.add("torso", 'cone', V(H * .05, H * .09, H * .05), V(-D * .43, H * .97, i * W * .09), bone, { segments: 4 });
        }
        if (b.has("warhorn"))
        {
            p.add("torso", 'cone', V(H * .18, H * .37, H * .18), V(-D * .43, H * .72, W * .17), bone, { turn: V(25, 0, -20), segments: 7 });
            p.add("torso", 'cylinder', V(H * .19, H * .025, H * .19), V(-D * .36, H * .55, W * .10), metal, { turn: V(25, 0, -20), segments: 7 });
        }
        if (b.has("censer")) p.add("torso", 'ellipsoid', V(H * .10, H * .13, H * .10), V(D * .38, H * .55, W * .14), b.c("accent", "#bd6848"), { glow: true, segments: 6 });
        if (b.has("snares")) for (let i = 0; i < 3; i++) p.add("hips", 'cylinder', V(H * .11, H * .02, H * .11), V(-D * .1, H * (.41 - i * .05), W * .36), metal, { turn: V(90, 0, 0), segments: 6 });
}
