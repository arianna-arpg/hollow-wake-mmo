/**
 * Snapshot interpolation for entities other than the player's own: poses are kept with the server time they
 * were taken at, and drawn a little in the past so there are usually two snapshots to blend between. Past
 * the newest snapshot the pose runs on with its velocity for a short while, then holds.
 */

export interface Pose {
  x: number;
  y: number;
  z: number;
  yaw: number;
}

export interface Sample extends Pose {
  /** Server time in seconds. */
  t: number;
  vx: number;
  vy: number;
  vz: number;
}

const MAX_SAMPLES = 12;

export function pushSample(samples: Sample[], sample: Sample): void {
  // Out-of-order or repeated snapshots (a re-spawn, a resent update) replace what they overlap.
  while (samples.length > 0 && samples[samples.length - 1].t >= sample.t) {
    samples.pop();
  }
  samples.push(sample);
  if (samples.length > MAX_SAMPLES) {
    samples.splice(0, samples.length - MAX_SAMPLES);
  }
}

export function lerpAngle(a: number, b: number, t: number): number {
  let d = (b - a) % (Math.PI * 2);
  if (d > Math.PI) {
    d -= Math.PI * 2;
  } else if (d < -Math.PI) {
    d += Math.PI * 2;
  }
  return a + d * t;
}

/** The pose at server time t (seconds). Extrapolation past the newest sample is capped at maxExtrapolation seconds. */
export function sampleAt(samples: readonly Sample[], t: number, maxExtrapolation: number): Pose | null {
  if (samples.length === 0) {
    return null;
  }
  const first = samples[0];
  if (t <= first.t) {
    return { x: first.x, y: first.y, z: first.z, yaw: first.yaw };
  }
  const last = samples[samples.length - 1];
  if (t >= last.t) {
    const ahead = Math.min(t - last.t, maxExtrapolation);
    return { x: last.x + last.vx * ahead, y: last.y + last.vy * ahead, z: last.z + last.vz * ahead, yaw: last.yaw };
  }
  for (let i = samples.length - 1; i > 0; i--) {
    const a = samples[i - 1];
    const b = samples[i];
    if (t >= a.t) {
      const k = b.t > a.t ? (t - a.t) / (b.t - a.t) : 1;
      return { x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k, z: a.z + (b.z - a.z) * k, yaw: lerpAngle(a.yaw, b.yaw, k) };
    }
  }
  return { x: last.x, y: last.y, z: last.z, yaw: last.yaw };
}

/**
 * The server's clock as seen from here: each snapshot says what time it was taken at, and the offset to the
 * local clock follows those readings smoothly so render time does not jump with network jitter.
 */
export class ServerClock {
  private offset: number | null = null;

  observe(serverTime: number, localSeconds: number): void {
    const sample = serverTime - localSeconds;
    if (this.offset === null || Math.abs(sample - this.offset) > 1) {
      this.offset = sample;
    } else {
      this.offset += (sample - this.offset) * 0.1;
    }
  }

  get known(): boolean {
    return this.offset !== null;
  }

  now(localSeconds: number): number {
    return localSeconds + (this.offset ?? 0);
  }

  reset(): void {
    this.offset = null;
  }
}
