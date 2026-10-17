import type { BoardInfo, Int64, ItemInfo } from '../net/protocol';
import { el } from './dom';
import { itemTile } from './items';
import type { Carry, DropTarget, ItemHost } from './leaf';
import { Board, type DropTarget as Fit } from './logic/board';

const sameUid = (a: Int64, b: Int64): boolean => String(a) === String(b);

/**
 * A board of cells with items on their positions and footprints, as the inventory state sends it (the bag, a
 * stash page, the reliquary). Masked cells are drawn sealed. While an item is carried over it, the cells it would
 * land on are tinted: free, a swap with the one item under it, or blocked. Dropping sends one move intent for this
 * board's container; the server answers with the new state. The desktop client's UI/Core/BoardView.cs.
 */
export class BoardView implements DropTarget {
  readonly root: HTMLElement;
  private readonly items: HTMLElement;
  private readonly tint: HTMLElement;
  private info: BoardInfo | null = null;
  private board = new Board(0, 0, []);
  private cell = 0;
  private locked = false;
  private target: Fit | null = null;
  private hoverUid = '';

  constructor(
    private readonly host: ItemHost,
    public container: string,
  ) {
    this.items = el('div', { class: 'board-items' });
    this.tint = el('div', { class: 'board-tint', hidden: '' });
    this.root = el('div', { class: 'board', 'data-container': container }, this.items, this.tint);
    this.root.addEventListener('pointermove', (e) => this.pointerMove(e));
    this.root.addEventListener('pointerleave', () => {
      this.setHover('');
      this.host.hoverItem(null, 0, 0, this.container);
    });
    this.root.addEventListener('pointerdown', (e) => this.pointerDown(e));
    this.root.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  get shown(): BoardInfo | null {
    return this.info;
  }

  /** The size the board draws at with a given cell size, in interface pixels. */
  static sizeFor(info: BoardInfo | null, cell: number): { w: number; h: number } {
    return info === null ? { w: 0, h: 0 } : { w: info.width * cell, h: info.height * cell };
  }

  /** Draws a board at a cell size; a locked board takes no drops (out of reach, not yet bought, an overflow). */
  show(info: BoardInfo | null, cell: number, locked = false): void {
    this.root.dataset.container = this.container;
    this.root.classList.toggle('locked', locked);
    if (info === this.info && cell === this.cell && locked === this.locked) {
      return;
    }
    this.info = info;
    this.cell = cell;
    this.locked = locked;
    const items = info?.items ?? [];
    this.board = info === null
      ? new Board(0, 0, [])
      : new Board(info.width, info.height, items.map((i) => ({ uid: i.uid, x: i.x ?? 0, y: i.y ?? 0, w: Math.max(1, i.w ?? 1), h: Math.max(1, i.h ?? 1) })), Board.maskFromRows(info.mask, info.width, info.height));
    const size = BoardView.sizeFor(info, cell);
    this.root.style.width = `${size.w}px`;
    this.root.style.height = `${size.h}px`;
    this.root.style.setProperty('--cell', `${cell}px`);
    this.root.hidden = info === null;
    this.items.replaceChildren();
    if (info === null) {
      return;
    }
    for (let y = 0; y < info.height; y++) {
      for (let x = 0; x < info.width; x++) {
        if (this.board.isBlocked(x, y)) {
          const sealed = el('div', { class: 'board-sealed' });
          sealed.style.left = `${x * cell}px`;
          sealed.style.top = `${y * cell}px`;
          sealed.style.width = `${cell}px`;
          sealed.style.height = `${cell}px`;
          this.items.append(sealed);
        }
      }
    }
    for (const item of items) {
      const w = Math.max(1, item.w ?? 1);
      const h = Math.max(1, item.h ?? 1);
      const tile = itemTile(this.host.skin, item, Math.min(w, h) * cell * 0.68);
      tile.style.left = `${(item.x ?? 0) * cell}px`;
      tile.style.top = `${(item.y ?? 0) * cell}px`;
      tile.style.width = `${w * cell}px`;
      tile.style.height = `${h * cell}px`;
      tile.classList.toggle('hover', String(item.uid) === this.hoverUid);
      this.items.append(tile);
    }
    this.target = null;
    this.tint.hidden = true;
  }

  /** The cell under a client point, fractional. */
  private cellAt(clientX: number, clientY: number): [number, number] {
    const r = this.root.getBoundingClientRect();
    const info = this.info;
    if (info === null || r.width <= 0 || r.height <= 0) {
      return [-1, -1];
    }
    return [((clientX - r.left) / r.width) * info.width, ((clientY - r.top) / r.height) * info.height];
  }

  private itemAt(clientX: number, clientY: number): ItemInfo | null {
    const info = this.info;
    if (info === null) {
      return null;
    }
    const [cx, cy] = this.cellAt(clientX, clientY);
    const hit = this.board.itemAt(Math.floor(cx), Math.floor(cy));
    return hit === null ? null : info.items.find((i) => sameUid(i.uid, hit.uid)) ?? null;
  }

  private setHover(uid: string): void {
    if (uid === this.hoverUid) {
      return;
    }
    this.hoverUid = uid;
    for (const tile of this.items.querySelectorAll<HTMLElement>('.item-tile')) {
      tile.classList.toggle('hover', tile.dataset.uid === uid);
    }
  }

  private pointerMove(e: PointerEvent): void {
    const item = this.host.carrying === null ? this.itemAt(e.clientX, e.clientY) : null;
    this.setHover(item === null ? '' : String(item.uid));
    this.host.hoverItem(item, e.clientX, e.clientY, this.container);
  }

  private pointerDown(e: PointerEvent): void {
    if (this.host.carrying !== null) {
      return;
    }
    const under = this.itemAt(e.clientX, e.clientY);
    if (under === null) {
      return;
    }
    if (e.button === 0) {
      e.preventDefault();
      e.stopPropagation();
      this.host.pressItem(under, { container: this.container, x: under.x ?? 0, y: under.y ?? 0 }, e.clientX, e.clientY);
    } else if (e.button === 2) {
      e.preventDefault();
      e.stopPropagation();
      this.host.quickActions(under, this.container, e.clientX, e.clientY);
    }
  }

  // ------------------------------------------------------------ drop target

  covers(x: number, y: number): boolean {
    if (this.info === null || !this.root.isConnected || this.root.offsetParent === null) {
      return false;
    }
    const r = this.root.getBoundingClientRect();
    return x >= r.left && x < r.right && y >= r.top && y < r.bottom;
  }

  hover(carry: Carry, x: number, y: number): void {
    const [cx, cy] = this.cellAt(x, y);
    let target = this.board.target(carry.item.uid, carry.w, carry.h, cx, cy);
    if (this.locked) {
      target = { ...target, fit: 'blocked' };
    }
    const t = this.target;
    if (t !== null && t.x === target.x && t.y === target.y && t.fit === target.fit) {
      return;
    }
    this.target = target;
    if (target.fit === 'outside') {
      this.tint.hidden = true;
      return;
    }
    this.tint.hidden = false;
    this.tint.dataset.fit = target.fit;
    this.tint.style.left = `${target.x * this.cell}px`;
    this.tint.style.top = `${target.y * this.cell}px`;
    this.tint.style.width = `${carry.w * this.cell}px`;
    this.tint.style.height = `${carry.h * this.cell}px`;
  }

  leave(): void {
    this.target = null;
    this.tint.hidden = true;
  }

  drop(carry: Carry, x: number, y: number): boolean {
    this.hover(carry, x, y);
    const t = this.target;
    this.leave();
    if (t === null || t.fit === 'outside') {
      return false;
    }
    if (t.fit === 'blocked') {
      // Over the board but nowhere it can land: the drop is spent here, nothing is sent.
      return true;
    }
    if (carry.from.container === this.container && carry.from.x === t.x && carry.from.y === t.y && !carry.from.slot) {
      return true;
    }
    this.host.sendMove(carry.item.uid, this.container, t.x, t.y);
    return true;
  }
}
