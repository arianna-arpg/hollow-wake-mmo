import { describe, expect, it } from 'vitest';
import { bundledKinds, loadNames, loadUi, rankOf, weaveIcon } from './content';
import { badgeText, foldTray, keyLabelOf, keyMatches, parseKeyName, servedPages, trayBadge, webCode } from './menu';
import { buildSheet, formatValue } from './sheet';

describe('the interface content', () => {
  const kinds = bundledKinds();

  it('loads the default theme, the layout, the pages and the sheet the desktop client reads', () => {
    const ui = loadUi(kinds);
    expect(ui.theme.id).toBe('etched_stone');
    expect(ui.theme.default).toBe(true);
    expect(ui.layout.orbs.map((o) => o.fill)).toEqual(['life', 'mana']);
    expect(ui.pages.find((p) => p.place === 'core')?.id).toBe('inventory');
    expect(ui.sheet.length).toBeGreaterThan(0);
    expect(rankOf(ui.layout, 'vendor')).toBeGreaterThan(rankOf(ui.layout, 'page'));
    expect(loadUi(kinds, 'runic_brass').theme.frame).toBe('brass');
  });

  it('colours every pool the cluster draws in every theme, and draws every page glyph', () => {
    const ui = loadUi(kinds);
    const pools = ui.layout.orbs.flatMap((o) => [o.fill, ...o.overlays, ...o.rim, ...o.rings]);
    for (const theme of ui.themes) {
      for (const pool of pools) {
        expect(theme.resources[pool], `${theme.id} ${pool}`).toBeDefined();
      }
    }
    for (const page of ui.pages) {
      expect(ui.icons.icons[page.icon], page.id).toBeDefined();
    }
    expect(weaveIcon(ui.icons, null, 'ember_bolt')).toBe('missile');
    expect(weaveIcon(ui.icons, 'strike', null)).toBe('blade');
  });

  it('names attributes, Threads, slots, statuses and the constellation from the content', () => {
    const names = loadNames(kinds);
    expect(names.attributeById.get('might')?.name).toBe('Might');
    expect(names.threads.some((t) => t.id === 'ember')).toBe(true);
    expect(names.slots.some((s) => s.id === 'main_hand')).toBe(true);
    expect(names.threadOfStatus.get('burning')).toBe('ember');
    const ui = loadUi(kinds);
    for (const status of names.statuses.keys()) {
      expect(ui.icons.statuses[status], status).toBeDefined();
    }
    const notable = names.passives.find((p) => p.kind === 'notable');
    expect(notable?.lines.length).toBeGreaterThan(0);
  });
});

describe('the menu tray', () => {
  const ui = loadUi(bundledKinds());

  it('folds the page state into hidden, sealed and open rows and sums the open pips', () => {
    // The stash lists itself once per stash: open when any of them is in reach. Options is the client's own.
    const served = servedPages(ui.pages.map((p) => p.id), [
      { id: 'inventory', sealed: false, pips: 0 },
      { id: 'weaves', sealed: true, reason: 'Not while fighting', pips: 2 },
      { id: 'passives', sealed: false, pips: 3 },
      { id: 'stash', sealed: true, reason: 'Stand at a stash chest.', pips: 0 },
      { id: 'stash', sealed: false, pips: 0 },
      { id: 'character', sealed: false, pips: 0 },
    ]);
    const rows = foldTray(ui.pages, served, () => 0, (id) => id === 'passives', (p) => keyLabelOf(p.action === 'page_weaves' ? 'K' : ''));
    expect(rows.map((r) => [r.page.id, r.state])).toEqual([
      ['inventory', 'open'],
      ['weaves', 'sealed'],
      ['passives', 'open'],
      ['reliquary', 'hidden'],
      ['stash', 'open'],
      ['character', 'open'],
      ['options', 'open'],
    ]);
    expect(rows[1].keyLabel).toBe('K');
    expect(trayBadge(rows)).toBe(3);
    expect(badgeText(12)).toBe('9+');
    const far = servedPages(['stash'], [
      { id: 'stash', sealed: true, reason: 'Stand at a stash chest.', pips: 0 },
      { id: 'stash', sealed: true, reason: 'Stand at the account chest.', pips: 1 },
    ]);
    expect(far.get('stash')).toEqual({ exists: true, sealed: 'Stand at a stash chest.', pips: 1 });
  });

  it('reads key names into labels and browser codes', () => {
    expect(keyLabelOf('shift+Tab')).toBe('Shift+Tab');
    expect(webCode(parseKeyName('i'))).toBe('KeyI');
    expect(webCode(parseKeyName('7'))).toBe('Digit7');
    expect(keyMatches({ code: 'Tab', shiftKey: true, ctrlKey: false, altKey: false }, 'Shift+Tab')).toBe(true);
    expect(keyMatches({ code: 'Tab', shiftKey: true, ctrlKey: false, altKey: false }, 'Tab')).toBe(false);
  });
});

describe('the sheet', () => {
  it('lists sections from content with the values the server sent', () => {
    const ui = loadUi(bundledKinds());
    const names = loadNames(bundledKinds());
    const values = { might: 14, 'life.max': 412.4, 'crit.chance': 0.075, 'resist.ember': 0.25, 'resist.ember.max': 0.75 };
    const sheet = buildSheet(ui.sheet, names, values, [{ name: 'Clear the Hedgerow', state: 'active', objectives: ['Hobs: 2/5'] }]);
    const attributes = sheet.find((s) => s.id === 'attributes');
    expect(attributes?.rows[0]).toMatchObject({ label: 'Might', value: '14' });
    const resist = sheet.find((s) => s.kind === 'threads' && s.rows.some((r) => r.thread === 'ember'));
    expect(resist?.rows.find((r) => r.thread === 'ember')?.value).toBe('25% (max 75%)');
    expect(sheet.find((s) => s.kind === 'quests')?.rows[0].description).toBe('Hobs: 2/5');
    expect(formatValue(1.5, names.attributeById.get('crit.chance'))).toBe('150%');
  });
});
