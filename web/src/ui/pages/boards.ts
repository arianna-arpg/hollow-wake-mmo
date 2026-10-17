import type { ContainerInfo, RungInfo } from '../../net/protocol';
import { ordinal } from './geometry';

/**
 * How a container page reads the inventory state: the boards it tabs between and their labels, the notes over the
 * shown board, the page of a stash that can be bought next, the unlock rungs and the overflow board. Text only, as
 * the desktop client's UI/Pages/ContainerPage.cs writes it; prices and reasons are the server's.
 */

const has = <T>(v: T | null | undefined): v is T => v !== null && v !== undefined;

/** The stash a board belongs to: its paged container, else itself. */
export const stashOf = (c: ContainerInfo): string => c.of ?? c.id;

/** The boards a page tabs between: every matching container but the overflow boards, by stash and then page. */
export function boardsOf(containers: readonly ContainerInfo[], matches: (c: ContainerInfo) => boolean): ContainerInfo[] {
  return containers
    .filter((c) => c.kind !== 'overflow' && matches(c))
    .sort((a, b) => ordinal(stashOf(a), stashOf(b)) || (a.page ?? 0) - (b.page ?? 0));
}

/** How many stashes a set of boards spans. */
export const stashCount = (boards: readonly ContainerInfo[]): number => new Set(boards.map(stashOf)).size;

/** A board's tab: "Page 2" among one stash's pages, the stash's name and page with several stashes, else its name. */
export function tabLabel(c: ContainerInfo, stashes: number): string {
  if (!has(c.page)) {
    return c.name;
  }
  return stashes > 1 ? `${c.name} ${c.page}` : `Page ${c.page}`;
}

/** A tab's tooltip: a page not yet bought names its price. */
export function tabTip(c: ContainerInfo, price: (p: Readonly<Record<string, number>>) => string): string {
  return c.locked === true && has(c.price) ? `Not yet bought: ${price(c.price)}` : '';
}

/** The first page not yet bought of the shown page's stash, when the shown page is not yet bought: pages are bought in order. */
export function nextToBuy(boards: readonly ContainerInfo[], shown: ContainerInfo): ContainerInfo | null {
  if (shown.locked !== true) {
    return null;
  }
  const stash = stashOf(shown);
  let next: ContainerInfo | null = null;
  for (const c of boards) {
    if (c.locked === true && stashOf(c) === stash && (next === null || (c.page ?? 0) < (next.page ?? 0))) {
      next = c;
    }
  }
  return next;
}

/** The notes over a board: that its items are active, why it is out of reach, and a page not yet bought with its price. */
export function boardNotes(shown: ContainerInfo, next: ContainerInfo | null, price: (p: Readonly<Record<string, number>>) => string): string {
  const notes: string[] = [];
  if (shown.active === true) {
    notes.push('Items seated here are active.');
  }
  if (!shown.reachable) {
    notes.push(shown.reason ?? 'Out of reach from here.');
  }
  if (shown.locked === true) {
    notes.push(`This page is not yet bought${has(shown.price) ? `: ${price(shown.price)}.` : '.'}`);
    if (next !== null && next.id !== shown.id) {
      notes.push('The pages before it are bought first.');
    }
  }
  return notes.join(' ');
}

export interface BuyState {
  visible: boolean;
  enabled: boolean;
  title: string;
}

/** The buy button for the shown page: on the first page not yet bought of its stash, priced, or why it cannot be bought here. */
export function buyState(shown: ContainerInfo, next: ContainerInfo | null, price: (p: Readonly<Record<string, number>>) => string): BuyState {
  return {
    visible: next !== null && next.id === shown.id,
    enabled: shown.reachable,
    title: shown.reachable ? (has(shown.price) ? `Costs ${price(shown.price)}` : '') : shown.reason ?? 'Out of reach from here.',
  };
}

/** The container a page is bought for: its stash. */
export const buyContainer = (shown: ContainerInfo): string => stashOf(shown);

/** A rung of a board's unlock ladder: its cells, and what it requires while locked. */
export function rungText(rung: RungInfo): string {
  const n = rung.cells.length;
  const tail = rung.unlocked ? '' : has(rung.requirement) ? `, requires ${rung.requirement}` : ', locked';
  return `Rung ${rung.index + 1}: ${n} cell${n === 1 ? '' : 's'}${tail}`;
}

/** The overflow board of a board: the items it holds without a cell. */
export function overflowOf(containers: readonly ContainerInfo[], board: string): ContainerInfo | null {
  return containers.find((c) => c.kind === 'overflow' && c.of === board) ?? null;
}

/** The note over an overflow board. */
export const overflowNote = (overflow: ContainerInfo): string => `${overflow.name}: ${overflow.reason ?? 'items without a place on the board.'}`;
