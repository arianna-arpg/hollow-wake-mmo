import { faunaQuadruped, faunaBird, faunaCrab } from './faunaFamilies';
import { orc } from './orcFamilies';
import { goblinkin, gnasher, warbeast } from './goblinkinFamilies';
import { skeleton, stitched, boneQuadruped } from './anatomyFamilies';
import type { Rgb } from '../palette';
import { BodyPlan, turnVec, v3 } from './bodyPlan';
import { jitter, paletteColor, scaleOf, variationUnit, type AppearanceDef } from './looks';

/**
 * The body families the browser builds, the twin of the desktop's World/Logic/BodyFamilies.cs, part for part:
 * each turns an appearance entry and an entity's size into a plan of parts on bones. Proportions are the
 * entry's shape numbers over a family default; nothing about a particular creature is coded here.
 */

export class BodyBuild {
  readonly height: number;
  readonly radius: number;
  readonly plan: BodyPlan;

  constructor(
    readonly def: AppearanceDef,
    readonly key: number | string,
    entityHeight: number,
    entityRadius: number,
  ) {
    const scale = def.scale * scaleOf(def.variation, key);
    this.height = Math.max(0.15, entityHeight) * scale;
    this.radius = Math.max(0.1, entityRadius) * scale;
    this.plan = new BodyPlan();
    this.plan.family = def.body;
    this.plan.glow = def.glow !== null ? jitter(paletteColor({ g: def.glow }, 'g', '#ffffff'), def.variation, key) : null;
  }

  s(name: string, fallback: number): number {
    const v = this.def.shape[name];
    return v === undefined ? fallback : v;
  }

  has(feature: string): boolean {
    return this.def.features.includes(feature);
  }

  c(name: string, fallback: string): Rgb {
    return jitter(paletteColor(this.def.palette, name, fallback), this.def.variation, this.key);
  }

  u(salt: number): number {
    return variationUnit(this.key, 100 + salt);
  }
}

type Builder = (b: BodyBuild) => void;

const V = v3;
const sides = [-1, 1];

function block(b: BodyBuild): void {
  const w = b.radius * 1.4;
  b.plan.add('root', 'box', V(w, b.height, w), V(0, b.height / 2, 0), b.c('stone', '#8c8a7a'));
  b.plan.rig = 'static';
}

function biped(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'biped';
  const H = b.height;
  const width = b.s('width', 1);
  const blocky = b.s('blocky', 0) > 0.5;
  const legs = 0.47 * H * b.s('legs', 1);
  const torso = 0.3 * H * b.s('torso', 1);
  const head = 0.16 * H * b.s('head', 1);
  const neck = 0.025 * H * b.s('neck', 1);
  const shoulders = 0.28 * H * width * b.s('shoulders', 1);
  const hips = 0.18 * H * width * b.s('stance', 1);
  const depth = 0.13 * H * width;
  const arms = 0.32 * H * b.s('arms', 1);
  const armR = 0.035 * H * width;
  const legR = 0.045 * H * width;
  const hunch = b.s('hunch', 0) * 28;
  const skin = b.c('skin', '#c8a888');
  const hair = b.c('hair', '#4a3828');
  const cloth = b.c('cloth', '#75685a');
  const cloth2 = b.c('cloth2', '#5c4f44');
  const leather = b.c('leather', '#55463a');
  const eyes = b.c('eyes', '#2a2622');
  const accent = b.c('accent', '#b89a67');
  const chestY = legs + torso;
  p.bone('hips', 'root', V(0, legs, 0));
  p.bone('torso', 'hips', V(0, legs + 0.02 * H, 0), V(0, -hunch, 0));
  p.bone('head', 'torso', V(0, chestY + neck, 0), V(0, hunch * 0.6, 0));
  p.bone('arm_l', 'torso', V(0, chestY - 0.02 * H, -shoulders / 2));
  p.bone('arm_r', 'torso', V(0, chestY - 0.02 * H, shoulders / 2));
  p.bone('leg_l', 'hips', V(0, legs, -hips / 2));
  p.bone('leg_r', 'hips', V(0, legs, hips / 2));
  const torsoShape = blocky ? 'box' : 'ellipsoid';
  p.add('torso', 'box', V(depth, torso * 0.9, shoulders * 0.78), V(0, legs + torso * 0.52, 0), cloth);
  p.add('torso', torsoShape, V(depth * 1.05, 0.09 * H, shoulders * 1.02), V(0, chestY - 0.02 * H, 0), cloth2);
  const belly = b.s('belly', 0);
  if (belly > 0) {
    p.add('torso', 'ellipsoid', V(depth * (1 + belly * 0.8), torso * 0.6, shoulders * 0.7), V(depth * belly * 0.4, legs + torso * 0.36, 0), cloth);
  }
  p.add('hips', 'box', V(depth * 0.95, 0.1 * H, hips * 1.15), V(0, legs - 0.02 * H, 0), leather);
  p.add('head', blocky ? 'box' : 'ellipsoid', V(head * 0.9, head, head * 0.86), V(0, chestY + neck + head / 2, 0), skin);
  p.add('head', 'cylinder', V(head * 0.4, neck + 0.02 * H, head * 0.4), V(0, chestY + neck / 2, 0), skin, { segments: 6 });
  p.add('head', blocky ? 'box' : 'ellipsoid', V(head * 0.92, head * 0.5, head * 0.9), V(-head * 0.06, chestY + neck + head * 0.78, 0), hair);
  const eye = head * 0.14;
  p.add('head', 'box', V(eye * 0.6, eye, eye), V(head * 0.42, chestY + neck + head * 0.55, -head * 0.2), eyes);
  p.add('head', 'box', V(eye * 0.6, eye, eye), V(head * 0.42, chestY + neck + head * 0.55, head * 0.2), eyes);
  for (const side of sides) {
    const bone = side < 0 ? 'arm_l' : 'arm_r';
    const z = (side * shoulders) / 2;
    const sy = chestY - 0.02 * H;
    p.add(bone, blocky ? 'box' : 'cylinder', V(armR * 2, arms * 0.55, armR * 2), V(0, sy - arms * 0.27, z), cloth, { taper: 0.85, segments: 6 });
    p.add(bone, blocky ? 'box' : 'cylinder', V(armR * 1.8, arms * 0.5, armR * 1.8), V(0.01 * H, sy - arms * 0.78, z), skin, { taper: 0.9, segments: 6 });
    p.add(bone, 'ellipsoid', V(armR * 2.4, armR * 2.6, armR * 2.2), V(0.012 * H, sy - arms * 1.02, z), skin, { segments: 6 });
    p.sockets.set(side < 0 ? 'hand_l' : 'hand_r', { bone, at: V(0.03 * H, sy - arms * 1.02, z), turn: V(0, 0, 0), size: H });
    const lz = (side * hips) / 2;
    p.add(side < 0 ? 'leg_l' : 'leg_r', blocky ? 'box' : 'cylinder', V(legR * 2, legs * 0.95, legR * 2), V(0, legs * 0.5, lz), cloth2, { taper: 0.8, segments: 6 });
    p.add(side < 0 ? 'leg_l' : 'leg_r', 'box', V(0.13 * H, 0.045 * H, legR * 2.1), V(0.025 * H, 0.022 * H, lz), leather);
  }
  p.sockets.set('head', { bone: 'head', at: V(0, chestY + neck + head, 0), turn: V(0, 0, 0), size: head });
  p.sockets.set('face', { bone: 'head', at: V(head * 0.45, chestY + neck + head * 0.5, 0), turn: V(0, 0, 0), size: head });
  p.sockets.set('chest', { bone: 'torso', at: V(0, legs + torso * 0.5, 0), turn: V(0, 0, 0), size: H });
  p.sockets.set('belt', { bone: 'hips', at: V(0, legs + 0.01 * H, 0), turn: V(0, 0, 0), size: H });
  p.sockets.set('feet', { bone: 'root', at: V(0, 0, 0), turn: V(0, 0, 0), size: H });
  p.sockets.set('back', { bone: 'torso', at: V(-depth * 0.55, chestY - 0.04 * H, 0), turn: V(0, 0, 0), size: H });
  p.sockets.set('off_hand', { bone: 'arm_l', at: V(0.03 * H, chestY - 0.02 * H - arms * 0.85, -shoulders / 2), turn: V(0, 0, 0), size: H });
  if (b.has('ears_long')) {
    for (const side of sides) {
      p.add('head', 'cone', V(head * 0.16, head * 0.5, head * 0.26), V(-head * 0.05, chestY + neck + head * 0.72, side * head * 0.5), skin, { turn: V(side * -55, 0, 0), segments: 5 });
    }
  }
  if (b.has('snout')) {
    p.add('head', 'ellipsoid', V(head * 0.5, head * 0.34, head * 0.5), V(head * 0.5, chestY + neck + head * 0.38, 0), skin, { segments: 6 });
  }
  if (b.has('horns')) {
    for (const side of sides) {
      p.add('head', 'cone', V(head * 0.16, head * 0.5, head * 0.16), V(head * 0.05, chestY + neck + head * 1.12, side * head * 0.28), accent, { turn: V(side * 20, -15, 0), segments: 5 });
    }
  }
  if (b.has('crest')) {
    p.add('head', 'wedge', V(head * 0.5, head * 0.4, head * 0.12), V(-head * 0.1, chestY + neck + head * 1.12, 0), hair);
  }
  if (b.has('leaf_crown')) {
    for (let i = 0; i < 5; i++) {
      const a = i * 72 + b.u(i) * 20;
      p.add('head', 'cone', V(head * 0.2, head * 0.55, head * 0.08), V(0, chestY + neck + head * 1.15, 0), accent, { turn: V(30, 25, a), segments: 4 });
    }
  }
  if (b.has('tail')) {
    p.add('hips', 'cone', V(0.05 * H, 0.5 * H, 0.05 * H), V(-0.2 * H, legs - 0.05 * H, 0), skin, { turn: V(0, 110, 0), segments: 5 });
  }
  if (b.has('runes')) {
    for (let i = 0; i < 3; i++) {
      p.add('torso', 'box', V(0.01 * H, 0.04 * H, 0.025 * H), V(depth * 0.5, legs + torso * (0.3 + 0.2 * i), (i - 1) * shoulders * 0.2), accent, { glow: true });
    }
  }
  p.height = chestY + neck + head * 1.05;
  p.width = shoulders * 1.1;
}

function quadruped(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'quadruped';
  const H = b.height;
  const width = b.s('width', 1);
  const legs = 0.42 * H * b.s('legs', 1);
  const torsoH = 0.3 * H;
  const torsoL = 0.78 * H * b.s('torso', 1);
  const W = 0.3 * H * width;
  const neck = 0.18 * H * b.s('neck', 1);
  const head = 0.2 * H * b.s('head', 1);
  const snout = b.s('snout', 1);
  const hide = b.c('hide', '#8a6d4b');
  const belly = b.c('belly', '#c2a98a');
  const hoof = b.c('hoof', '#3a2e24');
  const horn = b.c('horn', '#d9cfb5');
  const eyes = b.c('eyes', '#1b1612');
  const legR = 0.045 * H * width;
  const torsoY = legs + torsoH * 0.5;
  const front = torsoL * 0.36;
  const neckBase = V(torsoL * 0.45, legs + torsoH * 0.85, 0);
  const headAt = V(neckBase[0] + neck * 0.9, neckBase[1] + neck * 0.55, 0);
  p.bone('torso', 'root', V(0, torsoY, 0));
  p.bone('neck', 'torso', neckBase);
  p.bone('head', 'neck', headAt);
  p.bone('tail', 'torso', V(-torsoL * 0.48, legs + torsoH * 0.75, 0));
  const legSpots: Array<[string, number, number]> = [
    ['leg_fl', front, -W * 0.36],
    ['leg_fr', front, W * 0.36],
    ['leg_bl', -front, -W * 0.36],
    ['leg_br', -front, W * 0.36],
  ];
  for (const [name, x, z] of legSpots) {
    p.bone(name, 'torso', V(x, legs, z));
    p.add(name, 'cylinder', V(legR * 2, legs * 0.98, legR * 2), V(x, legs * 0.5, z), hide, { taper: 0.75, segments: 6 });
    p.add(name, 'box', V(legR * 2.4, legR * 1.4, legR * 2.2), V(x + 0.01 * H, legR * 0.7, z), hoof);
  }
  p.add('torso', 'ellipsoid', V(torsoL, torsoH, W), V(0, torsoY, 0), hide);
  p.add('torso', 'ellipsoid', V(torsoL * 0.5, torsoH * 1.1 * b.s('rump', 1), W * 1.05), V(-torsoL * 0.25, torsoY + torsoH * 0.05, 0), hide);
  p.add('torso', 'ellipsoid', V(torsoL * 0.8, torsoH * 0.5, W * 0.8), V(0.02 * H, torsoY - torsoH * 0.3, 0), belly);
  const mane = b.s('mane', 0);
  if (mane > 0) {
    p.add('torso', 'ellipsoid', V(torsoL * 0.45, torsoH * 0.35 * mane, W * 0.6), V(torsoL * 0.3, legs + torsoH * 1.05, 0), b.c('mane', '#3a2c24'), { segments: 6 });
  }
  p.add('neck', 'cylinder', V(neck * 0.55, neck * 1.15, W * 0.5), V(neckBase[0] + neck * 0.45, neckBase[1] + neck * 0.28, 0), hide, { turn: V(0, -52, 0), taper: 0.8, segments: 6 });
  p.add('head', 'ellipsoid', V(head * 0.9, head * 0.8, head * 0.7), V(headAt[0] + head * 0.1, headAt[1], 0), hide, { segments: 8 });
  p.add('head', 'box', V(head * 0.6 * snout, head * 0.4, head * 0.42), V(headAt[0] + head * 0.6, headAt[1] - head * 0.12, 0), hide);
  p.add('head', 'box', V(head * 0.15, head * 0.12, head * 0.3), V(headAt[0] + head * 0.6 + head * 0.3 * snout, headAt[1] - head * 0.05, 0), hoof);
  const eye = head * 0.12;
  p.add('head', 'box', V(eye * 0.6, eye, eye), V(headAt[0] + head * 0.35, headAt[1] + head * 0.12, -head * 0.3), eyes);
  p.add('head', 'box', V(eye * 0.6, eye, eye), V(headAt[0] + head * 0.35, headAt[1] + head * 0.12, head * 0.3), eyes);
  for (const side of sides) {
    p.add('head', 'cone', V(head * 0.18, head * 0.35, head * 0.12), V(headAt[0] - head * 0.1, headAt[1] + head * 0.45, side * head * 0.28), hide, { turn: V(side * -25, 10, 0), segments: 4 });
  }
  const tail = b.s('tail', 1);
  if (b.has('tail_bushy')) {
    p.add('tail', 'ellipsoid', V(0.42 * H * tail, 0.11 * H, 0.11 * H), V(-torsoL * 0.48 - 0.18 * H * tail, legs + torsoH * 0.7, 0), hide, { turn: V(0, 20, 0), segments: 6 });
  } else if (tail > 0.01) {
    p.add('tail', 'cone', V(0.05 * H, 0.3 * H * tail, 0.05 * H), V(-torsoL * 0.48 - 0.1 * H * tail, legs + torsoH * 0.6, 0), hide, { turn: V(0, 115, 0), segments: 5 });
  }
  if (b.has('antlers')) {
    for (const side of sides) {
      const baseAt = V(headAt[0] - head * 0.05, headAt[1] + head * 0.5, side * head * 0.25);
      p.add('head', 'cone', V(head * 0.12, head * 0.9, head * 0.12), V(baseAt[0], baseAt[1] + head * 0.45, baseAt[2] + side * head * 0.1), horn, { turn: V(side * 25, -10, 0), segments: 4 });
      p.add('head', 'cone', V(head * 0.08, head * 0.5, head * 0.08), V(baseAt[0] + head * 0.1, baseAt[1] + head * 0.6, baseAt[2] + side * head * 0.3), horn, { turn: V(side * 55, 25, 0), segments: 4 });
      p.add('head', 'cone', V(head * 0.08, head * 0.45, head * 0.08), V(baseAt[0] - head * 0.15, baseAt[1] + head * 0.7, baseAt[2] + side * head * 0.25), horn, { turn: V(side * 35, -45, 0), segments: 4 });
    }
  }
  if (b.has('tusks')) {
    for (const side of sides) {
      p.add('head', 'cone', V(head * 0.1, head * 0.35, head * 0.1), V(headAt[0] + head * 0.8, headAt[1] - head * 0.1, side * head * 0.22), horn, { turn: V(side * 15, -35, 0), segments: 4 });
    }
  }
  p.sockets.set('head', { bone: 'head', at: V(headAt[0], headAt[1] + head * 0.5, 0), turn: V(0, 0, 0), size: head });
  p.sockets.set('back', { bone: 'torso', at: V(0, legs + torsoH, 0), turn: V(0, 0, 0), size: H });
  p.height = Math.max(legs + torsoH, headAt[1] + head * 0.6);
  p.width = torsoL;
}

function crawler(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'crawler';
  const H = b.height;
  const W = b.radius * 2 * b.s('width', 1);
  const shell = b.c('shell', '#314f52');
  const flesh = b.c('flesh', '#5e8a8c');
  const claw = b.c('claw', '#d8d2ba');
  const eyes = b.c('eyes', '#81c3c9');
  const shellH = H * 0.6 * b.s('shell', 1);
  p.bone('shell', 'root', V(0, H * 0.3, 0));
  p.bone('claw_l', 'shell', V(W * 0.4, H * 0.3, -W * 0.3));
  p.bone('claw_r', 'shell', V(W * 0.4, H * 0.3, W * 0.3));
  p.add('shell', 'ellipsoid', V(W * 0.9, shellH, W), V(0, H * 0.5, 0), shell, { segments: 8 });
  p.add('shell', 'ellipsoid', V(W * 0.7, H * 0.35, W * 0.8), V(0, H * 0.28, 0), flesh, { segments: 8 });
  for (let i = 0; i < 3; i++) {
    const x = (i - 1) * W * 0.3;
    for (const side of sides) {
      p.add('shell', 'cone', V(H * 0.12, W * 0.55 * b.s('legs', 1), H * 0.12), V(x, H * 0.25, side * W * 0.55), flesh, { turn: V(side * 115, 0, 0), segments: 4 });
    }
  }
  for (const side of sides) {
    const bone = side < 0 ? 'claw_l' : 'claw_r';
    p.add(bone, 'wedge', V(W * 0.4 * b.s('claws', 1), H * 0.25, W * 0.18), V(W * 0.6, H * 0.3, side * W * 0.3), claw);
    const eye = H * 0.1 * b.s('eyes', 1);
    p.add('shell', 'ellipsoid', V(eye, eye, eye), V(W * 0.4, H * 0.62, side * W * 0.15), eyes, { glow: true, segments: 6 });
  }
  p.height = H * 0.8;
  p.width = W;
}

function wisp(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'wisp';
  const H = b.height;
  const hover = H * 0.2 * b.s('hover', 1);
  p.hover = hover;
  const width = 0.3 * H * b.s('width', 1);
  const torsoH = 0.5 * H * b.s('torso', 1);
  const head = 0.16 * H * b.s('head', 1);
  const cloth = b.c('cloth', '#4a4858');
  const cloth2 = b.c('cloth2', '#2c2a36');
  const face = b.c('face', '#d9d4c0');
  const glow = b.c('glow', '#c1a1c8');
  const torsoBase = hover + H * 0.08;
  const chestY = torsoBase + torsoH;
  p.bone('torso', 'root', V(0, torsoBase, 0));
  p.bone('head', 'torso', V(0, chestY, 0));
  p.add('torso', 'cylinder', V(width, torsoH, width * 0.9), V(0, torsoBase + torsoH / 2, 0), cloth, { taper: 0.3, segments: 7 });
  p.add('torso', 'ellipsoid', V(width * 0.5, H * 0.08, width * 1.1), V(0, chestY - H * 0.02, 0), cloth2, { segments: 7 });
  const trail = b.s('trail', 1);
  for (let i = 0; i < 3; i++) {
    const a = (i - 1) * 60;
    p.add('torso', 'wedge', V(width * 0.08, H * 0.25 * trail, width * 0.3), V(0, torsoBase - H * 0.06 * trail, 0), cloth2, { turn: V(0, 180, a + 90) });
  }
  if (b.has('hood')) {
    p.add('head', 'ellipsoid', V(head * 1.1, head * 1.15, head * 1.05), V(-head * 0.05, chestY + head * 0.55, 0), cloth, { segments: 7 });
    p.add('head', 'ellipsoid', V(head * 0.55, head * 0.6, head * 0.6), V(head * 0.28, chestY + head * 0.45, 0), face, { glow: true, segments: 6 });
  } else {
    p.add('head', 'ellipsoid', V(head * 0.9, head, head * 0.85), V(0, chestY + head / 2, 0), face, { glow: true });
  }
  const eye = head * 0.14;
  p.add('head', 'box', V(eye * 0.5, eye * 0.7, eye), V(head * 0.5, chestY + head * 0.55, -head * 0.18), glow, { glow: true });
  p.add('head', 'box', V(eye * 0.5, eye * 0.7, eye), V(head * 0.5, chestY + head * 0.55, head * 0.18), glow, { glow: true });
  if (b.has('arms')) {
    const arms = 0.3 * H * b.s('arms', 1);
    const armR = 0.03 * H;
    for (const side of sides) {
      const bone = side < 0 ? 'arm_l' : 'arm_r';
      const z = side * width * 0.55;
      p.bone(bone, 'torso', V(0, chestY - H * 0.04, z));
      p.add(bone, 'cylinder', V(armR * 2, arms, armR * 2), V(0, chestY - H * 0.04 - arms / 2, z), cloth, { taper: 0.7, segments: 5 });
      p.add(bone, 'ellipsoid', V(armR * 2.4, armR * 2.4, armR * 2.2), V(0.01 * H, chestY - H * 0.04 - arms, z), face, { segments: 5 });
      p.sockets.set(side < 0 ? 'hand_l' : 'hand_r', { bone, at: V(0.02 * H, chestY - H * 0.04 - arms, z), turn: V(0, 0, 0), size: H });
    }
  }
  p.sockets.set('head', { bone: 'head', at: V(0, chestY + head, 0), turn: V(0, 0, 0), size: head });
  p.height = chestY + head * 1.15;
  p.width = width * 1.2;
}

function totem(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 0.7 * b.s('width', 1);
  const wood = b.c('wood', '#4a3226');
  const band = b.c('band', '#b89a67');
  const face = b.c('face', '#edac69');
  p.add('root', 'cylinder', V(R * 2, H * 0.92, R * 2), V(0, H * 0.46, 0), wood, { taper: 0.85, segments: 7 });
  const faces = Math.trunc(b.s('faces', 3));
  for (let i = 0; i < faces; i++) {
    const y = H * (0.2 + (0.6 * i) / Math.max(1, faces - 1));
    p.add('root', 'cylinder', V(R * 2.3, H * 0.04, R * 2.3), V(0, y - H * 0.1, 0), band, { segments: 7 });
    p.add('root', 'box', V(R * 0.5, H * 0.06, R * 0.5), V(R * 0.95, y, 0), face, { glow: true });
  }
  if (b.s('crown', 1) > 0) {
    p.add('root', 'cone', V(R * 2.2, H * 0.16, R * 2.2), V(0, H * 0.98, 0), band, { segments: 7 });
  }
  p.height = H * 1.06;
  p.width = R * 2.3;
}

function dome(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.6 * b.s('width', 1);
  const shell = b.c('shell', '#eee0a3');
  const rib = b.c('rib', '#b89a67');
  p.add('root', 'ellipsoid', V(R * 2, H * 2, R * 2), V(0, 0, 0), shell, { glow: true, segments: 10 });
  const ribs = Math.trunc(b.s('ribs', 6));
  for (let i = 0; i < ribs; i++) {
    const a = (360 * i) / ribs;
    p.add('root', 'box', V(R * 1.6, H * 0.06, 0.04), V(0, H * 0.55, 0), rib, { turn: V(0, 0, a) });
  }
  p.height = H;
  p.width = R * 2;
}

function trap(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.5 * b.s('width', 1);
  const metal = b.c('metal', '#7c7a78');
  const teeth = b.c('teeth', '#c5ae82');
  p.add('root', 'cylinder', V(R * 2, H * 0.35, R * 2), V(0, H * 0.18, 0), metal, { segments: 10 });
  const n = Math.trunc(b.s('teeth', 8));
  for (let i = 0; i < n; i++) {
    const a = (2 * Math.PI * i) / n;
    p.add('root', 'cone', V(R * 0.22, H * 0.9, R * 0.12), V(Math.cos(a) * R * 0.85, H * 0.6, Math.sin(a) * R * 0.85), teeth, { turn: V(0, 0, (-a * 180) / Math.PI), segments: 4 });
  }
  p.height = H;
  p.width = R * 2;
}

function orb(b: BodyBuild): void {
  const p = b.plan;
  const r = Math.max(0.12, b.radius * 0.6) * b.s('core', 1);
  const core = b.c('core', '#eee9da');
  const halo = b.c('halo', '#edac69');
  p.add('root', 'ellipsoid', V(r * 2, r * 2, r * 2), V(0, b.height * 0.5, 0), core, { glow: true, segments: 8 });
  p.add('root', 'ellipsoid', V(r * 3.2 * b.s('halo', 1), r * 1.6, r * 1.6), V(-r * 0.6, b.height * 0.5, 0), halo, { glow: true, segments: 6 });
  p.height = b.height;
  p.width = r * 3;
}

function tree(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'tree';
  const H = b.height;
  const trunkR = Math.max(0.12, b.radius * 0.55) * b.s('trunk', 1);
  const bark = b.c('bark', '#5a4634');
  const canopy = b.c('canopy', '#4f7a3c');
  const canopy2 = b.c('canopy2', '#6f9a4e');
  const lean = b.s('lean', 0) * 8 * (b.u(1) - 0.5) * 2;
  const layers = Math.max(1, Math.trunc(b.s('layers', 3)));
  const spread = b.s('spread', 1);
  const taper = b.s('taper', 0.7);
  const bare = b.s('bare', 0) > 0.5;
  const trunkH = bare ? H * 0.7 : H * 0.45;
  p.bone('canopy', 'root', V(0, trunkH * 0.8, 0));
  p.add('root', 'cylinder', V(trunkR * 2, trunkH, trunkR * 2), V(0, trunkH / 2, 0), bark, { turn: V(0, 0, lean), taper: 0.7, segments: 7 });
  p.add('root', 'cylinder', V(trunkR * 3, trunkH * 0.12, trunkR * 3), V(0, trunkH * 0.06, 0), bark, { taper: 0.7, segments: 7 });
  if (bare) {
    const n = 5;
    for (let i = 0; i < n; i++) {
      const a = (360 * i) / n + b.u(i) * 40;
      const l = H * (0.3 + 0.15 * b.u(10 + i)) * spread;
      p.add('canopy', 'cone', V(trunkR * 1.2, l, trunkR * 1.2), V(0, trunkH * 0.8 + l * 0.35, 0), bark, { turn: V(0, 35 + 20 * b.u(20 + i), a), segments: 5 });
      if (p.glow !== null) {
        const tip = turnVec(V(0, l * 0.78, 0), 0, ((35 + 20 * b.u(20 + i)) * Math.PI) / 180, (a * Math.PI) / 180);
        p.add('canopy', 'ellipsoid', V(trunkR * 1.4, trunkR * 1.4, trunkR * 1.4), V(tip[0], tip[1] + trunkH * 0.8, tip[2]), canopy2, { glow: true, segments: 5 });
      }
    }
    p.height = H;
    p.width = H * 0.6 * spread;
    return;
  }
  const conifer = taper <= 0.5;
  const canopyR = H * 0.3 * spread * b.s('canopy', 1);
  const canopyBase = trunkH * 0.85;
  const canopyH = H - canopyBase;
  for (let i = 0; i < layers; i++) {
    const t = layers === 1 ? 0.5 : i / (layers - 1);
    const color = i % 2 === 0 ? canopy : canopy2;
    if (conifer) {
      const r = canopyR * (1.25 - 0.75 * t);
      const h = canopyH * 0.55;
      const y = canopyBase + canopyH * (0.15 + 0.65 * t);
      p.add('canopy', 'cone', V(r * 2, h, r * 2), V(0, y, 0), color, { segments: 7 });
    } else {
      const r = canopyR * (0.75 + 0.45 * Math.sin(Math.PI * (0.2 + 0.6 * t)));
      const y = canopyBase + canopyH * (0.2 + 0.6 * t);
      const dx = (b.u(30 + i) - 0.5) * canopyR * 0.6;
      const dz = (b.u(40 + i) - 0.5) * canopyR * 0.6;
      p.add('canopy', 'ellipsoid', V(r * 2, canopyH * 0.55 * taper + r * 0.6, r * 2), V(dx, y, dz), color, { segments: 8 });
    }
  }
  p.height = H;
  p.width = canopyR * 2.4;
}

function bush(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.3 * b.s('spread', 1);
  const leaf = b.c('leaf', '#5f7a46');
  const leaf2 = b.c('leaf2', '#7f9a5c');
  const lobes = Math.max(1, Math.trunc(b.s('lobes', 4)));
  for (let i = 0; i < lobes; i++) {
    const a = (2 * Math.PI * i) / lobes + b.u(i);
    const r = R * (0.55 + 0.25 * b.u(10 + i));
    p.add('root', 'ellipsoid', V(r * 2, H * (0.7 + 0.3 * b.u(20 + i)), r * 2), V(Math.cos(a) * R * 0.4, H * 0.4, Math.sin(a) * R * 0.4), i % 2 === 0 ? leaf : leaf2, { segments: 6 });
  }
  p.add('root', 'ellipsoid', V(R * 1.4, H * 1.1, R * 1.4), V(0, H * 0.5, 0), leaf, { segments: 6 });
  p.height = H;
  p.width = R * 2;
}

function fern(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.5 * b.s('spread', 1);
  const leaf = b.c('leaf', '#4a7a3c');
  const leaf2 = b.c('leaf2', '#6f9a4e');
  const fronds = Math.max(3, Math.trunc(b.s('fronds', 6)));
  for (let i = 0; i < fronds; i++) {
    const a = (360 * i) / fronds + b.u(i) * 30;
    p.add('root', 'wedge', V(R * 1.1, H * 0.08, R * 0.35), V(0, H * 0.35, 0), i % 2 === 0 ? leaf : leaf2, { turn: V(0, 35 + 15 * b.u(10 + i), a) });
  }
  p.height = H;
  p.width = R * 2;
}

function reed(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.2 * b.s('spread', 1);
  const leaf = b.c('leaf', '#7f8a4a');
  const leaf2 = b.c('leaf2', '#b8a35a');
  const stalks = Math.max(2, Math.trunc(b.s('stalks', 7)));
  for (let i = 0; i < stalks; i++) {
    const a = (2 * Math.PI * i) / stalks + b.u(i);
    const h = H * (0.7 + 0.3 * b.u(10 + i));
    const x = Math.cos(a) * R * 0.5;
    const z = Math.sin(a) * R * 0.5;
    p.add('root', 'cylinder', V(0.05, h, 0.05), V(x, h / 2, z), i % 3 === 0 ? leaf2 : leaf, { turn: V((b.u(20 + i) - 0.5) * 16, 0, (b.u(30 + i) - 0.5) * 16), taper: 0.5, segments: 4 });
    p.add('root', 'ellipsoid', V(0.08, h * 0.18, 0.08), V(x, h * 0.95, z), leaf2, { segments: 4 });
  }
  p.height = H;
  p.width = R * 1.4;
}

function plant(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const stem = b.c('stem', '#5f7a46');
  const petal = b.c('petal', '#edac69');
  const bud = b.c('bud', '#ffd27a');
  const stemH = H * 0.7 * b.s('stem', 1);
  p.add('root', 'cylinder', V(0.06, stemH, 0.06), V(0, stemH / 2, 0), stem, { taper: 0.7, segments: 5 });
  const petals = Math.max(3, Math.trunc(b.s('petals', 5)));
  for (let i = 0; i < petals; i++) {
    const a = (360 * i) / petals;
    p.add('root', 'wedge', V(H * 0.3, 0.03, H * 0.14), V(0, stemH, 0), petal, { turn: V(0, 25, a) });
  }
  p.add('root', 'ellipsoid', V(H * 0.22, H * 0.22, H * 0.22), V(0, stemH + H * 0.08, 0), bud, { glow: true, segments: 6 });
  for (let i = 0; i < 3; i++) {
    const a = 120 * i + 30;
    p.add('root', 'wedge', V(H * 0.35, 0.03, H * 0.12), V(0, H * 0.05, 0), stem, { turn: V(0, 12, a) });
  }
  p.height = H;
  p.width = H * 0.6;
}

function knot(b: BodyBuild): void {
  const p = b.plan;
  p.rig = 'knot';
  const H = b.height;
  const R = Math.max(b.radius, H * 0.3) * b.s('spread', 1);
  const strand = b.c('strand', '#d8d2ba');
  const strand2 = b.c('strand2', '#b89a67');
  const core = b.c('core', '#eee9da');
  const loops = Math.max(2, Math.trunc(b.s('loops', 5)));
  const thick = Math.max(0.05, R * 0.14);
  for (let i = 0; i < loops; i++) {
    const yaw = (360 * i) / loops + b.u(i) * 50;
    const pitch = 20 + 50 * b.u(10 + i);
    const l = R * (1.4 + 0.6 * b.u(20 + i));
    p.add('root', 'cylinder', V(thick, l, thick), V((b.u(30 + i) - 0.5) * R * 0.5, H * (0.3 + 0.4 * b.u(40 + i)), (b.u(50 + i) - 0.5) * R * 0.5), i % 2 === 0 ? strand : strand2, { turn: V(0, pitch, yaw), segments: 5 });
  }
  p.add('root', 'ellipsoid', V(R * 0.9, H * 0.55, R * 0.9), V(0, H * 0.45, 0), strand2, { segments: 6 });
  p.add('root', 'ellipsoid', V(R * 0.35, R * 0.35, R * 0.35), V(0, H * 0.5, 0), core, { glow: p.glow !== null, segments: 6 });
  p.height = H;
  p.width = R * 2.2;
}

function rock(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius * 1.1;
  const stone = b.c('stone', '#8c8a7a');
  const stone2 = b.c('stone2', '#6e6c60');
  const fleck = b.c('fleck', '#b89a67');
  const flat = b.s('flat', 0.3);
  p.add('root', 'ellipsoid', V(R * 2, H * (1.1 - 0.3 * flat), R * 1.8), V(0, H * 0.4, 0), stone, { turn: V(0, (b.u(1) - 0.5) * 20, b.u(2) * 360), segments: 6 });
  const facets = Math.max(1, Math.trunc(2 + 2 * b.s('facets', 1)));
  for (let i = 0; i < facets; i++) {
    const a = (2 * Math.PI * i) / facets + b.u(10 + i);
    const r = R * (0.5 + 0.3 * b.u(20 + i));
    p.add('root', 'box', V(r * 1.4, H * (0.5 + 0.3 * b.u(30 + i)), r * 1.2), V(Math.cos(a) * R * 0.5, H * 0.3, Math.sin(a) * R * 0.5), i % 2 === 0 ? stone2 : stone, { turn: V(b.u(40 + i) * 30, b.u(50 + i) * 30, (a * 180) / Math.PI) });
  }
  if (b.s('flecks', 0) > 0) {
    for (let i = 0; i < 6; i++) {
      const a = (2 * Math.PI * i) / 6 + b.u(60 + i);
      p.add('root', 'box', V(R * 0.18, H * 0.12, R * 0.18), V(Math.cos(a) * R * 0.85, H * (0.3 + 0.4 * b.u(70 + i)), Math.sin(a) * R * 0.8), fleck, { turn: V(b.u(80 + i) * 40, 0, (a * 180) / Math.PI) });
    }
  }
  p.height = H;
  p.width = R * 2.2;
}

function stone(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius;
  const stoneC = b.c('stone', '#9a9484');
  const stone2 = b.c('stone2', '#6e6c60');
  const rune = b.c('rune', '#b89a67');
  const lean = b.s('lean', 0.1) * 12 * (b.u(1) - 0.5) * 2;
  p.add('root', 'cylinder', V(R * 1.6, H * 0.95, R * 1.1), V(0, H * 0.475, 0), stoneC, { turn: V(0, 0, lean), taper: b.s('taper', 0.7), segments: 5 });
  p.add('root', 'cylinder', V(R * 2.2, H * 0.08, R * 1.8), V(0, H * 0.04, 0), stone2, { segments: 6 });
  if (b.s('cap', 1) > 0) {
    p.add('root', 'box', V(R * 1.5, H * 0.08, R * 1.1), V(0, H * 0.97, 0), stone2, { turn: V(0, 0, lean) });
  }
  for (let i = 0; i < 3; i++) {
    p.add('root', 'box', V(0.02, H * 0.07, R * 0.25), V(R * 0.78, H * (0.3 + 0.2 * i), 0), rune);
  }
  p.height = H;
  p.width = R * 2.2;
}

function log(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const L = b.radius * 2.2 * b.s('length', 1);
  const bark = b.c('bark', '#5a4634');
  const wood = b.c('wood', '#c2a98a');
  const moss = b.c('moss', '#71816c');
  const r = H * 0.45;
  const yaw = b.u(1) * 360;
  const yawR = (yaw * Math.PI) / 180;
  p.add('root', 'cylinder', V(r * 2, L, r * 2), V(0, r, 0), bark, { turn: V(0, 90, yaw), taper: 0.85, segments: 7 });
  const end = turnVec(V(0, L * 0.5 + 0.01, 0), 0, Math.PI / 2, yawR);
  p.add('root', 'cylinder', V(r * 1.7, 0.04, r * 1.7), V(end[0], end[1] + r, end[2]), wood, { turn: V(0, 90, yaw), segments: 7 });
  const stubs = Math.trunc(b.s('stubs', 3));
  for (let i = 0; i < stubs; i++) {
    const along = (b.u(10 + i) - 0.5) * L * 0.7;
    const at = turnVec(V(0, along, 0), 0, Math.PI / 2, yawR);
    p.add('root', 'cone', V(r * 0.5, r * 1.4, r * 0.5), V(at[0], at[1] + r * 1.2 + r * 0.5, at[2]), bark, { turn: V((b.u(20 + i) - 0.5) * 60, 0, b.u(30 + i) * 360), segments: 4 });
  }
  const mossAt = turnVec(V(0, L * 0.1, 0), 0, Math.PI / 2, yawR);
  p.add('root', 'ellipsoid', V(L * 0.4, r * 0.5, r * 1.4), V(mossAt[0], r * 1.7 + mossAt[1], mossAt[2]), moss, { turn: V(0, 0, yaw), segments: 6 });
  p.height = r * 2;
  p.width = L;
}

function anvil(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius;
  const metal = b.c('metal', '#5c5c60');
  const wood = b.c('wood', '#4a3226');
  const band = b.c('band', '#b89a67');
  p.add('root', 'cylinder', V(R * 1.6, H * 0.5, R * 1.6), V(0, H * 0.25, 0), wood, { taper: 0.9, segments: 8 });
  p.add('root', 'cylinder', V(R * 1.7, H * 0.05, R * 1.7), V(0, H * 0.45, 0), band, { segments: 8 });
  p.add('root', 'box', V(R * 1.9 * b.s('block', 1), H * 0.3, R * 0.9), V(0, H * 0.68, 0), metal);
  p.add('root', 'box', V(R * 1.5, H * 0.12, R * 1.1), V(0, H * 0.9, 0), metal);
  p.add('root', 'cone', V(R * 0.6, R * 1.1 * b.s('horn', 1), R * 0.6), V(R * 1.35, H * 0.86, 0), metal, { turn: V(0, -90, 0), segments: 6 });
  p.height = H;
  p.width = R * 3;
}

function loom(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const W = b.radius * 1.8 * b.s('frame', 1);
  const wood = b.c('wood', '#6b4a2e');
  const thread = b.c('thread', '#d8d2ba');
  const thread2 = b.c('thread2', '#81c3c9');
  const band = b.c('band', '#b89a67');
  const post = W * 0.08;
  for (const side of sides) {
    p.add('root', 'box', V(post, H, post), V(0, H / 2, (side * W) / 2), wood);
    p.add('root', 'box', V(W * 0.5, post, post), V(0, post / 2, (side * W) / 2), wood);
  }
  p.add('root', 'box', V(post, post, W), V(0, H * 0.95, 0), wood);
  p.add('root', 'box', V(post, post, W), V(0, H * 0.25, 0), wood);
  p.add('root', 'box', V(post * 1.2, post * 0.4, W), V(0, H * 0.6, 0), band);
  const threads = Math.max(2, Math.trunc(b.s('threads', 12)));
  for (let i = 0; i < threads; i++) {
    const z = -W * 0.42 + (W * 0.84 * i) / (threads - 1);
    p.add('root', 'box', V(0.015, H * 0.68, 0.015), V(0, H * 0.6, z), i % 4 === 0 ? thread2 : thread);
  }
  p.height = H;
  p.width = W;
}

function chest(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const W = b.radius * 2;
  const D = b.radius * 1.3;
  const wood = b.c('wood', '#4a3226');
  const band = b.c('band', '#b89a67');
  const lock = b.c('lock', '#7c7a78');
  p.add('root', 'box', V(D, H * 0.62, W), V(0, H * 0.31, 0), wood);
  p.add('root', 'cylinder', V(D, W, D * 0.95), V(0, H * 0.62, 0), wood, { turn: V(90, 0, 0), segments: 8 });
  const bands = Math.max(1, Math.trunc(b.s('bands', 2)));
  for (let i = 0; i < bands; i++) {
    const z = bands === 1 ? 0 : -W * 0.3 + (W * 0.6 * i) / (bands - 1);
    p.add('root', 'box', V(D * 1.04, H * 0.64, W * 0.08), V(0, H * 0.31, z), band);
    p.add('root', 'cylinder', V(D * 1.04, W * 0.08, D * 0.99), V(0, H * 0.62, z), band, { turn: V(90, 0, 0), segments: 8 });
  }
  p.add('root', 'box', V(0.04, H * 0.18, W * 0.14), V(D * 0.52, H * 0.6, 0), lock);
  p.height = H * 1.1;
  p.width = W;
}

function shrine(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = b.radius;
  const stoneC = b.c('stone', '#d8d2ba');
  const stone2 = b.c('stone2', '#b8b2a0');
  const light = b.c('light', '#eee0a3');
  const steps = Math.max(0, Math.trunc(b.s('steps', 2)));
  let y = 0;
  for (let i = 0; i < steps; i++) {
    const w = R * (2.6 - 0.5 * i);
    p.add('root', 'box', V(w, H * 0.08, w), V(0, y + H * 0.04, 0), i % 2 === 0 ? stone2 : stoneC);
    y += H * 0.08;
  }
  for (const side of sides) {
    p.add('root', 'box', V(R * 0.5, H - y - H * 0.1, R * 0.4), V(0, y + (H - y - H * 0.1) / 2, side * R * 0.65), stoneC);
  }
  p.add('root', 'box', V(R * 0.7, H * 0.12, R * 1.9), V(0, H - H * 0.06, 0), stone2);
  p.add('root', 'wedge', V(R * 0.8, H * 0.14, R * 2.1), V(0, H + H * 0.05, 0), stoneC);
  p.add('root', 'box', V(R * 0.35, H - y - H * 0.1, R * 0.9), V(-R * 0.2, y + (H - y - H * 0.1) / 2, 0), stone2);
  if (b.s('niche', 1) > 0) {
    p.add('root', 'ellipsoid', V(R * 0.3, R * 0.3, R * 0.3), V(R * 0.05, y + (H - y) * 0.5, 0), light, { glow: true, segments: 6 });
  }
  p.height = H * 1.12;
  p.width = R * 2.6;
}

function marker(b: BodyBuild): void {
  const p = b.plan;
  const H = Math.max(1.5, 3 * b.s('height', 1));
  const R = 0.45;
  const stoneC = b.c('stone', '#d8d2ba');
  const stone2 = b.c('stone2', '#b8b2a0');
  const rune = b.c('rune', '#b89a67');
  if (b.s('knotted', 0) > 0) {
    for (let i = 0; i < 4; i++) {
      const yaw = 90 * i + b.u(i) * 30;
      p.add('root', 'cylinder', V(R * 0.5, H * 0.9, R * 0.5), V(0, H * 0.45, 0), i % 2 === 0 ? stoneC : stone2, { turn: V(0, 12 + 10 * b.u(10 + i), yaw), taper: 0.6, segments: 5 });
    }
    p.add('root', 'ellipsoid', V(R * 1.2, R * 1.2, R * 1.2), V(0, H * 0.8, 0), rune, { glow: true, segments: 6 });
  } else {
    p.add('root', 'cylinder', V(R * 1.6, H * 0.85, R * 1.2), V(0, H * 0.425, 0), stoneC, { taper: 0.8, segments: 4 });
    p.add('root', 'box', V(R * 2.2, H * 0.1, R * 1.8), V(0, H * 0.05, 0), stone2);
    if (b.s('cap', 1) > 0) {
      p.add('root', 'cone', V(R * 1.5, H * 0.2, R * 1.2), V(0, H * 0.95, 0), stone2, { segments: 4 });
    }
    for (let i = 0; i < 3; i++) {
      p.add('root', 'box', V(0.02, H * 0.08, R * 0.3), V(R * 0.72, H * (0.4 + 0.15 * i), 0), rune, { glow: true });
    }
  }
  p.height = H;
  p.width = R * 2.4;
}

function fount(b: BodyBuild): void {
  const p = b.plan;
  const H = b.height;
  const R = Math.max(0.6, b.radius) * 1.4 * b.s('basin', 1);
  const stoneC = b.c('stone', '#d8d2ba');
  const stone2 = b.c('stone2', '#b8b2a0');
  const water = b.c('water', '#314f52');
  const light = b.c('light', '#9ad6d8');
  p.add('root', 'cylinder', V(R * 2.2, H * 0.12, R * 2.2), V(0, H * 0.06, 0), stone2, { segments: 9 });
  p.add('root', 'cylinder', V(R * 2, H * 0.45, R * 2), V(0, H * 0.3, 0), stoneC, { taper: 1.08, segments: 9 });
  p.add('root', 'cylinder', V(R * 2.2, H * 0.08 * b.s('rim', 1), R * 2.2), V(0, H * 0.55, 0), stone2, { segments: 9 });
  p.add('root', 'cylinder', V(R * 1.7, 0.03, R * 1.7), V(0, H * 0.52, 0), water, { segments: 9 });
  p.add('root', 'ellipsoid', V(R * 0.6, 0.06, R * 0.6), V(0, H * 0.54, 0), light, { glow: true, segments: 8 });
  if (b.s('spill', 1) > 0) {
    p.add('root', 'cylinder', V(R * 0.3, H * 0.5, R * 0.3), V(0, H * 0.75, 0), stoneC, { taper: 0.7, segments: 6 });
    p.add('root', 'ellipsoid', V(R * 0.5, R * 0.3, R * 0.5), V(0, H * 1.02, 0), light, { glow: true, segments: 6 });
  }
  p.height = H;
  p.width = R * 2.2;
}

function drop(b: BodyBuild): void {
  const p = b.plan;
  const ring = b.c('ring', '#eee9da');
  p.add('root', 'cylinder', V(0.7, 0.03, 0.7), V(0, 0.015, 0), ring, { taper: 1, segments: 10 });
  p.height = 0.3;
  p.width = 0.7;
}

export const BODY_FAMILIES: Record<string, Builder> = {
  fauna_quadruped: faunaQuadruped,
  fauna_bird: faunaBird,
  fauna_crab: faunaCrab,
  orc,
  goblinkin,
  gnasher,
  warbeast,
  biped,
  skeleton,
  stitched,
  bone_quadruped: boneQuadruped,
  quadruped,
  crawler,
  wisp,
  totem,
  dome,
  trap,
  orb,
  tree,
  bush,
  fern,
  reed,
  plant,
  knot,
  rock,
  stone,
  log,
  anvil,
  loom,
  chest,
  shrine,
  marker,
  fount,
  drop,
};

/** Builds the plan for an entry; an unknown family gets a plain block so nothing is ever invisible. */
export function buildBody(def: AppearanceDef, key: number | string, entityHeight: number, entityRadius: number): BodyPlan {
  const build = new BodyBuild(def, key, entityHeight, entityRadius);
  build.plan.bone('root', null, [0, 0, 0]);
  (BODY_FAMILIES[def.body] ?? block)(build);
  const plan = build.plan;
  const { min, max } = plan.bounds();
  plan.height = Math.max(plan.height, max[1]);
  plan.width = Math.max(plan.width, Math.max(max[0] - min[0], max[2] - min[2]));
  return plan;
}
