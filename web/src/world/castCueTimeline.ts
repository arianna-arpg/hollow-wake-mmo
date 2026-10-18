import type { CastCueMsg, Int64 } from '../net/protocol';
import type { Obj } from '../ui/logic/content';

export interface CuePhase {
  primitive: string; pieces: number; arc: number; show_on_dead: boolean;
  seconds: number; size: number; height: number; expand: number; spin: number; opacity: number; stroke: number; segments: number[][];
}
export interface CueProfile { id: string; layer: string; fallback: boolean; match: string[]; match_mechanisms: string[]; priority: number; color: string; phases: Record<string, CuePhase>; }
export interface CueSettings { max_effects: number; max_layers_per_event: number; max_event_age: number; max_preparation: number; profiles: CueProfile[]; outcomes: string[]; }
export interface CueInstance { event: CastCueMsg; phase: string; style: CuePhase; color: string; until: number; layer: string; profile: string; }

/** Validated at content build time. Missing optional look values have the same defaults as CastVisualPhase. */
export function cueSettings(raw: Obj | undefined, threads: Obj[] = []): CueSettings {
  const defaults = { primitive: 'segments', pieces: 8, arc: 360, show_on_dead: false, seconds: 0.25, size: 1, height: 0.6, expand: 0, spin: 0, opacity: 1, stroke: 0.055, segments: [] };
  const config = raw as unknown as (CueSettings & { thread_phases?: Record<string, CuePhase> }) | undefined;
  const profiles = [...(config?.profiles ?? []), ...threads.map(t => ({ id: `thread.${String(t.id)}`,
    layer: `thread.${String(t.id)}`, match: [String(t.id)], color: String(t.color ?? '#ffffff'), phases: config?.thread_phases ?? {},
    priority: 0, fallback: false, match_mechanisms: [], }))];
  return { max_effects: config?.max_effects ?? 128, max_layers_per_event: config?.max_layers_per_event ?? 8, max_event_age: config?.max_event_age ?? 1.5,
    outcomes: config?.outcomes ?? [],
    max_preparation: config?.max_preparation ?? 60,
    profiles: profiles.map(p => ({ ...p, layer: p.layer ?? 'base', fallback: p.fallback ?? false, match: p.match ?? [], match_mechanisms: p.match_mechanisms ?? [], priority: p.priority ?? 0,
      phases: Object.fromEntries(Object.entries(p.phases).map(([k, v]) => [k, { ...defaults, ...v }])) })) };
}

export const sameKey = (a: Int64, b: Int64): boolean => String(a) === String(b);

/** Pure event consumer: no gameplay prediction, wall-clock RNG, name-based skill cases, or inferred hits. */
export class CastCueTimeline {
  readonly active: CueInstance[] = [];
  private sequence = -1n;
  private readonly profiles: CueProfile[];
  constructor(readonly config: CueSettings) {
    this.profiles = [...config.profiles].sort((a, b) => Number(a.fallback) - Number(b.fallback) || b.priority - a.priority || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
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
    if ((!e.outcome && sameKey(e.cast_id, 0)) || now - e.server_time > this.config.max_event_age) return false;
    const phase = e.outcome ? (this.config.outcomes.includes(e.phase) && this.profiles.some(p => p.phases[e.phase]) ? e.phase : 'outcome')
      : e.phase === 'sync' ? 'start' : e.phase === 'end' ? (e.fired ? 'recovery' : 'cancel') : e.phase === 'interrupt' ? 'cancel' : e.phase;
    if (!e.outcome && !['start', 'release', 'impact', 'recovery', 'cancel'].includes(phase)) return false;
    if (!e.outcome && (phase === 'recovery' || phase === 'cancel' || phase === 'start')) {
      for (let i = this.active.length - 1; i >= 0; i--) {
        const old = this.active[i];
        if (old.phase === 'start' && sameKey(old.event.source, e.source) && sameKey(old.event.cast_id, e.cast_id)) this.active.splice(i, 1);
      }
    }
    const matches = (required: string[], actual: string[]): boolean => required.every(t => actual.some(x => x === t || x.startsWith(`${t}.`)));
    const layers = new Set<string>();
    for (const profile of this.profiles) {
      if (layers.size >= this.config.max_layers_per_event) break;
      const style = profile.phases[phase] ?? (e.outcome && profile.fallback ? profile.phases.outcome : undefined);
      if ((profile.fallback && layers.size > 0) || layers.has(profile.layer) || style === undefined || !matches(profile.match, e.tags) || !matches(profile.match_mechanisms, e.mechanisms ?? [])) continue;
      const until = !e.outcome && phase === 'start'
        ? Math.min(e.server_time + this.config.max_preparation, e.mode === 'cast' && e.duration > 0 ? e.started_at + e.duration + this.config.max_event_age : Infinity)
        : e.server_time + style.seconds;
      if (until <= now) continue;
      layers.add(profile.layer);
      while (this.active.length >= this.config.max_effects) this.active.shift();
      this.active.push({ event: e, phase, style, color: profile.color, until, layer: profile.layer, profile: profile.id });
    }
    return layers.size > 0;
  }
  advance(now: number): void {
    for (let i = this.active.length - 1; i >= 0; i--) if (this.active[i].until <= now) this.active.splice(i, 1);
  }
}
