import { describe, expect, it } from 'vitest';
import { DEFAULTS, merge } from './config';
import { ENTITY_DELTA_FEATURE } from './net/protocol';
import { bundledKinds } from './ui/logic/content';
import { OptionsCatalog } from './ui/logic/options';

describe('config', () => {
  it('asks for entity deltas by default, by the name the protocol gives them', () => {
    expect(DEFAULTS.server.features).toContain(ENTITY_DELTA_FEATURE);
  });

  it('leaves the bindings to content: every action the client reads is an input action with a key', () => {
    const catalog = OptionsCatalog.load(bundledKinds());
    const wanted = ['move_forward', 'move_back', 'move_left', 'move_right', 'jump', 'interact', 'pick_up', 'respawn', 'chat', 'cancel',
      'camera_toggle', 'camera_rotate_left', 'camera_rotate_right', 'ui_menu_tray', 'ui_book_next', 'ui_book_previous',
      'slot_1', 'slot_2', 'slot_3', 'slot_4', 'slot_5', 'slot_6', 'slot_7', 'slot_8'];
    for (const id of wanted) {
      expect(catalog.actionById.get(id)?.keys.length ?? 0, id).toBeGreaterThan(0);
    }
    expect(catalog.actionById.get('move_forward')?.held).toBe(true);
    expect(catalog.actionById.get('slot_8')?.keys).toContain('8');
  });

  it('merges a partial override over the defaults without losing the rest', () => {
    const merged = merge(DEFAULTS, { world: { chunkRadius: 5 }, entities: { telegraph: '#000000' } });
    expect(merged.world.chunkRadius).toBe(5);
    expect(merged.world.maxChunkRequestsInFlight).toBe(DEFAULTS.world.maxChunkRequestsInFlight);
    expect(merged.entities.telegraph).toBe('#000000');
    expect(merged.entities.labelColors).toEqual(DEFAULTS.entities.labelColors);
    expect(DEFAULTS.world.chunkRadius).toBe(3);
  });

  it('replaces arrays rather than merging them', () => {
    const merged = merge(DEFAULTS, { server: { features: [] } });
    expect(merged.server.features).toEqual([]);
    expect(merged.server.path).toBe(DEFAULTS.server.path);
  });
});
