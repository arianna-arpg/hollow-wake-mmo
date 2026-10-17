import type { CurrencyInfo, EquipSlotInfo, ItemInfo } from '../net/protocol';
import { BoardView } from './board';
import { clear, el } from './dom';
import { itemTile } from './items';
import type { Carry, DropTarget, ItemHost } from './leaf';
import { forSlot } from './logic/content';
import type { HeroState } from './logic/heroState';
import type { Box } from './logic/layout';
import { badgeText, type TrayRow } from './logic/menu';
import { buildSheet } from './logic/sheet';
import type { Skin } from './skin';
import { badge, makePane, type PaneParts } from './widgets';
import './core.css';

/**
 * The core pane: the inventory and the hub of the workspace. The equipment doll and the hero's level beside it,
 * the bag board below, the ribbon rail on the edge where pages dock with the wallet's chips. It drags by its
 * header and remembers its seat; its pages move with it. The desktop client's UI/Core/CorePane.cs.
 */
export class CorePane {
  readonly root: HTMLElement;
  readonly pane: PaneParts;
  readonly rail: RailView;
  readonly doll: DollView;
  readonly bag: BoardView;
  private readonly summary: HTMLElement;
  private summarySignature = '';

  constructor(
    private readonly skin: Skin,
    host: ItemHost,
    title: string,
    handlers: { close: () => void; ribbon: (pageId: string) => void; drag: (dx: number, dy: number) => void; dragEnd: () => void },
  ) {
    this.pane = makePane(skin, title, { closable: true, draggable: true, className: 'core-pane' });
    this.pane.close?.addEventListener('click', (e) => {
      e.stopPropagation();
      handlers.close();
    });
    this.rail = new RailView(skin, handlers.ribbon);
    this.doll = new DollView(host);
    this.summary = el('div', { class: 'core-summary' });
    this.bag = new BoardView(host, 'bag');
    this.bag.root.id = 'hud-bag';
    this.pane.body.append(el('div', { class: 'core-top' }, this.doll.root, this.summary), this.bag.root);
    this.root = el('div', { class: 'core', id: 'hud-core', hidden: '' }, this.rail.root, this.pane.root);
    // The header drags the pane while the movable layout is on.
    let dragging = false;
    let lastX = 0;
    let lastY = 0;
    this.pane.head.addEventListener('pointerdown', (e) => {
      if (e.button !== 0 || !this.pane.head.classList.contains('grip') || (e.target as HTMLElement).closest('.pane-close') !== null) {
        return;
      }
      dragging = true;
      lastX = e.clientX;
      lastY = e.clientY;
      this.pane.head.setPointerCapture(e.pointerId);
      e.preventDefault();
      e.stopPropagation();
    });
    this.pane.head.addEventListener('pointermove', (e) => {
      if (!dragging) {
        return;
      }
      handlers.drag(e.clientX - lastX, e.clientY - lastY);
      lastX = e.clientX;
      lastY = e.clientY;
    });
    const end = (e: PointerEvent) => {
      if (dragging) {
        dragging = false;
        this.pane.head.releasePointerCapture(e.pointerId);
        handlers.dragEnd();
      }
    };
    this.pane.head.addEventListener('pointerup', end);
    this.pane.head.addEventListener('pointercancel', end);
  }

  get railWidth(): number {
    return this.skin.metric('rail', 46);
  }

  set draggable(on: boolean) {
    this.pane.head.classList.toggle('grip', on);
  }

  /** The size the pane wants for this state: the rail, the doll and summary row, and the bag board. */
  desired(hero: HeroState, scale: number): { w: number; h: number } {
    const s = this.skin;
    const cell = s.metric('cell', 36) * scale;
    const dollCell = s.metric('doll_cell', 50) * scale;
    const board = BoardView.sizeFor(hero.bag(s.metric('bag_columns', 10)), cell);
    const gap = Math.max(4, dollCell * 0.12);
    const dollW = 3 * dollCell + 2 * gap;
    const dollH = 4 * dollCell + 3 * gap;
    const bevel = s.mat('bevel', 3);
    const sideInset = bevel + 9;
    const bodyW = Math.max(board.w + 2, dollW + 14 + 150);
    const header = s.metric('tab_height', 30) + bevel + 3;
    return { w: this.railWidth + bodyW + sideInset * 2 + 2, h: header + 6 + dollH + 10 + board.h + 2 + bevel + 8 + 6 };
  }

  /** Places the pane, its rail on the side where pages dock, and fills it from the hero's state. */
  show(box: Box, railLeft: boolean, hero: HeroState, slots: readonly EquipSlotInfo[], rows: readonly TrayRow[], wallet: readonly CurrencyInfo[], badges: boolean, chips: boolean, scale: number): void {
    this.root.hidden = false;
    this.root.style.left = `${box.x}px`;
    this.root.style.top = `${box.y}px`;
    this.root.style.width = `${box.w}px`;
    this.root.style.height = `${box.h}px`;
    this.root.classList.toggle('rail-left', railLeft);
    const rail = this.railWidth;
    this.rail.root.style.width = `${rail}px`;
    this.pane.root.style.left = railLeft ? `${rail - 2}px` : '0px';
    this.pane.root.style.top = '0px';
    this.pane.root.style.width = `${box.w - rail + 2}px`;
    this.pane.root.style.height = `${box.h}px`;
    this.rail.show(rows, wallet, badges, chips);
    const s = this.skin;
    this.doll.show(slots, hero.equipment, s, s.metric('doll_cell', 50) * scale);
    this.bag.show(hero.bag(s.metric('bag_columns', 10)), s.metric('cell', 36) * scale);
    this.showSummary(hero);
  }

  hide(): void {
    this.root.hidden = true;
  }

  /** The hero's level, experience and unspent points beside the doll, and the sheet's first section. */
  private showSummary(hero: HeroState): void {
    const self = hero.self;
    const points = hero.points('passive');
    const signature = self === null ? '' : `${self.level}|${Math.round(self.xp)}|${Math.round(self.xp_to_next)}|${points}|${hero.version}`;
    if (signature === this.summarySignature) {
      return;
    }
    this.summarySignature = signature;
    clear(this.summary);
    if (self === null) {
      return;
    }
    const frac = self.xp_to_next > 0 ? Math.min(1, Math.max(0, self.xp / self.xp_to_next)) : 0;
    const fill = el('div', { class: 'xp-fill' });
    fill.style.width = `${frac * 100}%`;
    this.summary.append(
      el('div', { class: 'core-level', text: `Level ${self.level}` }),
      el('div', { class: 'xp-bar well' }, fill),
      el('div', { class: 'core-xp muted num', text: `${Math.round(self.xp)} / ${Math.round(self.xp_to_next)} experience` }),
    );
    if (points > 0) {
      this.summary.append(el('div', { class: 'core-points accent', text: `${points} passive point${points === 1 ? '' : 's'} to spend` }));
    }
    // The sheet's first section (the core attributes) stands beside the doll.
    const first = this.skin.ui.sheet.find((section) => section.kind === 'attributes');
    if (first !== undefined) {
      const [section] = buildSheet([first], this.skin.names, self.attributes, []);
      for (const row of section.rows) {
        const line = el('div', { class: 'core-attr' }, el('span', { class: 'muted', text: row.label }), el('span', { class: 'num', text: row.value }));
        line.title = row.description;
        this.summary.append(line);
      }
    }
  }
}

/**
 * The ribbon rail along the core pane's edge: a ribbon per ribbon or popout page the hero has, each with its
 * glyph and key, lit while its page is shown and wearing its pips; the wallet's chips below. When the pane is too
 * short the rail drops its badges first and its chips next.
 */
export class RailView {
  readonly root: HTMLElement;
  private readonly ribbons: HTMLElement;
  private readonly chips: HTMLElement;
  private signature = '';

  static ribbonHeight(skin: Skin): number {
    return skin.metric('rail', 46) * 1.05;
  }

  static readonly badgeHeight = 12;
  static readonly chipHeight = 38;
  static readonly top = 40;

  constructor(
    private readonly skin: Skin,
    private readonly press: (pageId: string) => void,
  ) {
    this.ribbons = el('div', { class: 'rail-ribbons' });
    this.chips = el('div', { class: 'rail-chips' });
    this.root = el('div', { class: 'rail' }, this.ribbons, this.chips);
  }

  show(rows: readonly TrayRow[], wallet: readonly CurrencyInfo[], badges: boolean, chips: boolean): void {
    const shown = rows.filter((r) => (r.page.place === 'ribbon' || r.page.place === 'popout') && r.state !== 'hidden');
    const signature = `${shown.map((r) => `${r.page.id}:${r.state}:${r.showing}:${r.pips}:${r.keyLabel}:${r.reason ?? ''}`).join('|')}#${wallet.map((w) => `${w.id}:${Math.round(w.amount)}`).join(',')}#${badges}${chips}`;
    if (signature === this.signature) {
      return;
    }
    this.signature = signature;
    const s = this.skin;
    clear(this.ribbons);
    this.ribbons.style.paddingTop = `${RailView.top}px`;
    const h = RailView.ribbonHeight(s) + (badges ? RailView.badgeHeight : 0);
    for (const row of shown) {
      const open = row.state === 'open';
      const ribbon = el('button', { class: `ribbon${open ? '' : ' sealed'}${row.showing ? ' showing' : ''}`, type: 'button', tabindex: '-1', 'data-page': row.page.id });
      ribbon.style.height = `${h - 4}px`;
      const key = row.keyLabel.length > 0 ? ` (${row.keyLabel})` : '';
      ribbon.title = open ? `${row.page.name}${key}\n${row.page.description}` : `${row.page.name}${key}: ${row.reason ?? 'sealed here'}`;
      ribbon.append(s.icon(row.page.icon, Math.round(Math.min(s.metric('rail', 46), RailView.ribbonHeight(s)) * 0.54), 'ribbon-glyph'));
      if (row.keyLabel.length > 0) {
        ribbon.append(el('span', { class: 'ribbon-key num', text: row.keyLabel }));
      }
      if (badges && open && row.pips > 0) {
        const b = badge(badgeText(row.pips));
        b.classList.add('ribbon-badge');
        ribbon.append(b);
      }
      ribbon.addEventListener('pointerdown', (e) => e.stopPropagation());
      ribbon.addEventListener('click', (e) => {
        e.stopPropagation();
        if (open) {
          this.press(row.page.id);
        }
      });
      this.ribbons.append(ribbon);
    }
    clear(this.chips);
    this.chips.hidden = !chips;
    if (chips) {
      for (const w of wallet) {
        const amount = w.amount >= 10000 ? `${Math.round(w.amount / 100) / 10}k` : String(Math.round(w.amount));
        const chip = el('div', { class: 'chip', title: `${w.name}: ${Math.round(w.amount)}`, 'data-currency': w.id }, s.icon('coin', 14, 'chip-glyph'), el('span', { class: 'num', text: amount }));
        chip.style.height = `${RailView.chipHeight - 4}px`;
        this.chips.append(chip);
      }
    }
  }
}

/**
 * The equipment doll: every equipment slot from content, placed on the cells the layout gives it (slots it does
 * not place follow in slot order), each showing what is worn there or its own glyph faintly, and a slot filled by
 * another slot's item drawn as taken. Worn items lift like bag items; dropping an item on a slot sends the move
 * for that slot. The desktop client's UI/Core/DollView.cs.
 */
export class DollView implements DropTarget {
  readonly root: HTMLElement;
  private readonly cells = new Map<string, { slot: EquipSlotInfo; node: HTMLElement }>();
  private worn: Record<string, ItemInfo> = {};
  private signature = '';
  private dropSlot: string | null = null;

  constructor(private readonly host: ItemHost) {
    this.root = el('div', { class: 'doll', id: 'hud-doll' });
    this.root.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  show(slots: readonly EquipSlotInfo[], worn: Record<string, ItemInfo>, skin: Skin, cell: number): void {
    this.worn = worn;
    const carried = this.host.carrying === null ? '' : String(this.host.carrying.item.uid);
    const signature = `${cell}|${slots.map((s) => `${s.id}:${s.occupied_by ?? ''}`).join(',')}|${Object.entries(worn).map(([k, v]) => `${k}=${v.uid}:${v.count}`).join(',')}|${carried}`;
    if (signature === this.signature) {
      return;
    }
    this.signature = signature;
    clear(this.root);
    this.cells.clear();
    const layout = skin.ui.layout;
    const gap = Math.max(4, cell * 0.12);
    const placed = new Set<string>();
    const doll = layout.doll;
    const entries = Object.values(doll);
    const columns = Math.max(3, entries.length === 0 ? 3 : Math.max(...entries.map((c) => c[0])) + 1);
    const maxRow = entries.length === 0 ? -1 : Math.max(...entries.map((c) => c[1]));
    const ordered = [...slots].sort((a, b) => a.order - b.order);
    const cells: Array<{ slot: EquipSlotInfo; col: number; row: number }> = [];
    for (const slot of ordered) {
      const at = doll[slot.id];
      if (at !== undefined) {
        placed.add(`${at[0]},${at[1]}`);
        cells.push({ slot, col: at[0], row: at[1] });
      }
    }
    // Slots the layout does not place follow below, in slot order.
    let next = 0;
    for (const slot of ordered) {
      if (doll[slot.id] !== undefined) {
        continue;
      }
      let col = 0;
      let row = 0;
      do {
        col = next % columns;
        row = maxRow + 1 + Math.floor(next / columns);
        next++;
      } while (placed.has(`${col},${row}`));
      cells.push({ slot, col, row });
    }
    let w = 0;
    let h = 0;
    for (const { slot, col, row } of cells) {
      const node = el('div', { class: 'doll-slot', 'data-slot': slot.id });
      node.style.left = `${col * (cell + gap)}px`;
      node.style.top = `${row * (cell + gap)}px`;
      node.style.width = `${cell}px`;
      node.style.height = `${cell}px`;
      w = Math.max(w, col * (cell + gap) + cell);
      h = Math.max(h, row * (cell + gap) + cell);
      const item = worn[slot.id];
      if (item !== undefined && String(item.uid) !== carried) {
        const tile = itemTile(skin, item, (cell - 4) * 0.68);
        tile.style.inset = '2px';
        node.append(tile);
      } else {
        node.classList.add(slot.occupied_by ? 'taken' : 'empty');
        node.append(skin.icon(forSlot(skin.icons, slot.id), Math.round(cell * 0.56), 'doll-glyph'));
        node.title = slot.occupied_by ? `${slot.name}: held by ${slot.occupied_by}` : slot.name;
      }
      node.addEventListener('pointermove', (e) => {
        const on = this.worn[slot.id];
        this.host.hoverItem(this.host.carrying === null && on !== undefined ? on : null, e.clientX, e.clientY, 'equipment');
      });
      node.addEventListener('pointerleave', () => this.host.hoverItem(null, 0, 0, 'equipment'));
      node.addEventListener('pointerdown', (e) => {
        const on = this.worn[slot.id];
        if (this.host.carrying !== null || on === undefined) {
          return;
        }
        e.preventDefault();
        e.stopPropagation();
        if (e.button === 0) {
          this.host.pressItem(on, { container: 'equipment', x: 0, y: 0, slot: slot.id }, e.clientX, e.clientY);
        } else if (e.button === 2) {
          this.host.quickActions(on, 'equipment', e.clientX, e.clientY);
        }
      });
      this.root.append(node);
      this.cells.set(slot.id, { slot, node });
    }
    this.root.style.width = `${w}px`;
    this.root.style.height = `${h}px`;
  }

  private slotAt(x: number, y: number): string | null {
    for (const [id, { node }] of this.cells) {
      const r = node.getBoundingClientRect();
      if (x >= r.left && x < r.right && y >= r.top && y < r.bottom) {
        return id;
      }
    }
    return null;
  }

  covers(x: number, y: number): boolean {
    return this.root.offsetParent !== null && this.slotAt(x, y) !== null;
  }

  hover(carry: Carry, x: number, y: number): void {
    const slot = this.slotAt(x, y);
    // A slot takes the item when the item names it (or names no slot and is gear); the server decides.
    const cls = carry.item.class ?? '';
    const fits = slot !== null && (carry.item.slot ? carry.item.slot === slot : cls.startsWith('weapon') || cls.startsWith('armor'));
    if (slot !== this.dropSlot) {
      this.leave();
      this.dropSlot = slot;
    }
    if (slot !== null) {
      const node = this.cells.get(slot)?.node;
      node?.classList.toggle('drop-fit', fits);
      node?.classList.toggle('drop-swap', !fits);
    }
  }

  leave(): void {
    if (this.dropSlot !== null) {
      const node = this.cells.get(this.dropSlot)?.node;
      node?.classList.remove('drop-fit', 'drop-swap');
      this.dropSlot = null;
    }
  }

  drop(carry: Carry, x: number, y: number): boolean {
    const slot = this.slotAt(x, y);
    this.leave();
    if (slot === null) {
      return false;
    }
    if (carry.from.slot === slot) {
      return true;
    }
    this.host.sendMove(carry.item.uid, 'equipment', 0, 0, slot);
    return true;
  }
}
