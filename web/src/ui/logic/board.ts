import type { Int64 } from '../../net/protocol';

/** An item on a board: its uid, the cell of its top-left corner and its footprint in cells. */
export interface BoardItem {
  uid: Int64;
  x: number;
  y: number;
  w: number;
  h: number;
}

export type DropFit = 'fits' | 'swap' | 'blocked' | 'outside';

/** Where a carried footprint would land and how: the anchor cell, the fit and the item a swap would lift. */
export interface DropTarget {
  x: number;
  y: number;
  fit: DropFit;
  swap: Int64 | null;
}

const overlaps = (a: BoardItem, x: number, y: number, w: number, h: number): boolean =>
  x < a.x + a.w && a.x < x + w && y < a.y + a.h && a.y < y + h;

/**
 * A grid of cells with items laid on it by position and footprint, as the server sends it. The interface uses
 * it to find the cell under the pointer and to tint a drop target while an item is carried; the server decides
 * every move. The desktop client's UI/Logic/Board.cs, line for line.
 */
export class Board {
  private readonly blocked: boolean[];

  constructor(
    readonly width: number,
    readonly height: number,
    readonly items: readonly BoardItem[],
    mask?: readonly boolean[],
  ) {
    this.width = Math.max(0, width);
    this.height = Math.max(0, height);
    this.blocked = new Array<boolean>(this.width * this.height).fill(false);
    if (mask !== undefined) {
      for (let i = 0; i < Math.min(mask.length, this.blocked.length); i++) {
        this.blocked[i] = mask[i];
      }
    }
  }

  /** A mask from rows of X (usable) and . (not), as the inventory state sends it. */
  static maskFromRows(rows: readonly string[] | undefined, width: number, height: number): boolean[] | undefined {
    if (rows === undefined || rows.length === 0) {
      return undefined;
    }
    const mask = new Array<boolean>(width * height).fill(false);
    for (let y = 0; y < Math.min(rows.length, height); y++) {
      for (let x = 0; x < Math.min(rows[y].length, width); x++) {
        mask[y * width + x] = rows[y][x] !== 'X';
      }
    }
    return mask;
  }

  inBounds(x: number, y: number, w = 1, h = 1): boolean {
    return x >= 0 && y >= 0 && x + w <= this.width && y + h <= this.height;
  }

  isBlocked(x: number, y: number): boolean {
    return !this.inBounds(x, y) || this.blocked[y * this.width + x];
  }

  itemAt(x: number, y: number): BoardItem | null {
    return this.items.find((i) => x >= i.x && x < i.x + i.w && y >= i.y && y < i.y + i.h) ?? null;
  }

  /** Where a carried footprint lands with the pointer at a fractional cell: centred, pulled inside, tested. */
  target(carried: Int64, w: number, h: number, cellX: number, cellY: number): DropTarget {
    if (cellX < 0 || cellY < 0 || cellX >= this.width || cellY >= this.height || w > this.width || h > this.height) {
      return { x: 0, y: 0, fit: 'outside', swap: null };
    }
    const x = Math.min(Math.max(Math.floor(cellX - w / 2 + 0.5), 0), this.width - w);
    const y = Math.min(Math.max(Math.floor(cellY - h / 2 + 0.5), 0), this.height - h);
    return this.test(carried, x, y, w, h);
  }

  /** The fit of a footprint anchored at a cell; the carried item never blocks its own move. */
  test(carried: Int64, x: number, y: number, w: number, h: number): DropTarget {
    if (!this.inBounds(x, y, w, h)) {
      return { x, y, fit: 'outside', swap: null };
    }
    for (let cy = y; cy < y + h; cy++) {
      for (let cx = x; cx < x + w; cx++) {
        if (this.blocked[cy * this.width + cx]) {
          return { x, y, fit: 'blocked', swap: null };
        }
      }
    }
    const under = this.items.filter((i) => String(i.uid) !== String(carried) && overlaps(i, x, y, w, h));
    if (under.length === 0) {
      return { x, y, fit: 'fits', swap: null };
    }
    return under.length === 1 ? { x, y, fit: 'swap', swap: under[0].uid } : { x, y, fit: 'blocked', swap: null };
  }

  /** The first anchor, row by row, where a footprint fits on free cells; for previews only. */
  firstFit(w: number, h: number, ignore: Int64 = 0): [number, number] | null {
    for (let y = 0; y + h <= this.height; y++) {
      for (let x = 0; x + w <= this.width; x++) {
        if (this.test(ignore, x, y, w, h).fit === 'fits') {
          return [x, y];
        }
      }
    }
    return null;
  }
}
