import { keyLabelOf } from '../logic/menu';
import { sameValue, type OptionEntry, type OptionGroup, type OptionPage, type OptionsState, type OptionValue, type Settings } from '../logic/options';

/**
 * What the Options pane decides without the page: which pages and groups show, what a change touched and how
 * Escape puts it back, the notice, and how values read in the controls (a slider's value with its unit, a key
 * slot, a colour field, a controller input). The desktop client's UI/Pages/OptionsPane.cs, apart from its
 * drawing, so the rules are tested without a document.
 */

/** A copy of a value, so a later change cannot reach a remembered one. */
export const copyValue = (v: OptionValue | null): OptionValue | null => (Array.isArray(v) ? [...v] : v);

/** Every entry's value as it stands, to tell afterwards what a change touched. */
export function snapshotValues(settings: Settings): Map<string, OptionValue | null> {
  const before = new Map<string, OptionValue | null>();
  for (const id of settings.catalog.entries.keys()) {
    before.set(id, copyValue(settings.value(id)));
  }
  return before;
}

/**
 * Records what a change touched against the values before it: each changed entry keeps its first original for
 * Escape to put back, and a changed entry that applies after a restart joins the restart notice. The desktop
 * OptionsPane.Track.
 */
export function recordChanges(state: OptionsState, settings: Settings, before: ReadonlyMap<string, OptionValue | null>): void {
  for (const [id, was] of before) {
    if (sameValue(was, settings.value(id))) {
      continue;
    }
    if (!state.originals.has(id)) {
      state.originals.set(id, was);
    }
    if (settings.catalog.entry(id)?.applies === 'restart') {
      state.needRestart.add(id);
    }
  }
}

/** Escape's undo: every entry changed this visit back to its original. False when nothing was changed. */
export function undoChanges(state: OptionsState, settings: Settings): boolean {
  if (state.originals.size === 0) {
    return false;
  }
  const originals = [...state.originals];
  state.originals.clear();
  for (const [id, value] of originals) {
    const back = copyValue(value);
    if (back !== null) {
      settings.set(id, back);
    }
  }
  state.needRestart.clear();
  return true;
}

/** The line under the list: how to undo or keep the visit's changes, and what waits for a restart. */
export function noticeText(state: OptionsState, settings: Settings): string {
  const parts: string[] = [];
  if (state.originals.size > 0) {
    parts.push('Escape undoes the changes; Done keeps them.');
  }
  const restart = [...state.needRestart].map((id) => settings.catalog.entry(id)?.name ?? id);
  if (restart.length > 0) {
    parts.push(`After a restart: ${restart.join(', ')}.`);
  }
  return parts.join(' ');
}

/** The pages whose gates hold, in order. */
export const shownPages = (pages: readonly OptionPage[], holds: (expr: string | null) => boolean): OptionPage[] => pages.filter((p) => holds(p.gate));

/** A group as the list shows it: its heading and the entries that pass their gates and the search. */
export interface ShownGroup {
  page: OptionPage;
  group: OptionGroup;
  title: string;
  entries: OptionEntry[];
}

/**
 * The groups the list shows: the selected page's, or with a search every page's entries whose name or
 * description holds it (headed "page: group"). Entries pass their gates; a group with none left is not shown.
 */
export function shownGroups(pages: readonly OptionPage[], page: string, search: string, holds: (expr: string | null) => boolean): ShownGroup[] {
  const query = search.trim();
  const lower = query.toLowerCase();
  const out: ShownGroup[] = [];
  for (const p of query.length > 0 ? pages : pages.filter((x) => x.id === page)) {
    for (const group of p.groups) {
      const entries = group.entries
        .filter((e) => holds(e.gate))
        .filter((e) => query.length === 0 || e.name.toLowerCase().includes(lower) || e.description.toLowerCase().includes(lower));
      if (entries.length > 0) {
        out.push({ page: p, group, title: query.length > 0 ? `${p.name}: ${group.name}` : group.name, entries });
      }
    }
  }
  return out;
}

/** A slider's value as stored: the control's number to four places, as the desktop rounds it. */
export const roundSlider = (v: number): number => Math.round(v * 10000) / 10000;

/** A slider's value as a player reads it: up to two decimals, a short unit glued on (1.25x, 80%), a word spaced (3 chunks). */
export function sliderText(value: number, unit: string): string {
  const text = String(Math.round(value * 100) / 100);
  return unit.length === 0 ? text : unit.length <= 2 ? `${text}${unit}` : `${text} ${unit}`;
}

/** A key slot's label: "-" when empty, a controller input as content names it, a key as a player reads it. */
export const slotLabel = (held: string, slot: number): string => (held.length === 0 ? '-' : slot === 2 ? held : keyLabelOf(held));

const HEX = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;

/** The colour field's value (#rrggbb) for a stored colour (#rgb, #rrggbb or #rrggbbaa); white when malformed. */
export function colorFieldValue(text: string): string {
  const m = HEX.exec(text);
  if (m === null) {
    return '#ffffff';
  }
  const hex = m[1].toLowerCase();
  return hex.length === 3 ? `#${hex[0]}${hex[0]}${hex[1]}${hex[1]}${hex[2]}${hex[2]}` : `#${hex.slice(0, 6)}`;
}

/** A picked colour (#rrggbb) as stored: the stored colour's alpha kept when it was not opaque, as the desktop writes it. */
export function storedColor(picked: string, previous: string): string {
  const alpha = /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})$/.exec(previous)?.[1]?.toLowerCase();
  return alpha !== undefined && alpha !== 'ff' ? `${picked.toLowerCase()}${alpha}` : picked.toLowerCase();
}

// ---------------------------------------------------------------- controllers

/** The standard gamepad layout's buttons by index, as the input content names them (the desktop's InputSetup). */
export const PAD_BUTTONS: readonly string[] = ['A', 'B', 'X', 'Y', 'LB', 'RB', 'LT', 'RT', 'Back', 'Start', 'LeftStick', 'RightStick', 'DpadUp', 'DpadDown', 'DpadLeft', 'DpadRight'];

/** The standard layout's axes by index: the names for pushing each toward its negative and its positive end. */
export const PAD_AXES: ReadonlyArray<readonly [string, string]> = [
  ['LeftStickLeft', 'LeftStickRight'],
  ['LeftStickUp', 'LeftStickDown'],
  ['RightStickLeft', 'RightStickRight'],
  ['RightStickUp', 'RightStickDown'],
];

/** How far a stick or trigger must travel to count, as on the desktop. */
export const PAD_THRESHOLD = 0.6;

/** A controller as the Gamepad API reads it (only what the names need). */
export interface PadState {
  readonly buttons: ReadonlyArray<{ readonly pressed: boolean; readonly value: number }>;
  readonly axes: ReadonlyArray<number>;
}

/** The inputs held on a standard-layout controller, as content names: buttons, triggers and sticks past the threshold. */
export function heldPadInputs(pad: PadState): string[] {
  const held: string[] = [];
  pad.buttons.forEach((b, i) => {
    const name = PAD_BUTTONS[i];
    if (name === undefined) {
      return;
    }
    // The triggers are axes on the desktop; here they are analogue buttons, read against the same threshold.
    const down = i === 6 || i === 7 ? b.value > PAD_THRESHOLD : b.pressed;
    if (down) {
      held.push(name);
    }
  });
  pad.axes.forEach((v, i) => {
    const pair = PAD_AXES[i];
    if (pair !== undefined && Math.abs(v) > PAD_THRESHOLD) {
      held.push(v < 0 ? pair[0] : pair[1]);
    }
  });
  return held;
}

/** The first input held now that was not held at the last reading: a fresh press, or null. */
export function freshPadInput(before: ReadonlySet<string>, now: Iterable<string>): string | null {
  for (const name of now) {
    if (!before.has(name)) {
      return name;
    }
  }
  return null;
}
