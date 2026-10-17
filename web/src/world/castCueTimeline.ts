import type { CastCueMsg, Int64 } from '../net/protocol';
import type { Obj } from '../ui/logic/content';

export interface CuePhase {
  seconds: number; size: number; height: number; expand: number; spin: number; opacity: number; stroke: number; segments: number[][];
}
export interface CueProfile { id: string; match: string[]; priority: number; color: string; phases: Record<string, CuePhase>; }
export interface CueSettings { max_effects: number; max_event_age: number; max_preparation: number; profiles: CueProfile[]; }
export interface CueInstance { event: CastCueMsg; phase: string; style: CuePhase; color: string; until: number; }

/** Validated at content build time. Missing optional look values have the same defaults as CastVisualPhase. */
export function cueSettings(raw: Obj | undefined): CueSettings {
  const defaults = { seconds: 0.25, size: 1, height: 0.6, expand: 0, spin: 0, opacity: 1, stroke: 0.055, segments: [] };
  const config = raw as unknown as CueSettings | undefined;
  return { max_effects: config?.max_effects ?? 128, max_event_age: config?.max_event_age ?? 1.5,
    max_preparation: config?.max_preparation ?? 60,
    profiles: (config?.profiles ?? []).map(p => ({ ...p, priority: p.priority ?? 0,
      phases: Object.fromEntries(Object.entries(p.phases).map(([k, v]) => [k, { ...defaults, ...v }])) })) };
}

export const sameKey = (a: Int64, b: Int64): boolean => String(a) === String(b);

/** Pure event consumer: no gameplay prediction, wall-clock RNG, name-based skill cases, or inferred hits. */
export class CastCueTimeline {
  readonly active: CueInstance[] = [];
  private sequence = -1n;
  private readonly profiles: CueProfile[];
  constructor(readonly config: CueSettings) {
    this.profiles = [...config.profiles].sort((a, b) => b.priority - a.priority || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  }
  clear(): void { this.active.length = 0; this.sequence = -1n; }
  removeEntity(key: Int64): void {
    for (let i = this.active.length - 1; i >= 0; i--) {
      const e = this.active[i].event;
      if (sameKey(e.source, key) || sameKey(e.target, key)) this.active.splice(i, 1);
    }
  }
  receive(e: CastCueMsg, now: number): boolean {
    const sequence = BigInt(e.sequence);
    if (sequence <= this.sequence) return false;
    this.sequence = sequence;
    if (sameKey(e.cast_id, 0) || now - e.server_time > this.config.max_event_age) return false;
    const phase = e.phase === 'sync' ? 'start' : e.phase === 'end' ? (e.fired ? 'recovery' : 'cancel') : e.phase === 'interrupt' ? 'cancel' : e.phase;
    if (!['start', 'release', 'impact', 'recovery', 'cancel'].includes(phase)) return false;
    if (phase === 'recovery' || phase === 'cancel' || phase === 'start') {
      for (let i = this.active.length - 1; i >= 0; i--) {
        const old = this.active[i];
        if (old.phase === 'start' && sameKey(old.event.source, e.source) && sameKey(old.event.cast_id, e.cast_id)) this.active.splice(i, 1);
      }
    }
    const matches = this.profiles.filter(p => p.match.every(t => e.tags.some(x => x === t || x.startsWith(`${t}.`))));
    const profile = matches.find(p => p.phases[phase] !== undefined);
    if (profile === undefined) return false;
    const style = profile.phases[phase];
    const until = phase === 'start'
      ? Math.min(e.server_time + this.config.max_preparation, e.mode === 'cast' && e.duration > 0 ? e.started_at + e.duration + this.config.max_event_age : Infinity)
      : e.server_time + style.seconds;
    if (until <= now) return false;
    while (this.active.length >= this.config.max_effects) this.active.shift();
    this.active.push({ event: e, phase, style, color: profile.color, until });
    return true;
  }
  advance(now: number): void {
    for (let i = this.active.length - 1; i >= 0; i--) if (this.active[i].until <= now) this.active.splice(i, 1);
  }
}
