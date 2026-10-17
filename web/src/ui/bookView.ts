import type { LeafView } from './leaf';
import type { Leaf } from './logic/book';
import type { Box } from './logic/layout';
import type { Skin } from './skin';
import { makePane, setTabs, type PaneParts } from './widgets';

/**
 * One book on screen: a framed pane drawing its front leaf, with a tab strip when it holds two or more leaves.
 * Tabs keep binding order; a fresh tab glows until it is looked at. The close glyph closes the front leaf
 * through its own close path. The desktop client's UI/Book/BookView.cs.
 */
export class BookView {
  readonly root: HTMLElement;
  private readonly parts: PaneParts;
  private shownView: LeafView | null = null;
  frontId: string | null = null;

  constructor(
    skin: Skin,
    id: string,
    private readonly select: (leafId: string) => void,
    close: (leafId: string) => void,
  ) {
    this.parts = makePane(skin, '', { closable: true, className: 'book' });
    this.root = this.parts.root;
    this.root.id = id;
    this.root.hidden = true;
    this.parts.close?.addEventListener('click', (e) => {
      e.stopPropagation();
      if (this.frontId !== null) {
        close(this.frontId);
      }
    });
  }

  get shown(): LeafView | null {
    return this.shownView;
  }

  /** Draws a book: its leaves in tab order, the front one's view in the body. */
  show(box: Box, leaves: readonly Leaf[], front: Leaf, view: LeafView, emphasised: boolean): void {
    this.root.hidden = false;
    this.root.style.left = `${box.x}px`;
    this.root.style.top = `${box.y}px`;
    this.root.style.width = `${box.w}px`;
    this.root.style.height = `${box.h}px`;
    this.root.classList.toggle('emphasised', emphasised);
    this.root.dataset.front = front.id;
    setTabs(this.parts, front.title, leaves.length >= 2 ? leaves.map((l) => ({ id: l.id, title: l.title, active: l.id === front.id, fresh: l.fresh })) : [], this.select);
    if (this.shownView !== view) {
      if (this.shownView !== null && this.shownView.root.parentElement === this.parts.body) {
        this.shownView.root.remove();
      }
      this.parts.body.append(view.root);
      this.shownView = view;
    }
    this.frontId = front.id;
  }

  hide(): void {
    this.root.hidden = true;
    if (this.shownView !== null && this.shownView.root.parentElement === this.parts.body) {
      this.shownView.root.remove();
    }
    this.shownView = null;
    this.frontId = null;
  }

  /** Lets go of a leaf view that is about to be disposed. */
  forget(view: LeafView): void {
    if (this.shownView === view) {
      if (view.root.parentElement === this.parts.body) {
        view.root.remove();
      }
      this.shownView = null;
      this.frontId = null;
    }
  }
}
