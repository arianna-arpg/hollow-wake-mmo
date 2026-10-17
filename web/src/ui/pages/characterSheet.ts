import type { QuestInfo, SelfStateMsg } from '../../net/protocol';
import type { QuestLine, SheetRow } from '../logic/sheet';

/**
 * How the character sheet reads the self state: the quest log as sheet lines, a signature of what the sheet shows
 * so it rebuilds only when an attribute or a quest changed, and the note a row carries under it. Formatting only,
 * as the desktop client's UI/Pages/CharacterPage.cs writes it.
 */

/** The quest log as the sheet reads it: each objective with its count and what it requires. */
export function questLines(quests: readonly QuestInfo[]): QuestLine[] {
  return quests.map((q) => ({
    name: q.name,
    state: q.state,
    objectives: q.objectives.map((o, i) => `${o}: ${q.counts[i] ?? 0}/${q.required[i] ?? 0}`),
  }));
}

/** What the sheet shows, as one string: every attribute's value and every quest's state and counts. */
export function sheetSignature(self: SelfStateMsg): string {
  const parts: string[] = [];
  for (const [id, value] of Object.entries(self.attributes)) {
    parts.push(`${id}=${value}`);
  }
  for (const q of self.quests) {
    parts.push(`${q.id}|${q.state}|${q.counts.join(',')}`);
  }
  return parts.join(';');
}

export interface RowNote {
  text: string;
  kind: 'objectives' | 'description';
}

/** The note under a row: a quest's objectives, else a description short enough to read in place. */
export function rowNote(sectionKind: string, row: SheetRow): RowNote | null {
  if (row.description.length === 0) {
    return null;
  }
  if (sectionKind === 'quests') {
    return { text: row.description, kind: 'objectives' };
  }
  return row.description.length <= 80 ? { text: `  ${row.description}`, kind: 'description' } : null;
}
