import type { Vec3 } from './bodyPlan';
import { variationUnit, type MotionLook } from './looks';

/**
 * Procedural animation, the twin of the desktop's World/Logic/Animator.cs: idle, walk, swing, cast, hit
 * reaction and death as bone turns from the motion timings. Every input is a fact the server sent or the
 * drawn motion; anticipation follows the server's cast progress and release follows the cast's end on the
 * wire. Nothing is retimed for beauty.
 */

export interface VisualState {
  rig: string;
  swings: boolean;
  height: number;
  hover: number;
  key: number | string;
  /** The server clock now, for idle phases. */
  time: number;
  /** Ground speed of the drawn body, world units per second. */
  speed: number;
  grounded: boolean;
  leaping: boolean;
  dashing: boolean;
  dead: boolean;
  deadFor: number;
  /** The walk cycle in radians, advanced by the distance the body moved over its stride. */
  walkPhase: number;
  casting: boolean;
  castProgress: number;
  castMode: string;
  releaseAge: number;
  hitAge: number;
  hitStrength: number;
  /** Where the last hit came from, as a yaw in the body's frame (zero is ahead); NaN when unknown. */
  hitFrom: number;
  leanForward: number;
  leanSide: number;
  reducedMotion: boolean;
}

export function freshVisualState(): VisualState {
  return {
    rig: 'static',
    swings: false,
    height: 1.8,
    hover: 0,
    key: 0,
    time: 0,
    speed: 0,
    grounded: true,
    leaping: false,
    dashing: false,
    dead: false,
    deadFor: 0,
    walkPhase: 0,
    casting: false,
    castProgress: 0,
    castMode: '',
    releaseAge: 1e9,
    hitAge: 1e9,
    hitStrength: 0,
    hitFrom: Number.NaN,
    leanForward: 0,
    leanSide: 0,
    reducedMotion: false,
  };
}

export interface BonePose {
  /** Degrees: roll about x, pitch about z, yaw about y. */
  turn: [number, number, number];
  offset: [number, number, number];
  scale: number;
}

export class Pose {
  readonly bones = new Map<string, BonePose>();
  flash = 0;
  opacity = 1;

  get(bone: string): BonePose {
    let p = this.bones.get(bone);
    if (!p) {
      p = { turn: [0, 0, 0], offset: [0, 0, 0], scale: 1 };
      this.bones.set(bone, p);
    }
    return p;
  }

  /** A bone's pose, or the rest pose when the animator left it alone. */
  of(bone: string): BonePose {
    return this.bones.get(bone) ?? { turn: [0, 0, 0], offset: [0, 0, 0], scale: 1 };
  }

  clear(): void {
    this.bones.clear();
    this.flash = 0;
    this.opacity = 1;
  }

  turn(bone: string, roll: number, pitch: number, yaw: number): void {
    const p = this.get(bone);
    p.turn[0] += roll;
    p.turn[1] += pitch;
    p.turn[2] += yaw;
  }

  move(bone: string, x: number, y: number, z: number): void {
    const p = this.get(bone);
    p.offset[0] += x;
    p.offset[1] += y;
    p.offset[2] += z;
  }

  scale(bone: string, s: number): void {
    this.get(bone).scale *= s;
  }
}

const ease = (x: number): number => (x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x));
const pulse = (age: number, length: number): number => (age < 0 || age >= length ? 0 : Math.sin((Math.PI * age) / length));

function swing(s: VisualState, m: MotionLook, pose: Pose): void {
  const anticipation = m.swing('anticipation', 70);
  const follow = m.swing('follow', 85);
  const release = Math.max(0.05, m.swing('release', 0.16));
  const recovery = Math.max(0.05, m.swing('recovery', 0.3));
  const lean = m.swing('lean', 10);
  if (s.casting && s.castMode !== 'guard') {
    const p = s.castMode === 'channel' ? 0.6 + 0.2 * Math.sin(s.time * 8) : ease(s.castProgress);
    pose.turn('arm_r', 0, -anticipation * p, 0);
    pose.turn('arm_l', 0, -anticipation * 0.25 * p, 0);
    pose.turn('torso', 0, 0, lean * 1.5 * p);
    pose.turn('hips', 0, lean * 0.4 * p, 0);
    return;
  }
  if (s.casting && s.castMode === 'guard') {
    pose.turn('arm_l', 0, -80, 20);
    pose.turn('arm_r', 0, -30, 0);
    pose.turn('hips', 0, 6, 0);
    return;
  }
  if (s.releaseAge < release) {
    const k = ease(s.releaseAge / release);
    pose.turn('arm_r', 0, -anticipation + (anticipation + follow) * k, 0);
    pose.turn('torso', 0, 0, lean * 1.5 - lean * 3 * k);
    pose.turn('hips', 0, -lean * k, 0);
  } else if (s.releaseAge < release + recovery) {
    const k = 1 - ease((s.releaseAge - release) / recovery);
    pose.turn('arm_r', 0, follow * k, 0);
    pose.turn('torso', 0, 0, -lean * 1.5 * k);
    pose.turn('hips', 0, -lean * k, 0);
  }
}

function gather(s: VisualState, m: MotionLook, pose: Pose): void {
  const raise = m.cast('raise', 35);
  const gatherK = m.cast('gather', 0.6);
  const release = Math.max(0.05, m.cast('release', 0.2));
  const recovery = Math.max(0.05, m.cast('recovery', 0.35));
  if (s.casting) {
    if (s.castMode === 'guard') {
      pose.turn('arm_l', 0, -90, 25);
      pose.turn('arm_r', 0, -90, -25);
      pose.turn('hips', 0, 5, 0);
      return;
    }
    const p = s.castMode === 'channel' ? 1 : ease(s.castProgress);
    const sway = s.castMode === 'channel' && !s.reducedMotion ? Math.sin(s.time * 6) * m.cast('channel_sway', 6) : 0;
    pose.turn('arm_l', 0, -raise * p - 20 * gatherK * p, 30 * gatherK * p + sway);
    pose.turn('arm_r', 0, -raise * p - 20 * gatherK * p, -30 * gatherK * p - sway);
    pose.turn('hips', 0, 4 * p, 0);
    return;
  }
  if (s.releaseAge < release) {
    const k = ease(s.releaseAge / release);
    pose.turn('arm_l', 0, -raise + (raise - 95) * k, 0);
    pose.turn('arm_r', 0, -raise + (raise - 95) * k, 0);
    pose.turn('hips', 0, 4 - 10 * k, 0);
  } else if (s.releaseAge < release + recovery) {
    const k = 1 - ease((s.releaseAge - release) / recovery);
    pose.turn('arm_l', 0, -95 * k, 0);
    pose.turn('arm_r', 0, -95 * k, 0);
    pose.turn('hips', 0, -6 * k, 0);
  }
}

function biped(s: VisualState, m: MotionLook, pose: Pose, t: number, phase: number, reduced: boolean): void {
  const H = s.height;
  const run = Math.min(1, s.speed / Math.max(0.1, m.walk('run_speed', 5)));
  if (!reduced) {
    const breath = Math.sin(t * m.idle('rate', 0.55) * Math.PI * 2 + phase);
    pose.scale('torso', 1 + breath * m.idle('breath', 0.02) * (1 - run));
    pose.turn('head', 0, 0, Math.sin(t * 0.3 + phase) * m.idle('look', 8) * (1 - run));
    pose.turn('torso', Math.sin(t * 0.4 + phase * 2) * m.idle('sway', 1.5) * (1 - run), 0, 0);
  }
  if (run > 0.01) {
    const sw = Math.sin(s.walkPhase) * m.walk('swing', 38) * run;
    const armSwing = Math.sin(s.walkPhase) * m.walk('arm_swing', 28) * run;
    pose.turn('leg_l', 0, sw, 0);
    pose.turn('leg_r', 0, -sw, 0);
    pose.turn('arm_l', 0, -armSwing, 0);
    pose.turn('arm_r', 0, armSwing, 0);
    pose.move('root', 0, Math.abs(Math.sin(s.walkPhase)) * m.walk('bob', 0.035) * H * run, 0);
    pose.turn('hips', 0, -m.walk('lean', 6) * run, 0);
  }
  if (!s.grounded || s.leaping) {
    pose.turn('leg_l', 0, -18, 0);
    pose.turn('leg_r', 0, 22, 0);
    pose.turn('arm_l', 0, -35, 0);
    pose.turn('arm_r', 0, -35, 0);
  }
  if (s.dashing) {
    pose.turn('hips', 0, -14, 0);
  }
  pose.turn('hips', s.leanSide, -s.leanForward, 0);
  if (s.swings) {
    swing(s, m, pose);
  } else {
    gather(s, m, pose);
  }
}

function quadruped(s: VisualState, m: MotionLook, pose: Pose, t: number, phase: number, reduced: boolean): void {
  const H = s.height;
  const run = Math.min(1, s.speed / Math.max(0.1, m.walk('run_speed', 5)));
  if (!reduced) {
    const breath = Math.sin(t * m.idle('rate', 0.55) * Math.PI * 2 + phase);
    pose.scale('torso', 1 + breath * m.idle('breath', 0.02) * (1 - run));
    pose.turn('head', 0, Math.sin(t * 0.35 + phase) * m.idle('look', 8) * 0.5 * (1 - run), Math.sin(t * 0.25 + phase) * m.idle('look', 8) * (1 - run));
    pose.turn('tail', 0, 0, Math.sin(t * 1.3 + phase) * 12 * (1 - run * 0.5));
  }
  if (run > 0.01) {
    const sw = Math.sin(s.walkPhase) * m.walk('swing', 38) * 0.8 * run;
    pose.turn('leg_fl', 0, sw, 0);
    pose.turn('leg_br', 0, sw, 0);
    pose.turn('leg_fr', 0, -sw, 0);
    pose.turn('leg_bl', 0, -sw, 0);
    pose.move('root', 0, Math.abs(Math.sin(s.walkPhase)) * m.walk('bob', 0.035) * H * run, 0);
    pose.turn('neck', 0, Math.sin(s.walkPhase * 2) * 4 * run, 0);
  }
  if (!s.grounded || s.leaping) {
    pose.turn('leg_fl', 0, -30, 0);
    pose.turn('leg_fr', 0, -30, 0);
    pose.turn('leg_bl', 0, 25, 0);
    pose.turn('leg_br', 0, 25, 0);
  }
  pose.turn('torso', s.leanSide, -s.leanForward * 0.5, 0);
  const anticipation = m.swing('anticipation', 70) * 0.35;
  const release = Math.max(0.05, m.swing('release', 0.16));
  const recovery = Math.max(0.05, m.swing('recovery', 0.3));
  if (s.casting) {
    const p = ease(s.castProgress);
    pose.turn('torso', 0, anticipation * 0.5 * p, 0);
    pose.turn('neck', 0, anticipation * 0.6 * p, 0);
    pose.move('root', -0.08 * H * p, 0, 0);
  } else if (s.releaseAge < release) {
    const k = ease(s.releaseAge / release);
    pose.turn('torso', 0, anticipation * 0.5 - anticipation * 1.1 * k, 0);
    pose.turn('neck', 0, anticipation * 0.6 - anticipation * 1.4 * k, 0);
    pose.move('root', -0.08 * H + 0.3 * H * k, 0.05 * H * Math.sin(k * Math.PI), 0);
  } else if (s.releaseAge < release + recovery) {
    const k = 1 - ease((s.releaseAge - release) / recovery);
    pose.turn('torso', 0, -anticipation * 0.6 * k, 0);
    pose.turn('neck', 0, -anticipation * 0.8 * k, 0);
    pose.move('root', 0.22 * H * k, 0, 0);
  }
}

function wisp(s: VisualState, m: MotionLook, pose: Pose, t: number, phase: number, reduced: boolean): void {
  const H = s.height;
  if (!reduced) {
    pose.move('root', 0, Math.sin(t * 1.1 + phase) * 0.04 * H, 0);
    pose.turn('torso', Math.sin(t * 0.6 + phase) * 4, 0, 0);
    pose.turn('head', 0, Math.sin(t * 0.5 + phase * 2) * 5, Math.sin(t * 0.3 + phase) * 8);
  }
  const run = Math.min(1, s.speed / Math.max(0.1, m.walk('run_speed', 5)));
  pose.turn('torso', 0, -12 * run - s.leanForward * 0.5, 0);
  if (s.swings) {
    swing(s, m, pose);
  } else {
    gather(s, m, pose);
  }
}

function crawler(s: VisualState, m: MotionLook, pose: Pose, t: number, phase: number, reduced: boolean): void {
  const H = s.height;
  const run = Math.min(1, s.speed / Math.max(0.1, m.walk('run_speed', 5)));
  if (!reduced) {
    pose.turn('claw_l', 0, 0, Math.sin(t * 1.7 + phase) * 10);
    pose.turn('claw_r', 0, 0, -Math.sin(t * 1.7 + phase * 2) * 10);
  }
  if (run > 0.01) {
    pose.move('root', 0, Math.abs(Math.sin(s.walkPhase * 1.5)) * 0.02 * H * run, 0);
    pose.turn('shell', Math.sin(s.walkPhase * 1.5) * 4 * run, 0, 0);
  }
  const release = Math.max(0.05, m.swing('release', 0.16));
  const recovery = Math.max(0.05, m.swing('recovery', 0.3));
  if (s.casting) {
    const p = ease(s.castProgress);
    pose.turn('claw_l', 0, 0, 40 * p);
    pose.turn('claw_r', 0, 0, -40 * p);
  } else if (s.releaseAge < release) {
    const k = ease(s.releaseAge / release);
    pose.turn('claw_l', 0, 0, 40 - 80 * k);
    pose.turn('claw_r', 0, 0, -40 + 80 * k);
    pose.move('root', 0.2 * H * k, 0, 0);
  } else if (s.releaseAge < release + recovery) {
    const k = 1 - ease((s.releaseAge - release) / recovery);
    pose.turn('claw_l', 0, 0, -40 * k);
    pose.turn('claw_r', 0, 0, 40 * k);
    pose.move('root', 0.2 * H * k, 0, 0);
  }
}

function hit(s: VisualState, m: MotionLook, pose: Pose): void {
  const flinch = Math.max(0.01, m.hit('flinch', 0.14));
  const flash = Math.max(0.01, m.hit('flash', 0.16));
  if (s.hitAge < flash) {
    pose.flash = (1 - s.hitAge / flash) * Math.max(0.35, s.hitStrength);
  }
  if (s.hitAge < flinch && !s.reducedMotion) {
    const k = pulse(s.hitAge, flinch) * (0.4 + 0.6 * s.hitStrength);
    const lean = m.hit('lean', 14) * k;
    const squash = m.hit('squash', 0.08) * k;
    const from = Number.isNaN(s.hitFrom) ? 0 : s.hitFrom;
    pose.turn('root', Math.sin(from) * lean, Math.cos(from) * lean, 0);
    pose.get('root').scale *= 1 - squash;
  } else if (s.hitAge < flinch) {
    pose.flash = Math.max(pose.flash, 1 - s.hitAge / flinch);
  }
}

function death(s: VisualState, m: MotionLook, pose: Pose): void {
  const fall = Math.max(0.05, m.death('fall', 0.55));
  const settle = Math.max(0.05, m.death('settle', 0.5));
  const k = ease(s.deadFor / fall);
  const bounce = s.deadFor > fall && s.deadFor < fall + settle && !s.reducedMotion ? Math.sin(((s.deadFor - fall) / settle) * Math.PI) * 6 : 0;
  const H = s.height;
  switch (s.rig) {
    case 'quadruped':
    case 'crawler':
      pose.turn('root', 85 * k + bounce, 0, 0);
      pose.move('root', 0, -H * 0.1 * k, H * 0.25 * k);
      break;
    case 'wisp':
      pose.move('root', 0, -(s.hover + H * 0.2) * k, 0);
      pose.turn('root', 0, 60 * k, 0);
      pose.opacity = 1 - 0.5 * k;
      break;
    case 'tree':
    case 'knot':
    case 'static':
      break;
    default:
      pose.turn('root', 0, -88 * k - bounce, 0);
      pose.move('root', -H * 0.3 * k, H * 0.08 * k, 0);
      pose.turn('arm_l', 0, -40 * k, 20 * k);
      pose.turn('arm_r', 0, -20 * k, -30 * k);
      pose.turn('head', 0, 15 * k, 20 * k);
      break;
  }
  const fadeAfter = m.death('fade_after', 20);
  const fade = Math.max(0.1, m.death('fade', 2));
  if (s.deadFor > fadeAfter) {
    pose.opacity *= Math.max(0, 1 - (s.deadFor - fadeAfter) / fade);
  }
  const sink = m.death('sink', 0.25);
  if (s.deadFor > fall + settle) {
    pose.move('root', 0, -Math.min(sink * H, (s.deadFor - fall - settle) * 0.02 * H), 0);
  }
}

/** Writes the pose for a body's state; the renderer applies it to the plan's bones over their rest turns. */
export function animate(s: VisualState, m: MotionLook, pose: Pose): void {
  pose.clear();
  if (s.dead) {
    death(s, m, pose);
    return;
  }
  const reduced = s.reducedMotion;
  const t = s.time % 3600;
  const phase = variationUnit(s.key, 7) * Math.PI * 2;
  switch (s.rig) {
    case 'biped':
      biped(s, m, pose, t, phase, reduced);
      break;
    case 'quadruped':
      quadruped(s, m, pose, t, phase, reduced);
      break;
    case 'wisp':
      wisp(s, m, pose, t, phase, reduced);
      break;
    case 'crawler':
      crawler(s, m, pose, t, phase, reduced);
      break;
    case 'tree':
      if (!reduced) {
        pose.turn('canopy', Math.sin(t * 0.7 + phase) * 0.8, Math.sin(t * 0.5 + phase * 1.3) * 0.6, 0);
      }
      break;
    case 'knot':
      if (!reduced) {
        pose.turn('root', 0, 0, Math.sin(t * 0.4 + phase) * 3);
      }
      break;
    default:
      break;
  }
  hit(s, m, pose);
}

export type { Vec3 };
