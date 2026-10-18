import '../widgets';
import './pages.css';
import { el } from '../dom';
import type { Carry, DropTarget, LeafContext, LeafView } from '../leaf';
import { abilityArt, seatArt } from '../abilityArt';
import { seatReadyIn, type Seat } from '../logic/heroState';
import { css, lighten } from '../skin';
import { clamp01, drawnScale, inside, localPoint } from './geometry';
import { dropHint, dropIntent, glyphAt, hintTint, seatActions, seatCosts, seatMeta, seatTip } from './rack';

/** The glyph well's side, as the desktop draws it. */
const GLYPH = 52;

/** A seat dragged by its glyph onto another seat to swap them. */
interface SeatDrag {
  from: SeatRow;
  pointerId: number;
  x: number;
  y: number;
  started: boolean;
  ghost: HTMLElement | null;
  target: SeatRow | null;
}

/**
 * The rack: every weave seat drawn, in bar order. A learned form sits in its seat with its glyph sockets as pips; a
 * stone carried onto a pip sockets it and a form item onto a seat learns it; right-click takes a stone or a form
 * back out. A misfit glyph stays seated, greyed, with the reason in its tooltip. Seats reorder by dragging one by
 * its glyph onto another. The line under a seat's name, the preview lines and the costs are the server's. The
 * desktop client's UI/Pages/WeavesPage.cs.
 */
export class WeavesPage implements LeafView {
  readonly root: HTMLElement;
  private readonly list: HTMLElement;
  private readonly rows: SeatRow[] = [];
  private version = -1;
  private drag: SeatDrag | null = null;

  constructor(readonly ctx: LeafContext) {
    this.list = el('div', { class: 'rack-list' });
    this.root = el('div', { class: 'leaf-page weaves-page' }, this.list);
    this.root.addEventListener('contextmenu', (e) => e.preventDefault());
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460);
  }

  /** How many seats the rack has built, for scripted checks. */
  get seatCount(): number {
    return this.rows.length;
  }

  refresh(): void {
    const hero = this.ctx.hero;
    const count = Math.max(hero.slots?.seats.length ?? 0, this.ctx.skin.metric('slots', 8));
    const version = hero.slotsVersion * 1000 + (hero.self === null ? 0 : 1);
    if (version !== this.version) {
      this.version = version;
      const seats = hero.seats(count);
      while (this.rows.length < seats.length) {
        const row = new SeatRow(this, this.rows.length);
        this.list.append(row.root);
        this.rows.push(row);
      }
      this.rows.forEach((row, i) => {
        row.root.hidden = i >= seats.length;
        if (i < seats.length) {
          row.show(seats[i]);
        }
      });
    }
    const now = this.ctx.now;
    for (const row of this.rows) {
      row.tick(now);
    }
  }

  dropTargets(): DropTarget[] {
    return this.rows.filter((r) => !r.root.hidden);
  }

  /** Escape lets go of a seat being dragged. */
  onEscape(): boolean {
    if (this.drag === null) {
      return false;
    }
    this.cancelDrag();
    return true;
  }

  dispose(): void {
    this.cancelDrag();
  }

  /** Whether a client point is inside the page as it shows (a seat scrolled out of view takes nothing). */
  visibleAt(x: number, y: number): boolean {
    return inside(this.root.getBoundingClientRect(), x, y);
  }

  // ------------------------------------------------------------ reordering seats

  /** A left press on a seat's glyph: a drag onto another seat swaps them once the pointer moves. */
  pressSeat(row: SeatRow, e: PointerEvent): void {
    this.cancelDrag();
    this.drag = { from: row, pointerId: e.pointerId, x: e.clientX, y: e.clientY, started: false, ghost: null, target: null };
    try {
      row.root.setPointerCapture(e.pointerId);
    } catch {
      // the pointer is already gone: the release ends the drag
    }
  }

  dragMove(e: PointerEvent): void {
    const drag = this.drag;
    if (drag === null || e.pointerId !== drag.pointerId) {
      return;
    }
    if (!drag.started) {
      const r = drag.from.root.getBoundingClientRect();
      const threshold = this.ctx.skin.metric('drag_threshold', 6) * drawnScale(r.width, drag.from.root.offsetWidth);
      if (Math.hypot(e.clientX - drag.x, e.clientY - drag.y) < threshold) {
        return;
      }
      const seat = drag.from.seat;
      if (seat === null || seat.key === null) {
        this.cancelDrag();
        return;
      }
      drag.started = true;
      drag.ghost = this.ghostFor(seat);
    }
    this.placeGhost(drag.ghost, e.clientX, e.clientY);
    const over = this.rowAt(e.clientX, e.clientY);
    const target = over !== null && over !== drag.from ? over : null;
    if (target !== drag.target) {
      drag.target?.root.classList.remove('swap-target');
      target?.root.classList.add('swap-target');
      drag.target = target;
    }
  }

  dragEnd(e: PointerEvent): void {
    const drag = this.drag;
    if (drag === null || e.pointerId !== drag.pointerId) {
      return;
    }
    const target = drag.started ? this.rowAt(e.clientX, e.clientY) : null;
    this.cancelDrag();
    if (target !== null && target !== drag.from) {
      this.ctx.send({ t: 'swap_weave_slots', a: drag.from.index, b: target.index });
    }
  }

  cancelDrag(): void {
    const drag = this.drag;
    if (drag === null) {
      return;
    }
    this.drag = null;
    drag.ghost?.remove();
    drag.target?.root.classList.remove('swap-target');
    if (drag.from.root.hasPointerCapture(drag.pointerId)) {
      drag.from.root.releasePointerCapture(drag.pointerId);
    }
  }

  private rowAt(x: number, y: number): SeatRow | null {
    if (!this.visibleAt(x, y)) {
      return null;
    }
    return this.rows.find((r) => !r.root.hidden && inside(r.root.getBoundingClientRect(), x, y)) ?? null;
  }

  /** What a dragged seat looks like under the pointer: its glyph in a lit well, on the interface's top layer. */
  private ghostFor(seat: Seat): HTMLElement {
    const skin = this.ctx.skin;
    const ghost = el('div', { class: 'seat-ghost' });
    ghost.style.color = seat.threads.length > 0 ? css(skin.thread(seat.threads[0])) : skin.s('label');
    ghost.append(seatArt(skin, seat, 36, 'glyph'));
    const host = this.root.closest<HTMLElement>('.ui-root');
    if (host === null) {
      ghost.style.position = 'fixed';
      document.body.append(ghost);
    } else {
      host.append(ghost);
    }
    return ghost;
  }

  private placeGhost(ghost: HTMLElement | null, x: number, y: number): void {
    if (ghost === null) {
      return;
    }
    const host = ghost.parentElement;
    let [lx, ly] = [x, y];
    if (host !== null && ghost.style.position !== 'fixed') {
      const r = host.getBoundingClientRect();
      [lx, ly] = localPoint(r, drawnScale(r.width, host.offsetWidth), x, y);
    }
    ghost.style.transform = `translate(${lx - GLYPH / 2}px, ${ly - GLYPH / 2}px)`;
  }
}

/** One seat of the rack, and a drop target for stones and forms. */
class SeatRow implements DropTarget {
  readonly root: HTMLElement;
  seat: Seat | null = null;
  private glyph: HTMLElement | null = null;
  private sweep: HTMLElement | null = null;
  private sockets: HTMLElement[] = [];
  /** The socket a carried stone is over: -1 for the seat itself, -2 while nothing is carried here. */
  private dropSocket = -2;
  private hint = '';

  constructor(
    private readonly page: WeavesPage,
    readonly index: number,
  ) {
    this.root = el('div', { class: 'rack-seat empty', 'data-slot': String(index) });
    this.root.addEventListener('pointerdown', (e) => this.pointerDown(e));
    this.root.addEventListener('pointermove', (e) => page.dragMove(e));
    this.root.addEventListener('pointerup', (e) => page.dragEnd(e));
    this.root.addEventListener('pointercancel', () => page.cancelDrag());
    this.root.addEventListener('lostpointercapture', () => page.cancelDrag());
  }

  show(seat: Seat): void {
    this.seat = seat;
    const skin = this.page.ctx.skin;
    const info = seat.info;
    const empty = seat.key === null;
    const dormant = !empty && seat.dormant !== null;
    this.root.title = seatTip(seat);
    this.root.classList.toggle('empty', empty);
    this.root.classList.toggle('dormant', dormant);
    this.root.classList.toggle('active', !empty && info?.active === true);
    this.glyph = el('div', { class: 'seat-glyph well' });
    this.sweep = null;
    this.sockets = [];
    const main = el('div', { class: 'seat-main' });
    const preview = el('div', { class: 'seat-preview' });
    if (empty) {
      main.append(el('div', { class: 'seat-empty', text: 'Empty seat' }));
    } else {
      // The glyph and the name take the seat's first Thread; a dormant seat greys both (by its class).
      const thread = !dormant && seat.threads.length > 0 ? css(skin.thread(seat.threads[0])) : '';
      this.glyph.style.color = thread;
      this.sweep = el('span', { class: 'seat-sweep', hidden: '' });
      this.glyph.append(el('span', { class: 'seat-halo' }), seatArt(skin, seat, Math.round(GLYPH * 0.7), 'glyph seat-icon'), this.sweep);
      const costs = seatCosts(seat);
      if (costs.length > 0) {
        const box = el('span', { class: 'seat-costs' });
        for (const cost of costs) {
          const n = el('span', { class: 'seat-cost', 'data-resource': cost.resource, text: String(cost.amount) });
          n.style.color = css(lighten(skin.fill(cost.resource), 0.25));
          box.append(n);
        }
        this.glyph.append(box);
      }
      if (seat.converted !== null) {
        this.glyph.append(el('span', { class: 'seat-face' }));
      }
      const name = el('div', { class: 'seat-name', text: seat.name });
      name.style.color = thread;
      main.append(name, el('div', { class: 'seat-meta', text: seatMeta(seat) }));
      const sockets = el('div', { class: 'seat-sockets' });
      for (let i = 0; i < (info?.sockets ?? 0); i++) {
        const socket = el('span', { class: 'seat-socket', 'data-socket': String(i) });
        const glyph = glyphAt(info, i);
        if (glyph !== null) {
          const dormantGlyph = glyph.dormant !== undefined && glyph.dormant !== null;
          socket.append(abilityArt(skin, skin.names.abilityIcons.glyph(glyph.id), 16, 'socket-art', dormantGlyph));
        }
        sockets.append(socket);
        this.sockets.push(socket);
      }
      main.append(sockets);
      for (const line of (info?.preview ?? []).slice(0, 4)) {
        preview.append(el('div', { class: 'preview-line' }, el('span', { class: 'preview-label', text: line.label }), el('span', { class: 'preview-text num', text: line.text })));
      }
    }
    this.root.replaceChildren(el('span', { class: 'seat-index num', text: String(this.index + 1) }), this.glyph, main, preview);
    this.paintDrop();
  }

  /** The cooldown sweep, against the full cooldown, while the seat cools. */
  tick(now: number): void {
    const seat = this.seat;
    const sweep = this.sweep;
    if (seat === null || sweep === null) {
      return;
    }
    const left = seatReadyIn(seat, now);
    if (left > 0.05 && seat.cooldown > 0) {
      sweep.style.setProperty('--sweep', `${(clamp01(left / seat.cooldown) * 360).toFixed(1)}deg`);
      if (sweep.hidden) {
        sweep.hidden = false;
      }
    } else if (!sweep.hidden) {
      sweep.hidden = true;
    }
  }

  private pointerDown(e: PointerEvent): void {
    const seat = this.seat;
    if (seat === null || this.page.ctx.items.carrying !== null) {
      return;
    }
    if (e.button === 2) {
      e.preventDefault();
      if (seat.key === null) {
        return;
      }
      e.stopPropagation();
      const actions = seatActions(seat, this.index, this.socketAt(e.clientX, e.clientY), this.onGlyph(e.clientX, e.clientY));
      if (actions.length > 0) {
        this.page.ctx.menu(e.clientX, e.clientY, actions.map((a) => ({ label: a.label, act: () => this.page.ctx.send(a.intent) })));
      }
      return;
    }
    if (e.button === 0 && seat.key !== null && this.onGlyph(e.clientX, e.clientY)) {
      e.preventDefault();
      e.stopPropagation();
      this.page.pressSeat(this, e);
    }
  }

  private onGlyph(x: number, y: number): boolean {
    return this.glyph !== null && inside(this.glyph.getBoundingClientRect(), x, y);
  }

  /** The socket under a client point, with a little slack around each pip; -1 for none. */
  private socketAt(x: number, y: number): number {
    for (let i = 0; i < this.sockets.length; i++) {
      const s = this.sockets[i];
      const r = s.getBoundingClientRect();
      if (inside(r, x, y, 3, drawnScale(r.width, s.offsetWidth))) {
        return i;
      }
    }
    return -1;
  }

  /** Tints the seat with what the carried item would do here, and rings the socket a stone is over. */
  private paintDrop(): void {
    const tint = hintTint(this.hint);
    if (tint === null) {
      delete this.root.dataset.drop;
      delete this.root.dataset.hint;
    } else {
      this.root.dataset.drop = tint;
      this.root.dataset.hint = this.hint;
    }
    this.sockets.forEach((s, i) => s.classList.toggle('drop', i === this.dropSocket));
  }

  // ------------------------------------------------------------ drop target

  covers(x: number, y: number): boolean {
    if (this.seat === null || this.root.hidden || !this.root.isConnected || this.root.offsetParent === null) {
      return false;
    }
    return this.page.visibleAt(x, y) && inside(this.root.getBoundingClientRect(), x, y);
  }

  hover(carry: Carry, x: number, y: number): void {
    const isStone = carry.item.glyph !== undefined && carry.item.glyph !== null;
    const socket = isStone ? this.socketAt(x, y) : -1;
    const hint = dropHint(carry.item, this.index);
    if (socket !== this.dropSocket || hint !== this.hint) {
      this.dropSocket = socket;
      this.hint = hint;
      this.paintDrop();
    }
  }

  leave(): void {
    if (this.dropSocket !== -2 || this.hint.length > 0) {
      this.dropSocket = -2;
      this.hint = '';
      this.paintDrop();
    }
  }

  drop(carry: Carry, x: number, y: number): boolean {
    const isStone = carry.item.glyph !== undefined && carry.item.glyph !== null;
    const socket = isStone ? this.socketAt(x, y) : -1;
    this.leave();
    const intent = dropIntent(carry.item, this.index, socket, this.seat?.info ?? null);
    if (intent === null) {
      return false;
    }
    this.page.ctx.send(intent);
    return true;
  }
}
