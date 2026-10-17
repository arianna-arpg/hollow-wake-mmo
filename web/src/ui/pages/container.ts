import '../widgets';
import './pages.css';
import type { ContainerInfo } from '../../net/protocol';
import { BoardView } from '../board';
import { el } from '../dom';
import type { Carry, DropTarget, LeafContext, LeafView } from '../leaf';
import { nameOf } from '../logic/content';
import { button, priceText } from '../widgets';
import { boardNotes, boardsOf, buyContainer, buyState, nextToBuy, overflowNote, overflowOf, rungText, stashCount, tabLabel, tabTip } from './boards';
import { inside } from './geometry';

/**
 * A container's board as a page or a pane: the reliquary (whose seated relics are active), the stashes with their
 * pages, a station's board. It shows each board the inventory state sends for the page, a tab per board when there
 * are several (a stash's pages, named by their stash when more than one stash is shown), its unlock rungs with what
 * each requires, why it cannot be changed from here when it cannot, a page not yet bought with its price and, on
 * the first such page of its stash, a button to buy it, and the board's overflow (items it holds without a cell) to
 * take them out of. Items move between it and the bag by drag and drop or click-lift, one move intent each. The
 * desktop client's UI/Pages/ContainerPage.cs.
 */
export class ContainerPage implements LeafView {
  readonly root: HTMLElement;
  private readonly tabs: HTMLElement;
  private readonly note: HTMLElement;
  private readonly buy: HTMLButtonElement;
  private readonly board: BoardView;
  private readonly rungs: HTMLElement;
  private readonly overflowLine: HTMLElement;
  private readonly overflow: BoardView;
  /** The boards as drop targets that take nothing outside the page as it shows. */
  private readonly targets: { board: DropTarget; overflow: DropTarget };
  private selected = '';
  private version = -1;
  private shownBoard: ContainerInfo | null = null;

  /** @param matches which containers this page shows (a kind, or the pages of one stash). */
  constructor(
    private readonly ctx: LeafContext,
    private readonly matches: (c: ContainerInfo) => boolean,
    private readonly emptyText: string,
  ) {
    this.tabs = el('div', { class: 'container-tabs', role: 'tablist' });
    this.note = el('div', { class: 'container-note' });
    // Buys the stash's next page at the server's price; the server answers with the page unlocked.
    this.buy = button('Buy this page', () => {
      const page = this.shownBoard;
      if (page !== null && page.locked === true) {
        this.ctx.send({ t: 'buy_page', container: buyContainer(page) });
      }
    }, { className: 'container-buy' });
    this.buy.hidden = true;
    this.board = new BoardView(ctx.items, '');
    this.rungs = el('div', { class: 'container-rungs' });
    this.overflowLine = el('div', { class: 'container-overflow-note', hidden: '' });
    this.overflow = new BoardView(ctx.items, '');
    this.overflow.root.classList.add('container-overflow');
    this.root = el('div', { class: 'leaf-page container-page' },
      el('div', { class: 'container-column' }, this.tabs, this.note, this.buy, this.board.root, this.rungs, this.overflowLine, this.overflow.root));
    this.targets = { board: this.clipped(this.board), overflow: this.clipped(this.overflow) };
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460);
  }

  /** The board shown, for scripted checks. */
  get shown(): ContainerInfo | null {
    return this.shownBoard;
  }

  /** The boards the page tabs between: every matching container but the overflow boards, in page order. */
  get boards(): ContainerInfo[] {
    return boardsOf(this.ctx.hero.inventory?.containers ?? [], this.matches);
  }

  refresh(): void {
    const hero = this.ctx.hero;
    if (hero.inventoryVersion === this.version) {
      return;
    }
    this.version = hero.inventoryVersion;
    const containers = this.boards;
    this.tabs.replaceChildren();
    this.rungs.replaceChildren();
    this.tabs.hidden = true;
    this.rungs.hidden = true;
    if (containers.length === 0) {
      this.shownBoard = null;
      delete this.root.dataset.shown;
      this.setNote(this.emptyText);
      this.buy.hidden = true;
      this.board.show(null, 0);
      this.showOverflow(null);
      return;
    }
    if (!containers.some((c) => c.id === this.selected)) {
      this.selected = containers[0].id;
    }
    const price = (p: Readonly<Record<string, number>>) => priceText(p, (id) => nameOf(this.ctx.skin.names.currencies, id));
    if (containers.length > 1) {
      // Pages of one stash read "Page 2"; with more than one stash shown, each tab names its stash.
      const stashes = stashCount(containers);
      for (const c of containers) {
        const tab = button(tabLabel(c, stashes), () => {
          this.selected = c.id;
          this.version = -1;
          this.refresh();
        }, { className: `container-tab${c.locked === true ? ' locked' : ''}`, pressed: c.id === this.selected, title: tabTip(c, price) });
        tab.dataset.container = c.id;
        tab.setAttribute('role', 'tab');
        tab.setAttribute('aria-selected', String(c.id === this.selected));
        this.tabs.append(tab);
      }
      this.tabs.hidden = false;
    }
    const shown = containers.find((c) => c.id === this.selected) ?? containers[0];
    this.shownBoard = shown;
    this.root.dataset.shown = shown.id;
    // Pages are bought in order: the first locked page of its stash can be bought, later ones wait for it.
    const next = nextToBuy(containers, shown);
    this.setNote(boardNotes(shown, next, price));
    const buy = buyState(shown, next, price);
    this.buy.hidden = !buy.visible;
    this.buy.disabled = !buy.enabled;
    this.buy.title = buy.title;
    this.board.container = shown.id;
    this.board.show(shown.board, this.cell(), !shown.reachable || shown.locked === true);
    for (const rung of shown.board.rungs ?? []) {
      this.rungs.append(el('div', { class: `container-rung${rung.unlocked ? '' : ' locked'}`, 'data-rung': String(rung.index), text: rungText(rung) }));
    }
    this.rungs.hidden = this.rungs.childElementCount === 0;
    this.showOverflow(overflowOf(hero.inventory?.containers ?? [], shown.id));
  }

  dropTargets(): DropTarget[] {
    const targets: DropTarget[] = [];
    if (this.board.shown !== null) {
      targets.push(this.targets.board);
    }
    if (this.overflow.shown !== null && !this.overflow.root.hidden) {
      targets.push(this.targets.overflow);
    }
    return targets;
  }

  onEscape(): boolean {
    return false;
  }

  dispose(): void {
    // nothing held outside the page
  }

  private cell(): number {
    return this.ctx.skin.metric('cell', 36);
  }

  private setNote(text: string): void {
    this.note.textContent = text;
    this.note.hidden = text.length === 0;
  }

  /** The items a board holds without a cell, to drag out of (nothing drops onto them). */
  private showOverflow(overflow: ContainerInfo | null): void {
    this.overflowLine.hidden = overflow === null;
    if (overflow === null) {
      this.overflow.show(null, 0);
      return;
    }
    this.overflowLine.textContent = overflowNote(overflow);
    this.overflow.container = overflow.id;
    this.overflow.show(overflow.board, this.cell(), true);
  }

  /** A board as a drop target that also needs the point inside the page as it shows (a board scrolled away takes nothing). */
  private clipped(board: BoardView): DropTarget {
    return {
      covers: (x: number, y: number) => inside(this.root.getBoundingClientRect(), x, y) && board.covers(x, y),
      hover: (carry: Carry, x: number, y: number) => board.hover(carry, x, y),
      leave: () => board.leave(),
      drop: (carry: Carry, x: number, y: number) => board.drop(carry, x, y),
    };
  }
}
