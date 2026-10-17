import { describe, expect, it } from 'vitest';
import { bundledKinds, loadNames, type ChoiceGroup, type PassiveNode } from '../logic/content';
import { PanZoom } from '../logic/panZoom';
import { bounds, clickAction, graftBound, graftFrom, graftSeatLabel, linkPairs, named, nodeAt, nodeRadius, nodeTip, optionMatches, pointsText, reachable, realmName, realmsOpen, refundTip } from './constellation';

const node = (id: string, x: number, y: number, links: string[], over: Partial<PassiveNode> = {}): PassiveNode => ({
  id,
  name: id.toUpperCase(),
  kind: 'small',
  x,
  y,
  links,
  realm: 'star',
  cost: 1,
  calling: null,
  lines: [],
  choice: null,
  ...over,
});

const choices = new Map<string, ChoiceGroup>([
  ['oaths', { id: 'oaths', name: 'Oaths', pick: 1, options: [
    { id: 'oath_fire', name: 'Oath of Fire', lines: ['+10% increased Ember Damage'] },
    { id: 'oath_frost', name: 'Oath of Frost', lines: ['+10% increased Rime Damage'] },
  ] }],
]);

describe('the passive constellation', () => {
  it('opens the star when the state lists no realm, and names realms from content', () => {
    expect(realmsOpen(undefined)).toEqual(['star']);
    expect(realmsOpen([])).toEqual(['star']);
    expect(realmsOpen(['star', 'abyss'])).toEqual(['star', 'abyss']);
    const names = loadNames(bundledKinds());
    expect(realmName(names, 'devotion')).toBe('Devotion');
    expect(realmName(names, 'no_such_realm')).toBe('No Such Realm');
  });

  it('writes the points as the server counts them', () => {
    expect(pointsText({ passive: 3, ascendancy: 1 })).toBe('3 passive points   1 Ascendancy point');
    expect(pointsText({ passive: 1 })).toBe('1 passive point');
    expect(pointsText({})).toBe('');
  });

  it('sizes nodes by kind with the zoom held between 0.6 and 1.6', () => {
    expect(nodeRadius('small', 14, 1)).toBeCloseTo(7);
    expect(nodeRadius('keystone', 14, 1)).toBeCloseTo(11.9);
    expect(nodeRadius('notable', 14, 5)).toBeCloseTo(7 * 1.6 * 1.35);
    expect(nodeRadius('attribute', 14, 0.1)).toBeCloseTo(7 * 0.6 * 0.85);
    expect(named('notable', 0.8)).toBe(true);
    expect(named('notable', 0.7)).toBe(false);
    expect(named('small', 2)).toBe(false);
  });

  it('reaches the unallocated neighbours of allocated nodes, whichever side lists the link', () => {
    const nodes = [node('a', 0, 0, ['b']), node('b', 1, 0, ['c']), node('c', 2, 0, []), node('d', 0, 1, ['a'])];
    expect([...reachable(nodes, new Set(['a']))].sort()).toEqual(['b', 'd']);
    expect([...reachable(nodes, new Set(['a', 'b']))].sort()).toEqual(['c', 'd']);
    expect(reachable(nodes, new Set()).size).toBe(0);
  });

  it('draws each link once, whether both nodes list it or one does, and skips links out of the realm', () => {
    const nodes = [node('a', 0, 0, ['b', 'elsewhere']), node('b', 1, 0, ['a', 'c']), node('c', 2, 0, [])];
    const pairs = linkPairs(nodes).map(([x, y]) => `${x.id}-${y.id}`);
    expect(pairs).toEqual(['a-b', 'b-c']);
  });

  it('finds the nearest node within its radius and a little slack', () => {
    const nodes = [node('a', 0, 0, []), node('b', 1, 0, [], { kind: 'keystone' })];
    const view = new PanZoom(20, 0.4, 2.5);
    view.offsetX = 100;
    view.offsetY = 100;
    const radius = (n: PassiveNode) => nodeRadius(n.kind, 14, view.zoom);
    const at = (x: number, y: number) => nodeAt(nodes, (cx, cy) => view.toScreen(cx, cy), radius, x, y)?.id ?? null;
    expect(at(100, 100)).toBe('a');
    expect(at(110, 100)).toBe('a');
    expect(at(112, 100)).toBe('b');
    expect(at(100, 112)).toBeNull();
    expect(bounds(nodes)).toEqual({ minX: 0, minY: 0, maxX: 1, maxY: 0 });
    expect(bounds([])).toBeNull();
  });

  it('tells a node from content with its choice, the options picked and its cost', () => {
    const plain = node('a', 0, 0, [], { name: 'Kindling', kind: 'notable', lines: ['+10% increased Ember Damage'] });
    expect(nodeTip(plain, choices, undefined)).toBe('Kindling  (notable)\n+10% increased Ember Damage');
    const oath = node('oath', 0, 0, [], { name: 'Oathstone', choice: 'oaths', cost: 2 });
    expect(nodeTip(oath, choices, { oath: ['oath_frost', 'gone'] })).toBe('Oathstone  (small)\nChoose 1 of Oaths\nPicked: Oath of Frost, gone\nCosts 2 points');
    expect(nodeTip(oath, choices, { other: ['oath_fire'] })).toBe('Oathstone  (small)\nChoose 1 of Oaths\nCosts 2 points');
  });

  it('searches options by name and by line, ignoring case', () => {
    const [fire, frost] = choices.get('oaths')!.options;
    expect(optionMatches(fire, '')).toBe(true);
    expect(optionMatches(fire, '  FIRE ')).toBe(true);
    expect(optionMatches(frost, 'rime')).toBe(true);
    expect(optionMatches(frost, 'ember')).toBe(false);
  });

  it('refunds in refund mode, opens a choice node, allocates the rest and leaves allocated nodes', () => {
    const plain = node('a', 0, 0, []);
    const oath = node('oath', 0, 0, [], { choice: 'oaths' });
    const unknownGroup = node('odd', 0, 0, [], { choice: 'missing' });
    expect(clickAction(plain, true, true, choices)).toEqual({ kind: 'refund' });
    expect(clickAction(plain, false, true, choices)).toEqual({ kind: 'none' });
    expect(clickAction(plain, true, false, choices)).toEqual({ kind: 'none' });
    expect(clickAction(plain, false, false, choices)).toEqual({ kind: 'allocate' });
    expect(clickAction(oath, false, false, choices)).toMatchObject({ kind: 'choose', group: { id: 'oaths' } });
    expect(clickAction(unknownGroup, false, false, choices)).toEqual({ kind: 'allocate' });
    expect(refundTip('12 Gold')).toBe('Refund single nodes here for 12 Gold');
    expect(refundTip(undefined)).toBe('Refund single nodes here');
  });

  it('names a graft by the node that grants it and lists the seats it can bind to', () => {
    const names = loadNames(bundledKinds());
    const [first] = names.passives;
    expect(graftFrom(names, `${first.id}:0`)).toBe(first.name);
    expect(graftFrom(names, 'gone_node:1')).toBe('Gone Node');
    expect(graftSeatLabel(0, { key: null, name: '' })).toBe('Seat 1: empty');
    expect(graftSeatLabel(3, { key: 'ember_bolt', name: 'Ember Bolt' })).toBe('Seat 4: Ember Bolt');
    expect(graftBound(2, 8)).toBe(2);
    expect(graftBound(8, 8)).toBe(-1);
    expect(graftBound(-1, 8)).toBe(-1);
    expect(graftBound(undefined, 8)).toBe(-1);
  });

  it('reads the bundled constellation: every realm has nodes and links resolve within it', () => {
    const names = loadNames(bundledKinds());
    const realms = new Set(names.passives.map((p) => p.realm));
    expect(realms.has('star')).toBe(true);
    for (const realm of realms) {
      const nodes = names.passives.filter((p) => p.realm === realm);
      expect(bounds(nodes), realm).not.toBeNull();
      expect(linkPairs(nodes).length, realm).toBeGreaterThan(0);
    }
  });
});
