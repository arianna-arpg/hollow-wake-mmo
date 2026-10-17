import { clear, el } from '../dom';
import type { DropTarget, LeafContext, LeafView } from '../leaf';
import { eventKeyName, keyLabelOf } from '../logic/menu';
import { bindingSlot, gateFacts, gateHolds, type OptionEntry, type OptionPage, type OptionsState, type Settings } from '../logic/options';
import { button, line } from '../widgets';
import {
  colorFieldValue,
  freshPadInput,
  heldPadInputs,
  noticeText,
  recordChanges,
  roundSlider,
  shownGroups,
  shownPages,
  sliderText,
  slotLabel,
  snapshotValues,
  storedColor,
  undoChanges,
} from './optionsLogic';
import './panes.css';

/** A row of the list: an entry's name, description and reason, its control and its reset. */
interface Row {
  readonly entry: OptionEntry;
  readonly root: HTMLElement;
  /** Greys the controls when the requirement fails, shows the reset while the value differs from the default, relabels key slots. */
  refresh(): void;
}

/** A text field being typed in, carried across a rebuild. */
interface Typing {
  entry: string;
  value: string;
  start: number | null;
  end: number | null;
}

const SLOT_TIPS = ['Primary key', 'Second key', 'Controller'];

/** Where the list was scrolled, by the pane's state, so a pane rebuilt by a restyle keeps its place. */
const scrolled = new WeakMap<OptionsState, { view: string; top: number }>();

/** Whether the browser reads controllers at all (the Gamepad API wants a secure page). */
const padsReadable = (): boolean => typeof navigator !== 'undefined' && typeof navigator.getGamepads === 'function';

/** The inputs held on each standard-layout controller, by its index. */
function readPads(): Map<number, Set<string>> {
  const held = new Map<number, Set<string>>();
  let pads: ReadonlyArray<Gamepad | null> = [];
  try {
    pads = navigator.getGamepads();
  } catch {
    return held;
  }
  for (const pad of pads) {
    if (pad !== null && pad.connected && pad.mapping === 'standard') {
      held.set(pad.index, new Set(heldPadInputs(pad)));
    }
  }
  return held;
}

/** A key the capture took: kept from the game and from the page. */
function swallow(e: KeyboardEvent): void {
  e.preventDefault();
  e.stopImmediatePropagation();
}

/**
 * The Options pane (docs/design/options.md), drawn from the ui_options content: a column of pages, the selected
 * page's groups with their entries as rows, a search over every page, resets for a row, a group and a page, and
 * the notice. A change takes effect as the control moves; Escape puts back the values from before the visit and
 * Done keeps them. Key rows capture the next press, show conflicts within the action's context and swap or clear
 * the other binding; the controller slot reads standard-layout controllers through the Gamepad API. The desktop
 * client's UI/Pages/OptionsPane.cs.
 */
export class OptionsPane implements LeafView {
  readonly root: HTMLElement;
  private readonly settings: Settings;
  private readonly fact: (name: string) => number | null | undefined;
  private readonly pageList: HTMLElement;
  private readonly list: HTMLElement;
  private readonly search: HTMLInputElement;
  private readonly notice: HTMLElement;
  private readonly unsubscribe: () => void;
  private rows: Row[] = [];
  private dirty = true;
  private ownChange = false;
  private capture: { action: string; slot: number } | null = null;
  private captureButton: HTMLButtonElement | null = null;
  private conflict: HTMLElement | null = null;
  /** The controller inputs held at the last reading, by controller, so only a fresh press binds. */
  private padHeld = new Map<number, Set<string>>();
  /** A passing line after the notice (the controller slot where the browser reads no controllers). */
  private note = '';
  /** The page and search the list shows, to keep its scroll across a rebuild of the same view. */
  private view = '';
  private pendingScroll: number | null = null;
  private dragging = false;
  private disposed = false;

  constructor(
    private readonly ctx: LeafContext,
    private readonly state: OptionsState,
    facts: (name: string) => number | null | undefined,
    private readonly onConfirm: () => void,
  ) {
    this.settings = ctx.settings;
    this.fact = gateFacts(this.settings, facts);

    this.pageList = el('div', { class: 'options-pages' });
    const resetPage = button('Reset page', () => {
      const page = this.state.page;
      if (page.length > 0) {
        this.track(() => this.settings.resetPage(page));
      }
    }, { title: 'Every entry on this page to its default', className: 'page-reset' });
    const side = el('div', { class: 'options-side' }, this.pageList, el('div', { class: 'options-fill' }), resetPage);

    this.search = el('input', { class: 'options-search', type: 'search', placeholder: 'Search every page', 'aria-label': 'Search every page', spellcheck: 'false', autocomplete: 'off' });
    this.search.value = state.search;
    this.search.addEventListener('input', () => {
      this.state.search = this.search.value;
      this.dirty = true;
    });
    const done = button('Done', () => this.onConfirm(), { title: 'Keep the changes and close', className: 'options-done' });
    this.list = el('div', { class: 'options-list' });
    this.list.addEventListener('scroll', () => scrolled.set(this.state, { view: this.view, top: this.list.scrollTop }));
    this.notice = el('div', { class: 'options-notice', role: 'status' });
    const main = el('div', { class: 'options-main' }, el('div', { class: 'options-top' }, this.search, done), this.list, this.notice);
    this.root = el('div', { class: 'options-pane' }, side, main);

    this.unsubscribe = this.settings.onChange(this.changed);
    this.refresh();
    const memo = scrolled.get(state);
    this.pendingScroll = memo !== undefined && memo.view === this.view ? memo.top : null;
  }

  get preferredWidth(): number {
    return this.ctx.skin.metric('options_width', 760);
  }

  /** Whether a key slot is waiting for a press (not while a conflict asks). */
  get capturing(): boolean {
    return this.capture !== null;
  }

  get selectedPage(): string {
    return this.state.page;
  }

  get rowCount(): number {
    return this.rows.length;
  }

  /** A row by entry id, for scripted checks. */
  rowOf(entryId: string): HTMLElement | null {
    return this.rows.find((r) => r.entry.id === entryId)?.root ?? null;
  }

  refresh(): void {
    if (this.disposed) {
      return;
    }
    this.pollPads();
    if (this.pendingScroll !== null && this.list.isConnected && this.list.clientHeight > 0) {
      this.list.scrollTop = this.pendingScroll;
      this.pendingScroll = null;
    }
    // A slider being dragged keeps its element until the button lets go.
    if (this.dirty && !this.dragging) {
      this.dirty = false;
      this.rebuildPages();
      this.rebuildRows();
    }
    // The notice follows the state, which a pane built by a restyle shares with the one it replaced.
    this.updateNotice();
  }

  dropTargets(): DropTarget[] {
    return [];
  }

  /** Escape: a key capture ends, then the visit's changes are undone; with neither, the pane may close. */
  onEscape(): boolean {
    if (this.capture !== null) {
      this.endCapture();
      return true;
    }
    if (undoChanges(this.state, this.settings)) {
      this.dirty = true;
      this.updateNotice();
      return true;
    }
    return false;
  }

  /** Confirms the visit's changes (the pane is closing): nothing is left to undo. */
  confirm(): void {
    this.state.originals.clear();
    scrolled.delete(this.state);
    this.updateNotice();
  }

  dispose(): void {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.endCapture(false);
    this.endDrag();
    this.unsubscribe();
  }

  // ------------------------------------------------------------ changes

  /** A setting changed: every row refreshes, and a change no row's own control made rebuilds the list. */
  private readonly changed = (): void => {
    if (!this.ownChange) {
      this.dirty = true;
    }
    for (const row of this.rows) {
      row.refresh();
    }
  };

  /**
   * Runs a change the pane makes, remembering each changed entry's original for Escape. A change from a row's
   * own control (only, the entry's id) leaves the list standing, so the control keeps focus and drag.
   */
  private track(change: () => void, only: string | null = null): void {
    const before = snapshotValues(this.settings);
    this.ownChange = only !== null;
    try {
      change();
    } finally {
      this.ownChange = false;
    }
    recordChanges(this.state, this.settings, before);
    this.updateNotice();
    if (only === null) {
      this.dirty = true;
    }
  }

  private updateNotice(): void {
    const text = [noticeText(this.state, this.settings), this.note].filter((t) => t.length > 0).join(' ');
    if (this.notice.textContent !== text) {
      this.notice.textContent = text;
    }
  }

  // ------------------------------------------------------------ pages and rows

  private readonly holds = (expr: string | null): boolean => gateHolds(expr, this.fact);

  private pages(): OptionPage[] {
    return shownPages(this.settings.catalog.pages, this.holds);
  }

  private rebuildPages(): void {
    const pages = this.pages();
    if (!pages.some((p) => p.id === this.state.page)) {
      this.state.page = pages[0]?.id ?? '';
    }
    clear(this.pageList);
    const searching = this.state.search.length > 0;
    for (const page of pages) {
      const selected = page.id === this.state.page && !searching;
      const b = button(page.name, () => this.selectPage(page.id), { title: page.description, className: 'options-page', pressed: selected });
      b.dataset.page = page.id;
      b.setAttribute('aria-pressed', String(selected));
      this.pageList.append(b);
    }
  }

  /** A page from the list: it shows, and the search clears. */
  private selectPage(id: string): void {
    this.state.page = id;
    this.state.search = '';
    this.search.value = '';
    this.dirty = true;
  }

  private rebuildRows(): void {
    this.endCapture(false);
    const view = `${this.state.page}\n${this.state.search.trim()}`;
    const top = view === this.view ? this.list.scrollTop : 0;
    const typing = this.typingIn();
    this.view = view;
    clear(this.list);
    this.rows = [];
    for (const shown of shownGroups(this.pages(), this.state.page, this.state.search, this.holds)) {
      const { page, group } = shown;
      const reset = button('Reset group', () => this.track(() => this.settings.resetGroup(page.id, group.id)), { title: `Every entry of ${group.name} to its default`, className: 'group-reset' });
      reset.dataset.group = group.id;
      reset.dataset.page = page.id;
      this.list.append(el('div', { class: 'options-group', 'data-group': group.id, 'data-page': page.id }, el('div', { class: 'options-group-title', text: shown.title }), reset));
      if (group.description.length > 0) {
        this.list.append(line(group.description, 'muted options-group-desc'));
      }
      for (const entry of shown.entries) {
        const row = this.makeRow(entry);
        this.rows.push(row);
        this.list.append(row.root);
      }
      this.list.append(el('div', { class: 'options-gap' }));
    }
    if (this.rows.length === 0) {
      const query = this.state.search.trim();
      this.list.append(line(query.length > 0 ? `Nothing matches "${query}".` : 'Nothing to set here.', 'muted'));
    }
    this.list.scrollTop = top;
    this.restoreTyping(typing);
  }

  /** The text field being typed in, when it is in the list. */
  private typingIn(): Typing | null {
    const active = document.activeElement;
    if (!(active instanceof HTMLInputElement) || active.type !== 'text' || !this.list.contains(active)) {
      return null;
    }
    const entry = active.closest<HTMLElement>('.option-row')?.dataset.entry;
    return entry === undefined ? null : { entry, value: active.value, start: active.selectionStart, end: active.selectionEnd };
  }

  /** Puts the text being typed back in the rebuilt row's field, with the caret where it was. */
  private restoreTyping(typing: Typing | null): void {
    if (typing === null) {
      return;
    }
    const field = this.rowOf(typing.entry)?.querySelector<HTMLInputElement>('input[type="text"]');
    if (field === null || field === undefined || field.disabled) {
      return;
    }
    field.value = typing.value;
    field.focus({ preventScroll: true });
    field.setSelectionRange(typing.start, typing.end);
  }

  private makeRow(entry: OptionEntry): Row {
    const s = this.settings;
    const controls: Array<HTMLInputElement | HTMLSelectElement | HTMLButtonElement> = [];
    const keys: HTMLButtonElement[] = [];
    // A change from the row's own control: the list stays, the control keeps focus and drag.
    const own = (change: () => void) => this.track(change, entry.id);

    const text = el('div', { class: 'option-text' }, el('div', { class: 'option-name', text: entry.name }));
    if (entry.description.length > 0 && entry.kind !== 'key') {
      text.append(el('div', { class: 'option-desc', text: entry.description }));
    }
    const reason = el('div', { class: 'option-reason', hidden: '' });
    text.append(reason);

    const control = el('div', { class: 'option-control' });
    switch (entry.kind) {
      case 'toggle': {
        const check = el('input', { type: 'checkbox', tabindex: '-1' });
        check.checked = s.toggle(entry.id);
        check.addEventListener('change', () => {
          own(() => s.set(entry.id, check.checked));
          check.blur();
        });
        control.append(el('label', { class: 'switch' }, check, el('span')));
        controls.push(check);
        break;
      }
      case 'choice': {
        const select = el('select', { tabindex: '-1' });
        for (const c of entry.choices) {
          select.append(el('option', { value: c.id, text: c.name }));
        }
        select.value = s.text(entry.id);
        select.addEventListener('change', () => {
          const id = select.value;
          if (entry.choices.some((c) => c.id === id)) {
            own(() => s.set(entry.id, id));
          }
          select.blur();
        });
        control.append(select);
        controls.push(select);
        break;
      }
      case 'slider': {
        const range = el('input', { type: 'range', tabindex: '-1', min: String(entry.min), max: String(entry.max), step: entry.step > 0 ? String(entry.step) : 'any' });
        range.value = String(s.number(entry.id));
        const value = el('span', { class: 'option-value num', text: sliderText(s.number(entry.id), entry.unit) });
        range.addEventListener('pointerdown', () => this.beginDrag());
        range.addEventListener('input', () => {
          const v = range.valueAsNumber;
          if (!Number.isFinite(v)) {
            return;
          }
          value.textContent = sliderText(v, entry.unit);
          own(() => s.set(entry.id, roundSlider(v)));
        });
        range.addEventListener('change', () => {
          this.endDrag();
          range.blur();
        });
        control.append(range, value);
        controls.push(range);
        break;
      }
      case 'key': {
        const action = entry.action ?? '';
        const binding = s.bindings(action);
        for (let slot = 0; slot < 3; slot++) {
          const b: HTMLButtonElement = button(slotLabel(bindingSlot(binding, slot), slot), () => this.beginCapture(action, slot, b), { title: SLOT_TIPS[slot], className: 'key-slot' });
          b.dataset.slot = String(slot);
          control.append(b);
          keys.push(b);
          controls.push(b);
        }
        break;
      }
      case 'color': {
        const picker = el('input', { type: 'color', tabindex: '-1' });
        picker.value = colorFieldValue(s.text(entry.id));
        picker.addEventListener('input', () => own(() => s.set(entry.id, storedColor(picker.value, s.text(entry.id)))));
        picker.addEventListener('change', () => picker.blur());
        control.append(picker);
        controls.push(picker);
        break;
      }
      default: {
        const field = el('input', { type: 'text', spellcheck: 'false', autocomplete: 'off' });
        if (entry.maxLength > 0) {
          field.maxLength = entry.maxLength;
        }
        field.value = s.text(entry.id);
        // Kept when submitted or left, as on the desktop.
        const commit = () => own(() => s.set(entry.id, field.value));
        field.addEventListener('change', commit);
        field.addEventListener('keydown', (e) => {
          if (e.key === 'Enter') {
            e.stopPropagation();
            commit();
          }
        });
        control.append(field);
        controls.push(field);
      }
    }

    const reset = button('Reset', () => this.track(() => s.reset(entry.id)), { title: 'Back to the default', className: 'row-reset' });
    const root = el('div', { class: 'option-row', 'data-entry': entry.id, 'data-kind': entry.kind }, text, control, reset);
    if (entry.description.length > 0) {
      root.title = entry.description;
    }
    const refresh = (): void => {
      const requires = this.holds(entry.requires);
      for (const c of controls) {
        c.disabled = !requires;
      }
      reason.textContent = requires ? '' : entry.requiresReason;
      reason.hidden = requires || entry.requiresReason.length === 0;
      root.classList.toggle('unmet', !requires);
      reset.hidden = s.isDefault(entry.id);
      if (entry.kind === 'key' && entry.action !== null) {
        const binding = s.bindings(entry.action);
        keys.forEach((k, i) => {
          const label = slotLabel(bindingSlot(binding, i), i);
          if (k !== this.captureButton && k.textContent !== label) {
            k.textContent = label;
          }
        });
      }
    };
    refresh();
    return { entry, root, refresh };
  }

  // ------------------------------------------------------------ sliders

  /** A slider is being dragged: the list is not rebuilt under it until the button lets go. */
  private beginDrag(): void {
    if (this.dragging) {
      return;
    }
    this.dragging = true;
    window.addEventListener('pointerup', this.endDrag, true);
    window.addEventListener('pointercancel', this.endDrag, true);
    window.addEventListener('blur', this.endDrag);
  }

  private readonly endDrag = (): void => {
    this.dragging = false;
    window.removeEventListener('pointerup', this.endDrag, true);
    window.removeEventListener('pointercancel', this.endDrag, true);
    window.removeEventListener('blur', this.endDrag);
  };

  // ------------------------------------------------------------ key capture

  private beginCapture(action: string, slot: number, b: HTMLButtonElement): void {
    if (this.disposed) {
      return;
    }
    this.endCapture();
    if (slot === 2 && !padsReadable()) {
      this.note = 'Controller inputs are rebound on the desktop client.';
      this.updateNotice();
      return;
    }
    this.capture = { action, slot };
    this.captureButton = b;
    b.textContent = slot === 2 ? 'Press a button' : 'Press a key';
    b.classList.add('capturing');
    // Space or Enter must reach the capture, not press the focused slot again.
    b.blur();
    // Inputs already held when the capture began do not count until they are let go and pressed again.
    this.padHeld = slot === 2 ? readPads() : new Map();
    window.addEventListener('keydown', this.keyDown, true);
  }

  /** Ends a capture or a conflict prompt; the slot shows its binding again unless the list is about to be rebuilt. */
  private endCapture(relabel = true): void {
    window.removeEventListener('keydown', this.keyDown, true);
    this.capture = null;
    this.padHeld = new Map();
    this.conflict?.remove();
    this.conflict = null;
    const b = this.captureButton;
    this.captureButton = null;
    if (b !== null) {
      b.classList.remove('capturing');
      if (relabel) {
        for (const row of this.rows) {
          row.refresh();
        }
      }
    }
    if (this.note.length > 0) {
      this.note = '';
      this.updateNotice();
    }
  }

  /**
   * The next key press while a key slot captures, ahead of the game: Escape cancels, Delete or Backspace clears
   * the slot, a bare modifier waits for its key. While the controller slot captures, only Escape is taken.
   */
  private readonly keyDown = (e: KeyboardEvent): void => {
    const capture = this.capture;
    if (capture === null) {
      return;
    }
    if (capture.slot === 2) {
      if (e.code === 'Escape') {
        swallow(e);
        this.endCapture();
      }
      return;
    }
    swallow(e);
    if (e.repeat) {
      return;
    }
    if (e.code === 'Escape') {
      this.endCapture();
      return;
    }
    const clears = e.code === 'Delete' || e.code === 'Backspace';
    const name = clears ? '' : eventKeyName(e);
    if (!clears && name.length === 0) {
      return;
    }
    this.bind(capture.action, capture.slot, name);
  };

  /** While the controller slot captures (each frame), binds the first fresh press on a standard-layout controller. */
  private pollPads(): void {
    const capture = this.capture;
    if (capture === null || capture.slot !== 2) {
      return;
    }
    const now = readPads();
    for (const [index, held] of now) {
      const fresh = freshPadInput(this.padHeld.get(index) ?? new Set<string>(), held);
      if (fresh !== null) {
        this.bind(capture.action, capture.slot, fresh);
        return;
      }
    }
    this.padHeld = now;
  }

  /** Binds a key or controller input, or asks what to do with the action that already holds it. */
  private bind(action: string, slot: number, name: string): void {
    const conflicts = name.length === 0 ? [] : this.settings.conflicts(action, name, slot === 2);
    if (conflicts.length === 0) {
      this.track(() => this.settings.rebind(action, slot, name, false));
      this.endCapture();
      return;
    }
    // The press is taken; the prompt decides. The slot keeps its "press" label until then, as on the desktop.
    window.removeEventListener('keydown', this.keyDown, true);
    this.capture = null;
    const settle = (swap: boolean) => () => {
      this.track(() => this.settings.rebind(action, slot, name, swap));
      this.endCapture();
    };
    this.conflict?.remove();
    this.conflict = el(
      'div',
      { class: 'conflict', role: 'alert' },
      el('div', { class: 'conflict-text', text: `${keyLabelOf(name)} is also bound to ${conflicts.map((c) => c.name).join(', ')}.` }),
      button('Swap', settle(true), { title: "The other action takes this action's old key", className: 'conflict-swap' }),
      button('Clear theirs', settle(false), { title: 'The other action loses the key', className: 'conflict-clear' }),
      button('Cancel', () => this.endCapture(), { className: 'conflict-cancel' }),
    );
    const owner = this.rows.find((r) => r.entry.action === action);
    if (owner !== undefined) {
      owner.root.after(this.conflict);
    } else {
      this.list.append(this.conflict);
    }
  }
}
