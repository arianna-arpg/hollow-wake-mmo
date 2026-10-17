import { describe, expect, it } from 'vitest';
import { BookRule } from './book';
import type { UiPage } from './content';
import { Workspace } from './workspace';

const world = (b: BookRule, id: string, rank = 1, ask: 'auto' | 'front' | 'behind' = 'auto') => b.bind(id, 'world', id, rank, id, ask);
const page = (b: BookRule, id: string, ask: 'auto' | 'front' | 'behind' = 'auto') => b.bind(id, 'page', id, 0, id, ask);

describe('the book rule', () => {
  it('keeps the first leaf at the front and lands newcomers of the same rank behind, fresh', () => {
    const b = new BookRule();
    world(b, 'dialogue');
    const vendor = world(b, 'vendor');
    expect(b.front('world')?.id).toBe('dialogue');
    expect(vendor.fresh).toBe(true);
  });

  it('fronts a higher rank and an explicit ask', () => {
    const b = new BookRule();
    page(b, 'weaves');
    world(b, 'station');
    expect(b.front('page')?.id).toBe('station');
    page(b, 'passives', 'front');
    expect(b.front('page')?.id).toBe('passives');
    world(b, 'choice', 2, 'behind');
    expect(b.front('page')?.id).toBe('passives');
  });

  it('promotes the leaf last looked at, else the first tab, and never reshuffles tabs', () => {
    const b = new BookRule();
    world(b, 'a');
    world(b, 'b');
    world(b, 'c');
    b.select('c');
    b.select('b');
    b.close('b');
    expect(b.front('world')?.id).toBe('c');
    expect(b.leaves.map((l) => l.id)).toEqual(['a', 'c']);
    const fresh = new BookRule();
    world(fresh, 'x');
    world(fresh, 'y');
    world(fresh, 'z');
    fresh.close('x');
    expect(fresh.front('world')?.id).toBe('y');
  });

  it('keeps a front per side when split and one when merged', () => {
    const b = new BookRule();
    b.merged = false;
    page(b, 'weaves');
    world(b, 'vendor');
    expect(b.drawn().map((l) => l.id)).toEqual(['weaves', 'vendor']);
    b.merged = true;
    expect(b.drawn().map((l) => l.id)).toEqual(['vendor']);
  });

  it('walks a book of two or more in tab order', () => {
    const b = new BookRule();
    world(b, 'a');
    world(b, 'b');
    world(b, 'c');
    expect(b.walk(1)?.id).toBe('b');
    expect(b.walk(1)?.id).toBe('c');
    expect(b.walk(1)?.id).toBe('a');
    expect(b.walk(-1)?.id).toBe('c');
    const one = new BookRule();
    world(one, 'solo');
    expect(one.canWalk).toBe(false);
  });
});

const pages: UiPage[] = [
  { id: 'inventory', name: 'Inventory', action: 'page_inventory', icon: 'bag', order: 0, place: 'core', description: '', container: null },
  { id: 'weaves', name: 'Weaves', action: 'page_weaves', icon: 'weave', order: 1, place: 'ribbon', description: '', container: null },
  { id: 'passives', name: 'Passives', action: 'page_passives', icon: 'star', order: 2, place: 'ribbon', description: '', container: null },
  { id: 'options', name: 'Options', action: 'page_options', icon: 'rune', order: 9, place: 'menu', description: '', container: null },
];
const ranks: Record<string, number> = { page: 0, vendor: 1, dialogue: 1, quest_offer: 1, choice: 2 };
const make = () => new Workspace((id) => pages.find((p) => p.id === id), (k) => ranks[k] ?? 0);

describe('the workspace', () => {
  it('opens the core pane for a page, closes it on the same key, and dismisses only a page by its ribbon', () => {
    const w = make();
    w.requestPage('weaves', 'key');
    expect(w.coreOpen).toBe(true);
    expect(w.selectedPage).toBe('weaves');
    w.requestPage('passives', 'key');
    w.requestPage('passives', 'ribbon');
    expect(w.coreOpen).toBe(true);
    expect(w.book.has('passives')).toBe(false);
    w.requestPage('weaves', 'key');
    expect(w.coreOpen).toBe(false);
  });

  it('remembers its pages and selection across a close', () => {
    const w = make();
    w.requestPage('weaves', 'key');
    w.requestPage('passives', 'key');
    w.requestPage('weaves', 'ribbon');
    w.requestPage('inventory', 'key');
    w.requestPage('inventory', 'key');
    expect(w.book.has('passives')).toBe(true);
    expect(w.selectedPage).toBe('weaves');
  });

  it('opens the core pane beside a world pane, fronts a station over a page, and promotes the page back', () => {
    const w = make();
    w.requestPage('weaves', 'key');
    w.openWorld('vendor:5', 'vendor', 'Vendor', 5);
    expect(w.book.front('page')?.id).toBe('vendor:5');
    w.closeWorld('vendor:5');
    expect(w.book.front('page')?.id).toBe('weaves');
  });

  it('escapes the tray, then the front pane, then the core pane', () => {
    const w = make();
    w.requestPage('weaves', 'key');
    w.openWorld('dialogue:3', 'dialogue', 'Talk', 3);
    w.trayOpen = true;
    expect(w.escape()).toBe('tray');
    expect(w.escape()).toBe('pane');
    expect(w.escape()).toBe('pane');
    expect(w.escape()).toBe('core');
    expect(w.escape()).toBe('nothing');
  });

  it('departs world panes out of reach or whose source is gone, and tells the listeners', () => {
    const w = make();
    const closed: string[] = [];
    w.onWorldClosed((leaf) => closed.push(leaf.id));
    w.openWorld('vendor:5', 'vendor', 'Vendor', 5, 4);
    w.openWorld('dialogue:6', 'dialogue', 'Talk', 6);
    const distances = new Map<number | string, number>([[5, 3.5], [6, 7]]);
    w.depart((k) => distances.get(k) ?? null, 6);
    expect(closed).toEqual(['dialogue:6']);
    w.depart(() => null, 6);
    expect(closed).toEqual(['dialogue:6', 'vendor:5']);
    expect(w.coreOpen).toBe(true);
  });

  it('lets a menu page stand alone and outlive the core pane', () => {
    const w = make();
    w.requestPage('options', 'key');
    expect(w.coreOpen).toBe(false);
    expect(w.book.isDrawn('options')).toBe(true);
    expect(w.anythingOpen).toBe(true);
    w.requestPage('weaves', 'key');
    w.closeCore();
    expect(w.book.has('options')).toBe(true);
    w.requestPage('options', 'key');
    expect(w.book.has('options')).toBe(false);
    expect(w.escape()).toBe('nothing');
    expect(w.anythingOpen).toBe(false);
  });
});
