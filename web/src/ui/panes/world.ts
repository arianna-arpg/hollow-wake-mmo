import type {
  ChooseDialogueMsg,
  DialogueChoiceInfo,
  DialogueStateMsg,
  Int64,
  ItemInfo,
  Message,
  QuestInfo,
  QuestOffersMsg,
  StationServiceInfo,
  StationServiceMsg,
  StationStateMsg,
  VendorStateMsg,
} from '../../net/protocol';
import { clear, el } from '../dom';
import { itemTile } from '../items';
import type { Carry, DropTarget, LeafContext, LeafView } from '../leaf';
import { nameOf } from '../logic/content';
import { css } from '../skin';
import { button, heading, line, priceText } from '../widgets';
import './panes.css';

const sameUid = (a: Int64, b: Int64): boolean => String(a) === String(b);
const given = <T>(v: T | null | undefined): v is T => v !== undefined && v !== null;

/** A rule between sections (the desktop's HSeparator). */
const rule = (): HTMLElement => el('div', { class: 'pane-rule', role: 'separator' });

/**
 * What every world pane shares: a column that scrolls inside the book's body (the desktop's ScrollContainer
 * around a VBoxContainer), redrawn only when a new state arrived, item tiles whose cards show while hovered,
 * and prices with the content's currency names. Every string the server sent goes in as text.
 */
abstract class WorldPane implements LeafView {
  readonly root: HTMLElement;
  protected readonly column: HTMLElement;
  protected dirty = false;
  private hovering = false;

  protected constructor(
    protected readonly ctx: LeafContext,
    className: string,
  ) {
    this.column = el('div', { class: 'pane-column' });
    this.root = el('div', { class: `world-pane ${className}` }, this.column);
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460);
  }

  refresh(): void {
    if (!this.dirty) {
      return;
    }
    this.dirty = false;
    this.unhover();
    clear(this.column);
    this.draw();
  }

  /** Draws the last state into the empty column. */
  protected abstract draw(): void;

  dropTargets(): DropTarget[] {
    return [];
  }

  onEscape(): boolean {
    return false;
  }

  dispose(): void {
    this.unhover();
  }

  /** Prices as a player reads them, with the content's currency names: "12 Gold, 3 Embers". */
  protected price(price: Readonly<Record<string, number>> | undefined): string {
    return priceText(price, (id) => nameOf(this.ctx.skin.names.currencies, id));
  }

  /** An item's tile in a list, its card shown while the pointer is over it (the desktop's ItemSwatch). */
  protected swatch(item: ItemInfo, size = 36): HTMLElement {
    const box = el('div', { class: 'pane-swatch' });
    box.style.width = `${size}px`;
    box.style.height = `${size}px`;
    box.append(itemTile(this.ctx.skin, item, (size - 4) * 0.68));
    box.addEventListener('pointermove', (e) => {
      // While an item is carried the card would only be in the way, as on the boards.
      const shown = this.ctx.items.carrying === null ? item : null;
      this.hovering = shown !== null;
      this.ctx.items.hoverItem(shown, e.clientX, e.clientY, 'pane');
    });
    box.addEventListener('pointerleave', () => this.unhover());
    return box;
  }

  /** A name in an item's rarity colour, taking the row's spare width. */
  protected itemName(text: string, item: ItemInfo, className = ''): HTMLElement {
    const name = el('div', { class: `line pane-grow${className ? ` ${className}` : ''}`, text });
    name.style.color = css(this.ctx.skin.rarity(item.rarity));
    return name;
  }

  private unhover(): void {
    if (this.hovering) {
      this.hovering = false;
      this.ctx.items.hoverItem(null, 0, 0, 'pane');
    }
  }
}

/**
 * A quest giver's offers and the quests ready to hand in: a button each, with the quest's level and its
 * objectives below. Answering sends accept_quest or turn_in_quest and lets the interface close the pane. The
 * desktop client's QuestOfferPane (UI/WorldPanes/WorldPanes.cs).
 */
export class QuestOfferPane extends WorldPane {
  private offers: QuestOffersMsg | null = null;
  private drawn = 0;

  constructor(
    ctx: LeafContext,
    private readonly onAnswered: () => void,
  ) {
    super(ctx, 'quest-pane');
  }

  override get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460) * 0.9;
  }

  get state(): QuestOffersMsg | null {
    return this.offers;
  }

  /** The offer and turn-in buttons drawn, for scripted checks. */
  get buttons(): number {
    return this.drawn;
  }

  show(msg: QuestOffersMsg): void {
    this.offers = msg;
    this.dirty = true;
  }

  protected override draw(): void {
    const offers = this.offers;
    if (offers === null) {
      return;
    }
    let buttons = 0;
    const entry = (q: QuestInfo, verb: string, kind: string, intent: Message): void => {
      const b = button(`${verb}: ${q.name}  (level ${q.level})`, () => {
        this.ctx.send(intent);
        this.onAnswered();
      }, { className: 'quest-button' });
      b.dataset.quest = q.id;
      b.dataset.kind = kind;
      const box = el('div', { class: 'quest-entry' }, b);
      for (const objective of q.objectives ?? []) {
        box.append(line(objective, 'muted pane-sub'));
      }
      this.column.append(box);
      buttons++;
    };
    for (const q of offers.offers ?? []) {
      entry(q, 'Accept', 'accept', { t: 'accept_quest', quest: q.id, giver: offers.giver });
    }
    for (const q of offers.turn_ins ?? []) {
      entry(q, 'Turn in', 'turn_in', { t: 'turn_in_quest', quest: q.id, giver: offers.giver });
    }
    if (buttons === 0) {
      this.column.append(line('Nothing for you right now.', 'muted'));
    }
    this.drawn = buttons;
  }
}

/**
 * A conversation: who speaks, the line, and the choices, numbered; a choice that cannot be taken is disabled
 * with why not as its tooltip. Choosing sends the line's node with the choice; the server runs the conversation
 * and answers with the next line or ends it, and a line can open a vendor or a station in the same book. The
 * desktop client's DialoguePane (UI/WorldPanes/WorldPanes.cs).
 */
export class DialoguePane extends WorldPane {
  private current: DialogueStateMsg | null = null;

  constructor(ctx: LeafContext) {
    super(ctx, 'dialogue-pane');
  }

  get state(): DialogueStateMsg | null {
    return this.current;
  }

  show(state: DialogueStateMsg): void {
    this.current = state;
    this.dirty = true;
  }

  protected override draw(): void {
    const s = this.current;
    if (s === null) {
      return;
    }
    this.column.append(el('div', { class: 'dialogue-speaker', text: s.speaker }), el('div', { class: 'dialogue-line', text: s.line }), rule());
    (s.choices ?? []).forEach((c, i) => {
      const b = button(`${i + 1}. ${c.text}`, () => this.ctx.send(chooseIntent(s, c)), {
        disabled: !c.enabled,
        title: c.enabled ? undefined : c.reason,
        className: 'dialogue-choice',
      });
      b.dataset.choice = c.id;
      this.column.append(b);
    });
  }
}

function chooseIntent(s: DialogueStateMsg, c: DialogueChoiceInfo): ChooseDialogueMsg {
  const intent: ChooseDialogueMsg = { t: 'choose_dialogue', conversation: s.conversation, choice: c.id };
  if (given(s.node)) {
    intent.node = s.node;
  }
  return intent;
}

/**
 * A vendor: the stock with the price of one unit per currency and the units left, what the hero sold and can
 * buy back, and what the vendor pays for each bag item (shown on bag items' cards while the vendor is open).
 * Buying, selling (drop a bag item on the pane) and buying back are one intent each; the bag updates through
 * the inventory state. The desktop client's VendorPane (UI/WorldPanes/WorldPanes.cs).
 */
export class VendorPane extends WorldPane implements DropTarget {
  private current: VendorStateMsg | null = null;
  private rows = 0;
  private dropHover = false;

  constructor(ctx: LeafContext) {
    super(ctx, 'vendor-pane');
  }

  get state(): VendorStateMsg | null {
    return this.current;
  }

  /** The stock rows drawn, for scripted checks. */
  get stockRows(): number {
    return this.rows;
  }

  show(state: VendorStateMsg): void {
    this.current = state;
    this.dirty = true;
  }

  /** What the vendor pays for one bag item, as the card's line ("Sells for 12 Gold"), or null. */
  quoteFor(uid: Int64): string | null {
    const quote = (this.current?.sell_prices ?? []).find((q) => sameUid(q.item, uid));
    return quote === undefined ? null : `Sells for ${this.price(quote.price)}`;
  }

  protected override draw(): void {
    const s = this.current;
    if (s === null) {
      return;
    }
    this.column.append(heading('For sale'));
    const stock = s.stock ?? [];
    for (const st of stock) {
      const enabled = st.enabled !== false;
      const buy = button('Buy', () => this.ctx.send({ t: 'buy', vendor: s.source, stock: st.id, count: 1 }), {
        disabled: !enabled,
        title: enabled ? undefined : st.reason,
        className: 'vendor-buy',
      });
      const name = st.item.name + (given(st.count) ? `  (${st.count} left)` : '');
      this.column.append(el('div', { class: 'pane-row vendor-stock', 'data-stock': st.id }, this.swatch(st.item), this.itemName(name, st.item), el('div', { class: 'pane-price num', text: this.price(st.price) }), buy));
    }
    if (stock.length === 0) {
      this.column.append(line('Nothing on the shelf.', 'muted'));
    }
    this.rows = stock.length;
    const back = s.buy_back ?? [];
    if (back.length > 0) {
      this.column.append(rule(), heading('Buy back'));
      for (const b of back) {
        const again = button('Buy back', () => this.ctx.send({ t: 'buy_back', vendor: s.source, item: b.item.uid }), { className: 'vendor-buyback-button' });
        this.column.append(el('div', { class: 'pane-row vendor-buyback', 'data-item': String(b.item.uid) }, this.swatch(b.item), this.itemName(b.item.name, b.item), el('div', { class: 'pane-price num', text: this.price(b.price) }), again));
      }
    }
    this.column.append(rule(), line('Drop an item from the bag here to sell it.', 'muted pane-small'));
  }

  override dropTargets(): DropTarget[] {
    return [this];
  }

  override dispose(): void {
    this.leave();
    super.dispose();
  }

  // ------------------------------------------------------------ drop target: selling

  covers(x: number, y: number): boolean {
    if (!this.root.isConnected || this.root.offsetParent === null) {
      return false;
    }
    const r = this.root.getBoundingClientRect();
    return x >= r.left && x < r.right && y >= r.top && y < r.bottom;
  }

  hover(_carry: Carry, _x: number, _y: number): void {
    if (!this.dropHover) {
      this.dropHover = true;
      this.root.classList.add('drop-hover');
    }
  }

  leave(): void {
    if (this.dropHover) {
      this.dropHover = false;
      this.root.classList.remove('drop-hover');
    }
  }

  drop(carry: Carry, _x: number, _y: number): boolean {
    this.leave();
    const s = this.current;
    if (s === null || carry.from.container !== 'bag') {
      return false;
    }
    this.ctx.send({ t: 'sell', vendor: s.source, item: carry.item.uid });
    return true;
  }
}

/**
 * A crafting station: the services it sells, each quoted for the item, weave slot or node it applies to with
 * an Ask button (and why not, when it cannot be done), then its recipes with what each needs against what the
 * hero has and a Craft button each. The desktop client's StationPane (UI/WorldPanes/WorldPanes.cs).
 */
export class StationPane extends WorldPane {
  private current: StationStateMsg | null = null;

  constructor(ctx: LeafContext) {
    super(ctx, 'station-pane');
  }

  get state(): StationStateMsg | null {
    return this.current;
  }

  show(state: StationStateMsg): void {
    this.current = state;
    this.dirty = true;
  }

  protected override draw(): void {
    const s = this.current;
    if (s === null) {
      return;
    }
    const recipes = s.recipes ?? [];
    const services = s.services ?? [];
    if (recipes.length === 0 && services.length === 0) {
      this.column.append(line('Nothing can be made here yet.', 'muted'));
    }
    if (services.length > 0) {
      this.column.append(heading('Services'));
      for (const sv of services) {
        this.column.append(this.service(s, sv));
      }
      if (recipes.length > 0) {
        this.column.append(heading('Recipes'));
      }
    }
    for (const r of recipes) {
      const craft = button('Craft', () => this.ctx.send({ t: 'craft_start', recipe: r.id, station: s.source }), {
        disabled: !r.craftable,
        title: r.craftable ? undefined : r.reason,
        className: 'station-craft',
      });
      const head = el('div', { class: 'pane-row' }, this.swatch(r.output), this.itemName(r.name, r.output, 'recipe-name'), craft);
      const block = el('div', { class: 'station-recipe', 'data-recipe': r.id }, head);
      for (const input of r.inputs ?? []) {
        block.append(line(`${input.text}  ${input.have}/${input.need}`, input.have >= input.need ? 'pane-sub' : 'bad pane-sub'));
      }
      if (given(r.cost) && Object.keys(r.cost).length > 0) {
        block.append(line(`Costs ${this.price(r.cost)}`, 'muted pane-sub'));
      }
      this.column.append(block);
    }
  }

  /** A service row: the item it applies to when the hero has it, the label with its price, and Ask. */
  private service(s: StationStateMsg, sv: StationServiceInfo): HTMLElement {
    const row = el('div', { class: 'pane-row' });
    if (given(sv.item)) {
      const item = this.ctx.hero.findItem(sv.item);
      if (item !== null) {
        row.append(this.swatch(item, 30));
      }
    }
    const price = given(sv.price) && Object.keys(sv.price).length > 0 ? this.price(sv.price) : '';
    const enabled = sv.enabled !== false;
    row.append(
      el('div', { class: `line pane-grow${enabled ? '' : ' muted'}`, text: price.length > 0 ? `${sv.label}  (${price})` : sv.label }),
      button('Ask', () => this.ctx.send(serviceIntent(s.source, sv)), { disabled: !enabled, title: sv.reason, className: 'station-ask' }),
    );
    const block = el('div', { class: 'station-service', 'data-service': sv.id }, row);
    if (!enabled && given(sv.reason) && sv.reason.length > 0) {
      block.append(line(sv.reason, 'bad pane-sub'));
    }
    return block;
  }
}

function serviceIntent(station: Int64, sv: StationServiceInfo): StationServiceMsg {
  const intent: StationServiceMsg = { t: 'station_service', station, service: sv.id };
  if (given(sv.item)) {
    intent.item = sv.item;
  }
  if (given(sv.slot)) {
    intent.slot = sv.slot;
  }
  if (given(sv.node)) {
    intent.node = sv.node;
  }
  return intent;
}
