import { describe, expect, it } from 'vitest';
import type { UiLayout } from './content';
import { bottom, clusterLayout, overlaps, paneLayout, right, ringRadius, seatOf, type PaneInput } from './layout';

const layout: UiLayout = {
  id: 'default',
  orbs: [
    { side: 'left', fill: 'life', overlays: ['shield'], rim: ['ward'], rings: ['poise', 'insight'] },
    { side: 'right', fill: 'mana', overlays: [], rim: [], rings: ['stamina'] },
  ],
  ranks: ['page', 'station', 'decision'],
  panes: {},
  doll: {},
  metrics: { orb: 124, rim: 7, ring: 6, ring_gap: 3, slot: 52, slot_gap: 6, slots: 8, bar_pad: 10, cluster_margin: 10, cast_bar: 14, status_icon: 28, menu_button: 46, edge: 14 },
};

describe('the cluster', () => {
  it('stands at the bottom centre with the orbs either side and the menu by the mana orb', () => {
    const c = clusterLayout(layout, 1600, 900);
    expect(c.scale).toBe(1);
    expect(c.bar.x + c.bar.w / 2).toBeCloseTo(800);
    expect(bottom(c.bar)).toBeCloseTo(890);
    expect(right(c.leftOrb)).toBeLessThanOrEqual(c.bar.x);
    expect(c.rightOrb.x).toBeGreaterThanOrEqual(right(c.bar));
    expect(c.menu.x).toBeGreaterThanOrEqual(right(c.rightOrb));
    expect(bottom(c.status)).toBeLessThanOrEqual(c.cast.y);
    expect(c.slots).toHaveLength(8);
  });

  it('reserves ring space for every pool and shrinks on a narrow screen', () => {
    const c = clusterLayout(layout, 1600, 900);
    expect(c.outerRadius).toBeCloseTo(62 + 7 + 18);
    expect(ringRadius(c, 1) + c.ringWidth / 2).toBeLessThanOrEqual(c.outerRadius + 0.01);
    const narrow = clusterLayout(layout, 800, 600);
    expect(narrow.scale).toBeLessThan(1);
    expect(narrow.rect.x).toBeGreaterThanOrEqual(0);
    expect(right(narrow.rect)).toBeLessThanOrEqual(800);
  });
});

const input = (over: Partial<PaneInput> = {}): PaneInput => ({
  viewport: { x: 0, y: 0, w: 1600, h: 900 },
  cluster: { x: 420, y: 700, w: 760, h: 190 },
  seat: null,
  coreWidth: 430,
  coreHeight: 560,
  pageWidth: 460,
  worldWidth: 420,
  edge: 14,
  hasPages: true,
  hasWorld: false,
  ribbons: 4,
  ribbonHeight: 52,
  badgeHeight: 14,
  chips: 3,
  chipHeight: 40,
  railTop: 40,
  ...over,
});

describe('pane placement', () => {
  it('seats the core pane at the right just above the cluster, with pages docked beside it', () => {
    const r = paneLayout(input());
    expect(right(r.core)).toBeCloseTo(1586);
    expect(bottom(r.core)).toBeCloseTo(686);
    expect(r.pagesLeft).toBe(true);
    expect(right(r.pageBook)).toBeLessThanOrEqual(r.core.x);
    expect(r.pageBook.h).toBe(r.core.h);
  });

  it('keeps a remembered seat, clamped above the cluster', () => {
    const r = paneLayout(input({ seat: { fx: 1.2, fy: 0.9 } }));
    expect(right(r.core)).toBeLessThanOrEqual(1586.01);
    expect(bottom(r.core)).toBeLessThanOrEqual(686.01);
    expect(seatOf({ x: 250, y: 100, w: 1, h: 1 }, { x: 0, y: 0, w: 1000, h: 500 })).toEqual({ fx: 0.25, fy: 0.2 });
  });

  it('stands world panes across the core pane when there is room, and binds the sides when there is not', () => {
    const split = paneLayout(input({ hasWorld: true, seat: { fx: 0.36, fy: 0.1 } }));
    expect(split.merged).toBe(false);
    expect(overlaps(split.pageBook, split.worldBook)).toBe(false);
    const merged = paneLayout(input({ hasWorld: true }));
    expect(merged.merged).toBe(true);
    expect(merged.worldBook).toEqual(merged.pageBook);
  });

  it('drops the rail badges before the wallet chips', () => {
    expect(paneLayout(input())).toMatchObject({ railBadges: true, railChips: true });
    expect(paneLayout(input({ coreHeight: 400 }))).toMatchObject({ railBadges: false, railChips: true });
    expect(paneLayout(input({ coreHeight: 300 }))).toMatchObject({ railBadges: false, railChips: false });
  });
});
