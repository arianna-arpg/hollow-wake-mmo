import { describe, expect, it } from 'vitest';
import type { ContainerInfo } from '../../net/protocol';
import { boardNotes, boardsOf, buyContainer, buyState, nextToBuy, overflowNote, overflowOf, rungText, stashCount, tabLabel, tabTip } from './boards';

const board = { width: 4, height: 2, items: [] };

const container = (id: string, over: Partial<ContainerInfo> = {}): ContainerInfo => ({ id, name: id, kind: 'stash', board, reachable: true, ...over });

const price = (p: Readonly<Record<string, number>>): string => Object.entries(p).map(([k, v]) => `${Math.round(v)} ${k}`).join(', ');

const pages = [
  container('personal_stash.3', { name: 'Personal Stash', of: 'personal_stash', page: 3, locked: true, price: { gold: 500 } }),
  container('personal_stash.1', { name: 'Personal Stash', of: 'personal_stash', page: 1 }),
  container('overflow.personal_stash.1', { kind: 'overflow', name: 'Overflow', of: 'personal_stash.1' }),
  container('personal_stash.2', { name: 'Personal Stash', of: 'personal_stash', page: 2, locked: true, price: { gold: 250 } }),
  container('reliquary', { kind: 'reliquary', name: 'Reliquary', active: true }),
];

describe('the container page', () => {
  it('tabs between the matching boards by stash and page, leaving out the overflow boards', () => {
    const stash = boardsOf(pages, (c) => c.kind === 'stash' || c.kind === 'overflow');
    expect(stash.map((c) => c.id)).toEqual(['personal_stash.1', 'personal_stash.2', 'personal_stash.3']);
    expect(stashCount(stash)).toBe(1);
    expect(boardsOf(pages, () => true).map((c) => c.id)).toEqual(['personal_stash.1', 'personal_stash.2', 'personal_stash.3', 'reliquary']);
  });

  it('labels a page by number within one stash and by its stash among several', () => {
    const [first] = boardsOf(pages, (c) => c.kind === 'stash');
    expect(tabLabel(first, 1)).toBe('Page 1');
    expect(tabLabel(first, 2)).toBe('Personal Stash 1');
    expect(tabLabel(pages[4], 2)).toBe('Reliquary');
    expect(tabTip(pages[0], price)).toBe('Not yet bought: 500 gold');
    expect(tabTip(pages[1], price)).toBe('');
  });

  it('buys pages in order: only the first page not yet bought of its stash has the button', () => {
    const stash = boardsOf(pages, (c) => c.kind === 'stash');
    const [one, two, three] = stash;
    expect(nextToBuy(stash, one)).toBeNull();
    expect(nextToBuy(stash, two)?.id).toBe('personal_stash.2');
    expect(nextToBuy(stash, three)?.id).toBe('personal_stash.2');
    expect(buyState(two, nextToBuy(stash, two), price)).toEqual({ visible: true, enabled: true, title: 'Costs 250 gold' });
    expect(buyState(three, nextToBuy(stash, three), price).visible).toBe(false);
    const away = { ...two, reachable: false, reason: 'Stand at a stash chest.' };
    expect(buyState(away, nextToBuy(stash, away), price)).toEqual({ visible: true, enabled: false, title: 'Stand at a stash chest.' });
    expect(buyContainer(two)).toBe('personal_stash');
    expect(buyContainer(pages[4])).toBe('reliquary');
  });

  it('notes active seats, why a board is out of reach and a page not yet bought', () => {
    const stash = boardsOf(pages, (c) => c.kind === 'stash');
    const [one, two, three] = stash;
    expect(boardNotes(one, null, price)).toBe('');
    expect(boardNotes(pages[4], null, price)).toBe('Items seated here are active.');
    expect(boardNotes({ ...one, reachable: false }, null, price)).toBe('Out of reach from here.');
    expect(boardNotes(two, nextToBuy(stash, two), price)).toBe('This page is not yet bought: 250 gold.');
    expect(boardNotes(three, nextToBuy(stash, three), price)).toBe('This page is not yet bought: 500 gold. The pages before it are bought first.');
    expect(boardNotes({ ...two, price: undefined, reachable: false, reason: 'Far away.' }, null, price)).toBe('Far away. This page is not yet bought.');
  });

  it('writes the rungs and finds a board overflow', () => {
    expect(rungText({ index: 0, unlocked: true, cells: ['0,0'] })).toBe('Rung 1: 1 cell');
    expect(rungText({ index: 1, unlocked: false, cells: ['0,1', '1,1'], requirement: 'Level 10' })).toBe('Rung 2: 2 cells, requires Level 10');
    expect(rungText({ index: 2, unlocked: false, cells: [] })).toBe('Rung 3: 0 cells, locked');
    const overflow = overflowOf(pages, 'personal_stash.1');
    expect(overflow?.id).toBe('overflow.personal_stash.1');
    expect(overflowOf(pages, 'personal_stash.2')).toBeNull();
    expect(overflowNote(overflow!)).toBe('Overflow: items without a place on the board.');
    expect(overflowNote({ ...overflow!, reason: 'Take these out first.' })).toBe('Overflow: Take these out first.');
  });
});
