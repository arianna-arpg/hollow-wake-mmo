import { CONTENT_FILES } from './contentFiles';
import { parseYamlDocuments, type YamlValue } from './yaml';

/**
 * The interface's content as the browser reads it: themes, the icon set, pages, the layout and the sheet,
 * and the names it labels things with. The same reading as the desktop client's UI/Logic/UiContent.cs and
 * ContentNames.cs, from the same files.
 */

export type Obj = { [key: string]: YamlValue };

const isObj = (v: YamlValue | undefined): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);

export const str = (v: YamlValue | undefined, fallback = ''): string =>
  typeof v === 'string' ? v : typeof v === 'number' || typeof v === 'boolean' ? String(v) : fallback;
export const num = (v: YamlValue | undefined, fallback = 0): number =>
  typeof v === 'number' ? v : typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v)) ? Number(v) : fallback;
const strings = (v: YamlValue | undefined): string[] =>
  Array.isArray(v) ? v.map((x) => str(x)).filter((s) => s.length > 0) : typeof v === 'string' ? v.split(' ').filter((s) => s.length > 0) : [];
const strMap = (v: YamlValue | undefined): Record<string, string> =>
  isObj(v) ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, str(x)])) : {};
const numMap = (v: YamlValue | undefined): Record<string, number> =>
  isObj(v) ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, num(x)])) : {};
const objects = (v: YamlValue | undefined): Obj[] => (Array.isArray(v) ? v.filter(isObj) : []);

export function humanize(id: string): string {
  const last = id.includes('.') ? id.slice(id.lastIndexOf('.') + 1) : id;
  return last.split('_').filter((w) => w.length > 0).map((w) => w[0].toUpperCase() + w.slice(1)).join(' ');
}

/** Entries of every kind in a set of YAML texts, in file path order, later entries replacing earlier ones by id. */
export function rawKinds(files: Record<string, string>): Map<string, Obj[]> {
  const byKind = new Map<string, Map<string, Obj>>();
  for (const path of Object.keys(files).sort()) {
    let docs: YamlValue[];
    try {
      docs = parseYamlDocuments(files[path]);
    } catch (error) {
      console.warn(`content ${path} could not be read`, error);
      continue;
    }
    for (const doc of docs) {
      if (!isObj(doc) || typeof doc.kind !== 'string') {
        continue;
      }
      const kind = byKind.get(doc.kind) ?? new Map<string, Obj>();
      byKind.set(doc.kind, kind);
      const entries = doc.entries;
      const list: Array<[string, Obj]> = Array.isArray(entries)
        ? entries.filter(isObj).map((e) => [str(e.id), e])
        : isObj(entries) ? Object.entries(entries).filter(([, e]) => isObj(e)).map(([id, e]) => [id, { ...(e as Obj), id: (e as Obj).id ?? id }]) : [];
      for (const [id, entry] of list) {
        if (id.length === 0) {
          continue;
        }
        if (entry.remove === true) {
          kind.delete(id);
        } else if (entry.patch === true && kind.has(id)) {
          kind.set(id, mergePatch(kind.get(id)!, entry));
        } else {
          kind.set(id, entry);
        }
      }
    }
  }
  return new Map([...byKind].map(([k, m]) => [k, [...m.values()]]));
}

function mergePatch(target: Obj, patch: Obj): Obj {
  const out: Obj = { ...target };
  for (const [k, v] of Object.entries(patch)) {
    if (k === 'patch') {
      continue;
    }
    if (v === null) {
      delete out[k];
    } else if (isObj(v) && isObj(out[k])) {
      out[k] = mergePatch(out[k] as Obj, v);
    } else {
      out[k] = v;
    }
  }
  return out;
}

// ---------------------------------------------------------------- the interface content

export interface FontSpec {
  families: string[];
  size: number;
  weight: number;
}

export interface Swatch {
  fill: string;
  deep: string;
}

export interface UiTheme {
  id: string;
  name: string;
  default: boolean;
  frame: string;
  iconSet: string;
  fonts: Record<string, FontSpec>;
  surface: Record<string, string>;
  material: Record<string, number>;
  resources: Record<string, Swatch>;
  resourceFallback: Swatch;
  threads: Record<string, string>;
  rarities: Record<string, string>;
  notices: Record<string, string>;
  states: Record<string, string>;
}

export interface UiIconSet {
  id: string;
  box: number;
  stroke: number;
  icons: Record<string, string>;
  filled: Set<string>;
  pools: Record<string, string>;
  statuses: Record<string, string>;
  looks: Record<string, string>;
  slots: Record<string, string>;
  fallback: string;
}

export interface UiPage {
  id: string;
  name: string;
  /** The input action whose bindings request the page (page_<id> unless content names another). */
  action: string;
  icon: string;
  order: number;
  place: string;
  description: string;
  /** The kind of container the page shows (reliquary, stash), or null for a page of its own. */
  container: string | null;
}

export const isCore = (p: UiPage): boolean => p.place === 'core';
export const onRibbon = (p: UiPage): boolean => p.place === 'ribbon' || p.place === 'popout';
/** A client page reached from the menu alone (Options); the page state never hides it. */
export const inMenuOnly = (p: UiPage): boolean => p.place === 'menu';

export interface UiOrb {
  side: string;
  fill: string;
  overlays: string[];
  rim: string[];
  rings: string[];
}

export interface UiLayout {
  id: string;
  orbs: UiOrb[];
  ranks: string[];
  panes: Record<string, string>;
  doll: Record<string, [number, number]>;
  metrics: Record<string, number>;
}

export interface UiSheetSection {
  id: string;
  name: string;
  kind: string;
  attributes: string[];
  categories: string[];
  exclude: string[];
  pattern?: string;
  cap?: string;
}

export interface UiContent {
  theme: UiTheme;
  themes: UiTheme[];
  icons: UiIconSet;
  iconSets: Map<string, UiIconSet>;
  pages: UiPage[];
  layout: UiLayout;
  sheet: UiSheetSection[];
}

/**
 * The same content drawn with a theme and an icon set the player picked: the theme by id (else this one), the
 * icon set by id, else the one the theme names, else this one. The desktop's UiContent.WithLook.
 */
export function withLook(ui: UiContent, themeId?: string | null, iconSetId?: string | null): UiContent {
  const theme = ui.themes.find((t) => t.id === themeId) ?? ui.theme;
  const icons = (iconSetId ? ui.iconSets.get(iconSetId) : undefined) ?? ui.iconSets.get(theme.iconSet) ?? ui.icons;
  return { ...ui, theme, icons };
}

export const pageById = (ui: UiContent, id: string): UiPage | undefined => ui.pages.find((p) => p.id === id);
export const corePage = (ui: UiContent): UiPage | undefined => ui.pages.find(isCore);

export const metric = (layout: UiLayout, name: string, fallback: number): number => layout.metrics[name] ?? fallback;

/** A world pane kind's (or a rank's) place on the primacy ladder; pages rank lowest. */
export function rankOf(layout: UiLayout, kindOrRank: string): number {
  const rank = layout.panes[kindOrRank] ?? kindOrRank;
  const i = layout.ranks.indexOf(rank);
  return i < 0 ? 0 : i;
}

function themeFrom(o: Obj): UiTheme {
  const fallback: Swatch = { fill: '#9a9a9a', deep: '#333333' };
  const swatch = (s: YamlValue | undefined, base: Swatch): Swatch => (isObj(s) ? { fill: str(s.fill, base.fill), deep: str(s.deep, base.deep) } : base);
  const resourceFallback = swatch(o.resource_fallback, fallback);
  const resources: Record<string, Swatch> = {};
  if (isObj(o.resources)) {
    for (const [k, v] of Object.entries(o.resources)) {
      resources[k] = swatch(v, resourceFallback);
    }
  }
  const fonts: Record<string, FontSpec> = {};
  if (isObj(o.fonts)) {
    for (const [role, f] of Object.entries(o.fonts)) {
      if (isObj(f)) {
        const families = strings(f.families);
        fonts[role] = { families: families.length > 0 ? families : ['serif'], size: num(f.size, 14), weight: num(f.weight, 400) };
      }
    }
  }
  return {
    id: str(o.id),
    name: str(o.name, str(o.id)),
    default: o.default === true,
    frame: str(o.frame, 'stone'),
    iconSet: str(o.icon_set, 'runic'),
    fonts,
    surface: strMap(o.surface),
    material: numMap(o.material),
    resources,
    resourceFallback,
    threads: strMap(o.threads),
    rarities: strMap(o.rarities),
    notices: strMap(o.notices),
    states: strMap(o.states),
  };
}

function iconSetFrom(o: Obj): UiIconSet {
  return {
    id: str(o.id),
    box: num(o.box, 24),
    stroke: num(o.stroke, 1.75),
    icons: strMap(o.icons),
    filled: new Set(strings(o.filled)),
    pools: strMap(o.pools),
    statuses: strMap(o.statuses),
    looks: strMap(o.looks),
    slots: strMap(o.slots),
    fallback: str(o.fallback, 'rune'),
  };
}

function layoutFrom(o: Obj): UiLayout {
  const doll: Record<string, [number, number]> = {};
  if (isObj(o.doll)) {
    for (const [slot, cell] of Object.entries(o.doll)) {
      if (Array.isArray(cell) && cell.length === 2) {
        doll[slot] = [num(cell[0]), num(cell[1])];
      }
    }
  }
  const ranks = strings(o.ranks);
  return {
    id: str(o.id, 'default'),
    orbs: objects(o.orbs).map((orb) => ({
      side: str(orb.side, 'left'),
      fill: str(orb.fill),
      overlays: strings(orb.overlays),
      rim: strings(orb.rim),
      rings: strings(orb.rings),
    })),
    ranks: ranks.length > 0 ? ranks : ['page', 'station', 'decision'],
    panes: strMap(o.panes),
    doll,
    metrics: numMap(o.metrics),
  };
}

/** Reads the interface content; a theme id picks a theme, else the default, else the first. */
export function loadUi(kinds: Map<string, Obj[]>, themeId?: string | null): UiContent {
  const themes = (kinds.get('ui_themes') ?? []).map(themeFrom).sort((a, b) => a.id.localeCompare(b.id));
  if (themes.length === 0) {
    themes.push(themeFrom({ id: 'plain', default: true }));
  }
  const theme = themes.find((t) => t.id === themeId) ?? themes.find((t) => t.default) ?? themes[0];
  const sets = (kinds.get('ui_icon_sets') ?? []).map(iconSetFrom);
  const icons = sets.find((s) => s.id === theme.iconSet) ?? sets[0] ?? iconSetFrom({ id: 'none' });
  const iconSets = new Map(sets.map((s) => [s.id, s]));
  const pages = (kinds.get('ui_pages') ?? [])
    .map((p) => ({
      id: str(p.id),
      name: str(p.name, str(p.id)),
      action: str(p.action, `page_${str(p.id)}`),
      icon: str(p.icon),
      order: num(p.order),
      place: str(p.place, 'ribbon'),
      description: str(p.description),
      container: typeof p.container === 'string' && p.container.length > 0 ? p.container : null,
    }))
    .sort((a, b) => a.order - b.order || a.id.localeCompare(b.id));
  const layouts = (kinds.get('ui_layouts') ?? []).map(layoutFrom);
  const layout = layouts.find((l) => l.id === 'default') ?? layouts[0] ?? layoutFrom({});
  const sheets = kinds.get('ui_sheets') ?? [];
  const sheetRaw = sheets.find((s) => str(s.id) === 'character') ?? sheets[0];
  const sheet = sheetRaw === undefined ? [] : objects(sheetRaw.sections).map((s) => ({
    id: str(s.id),
    name: str(s.name, str(s.id)),
    kind: str(s.kind, 'attributes'),
    attributes: strings(s.attributes),
    categories: strings(s.categories),
    exclude: strings(s.exclude),
    pattern: typeof s.pattern === 'string' ? s.pattern : undefined,
    cap: typeof s.cap === 'string' ? s.cap : undefined,
  }));
  return { theme, themes, icons, iconSets, pages, layout, sheet };
}

/** The icon id the set draws for an icon (the fallback when the set lacks it). */
export const resolveIcon = (set: UiIconSet, icon: string | undefined | null): string => (icon && set.icons[icon] !== undefined ? icon : set.fallback);

export const forPool = (set: UiIconSet, pool: string): string => resolveIcon(set, set.pools[pool]);
export const forStatus = (set: UiIconSet, status: string): string => resolveIcon(set, set.statuses[status]);
export const forSlot = (set: UiIconSet, slot: string): string => resolveIcon(set, set.slots[slot]);

/** A weave's glyph: its look, else a word of its key that names a look or form, else the fallback. */
export function weaveIcon(set: UiIconSet, look: string | undefined | null, key: string | undefined | null): string {
  if (look && set.looks[look] !== undefined) {
    return resolveIcon(set, set.looks[look]);
  }
  if (key) {
    for (const part of key.split(/[_[\]+,. ]/)) {
      if (part && set.looks[part] !== undefined) {
        return resolveIcon(set, set.looks[part]);
      }
    }
  }
  return set.fallback;
}

// ---------------------------------------------------------------- names from the rest of the content

export interface NamedEntry {
  id: string;
  name: string;
  description: string;
  order: number;
}

export interface AttributeInfo {
  id: string;
  name: string;
  category: string;
  description: string;
  percent: boolean;
  decimals: number | null;
  hidden: boolean;
  index: number;
}

export interface PassiveNode {
  id: string;
  name: string;
  kind: string;
  x: number;
  y: number;
  links: string[];
  realm: string;
  cost: number;
  calling: string | null;
  lines: string[];
  choice: string | null;
}

export interface ChoiceOption {
  id: string;
  name: string;
  lines: string[];
}

export interface ChoiceGroup {
  id: string;
  name: string;
  pick: number;
  options: ChoiceOption[];
}

/**
 * How a status is presented, read from its content (never from the simulation): its meta block's hidden keeps it
 * out of the status rows (the pool states the orbs draw), icon names its glyph, color tints it (the status's own
 * color stands in), kind (buff, debuff or neutral) frames it (by default a buff when the status is beneficial,
 * else a debuff), and cue (pulse or none, by default none) says how it draws the eye. The desktop's StatusLook.
 */
export interface StatusLook {
  hidden: boolean;
  icon: string | null;
  color: string | null;
  kind: string;
  cue: string | null;
}

export const PLAIN_LOOK: StatusLook = { hidden: false, icon: null, color: null, kind: 'neutral', cue: null };

const isColorText = (v: YamlValue | undefined): v is string => typeof v === 'string' && /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(v);

export function statusLookOf(status: Obj): StatusLook {
  const meta = isObj(status.meta) ? status.meta : {};
  return {
    hidden: meta.hidden === true,
    icon: typeof meta.icon === 'string' && meta.icon.length > 0 ? meta.icon : null,
    color: isColorText(meta.color) ? meta.color : isColorText(status.color) ? status.color : null,
    kind: typeof meta.kind === 'string' && meta.kind.length > 0 ? meta.kind : status.beneficial === true ? 'buff' : 'debuff',
    cue: typeof meta.cue === 'string' ? meta.cue : null,
  };
}

export const lookOf = (names: ContentNames, status: string): StatusLook => names.statusLooks.get(status) ?? PLAIN_LOOK;

export interface ContentNames {
  statuses: Map<string, NamedEntry>;
  statusLooks: Map<string, StatusLook>;
  threads: NamedEntry[];
  currencies: Map<string, NamedEntry>;
  slots: NamedEntry[];
  resources: Map<string, NamedEntry>;
  attributes: AttributeInfo[];
  attributeById: Map<string, AttributeInfo>;
  passives: PassiveNode[];
  threadOfStatus: Map<string, string>;
  choices: Map<string, ChoiceGroup>;
  realms: Map<string, string>;
}

export const nameOf = (map: Map<string, NamedEntry>, id: string): string => map.get(id)?.name ?? humanize(id);

const roundTo = (v: number): string => String(Math.round(v * 100) / 100);
const signed = (v: number): string => (v >= 0 ? '+' : '') + roundTo(v);

/** A content modifier as a line a player reads: "+10% increased Armor (Ember)". Formatting only. */
export function modifierLine(m: Obj, attributes: Map<string, AttributeInfo>, threads: NamedEntry[] = []): string {
  const attribute = str(m.attribute);
  if (attribute.length === 0) {
    return '';
  }
  const form = str(m.form, 'flat');
  const info = attributes.get(attribute);
  const name = info?.name ?? humanize(attribute);
  const isNumber = typeof m.value === 'number';
  const value = num(m.value);
  let text: string;
  if (!isNumber) {
    text = `${name} ${form} ${str(m.value)}`;
  } else if (form === 'flat') {
    text = info?.percent ? `${signed(value * 100)}% ${name}` : `${signed(value)} ${name}`;
  } else if (form === 'increased') {
    text = value >= 0 ? `${roundTo(value * 100)}% increased ${name}` : `${roundTo(-value * 100)}% reduced ${name}`;
  } else if (form === 'more') {
    text = value >= 0 ? `${roundTo(value * 100)}% more ${name}` : `${roundTo(-value * 100)}% less ${name}`;
  } else if (form === 'override') {
    text = `${name} is ${info?.percent ? roundTo(value * 100) + '%' : roundTo(value)}`;
  } else {
    text = `${name} ${form} ${roundTo(value)}`;
  }
  const scope = strings(m.scope);
  if (scope.length > 0) {
    text += ` (${scope.map(id => threads.find(t => t.id === id)?.name ?? humanize(id)).join(', ')})`;
  }
  const condition = str(m.condition);
  if (condition.length > 0) {
    const has = /^has\("(.*)"\)$/.exec(condition);
    text += ` when ${has ? has[1].replace(/_/g, ' ') : condition}`;
  }
  return text;
}

export function loadNames(kinds: Map<string, Obj[]>): ContentNames {
  const named = (o: Obj, i: number): NamedEntry => ({
    id: str(o.id),
    name: str(o.name, humanize(str(o.id))),
    description: str(o.description),
    order: o.order === undefined ? i : num(o.order),
  });
  const attributes: AttributeInfo[] = (kinds.get('attributes') ?? []).map((a, index) => ({
    id: str(a.id),
    name: str(a.name, humanize(str(a.id))),
    category: str(a.category, 'misc'),
    description: str(a.description),
    percent: a.percent === true,
    decimals: a.decimals === undefined || a.decimals === null ? null : num(a.decimals),
    hidden: a.hidden === true,
    index,
  }));
  const attributeById = new Map(attributes.map((a) => [a.id, a]));
  const threads = (kinds.get('threads') ?? []).map(named);
  const passives: PassiveNode[] = (kinds.get('passives') ?? []).map((p) => {
    const choice = isObj(p.choice) ? str(p.choice.group) : str(p.choice);
    return {
      id: str(p.id),
      name: str(p.name, humanize(str(p.id))),
      kind: str(p.kind, 'small'),
      x: num(p.x),
      y: num(p.y),
      links: strings(p.links),
      realm: str(p.realm, 'star'),
      cost: num(p.cost, 1),
      calling: typeof p.calling === 'string' ? p.calling : null,
      lines: objects(p.modifiers).map((m) => modifierLine(m, attributeById, threads)).filter((l) => l.length > 0),
      choice: choice.length > 0 ? choice : null,
    };
  });
  const threadOfStatus = new Map<string, string>();
  for (const t of kinds.get('threads') ?? []) {
    const status = str(t.status);
    if (status.length > 0 && !threadOfStatus.has(status)) {
      threadOfStatus.set(status, str(t.id));
    }
  }
  const choices = new Map<string, ChoiceGroup>();
  for (const g of kinds.get('passive_choices') ?? []) {
    const id = str(g.id);
    const options: ChoiceOption[] = objects(g.options).map((o) => ({
      id: str(o.id),
      name: str(o.name, humanize(str(o.id))),
      lines: objects(o.modifiers).map((m) => modifierLine(m, attributeById, threads)).filter((l) => l.length > 0),
    }));
    // A generated group deals one option per visible attribute of a category, as PassiveChoices.OptionsOf does.
    if (isObj(g.generate)) {
      const gen = g.generate;
      const category = str(gen.category);
      for (const a of attributes.filter((x) => x.category === category && !x.hidden).sort((x, y) => (x.id < y.id ? -1 : x.id > y.id ? 1 : 0))) {
        if (options.some((o) => o.id === a.id)) {
          continue;
        }
        options.push({ id: a.id, name: a.name, lines: [modifierLine({ attribute: a.id, form: str(gen.form, 'flat'), value: num(gen.value) }, attributeById)] });
      }
    }
    choices.set(id, { id, name: str(g.name, humanize(id)), pick: num(g.pick, 1), options });
  }
  const realms = new Map<string, string>();
  for (const r of kinds.get('passive_realms') ?? []) {
    realms.set(str(r.id), str(r.name, humanize(str(r.id))));
  }
  return {
    statuses: new Map((kinds.get('statuses') ?? []).map((s, i) => [str(s.id), named(s, i)])),
    statusLooks: new Map((kinds.get('statuses') ?? []).map((s) => [str(s.id), statusLookOf(s)])),
    threads,
    currencies: new Map((kinds.get('currencies') ?? []).map((c, i) => [str(c.id), named(c, i)])),
    slots: (kinds.get('slots') ?? []).map(named).sort((a, b) => a.order - b.order),
    resources: new Map((kinds.get('resources') ?? []).map((r, i) => [str(r.id), named(r, i)])),
    attributes,
    attributeById,
    passives,
    threadOfStatus,
    choices,
    realms,
  };
}

let bundled: Map<string, Obj[]> | null = null;

/** The bundled content, read once. */
export function bundledKinds(): Map<string, Obj[]> {
  bundled ??= rawKinds(CONTENT_FILES);
  return bundled;
}
