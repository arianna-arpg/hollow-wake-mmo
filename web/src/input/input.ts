import type { Action } from '../config';

/** Actions that are held (sampled every frame) rather than pressed. */
const HELD: ReadonlySet<Action> = new Set<Action>(['move_forward', 'move_back', 'move_left', 'move_right']);

export interface InputHandlers {
  /** A press of a non-held action (jump, slots, camera keys, ...). */
  action: (action: Action) => void;
  /** Left button pressed on the world at a canvas point. */
  click: (x: number, y: number) => void;
  /** Wheel steps: positive zooms in. */
  zoom: (steps: number) => void;
  /** Right-button drag. */
  orbit: (dx: number, dy: number) => void;
  /** Keyboard focus left the page: everything held is released. */
  released: () => void;
}

/**
 * Keyboard and mouse, mapped through the bindings in config.json (KeyboardEvent.code values), the browser
 * counterpart of the desktop client's InputSetup. Keys typed into a text field never reach the game.
 */
export class Input {
  private readonly down = new Set<string>();
  private readonly actionsByCode = new Map<string, Action[]>();
  private readonly codesByAction = new Map<Action, string[]>();
  private orbiting = false;
  private wheel = 0;
  readonly mouse = { x: 0, y: 0, over: false };
  private readonly listeners: Array<() => void> = [];

  constructor(
    bindings: Record<Action, string[]>,
    private readonly surface: HTMLElement,
    private readonly handlers: InputHandlers,
    private readonly wheelStepPixels: number,
  ) {
    for (const [action, codes] of Object.entries(bindings) as Array<[Action, string[]]>) {
      this.codesByAction.set(action, codes);
      for (const code of codes) {
        const list = this.actionsByCode.get(code) ?? [];
        list.push(action);
        this.actionsByCode.set(code, list);
      }
    }
    this.listen(window, 'keydown', (e) => this.keyDown(e as KeyboardEvent));
    this.listen(window, 'keyup', (e) => this.keyUp(e as KeyboardEvent));
    this.listen(window, 'blur', () => this.releaseAll());
    this.listen(document, 'visibilitychange', () => {
      if (document.hidden) {
        this.releaseAll();
      }
    });
    this.listen(surface, 'pointermove', (e) => this.pointerMove(e as PointerEvent));
    this.listen(surface, 'pointerdown', (e) => this.pointerDown(e as PointerEvent));
    this.listen(window, 'pointerup', (e) => {
      if ((e as PointerEvent).button === 2) {
        this.orbiting = false;
      }
    });
    this.listen(surface, 'pointerleave', () => {
      this.mouse.over = false;
    });
    this.listen(surface, 'contextmenu', (e) => e.preventDefault());
    this.listen(surface, 'wheel', (e) => this.onWheel(e as WheelEvent), { passive: false });
  }

  private listen(target: EventTarget, type: string, fn: (e: Event) => void, options?: AddEventListenerOptions): void {
    target.addEventListener(type, fn, options);
    this.listeners.push(() => target.removeEventListener(type, fn, options));
  }

  static typing(): boolean {
    const el = document.activeElement;
    return el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement || el instanceof HTMLSelectElement || (el instanceof HTMLElement && el.isContentEditable);
  }

  isHeld(action: Action): boolean {
    return (this.codesByAction.get(action) ?? []).some((code) => this.down.has(code));
  }

  matches(event: KeyboardEvent, action: Action): boolean {
    return (this.codesByAction.get(action) ?? []).includes(event.code);
  }

  private keyDown(e: KeyboardEvent): void {
    if (Input.typing() || e.ctrlKey || e.metaKey || e.altKey) {
      return;
    }
    const actions = this.actionsByCode.get(e.code);
    if (actions === undefined) {
      return;
    }
    e.preventDefault();
    this.down.add(e.code);
    if (e.repeat) {
      return;
    }
    for (const action of actions) {
      if (!HELD.has(action)) {
        this.handlers.action(action);
      }
    }
  }

  private keyUp(e: KeyboardEvent): void {
    this.down.delete(e.code);
  }

  private releaseAll(): void {
    this.down.clear();
    this.orbiting = false;
    this.handlers.released();
  }

  private pointerMove(e: PointerEvent): void {
    const rect = this.surface.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    if (this.orbiting) {
      this.handlers.orbit(e.movementX, e.movementY);
    }
    this.mouse.x = x;
    this.mouse.y = y;
    this.mouse.over = true;
  }

  private pointerDown(e: PointerEvent): void {
    const rect = this.surface.getBoundingClientRect();
    this.mouse.x = e.clientX - rect.left;
    this.mouse.y = e.clientY - rect.top;
    this.mouse.over = true;
    if (Input.typing()) {
      (document.activeElement as HTMLElement).blur();
    }
    if (e.button === 0) {
      this.handlers.click(this.mouse.x, this.mouse.y);
    } else if (e.button === 2) {
      this.orbiting = true;
    }
  }

  private onWheel(e: WheelEvent): void {
    e.preventDefault();
    // Mouse wheels send one big step, trackpads many small ones; both add up to whole zoom steps.
    const pixels = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * 33 : e.deltaMode === WheelEvent.DOM_DELTA_PAGE ? e.deltaY * 400 : e.deltaY;
    this.wheel += pixels;
    const steps = Math.trunc(this.wheel / this.wheelStepPixels);
    if (steps !== 0) {
      this.wheel -= steps * this.wheelStepPixels;
      this.handlers.zoom(-steps);
    }
  }

  dispose(): void {
    for (const remove of this.listeners) {
      remove();
    }
    this.listeners.length = 0;
  }
}
