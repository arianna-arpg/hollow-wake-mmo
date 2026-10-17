import { humanize, num, str, type Obj } from './content';
import { canonicalKey } from './menu';
import type { YamlValue } from './yaml';

/**
 * Options as content (docs/design/options.md): the catalog of pages, groups and entries the Options pane draws,
 * the input actions every binding comes from, the player's settings over them, and the small expressions that
 * gate and require entries. The desktop client's UI/Logic/Options.cs, read from the same content.
 */

/** A value an entry holds: a toggle, a number, a choice or text, or a key entry's [primary, secondary, pad]. */
export type OptionValue = boolean | number | string | string[];

export interface OptionChoice {
  id: string;
  name: string;
}

export interface OptionEntry {
  id: string;
  name: string;
  description: string;
  kind: string;
  default: OptionValue | null;
  scope: string;
  applies: string;
  choices: OptionChoice[];
  min: number;
  max: number;
  step: number;
  unit: string;
  action: string | null;
  maxLength: number;
  gate: string | null;
  requires: string | null;
  requiresReason: string;
  pageId: string;
  groupId: string;
}

export interface OptionGroup {
  id: string;
  name: string;
  description: string;
  entries: OptionEntry[];
}

export interface OptionPage {
  id: string;
  name: string;
  description: string;
  order: number;
  icon: string;
  gate: string | null;
  groups: OptionGroup[];
}

/** An input action and its default bindings (input_actions content). */
export interface InputAction {
  id: string;
  name: string;
  group: string;
  context: string;
  order: number;
  keys: string[];
  pad: string[];
  held: boolean;
}

/** An action's bindings: a primary and a secondary key and a controller input; empty means unbound. */
export interface Binding {
  primary: string;
  secondary: string;
  pad: string;
}

export const bindingKeys = (b: Binding): string[] => [b.primary, b.secondary].filter((k) => k.length > 0);
export const bindingSlot = (b: Binding, index: number): string => (index === 0 ? b.primary : index === 1 ? b.secondary : b.pad);
export const bindingWith = (b: Binding, index: number, value: string): Binding =>
  index === 0 ? { ...b, primary: value } : index === 1 ? { ...b, secondary: value } : { ...b, pad: value };
export const bindingJson = (b: Binding): string[] => [b.primary, b.secondary, b.pad];
export const bindingOf = (a: InputAction): Binding => ({ primary: a.keys[0] ?? '', secondary: a.keys[1] ?? '', pad: a.pad[0] ?? '' });

export const keyEntryId = (action: string): string => `key.${action}`;

const isObj = (v: YamlValue | undefined): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);
const objects = (v: YamlValue | undefined): Obj[] => (Array.isArray(v) ? v.filter(isObj) : []);
const strings = (v: YamlValue | undefined): string[] => (Array.isArray(v) ? v.map((x) => str(x)).filter((s) => s.length > 0) : typeof v === 'string' && v.length > 0 ? [v] : []);
const strOrNull = (v: YamlValue | undefined): string | null => (typeof v === 'string' && v.length > 0 ? v : null);

function toValue(v: YamlValue | undefined): OptionValue | null {
  if (typeof v === 'boolean' || typeof v === 'number' || typeof v === 'string') {
    return v;
  }
  if (Array.isArray(v)) {
    return v.map((x) => str(x));
  }
  return null;
}

/** Deep equality of option values (arrays element by element). */
export function sameValue(a: OptionValue | null | undefined, b: OptionValue | null | undefined): boolean {
  if (Array.isArray(a) || Array.isArray(b)) {
    return Array.isArray(a) && Array.isArray(b) && a.length === b.length && a.every((x, i) => x === b[i]);
  }
  return a === b;
}

const copy = (v: OptionValue): OptionValue => (Array.isArray(v) ? [...v] : v);

/**
 * Every option and input action the content declares: pages in order, their groups with entries (binding
 * groups expanded into one key entry per action of the input action group), choices drawn from other kinds
 * resolved to ids and names. The validators in InterfaceKinds.cs keep the content sound.
 */
export class OptionsCatalog {
  constructor(
    readonly pages: OptionPage[],
    readonly actions: InputAction[],
    readonly entries: Map<string, OptionEntry>,
    readonly actionById: Map<string, InputAction>,
  ) {}

  entry(id: string): OptionEntry | undefined {
    return this.entries.get(id);
  }

  static load(kinds: Map<string, Obj[]>): OptionsCatalog {
    const actions: InputAction[] = (kinds.get('input_actions') ?? [])
      .map((a) => ({
        id: str(a.id),
        name: str(a.name, humanize(str(a.id))),
        group: str(a.group, 'general'),
        context: str(a.context, 'world'),
        order: num(a.order),
        keys: strings(a.keys),
        pad: strings(a.pad),
        held: a.held === true,
      }))
      .sort((x, y) => x.order - y.order || (x.id < y.id ? -1 : x.id > y.id ? 1 : 0));
    const choicesFrom = (kind: string): OptionChoice[] =>
      (kinds.get(kind) ?? [])
        .map((o) => ({ id: str(o.id), name: str(o.name, humanize(str(o.id))) }))
        .sort((x, y) => (x.id < y.id ? -1 : x.id > y.id ? 1 : 0));
    const pages: OptionPage[] = [];
    const entries = new Map<string, OptionEntry>();
    for (const p of kinds.get('ui_options') ?? []) {
      const pageId = str(p.id);
      const groups: OptionGroup[] = [];
      for (const g of objects(p.groups)) {
        const groupId = str(g.id);
        const list: OptionEntry[] = [];
        for (const e of objects(g.entries)) {
          const choices = objects(e.choices).map((c) => ({ id: str(c.id), name: str(c.name, humanize(str(c.id))) }));
          const from = strOrNull(e.choices_from);
          if (from !== null) {
            choices.push(...choicesFrom(from));
          }
          list.push({
            id: str(e.id),
            name: str(e.name, humanize(str(e.id))),
            description: str(e.description),
            kind: str(e.kind, 'toggle'),
            default: toValue(e.default),
            scope: str(e.scope, 'client'),
            applies: str(e.applies, 'live'),
            choices,
            min: num(e.min),
            max: num(e.max, 1),
            step: num(e.step, 0.1),
            unit: str(e.unit),
            action: strOrNull(e.action),
            maxLength: num(e.max_length, 120),
            gate: strOrNull(e.gate),
            requires: strOrNull(e.requires),
            requiresReason: str(e.requires_reason),
            pageId,
            groupId,
          });
        }
        const bindings = strOrNull(g.bindings);
        if (bindings !== null) {
          for (const a of actions.filter((x) => x.group === bindings)) {
            list.push({
              id: keyEntryId(a.id),
              name: a.name,
              description: '',
              kind: 'key',
              default: bindingJson(bindingOf(a)),
              scope: 'client',
              applies: 'live',
              choices: [],
              min: 0,
              max: 1,
              step: 1,
              unit: '',
              action: a.id,
              maxLength: 0,
              gate: null,
              requires: null,
              requiresReason: '',
              pageId,
              groupId,
            });
          }
        }
        for (const entry of list) {
          entries.set(entry.id, entry);
        }
        groups.push({ id: groupId, name: str(g.name, humanize(groupId)), description: str(g.description), entries: list });
      }
      pages.push({ id: pageId, name: str(p.name, humanize(pageId)), description: str(p.description), order: num(p.order), icon: str(p.icon), gate: strOrNull(p.gate), groups });
    }
    pages.sort((x, y) => x.order - y.order || (x.id < y.id ? -1 : x.id > y.id ? 1 : 0));
    return new OptionsCatalog(pages, actions, entries, new Map(actions.map((a) => [a.id, a])));
  }
}

const isColor = (text: string): boolean => /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(text);

/**
 * The player's settings for one profile: a value per option entry, defaults from the content for anything
 * missing or malformed, written as JSON. Every change reaches the listeners with the entry that changed; the
 * client applies what it understands and the rest is kept for later systems. Account and character scoped
 * entries live here too until the account store carries them.
 */
export class Settings {
  private readonly values = new Map<string, OptionValue>();
  private readonly listeners: Array<(entry: OptionEntry) => void> = [];

  constructor(readonly catalog: OptionsCatalog) {}

  /** Listens to every change; returns a function that stops listening. */
  onChange(listener: (entry: OptionEntry) => void): () => void {
    this.listeners.push(listener);
    return () => {
      const i = this.listeners.indexOf(listener);
      if (i >= 0) {
        this.listeners.splice(i, 1);
      }
    };
  }

  /** The value of an entry, or its default. */
  value(id: string): OptionValue | null {
    return this.values.get(id) ?? this.catalog.entry(id)?.default ?? null;
  }

  toggle(id: string, fallback = false): boolean {
    const v = this.value(id);
    return typeof v === 'boolean' ? v : fallback;
  }

  number(id: string, fallback = 0): number {
    const v = this.value(id);
    return typeof v === 'number' && Number.isFinite(v) ? v : fallback;
  }

  text(id: string, fallback = ''): string {
    const v = this.value(id);
    return typeof v === 'string' ? v : fallback;
  }

  /** An action's effective bindings: the player's rebinds over the content's defaults. */
  bindings(action: string): Binding {
    const stored = this.values.get(keyEntryId(action));
    if (Array.isArray(stored)) {
      return { primary: stored[0] ?? '', secondary: stored[1] ?? '', pad: stored[2] ?? '' };
    }
    const a = this.catalog.actionById.get(action);
    return a === undefined ? { primary: '', secondary: '', pad: '' } : bindingOf(a);
  }

  isDefault(id: string): boolean {
    return !this.values.has(id) || sameValue(this.values.get(id), this.catalog.entry(id)?.default);
  }

  /** Sets a value when it is valid for the entry's kind; returns whether it changed. */
  set(id: string, value: OptionValue): boolean {
    const entry = this.catalog.entry(id);
    if (entry === undefined || !Settings.valid(entry, value)) {
      return false;
    }
    if (sameValue(this.value(id), value)) {
      return false;
    }
    if (sameValue(value, entry.default)) {
      this.values.delete(id);
    } else {
      this.values.set(id, copy(value));
    }
    for (const listener of [...this.listeners]) {
      listener(entry);
    }
    return true;
  }

  setBinding(action: string, binding: Binding): boolean {
    return this.set(keyEntryId(action), bindingJson(binding));
  }

  reset(id: string): void {
    const d = this.catalog.entry(id)?.default;
    if (d !== undefined && d !== null) {
      this.set(id, copy(d));
    }
  }

  resetGroup(pageId: string, groupId: string): void {
    for (const e of [...this.catalog.entries.values()].filter((x) => x.pageId === pageId && x.groupId === groupId)) {
      this.reset(e.id);
    }
  }

  resetPage(pageId: string): void {
    for (const e of [...this.catalog.entries.values()].filter((x) => x.pageId === pageId)) {
      this.reset(e.id);
    }
  }

  /** Whether a value fits the entry: the kind's type, a choice among its choices, a number in range, a colour. */
  static valid(entry: OptionEntry, value: unknown): value is OptionValue {
    switch (entry.kind) {
      case 'toggle':
        return typeof value === 'boolean';
      case 'slider':
        return typeof value === 'number' && Number.isFinite(value) && value >= entry.min - 1e-9 && value <= entry.max + 1e-9;
      case 'choice':
        return typeof value === 'string' && entry.choices.some((c) => c.id === value);
      case 'key':
        return Array.isArray(value) && value.length === 3 && value.every((x) => typeof x === 'string');
      case 'color':
        return typeof value === 'string' && isColor(value);
      case 'text':
        return typeof value === 'string' && value.length <= entry.maxLength;
      default:
        return false;
    }
  }

  /**
   * The actions sharing a context whose bindings include a key (other than the asking action): keys against
   * the keyboard slots, a controller input (pad) against the controller slot.
   */
  conflicts(action: string, key: string, pad = false): InputAction[] {
    const mine = this.catalog.actionById.get(action);
    if (mine === undefined || key.length === 0) {
      return [];
    }
    const wanted = canonicalKey(key);
    return this.catalog.actions.filter((other) => {
      if (other.id === action || other.context !== mine.context) {
        return false;
      }
      const b = this.bindings(other.id);
      return pad ? b.pad === key : bindingKeys(b).some((k) => canonicalKey(k) === wanted);
    });
  }

  /** Binds a key to an action's slot; with swap, a conflicting action takes the key this slot held. */
  rebind(action: string, slot: number, key: string, swap: boolean): void {
    const pad = slot === 2;
    const mine = this.bindings(action);
    const previous = bindingSlot(mine, slot);
    for (const other of this.conflicts(action, key, pad)) {
      let b = this.bindings(other.id);
      for (let i = pad ? 2 : 0; i < (pad ? 3 : 2); i++) {
        const held = bindingSlot(b, i);
        if (held.length > 0 && (pad ? held === key : canonicalKey(held) === canonicalKey(key))) {
          b = bindingWith(b, i, swap ? previous : '');
        }
      }
      this.setBinding(other.id, b);
    }
    this.setBinding(action, bindingWith(mine, slot, key));
  }

  // ------------------------------------------------------------ the stored form

  serialize(): string {
    const values: Record<string, OptionValue> = {};
    for (const id of [...this.values.keys()].sort()) {
      values[id] = copy(this.values.get(id)!);
    }
    return JSON.stringify({ version: 1, values }, null, 2);
  }

  /** Reads stored settings; anything unknown or malformed takes the default. */
  static load(catalog: OptionsCatalog, json: string | null | undefined): Settings {
    const settings = new Settings(catalog);
    if (json === null || json === undefined || json.trim().length === 0) {
      return settings;
    }
    try {
      const root = JSON.parse(json) as { values?: Record<string, unknown> };
      if (typeof root === 'object' && root !== null && typeof root.values === 'object' && root.values !== null) {
        for (const [id, value] of Object.entries(root.values)) {
          const entry = catalog.entry(id);
          if (entry !== undefined && Settings.valid(entry, value)) {
            settings.values.set(id, copy(value));
          }
        }
      }
    } catch {
      // A broken store is no settings at all: defaults.
    }
    return settings;
  }
}

// ---------------------------------------------------------------- gates

/**
 * Evaluates the small expressions options gate and require with: numbers, identifiers with dots (client.web,
 * option.shadows), has("flag"), ! && ||, comparisons, arithmetic, parentheses and c ? a : b. An unknown
 * identifier reads zero; a blank expression is true. Throws on a malformed expression.
 */
export function evaluateGate(text: string | null | undefined, identifier: (name: string) => number | null | undefined, has: (flag: string) => boolean = () => false): number {
  if (text === null || text === undefined || text.trim().length === 0) {
    return 1;
  }
  let i = 0;
  const ws = () => {
    while (i < text.length && /\s/.test(text[i])) {
      i++;
    }
  };
  const eat = (token: string): boolean => {
    ws();
    if (text.startsWith(token, i)) {
      i += token.length;
      return true;
    }
    return false;
  };
  const fail = (why: string): never => {
    throw new Error(`${why} in '${text}'`);
  };
  const ternary = (): number => {
    const c = or();
    if (eat('?')) {
      const a = ternary();
      if (!eat(':')) {
        fail("expected ':'");
      }
      const b = ternary();
      return c !== 0 ? a : b;
    }
    return c;
  };
  const or = (): number => {
    let v = and();
    while (eat('||')) {
      const r = and();
      v = v !== 0 || r !== 0 ? 1 : 0;
    }
    return v;
  };
  const and = (): number => {
    let v = compare();
    while (eat('&&')) {
      const r = compare();
      v = v !== 0 && r !== 0 ? 1 : 0;
    }
    return v;
  };
  const compare = (): number => {
    let v = sum();
    for (;;) {
      if (eat('==')) {
        v = Math.abs(v - sum()) < 1e-9 ? 1 : 0;
      } else if (eat('!=')) {
        v = Math.abs(v - sum()) >= 1e-9 ? 1 : 0;
      } else if (eat('<=')) {
        v = v <= sum() ? 1 : 0;
      } else if (eat('>=')) {
        v = v >= sum() ? 1 : 0;
      } else if (eat('<')) {
        v = v < sum() ? 1 : 0;
      } else if (eat('>')) {
        v = v > sum() ? 1 : 0;
      } else {
        return v;
      }
    }
  };
  const sum = (): number => {
    let v = product();
    for (;;) {
      if (eat('+')) {
        v += product();
      } else if (eat('-')) {
        v -= product();
      } else {
        return v;
      }
    }
  };
  const product = (): number => {
    let v = unary();
    for (;;) {
      if (eat('*')) {
        v *= unary();
      } else if (eat('/')) {
        const d = unary();
        v = d === 0 ? 0 : v / d;
      } else {
        return v;
      }
    }
  };
  const unary = (): number => {
    if (eat('!')) {
      return unary() === 0 ? 1 : 0;
    }
    if (eat('-')) {
      return -unary();
    }
    return atom();
  };
  const atom = (): number => {
    ws();
    if (eat('(')) {
      const v = ternary();
      if (!eat(')')) {
        fail("expected ')'");
      }
      return v;
    }
    if (i < text.length && /[0-9.]/.test(text[i])) {
      const start = i;
      while (i < text.length && /[0-9.]/.test(text[i])) {
        i++;
      }
      let n = Number(text.slice(start, i));
      if (Number.isNaN(n)) {
        fail('a malformed number');
      }
      if (text[i] === '%') {
        i++;
        n /= 100;
      }
      return n;
    }
    if (i < text.length && /[A-Za-z_]/.test(text[i])) {
      const start = i;
      while (i < text.length && /[A-Za-z0-9_.]/.test(text[i])) {
        i++;
      }
      const name = text.slice(start, i);
      if (name === 'has' && eat('(')) {
        ws();
        if (text[i] !== '"') {
          fail('has() takes a quoted flag');
        }
        const end = text.indexOf('"', i + 1);
        if (end < 0) {
          fail('an unclosed string');
        }
        const flag = text.slice(i + 1, end);
        i = end + 1;
        if (!eat(')')) {
          fail("expected ')'");
        }
        return has(flag) ? 1 : 0;
      }
      if (name === 'true') {
        return 1;
      }
      if (name === 'false') {
        return 0;
      }
      return identifier(name) ?? 0;
    }
    return fail(`unexpected '${i < text.length ? text.slice(i) : 'end'}'`);
  };
  const value = ternary();
  ws();
  if (i < text.length) {
    fail(`unexpected '${text.slice(i)}'`);
  }
  return value;
}

/** Whether a gate holds; a malformed expression does not. */
export function gateHolds(text: string | null | undefined, identifier: (name: string) => number | null | undefined, has?: (flag: string) => boolean): boolean {
  try {
    return evaluateGate(text, identifier, has) !== 0;
  } catch {
    return false;
  }
}

/**
 * The identifiers gates read: client facts (client.web, client.desktop, client.developer) from the caller and
 * option.<id> from the settings (a toggle reads one or zero, a number itself, a choice one when set).
 */
export function gateFacts(settings: Settings, client: (name: string) => number | null | undefined): (name: string) => number | null | undefined {
  return (name) => {
    if (name.startsWith('option.')) {
      const v = settings.value(name.slice(7));
      return typeof v === 'boolean' ? (v ? 1 : 0) : typeof v === 'number' ? v : typeof v === 'string' ? (v.length > 0 ? 1 : 0) : 0;
    }
    return client(name);
  };
}
