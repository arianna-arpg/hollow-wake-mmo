import { humanize, type ChoiceGroup, type ChoiceOption, type ContentNames, type PassiveNode } from '../logic/content';
import { ordinal } from './geometry';

/**
 * How the passive constellation reads content and the passive state: the realms open, the points line, each
 * node's size, which nodes are reachable, each link once, the node under a point, a node's tooltip, what a click
 * on it does and the search over a choice node's options. Formatting and hit tests only, as the desktop client's
 * UI/Pages/PassivesPage.cs draws them; the server validates every allocation and refund.
 */

/** The realms open to the hero, as the passive state lists them; the star alone when it lists none. */
export function realmsOpen(realms: readonly string[] | null | undefined): string[] {
  return realms === null || realms === undefined || realms.length === 0 ? ['star'] : [...realms];
}

/** A realm's name from content. */
export const realmName = (names: ContentNames, id: string): string => names.realms.get(id) ?? humanize(id);

/** The points line: each currency's points as the server counts them, "3 passive points   1 Ascendancy point". */
export function pointsText(points: Readonly<Record<string, number>>): string {
  return Object.entries(points)
    .map(([id, n]) => `${n} ${id === 'passive' ? 'passive' : humanize(id)} point${n === 1 ? '' : 's'}`)
    .join('   ');
}

/** A node's radius on screen: half the node metric at the zoom (held between 0.6 and 1.6), larger for grander kinds. */
export function nodeRadius(kind: string, nodeMetric: number, zoom: number): number {
  const b = nodeMetric * 0.5 * Math.min(1.6, Math.max(0.6, zoom));
  switch (kind) {
    case 'keystone':
      return b * 1.7;
    case 'notable':
      return b * 1.35;
    case 'start':
      return b * 1.5;
    case 'attribute':
      return b * 0.85;
    default:
      return b;
  }
}

/** Whether a node's kind is drawn with a second ring and its name. */
export const isGrand = (kind: string): boolean => kind === 'notable' || kind === 'start';

/** Whether a node's name is written under it at a zoom. */
export const named = (kind: string, zoom: number): boolean => (kind === 'notable' || kind === 'keystone' || kind === 'start') && zoom > 0.7;

/** The nodes linked to an allocated node that are not allocated themselves: where a point can go next. */
export function reachable(nodes: readonly PassiveNode[], allocated: ReadonlySet<string>): Set<string> {
  const out = new Set<string>();
  for (const n of nodes) {
    for (const link of n.links) {
      if (allocated.has(n.id) && !allocated.has(link)) {
        out.add(link);
      }
      if (allocated.has(link) && !allocated.has(n.id)) {
        out.add(n.id);
      }
    }
  }
  return out;
}

/** Every link between two nodes of the set, once: a link both nodes list is drawn from the lower id. */
export function linkPairs(nodes: readonly PassiveNode[]): Array<[PassiveNode, PassiveNode]> {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const pairs: Array<[PassiveNode, PassiveNode]> = [];
  for (const n of nodes) {
    for (const link of n.links) {
      const other = byId.get(link);
      if (other === undefined || (ordinal(n.id, link) > 0 && other.links.includes(n.id))) {
        continue;
      }
      pairs.push([n, other]);
    }
  }
  return pairs;
}

/** The bounds of a set of nodes in content units, or null for none. */
export function bounds(nodes: readonly PassiveNode[]): { minX: number; minY: number; maxX: number; maxY: number } | null {
  if (nodes.length === 0) {
    return null;
  }
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const n of nodes) {
    minX = Math.min(minX, n.x);
    minY = Math.min(minY, n.y);
    maxX = Math.max(maxX, n.x);
    maxY = Math.max(maxY, n.y);
  }
  return { minX, minY, maxX, maxY };
}

/** The node under a point on screen: the nearest whose disc, with a little slack, holds the point. */
export function nodeAt(
  nodes: readonly PassiveNode[],
  toScreen: (x: number, y: number) => [number, number],
  radius: (n: PassiveNode) => number,
  px: number,
  py: number,
  slack = 4,
): PassiveNode | null {
  let best: PassiveNode | null = null;
  let bestD = Infinity;
  for (const n of nodes) {
    const [x, y] = toScreen(n.x, n.y);
    const d = Math.hypot(x - px, y - py);
    if (d <= radius(n) + slack && d < bestD) {
      best = n;
      bestD = d;
    }
  }
  return best;
}

/** An option's name wherever content offers it, else its id. */
export function optionName(choices: ReadonlyMap<string, ChoiceGroup>, id: string): string {
  for (const group of choices.values()) {
    const option = group.options.find((o) => o.id === id);
    if (option !== undefined) {
      return option.name;
    }
  }
  return id;
}

/** A node's tooltip from the content the server validates against, with the options the hero picked on it. */
export function nodeTip(node: PassiveNode, choices: ReadonlyMap<string, ChoiceGroup>, picks: Readonly<Record<string, string[]>> | null | undefined): string {
  const lines = [`${node.name}  (${node.kind})`, ...node.lines];
  const group = node.choice === null ? undefined : choices.get(node.choice);
  if (group !== undefined) {
    lines.push(`Choose ${group.pick} of ${group.name}`);
  }
  const picked = picks !== null && picks !== undefined && Object.hasOwn(picks, node.id) ? picks[node.id] : [];
  if (picked.length > 0) {
    lines.push(`Picked: ${picked.map((p) => optionName(choices, p)).join(', ')}`);
  }
  if (node.cost !== 1) {
    lines.push(`Costs ${node.cost} points`);
  }
  return lines.join('\n');
}

/** Whether a choice option matches a search: its name or one of its lines holds the text, ignoring case. */
export function optionMatches(option: ChoiceOption, query: string): boolean {
  const q = query.trim().toLowerCase();
  return q.length === 0 || option.name.toLowerCase().includes(q) || option.lines.some((l) => l.toLowerCase().includes(q));
}

export type NodeClick = { kind: 'refund' } | { kind: 'choose'; group: ChoiceGroup } | { kind: 'allocate' } | { kind: 'none' };

/**
 * What a click on a node does: in refund mode an allocated node is refunded; otherwise an allocated node does
 * nothing, a choice node opens its options and any other node is allocated.
 */
export function clickAction(node: PassiveNode, allocated: boolean, refundMode: boolean, choices: ReadonlyMap<string, ChoiceGroup>): NodeClick {
  if (refundMode) {
    return allocated ? { kind: 'refund' } : { kind: 'none' };
  }
  if (allocated) {
    return { kind: 'none' };
  }
  const group = node.choice === null ? undefined : choices.get(node.choice);
  return group !== undefined ? { kind: 'choose', group } : { kind: 'allocate' };
}

/** The refund button's tooltip: where single nodes are refunded and at what cost, as the server words it. */
export const refundTip = (cost: string | null | undefined): string =>
  cost !== null && cost !== undefined ? `Refund single nodes here for ${cost}` : 'Refund single nodes here';

// ---------------------------------------------------------------- grafts

/** The node a graft comes from, by name: its source is the granting node's id, then a colon and more. */
export function graftFrom(names: ContentNames, source: string): string {
  const node = source.split(':')[0];
  return names.passives.find((p) => p.id === node)?.name ?? humanize(node);
}

/** A seat as a graft's binding picker lists it. */
export const graftSeatLabel = (index: number, seat: { key: string | null; name: string }): string =>
  `Seat ${index + 1}: ${seat.key === null ? 'empty' : seat.name}`;

/** The seat a graft is bound to among a number of seats, or -1 for unbound (or a seat out of range). */
export const graftBound = (slot: number | null | undefined, seats: number): number =>
  slot !== null && slot !== undefined && slot >= 0 && slot < seats ? slot : -1;
