import { describe, expect, it } from 'vitest';
import { bundledKinds } from '../logic/content';
import { gateFacts, gateHolds, keyEntryId, OptionsCatalog, OptionsState, Settings } from '../logic/options';
import {
  colorFieldValue,
  freshPadInput,
  heldPadInputs,
  noticeText,
  recordChanges,
  roundSlider,
  shownGroups,
  shownPages,
  sliderText,
  slotLabel,
  snapshotValues,
  storedColor,
  undoChanges,
} from './optionsLogic';

const catalog = () => OptionsCatalog.load(bundledKinds());

/** Runs a change the way the pane does, recording what it touched. */
const tracked = (state: OptionsState, settings: Settings, change: () => void): void => {
  const before = snapshotValues(settings);
  change();
  recordChanges(state, settings, before);
};

const web = (name: string): number | null => ({ 'client.web': 1, 'client.desktop': 0, 'client.developer': 0 } as Record<string, number>)[name] ?? null;

describe('options pane logic', () => {
  it('keeps the first original of each change and puts every one back on undo', () => {
    const s = new Settings(catalog());
    const state = new OptionsState();
    tracked(state, s, () => s.set('ui_scale', 1.25));
    tracked(state, s, () => s.set('ui_scale', 1.5));
    tracked(state, s, () => s.rebind('interact', 0, 'E', true));
    expect(state.originals.get('ui_scale')).toBe(1);
    expect(state.originals.has(keyEntryId('interact'))).toBe(true);
    expect(state.originals.has(keyEntryId('camera_rotate_right'))).toBe(true);
    expect(state.originals.has('theme')).toBe(false);

    expect(undoChanges(state, s)).toBe(true);
    expect(s.number('ui_scale')).toBe(1);
    expect(s.bindings('interact').primary).toBe('F');
    expect(s.bindings('camera_rotate_right').primary).toBe('E');
    expect(state.originals.size).toBe(0);
    expect(undoChanges(state, s)).toBe(false);
  });

  it('counts resets as changes and names restart entries in the notice', () => {
    const s = new Settings(catalog());
    const state = new OptionsState();
    expect(noticeText(state, s)).toBe('');
    s.set('combat_text', false);
    tracked(state, s, () => s.resetGroup('interface', 'text'));
    expect(s.toggle('combat_text')).toBe(true);
    expect(state.originals.get('combat_text')).toBe(false);
    expect(noticeText(state, s)).toBe('Escape undoes the changes; Done keeps them.');
    tracked(state, s, () => s.set('pixel_ratio', 1.5));
    expect(noticeText(state, s)).toBe('Escape undoes the changes; Done keeps them. After a restart: Pixel ratio cap.');
    // Done keeps the changes: nothing is left to undo, the restart still waits.
    state.originals.clear();
    expect(noticeText(state, s)).toBe('After a restart: Pixel ratio cap.');
  });

  it('shows the selected page, or every page a search matches with the page in each heading', () => {
    const s = new Settings(catalog());
    const holds = (expr: string | null) => gateHolds(expr, gateFacts(s, web));
    const pages = shownPages(s.catalog.pages, holds);
    expect(pages.some((p) => p.id === 'developer')).toBe(false);
    expect(pages[0].id).toBe('interface');

    const graphics = shownGroups(pages, 'graphics', '', holds);
    const ids = graphics.flatMap((g) => g.entries.map((e) => e.id));
    expect(ids).toContain('pixel_ratio');
    expect(ids).not.toContain('vsync');
    expect(graphics[0].title).toBe('Display');

    const found = shownGroups(pages, 'graphics', '  CHAT ', holds);
    expect(found.map((g) => g.title)).toContain('Interface: Chat');
    expect(found.every((g) => g.title.includes(': '))).toBe(true);
    expect(found.flatMap((g) => g.entries.map((e) => e.id))).toContain('chat_lines');
    expect(shownGroups(pages, 'graphics', 'no such words here', holds)).toEqual([]);
  });

  it('reads values the way the desktop writes them', () => {
    expect(sliderText(1, 'x')).toBe('1x');
    expect(sliderText(1.25, 'x')).toBe('1.25x');
    expect(sliderText(45, '°')).toBe('45°');
    expect(sliderText(80, '%')).toBe('80%');
    expect(sliderText(3, 'chunks')).toBe('3 chunks');
    expect(sliderText(0.1 + 0.2, '')).toBe('0.3');
    expect(roundSlider(0.9500000000000001)).toBe(0.95);
    expect(slotLabel('', 0)).toBe('-');
    expect(slotLabel('shift+alt+q', 0)).toBe('Alt+Shift+Q');
    expect(slotLabel('Shift+Tab', 0)).toBe('Shift+Tab');
    expect(slotLabel('e', 1)).toBe('E');
    expect(slotLabel('RB', 2)).toBe('RB');
  });

  it('keeps a stored colour alpha across the colour field', () => {
    expect(colorFieldValue('#abc')).toBe('#aabbcc');
    expect(colorFieldValue('#11223344')).toBe('#112233');
    expect(colorFieldValue('#A0B0C0')).toBe('#a0b0c0');
    expect(colorFieldValue('red')).toBe('#ffffff');
    expect(storedColor('#445566', '#11223380')).toBe('#44556680');
    expect(storedColor('#445566', '#112233ff')).toBe('#445566');
    expect(storedColor('#445566', '#123')).toBe('#445566');
  });

  it('names controller inputs and tells a fresh press from one already held', () => {
    const buttons = Array.from({ length: 17 }, () => ({ pressed: false, value: 0 }));
    buttons[0] = { pressed: true, value: 1 };
    buttons[6] = { pressed: true, value: 0.3 };
    buttons[7] = { pressed: true, value: 0.9 };
    buttons[13] = { pressed: true, value: 1 };
    expect(heldPadInputs({ buttons, axes: [0.1, -0.9, 0.2, 0.7] })).toEqual(['A', 'RT', 'DpadDown', 'LeftStickUp', 'RightStickDown']);
    expect(freshPadInput(new Set(['A']), ['A', 'B'])).toBe('B');
    expect(freshPadInput(new Set(['A']), ['A'])).toBeNull();
    expect(freshPadInput(new Set(), [])).toBeNull();
  });
});
