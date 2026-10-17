import { describe, expect, it } from 'vitest';
import { bundledKinds, type Obj } from '../ui/logic/content';
import { CastCueTimeline, cueSettings } from './castCueTimeline';
import type { CastCueMsg } from '../net/protocol';
import profileContent from '../../../tests/fixtures/cast-visuals/content/profiles.json';
import profileCases from '../../../tests/fixtures/cast-visuals/cases.json';

const config = cueSettings(bundledKinds().get('ui_cast_visuals')?.[0]);
const cue = (phase: string, sequence: number, extra: Partial<CastCueMsg> = {}): CastCueMsg => ({
  t: 'cast_cue', sequence, cast_id: 7, phase, source: 1, target: 2, weave: 'ember_bolt', name: 'Flame Bolt', mode: 'cast',
  tags: ['spell', 'ember'], server_time: 10, started_at: 9, duration: 2, x: 1, y: 2, z: 3, aim_x: 4, aim_y: 5, aim_z: 3,
  fired: false, reason: '', ...extra,
});

describe('confirmed cast presentation', () => {
  it.each(profileCases)('shared content-only profile: $id', example => {
    // JSON has no undefined values; TS adds optional undefined fields while inferring this heterogeneous array.
    const settings = cueSettings(profileContent.entries[0] as unknown as Obj);
    const t = new CastCueTimeline(settings);
    expect(t.receive(cue(example.phase, 1, {
      weave: example.weave, name: example.name, tags: example.tags, fired: example.fired ?? false,
    }), 10)).toBe(true);
    expect(t.active).toHaveLength(1);
    const expected = settings.profiles.find(p => p.id === example.expected_profile)!;
    expect(t.active[0].phase).toBe(example.expected_phase);
    expect(t.active[0].color).toBe(expected.color);
    expect(t.active[0].style).toBe(expected.phases[example.expected_phase]);
  });
  it('selects Flame by tags and neutral fallback without weave-name switches', () => {
    const t = new CastCueTimeline(config);
    t.receive(cue('start', 1, { weave: 'a_modded_spell', name: 'Custom' }), 10);
    expect(t.active[0].color).toBe('#ffb45c');
    t.receive(cue('impact', 2, { tags: ['unknown'], cast_id: 8 }), 10);
    expect(t.active[1].color).toBe('#e8dcc0');
  });
  it('deduplicates, rejects stale events, and bounds cosmetic work', () => {
    const t = new CastCueTimeline({ ...config, max_effects: 2 });
    expect(t.receive(cue('release', 1), 10)).toBe(true);
    expect(t.receive(cue('release', 1), 10)).toBe(false);
    expect(t.receive(cue('impact', 2), 20)).toBe(false);
    for (let i = 3; i < 10; i++) t.receive(cue('release', i), 10);
    expect(t.active).toHaveLength(2);
    t.advance(11);
    expect(t.active).toHaveLength(0);
  });
  it('underfilled release and interruption dissolve without success or impact', () => {
    const t = new CastCueTimeline(config);
    t.receive(cue('start', 1), 10);
    t.receive(cue('end', 2, { reason: 'underfilled' }), 10);
    expect(t.active.map(x => x.phase)).toEqual(['cancel']);
    expect(t.active[0].color).toBe('#e8dcc0');
    t.clear();
    t.receive(cue('start', 1), 10);
    t.receive(cue('interrupt', 2), 10);
    expect(t.active.map(x => x.phase)).toEqual(['cancel']);
  });
  it('sync reconstructs ongoing preparation without replaying a release', () => {
    const t = new CastCueTimeline(config);
    t.receive(cue('sync', 1), 10);
    expect(t.active.map(x => x.phase)).toEqual(['start']);
    t.advance(13);
    expect(t.active).toHaveLength(0);
  });
  it('allows delayed confirmed contact after recovery and clears on despawn/reset', () => {
    const t = new CastCueTimeline(config);
    t.receive(cue('start', 1), 10);
    t.receive(cue('end', 2, { fired: true }), 10);
    expect(t.active.map(x => x.phase)).toEqual(['recovery']);
    t.receive(cue('impact', 3, { server_time: 11 }), 11);
    t.advance(11);
    expect(t.active.map(x => x.phase)).toEqual(['impact']);
    t.removeEntity(2);
    expect(t.active).toHaveLength(0);
    t.clear();
    expect(t.receive(cue('start', 1), 10)).toBe(true);
  });
});
