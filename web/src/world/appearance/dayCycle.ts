import type { Obj } from '../../ui/logic/content';
import { num, str } from '../../ui/logic/content';
import { lerp, parseHex, type Rgb } from '../palette';

/**
 * The day as the browser draws it, the twin of the desktop's World/Logic/DayCycle.cs: the world's day as the
 * server reports it in enter_world and on the self state's cadence, advanced between messages by the server
 * clock, or for a server that never says, the zone clock folded by the content's day length; the sky keyframes
 * blended at that fraction, and the sun's bearing and height around noon. Nothing here is a gameplay clock: the
 * simulation's own day is the server's, and this only follows it.
 */

/** The world's day as the server reports it: how long it lasts, and the fraction it stood at when a server time was read. */
export interface ServerDay {
  lengthSeconds: number;
  fraction: number;
  atServerTime: number;
}

const wrap = (t: number): number => {
  const r = t % 1;
  return r < 0 ? r + 1 : r;
};

export interface SkyState {
  sky: Rgb;
  horizon: Rgb;
  sun: Rgb;
  sunEnergy: number;
  ambient: Rgb;
  ambientEnergy: number;
  fog: Rgb;
  fogDensity: number;
  /** Degrees above the horizon; zero when the sun is down. */
  sunPitch: number;
  /** Degrees clockwise from north. */
  sunYaw: number;
  daylight: number;
}

interface SkyKey {
  at: number;
  sky: Rgb;
  horizon: Rgb;
  sun: Rgb;
  sunEnergy: number;
  ambient: Rgb;
  ambientEnergy: number;
  fog: Rgb;
}

const isObj = (v: unknown): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);
const color = (v: unknown, fallback: string): Rgb => (typeof v === 'string' && /^#[0-9a-fA-F]{6}$/.test(v) ? parseHex(v) : parseHex(fallback));

export interface FoliageRule {
  tags: string[];
  density: number;
  color: Rgb;
  tip: Rgb;
  height: number;
}

export class DayCycle {
  private readonly keys: SkyKey[] = [];
  readonly lengthSeconds: number;
  private readonly start: number;
  private readonly noonPitch: number;
  private readonly noonYaw: number;
  private readonly daylight: number;
  private readonly hazeDensity: number;
  private readonly hazeNight: number;
  readonly foliage: FoliageRule[] = [];

  constructor(def: Obj | undefined) {
    const d = def ?? {};
    const day = isObj(d.day) ? d.day : {};
    this.lengthSeconds = Math.max(1, num(day.length_seconds, 1800));
    this.start = num(day.start, 0.3);
    const sun = isObj(d.sun) ? d.sun : {};
    this.noonPitch = num(sun.noon_pitch, 58);
    this.noonYaw = num(sun.noon_yaw, 35);
    this.daylight = num(sun.daylight, 0.6);
    const haze = isObj(d.haze) ? d.haze : {};
    this.hazeDensity = num(haze.density, 0.0007);
    this.hazeNight = num(haze.night, 1.6);
    for (const k of Array.isArray(d.sky) ? d.sky : []) {
      if (!isObj(k)) {
        continue;
      }
      this.keys.push({
        at: num(k.at),
        sky: color(k.sky, '#8ca8cc'),
        horizon: color(k.horizon, '#c8d4e2'),
        sun: color(k.sun, '#fff6e8'),
        sunEnergy: num(k.sun_energy, 1.2),
        ambient: color(k.ambient, '#9a9eac'),
        ambientEnergy: num(k.ambient_energy, 0.55),
        fog: color(k.fog, '#9eb3d1'),
      });
    }
    this.keys.sort((a, b) => a.at - b.at);
    if (this.keys.length === 0) {
      this.keys.push({ at: 0, sky: parseHex('#8ca8cc'), horizon: parseHex('#c8d4e2'), sun: parseHex('#fff6e8'), sunEnergy: 1.2, ambient: parseHex('#9a9eac'), ambientEnergy: 0.55, fog: parseHex('#9eb3d1') });
    }
    const foliage = isObj(d.foliage) ? d.foliage : {};
    for (const t of Array.isArray(foliage.tufts) ? foliage.tufts : []) {
      if (isObj(t)) {
        this.foliage.push({ tags: Array.isArray(t.tags) ? t.tags.map((x) => str(x as never)) : [], density: num(t.density, 0.4), color: color(t.color, '#6a9a40'), tip: color(t.tip, '#9ac060'), height: num(t.height, 0.35) });
      }
    }
  }

  /** The server's day, once a message carried it; null before that, and for a server that predates the field. */
  server: ServerDay | null = null;

  /** How long the day lasts: the server's length once known, else the content's. */
  get dayLengthSeconds(): number {
    return this.server?.lengthSeconds ?? this.lengthSeconds;
  }

  /**
   * The day as a message reported it: the fraction, and the length when the message carries one (enter_world does,
   * the self state does not, and a fraction alone keeps the length last reported, else the content's). The server
   * time is the client's estimate of the server clock as the message arrived; the fraction advances from it. A
   * message without a fraction changes nothing.
   */
  observeServer(lengthSeconds: number | undefined, fraction: number | undefined, atServerTime: number): void {
    if (fraction === undefined || !Number.isFinite(fraction)) {
      return;
    }
    const length = lengthSeconds ?? this.server?.lengthSeconds ?? this.lengthSeconds;
    this.server = { lengthSeconds: Math.max(0, length), fraction: wrap(fraction), atServerTime };
  }

  /** Forgets the server's day, as on entering a world: the next message that carries one sets it again, and until then the clock folds. */
  forgetServer(): void {
    this.server = null;
  }

  /**
   * The fraction of the day at a server time, from zero (midnight) through half (noon): the server's fraction advanced
   * by the server clock when the server said, else the clock folded by the content's day from its start, which agrees
   * between two zones only while their clocks do.
   */
  timeOfDay(serverTime: number): number {
    const s = this.server;
    if (s !== null) {
      if (s.lengthSeconds <= 0) return s.fraction;
      return wrap(s.fraction + (serverTime - s.atServerTime) / s.lengthSeconds);
    }
    return wrap(serverTime / this.lengthSeconds + this.start);
  }

  /** The sky at a fraction of the day, blended between the keyframes around it, wrapping at midnight. */
  at(fraction: number): SkyState {
    fraction = ((fraction % 1) + 1) % 1;
    const [a, b, t] = this.bracket(fraction);
    const c = (x: Rgb, y: Rgb) => lerp(x, y, t);
    const f = (x: number, y: number) => x + (y - x) * t;
    const fromNoon = (fraction - 0.5) / (this.daylight / 2);
    const up = Math.max(0, 1 - fromNoon * fromNoon);
    const pitch = this.noonPitch * Math.sqrt(up);
    const dayness = Math.min(1, Math.max(0, 1 - Math.abs(fromNoon)));
    const yaw = this.noonYaw + fromNoon * 70;
    const night = 1 - Math.min(1, up * 1.5);
    const fog = this.hazeDensity * (1 + (this.hazeNight - 1) * night);
    return {
      sky: c(a.sky, b.sky),
      horizon: c(a.horizon, b.horizon),
      sun: c(a.sun, b.sun),
      sunEnergy: f(a.sunEnergy, b.sunEnergy),
      ambient: c(a.ambient, b.ambient),
      ambientEnergy: f(a.ambientEnergy, b.ambientEnergy),
      fog: c(a.fog, b.fog),
      fogDensity: fog,
      sunPitch: pitch,
      sunYaw: yaw,
      daylight: dayness,
    };
  }

  private bracket(fraction: number): [SkyKey, SkyKey, number] {
    if (this.keys.length === 1) {
      return [this.keys[0], this.keys[0], 0];
    }
    for (let i = 0; i < this.keys.length - 1; i++) {
      const k0 = this.keys[i];
      const k1 = this.keys[i + 1];
      if (fraction >= k0.at && fraction <= k1.at) {
        const span = k1.at - k0.at;
        return [k0, k1, span <= 1e-6 ? 0 : (fraction - k0.at) / span];
      }
    }
    const last = this.keys[this.keys.length - 1];
    const first = this.keys[0];
    const wrap = 1 - last.at + first.at;
    const tt = wrap <= 1e-6 ? 0 : fraction >= last.at ? (fraction - last.at) / wrap : (fraction + 1 - last.at) / wrap;
    return [last, first, Math.min(1, Math.max(0, tt))];
  }
}
