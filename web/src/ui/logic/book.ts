import type { Int64 } from '../../net/protocol';

/** Which side of the core pane a leaf belongs to: the hero's development pages or the panes the world opens. */
export type LeafGroup = 'page' | 'world';

/** How an arrival asks to land: by the laws, at the front (a key, a click, a menu pick) or quietly behind. */
export type Ask = 'auto' | 'front' | 'behind';

/** One pane bound into a book. */
export interface Leaf {
  id: string;
  group: LeafGroup;
  /** A page id for pages, the pane kind (dialogue, vendor, ...) for world panes. */
  kind: string;
  rank: number;
  title: string;
  /** The entity that opened a world pane, or 0. */
  source: Int64;
  /** How far the hero may stand from the source before the pane departs; 0 takes the default. */
  reach: number;
  boundAt: number;
  frontedAt: number;
  fresh: boolean;
  /** Stands without the core pane (Options): opening it does not open the core pane, closing the core pane keeps it. */
  standalone: boolean;
}

/**
 * The book rule, after the prototype's folio in simplified form: the first leaf opened holds the front; a
 * newcomer of the same rank arrives behind as a fresh tab; a newcomer of a higher rank takes the front; an
 * explicit ask is absolute. Closing the front promotes the leaf last looked at, else the first in tab order.
 * Tab order is binding order. When the page side and the world side both have room they are two books;
 * when they would overlap they are one. The desktop client's UI/Logic/Book.cs.
 */
export class BookRule {
  private readonly bound: Leaf[] = [];
  private clock = 0;
  merged = true;

  get leaves(): readonly Leaf[] {
    return this.bound;
  }

  get(id: string): Leaf | undefined {
    return this.bound.find((l) => l.id === id);
  }

  has(id: string): boolean {
    return this.get(id) !== undefined;
  }

  bookOf(group: LeafGroup): Leaf[] {
    return this.merged ? [...this.bound] : this.within(group);
  }

  within(group: LeafGroup): Leaf[] {
    return this.bound.filter((l) => l.group === group);
  }

  static frontOf(book: readonly Leaf[]): Leaf | undefined {
    let best: Leaf | undefined;
    for (const leaf of book) {
      if (best === undefined || leaf.frontedAt > best.frontedAt) {
        best = leaf;
      }
    }
    return best;
  }

  front(group: LeafGroup): Leaf | undefined {
    return BookRule.frontOf(this.bookOf(group));
  }

  frontWithin(group: LeafGroup): Leaf | undefined {
    return BookRule.frontOf(this.within(group));
  }

  drawn(): Leaf[] {
    if (this.merged) {
      const f = BookRule.frontOf(this.bound);
      return f === undefined ? [] : [f];
    }
    return [this.frontWithin('page'), this.frontWithin('world')].filter((l): l is Leaf => l !== undefined);
  }

  isDrawn(id: string): boolean {
    return this.drawn().some((l) => l.id === id);
  }

  /** The drawn front the player touched last: what Escape closes and what the Tab walk turns. */
  active(): Leaf | undefined {
    let best: Leaf | undefined;
    for (const leaf of this.drawn()) {
      if (best === undefined || leaf.frontedAt > best.frontedAt || (leaf.frontedAt === best.frontedAt && leaf.rank > best.rank)) {
        best = leaf;
      }
    }
    return best;
  }

  bind(id: string, group: LeafGroup, kind: string, rank: number, title: string, ask: Ask = 'auto', source: Int64 = 0, reach = 0): Leaf {
    const existing = this.get(id);
    if (existing !== undefined) {
      existing.title = title;
      existing.source = source;
      existing.reach = reach;
      existing.rank = rank;
      if (ask === 'front') {
        this.select(id);
      }
      return existing;
    }
    const front = this.front(group);
    const leaf: Leaf = { id, group, kind, rank, title, source, reach, boundAt: ++this.clock, frontedAt: 0, fresh: false, standalone: false };
    this.bound.push(leaf);
    const fronts = ask === 'front' ? true : ask === 'behind' ? false : front === undefined || rank > front.rank;
    if (fronts) {
      leaf.frontedAt = ++this.clock;
    } else {
      leaf.fresh = true;
    }
    return leaf;
  }

  select(id: string): boolean {
    const leaf = this.get(id);
    if (leaf === undefined) {
      return false;
    }
    leaf.frontedAt = ++this.clock;
    leaf.fresh = false;
    return true;
  }

  close(id: string): boolean {
    const i = this.bound.findIndex((l) => l.id === id);
    if (i < 0) {
      return false;
    }
    this.bound.splice(i, 1);
    return true;
  }

  walk(direction: number): Leaf | undefined {
    const active = this.active();
    if (active === undefined) {
      return undefined;
    }
    const book = this.bookOf(active.group);
    if (book.length < 2) {
      return undefined;
    }
    const i = book.indexOf(active);
    const step = direction < 0 ? -1 : 1;
    const next = book[(((i + step) % book.length) + book.length) % book.length];
    this.select(next.id);
    return next;
  }

  get canWalk(): boolean {
    const a = this.active();
    return a !== undefined && this.bookOf(a.group).length >= 2;
  }
}
