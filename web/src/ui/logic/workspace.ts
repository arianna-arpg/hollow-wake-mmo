import type { Int64 } from '../../net/protocol';
import { BookRule, type Ask, type Leaf } from './book';
import { isCore, type UiPage } from './content';

/** How a page was requested: its key, a menu row, its ribbon on the rail, or a show from within a page. */
export type Via = 'key' | 'menu' | 'ribbon' | 'show';

/** What one press of Escape did. */
export type EscapeStep = 'nothing' | 'tray' | 'pane' | 'core';

/**
 * The core pane and the panes beside it, as one state machine both clients share: requesting a page opens the
 * core pane and selects it; repeating its key or menu row closes the core pane; activating the selected
 * page's ribbon dismisses only that page. Closing the core pane remembers its pages and the selected one;
 * the world's panes close with it. Escape folds the tray, then closes the front pane, the core pane last.
 * A world pane departs when the hero walks out of reach. The desktop client's UI/Logic/Workspace.cs.
 */
export class Workspace {
  readonly book = new BookRule();
  coreOpen = false;
  trayOpen = false;
  private retained: string[] = [];
  private retainedFront: string | null = null;
  private readonly closedListeners: Array<(leaf: Leaf) => void> = [];

  constructor(
    private readonly pages: (id: string) => UiPage | undefined,
    private readonly rankOf: (kind: string) => number,
  ) {}

  onWorldClosed(listener: (leaf: Leaf) => void): void {
    this.closedListeners.push(listener);
  }

  get selectedPage(): string | undefined {
    return this.book.frontWithin('page')?.id;
  }

  isShown(pageId: string): boolean {
    return this.coreOpen && this.book.isDrawn(pageId);
  }

  requestPage(pageId: string, via: Via): void {
    const page = this.pages(pageId);
    if (page === undefined) {
      return;
    }
    if (isCore(page)) {
      if (via === 'show' || !this.coreOpen) {
        this.openCore();
      } else {
        this.closeCore();
      }
      return;
    }
    if (this.coreOpen && this.isShown(pageId)) {
      if (via === 'key' || via === 'menu') {
        this.closeCore();
        return;
      }
      if (via === 'ribbon') {
        this.dismissPage(pageId);
        return;
      }
    }
    this.openCore();
    this.book.bind(page.id, 'page', page.id, this.rankOf('page'), page.name, 'front');
  }

  openCore(): void {
    if (this.coreOpen) {
      return;
    }
    this.coreOpen = true;
    for (const id of this.retained) {
      const page = this.pages(id);
      if (page !== undefined) {
        this.book.bind(page.id, 'page', page.id, this.rankOf('page'), page.name, 'behind');
      }
    }
    if (this.retainedFront !== null && this.book.has(this.retainedFront)) {
      this.book.select(this.retainedFront);
    }
    this.retained = [];
    this.retainedFront = null;
  }

  closeCore(): void {
    if (!this.coreOpen) {
      return;
    }
    this.retained = this.book.within('page').map((l) => l.id);
    this.retainedFront = this.book.frontWithin('page')?.id ?? null;
    for (const leaf of this.book.within('page')) {
      this.book.close(leaf.id);
    }
    for (const leaf of this.book.within('world')) {
      this.closeWorld(leaf.id);
    }
    this.coreOpen = false;
    this.trayOpen = false;
  }

  dismissPage(pageId: string): void {
    this.book.close(pageId);
  }

  openWorld(id: string, kind: string, title: string, source: Int64 = 0, reach = 0, ask: Ask = 'auto'): Leaf {
    this.openCore();
    return this.book.bind(id, 'world', kind, this.rankOf(kind), title, ask, source, reach);
  }

  hasWorld(id: string): boolean {
    return this.book.get(id)?.group === 'world';
  }

  closeWorld(id: string): void {
    const leaf = this.book.get(id);
    if (leaf === undefined || leaf.group !== 'world') {
      return;
    }
    this.book.close(id);
    for (const listener of this.closedListeners) {
      listener(leaf);
    }
  }

  selectTab(id: string): void {
    this.book.select(id);
  }

  closeLeaf(id: string): void {
    const leaf = this.book.get(id);
    if (leaf === undefined) {
      return;
    }
    if (leaf.group === 'world') {
      this.closeWorld(id);
    } else {
      this.dismissPage(id);
    }
  }

  escape(): EscapeStep {
    if (this.trayOpen) {
      this.trayOpen = false;
      return 'tray';
    }
    const front = this.book.active();
    if (front !== undefined) {
      this.closeLeaf(front.id);
      return 'pane';
    }
    if (this.coreOpen) {
      this.closeCore();
      return 'core';
    }
    return 'nothing';
  }

  /** Closes every world pane whose source is gone (null) or farther than its reach, or the default reach. */
  depart(distanceTo: (key: Int64) => number | null, defaultReach: number): Leaf[] {
    const departed = this.book.within('world').filter((leaf) => {
      if (leaf.source === 0 || leaf.source === '0') {
        return false;
      }
      const reach = leaf.reach > 0 ? leaf.reach : defaultReach;
      const d = distanceTo(leaf.source);
      return d === null || d > reach;
    });
    for (const leaf of departed) {
      this.closeWorld(leaf.id);
    }
    return departed;
  }
}
