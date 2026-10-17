import { inMenuOnly, type UiPage } from './content';

export type TrayState = 'hidden' | 'sealed' | 'open';

/** What the server says of one page. */
export interface PageStatus {
  exists: boolean;
  sealed: string | null;
  pips: number;
}

export interface TrayRow {
  page: UiPage;
  state: TrayState;
  reason: string | null;
  pips: number;
  keyLabel: string;
  showing: boolean;
}

/**
 * Folds the pages into the menu tray: every page content declares, hidden, sealed or open as the page state
 * says, with its pips and the label of its action's first key; a page the state does not mention is open with
 * the fallback's pips, and a menu page (Options) is the client's own, never hidden or sealed. The desktop
 * client's UI/Logic/MenuFold.cs.
 */
export function foldTray(
  pages: readonly UiPage[],
  served: ReadonlyMap<string, PageStatus> | null,
  fallbackPips: (id: string) => number,
  showing: (id: string) => boolean,
  keyLabel: (page: UiPage) => string = () => '',
): TrayRow[] {
  return pages.map((page) => {
    const label = keyLabel(page);
    const status = inMenuOnly(page) ? undefined : served?.get(page.id);
    if (status !== undefined) {
      if (!status.exists) {
        return { page, state: 'hidden', reason: null, pips: 0, keyLabel: label, showing: false };
      }
      return { page, state: status.sealed === null ? 'open' : 'sealed', reason: status.sealed, pips: Math.max(0, status.pips), keyLabel: label, showing: showing(page.id) };
    }
    return { page, state: 'open', reason: null, pips: Math.max(0, fallbackPips(page.id)), keyLabel: label, showing: showing(page.id) };
  });
}

/**
 * The page state as the tray reads it: every content page absent until the state names it. A page named more
 * than once (a container page lists itself once per container) exists, is sealed only when every entry is
 * (with the first reason), and shows the largest pip count. The desktop's MenuFold.Served.
 */
export function servedPages(
  contentPages: readonly string[],
  entries: ReadonlyArray<{ id: string; sealed: boolean; reason?: string | null; pips: number }>,
): Map<string, PageStatus> {
  const map = new Map<string, PageStatus>(contentPages.map((id) => [id, { exists: false, sealed: null, pips: 0 }]));
  for (const e of entries) {
    const sealedWith = e.sealed ? e.reason ?? 'Sealed here' : null;
    const prior = map.get(e.id);
    map.set(e.id, prior !== undefined && prior.exists
      ? { exists: true, sealed: prior.sealed !== null && sealedWith !== null ? prior.sealed : null, pips: Math.max(prior.pips, e.pips) }
      : { exists: true, sealed: sealedWith, pips: e.pips });
  }
  return map;
}

export const trayBadge = (rows: readonly TrayRow[]): number => rows.filter((r) => r.state === 'open').reduce((n, r) => n + r.pips, 0);

export const badgeText = (pips: number, cap = 9): string => (pips <= 0 ? '' : pips > cap ? `${cap}+` : String(pips));

export const traySignature = (rows: readonly TrayRow[]): string =>
  rows.map((r) => `${r.page.id}:${r.state}:${r.reason ?? ''}:${r.pips}:${r.keyLabel}:${r.showing}`).join('|');

/** A key as content writes it: a key, optionally with modifiers joined by plus signs. */
export interface KeyName {
  key: string;
  shift: boolean;
  ctrl: boolean;
  alt: boolean;
}

export function parseKeyName(text: string | undefined | null): KeyName {
  let shift = false;
  let ctrl = false;
  let alt = false;
  let key = '';
  for (const part of (text ?? '').split('+').map((p) => p.trim()).filter((p) => p.length > 0)) {
    const lower = part.toLowerCase();
    if (lower === 'shift') {
      shift = true;
    } else if (lower === 'ctrl' || lower === 'control') {
      ctrl = true;
    } else if (lower === 'alt') {
      alt = true;
    } else {
      key = part;
    }
  }
  return { key, shift, ctrl, alt };
}

/** A key name from its parts, as content writes it. */
export const composeKeyName = (key: string, shift = false, ctrl = false, alt = false): string =>
  `${ctrl ? 'Ctrl+' : ''}${alt ? 'Alt+' : ''}${shift ? 'Shift+' : ''}${key}`;

/** A key name in one spelling (modifiers in a fixed order, a letter upper case), for comparing bindings. */
export function canonicalKey(text: string | undefined | null): string {
  const k = parseKeyName(text);
  if (k.key.length === 0) {
    return '';
  }
  const key = k.key.length === 1 ? k.key.toUpperCase() : k.key === 'Esc' ? 'Escape' : k.key === 'Return' ? 'Enter' : k.key;
  return `${k.alt ? 'Alt+' : ''}${k.ctrl ? 'Ctrl+' : ''}${k.shift ? 'Shift+' : ''}${key}`;
}

export function keyLabelOf(text: string | undefined | null): string {
  const k = parseKeyName(text);
  if (k.key.length === 0) {
    return '';
  }
  return `${k.ctrl ? 'Ctrl+' : ''}${k.alt ? 'Alt+' : ''}${k.shift ? 'Shift+' : ''}${k.key.length === 1 ? k.key.toUpperCase() : k.key}`;
}

/** Named keys whose browser code differs from their content name. */
const CODES: Record<string, string> = {
  Esc: 'Escape',
  Return: 'Enter',
  Up: 'ArrowUp',
  Down: 'ArrowDown',
  Left: 'ArrowLeft',
  Right: 'ArrowRight',
  QuoteLeft: 'Backquote',
  Apostrophe: 'Quote',
};

const NAMES: Record<string, string> = {
  Escape: 'Escape',
  Enter: 'Enter',
  NumpadEnter: 'Enter',
  ArrowUp: 'Up',
  ArrowDown: 'Down',
  ArrowLeft: 'Left',
  ArrowRight: 'Right',
  Backquote: 'QuoteLeft',
  Quote: 'Apostrophe',
};

/** The browser's KeyboardEvent.code for a key name (KeyI, Digit1, Tab, ArrowUp, F1). */
export function webCode(name: KeyName): string {
  if (name.key.length === 1) {
    const c = name.key.toUpperCase();
    if (c >= 'A' && c <= 'Z') {
      return `Key${c}`;
    }
    if (c >= '0' && c <= '9') {
      return `Digit${c}`;
    }
  }
  return CODES[name.key] ?? name.key;
}

/** The content key name of a browser code (KeyI is I, Digit1 is 1, Numpad1 is 1, ArrowUp is Up); empty for modifiers. */
export function keyOfCode(code: string): string {
  if (/^Key[A-Z]$/.test(code)) {
    return code.slice(3);
  }
  if (/^(Digit|Numpad)[0-9]$/.test(code)) {
    return code.slice(code.length - 1);
  }
  if (/^(Shift|Control|Alt|Meta)(Left|Right)$/.test(code) || code === 'CapsLock' || code === 'ContextMenu') {
    return '';
  }
  return NAMES[code] ?? code;
}

/** A keyboard event as a content key name with its modifiers (Shift+Tab, F10, W); empty for a bare modifier. */
export function eventKeyName(event: { code: string; shiftKey: boolean; ctrlKey: boolean; altKey: boolean }): string {
  const key = keyOfCode(event.code);
  return key.length === 0 ? '' : composeKeyName(key, event.shiftKey, event.ctrlKey, event.altKey);
}

/** Whether a keyboard event is the key a content key name names (modifiers must match). */
export function keyMatches(event: { code: string; shiftKey: boolean; ctrlKey: boolean; altKey: boolean }, text: string | undefined | null): boolean {
  const k = parseKeyName(text);
  if (k.key.length === 0 || event.shiftKey !== k.shift || event.ctrlKey !== k.ctrl || event.altKey !== k.alt) {
    return false;
  }
  return event.code === webCode(k) || canonicalKey(keyOfCode(event.code)) === canonicalKey(k.key);
}
