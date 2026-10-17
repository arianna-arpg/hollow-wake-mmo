import { metric, type UiLayout } from './content';

/** A screen rectangle in pixels: x right, y down. */
export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

export const EMPTY: Box = { x: 0, y: 0, w: 0, h: 0 };
export const right = (b: Box): number => b.x + b.w;
export const bottom = (b: Box): number => b.y + b.h;
export const isEmpty = (b: Box): boolean => b.w <= 0 || b.h <= 0;
export const contains = (b: Box, x: number, y: number): boolean => x >= b.x && x < right(b) && y >= b.y && y < bottom(b);

export function union(a: Box, b: Box): Box {
  if (isEmpty(a)) {
    return b;
  }
  if (isEmpty(b)) {
    return a;
  }
  const x = Math.min(a.x, b.x);
  const y = Math.min(a.y, b.y);
  return { x, y, w: Math.max(right(a), right(b)) - x, h: Math.max(bottom(a), bottom(b)) - y };
}

export function overlaps(a: Box, b: Box): boolean {
  const w = Math.min(right(a), right(b)) - Math.max(a.x, b.x);
  const h = Math.min(bottom(a), bottom(b)) - Math.max(a.y, b.y);
  return w > 0 && h > 0;
}

/** Moves a box inside bounds, keeping its size where it fits. */
export function clampInto(b: Box, bounds: Box): Box {
  const w = Math.min(b.w, bounds.w);
  const h = Math.min(b.h, bounds.h);
  return {
    x: Math.min(Math.max(b.x, bounds.x), right(bounds) - w),
    y: Math.min(Math.max(b.y, bounds.y), bottom(bounds) - h),
    w,
    h,
  };
}

/** Where the cluster's parts stand: the desktop client's UI/Logic/ClusterLayout.cs. */
export interface ClusterLayout {
  scale: number;
  orbRadius: number;
  rimWidth: number;
  ringWidth: number;
  ringGap: number;
  outerRadius: number;
  leftOrb: Box;
  rightOrb: Box;
  bar: Box;
  slots: Box[];
  cast: Box;
  status: Box;
  menu: Box;
  rect: Box;
  slotSize: number;
  statusIcon: number;
}

/** The radius of ring index (zero nearest the orb), at its centre line. */
export const ringRadius = (c: ClusterLayout, index: number): number =>
  c.orbRadius + c.rimWidth + c.ringGap * (index + 1) + c.ringWidth * index + c.ringWidth / 2;

export const rimRadius = (c: ClusterLayout): number => c.orbRadius + c.rimWidth / 2;

/**
 * The cluster at the bottom centre: the life orb left of the weave bar, the mana orb right of it, the menu
 * button beside the mana orb, the cast bar above the bar and the status row above that, with ring space for
 * every pool the layout names. It shrinks to fit a narrow screen. Its rectangle is what every pane fits above.
 */
export function clusterLayout(layout: UiLayout, viewWidth: number, viewHeight: number, userScale = 1): ClusterLayout {
  const rings = layout.orbs.reduce((n, o) => Math.max(n, o.rings.length), 0);
  const m = (name: string, fallback: number) => metric(layout, name, fallback);
  const orb = m('orb', 124);
  const rim = m('rim', 7);
  const ring = m('ring', 6);
  const ringGap = m('ring_gap', 3);
  const slot = m('slot', 52);
  const slotGap = m('slot_gap', 6);
  const slots = Math.max(1, Math.floor(m('slots', 8)));
  const pad = m('bar_pad', 10);
  const margin = m('cluster_margin', 10);
  const cast = m('cast_bar', 14);
  const statusIcon = m('status_icon', 28);
  const menu = m('menu_button', 46);
  const edge = m('edge', 14);
  const outer = orb / 2 + rim + rings * (ringGap + ring);
  const barW = slots * slot + (slots - 1) * slotGap + pad * 2;
  const barH = slot + pad * 2;
  const gap = slotGap * 2;
  const leftHalf = barW / 2 + gap + outer * 2;
  const rightHalf = barW / 2 + gap * 2 + outer * 2 + menu;
  const width = Math.max(leftHalf, rightHalf) * 2;
  let scale = userScale;
  const room = viewWidth - edge * 2;
  if (width * scale > room && width > 0) {
    scale = Math.max(0.35, room / width);
  }
  const s = (v: number) => v * scale;
  const bar: Box = { x: (viewWidth - s(barW)) / 2, y: viewHeight - s(margin) - s(barH), w: s(barW), h: s(barH) };
  const slotBoxes: Box[] = [];
  for (let i = 0; i < slots; i++) {
    slotBoxes.push({ x: bar.x + s(pad) + i * s(slot + slotGap), y: bar.y + s(pad), w: s(slot), h: s(slot) });
  }
  const outerS = s(outer);
  const base = viewHeight - s(margin);
  const leftOrb: Box = { x: bar.x - s(gap) - outerS * 2, y: base - outerS * 2, w: outerS * 2, h: outerS * 2 };
  const rightOrb: Box = { x: right(bar) + s(gap), y: base - outerS * 2, w: outerS * 2, h: outerS * 2 };
  const menuBox: Box = { x: right(rightOrb) + s(gap), y: base - s(menu), w: s(menu), h: s(menu) };
  const castW = bar.w * 0.62;
  const castBox: Box = { x: bar.x + bar.w / 2 - castW / 2, y: bar.y - s(slotGap) - s(cast), w: castW, h: s(cast) };
  const statusBox: Box = { x: bar.x, y: castBox.y - s(slotGap) - s(statusIcon), w: bar.w, h: s(statusIcon) };
  const rect = [rightOrb, bar, menuBox, castBox, statusBox].reduce(union, leftOrb);
  return {
    scale,
    orbRadius: s(orb / 2),
    rimWidth: s(rim),
    ringWidth: s(ring),
    ringGap: s(ringGap),
    outerRadius: outerS,
    leftOrb,
    rightOrb,
    bar,
    slots: slotBoxes,
    cast: castBox,
    status: statusBox,
    menu: menuBox,
    rect,
    slotSize: s(slot),
    statusIcon: s(statusIcon),
  };
}

/** A pane standing alone (Options with the core pane closed): centred above the cluster, inside the viewport. */
export function centered(viewport: Box, cluster: Box, width: number, height: number, edge: number): Box {
  const floor = Math.max(viewport.y + edge * 4, isEmpty(cluster) ? bottom(viewport) - edge : cluster.y - edge);
  const area: Box = { x: viewport.x + edge, y: viewport.y + edge, w: Math.max(0, viewport.w - edge * 2), h: Math.max(0, floor - viewport.y - edge) };
  const w = Math.min(width, area.w);
  const h = Math.min(height, area.h);
  return { x: area.x + (area.w - w) / 2, y: area.y + (area.h - h) / 2, w, h };
}

/** A seat remembered as fractions of the viewport, so it survives a resize. */
export interface Seat {
  fx: number;
  fy: number;
}

export const seatOf = (b: Box, view: Box): Seat => ({ fx: view.w <= 0 ? 0 : (b.x - view.x) / view.w, fy: view.h <= 0 ? 0 : (b.y - view.y) / view.h });

export interface PaneInput {
  viewport: Box;
  cluster: Box;
  seat: Seat | null;
  coreWidth: number;
  coreHeight: number;
  pageWidth: number;
  worldWidth: number;
  edge: number;
  hasPages: boolean;
  hasWorld: boolean;
  ribbons: number;
  ribbonHeight: number;
  badgeHeight: number;
  chips: number;
  chipHeight: number;
  railTop: number;
}

export interface WorkspaceRects {
  core: Box;
  pageBook: Box;
  worldBook: Box;
  merged: boolean;
  pagesLeft: boolean;
  railBadges: boolean;
  railChips: boolean;
}

/**
 * The placement law of the workspace: the core pane where the player seated it (by default at the right, just
 * above the cluster), always above the cluster and inside the viewport; pages beside it on the side with more
 * room; world panes across when that side has room, else sharing the page side as one book; books clamp on a
 * narrow screen; the rail drops badges before chips. The desktop client's UI/Logic/PaneLayout.cs.
 */
export function paneLayout(i: PaneInput): WorkspaceRects {
  const view = i.viewport;
  const edge = i.edge;
  const floor = Math.max(view.y + edge * 4, isEmpty(i.cluster) ? bottom(view) - edge : i.cluster.y - edge);
  const area: Box = { x: view.x + edge, y: view.y + edge, w: Math.max(0, view.w - edge * 2), h: Math.max(0, floor - view.y - edge) };
  const coreH = Math.min(i.coreHeight, area.h);
  const coreW = Math.min(i.coreWidth, area.w);
  const core = i.seat !== null
    ? clampInto({ x: view.x + i.seat.fx * view.w, y: view.y + i.seat.fy * view.h, w: coreW, h: coreH }, area)
    : { x: right(area) - coreW, y: bottom(area) - coreH, w: coreW, h: coreH };
  const gap = edge / 2;
  const roomLeft = core.x - area.x;
  const roomRight = right(area) - right(core);
  const pagesLeft = roomLeft >= roomRight;
  const beside = (left: boolean, width: number): Box => {
    const w = Math.min(width, area.w);
    return clampInto({ x: left ? core.x - gap - w : right(core) + gap, y: core.y, w, h: core.h }, area);
  };
  const fits = (left: boolean, width: number) => (left ? roomLeft : roomRight) >= width + gap;
  let pageBook = i.hasPages ? beside(pagesLeft, i.pageWidth) : EMPTY;
  let worldBook = EMPTY;
  let merged = false;
  if (i.hasWorld) {
    if (!i.hasPages) {
      worldBook = beside(pagesLeft, i.worldWidth);
    } else if (fits(!pagesLeft, i.worldWidth)) {
      worldBook = beside(!pagesLeft, i.worldWidth);
    } else {
      merged = true;
      pageBook = beside(pagesLeft, Math.max(i.pageWidth, i.worldWidth));
      worldBook = pageBook;
    }
  }
  const railRoom = core.h - i.railTop;
  const ribbons = i.ribbons * i.ribbonHeight;
  const badges = i.ribbons * i.badgeHeight;
  const chips = i.chips * i.chipHeight;
  const railBadges = ribbons + badges + chips <= railRoom;
  const railChips = ribbons + (railBadges ? badges : 0) + chips <= railRoom;
  return { core, pageBook, worldBook, merged, pagesLeft, railBadges, railChips };
}
