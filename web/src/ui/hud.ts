import type { UiSettings } from '../config';
import type { Int64, QuestInfo, QuestOffersMsg, SelfStateMsg, WeaveSlotInfo } from '../net/protocol';
import { clear, el } from './dom';

export interface HudHandlers {
  slot: (slot: number) => void;
  chat: (text: string) => void;
  accept: (quest: string, giver: Int64) => void;
  turnIn: (quest: string, giver: Int64) => void;
}

interface SlotView {
  root: HTMLButtonElement;
  name: HTMLElement;
  cost: HTMLElement;
  cooldown: HTMLElement;
  shade: HTMLElement;
  weave: WeaveSlotInfo | null;
  readyAt: number;
  total: number;
}

const SLOTS = 8;

/**
 * In-world UI: resource bars, a weave bar with cooldowns and costs, a target line, notices, a chat log,
 * a character sheet and a quest offer panel. Placeholder like the desktop Hud; the data comes from
 * self_state and the quest messages, and every server string goes in as text.
 */
export class Hud {
  readonly root: HTMLElement;
  private readonly title: HTMLElement;
  private readonly bars: HTMLElement;
  private readonly barViews = new Map<string, { fill: HTMLElement; text: HTMLElement }>();
  private readonly target: HTMLElement;
  private readonly notice: HTMLElement;
  private readonly net: HTMLElement;
  private readonly slots: SlotView[] = [];
  private readonly log: HTMLElement;
  private readonly chatInput: HTMLInputElement;
  private readonly sheet: HTMLElement;
  private readonly sheetBody: HTMLElement;
  private readonly quests: HTMLElement;
  private readonly questList: HTMLElement;
  private noticeUntil = 0;
  private now = 0;

  constructor(
    private readonly settings: UiSettings,
    private readonly handlers: HudHandlers,
  ) {
    this.title = el('div', { class: 'hud-title', id: 'hud-title', text: 'Hollow Wake MMO' });
    this.bars = el('div', { class: 'hud-bars', id: 'hud-bars' });
    this.target = el('div', { class: 'hud-target', id: 'hud-target' });
    const hint = el('div', { class: 'hud-hint', text: settings.hint });
    this.notice = el('div', { class: 'hud-notice', id: 'hud-notice', role: 'status' });
    this.net = el('div', { class: 'hud-net', id: 'hud-net' });

    const weaveBar = el('div', { class: 'hud-weaves', id: 'hud-weaves' });
    for (let i = 0; i < SLOTS; i++) {
      const name = el('span', { class: 'slot-name', text: '-' });
      const cost = el('span', { class: 'slot-cost' });
      const cooldown = el('span', { class: 'slot-cooldown' });
      const shade = el('span', { class: 'slot-shade' });
      const root = el('button', { class: 'slot', type: 'button', tabindex: '-1', 'data-slot': String(i) },
        shade, el('span', { class: 'slot-key', text: String(i + 1) }), name, cost, cooldown);
      root.disabled = true;
      root.addEventListener('click', () => this.handlers.slot(i));
      weaveBar.append(root);
      this.slots.push({ root, name, cost, cooldown, shade, weave: null, readyAt: 0, total: 0 });
    }

    this.log = el('div', { class: 'hud-log', id: 'hud-log', 'aria-live': 'polite' });
    this.chatInput = el('input', { class: 'hud-chat-input', id: 'hud-chat-input', type: 'text', placeholder: 'Enter to chat', maxlength: '400', autocomplete: 'off' });
    this.chatInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        const text = this.chatInput.value.trim();
        if (text.length > 0) {
          this.handlers.chat(text);
        }
        this.chatInput.value = '';
        this.chatInput.blur();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        this.chatInput.value = '';
        this.chatInput.blur();
      }
      e.stopPropagation();
    });

    this.sheetBody = el('div', { class: 'sheet-body' });
    this.sheet = el('div', { class: 'hud-panel hud-sheet', id: 'hud-sheet', hidden: '' }, this.sheetBody);
    this.questList = el('div', { class: 'quest-list' });
    this.quests = el('div', { class: 'hud-panel hud-quests', id: 'hud-quests', hidden: '' }, this.questList);

    this.root = el('div', { class: 'hud', id: 'hud', hidden: '' },
      el('div', { class: 'hud-left' }, this.title, this.bars, this.target, hint),
      this.notice,
      this.net,
      weaveBar,
      el('div', { class: 'hud-chat' }, this.log, this.chatInput),
      this.sheet,
      this.quests);
  }

  show(visible: boolean): void {
    this.root.hidden = !visible;
  }

  get chatFocused(): boolean {
    return document.activeElement === this.chatInput;
  }

  get sheetVisible(): boolean {
    return !this.sheet.hidden;
  }

  get questsVisible(): boolean {
    return !this.quests.hidden;
  }

  focusChat(): void {
    this.chatInput.focus();
  }

  toggleSheet(): void {
    this.sheet.hidden = !this.sheet.hidden;
  }

  hideQuests(): void {
    this.quests.hidden = true;
  }

  setTarget(text: string): void {
    if (this.target.textContent !== text) {
      this.target.textContent = text;
    }
  }

  get targetText(): string {
    return this.target.textContent ?? '';
  }

  setNet(text: string): void {
    if (this.net.textContent !== text) {
      this.net.textContent = text;
    }
  }

  notify(text: string, level: string): void {
    this.notice.textContent = text;
    this.notice.dataset.level = level;
    this.noticeUntil = this.now + this.settings.noticeSeconds;
    this.line(text, `notice ${level}`);
  }

  /** One line in the log. Kinds style it: chat, combat, system, notice. */
  line(text: string, kind: string, from?: string): void {
    const row = el('div', { class: `log-line ${kind}` });
    if (from !== undefined) {
      row.append(el('span', { class: 'log-from', text: from }), ' ');
    }
    row.append(text);
    const pinned = this.log.scrollTop + this.log.clientHeight >= this.log.scrollHeight - 4;
    this.log.append(row);
    while (this.log.childElementCount > this.settings.chatLines) {
      this.log.firstElementChild?.remove();
    }
    if (pinned) {
      this.log.scrollTop = this.log.scrollHeight;
    }
  }

  chat(from: string, channel: string, text: string): void {
    this.line(`[${channel}]: ${text}`, 'chat', from);
  }

  frame(nowSeconds: number): void {
    this.now = nowSeconds;
    if (this.notice.textContent && nowSeconds > this.noticeUntil) {
      this.notice.textContent = '';
    }
    for (const slot of this.slots) {
      if (slot.weave === null) {
        continue;
      }
      const left = Math.max(0, slot.readyAt - nowSeconds);
      const text = left > 0.05 ? `${left.toFixed(1)}s` : '';
      if (slot.cooldown.textContent !== text) {
        slot.cooldown.textContent = text;
      }
      const frac = slot.total > 0 ? Math.min(1, left / slot.total) : 0;
      slot.shade.style.height = `${(frac * 100).toFixed(1)}%`;
    }
  }

  applySelf(self: SelfStateMsg, zoneName: string): void {
    this.title.textContent = `Level ${self.level}  xp ${Math.round(self.xp)}/${Math.round(self.xp_to_next)}  points ${self.passive_points}   ${zoneName}`;
    for (const [resource, value] of Object.entries(self.resources)) {
      let bar = this.barViews.get(resource);
      if (bar === undefined) {
        const fill = el('div', { class: 'bar-fill' });
        fill.style.background = this.settings.resourceColors[resource] ?? this.settings.resourceFallback;
        const text = el('span', { class: 'bar-text' });
        this.bars.append(el('div', { class: 'bar-row', 'data-resource': resource },
          el('span', { class: 'bar-label', text: resource }),
          el('div', { class: 'bar' }, fill, text)));
        bar = { fill, text };
        this.barViews.set(resource, bar);
      }
      const max = Math.max(1, self.resource_max[resource] ?? 1);
      bar.fill.style.width = `${Math.min(100, Math.max(0, (value / max) * 100)).toFixed(1)}%`;
      bar.text.textContent = `${Math.round(value)}/${Math.round(max)}`;
    }
    for (let i = 0; i < SLOTS; i++) {
      const view = this.slots[i];
      const weave = self.weaves[i] ?? null;
      view.weave = weave;
      view.root.disabled = weave === null;
      if (weave === null) {
        view.name.textContent = '-';
        view.cost.textContent = '';
        view.cooldown.textContent = '';
        view.shade.style.height = '0';
        view.root.title = '';
        continue;
      }
      view.name.textContent = weave.name;
      view.cost.textContent = Object.entries(weave.costs).map(([k, v]) => `${Math.round(v)}${k[0]}`).join(' ');
      view.root.title = `${weave.name}: ${weave.targeting}, range ${weave.range.toFixed(1)}`;
      const readyAt = this.now + weave.ready_in;
      if (weave.ready_in > 0.05 && (view.readyAt <= this.now || weave.ready_in > view.total)) {
        view.total = weave.ready_in;
      }
      view.readyAt = readyAt;
    }
    this.renderSheet(self);
  }

  private renderSheet(self: SelfStateMsg): void {
    const rarity = (r: string) => this.settings.rarityColors[r] ?? this.settings.rarityFallback;
    const section = (title: string) => el('h3', { text: title });
    const row = (label: string, value: string, color?: string) => {
      const line = el('div', { class: 'sheet-row' }, el('span', { text: label }), el('span', { text: value }));
      if (color) {
        line.style.color = color;
      }
      return line;
    };
    const body = el('div');
    body.append(el('h2', { text: `Character  level ${self.level}` }));
    const attributes = Object.entries(self.attributes).sort(([a], [b]) => a.localeCompare(b));
    for (const [k, v] of attributes) {
      if (k.includes('.max') || /^(resist|pen|convert|extra|weapon)\./.test(k)) {
        continue;
      }
      body.append(row(k, String(Math.round(v * 100) / 100)));
    }
    body.append(section('Resistances'));
    for (const [k, v] of attributes) {
      if (k.startsWith('resist.') && !k.endsWith('.max')) {
        body.append(row(k.slice(7), `${Math.round(v * 100)}%`));
      }
    }
    body.append(section('Equipment'));
    for (const [slot, item] of Object.entries(self.equipment)) {
      body.append(row(slot, item.name, rarity(item.rarity)));
    }
    body.append(section(`Bag ${self.bag.length}/${self.bag_capacity}`));
    for (const item of self.bag.slice(0, 40)) {
      body.append(row(item.name, item.count > 1 ? `x${item.count}` : '', rarity(item.rarity)));
    }
    body.append(section('Currency'));
    body.append(el('div', { class: 'sheet-row', text: Object.entries(self.currency).map(([k, v]) => `${k} ${Math.round(v)}`).join(', ') || 'none' }));
    body.append(section('Quests'));
    for (const q of self.quests) {
      body.append(el('div', { class: 'sheet-quest', text: `${q.name} [${q.state}]` }));
      q.objectives.forEach((o, i) => body.append(row(`  ${o}`, `${q.counts[i] ?? 0}/${q.required[i] ?? 0}`)));
    }
    clear(this.sheetBody);
    this.sheetBody.append(body);
  }

  showQuestOffers(msg: QuestOffersMsg): void {
    clear(this.questList);
    this.questList.append(el('h2', { text: 'Quests' }));
    const entry = (q: QuestInfo, verb: string, act: () => void) => {
      const button = el('button', { type: 'button', class: 'quest-button', text: `${verb}: ${q.name} [L${q.level}]` });
      button.addEventListener('click', () => {
        act();
        this.hideQuests();
      });
      const objectives = el('ul', { class: 'quest-objectives' });
      for (const o of q.objectives) {
        objectives.append(el('li', { text: o }));
      }
      this.questList.append(el('div', { class: 'quest' }, button, objectives));
    };
    for (const q of msg.offers) {
      entry(q, 'Accept', () => this.handlers.accept(q.id, msg.giver));
    }
    for (const q of msg.turn_ins) {
      entry(q, 'Turn in', () => this.handlers.turnIn(q.id, msg.giver));
    }
    if (msg.offers.length === 0 && msg.turn_ins.length === 0) {
      this.questList.append(el('p', { text: 'Nothing for you right now.' }));
    }
    const close = el('button', { type: 'button', class: 'quest-close', text: 'Close' });
    close.addEventListener('click', () => this.hideQuests());
    this.questList.append(close);
    this.quests.hidden = false;
  }
}
