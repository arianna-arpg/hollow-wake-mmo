import type { CastCueMsg, CastForecastInfo, CastForecastMsg, CastForecastRegion, Int64 } from '../net/protocol';
import { SHAPES, WIDTH_SHAPES, ANGLE_SHAPES, regionOf } from './shapes';

export class CastForecastTimeline {
  private epoch: string | undefined;
  private sequence = -1n;
  private time = -Infinity;
  private validUntil = 0;
  private ended = new Map<Int64, number>();
  readonly active = new Map<Int64, CastForecastInfo>();
  private revisions = new Map<Int64, bigint>();
  omitted = 0;
  stale(now: number): boolean { return now > this.validUntil; }
  reset(epoch?: string): void {
    this.epoch = epoch; this.sequence = -1n; this.time = -Infinity; this.validUntil = 0;
    this.active.clear(); this.revisions.clear(); this.ended.clear(); this.omitted = 0;
  }
  receive(m: CastForecastMsg): boolean {
    if (this.epoch === undefined || m.epoch !== this.epoch || BigInt(m.sequence) <= this.sequence ||
        !Number.isFinite(m.server_time) || !Number.isFinite(m.valid_until) || m.server_time < this.time || m.valid_until < m.server_time) return false;
    this.sequence = BigInt(m.sequence); this.time = m.server_time; this.validUntil = m.valid_until;
    const previous = new Map(this.active);
    this.active.clear(); this.omitted = m.omitted;
    for (const reference of m.retained ?? []) {
      if (this.ended.has(reference.cast_id)) continue;
      const kept = previous.get(reference.cast_id);
      if (kept && this.revisions.get(reference.cast_id) === BigInt(reference.revision)) this.active.set(reference.cast_id, kept);
      else this.omitted++;
    }
    for (const c of m.casts) if (BigInt(c.cast_id) > 0n && !this.ended.has(c.cast_id) &&
        [c.started_at, c.x, c.y, c.z, c.next_emission_started_at ?? 0, c.next_emission_at ?? 0, c.ready_at ?? 0, c.max_hold_ends_at ?? 0].every(Number.isFinite)) {
      this.active.set(c.cast_id, c); this.revisions.set(c.cast_id, BigInt(m.sequence));
    }
    for (const id of this.revisions.keys()) if (!this.active.has(id)) this.revisions.delete(id);
    for (const [id, time] of this.ended) if (time <= this.time) this.ended.delete(id);
    return true;
  }
  lifecycle(c: CastCueMsg): void {
    if (BigInt(c.cast_id) <= 0n || c.outcome || !['end', 'interrupt'].includes(c.phase)) return;
    this.active.delete(c.cast_id);
    this.revisions.delete(c.cast_id);
    if (c.server_time >= this.time) this.ended.set(c.cast_id, c.server_time);
  }
  removeEntity(key: Int64): void { for (const [id, c] of this.active) if (c.source === key) { this.active.delete(id); this.revisions.delete(id); } }
}
export function forecastProgress(c: CastForecastInfo, now: number): number {
  const end = c.ready_at ?? c.next_emission_at;
  const start = c.ready_at !== undefined ? c.started_at : c.next_emission_started_at ?? c.started_at;
  return end !== undefined && end > start ? Math.min(1, Math.max(0, (now - start) / (end - start))) : 0;
}
export function forecastCaption(c: CastForecastInfo, now: number, stale: boolean): string {
  if (stale) return 'stale';
  if (c.ready_at !== undefined && now >= c.ready_at) return 'ready';
  if (c.mode === 'channel') return 'channel';
  if (c.next_emission_at !== undefined && now >= c.next_emission_at) return 'waiting';
  return usableForecast(c.region) ? 'primary' : 'unavailable';
}
export function usableForecast(r: CastForecastRegion | undefined): r is CastForecastRegion {
  return r !== undefined && (SHAPES as readonly string[]).includes(r.shape) &&
    (r.target_test === 'center' || r.target_test === 'body' && Number.isFinite(r.bottom) && Number.isFinite(r.top) && r.bottom! <= r.top!) &&
    (!WIDTH_SHAPES.includes(r.shape) || (r.size.width ?? 0) > 0) &&
    (!ANGLE_SHAPES.includes(r.shape) || (r.size.angle ?? 0) > 0) &&
    (r.size.radius ?? 0) > 0 && r.height_tolerance >= 0 && (r.center_allowance ?? 0) >= 0 &&
    [r.x, r.y, r.z, r.heading, r.height_tolerance, r.center_allowance ?? 0, r.size.radius ?? 0, r.size.angle ?? 0,
      r.size.width ?? 0, r.size.inner ?? 0, r.size.curvature ?? 0].every(Number.isFinite);
}
export const forecastVerticalBounds = (r: CastForecastRegion): [number, number] => r.target_test === 'body'
  ? [r.bottom! - r.z, r.top! - r.z] : [-r.height_tolerance, r.height_tolerance];
export const forecastOutline = (r: CastForecastRegion, segments: number) => regionOf({ name: r.shape,
  radius: r.size.radius!, angle: r.size.angle ?? 0, width: r.size.width ?? 0, inner: r.size.inner ?? 0,
  curvature: r.size.curvature ?? 0, centerAllowance: r.center_allowance ?? 0 }, segments);
export function forecastSegments(r: CastForecastRegion | undefined, minimum: number, maximum: number, error: number): number {
  if (!usableForecast(r)) return 0;
  const radius = r.size.radius! * Math.max(1, r.size.curvature ?? 0) + (r.size.width ?? 0) / 2;
  const angle = Math.acos(Math.max(-1, Math.min(1, 1 - error / radius)));
  const required = angle > 0 ? Math.max(minimum, Math.ceil(Math.PI / angle)) : Infinity;
  return required <= maximum ? required : 0;
}
