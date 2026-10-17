import type {
  ContainerInfo,
  DialogueStateMsg,
  EntityState,
  EquipSlotInfo,
  Int64,
  InventoryStateMsg,
  ItemInfo,
  Message,
  PageStateMsg,
  PassiveStateMsg,
  QuestOffersMsg,
  SelfStateMsg,
  StationStateMsg,
  VendorStateMsg,
  WeaveSlotsMsg,
} from '../net/protocol';
import { BookView } from './bookView';
import { ClusterView } from './cluster';
import { CorePane, RailView } from './core';
import { el, storageGet, storageSet } from './dom';
import { CarryGhost, ItemCard, QuickMenu } from './items';
import type { Carry, CarrySource, DropTarget, ItemHost, LeafContext, LeafView, MenuAction } from './leaf';
import type { Leaf } from './logic/book';
import { BookRule } from './logic/book';
import { corePage, humanize, isCore, onRibbon, pageById, rankOf, withLook, type ContentNames, type UiContent, type UiPage } from './logic/content';
import { HeroState, seatEmpty } from './logic/heroState';
import { centered, paneLayout, seatOf, type Box, type Seat, type WorkspaceRects } from './logic/layout';
import { foldTray, keyLabelOf, servedPages, type TrayRow } from './logic/menu';
import { OptionsState, type OptionEntry, type Settings } from './logic/options';
import { Workspace } from './logic/workspace';
import { ChatPane, NetOverlay, Notices, TargetPlate } from './overlay';
import { CharacterPage } from './pages/character';
import { ContainerPage } from './pages/container';
import { PassivesPage } from './pages/passives';
import { WeavesPage } from './pages/weaves';
import { OptionsPane } from './panes/options';
import { DialoguePane, QuestOfferPane, StationPane, VendorPane } from './panes/world';
import { css, lighten, Skin } from './skin';
import './interface.css';

const STORAGE_SEAT = 'hollowwake.interface.seat';

interface ChatLine {
  text: string;
  kind: string;
  from?: string;
  color?: string;
}

/**
 * The interface of docs/design/ui.md in the browser: the cluster at the bottom centre, the target plate, notices
 * and combat text, the chat in the lower left corner, and the workspace: the core pane (the inventory) with its
 * ribbon rail, the development pages docked beside it, the panes the world opens, bound into books by the book
 * rule, and the Options pane. It draws what the server sent and raises intents through send; it never folds a
 * number. Its look comes from the theme content and the player's options; the model (the hero's states, the
 * workspace, the chat, the world panes' last states) outlives its views, so a theme change rebuilds the views in
 * place. The desktop client's UI/Hud.cs.
 */
export class Hud implements ItemHost {
  readonly root: HTMLElement;
  readonly hero = new HeroState();
  readonly workspace: Workspace;
  private readonly optionsState = new OptionsState();
  private readonly views = new Map<string, LeafView>();
  private readonly worldMessages = new Map<string, Message>();
  private readonly quietClose = new Set<string>();
  private readonly chatLines: ChatLine[] = [];
  private skinNow: Skin;
  private cluster!: ClusterView;
  private plate!: TargetPlate;
  private notices!: Notices;
  private chatPane!: ChatPane;
  private net!: NetOverlay;
  private core!: CorePane;
  private pageBook!: BookView;
  private worldBook!: BookView;
  private card!: ItemCard;
  private ghost!: CarryGhost;
  private quick!: QuickMenu;
  private devRects!: HTMLElement;
  private context!: LeafContext;
  private carry: Carry | null = null;
  private carryMoved = false;
  private hoverTarget: DropTarget | null = null;
  private seat: Seat | null = null;
  private now = 0;
  private rects: WorkspaceRects | null = null;
  private rows: TrayRow[] = [];
  private target: EntityState | null = null;
  private lastNotice: { text: string; level: string } | null = null;
  private restyle = false;
  private viewW = 1;
  private viewH = 1;
  private pointer = { x: 0, y: 0 };

  /** The cluster, the chat and the plate show only in the world; the workspace (Options) shows anywhere. */
  inWorld = false;
  /** The scale the interface draws at (the interface scale option). */
  uiScale = 1;
  /** A line for the connection overlay, set by the client. */
  netText = '';
  /** Every intent the interface raises; the client sends it. */
  send: (message: Message) => void = () => {};
  /** A weave slot clicked on the bar; the client casts it as its key would. */
  slotPressed: (slot: number) => void = () => {};
  /** The Options pane closed: entries that apply on close take effect. */
  optionsClosed: () => void = () => {};
  /** The hero's head on screen this frame, in client pixels, for combat text; null when not on screen. */
  heroScreen: () => { x: number; y: number } | null = () => null;
  /** The ground distance from the hero to an entity, or null when the client does not know it. */
  distanceTo: ((key: Int64) => number | null) | null = null;
  /** Whether the hero is casting (Escape cancels a cast before it opens Options). */
  casting: () => boolean = () => false;
  /** The interface was drawn again in another look; surfaces outside it (the login panel) follow. */
  restyled: (skin: Skin) => void = () => {};

  constructor(
    private readonly baseUi: UiContent,
    readonly names: ContentNames,
    readonly settings: Settings,
    private readonly facts: (name: string) => number | null | undefined,
  ) {
    this.skinNow = this.makeSkin();
    this.workspace = new Workspace((id) => this.allPages().find((p) => p.id === id), (kind) => rankOf(this.skinNow.ui.layout, kind));
    this.workspace.onWorldClosed((leaf) => this.onWorldClosed(leaf));
    this.settings.onChange((entry) => this.onSettingChanged(entry));
    this.root = el('div', { class: 'ui-root', id: 'interface' });
    this.loadSeat();
    this.buildViews();
    window.addEventListener('pointermove', (e) => this.pointerMove(e), true);
    window.addEventListener('pointerdown', (e) => this.pointerDown(e), true);
    window.addEventListener('pointerup', (e) => this.pointerUp(e), true);
  }

  get skin(): Skin {
    return this.skinNow;
  }

  get ui(): UiContent {
    return this.skinNow.ui;
  }

  private makeSkin(): Skin {
    return new Skin(withLook(this.baseUi, this.settings.text('theme'), this.settings.text('icon_set')), this.names);
  }

  private onSettingChanged(entry: OptionEntry): void {
    if (entry.id === 'theme' || entry.id === 'icon_set') {
      // Restyled on the next frame, never inside the control's own event.
      this.restyle = true;
    }
    if (entry.id === 'chat_lines') {
      this.chatPane.cap = this.settings.number('chat_lines', 200);
    }
  }

  // ------------------------------------------------------------ views

  private buildViews(): void {
    const skin = this.skinNow;
    skin.apply(this.root);
    const self = this;
    this.context = {
      get skin() {
        return self.skinNow;
      },
      hero: this.hero,
      items: this,
      settings: this.settings,
      send: (m) => this.send(m),
      menu: (x, y, actions) => this.openMenu(x, y, actions),
      notify: (text, level) => this.notice(text, level),
      get now() {
        return self.now;
      },
    };
    this.chatPane = new ChatPane(skin, (text) => this.send({ t: 'chat', channel: 'zone', text }));
    this.chatPane.cap = this.settings.number('chat_lines', 200);
    this.cluster = new ClusterView(skin, {
      slot: (slot) => this.slotPressed(slot),
      menu: () => {
        this.workspace.trayOpen = !this.workspace.trayOpen;
      },
      pick: (id) => {
        this.workspace.trayOpen = false;
        this.workspace.requestPage(id, 'menu');
      },
    });
    this.plate = new TargetPlate(skin);
    this.notices = new Notices(skin);
    this.net = new NetOverlay();
    this.pageBook = new BookView(skin, 'hud-page-book', (id) => this.workspace.selectTab(id), (id) => this.workspace.closeLeaf(id));
    this.worldBook = new BookView(skin, 'hud-world-book', (id) => this.workspace.selectTab(id), (id) => this.workspace.closeLeaf(id));
    this.core = new CorePane(skin, this, corePage(this.ui)?.name ?? 'Inventory', {
      close: () => this.workspace.closeCore(),
      ribbon: (id) => this.workspace.requestPage(id, 'ribbon'),
      drag: (dx, dy) => this.moveSeat(dx, dy),
      dragEnd: () => this.saveSeat(),
    });
    this.card = new ItemCard(skin);
    this.ghost = new CarryGhost(skin);
    this.quick = new QuickMenu();
    // Outlines of the published cluster rectangle and the workspace's seats (the developer option).
    this.devRects = el('div', { class: 'layer dev-rects', hidden: '' },
      el('div', { class: 'dev-rect cluster-rect' }),
      el('div', { class: 'dev-rect core-rect' }),
      el('div', { class: 'dev-rect page-rect' }),
      el('div', { class: 'dev-rect world-rect' }));
    this.root.replaceChildren(
      this.devRects,
      this.chatPane.root,
      this.cluster.root,
      this.notices.root,
      this.plate.root,
      this.net.root,
      this.core.root,
      this.pageBook.root,
      this.worldBook.root,
      this.card.root,
      this.ghost.root,
      this.quick.root,
    );
    // The model outlives the views: the chat and the last notice come back.
    for (const line of this.chatLines) {
      this.chatPane.add(line.text, line.kind, line.from, line.color);
    }
    if (this.lastNotice !== null) {
      this.notices.show(this.lastNotice.text, this.lastNotice.level, this.now);
    }
    this.restyled(skin);
  }

  /** Draws the interface again with the theme and icon set the options name, keeping everything open. */
  private rebuild(): void {
    this.cancelCarry();
    for (const view of this.views.values()) {
      view.dispose();
    }
    this.views.clear();
    this.skinNow = this.makeSkin();
    this.buildViews();
  }

  // ------------------------------------------------------------ messages in

  applySelf(self: SelfStateMsg): void {
    this.hero.applySelf(self, this.clock());
  }

  applySlots(slots: WeaveSlotsMsg): void {
    this.hero.applySlots(slots, this.clock());
  }

  applyInventory(inventory: InventoryStateMsg): void {
    this.hero.applyInventory(inventory);
  }

  applyPassives(passives: PassiveStateMsg): void {
    this.hero.applyPassives(passives);
  }

  applyPages(pages: PageStateMsg): void {
    this.hero.applyPages(pages);
  }

  setSelfEntity(state: EntityState | null): void {
    this.hero.entity = state;
  }

  /** The hovered entity, for the target line and plate. */
  setTarget(target: EntityState | null): void {
    this.target = target;
  }

  get targetLine(): string {
    return this.plate.line;
  }

  private clock(): number {
    return performance.now() / 1000;
  }

  notice(text: string, level: string): void {
    this.lastNotice = { text, level };
    this.notices.show(text, level, this.clock());
    this.line(text, `notice ${level}`, undefined, css(this.skinNow.notice(level)));
  }

  /** One line in the chat log, kept so a rebuilt interface shows it again. */
  line(text: string, kind: string, from?: string, color?: string): void {
    const entry: ChatLine = { text, kind, from, color };
    this.chatLines.push(entry);
    const cap = Math.max(10, this.settings.number('chat_lines', 200));
    while (this.chatLines.length > cap) {
      this.chatLines.shift();
    }
    this.chatPane.add(text, kind, from, color);
  }

  chat(from: string, channel: string, text: string): void {
    this.line(`[${channel}]: ${text}`, 'chat', from);
  }

  /** Combat text above the hero: hits it lands, hits it takes, heals, as the options allow. */
  combatText(kind: string, amount: number, crit: boolean, takenBySelf: boolean): void {
    if (!this.settings.toggle('combat_text', true)) {
      return;
    }
    const text = kind === 'damage' ? String(Math.round(amount)) : kind === 'heal' ? `+${Math.round(amount)}` : kind === 'evade' ? 'evaded' : kind === 'block' ? 'blocked' : '';
    if (text.length === 0) {
      return;
    }
    const big = crit && this.settings.toggle('combat_text_crits', true);
    const s = this.skinNow;
    const color = kind === 'heal' ? css(lighten(s.fill('stamina'), 0.2))
      : takenBySelf ? css(lighten(s.fill('life'), 0.25))
      : big ? css(lighten(s.surface('accent'), 0.2))
      : '#ffffff';
    this.notices.float(big ? `${text}!` : text, color, this.clock(), big);
  }

  showQuestOffers(offers: QuestOffersMsg): void {
    const id = `quest:${offers.giver}`;
    this.worldMessages.set(id, offers);
    (this.viewFor(id, 'quest_offer') as QuestOfferPane).show(offers);
    this.workspace.openWorld(id, 'quest_offer', 'Quests', offers.giver);
  }

  showDialogue(state: DialogueStateMsg): void {
    const id = `dialogue:${state.source}`;
    if (state.ended) {
      this.closeQuietly(id);
      return;
    }
    this.worldMessages.set(id, state);
    (this.viewFor(id, 'dialogue') as DialoguePane).show(state);
    this.workspace.openWorld(id, 'dialogue', state.speaker, state.source, state.reach ?? 0);
  }

  showVendor(state: VendorStateMsg): void {
    const id = `vendor:${state.source}`;
    if (state.closed) {
      this.closeQuietly(id);
      return;
    }
    this.worldMessages.set(id, state);
    (this.viewFor(id, 'vendor') as VendorPane).show(state);
    this.workspace.openWorld(id, 'vendor', state.name, state.source, state.reach ?? 0);
  }

  showStation(state: StationStateMsg): void {
    const id = `station:${state.source}`;
    if (state.closed) {
      this.closeQuietly(id);
      return;
    }
    this.worldMessages.set(id, state);
    (this.viewFor(id, 'station') as StationPane).show(state);
    this.workspace.openWorld(id, 'station', state.name, state.source, state.reach ?? 0);
  }

  /** Opens the Options pane on its own (from the login screen as well as in play). */
  openOptions(): void {
    if (!this.workspace.book.has('options')) {
      this.workspace.requestPage('options', 'show');
    } else {
      this.workspace.selectTab('options');
    }
  }

  get optionsOpen(): boolean {
    return this.workspace.book.has('options');
  }

  private closeQuietly(id: string): void {
    this.worldMessages.delete(id);
    if (this.workspace.hasWorld(id)) {
      this.quietClose.add(id);
      this.workspace.closeWorld(id);
    }
  }

  private onWorldClosed(leaf: Leaf): void {
    const quiet = this.quietClose.delete(leaf.id);
    const message = this.worldMessages.get(leaf.id);
    this.worldMessages.delete(leaf.id);
    if (!quiet && message !== undefined) {
      switch (message.t) {
        case 'dialogue_state':
          this.send({ t: 'close_dialogue', conversation: message.conversation });
          break;
        case 'vendor_state':
          this.send({ t: 'close_pane', pane: 'vendor', source: message.source });
          break;
        case 'station_state':
          this.send({ t: 'close_pane', pane: 'station', source: message.source });
          break;
        default:
          break;
      }
    }
    const view = this.views.get(leaf.id);
    if (view !== undefined) {
      this.views.delete(leaf.id);
      if (view instanceof OptionsPane) {
        view.confirm();
        // The pane reopens on the page it was on, with an empty search.
        this.optionsState.search = '';
        this.optionsClosed();
      }
      this.pageBook.forget(view);
      this.worldBook.forget(view);
      view.dispose();
      view.root.remove();
    }
  }

  // ------------------------------------------------------------ the leaves

  private viewFor(id: string, kind: string): LeafView {
    const existing = this.views.get(id);
    if (existing !== undefined) {
      return existing;
    }
    let view: LeafView;
    switch (kind) {
      case 'weaves':
        view = new WeavesPage(this.context);
        break;
      case 'passives':
        view = new PassivesPage(this.context);
        break;
      case 'character':
        view = new CharacterPage(this.context);
        break;
      case 'options':
        view = new OptionsPane(this.context, this.optionsState, this.facts, () => this.workspace.closeWorld(id));
        break;
      case 'quest_offer':
        view = new QuestOfferPane(this.context, () => this.workspace.closeWorld(id));
        break;
      case 'dialogue':
        view = new DialoguePane(this.context);
        break;
      case 'vendor':
        view = new VendorPane(this.context);
        break;
      case 'station':
        view = new StationPane(this.context);
        break;
      default:
        view = this.containerPageFor(id);
        break;
    }
    // A world pane rebuilt after a restyle shows the last state it had.
    const last = this.worldMessages.get(id);
    if (last !== undefined) {
      if (view instanceof QuestOfferPane && last.t === 'quest_offers') {
        view.show(last);
      } else if (view instanceof DialoguePane && last.t === 'dialogue_state') {
        view.show(last);
      } else if (view instanceof VendorPane && last.t === 'vendor_state') {
        view.show(last);
      } else if (view instanceof StationPane && last.t === 'station_state') {
        view.show(last);
      }
    }
    this.views.set(id, view);
    return view;
  }

  /**
   * A container page: a content page naming a container kind shows every container of that kind the inventory
   * state sends (the reliquary, each stash's pages); a page only the page state names shows the container it
   * names and that container's pages.
   */
  private containerPageFor(pageId: string): LeafView {
    const page = pageById(this.ui, pageId);
    if (page !== undefined && page.container !== null) {
      const kind = page.container;
      return new ContainerPage(this.context, (c: ContainerInfo) => c.kind === kind, `No ${page.name.toLowerCase()} has opened to you.`);
    }
    const container = this.hero.pages?.pages.find((p) => p.id === pageId)?.container ?? pageId;
    return new ContainerPage(this.context, (c: ContainerInfo) => c.id === container || c.of === container, 'Nothing is stored here.');
  }

  private viewOf(leaf: Leaf): LeafView {
    return this.viewFor(leaf.id, leaf.kind);
  }

  /** The pages the menu and the rail list: content pages, then container pages the page state names. */
  allPages(): UiPage[] {
    const pages = [...this.ui.pages];
    for (const p of this.hero.pages?.pages ?? []) {
      if (p.container && !pages.some((x) => x.id === p.id)) {
        const container = p.container;
        const name = this.hero.inventory?.containers.find((c) => c.id === container || c.of === container)?.name ?? humanize(p.id);
        pages.push({ id: p.id, name, action: '', icon: 'chest', order: 100 + pages.length, place: 'ribbon', description: name, container: null });
      }
    }
    return pages;
  }

  private served() {
    const pages = this.hero.pages;
    if (pages === null) {
      return null;
    }
    // A container page arrives once per container: it is open when any of them is in reach.
    return servedPages(this.ui.pages.map((p) => p.id), pages.pages.map((p) => ({ id: p.id, sealed: p.sealed, reason: p.reason ?? null, pips: p.pips })));
  }

  /** The label of an action's first binding, as the tray and the rail show it. */
  keyLabelOf(action: string): string {
    return keyLabelOf(this.settings.bindings(action).primary);
  }

  // ------------------------------------------------------------ the frame

  /** Lays the interface out for this frame in a window of the given size (client pixels). */
  frame(now: number, windowW: number, windowH: number): void {
    if (this.restyle) {
      this.restyle = false;
      this.rebuild();
    }
    this.now = now;
    const reduced = this.settings.toggle('reduce_motion');
    this.root.classList.toggle('reduce-motion', reduced);
    this.uiScale = Math.min(2, Math.max(0.5, this.settings.number('ui_scale', 1)));
    this.viewW = windowW / this.uiScale;
    this.viewH = windowH / this.uiScale;
    this.root.style.transform = `scale(${this.uiScale})`;
    this.root.style.width = `${this.viewW}px`;
    this.root.style.height = `${this.viewH}px`;
    if (this.viewW < 10 || this.viewH < 10) {
      return;
    }
    const pages = this.allPages();
    this.rows = foldTray(
      pages,
      this.served(),
      (id) => (id === 'passives' ? this.hero.points('passive') : 0),
      (id) => this.workspace.isShown(id) || this.workspace.book.isDrawn(id) || (id === corePage(this.ui)?.id && this.workspace.coreOpen),
      (page) => this.keyLabelOf(page.action),
    );
    this.cluster.root.hidden = !this.inWorld;
    this.chatPane.root.hidden = !this.inWorld;
    this.notices.root.hidden = !this.inWorld;
    this.cluster.update(this.viewW, this.viewH, this.hero, now, this.rows, this.workspace.trayOpen, {
      menu: this.keyLabelOf('ui_menu_tray'),
      slot: (i) => this.keyLabelOf(`slot_${i + 1}`),
    }, (window.devicePixelRatio || 1) * this.uiScale, reduced);
    this.notices.seconds = this.settings.number('notice_seconds', 4);
    const head = this.heroScreen();
    this.notices.update(this.viewW, head === null ? null : { x: head.x / this.uiScale, y: head.y / this.uiScale }, now);
    const scale = this.cluster.layout?.scale ?? 1;
    this.chatPane.place(this.viewW, this.viewH, this.cluster.drawn, scale);
    this.plate.show(this.inWorld ? this.target : null, this.viewW, scale);
    this.net.show(this.netText, this.settings.toggle('net_overlay'));
    // Departure: a world pane closes when the hero walks out of reach of what opened it.
    if (this.distanceTo !== null && this.inWorld) {
      this.workspace.depart(this.distanceTo, this.skinNow.metric('departure_reach', 6));
    }
    this.layoutWorkspace();
    const cell = this.skinNow.metric('cell', 36) * scale;
    this.ghost.follow(this.carryMoved || this.carry?.lifted ? this.carry : null, this.pointer.x / this.uiScale, this.pointer.y / this.uiScale, cell);
    this.drawDevRects();
  }

  private drawDevRects(): void {
    const on = this.settings.toggle('dev_rects');
    this.devRects.hidden = !on;
    if (!on) {
      return;
    }
    const empty: Box = { x: 0, y: 0, w: 0, h: 0 };
    const boxes = [this.cluster.drawn, this.rects?.core ?? empty, this.rects?.pageBook ?? empty, this.rects?.worldBook ?? empty];
    [...this.devRects.children].forEach((node, i) => {
      const b = boxes[i];
      const el2 = node as HTMLElement;
      el2.hidden = b.w <= 0 || b.h <= 0;
      el2.style.left = `${b.x}px`;
      el2.style.top = `${b.y}px`;
      el2.style.width = `${b.w}px`;
      el2.style.height = `${b.h}px`;
    });
  }

  private layoutWorkspace(): void {
    const book = this.workspace.book;
    const s = this.skinNow;
    const viewBox: Box = { x: 0, y: 0, w: this.viewW, h: this.viewH };
    const clusterBox = this.inWorld ? this.cluster.drawn : { x: 0, y: 0, w: 0, h: 0 };
    if (!this.workspace.coreOpen) {
      this.core.hide();
      this.pageBook.hide();
      // A standalone pane (Options) stands centred above the cluster.
      const alone = book.within('world').filter((l) => l.standalone);
      const front = BookRule.frontOf(alone);
      if (front !== undefined) {
        const view = this.viewOf(front);
        const box = centered(viewBox, clusterBox, view.preferredWidth, s.metric('options_height', 640), s.metric('edge', 14));
        this.worldBook.show(box, alone, front, view, true);
        view.refresh();
        this.rects = { core: { x: 0, y: 0, w: 0, h: 0 }, pageBook: { x: 0, y: 0, w: 0, h: 0 }, worldBook: box, merged: false, pagesLeft: true, railBadges: false, railChips: false };
      } else {
        this.worldBook.hide();
        this.rects = null;
      }
      return;
    }
    const scale = Math.min(1, (this.cluster.layout?.scale ?? 1) * 1.1);
    const desired = this.core.desired(this.hero, scale);
    const pageFront = book.frontWithin('page');
    const worldFront = book.frontWithin('world');
    const pageWidth = pageFront === undefined ? s.metric('page_width', 460) : this.viewOf(pageFront).preferredWidth;
    const worldWidth = worldFront === undefined ? s.metric('page_width', 460) : this.viewOf(worldFront).preferredWidth;
    const ribbons = this.rows.filter((r) => onRibbon(r.page) && r.state !== 'hidden').length;
    const wallet = this.hero.wallet((id) => this.names.currencies.get(id)?.name ?? humanize(id));
    const fixed = this.settings.text('panel_layout') === 'fixed';
    const rects = paneLayout({
      viewport: viewBox,
      cluster: clusterBox,
      seat: fixed ? null : this.seat,
      coreWidth: desired.w,
      coreHeight: desired.h,
      pageWidth,
      worldWidth,
      edge: s.metric('edge', 14),
      hasPages: book.within('page').length > 0,
      hasWorld: book.within('world').length > 0,
      ribbons,
      ribbonHeight: RailView.ribbonHeight(s),
      badgeHeight: RailView.badgeHeight,
      chips: wallet.length,
      chipHeight: RailView.chipHeight,
      railTop: RailView.top,
      minWidth: s.metric('pane_min_width', 340),
    });
    this.rects = rects;
    book.merged = rects.merged;
    this.core.draggable = !fixed;
    const slots: EquipSlotInfo[] = this.hero.inventory?.slots ?? this.names.slots.map((x) => ({ id: x.id, name: x.name, order: x.order }));
    this.core.show(rects.core, rects.pagesLeft, this.hero, slots, this.rows, wallet, rects.railBadges, rects.railChips, scale);
    if (rects.merged) {
      this.showBook(this.pageBook, rects.pageBook, book.leaves, book.front('page'));
      this.worldBook.hide();
    } else {
      this.showBook(this.pageBook, rects.pageBook, book.within('page'), pageFront);
      this.showBook(this.worldBook, rects.worldBook, book.within('world'), worldFront);
    }
  }

  private showBook(view: BookView, box: Box, leaves: readonly Leaf[], front: Leaf | undefined): void {
    if (front === undefined || leaves.length === 0 || box.w <= 0 || box.h <= 0) {
      view.hide();
      return;
    }
    const leafView = this.viewOf(front);
    view.show(box, leaves, front, leafView, front.group === 'world');
    leafView.refresh();
  }

  // ------------------------------------------------------------ the core pane's seat

  private moveSeat(dx: number, dy: number): void {
    const r = this.rects;
    if (r === null || this.settings.text('panel_layout') === 'fixed') {
      return;
    }
    const view: Box = { x: 0, y: 0, w: this.viewW, h: this.viewH };
    this.seat = seatOf({ ...r.core, x: r.core.x + dx / this.uiScale, y: r.core.y + dy / this.uiScale }, view);
  }

  private saveSeat(): void {
    if (this.seat !== null) {
      storageSet(STORAGE_SEAT, JSON.stringify(this.seat));
    }
  }

  private loadSeat(): void {
    try {
      const raw = storageGet(STORAGE_SEAT);
      if (raw !== null) {
        const s = JSON.parse(raw) as Seat;
        if (typeof s.fx === 'number' && typeof s.fy === 'number') {
          this.seat = s;
        }
      }
    } catch {
      this.seat = null;
    }
  }

  /** Seats the core pane for this session without remembering it (scripted shots). */
  useSeat(seat: Seat | null): void {
    this.seat = seat;
  }

  // ------------------------------------------------------------ keys

  get chatFocused(): boolean {
    return this.chatPane.focused;
  }

  focusChat(): void {
    this.chatPane.focus();
  }

  /**
   * The interface's actions, before the world's: Escape drops a carried item, folds the quick menu, lets the front
   * pane use it (Options ends a capture or undoes changes), closes the front pane and the core pane last, and with
   * nothing open opens Options unless a cast is running (the world then cancels it); Tab walks a book of two or
   * more or opens the tray; page actions request their pages. True when the interface took the press.
   */
  press(actions: readonly string[]): boolean {
    const has = (a: string) => actions.includes(a);
    if (has('cancel')) {
      if (this.carry !== null) {
        this.cancelCarry();
        return true;
      }
      if (this.quick.open) {
        this.quick.hide();
        return true;
      }
      const front = this.workspace.book.active();
      if (!this.workspace.trayOpen && front !== undefined && this.views.get(front.id)?.onEscape() === true) {
        return true;
      }
      if (this.workspace.escape() !== 'nothing') {
        return true;
      }
      if (this.casting()) {
        return false;
      }
      this.openOptions();
      return true;
    }
    if (this.workspace.book.canWalk) {
      if (has('ui_book_previous')) {
        this.workspace.book.walk(-1);
        return true;
      }
      if (has('ui_book_next')) {
        this.workspace.book.walk(1);
        return true;
      }
    }
    if (has('ui_menu_tray')) {
      this.workspace.trayOpen = !this.workspace.trayOpen;
      return true;
    }
    for (const page of this.allPages()) {
      if (page.action.length > 0 && has(page.action)) {
        const row = this.rows.find((r) => r.page.id === page.id);
        if (row === undefined || row.state === 'open') {
          this.workspace.requestPage(page.id, 'key');
        } else if (row.state === 'sealed') {
          this.notice(`${page.name}: ${row.reason ?? 'sealed here'}`, 'error');
        }
        return true;
      }
    }
    return false;
  }

  // ------------------------------------------------------------ items: hover, carry, quick actions

  get carrying(): Carry | null {
    return this.carry !== null && (this.carryMoved || this.carry.lifted) ? this.carry : null;
  }

  hoverItem(item: ItemInfo | null, x: number, y: number, container: string): void {
    if (item === null) {
      this.card.hide();
      return;
    }
    let worn: ItemInfo | null = null;
    if (container !== 'equipment' && item.slot && this.settings.toggle('compare_worn', true)) {
      worn = this.hero.equipment[item.slot] ?? null;
    }
    const extra: string[] = [];
    if (container === 'bag') {
      for (const view of this.views.values()) {
        if (view instanceof VendorPane) {
          const quote = view.quoteFor(item.uid);
          if (quote !== null) {
            extra.push(quote);
          }
        }
      }
    }
    const seats = this.hero.seats(this.skinNow.metric('slots', 8));
    this.card.show(item, x / this.uiScale, y / this.uiScale, this.viewW, this.viewH, worn, extra, (i) => (i < seats.length && !seatEmpty(seats[i]) ? seats[i].name : `Seat ${i + 1}`));
  }

  pressItem(item: ItemInfo, from: CarrySource, x: number, y: number): void {
    this.carry = { item, from, w: Math.max(1, item.w ?? 1), h: Math.max(1, item.h ?? 1), lifted: false, pressed: true, pressX: x, pressY: y };
    this.carryMoved = false;
    this.card.hide();
  }

  quickActions(item: ItemInfo, container: string, x: number, y: number): void {
    const actions: MenuAction[] = [];
    if (container === 'equipment') {
      if (item.slot) {
        const worn = item.slot;
        actions.push({ label: 'Unequip', act: () => this.send({ t: 'unequip', slot: worn }) });
      }
    } else if (container === 'bag') {
      if (item.slot) {
        const slot = item.slot;
        actions.push({ label: 'Equip', act: () => this.send({ t: 'equip', item_uid: item.uid, slot }) });
      }
      for (const view of this.views.values()) {
        if (view instanceof VendorPane && view.state !== null) {
          const quote = view.quoteFor(item.uid);
          const v = view.state;
          if (quote !== null) {
            actions.push({ label: `Sell to ${v.name} (${quote.replace('Sells for ', '')})`, act: () => this.send({ t: 'sell', vendor: v.source, item: item.uid }) });
          }
        }
      }
    }
    if ((container === 'bag' || container === 'equipment') && item.locked !== true) {
      actions.push({ label: 'Drop', act: () => this.send({ t: 'move_item', item: item.uid, container: 'world', x: 0, y: 0 }) });
    }
    this.openMenu(x, y, actions);
  }

  private openMenu(x: number, y: number, actions: MenuAction[]): void {
    this.quick.show(x / this.uiScale, y / this.uiScale, actions, this.viewW, this.viewH);
  }

  sendMove(uid: Int64, container: string, x: number, y: number, slot?: string | null): void {
    this.send(slot ? { t: 'move_item', item: uid, container, x, y, slot } : { t: 'move_item', item: uid, container, x, y });
  }

  private dropTargets(): DropTarget[] {
    // Front-most first: the books, then the core pane's doll and bag.
    const targets: DropTarget[] = [];
    for (const book of [this.worldBook, this.pageBook]) {
      if (!book.root.hidden && book.shown !== null) {
        targets.push(...book.shown.dropTargets());
      }
    }
    if (!this.core.root.hidden) {
      targets.push(this.core.doll, this.core.bag);
    }
    return targets;
  }

  private cancelCarry(): void {
    this.hoverTarget?.leave();
    this.hoverTarget = null;
    this.carry = null;
    this.carryMoved = false;
  }

  private dropCarry(x: number, y: number): void {
    const carry = this.carry;
    if (carry === null) {
      return;
    }
    for (const target of this.dropTargets()) {
      if (target.covers(x, y)) {
        target.drop(carry, x, y);
        this.cancelCarry();
        return;
      }
    }
    // Over a pane but no target: nothing happens. Over the world: the item is dropped there.
    const under = document.elementFromPoint(x, y);
    const overInterface = under !== null && this.root.contains(under) && under !== this.root && getComputedStyle(under).pointerEvents !== 'none';
    if (!overInterface) {
      this.sendMove(carry.item.uid, 'world', 0, 0);
    }
    this.cancelCarry();
  }

  private pointerMove(e: PointerEvent): void {
    this.pointer = { x: e.clientX, y: e.clientY };
    const carry = this.carry;
    if (carry === null) {
      return;
    }
    if (!this.carryMoved && carry.pressed && Math.hypot(e.clientX - carry.pressX, e.clientY - carry.pressY) > this.skinNow.metric('drag_threshold', 6) * this.uiScale) {
      this.carryMoved = true;
      this.card.hide();
    }
    if (this.carryMoved || carry.lifted) {
      const target = this.dropTargets().find((t) => t.covers(e.clientX, e.clientY)) ?? null;
      if (target !== this.hoverTarget) {
        this.hoverTarget?.leave();
        this.hoverTarget = target;
      }
      target?.hover(carry, e.clientX, e.clientY);
    }
  }

  private pointerUp(e: PointerEvent): void {
    const carry = this.carry;
    if (e.button !== 0 || carry === null || !carry.pressed) {
      return;
    }
    carry.pressed = false;
    if (this.carryMoved) {
      this.dropCarry(e.clientX, e.clientY);
    } else {
      // A click without a drag lifts the item; the next click drops it.
      carry.lifted = true;
    }
    e.stopPropagation();
  }

  private pointerDown(e: PointerEvent): void {
    this.pointer = { x: e.clientX, y: e.clientY };
    if (this.quick.open && !this.quick.root.contains(e.target as Node)) {
      this.quick.hide();
    }
    if (this.carry !== null) {
      if (e.button === 2) {
        this.cancelCarry();
        e.stopPropagation();
        e.preventDefault();
        return;
      }
      if (e.button === 0 && this.carry.lifted) {
        this.dropCarry(e.clientX, e.clientY);
        e.stopPropagation();
        e.preventDefault();
        return;
      }
    }
    if (this.workspace.trayOpen) {
      const onMenu = (e.target as HTMLElement | null)?.closest('#hud-menu') !== null;
      if (!this.cluster.tray.covers(e.clientX, e.clientY) && !onMenu) {
        this.workspace.trayOpen = false;
      }
    }
  }

  // ------------------------------------------------------------ for scripted checks

  get coreOpen(): boolean {
    return this.workspace.coreOpen;
  }

  isShown(pageId: string): boolean {
    return this.workspace.isShown(pageId);
  }

  get sheetVisible(): boolean {
    return this.workspace.isShown('character');
  }

  get questsVisible(): boolean {
    return this.workspace.book.within('world').some((l) => l.kind === 'quest_offer');
  }

  get trayRows(): readonly TrayRow[] {
    return this.rows;
  }

  get ribbonPages(): string[] {
    return this.rows.filter((r) => onRibbon(r.page) && r.state === 'open').map((r) => r.page.id);
  }

  hasWorldPane(kind: string): boolean {
    return this.workspace.book.within('world').some((l) => l.kind === kind);
  }

  frontView<T extends LeafView>(type: abstract new (...args: never[]) => T): T | null {
    for (const view of this.views.values()) {
      if (view instanceof type) {
        return view;
      }
    }
    return null;
  }

  get clusterRect(): Box {
    return this.cluster.drawn;
  }

  get workspaceRects(): WorkspaceRects | null {
    return this.rects;
  }

  get bagItems(): number {
    return this.hero.bag(this.skinNow.metric('bag_columns', 10))?.items.length ?? 0;
  }

  /** Closes the quest offer through its own close path, for scripted checks. */
  hideQuests(): void {
    for (const leaf of this.workspace.book.within('world').filter((l) => l.kind === 'quest_offer')) {
      this.workspace.closeWorld(leaf.id);
    }
  }

  /** The page that is the core pane itself. */
  get corePageId(): string {
    const page = this.ui.pages.find(isCore);
    return page?.id ?? 'inventory';
  }

  get noticeText(): string {
    return this.notices.current;
  }
}
