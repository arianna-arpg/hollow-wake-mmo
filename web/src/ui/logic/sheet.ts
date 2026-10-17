import type { AttributeInfo, ContentNames, UiSheetSection } from './content';
import { humanize } from './content';

export interface SheetRow {
  id: string;
  label: string;
  value: string;
  description: string;
  thread?: string;
}

export interface SheetSection {
  id: string;
  name: string;
  kind: string;
  rows: SheetRow[];
}

export interface QuestLine {
  name: string;
  state: string;
  objectives: string[];
}

/** A value as the sheet reads it: percentages for percent attributes, the definition's decimals otherwise. */
export function formatValue(value: number, info: AttributeInfo | undefined): string {
  if (info?.percent) {
    return `${Math.round(value * 1000) / 10}%`;
  }
  const decimals = info?.decimals ?? 2;
  const factor = 10 ** Math.max(0, decimals);
  return String(Math.round(value * factor) / factor);
}

/**
 * Lays out the character sheet from the sheet content with the values the server sent, labelled and described
 * by the attribute definitions. Formatting only. The desktop client's UI/Logic/SheetModel.cs.
 */
export function buildSheet(sections: readonly UiSheetSection[], names: ContentNames, values: Readonly<Record<string, number>>, quests: readonly QuestLine[]): SheetSection[] {
  return sections.map((section) => {
    const rows: SheetRow[] = [];
    if (section.kind === 'attributes') {
      const listed = new Set<string>();
      const add = (id: string) => {
        if (listed.has(id) || values[id] === undefined) {
          return;
        }
        listed.add(id);
        const info = names.attributeById.get(id);
        rows.push({ id, label: info?.name ?? humanize(id), value: formatValue(values[id], info), description: info?.description ?? '' });
      };
      section.attributes.forEach(add);
      for (const info of names.attributes) {
        if (info.hidden || !section.categories.includes(info.category) || section.exclude.some((p) => info.id.startsWith(p))) {
          continue;
        }
        add(info.id);
      }
    } else if (section.kind === 'threads' && section.pattern !== undefined) {
      for (const thread of names.threads) {
        const id = section.pattern.split('{thread}').join(thread.id);
        if (values[id] === undefined) {
          continue;
        }
        let text = formatValue(values[id], names.attributeById.get(id));
        if (section.cap !== undefined) {
          const capId = section.cap.split('{thread}').join(thread.id);
          if (values[capId] !== undefined) {
            text += ` (max ${formatValue(values[capId], names.attributeById.get(capId))})`;
          }
        }
        rows.push({ id, label: thread.name, value: text, description: thread.description, thread: thread.id });
      }
    } else if (section.kind === 'quests') {
      for (const q of quests) {
        rows.push({ id: q.name, label: q.name, value: q.state, description: q.objectives.join('\n') });
      }
    }
    return { id: section.id, name: section.name, kind: section.kind, rows };
  });
}
