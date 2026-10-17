import { describe, expect, it } from 'vitest';
import { ACTIONS, DEFAULTS, merge } from './config';
import { ENTITY_DELTA_FEATURE } from './net/protocol';

describe('config', () => {
  it('asks for entity deltas by default, by the name the protocol gives them', () => {
    expect(DEFAULTS.server.features).toContain(ENTITY_DELTA_FEATURE);
  });

  it('binds every action, like the desktop input map', () => {
    for (const action of ACTIONS) {
      expect(DEFAULTS.input.bindings[action].length).toBeGreaterThan(0);
    }
    expect(DEFAULTS.input.bindings.move_forward).toContain('KeyW');
    expect(DEFAULTS.input.bindings.slot_8).toContain('Digit8');
  });

  it('merges a partial override over the defaults without losing the rest', () => {
    const merged = merge(DEFAULTS, { world: { chunkRadius: 5 }, terrain: { fallback: '#000000' } });
    expect(merged.world.chunkRadius).toBe(5);
    expect(merged.world.maxChunkRequestsInFlight).toBe(DEFAULTS.world.maxChunkRequestsInFlight);
    expect(merged.terrain.fallback).toBe('#000000');
    expect(merged.terrain.base).toEqual(DEFAULTS.terrain.base);
    expect(DEFAULTS.world.chunkRadius).toBe(3);
  });

  it('replaces arrays rather than merging them', () => {
    const merged = merge(DEFAULTS, { input: { bindings: { jump: ['KeyJ'] } } });
    expect(merged.input.bindings.jump).toEqual(['KeyJ']);
    expect(merged.input.bindings.move_back).toEqual(DEFAULTS.input.bindings.move_back);
  });
});
