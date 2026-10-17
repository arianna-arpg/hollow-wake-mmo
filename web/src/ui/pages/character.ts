import '../widgets';
import './pages.css';
import type { SelfStateMsg } from '../../net/protocol';
import { el } from '../dom';
import type { DropTarget, LeafContext, LeafView } from '../leaf';
import { buildSheet } from '../logic/sheet';
import { css } from '../skin';
import { questLines, rowNote, sheetSignature } from './characterSheet';

/**
 * The character popout: the sheet by the sections content lists, each row labelled and described by its attribute
 * definition with the value the self state carries, resistances by Thread in the Thread's colour, and the quest
 * log. A row's description is its tooltip, and a short one reads under it. It rebuilds only when an attribute or a
 * quest changed. The desktop client's UI/Pages/CharacterPage.cs.
 */
export class CharacterPage implements LeafView {
  readonly root: HTMLElement;
  private seen: SelfStateMsg | null = null;
  private signature: string | null = null;
  private count = 0;

  constructor(private readonly ctx: LeafContext) {
    this.root = el('div', { class: 'leaf-page character-page' });
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460) * 0.8;
  }

  /** How many rows the sheet shows, for scripted checks. */
  get rows(): number {
    return this.count;
  }

  refresh(): void {
    const self = this.ctx.hero.self;
    if (self === null || self === this.seen) {
      return;
    }
    this.seen = self;
    const signature = sheetSignature(self);
    if (signature === this.signature) {
      return;
    }
    this.signature = signature;
    this.build(self);
  }

  dropTargets(): DropTarget[] {
    return [];
  }

  onEscape(): boolean {
    return false;
  }

  dispose(): void {
    // nothing held outside the page
  }

  private build(self: SelfStateMsg): void {
    const skin = this.ctx.skin;
    const sections = buildSheet(skin.ui.sheet, skin.names, self.attributes, questLines(self.quests));
    const parts: HTMLElement[] = [];
    let rows = 0;
    for (const section of sections) {
      const quests = section.kind === 'quests';
      if (section.rows.length === 0 && !quests) {
        continue;
      }
      const box = el('section', { class: 'char-section', 'data-section': section.id }, el('h3', { class: 'pane-heading char-heading', text: section.name }));
      if (section.rows.length === 0) {
        box.append(el('div', { class: 'line muted char-empty', text: 'Nothing yet.' }));
      }
      for (const row of section.rows) {
        const label = el('span', { class: 'char-label', text: row.label });
        if (!quests && row.thread !== undefined) {
          label.style.color = css(skin.thread(row.thread));
        }
        const line = el('div', { class: `char-row${quests ? ' quest' : ''}`, 'data-row': row.id }, label, el('span', { class: 'char-value num', text: row.value }));
        if (row.description.length > 0) {
          line.title = row.description;
        }
        box.append(line);
        const note = rowNote(section.kind, row);
        if (note !== null) {
          box.append(el('div', { class: note.kind === 'objectives' ? 'char-objectives' : 'char-desc', text: note.text }));
        }
        rows++;
      }
      parts.push(box);
    }
    this.root.replaceChildren(...parts);
    this.count = rows;
    this.root.dataset.rows = String(rows);
  }
}
