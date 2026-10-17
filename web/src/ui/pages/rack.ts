import type { ItemInfo, Message, SeatGlyphInfo, WeaveSeatInfo } from '../../net/protocol';
import type { Seat } from '../logic/heroState';

/**
 * How the rack reads a seat: its tooltip and the line under its name, what a carried stone or form would do on
 * it, the intent a drop or a right click sends, and the costs drawn on its glyph. Text and intents only, as the
 * desktop client's UI/Pages/WeavesPage.cs writes them; every number is the server's.
 */

const has = <T>(v: T | null | undefined): v is T => v !== null && v !== undefined;

/** The tooltip of a seat: its name, why it is dormant, and each glyph it holds with its own dormant reason. */
export function seatTip(seat: Seat): string {
  if (seat.key === null) {
    return 'An empty seat: drag a form item here to learn it.';
  }
  const lines = [seat.name];
  if (seat.dormant !== null) {
    lines.push(`Dormant: ${seat.dormant}`);
  }
  for (const g of seat.info?.glyphs ?? []) {
    lines.push(`${g.name} ${g.level}${has(g.dormant) ? `  (dormant: ${g.dormant})` : ''}`);
  }
  return lines.join('\n');
}

/**
 * The line under a seat's name: its level, form, cast mode, whether its toggle runs, its uses, its stacks and its
 * second face; or, for a dormant seat, why.
 */
export function seatMeta(seat: Seat): string {
  if (seat.dormant !== null) {
    return `Dormant: ${seat.dormant}`;
  }
  const info = seat.info;
  if (info === null) {
    return '';
  }
  const parts = [
    `Level ${info.level}`,
    info.form?.replace(/_/g, ' '),
    info.cast_mode,
    info.active === true ? 'on' : null,
    (info.uses_max ?? 0) > 1 ? `${info.uses ?? 0}/${info.uses_max} uses` : null,
    (info.stacks ?? 0) > 0 ? `${info.stacks} stacks` : null,
    has(info.meta) ? `second: ${info.meta.label}` : null,
  ];
  return parts.filter((t): t is string => typeof t === 'string' && t.length > 0).join('  ');
}

/** The fit the server sent on a stone's card for a seat; unknown when it sent none. */
export function fitHint(stone: ItemInfo, slot: number): string {
  return stone.fits?.find((f) => f.slot === slot)?.fit ?? 'unknown';
}

/** What a carried item would do on a seat: a stone's fit, learn for a form, nothing for anything else. */
export function dropHint(item: ItemInfo, slot: number): string {
  return has(item.glyph) ? fitHint(item, slot) : has(item.form) ? 'learn' : '';
}

export type HintTint = 'fit' | 'swap' | 'blocked';

/** The colour a drop hint tints a seat with: fit for a stone that fits or a form, swap for a misfit, else blocked. */
export function hintTint(hint: string): HintTint | null {
  if (hint.length === 0) {
    return null;
  }
  return hint === 'fits' || hint === 'learn' ? 'fit' : hint === 'misfit' ? 'swap' : 'blocked';
}

/** The glyph seated in a socket, if any. */
export function glyphAt(info: WeaveSeatInfo | null, socket: number): SeatGlyphInfo | null {
  return info?.glyphs.find((g) => g.socket === socket) ?? null;
}

/** The socket a stone dropped on the seat itself takes: the first free one, else the first. */
export function firstFreeSocket(info: WeaveSeatInfo | null): number {
  const taken = new Set((info?.glyphs ?? []).map((g) => g.socket));
  const sockets = info?.sockets ?? 0;
  for (let i = 0; i < sockets; i++) {
    if (!taken.has(i)) {
      return i;
    }
  }
  return 0;
}

/**
 * The intent for dropping a carried item on a seat: a form is learned there, a stone goes into the socket it was
 * dropped on (-1 for the seat itself: the first free socket). Anything else means nothing here.
 */
export function dropIntent(item: ItemInfo, slot: number, socket: number, info: WeaveSeatInfo | null): Message | null {
  if (has(item.form)) {
    return { t: 'learn_form', item: item.uid, slot };
  }
  if (has(item.glyph)) {
    return { t: 'socket_stone', stone: item.uid, slot, socket: socket >= 0 ? socket : firstFreeSocket(info) };
  }
  return null;
}

export interface SeatAction {
  label: string;
  intent: Message;
}

/**
 * The quick actions of a right click on a seat: a stone takes out of the socket clicked, the form unlearns from
 * the glyph clicked. An empty seat offers none.
 */
export function seatActions(seat: Seat, slot: number, socket: number, onGlyph: boolean): SeatAction[] {
  if (seat.key === null) {
    return [];
  }
  const glyph = socket >= 0 ? glyphAt(seat.info, socket) : null;
  if (glyph !== null) {
    return [{ label: `Take out ${glyph.name}`, intent: { t: 'unsocket_stone', slot, socket } }];
  }
  if (onGlyph) {
    return [{ label: `Unlearn ${seat.name}`, intent: { t: 'unlearn_form', slot } }];
  }
  return [];
}

/** The costs drawn on a seat's glyph, as the weave bar draws them: each resource above zero, rounded. */
export function seatCosts(seat: Seat): Array<{ resource: string; amount: number }> {
  return Object.entries(seat.costs)
    .filter(([, amount]) => amount > 0)
    .map(([resource, amount]) => ({ resource, amount: Math.round(amount) }));
}

/** The letter a seated glyph shows on its pip. */
export const glyphLetter = (glyph: SeatGlyphInfo): string => (glyph.name.length > 0 ? glyph.name.slice(0, 1) : '?');
