import type { Rgb } from '../palette';
import { turnVec, v3, type BodyPlan, type BodySocket, type PartOptions, type PartShape, type Vec3 } from './bodyPlan';
import { BodyBuild } from './bodyFamilies';
import type { AppearanceDef } from './looks';

/**
 * The gear families the browser builds, the twin of the desktop's World/Logic/GearFamilies.cs: each adds
 * parts to a body plan at one of its sockets, scaled by the socket's size. A held piece hangs from the hand
 * down the body's y and the animator swings the arm bone it sits on; a dropped item uses the same builder at
 * the drop plan's root, laid flat.
 */

export class GearFit {
  laid = false;

  constructor(
    readonly plan: BodyPlan,
    readonly socket: BodySocket,
    readonly slot: string,
    readonly size: number,
    readonly held: boolean,
  ) {}

  /** A part in the socket's frame: sizes and offsets are fractions of the socket's size along the body's axes. */
  add(shape: PartShape, size: Vec3, at: Vec3, color: Rgb, options: PartOptions & { socket?: BodySocket } = {}): void {
    const s = this.size;
    let local: Vec3 = [at[0] * s, at[1] * s, at[2] * s];
    let turn = options.turn ?? [0, 0, 0];
    if (this.laid) {
      local = turnVec(local, 0, -Math.PI / 2, 0);
      turn = [turn[0], turn[1] - 90, turn[2]];
    }
    const where = options.socket ?? this.socket;
    this.plan.add(where.bone, shape, [size[0] * s, size[1] * s, size[2] * s], [where.at[0] + local[0], where.at[1] + local[1], where.at[2] + local[2]], color, {
      turn,
      taper: options.taper,
      glow: options.glow,
      segments: options.segments,
    });
  }
}

type Builder = (b: BodyBuild, f: GearFit) => void;

const V = v3;
const sides = [-1, 1];

export function socketOf(slot: string): { socket: string; held: boolean } {
  switch (slot) {
    case 'main_hand':
      return { socket: 'hand_r', held: true };
    case 'off_hand':
      return { socket: 'hand_l', held: true };
    case 'head':
      return { socket: 'head', held: false };
    case 'chest':
      return { socket: 'chest', held: false };
    case 'hands':
      return { socket: 'hand_r', held: false };
    case 'feet':
      return { socket: 'feet', held: false };
    case 'belt':
      return { socket: 'belt', held: false };
    default:
      return { socket: 'chest', held: false };
  }
}

export const swingsWith = (family: string): boolean => family === 'blade' || family === 'axe' || family === 'spear' || family === 'maul';

function blade(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 0.75) * 0.42;
  const width = b.s('width', 0.07) * 0.6;
  const guard = b.s('guard', 1);
  const metal = b.c('metal', '#b9bcc4');
  const edge = b.c('edge', '#eee9da');
  const grip = b.c('grip', '#4a3226');
  const band = b.c('band', '#b89a67');
  f.add('cylinder', V(0.028, 0.1, 0.028), V(0, 0.015, 0), grip, { segments: 6 });
  f.add('ellipsoid', V(0.04, 0.03, 0.04), V(0, 0.07, 0), band, { segments: 5 });
  if (guard > 0) {
    f.add('box', V(0.025, 0.018, 0.1 * guard), V(0, -0.04, 0), band);
  }
  f.add('box', V(0.012, length, width), V(0, -0.05 - length / 2, 0), metal);
  f.add('box', V(0.006, length * 0.95, width * 0.45), V(0.009, -0.05 - length / 2, 0), edge);
  f.add('cone', V(width, width * 0.9, 0.012), V(0, -0.05 - length - width * 0.4, 0), metal, { turn: V(180, 0, 0), segments: 4 });
}

function axe(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 0.7) * 0.42;
  const head = b.s('head', 1);
  const metal = b.c('metal', '#a4a6ac');
  const edge = b.c('edge', '#eee9da');
  const wood = b.c('wood', '#5a4634');
  const band = b.c('band', '#b89a67');
  f.add('cylinder', V(0.028, length + 0.08, 0.028), V(0, 0.04 - length / 2, 0), wood, { segments: 6 });
  f.add('cylinder', V(0.034, 0.03, 0.034), V(0, -length + 0.06, 0), band, { segments: 6 });
  f.add('wedge', V(0.11 * head, 0.14 * head, 0.03), V(0.07 * head, -length + 0.05, 0), metal, { turn: V(0, -90, 0) });
  f.add('box', V(0.012, 0.13 * head, 0.012), V(0.125 * head, -length + 0.05, 0), edge);
  if (b.s('double', 0) > 0) {
    f.add('wedge', V(0.11 * head, 0.14 * head, 0.03), V(-0.07 * head, -length + 0.05, 0), metal, { turn: V(0, 90, 0) });
  }
}

function spear(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 1.9) * 0.42;
  const head = b.s('head', 1);
  const metal = b.c('metal', '#b9bcc4');
  const wood = b.c('wood', '#6b4a2e');
  const band = b.c('band', '#b89a67');
  const centre = length * 0.18;
  f.add('cylinder', V(0.024, length, 0.024), V(0, centre, 0), wood, { segments: 6 });
  f.add('cylinder', V(0.032, 0.04, 0.032), V(0, centre + length / 2 - 0.04, 0), band, { segments: 6 });
  f.add('cone', V(0.06 * head, 0.16 * head, 0.02), V(0, centre + length / 2 + 0.07 * head, 0), metal, { segments: 4 });
}

function maul(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 1.2) * 0.42;
  const head = b.s('head', 1);
  const metal = b.c('metal', '#7c7a78');
  const wood = b.c('wood', '#4a3226');
  const band = b.c('band', '#b89a67');
  f.add('cylinder', V(0.03, length + 0.06, 0.03), V(0, 0.03 - length / 2, 0), wood, { segments: 6 });
  f.add('cylinder', V(0.038, 0.03, 0.038), V(0, 0.06, 0), band, { segments: 6 });
  if (head >= 0.8) {
    f.add('box', V(0.12 * head, 0.09 * head, 0.1 * head), V(0, -length + 0.04, 0), metal);
    f.add('box', V(0.125 * head, 0.02, 0.105 * head), V(0, -length + 0.04, 0), band);
  } else {
    f.add('ellipsoid', V(0.09, 0.16, 0.09), V(0, -length + 0.04, 0), metal, { segments: 6 });
  }
}

function bow(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 1.3) * 0.42;
  const curve = b.s('curve', 1);
  const wood = b.c('wood', '#6b4a2e');
  const string = b.c('string', '#d8d2ba');
  const band = b.c('band', '#b89a67');
  f.add('cylinder', V(0.03, length * 0.3, 0.03), V(0, 0, 0), band, { segments: 6 });
  f.add('cylinder', V(0.024, length * 0.4, 0.024), V(-0.03 * curve, length * 0.3, 0), wood, { turn: V(0, -12 * curve, 0), taper: 0.7, segments: 5 });
  f.add('cylinder', V(0.024, length * 0.4, 0.024), V(-0.03 * curve, -length * 0.3, 0), wood, { turn: V(0, 12 * curve, 0), taper: 0.7, segments: 5 });
  f.add('box', V(0.006, length * 0.9, 0.006), V(-0.09 * curve, 0, 0), string);
}

function wand(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 0.5) * 0.42;
  const wood = b.c('wood', '#3a2c28');
  const tip = b.c('tip', '#edac69');
  const band = b.c('band', '#b89a67');
  const t = b.s('tip', 1);
  f.add('cylinder', V(0.02, length, 0.02), V(0, 0.03 - length / 2, 0), wood, { taper: 0.7, segments: 5 });
  f.add('cylinder', V(0.028, 0.02, 0.028), V(0, 0.02, 0), band, { segments: 5 });
  f.add('ellipsoid', V(0.045 * t, 0.045 * t, 0.045 * t), V(0, 0.03 - length, 0), tip, { glow: true, segments: 5 });
}

function staff(b: BodyBuild, f: GearFit): void {
  const length = b.s('length', 1.8) * 0.42;
  const wood = b.c('wood', '#5a4634');
  const head = b.c('head', '#9ad6d8');
  const band = b.c('band', '#b89a67');
  const centre = length * 0.15;
  const h = b.s('head', 1);
  f.add('cylinder', V(0.03, length, 0.03), V(0, centre, 0), wood, { taper: 0.8, segments: 6 });
  f.add('cylinder', V(0.04, 0.03, 0.04), V(0, centre + length / 2 - 0.08, 0), band, { segments: 6 });
  f.add('ellipsoid', V(0.07 * h, 0.09 * h, 0.07 * h), V(0, centre + length / 2 + 0.01, 0), head, { glow: true, segments: 6 });
}

function shield(b: BodyBuild, f: GearFit): void {
  const size = b.s('size', 0.6) * 0.42;
  const face = b.c('face', '#6b4a2e');
  const rim = b.c('rim', '#b89a67');
  const boss = b.c('boss', '#8c8c90');
  const at = V(0.02, -0.04, -0.04);
  if (b.s('round', 1) > 0) {
    f.add('cylinder', V(size * 1.1, 0.02, size * 1.1), at, rim, { turn: V(90, 0, 0), segments: 10 });
    f.add('cylinder', V(size, 0.03, size), V(at[0], at[1], at[2] - 0.01), face, { turn: V(90, 0, 0), segments: 10 });
  } else {
    f.add('box', V(size * 0.8, size * 1.1, 0.03), at, face);
    f.add('box', V(size * 0.85, size * 1.15, 0.015), V(at[0], at[1], at[2] + 0.01), rim);
  }
  f.add('ellipsoid', V(size * 0.25, size * 0.25, 0.05), V(at[0], at[1], at[2] - 0.03), boss, { segments: 6 });
}

function tome(b: BodyBuild, f: GearFit): void {
  const size = b.s('size', 0.3) * 0.42;
  const cover = b.c('cover', '#5a2a2a');
  const page = b.c('page', '#eee9da');
  const band = b.c('band', '#b89a67');
  f.add('box', V(size * 0.25, size * 0.9, size * 0.7), V(0.02, -0.04, 0), cover);
  f.add('box', V(size * 0.2, size * 0.82, size * 0.62), V(0.045, -0.04, 0), page);
  f.add('box', V(size * 0.27, size * 0.12, size * 0.72), V(0.02, -0.04, 0), band);
}

function helm(b: BodyBuild, f: GearFit): void {
  const metal = b.c('metal', '#9a9ca4');
  const band = b.c('band', '#b89a67');
  const brim = b.s('brim', 0.6);
  const crest = b.s('crest', 0.4);
  const visor = b.s('visor', 1);
  f.add('ellipsoid', V(0.98, 0.7, 0.95), V(0, -0.32, 0), metal, { segments: 8 });
  if (brim > 0) {
    f.add('cylinder', V(1.05 + 0.15 * brim, 0.06, 1.0 + 0.15 * brim), V(0, -0.48, 0), band, { segments: 8 });
  }
  if (crest > 0) {
    f.add('wedge', V(0.7, 0.3 * crest, 0.1), V(-0.05, 0.08, 0), band);
  }
  if (visor > 0) {
    f.add('box', V(0.2, 0.22, 0.75), V(0.4, -0.56, 0), metal);
  }
}

function hood(b: BodyBuild, f: GearFit): void {
  const cloth = b.c('cloth', '#4a4458');
  const depth = b.s('depth', 1);
  f.add('cylinder', V(1.25, 1.25, 1.2), V(-0.2 * depth, -0.42, 0), cloth, { turn: V(0, 10, 0), taper: 0.35, segments: 7 });
  f.add('ellipsoid', V(1.1, 0.5, 1.15), V(-0.15 * depth, -0.95, 0), cloth, { segments: 7 });
}

function cuirass(b: BodyBuild, f: GearFit): void {
  const metal = b.c('metal', '#9a9ca4');
  const band = b.c('band', '#b89a67');
  const cloth = b.c('cloth', '#4a4a3e');
  const plates = b.s('plates', 1);
  const skirt = b.s('skirt', 0.6);
  f.add('box', V(0.148, 0.29, 0.245), V(0, 0.005, 0), metal);
  f.add('box', V(0.152, 0.03, 0.25), V(0, 0.08, 0), band);
  if (plates > 0.3) {
    for (const side of sides) {
      f.add('ellipsoid', V(0.11, 0.075, 0.11), V(0, 0.15, side * 0.15), metal, { segments: 6 });
    }
  }
  if (skirt > 0) {
    f.add('cylinder', V(0.26, 0.12 * skirt, 0.3), V(0, -0.18 - 0.05 * skirt, 0), cloth, { taper: 0.8, segments: 8 });
  }
}

function jerkin(b: BodyBuild, f: GearFit): void {
  const leather = b.c('leather', '#6b4a2e');
  const band = b.c('band', '#b89a67');
  f.add('box', V(0.144, 0.275, 0.24), V(0, 0, 0), leather);
  if (b.s('straps', 1) > 0) {
    f.add('box', V(0.15, 0.025, 0.26), V(0, 0.09, 0), band);
    f.add('box', V(0.15, 0.2, 0.03), V(0, 0.02, 0.03), band);
  }
}

function robe(b: BodyBuild, f: GearFit): void {
  const cloth = b.c('cloth', '#5a4a6a');
  const sash = b.c('sash', '#b89a67');
  const length = b.s('length', 1);
  f.add('box', V(0.142, 0.28, 0.238), V(0, 0, 0), cloth);
  f.add('cylinder', V(0.27, 0.38 * length, 0.27), V(0, -0.33 * length, 0), cloth, { taper: 0.75, segments: 8 });
  if (b.s('sash', 1) > 0) {
    f.add('box', V(0.15, 0.035, 0.25), V(0, -0.1, 0), sash);
  }
}

function gloves(b: BodyBuild, f: GearFit): void {
  const leather = b.c('leather', '#5a4634');
  const cuff = b.s('cuff', 1);
  for (const name of ['hand_r', 'hand_l']) {
    const hand = f.plan.sockets.get(name);
    if (hand) {
      f.add('ellipsoid', V(0.085, 0.09, 0.08), V(0, 0.01, 0), leather, { segments: 6, socket: hand });
      f.add('cylinder', V(0.08, 0.05 * cuff, 0.08), V(-0.01, 0.07, 0), leather, { taper: 1.15, segments: 6, socket: hand });
    }
  }
}

function boots(b: BodyBuild, f: GearFit): void {
  const leather = b.c('leather', '#4a3226');
  const height = b.s('height', 1);
  for (const name of ['leg_l', 'leg_r']) {
    const leg = f.plan.bones.find((x) => x.name === name);
    if (!leg) {
      continue;
    }
    const socket: BodySocket = { bone: name, at: [0, 0, leg.pivot[2]], turn: [0, 0, 0], size: f.size };
    f.add('box', V(0.14, 0.05, 0.1), V(0.025, 0.025, 0), leather, { socket });
    f.add('cylinder', V(0.1, 0.09 * height, 0.1), V(0, 0.07 * height, 0), leather, { taper: 1.1, segments: 6, socket });
  }
}

function belt(b: BodyBuild, f: GearFit): void {
  const leather = b.c('leather', '#4a3226');
  const band = b.c('band', '#b89a67');
  f.add('box', V(0.138, 0.032, 0.225), V(0, 0, 0), leather);
  f.add('box', V(0.03, 0.04, 0.035), V(0.072, 0, 0), band);
  const pouches = Math.trunc(b.s('pouches', 2));
  for (let i = 0; i < pouches; i++) {
    const side = i % 2 === 0 ? 1 : -1;
    f.add('box', V(0.05, 0.06, 0.05), V(0.02, -0.035, side * 0.1), leather);
  }
}

function sack(b: BodyBuild, f: GearFit): void {
  const cloth = b.c('cloth', '#b8a888');
  const band = b.c('band', '#6b4a2e');
  const size = b.s('size', 1) * 0.3;
  f.add('ellipsoid', V(size, size * 1.1, size), V(0, -size * 0.4, 0), cloth, { segments: 7 });
  f.add('cylinder', V(size * 0.4, size * 0.1, size * 0.4), V(0, size * 0.12, 0), band, { segments: 6 });
}

function gem(b: BodyBuild, f: GearFit): void {
  const metal = b.c('metal', '#c9a458');
  const gemC = b.c('gem', '#81c3c9');
  const size = b.s('size', 1) * 0.1;
  f.add('cylinder', V(size * 1.3, size * 0.3, size * 1.3), V(0, -size * 0.5, 0), metal, { segments: 8 });
  f.add('ellipsoid', V(size * 0.7, size * 0.7, size * 0.7), V(0, 0, 0), gemC, { glow: true, segments: 6 });
}

export const GEAR_FAMILIES: Record<string, Builder> = { blade, axe, spear, maul, bow, wand, staff, shield, tome, helm, hood, cuirass, jerkin, robe, gloves, boots, belt, sack, gem };

/** Adds a worn piece to a body; a body without the socket wears nothing there. */
export function wear(plan: BodyPlan, gear: AppearanceDef, slot: string, key: number | string): void {
  const { socket: socketName, held } = socketOf(slot);
  const socket = plan.sockets.get(socketName);
  const builder = GEAR_FAMILIES[gear.body];
  if (!socket || !builder) {
    return;
  }
  const build = new BodyBuild(gear, key, socket.size, socket.size * 0.2);
  builder(build, new GearFit(plan, socket, slot, socket.size, held));
  if (held && slot === 'main_hand') {
    plan.swings = swingsWith(gear.body);
  }
}

/** Lays a piece on the ground at a drop plan's root, as a dropped item. */
export function lay(plan: BodyPlan, gear: AppearanceDef, key: number | string): void {
  const builder = GEAR_FAMILIES[gear.body];
  if (!builder) {
    return;
  }
  const size = 1.5;
  const socket: BodySocket = { bone: 'root', at: [0, 0.06, 0], turn: [0, 0, 0], size };
  const build = new BodyBuild(gear, key, size, 0.3);
  const fit = new GearFit(plan, socket, 'drop', size, false);
  fit.laid = true;
  builder(build, fit);
}
