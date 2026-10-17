import { describe, expect, it } from 'vitest';
import type { QuestInfo, SelfStateMsg } from '../../net/protocol';
import { bundledKinds, loadNames, loadUi } from '../logic/content';
import { buildSheet } from '../logic/sheet';
import { questLines, rowNote, sheetSignature } from './characterSheet';

const quest = (over: Partial<QuestInfo> = {}): QuestInfo => ({
  id: 'rats',
  name: 'Rats in the Well',
  state: 'active',
  objectives: ['Slay rats', 'Find the den'],
  counts: [3],
  required: [10, 1],
  level: 2,
  ...over,
});

const self = (attributes: Record<string, number>, quests: QuestInfo[]): SelfStateMsg => ({
  t: 'self_state',
  key: 1,
  level: 3,
  xp: 0,
  xp_to_next: 100,
  passive_points: 0,
  passives: [],
  attributes,
  resources: {},
  resource_max: {},
  weaves: [],
  bag: [],
  bag_capacity: 0,
  equipment: {},
  currency: {},
  quests,
  reputation: {},
  holdings: [],
});

describe('the character sheet', () => {
  it('writes each objective with its count and what it requires', () => {
    expect(questLines([quest()])).toEqual([{ name: 'Rats in the Well', state: 'active', objectives: ['Slay rats: 3/10', 'Find the den: 0/1'] }]);
  });

  it('changes its signature only when an attribute or a quest changes', () => {
    const a = sheetSignature(self({ might: 10 }, [quest()]));
    expect(sheetSignature(self({ might: 10 }, [quest()]))).toBe(a);
    expect(sheetSignature(self({ might: 11 }, [quest()]))).not.toBe(a);
    expect(sheetSignature(self({ might: 10 }, [quest({ counts: [4] })]))).not.toBe(a);
    expect(sheetSignature(self({ might: 10 }, [quest({ state: 'complete' })]))).not.toBe(a);
  });

  it('puts a quest objectives under it and a short description in place', () => {
    expect(rowNote('quests', { id: 'q', label: 'Q', value: 'active', description: 'Slay rats: 3/10' })).toEqual({ text: 'Slay rats: 3/10', kind: 'objectives' });
    expect(rowNote('attributes', { id: 'might', label: 'Might', value: '10', description: 'Raises life.' })).toEqual({ text: '  Raises life.', kind: 'description' });
    expect(rowNote('attributes', { id: 'might', label: 'Might', value: '10', description: 'x'.repeat(81) })).toBeNull();
    expect(rowNote('quests', { id: 'q', label: 'Q', value: 'active', description: '' })).toBeNull();
  });

  it('lays out the bundled sheet with the quest log from the self state', () => {
    const kinds = bundledKinds();
    const ui = loadUi(kinds);
    const sections = buildSheet(ui.sheet, loadNames(kinds), { might: 12 }, questLines([quest()]));
    const log = sections.find((s) => s.kind === 'quests');
    expect(log?.rows[0]).toMatchObject({ label: 'Rats in the Well', value: 'active', description: 'Slay rats: 3/10\nFind the den: 0/1' });
    expect(sections.flatMap((s) => s.rows).some((r) => r.id === 'might')).toBe(true);
  });
});
