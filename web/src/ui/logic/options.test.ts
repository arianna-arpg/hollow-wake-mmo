import { describe, expect, it } from 'vitest';
import { bundledKinds } from './content';
import { canonicalKey, composeKeyName, eventKeyName, keyMatches } from './menu';
import { evaluateGate, gateFacts, gateHolds, keyEntryId, OptionsCatalog, Settings } from './options';

const catalog = () => OptionsCatalog.load(bundledKinds());

describe('options', () => {
  it('lists pages, groups and entries in order, with choices drawn from other kinds', () => {
    const c = catalog();
    expect(c.pages[0].id).toBe('interface');
    expect(c.pages.some((p) => p.id === 'graphics')).toBe(true);
    const theme = c.entry('theme');
    expect(theme?.kind).toBe('choice');
    expect(theme?.choices.map((x) => x.id)).toEqual(expect.arrayContaining(['etched_stone', 'runic_brass']));
    expect(theme?.default).toBe('etched_stone');
  });

  it('turns binding groups into a key entry per action', () => {
    const c = catalog();
    const move = c.entry(keyEntryId('move_forward'));
    expect(move?.kind).toBe('key');
    expect(move?.action).toBe('move_forward');
    for (const action of c.actions) {
      expect(c.entry(keyEntryId(action.id)), `${action.id} has no key entry`).toBeDefined();
    }
  });

  it('falls back to defaults and refuses malformed values', () => {
    const s = new Settings(catalog());
    expect(s.number('ui_scale')).toBe(1);
    expect(s.text('theme')).toBe('etched_stone');
    expect(s.toggle('combat_text')).toBe(true);
    expect(s.set('ui_scale', 9)).toBe(false);
    expect(s.set('theme', 'no_such_theme')).toBe(false);
    expect(s.set('combat_text', 'yes')).toBe(false);
    expect(s.set('theme', 'runic_brass')).toBe(true);
    expect(s.text('theme')).toBe('runic_brass');
    expect(s.isDefault('theme')).toBe(false);
  });

  it('announces changes and resets to the default by group and page', () => {
    const s = new Settings(catalog());
    const changed: string[] = [];
    s.onChange((e) => changed.push(e.id));
    s.set('ui_scale', 1.25);
    s.set('combat_text', false);
    expect(changed).toEqual(['ui_scale', 'combat_text']);
    s.resetGroup('interface', 'look');
    expect(s.number('ui_scale')).toBe(1);
    expect(s.toggle('combat_text')).toBe(false);
    s.resetPage('interface');
    expect(s.toggle('combat_text')).toBe(true);
    expect(s.isDefault('combat_text')).toBe(true);
  });

  it('round-trips the stored form and reads a broken one as defaults', () => {
    const c = catalog();
    const s = new Settings(c);
    s.set('theme', 'runic_brass');
    s.rebind('jump', 0, 'J', false);
    const again = Settings.load(c, s.serialize());
    expect(again.text('theme')).toBe('runic_brass');
    expect(again.bindings('jump').primary).toBe('J');
    expect(Settings.load(c, '{ not json').text('theme')).toBe('etched_stone');
    const wrong = Settings.load(c, '{"version":1,"values":{"ui_scale":"huge","theme":"runic_brass"}}');
    expect(wrong.number('ui_scale')).toBe(1);
    expect(wrong.text('theme')).toBe('runic_brass');
  });

  it('finds conflicts within a context and swaps or clears on rebinding', () => {
    const s = new Settings(catalog());
    expect(s.conflicts('interact', 'E').map((a) => a.id)).toEqual(['camera_rotate_right']);
    s.rebind('interact', 0, 'E', true);
    expect(s.bindings('interact').primary).toBe('E');
    expect(s.bindings('camera_rotate_right').primary).toBe('F');
    s.rebind('jump', 0, 'F', false);
    expect(s.bindings('camera_rotate_right').primary).toBe('');
    // Tab is the menu and the book walk, in different contexts: no conflict.
    expect(s.conflicts('ui_book_next', 'Tab')).toEqual([]);
    // A controller input conflicts only with controller bindings.
    expect(s.conflicts('jump', 'B', true).map((a) => a.id)).toEqual(['cancel']);
    expect(s.conflicts('jump', 'B', false)).toEqual([]);
  });

  it('evaluates gates over client facts and other options', () => {
    const facts = (name: string) => ({ 'client.desktop': 0, 'client.web': 1, 'option.shadows': 1 } as Record<string, number>)[name] ?? null;
    expect(gateHolds('client.web', facts)).toBe(true);
    expect(gateHolds('client.desktop', facts)).toBe(false);
    expect(gateHolds('client.web && !client.desktop', facts)).toBe(true);
    expect(gateHolds('option.shadows == 1 || client.desktop', facts)).toBe(true);
    expect(gateHolds('has("x")', facts, (f) => f === 'x')).toBe(true);
    expect(gateHolds('', facts)).toBe(true);
    expect(gateHolds('client.developer', facts)).toBe(false);
    expect(evaluateGate('1 + 2 * (3 - 2)', facts)).toBe(3);
    expect(evaluateGate('client.web ? 5 : 7', facts)).toBe(5);
    expect(gateHolds('(1', facts)).toBe(false);
    const s = new Settings(catalog());
    const read = gateFacts(s, () => null);
    expect(read('option.combat_text')).toBe(1);
    s.set('combat_text', false);
    expect(read('option.combat_text')).toBe(0);
    expect(read('option.ui_scale')).toBe(1);
  });

  it('compares key names in one spelling and reads browser keys as content names', () => {
    expect(canonicalKey('shift+Tab')).toBe('Shift+Tab');
    expect(canonicalKey('Ctrl+Shift+k')).toBe(canonicalKey('Shift+Ctrl+K'));
    expect(canonicalKey('Esc')).toBe('Escape');
    expect(composeKeyName('F1', true, true, false)).toBe('Ctrl+Shift+F1');
    const ev = (code: string, shiftKey = false) => ({ code, shiftKey, ctrlKey: false, altKey: false });
    expect(eventKeyName(ev('KeyB'))).toBe('B');
    expect(eventKeyName(ev('Digit1'))).toBe('1');
    expect(eventKeyName(ev('ArrowUp'))).toBe('Up');
    expect(eventKeyName(ev('Tab', true))).toBe('Shift+Tab');
    expect(eventKeyName(ev('ShiftLeft', true))).toBe('');
    expect(keyMatches(ev('ArrowLeft'), 'Left')).toBe(true);
    expect(keyMatches(ev('Numpad3'), '3')).toBe(true);
    expect(keyMatches(ev('F10'), 'F10')).toBe(true);
  });
});
