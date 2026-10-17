import type { UiPage } from './content';

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
 * says, with its pips and key; a page the state does not mention is open with the fallback's pips. The
 * desktop client's UI/Logic/MenuFold.cs.
 */
export function foldTray(
  pages: readonly UiPage[],
  served: ReadonlyMap<string, PageStatus> | null,
  fallbackPips: (id: string) => number,
  showing: (id: string) => boolean,
): TrayRow[] {
  return pages.map((page) => {
    const keyLabel = keyLabelOf(page.key);
    const status = served?.get(page.id);
    if (status !== undefined) {
      if (!status.exists) {
        return { page, state: 'hidden', reason: null, pips: 0, keyLabel, showing: false };
      }
      return { page, state: status.sealed === null ? 'open' : 'sealed', reason: status.sealed, pips: Math.max(0, status.pips), keyLabel, showing: showing(page.id) };
    }
    return { page, state: 'open', reason: null, pips: Math.max(0, fallbackPips(page.id)), keyLabel, showing: showing(page.id) };
  });
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

export function keyLabelOf(text: string | undefined | null): string {
  const k = parseKeyName(text);
  if (k.key.length === 0) {
    return '';
  }
  return `${k.ctrl ? 'Ctrl+' : ''}${k.alt ? 'Alt+' : ''}${k.shift ? 'Shift+' : ''}${k.key.length === 1 ? k.key.toUpperCase() : k.key}`;
}

/** The browser's KeyboardEvent.code for a key name (KeyI, Digit1, Tab, F1). */
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
  return name.key === 'Esc' ? 'Escape' : name.key === 'Return' ? 'Enter' : name.key;
}

/** Whether a keyboard event is the key a content key name names (modifiers must match). */
export function keyMatches(event: { code: string; shiftKey: boolean; ctrlKey: boolean; altKey: boolean }, text: string | undefined | null): boolean {
  const k = parseKeyName(text);
  return k.key.length > 0 && event.code === webCode(k) && event.shiftKey === k.shift && event.ctrlKey === k.ctrl && event.altKey === k.alt;
}
