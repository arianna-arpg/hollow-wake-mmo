import '../widgets';
import './pages.css';
import { el } from '../dom';
import type { DropTarget, LeafContext, LeafView } from '../leaf';
import type { ChoiceGroup, PassiveNode } from '../logic/content';
import { PanZoom } from '../logic/panZoom';
import { css, lighten } from '../skin';
import { button } from '../widgets';
import { bounds, clickAction, graftBound, graftFrom, graftSeatLabel, isGrand, linkPairs, named, nodeAt, nodeRadius, nodeTip, optionMatches, pointsText, reachable, realmName, realmsOpen, refundTip } from './constellation';
import { backingSize, clampBox, drawnScale, localPoint, wheelFactor } from './geometry';

const TAU = Math.PI * 2;

/** A press on the canvas: a drag pans, a release where it began clicks the node there. */
interface Press {
  pointerId: number;
  x: number;
  y: number;
  lastX: number;
  lastY: number;
}

/** The nodes of the realm shown and each link between them once, read from content when the realm changes. */
interface RealmNodes {
  realm: string;
  nodes: PassiveNode[];
  links: Array<[PassiveNode, PassiveNode]>;
}

/**
 * The passive constellation, read from passive content and drawn with the passive state: a tab per open realm, the
 * nodes on a canvas panned with a drag and zoomed with the wheel, allocated nodes lit, choice nodes opening a
 * searchable popup of their options, and a refund mode while a font is in reach; under it, the grafts the passives
 * grant, each bound to a seat picked from the rack. Clicking sends the allocate or refund intent; the server
 * validates every one. Tooltips come from the same content the server validates against. The canvas is drawn at
 * the screen's pixels and only when something changed. The desktop client's UI/Pages/PassivesPage.cs.
 */
export class PassivesPage implements LeafView {
  readonly root: HTMLElement;
  readonly canvas: HTMLCanvasElement;
  private readonly bar: HTMLElement;
  private readonly points: HTMLElement;
  private readonly refundButton: HTMLButtonElement;
  private readonly holder: HTMLElement;
  private readonly grafts: HTMLElement;
  private readonly popup: ChoicePopup;
  private readonly resize: ResizeObserver | null;
  private realmId = 'star';
  private refunding = false;
  private barVersion = -1;
  private graftsVersion = -1;
  private view: PanZoom | null = null;
  private fitted = { realm: '', w: -1, h: -1 };
  private realmNodes: RealmNodes | null = null;
  private hover: PassiveNode | null = null;
  private press: Press | null = null;
  private dirty = true;
  private drawn = { scale: 0, ratio: 0 };
  private frames = 0;

  constructor(private readonly ctx: LeafContext) {
    this.points = el('span', { class: 'passives-points' });
    this.refundButton = button('Refund', () => this.toggleRefund(), { className: 'refund-toggle' });
    this.bar = el('div', { class: 'passives-bar' });
    this.canvas = el('canvas', { class: 'passives-canvas' });
    this.holder = el('div', { class: 'passives-view' }, this.canvas);
    this.grafts = el('div', { class: 'passives-grafts', hidden: '' });
    this.popup = new ChoicePopup((node, option) => this.pick(node, option));
    this.root = el('div', { class: 'leaf-page passives-page', 'data-realm': this.realmId }, this.bar, this.holder, this.grafts, this.popup.root);
    const c = this.canvas;
    c.addEventListener('pointerdown', (e) => this.pointerDown(e));
    c.addEventListener('pointermove', (e) => this.pointerMove(e));
    c.addEventListener('pointerup', (e) => this.pointerUp(e));
    c.addEventListener('pointercancel', () => this.endPress());
    c.addEventListener('lostpointercapture', () => this.endPress());
    c.addEventListener('pointerleave', () => {
      if (this.press === null) {
        this.setHover(null);
      }
    });
    c.addEventListener('wheel', (e) => this.wheel(e), { passive: false });
    c.addEventListener('contextmenu', (e) => e.preventDefault());
    this.resize = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(() => {
      this.dirty = true;
      this.draw();
    });
    this.resize?.observe(this.holder);
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('page_width', 460) * 1.15;
  }

  /** The realm shown, for scripted checks. */
  get realm(): string {
    return this.realmId;
  }

  /** Whether clicks refund allocated nodes, for scripted checks. */
  get refundMode(): boolean {
    return this.refunding;
  }

  /** Whether a choice node's options are open. */
  get choosing(): boolean {
    return this.popup.isOpen;
  }

  refresh(): void {
    const hero = this.ctx.hero;
    const version = hero.passivesVersion * 7919 + hero.points('passive') * 31 + hero.allocated.length;
    if (version !== this.barVersion) {
      this.barVersion = version;
      this.rebuildBar();
      this.paintTip();
      this.dirty = true;
    }
    if (hero.slotsVersion !== this.graftsVersion) {
      // The seats a graft binds to are named from the weave slots.
      this.rebuildGrafts();
    }
    if ((window.devicePixelRatio || 1) !== this.drawn.ratio) {
      this.dirty = true;
    }
    // A new interface scale need not resize the canvas: look at the scale it is drawn at now and then.
    if (!this.dirty && ++this.frames % 20 === 0 && this.canvas.clientWidth > 0) {
      const r = this.canvas.getBoundingClientRect();
      if (Math.abs(drawnScale(r.width, this.canvas.clientWidth) - this.drawn.scale) > 1e-3) {
        this.dirty = true;
      }
    }
    if (this.dirty) {
      this.draw();
    }
  }

  dropTargets(): DropTarget[] {
    return [];
  }

  /** Escape closes a choice node's options. */
  onEscape(): boolean {
    if (!this.popup.isOpen) {
      return false;
    }
    this.popup.hide();
    return true;
  }

  dispose(): void {
    this.resize?.disconnect();
    this.popup.hide();
    this.endPress();
  }

  /** Where a node of the realm shown is drawn, in client pixels (for scripted clicks); null when it is not drawn. */
  nodePoint(id: string): { x: number; y: number } | null {
    const node = this.nodes().find((n) => n.id === id);
    const w = this.canvas.clientWidth;
    if (node === undefined || w <= 0) {
      return null;
    }
    const [x, y] = this.panZoom().toScreen(node.x, node.y);
    const r = this.canvas.getBoundingClientRect();
    const scale = drawnScale(r.width, w);
    return { x: r.left + x * scale, y: r.top + y * scale };
  }

  // ------------------------------------------------------------ the bar

  private rebuildBar(): void {
    const hero = this.ctx.hero;
    const realms = realmsOpen(hero.passives?.realms);
    if (!realms.includes(this.realmId)) {
      this.realmId = realms[0];
      this.dirty = true;
    }
    this.root.dataset.realm = this.realmId;
    const parts: HTMLElement[] = [];
    if (realms.length > 1) {
      for (const realm of realms) {
        const tab = button(realmName(this.ctx.skin.names, realm), () => this.showRealm(realm), { className: 'realm-tab', pressed: realm === this.realmId });
        tab.dataset.realm = realm;
        parts.push(tab);
      }
    }
    this.points.textContent = pointsText(hero.passives?.points ?? { passive: hero.points('passive') });
    parts.push(this.points);
    const refund = hero.passives?.refund === true;
    if (!refund && this.refunding) {
      this.refunding = false;
      this.dirty = true;
    }
    this.paintRefund();
    if (refund) {
      this.refundButton.title = refundTip(hero.passives?.refund_cost);
      parts.push(this.refundButton);
    }
    this.bar.replaceChildren(...parts);
    this.rebuildGrafts();
  }

  /**
   * The grafts the passives grant: each glyph with its level and the node that grants it, and the weave seat it is
   * bound to, picked from the seats; the server validates the binding.
   */
  private rebuildGrafts(): void {
    const hero = this.ctx.hero;
    const skin = this.ctx.skin;
    this.graftsVersion = hero.slotsVersion;
    const grafts = hero.passives?.grafts ?? [];
    if (grafts.length === 0) {
      this.grafts.replaceChildren();
      this.grafts.hidden = true;
      return;
    }
    const seats = hero.seats(skin.metric('slots', 8));
    const rows: HTMLElement[] = [el('div', { class: 'grafts-heading', text: 'Grafts' })];
    for (const graft of grafts) {
      const name = el('span', { class: 'graft-name', title: `Granted by ${graftFrom(skin.names, graft.source)}`, text: `${graft.name} ${graft.level}` });
      const picker = el('select', { class: 'graft-seat', tabindex: '-1' });
      picker.append(el('option', { value: '-1', text: 'Unbound' }));
      seats.forEach((seat, i) => picker.append(el('option', { value: String(i), text: graftSeatLabel(i, seat) })));
      picker.value = String(graftBound(graft.slot, seats.length));
      picker.addEventListener('change', () => {
        this.ctx.send({ t: 'bind_graft', source: graft.source, slot: Number(picker.value) });
        // Keys go back to the game once a seat is picked.
        picker.blur();
      });
      rows.push(el('div', { class: 'graft', 'data-source': graft.source }, name, picker));
    }
    this.grafts.replaceChildren(...rows);
    this.grafts.hidden = false;
  }

  private showRealm(realm: string): void {
    this.realmId = realm;
    // A realm tab fits its constellation to the canvas afresh, even the one shown.
    this.fitted.realm = '';
    this.setHover(null);
    this.rebuildBar();
    this.dirty = true;
  }

  private toggleRefund(): void {
    this.refunding = !this.refunding;
    this.paintRefund();
    this.dirty = true;
  }

  private paintRefund(): void {
    this.refundButton.textContent = this.refunding ? 'Refunding' : 'Refund';
    this.refundButton.classList.toggle('pressed', this.refunding);
    this.root.toggleAttribute('data-refunding', this.refunding);
  }

  // ------------------------------------------------------------ clicks

  private click(node: PassiveNode, clientX: number, clientY: number): void {
    const action = clickAction(node, this.ctx.hero.allocated.includes(node.id), this.refunding, this.ctx.skin.names.choices);
    if (action.kind === 'refund') {
      this.ctx.send({ t: 'refund_passive', node: node.id });
    } else if (action.kind === 'allocate') {
      this.ctx.send({ t: 'allocate_passive', node: node.id });
    } else if (action.kind === 'choose') {
      const r = this.root.getBoundingClientRect();
      const [x, y] = localPoint(r, drawnScale(r.width, this.root.offsetWidth), clientX, clientY);
      this.popup.open(node, action.group, x, y, this.root.clientWidth, this.root.clientHeight);
    }
  }

  private pick(node: PassiveNode, option: string): void {
    this.popup.hide();
    this.ctx.send({ t: 'allocate_passive', node: node.id, option });
  }

  // ------------------------------------------------------------ the canvas's input

  /** A client point in the canvas's interface pixels. A change of the drawn scale redraws the canvas sharp. */
  private local(e: { clientX: number; clientY: number }): [number, number] {
    const r = this.canvas.getBoundingClientRect();
    const scale = drawnScale(r.width, this.canvas.clientWidth);
    if (Math.abs(scale - this.drawn.scale) > 1e-3) {
      this.dirty = true;
    }
    return localPoint(r, scale, e.clientX, e.clientY);
  }

  private pointerDown(e: PointerEvent): void {
    if (e.button !== 0 || this.ctx.items.carrying !== null) {
      return;
    }
    e.preventDefault();
    // The press keeps focus where it was: let go of the search field so keys reach the game again.
    this.popup.blur();
    const [x, y] = this.local(e);
    this.press = { pointerId: e.pointerId, x, y, lastX: x, lastY: y };
    try {
      this.canvas.setPointerCapture(e.pointerId);
    } catch {
      // the pointer is already gone: the release ends the press
    }
    this.canvas.classList.add('panning');
  }

  private pointerMove(e: PointerEvent): void {
    const [x, y] = this.local(e);
    const press = this.press;
    if (press !== null && e.pointerId === press.pointerId && (e.buttons & 1) !== 0) {
      this.panZoom().pan(x - press.lastX, y - press.lastY);
      press.lastX = x;
      press.lastY = y;
      this.dirty = true;
    }
    this.setHover(this.nodeUnder(x, y));
  }

  private pointerUp(e: PointerEvent): void {
    const press = this.press;
    if (press === null || e.pointerId !== press.pointerId || e.button !== 0) {
      return;
    }
    this.endPress();
    const [x, y] = this.local(e);
    if (Math.hypot(x - press.x, y - press.y) < 5) {
      const node = this.nodeUnder(x, y);
      if (node !== null) {
        this.click(node, e.clientX, e.clientY);
      }
    }
  }

  private endPress(): void {
    const press = this.press;
    this.press = null;
    this.canvas.classList.remove('panning');
    if (press !== null && this.canvas.hasPointerCapture(press.pointerId)) {
      this.canvas.releasePointerCapture(press.pointerId);
    }
  }

  private wheel(e: WheelEvent): void {
    e.preventDefault();
    const [x, y] = this.local(e);
    this.panZoom().zoomAt(x, y, wheelFactor(e.deltaY, e.deltaMode));
    this.dirty = true;
    this.setHover(this.nodeUnder(x, y));
  }

  private setHover(node: PassiveNode | null): void {
    if ((node?.id ?? null) === (this.hover?.id ?? null)) {
      return;
    }
    this.hover = node;
    this.paintTip();
    this.canvas.classList.toggle('over-node', node !== null);
    this.dirty = true;
  }

  /** The hovered node's tooltip, from content with the hero's picks. */
  private paintTip(): void {
    const node = this.hover;
    if (node === null) {
      this.canvas.removeAttribute('title');
      delete this.canvas.dataset.hover;
      return;
    }
    this.canvas.title = nodeTip(node, this.ctx.skin.names.choices, this.ctx.hero.passives?.picks);
    this.canvas.dataset.hover = node.id;
  }

  // ------------------------------------------------------------ the constellation

  private nodes(): PassiveNode[] {
    return this.constellation().nodes;
  }

  private constellation(): RealmNodes {
    if (this.realmNodes === null || this.realmNodes.realm !== this.realmId) {
      const nodes = this.ctx.skin.names.passives.filter((n) => n.realm === this.realmId);
      this.realmNodes = { realm: this.realmId, nodes, links: linkPairs(nodes) };
    }
    return this.realmNodes;
  }

  /** The view of the realm shown, fitted to the canvas when the realm or the canvas's size changes. */
  private panZoom(): PanZoom {
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    const skin = this.ctx.skin;
    if (this.view === null || this.fitted.realm !== this.realmId || this.fitted.w !== w || this.fitted.h !== h) {
      this.view ??= new PanZoom(skin.metric('passive_scale', 26), skin.metric('zoom_min', 0.4), skin.metric('zoom_max', 2.5));
      const b = bounds(this.nodes());
      if (b !== null && w > 10) {
        this.view.fit(b.minX, b.minY, b.maxX, b.maxY, w, h);
      }
      this.fitted = { realm: this.realmId, w, h };
    }
    return this.view;
  }

  private nodeUnder(x: number, y: number): PassiveNode | null {
    const view = this.panZoom();
    const size = this.ctx.skin.metric('passive_node', 14);
    return nodeAt(this.nodes(), (cx, cy) => view.toScreen(cx, cy), (n) => nodeRadius(n.kind, size, view.zoom), x, y);
  }

  /** Draws the canvas at the pixels it covers on screen; nothing while it is not laid out. */
  private draw(): void {
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    if (w <= 0 || h <= 0) {
      return;
    }
    const g = this.canvas.getContext('2d');
    if (g === null) {
      return;
    }
    const r = this.canvas.getBoundingClientRect();
    const scale = drawnScale(r.width, w);
    const ratio = window.devicePixelRatio || 1;
    const [pw, ph] = backingSize(w, h, scale, ratio);
    if (this.canvas.width !== pw || this.canvas.height !== ph) {
      this.canvas.width = pw;
      this.canvas.height = ph;
    }
    this.drawn = { scale, ratio };
    this.dirty = false;
    g.setTransform(1, 0, 0, 1, 0, 0);
    g.clearRect(0, 0, pw, ph);
    g.setTransform(pw / w, 0, 0, ph / h, 0, 0);
    this.paint(g, w, h);
  }

  private paint(g: CanvasRenderingContext2D, w: number, h: number): void {
    const skin = this.ctx.skin;
    const view = this.panZoom();
    const { nodes, links } = this.constellation();
    const allocated = new Set(this.ctx.hero.allocated);
    const accent = skin.surface('accent');
    g.fillStyle = skin.s('well', 0.55);
    g.fillRect(0, 0, w, h);
    // faint concentric guides in the etched style
    const [ox, oy] = view.toScreen(0, 0);
    g.strokeStyle = skin.s('etch', 0.06);
    g.lineWidth = 1;
    for (let k = 1; k <= 4; k++) {
      g.beginPath();
      g.arc(ox, oy, k * 8 * view.scale * view.zoom, 0, TAU);
      g.stroke();
    }
    // the links: dim ones, then the lit ones between two allocated nodes over them
    const path = (pairs: ReadonlyArray<[PassiveNode, PassiveNode]>, color: string, width: number) => {
      g.beginPath();
      for (const [a, b] of pairs) {
        const [ax, ay] = view.toScreen(a.x, a.y);
        const [bx, by] = view.toScreen(b.x, b.y);
        g.moveTo(ax, ay);
        g.lineTo(bx, by);
      }
      g.strokeStyle = color;
      g.lineWidth = width;
      g.stroke();
    };
    const lit = links.filter(([a, b]) => allocated.has(a.id) && allocated.has(b.id));
    path(links.filter(([a, b]) => !(allocated.has(a.id) && allocated.has(b.id))), skin.s('ink_muted', 0.45), 1.5);
    path(lit, css(accent), 3);
    // the nodes
    const reach = reachable(nodes, allocated);
    const size = skin.metric('passive_node', 14);
    const body = skin.fontSize('body');
    const paneLight = skin.theme.surface.pane_light !== undefined ? skin.s('pane_light') : skin.s('inset');
    const inset = skin.s('inset');
    const edgeDark = skin.s('edge_dark');
    const rimLit = lighten(accent, 0.3);
    const ink = skin.surface('ink');
    const edge = skin.surface('edge');
    const bad = skin.s('bad');
    const label = skin.s('label');
    const disc = (x: number, y: number, r: number, color: string) => {
      g.beginPath();
      g.arc(x, y, r, 0, TAU);
      g.fillStyle = color;
      g.fill();
    };
    const ring = (x: number, y: number, r: number, color: string, width: number) => {
      g.beginPath();
      g.arc(x, y, r, 0, TAU);
      g.strokeStyle = color;
      g.lineWidth = width;
      g.stroke();
    };
    g.lineJoin = 'round';
    g.font = skin.canvasFont('body', Math.max(10, body - 3));
    g.textAlign = 'center';
    g.textBaseline = 'alphabetic';
    for (const n of nodes) {
      const [x, y] = view.toScreen(n.x, n.y);
      if (x < -40 || y < -40 || x > w + 40 || y > h + 40) {
        continue;
      }
      const r = nodeRadius(n.kind, size, view.zoom);
      const on = allocated.has(n.id);
      const near = reach.has(n.id);
      const fill = on ? css(accent) : near ? paneLight : inset;
      const rim = on ? rimLit : near ? ink : edge;
      if (n.kind === 'keystone' || n.kind === 'attribute') {
        g.beginPath();
        if (n.kind === 'keystone') {
          for (let i = 0; i < 6; i++) {
            const a = (TAU * i) / 6 - Math.PI / 2;
            g.lineTo(x + Math.cos(a) * r, y + Math.sin(a) * r);
          }
        } else {
          g.moveTo(x, y - r);
          g.lineTo(x + r, y);
          g.lineTo(x, y + r);
          g.lineTo(x - r, y);
        }
        g.closePath();
        g.fillStyle = fill;
        g.fill();
        g.strokeStyle = css(rim);
        g.lineWidth = n.kind === 'keystone' ? 2 : 1.5;
        g.stroke();
      } else {
        disc(x, y, r + 1.5, edgeDark);
        disc(x, y, r, fill);
        ring(x, y, r, css(rim), isGrand(n.kind) ? 2.5 : 1.5);
        if (isGrand(n.kind)) {
          ring(x, y, r + 4, css(rim, 0.5), 1);
        }
      }
      if (n.choice !== null) {
        ring(x, y, r + 7, css(accent, 0.6), 1);
      }
      if (this.refunding && on) {
        ring(x, y, r + 5, bad, 2);
      }
      if (this.hover?.id === n.id) {
        ring(x, y, r + 3, 'rgba(255, 255, 255, 0.8)', 1.5);
      }
      if (named(n.kind, view.zoom)) {
        const ty = y + r + body;
        g.lineWidth = 3;
        g.strokeStyle = 'rgba(0, 0, 0, 0.85)';
        g.strokeText(n.name, x, ty);
        g.fillStyle = label;
        g.fillText(n.name, x, ty);
      }
    }
  }
}

/** The searchable popup of a choice node's options. */
class ChoicePopup {
  readonly root: HTMLElement;
  private readonly search: HTMLInputElement;
  private readonly list: HTMLElement;
  private node: PassiveNode | null = null;
  private group: ChoiceGroup | null = null;

  constructor(private readonly picked: (node: PassiveNode, option: string) => void) {
    this.search = el('input', { class: 'choice-search', type: 'search', placeholder: 'Search the options', autocomplete: 'off', spellcheck: 'false' });
    this.search.addEventListener('input', () => this.fill());
    // Keys go to the field while it has focus; Escape closes the popup from there.
    this.search.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        e.stopPropagation();
        this.hide();
      }
    });
    this.list = el('div', { class: 'choice-list' });
    this.root = el('div', { class: 'choice-popup', hidden: '' }, this.search, this.list, button('Cancel', () => this.hide(), { className: 'choice-cancel' }));
  }

  get isOpen(): boolean {
    return !this.root.hidden;
  }

  /** Opens at a point in the page's own pixels, held inside the page. */
  open(node: PassiveNode, group: ChoiceGroup, x: number, y: number, areaW: number, areaH: number): void {
    this.node = node;
    this.group = group;
    this.search.value = '';
    this.root.dataset.node = node.id;
    this.fill();
    this.root.hidden = false;
    const [left, top] = clampBox(x, y, this.root.offsetWidth, this.root.offsetHeight, areaW, areaH);
    this.root.style.left = `${left}px`;
    this.root.style.top = `${top}px`;
    this.search.focus({ preventScroll: true });
  }

  hide(): void {
    if (this.root.hidden) {
      return;
    }
    this.root.hidden = true;
    this.blur();
  }

  /** Lets go of the keyboard if the search field holds it. */
  blur(): void {
    if (document.activeElement === this.search) {
      this.search.blur();
    }
  }

  private fill(): void {
    const node = this.node;
    const group = this.group;
    if (node === null || group === null) {
      this.list.replaceChildren();
      return;
    }
    const query = this.search.value;
    this.list.replaceChildren(
      ...group.options
        .filter((o) => optionMatches(o, query))
        .map((o) => {
          const b = button(o.name, () => this.picked(node, o.id), { className: 'choice-option', title: o.lines.join('\n') });
          b.dataset.option = o.id;
          return b;
        }),
    );
  }
}
