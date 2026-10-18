import { describe, expect, it } from 'vitest';
import type { ItemInfo, WeaveSeatInfo } from '../../net/protocol';
import type { Seat } from '../logic/heroState';
import { dropHint, dropIntent, firstFreeSocket, fitHint, hintTint, seatActions, seatCosts, seatMeta, seatTip } from './rack';

const info = (over: Partial<WeaveSeatInfo> = {}): WeaveSeatInfo => ({
  slot: 2,
  key: 'ember_bolt',
  form: 'bolt_form',
  name: 'Ember Bolt',
  level: 3,
  sockets: 3,
  glyphs: [
    { socket: 0, id: 'swift', name: 'Swift', level: 2 },
    { socket: 2, id: 'chain', name: 'Chain', level: 1, dormant: 'needs a projectile' },
  ],
  ready_in: 0,
  cooldown: 0,
  preview: [],
  cast_mode: 'cast',
  ...over,
});

const seat = (over: Partial<Seat> = {}, seatInfo: WeaveSeatInfo | null = info()): Seat => ({
  slot: 2,
  key: 'ember_bolt',
  name: 'Ember Bolt',
  icon: null,
  threads: ['ember'],
  readyAt: 0,
  cooldown: 0,
  costs: { mana: 12.4, life: 0 },
  dormant: null,
  uses: null,
  usesMax: null,
  converted: null,
  meta: null,
  info: seatInfo,
  ...over,
});

const empty = (): Seat => seat({ key: null, name: '', threads: [], costs: {} }, null);

const item = (over: Partial<ItemInfo> = {}): ItemInfo => ({ uid: 41, base: 'stone', name: 'Swift Stone', rarity: 'magic', count: 1, level: 1, lines: [], local: {}, ...over });

describe('the rack', () => {
  it('names a seat, its dormancy and each glyph with its own reason in the tooltip', () => {
    expect(seatTip(empty())).toBe('An empty seat: drag a form item here to learn it.');
    expect(seatTip(seat({ dormant: 'Requires 20 Might' }))).toBe('Ember Bolt\nDormant: Requires 20 Might\nSwift 2\nChain 1  (dormant: needs a projectile)');
  });

  it('writes the line under the name from the seat state, or why the seat is dormant', () => {
    expect(seatMeta(seat())).toBe('Level 3  bolt form  cast');
    const full = info({ active: true, uses: 1, uses_max: 3, stacks: 4, meta: { label: 'Detonate' } });
    expect(seatMeta(seat({}, full))).toBe('Level 3  bolt form  cast  on  1/3 uses  4 stacks  second: Detonate');
    expect(seatMeta(seat({}, info({ uses_max: 1, uses: 1, stacks: 0, form: undefined, cast_mode: undefined })))).toBe('Level 3');
    expect(seatMeta(seat({ dormant: 'Requires 20 Might' }))).toBe('Dormant: Requires 20 Might');
    expect(seatMeta(seat({}, null))).toBe('');
  });

  it('reads a stone fit from its card, a form as learn, and tints them', () => {
    const stone = item({ glyph: { id: 'swift', name: 'Swift', level: 2 }, fits: [{ slot: 2, fit: 'misfit', reason: 'no projectile' }, { slot: 0, fit: 'fits' }] });
    expect(fitHint(stone, 2)).toBe('misfit');
    expect(fitHint(stone, 5)).toBe('unknown');
    expect(dropHint(stone, 0)).toBe('fits');
    expect(dropHint(item({ form: { id: 'bolt', name: 'Bolt', level: 1, sockets: 2 } }), 3)).toBe('learn');
    expect(dropHint(item(), 3)).toBe('');
    expect(hintTint('fits')).toBe('fit');
    expect(hintTint('learn')).toBe('fit');
    expect(hintTint('misfit')).toBe('swap');
    expect(hintTint('full')).toBe('blocked');
    expect(hintTint('unknown')).toBe('blocked');
    expect(hintTint('')).toBeNull();
  });

  it('sockets a stone dropped on the seat itself into the first free socket, else the first', () => {
    expect(firstFreeSocket(info())).toBe(1);
    expect(firstFreeSocket(info({ sockets: 1, glyphs: [{ socket: 0, id: 'a', name: 'A', level: 1 }] }))).toBe(0);
    expect(firstFreeSocket(null)).toBe(0);
  });

  it('sends learn for a form, socket for a stone and nothing for anything else', () => {
    const stone = item({ uid: '9007199254740993', glyph: { id: 'swift', name: 'Swift', level: 2 } });
    expect(dropIntent(stone, 2, -1, info())).toEqual({ t: 'socket_stone', stone: '9007199254740993', slot: 2, socket: 1 });
    expect(dropIntent(stone, 2, 2, info())).toEqual({ t: 'socket_stone', stone: '9007199254740993', slot: 2, socket: 2 });
    expect(dropIntent(item({ form: { id: 'bolt', name: 'Bolt', level: 1, sockets: 2 } }), 4, -1, null)).toEqual({ t: 'learn_form', item: 41, slot: 4 });
    expect(dropIntent(item(), 4, 0, info())).toBeNull();
  });

  it('offers to take a stone out of the socket clicked, else to unlearn from the glyph', () => {
    expect(seatActions(seat(), 2, 2, false)).toEqual([{ label: 'Take out Chain', intent: { t: 'unsocket_stone', slot: 2, socket: 2 } }]);
    expect(seatActions(seat(), 2, 1, true)).toEqual([{ label: 'Unlearn Ember Bolt', intent: { t: 'unlearn_form', slot: 2 } }]);
    expect(seatActions(seat(), 2, 1, false)).toEqual([]);
    expect(seatActions(empty(), 2, -1, true)).toEqual([]);
  });

  it('draws the costs above zero as the server sent them', () => {
    expect(seatCosts(seat())).toEqual([{ resource: 'mana', amount: 12 }]);
  });
});
