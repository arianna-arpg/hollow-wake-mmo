import { v3 as V } from './bodyPlan';
import type { BodyBuild } from './bodyFamilies';
import type { GearFit } from './gearFamilies';

/** Reusable supplies and armor on a body's chest/belt sockets; twin of FieldGearFamilies.cs. */
export function fieldKit(b: BodyBuild, f: GearFit): void {
    const W = b.s('breadth', 0.4);
    const D = b.s('depth', 0.37);
    const cloth = b.c('cloth', '#514d47');
    const metal = b.c('metal', '#929897');
    const bone = b.c('bone', '#e6d5ad');
    const wood = b.c('wood', '#765942');
    const accent = b.c('accent', '#c7a36a');
    // A complete field kit may include belt pieces. Each follows its own animated bone.
    const belt = f.plan.sockets.get('belt') ?? f.socket;
    const head = f.plan.sockets.get('head');
    if (b.has('crown') && head) for (let i = 0; i < 3; i++)
        f.add('wedge', V(D * 0.18, 0.17, W * 0.08), V(-D * 0.26 + i * D * 0.19, 0, 0), metal, { socket: head });
    if (b.has('armor'))
    {
        for (const side of [-1, 1])
            f.add('wedge', V(D * 0.44, 0.11, W * 0.30), V(-D * 0.03, 0.11, side * W * 0.37), metal);
        f.add('wedge', V(D * 0.17, 0.28, W * 0.58), V(D * 0.31, 0.01, 0), metal);
    }
    if (b.has('backplates')) for (let i = 0; i < 3; i++)
        f.add('wedge', V(D * 0.35, 0.16, W * 0.77), V(-D * 0.38, -0.11 + i * 0.13, 0), metal);
    if (b.has('robe')) f.add('cone', V(D * 0.80, 0.32, W * 0.84), V(0, -0.09, 0), cloth, { segments: 7, socket: belt });
    if (b.has('satchel')) f.add('box', V(D * 0.35, 0.27, W * 0.61), V(-D * 0.48, -0.03, 0), cloth);
    if (b.has('stonepack')) for (let i = -1; i <= 1; i++)
        f.add('ellipsoid', V(D * 0.43, 0.22, W * 0.29), V(-D * 0.48, 0.08 * (i == 0 ? 1 : 0), i * W * 0.24), metal, { segments: 5 });
    if (b.has('quiver')) for (let i = -1; i <= 1; i++)
    {
        f.add('cylinder', V(0.018, 0.53, 0.018), V(-D * 0.43, 0.10, i * W * 0.14), wood, { segments: 4 });
        f.add('wedge', V(0.06, 0.09, 0.03), V(-D * 0.43, 0.37, i * W * 0.14), bone);
    }
    if (b.has('vials')) for (let i = -1; i <= 1; i++)
        f.add('cylinder', V(0.065, 0.15, 0.065), V(D * 0.39, 0, i * W * 0.26), accent, { segments: 6, socket: belt });
    if (b.has('censer')) f.add('ellipsoid', V(D * 0.37, 0.22, W * 0.36), V(-D * 0.47, 0.18, 0), accent, { glow: true, segments: 6 });
    if (b.has('coil_yoke')) for (let i = -1; i <= 1; i += 2)
    {
        f.add('cylinder', V(0.035, 0.46, 0.035), V(-D * 0.38, 0.24, i * W * 0.40), metal, { segments: 5 });
        f.add('ellipsoid', V(0.12, 0.12, 0.12), V(-D * 0.38, 0.48, i * W * 0.40), accent, { segments: 6 });
    }
    if (b.has('drum')) f.add('cylinder', V(W * 0.52, 0.21, W * 0.52), V(D * 0.47, 0.04, 0), wood, { segments: 8, socket: belt });
    if (b.has('banner'))
    {
        f.add('cylinder', V(0.022, 0.65, 0.022), V(-D * 0.48, 0.32, W * 0.25), wood, { segments: 4 });
        f.add('wedge', V(0.018, 0.23, W * 0.62), V(-D * 0.48, 0.53, W * 0.49), accent);
    }
    // Card framing includes the worn kit, including its banner.
    const { min, max } = f.plan.bounds();
    f.plan.height = Math.max(f.plan.height, max[1]);
    f.plan.width = Math.max(f.plan.width, max[0] - min[0], max[2] - min[2]);
}
