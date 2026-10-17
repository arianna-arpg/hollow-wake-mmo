import { keyMatches, parseKeyName, webCode } from '../ui/logic/menu';
import type { Binding, InputAction } from '../ui/logic/options';

export interface InputHandlers {
  /**
   * A press of the keys or controller inputs bound to one or more actions (Tab is the menu and the book's walk):
   * the interface decides first, then the world. Held actions are sampled instead.
   */
  press: (actions: readonly string[]) => void;
  /** Left button pressed on the world at a canvas point. */
  click: (x: number, y: number) => void;
  /** Wheel steps: positive zooms in. */
  zoom: (steps: number) => void;
  /** Right-button drag. */
  orbit: (dx: number, dy: number) => void;
  /** Keyboard focus left the page: everything held is released. */
  released: () => void;
}

/** The standard gamepad layout's buttons by the names content gives them. */
const PAD_BUTTONS: Record<string, number> = {
  A: 0,
  B: 1,
  X: 2,
  Y: 3,
  LB: 4,
  RB: 5,
  LT: 6,
  RT: 7,
  Back: 8,
  Start: 9,
  LeftStick: 10,
  RightStick: 11,
  DpadUp: 12,
  DpadDown: 13,
  DpadLeft: 14,
  DpadRight: 15,
};

/** Stick directions by name: the axis and its sign. */
const PAD_AXES: Record<string, [number, number]> = {
  LeftStickUp: [1, -1],
  LeftStickDown: [1, 1],
  LeftStickLeft: [0, -1],
  LeftStickRight: [0, 1],
  RightStickUp: [3, -1],
  RightStickDown: [3, 1],
  RightStickLeft: [2, -1],
  RightStickRight: [2, 1],
};

const DEAD_ZONE = 0.35;

/**
 * Keyboard, mouse and controller, mapped through the input actions content declares with the player's rebinds
 * from Options over them: the browser counterpart of the desktop client's InputSetup. Keys typed into a text
 * field never reach the game; a capture in the Options pane takes keys before this sees them.
 */
export class Input {
  private readonly down = new Set<string>();
  private actions: InputAction[] = [];
  private bindings = new Map<string, Binding>();
  private readonly heldPad = new Set<string>();
  private orbiting = false;
  private wheel = 0;
  readonly mouse = { x: 0, y: 0, over: false };
  private readonly listeners: Array<() => void> = [];

  constructor(
    private readonly surface: HTMLElement,
    private readonly handlers: InputHandlers,
    private readonly wheelStepPixels: number,
  ) {
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

  /** Takes the input map: every action content declares and its effective bindings. */
  bind(actions: readonly InputAction[], bindingsOf: (action: string) => Binding): void {
    this.actions = [...actions];
    this.bindings = new Map(actions.map((a) => [a.id, bindingsOf(a.id)]));
  }

  private listen(target: EventTarget, type: string, fn: (e: Event) => void, options?: AddEventListenerOptions): void {
    target.addEventListener(type, fn, options);
    this.listeners.push(() => target.removeEventListener(type, fn, options));
  }

  static typing(): boolean {
    const el = document.activeElement;
    return el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement || el instanceof HTMLSelectElement || (el instanceof HTMLElement && el.isContentEditable);
  }

  private keysOf(action: string): string[] {
    const b = this.bindings.get(action);
    return b === undefined ? [] : [b.primary, b.secondary].filter((k) => k.length > 0);
  }

  /** Whether a held action's key or controller input is down; held keys ignore modifiers. */
  isHeld(action: string): boolean {
    if (this.keysOf(action).some((k) => this.down.has(webCode(parseKeyName(k))))) {
      return true;
    }
    const pad = this.bindings.get(action)?.pad ?? '';
    return pad.length > 0 && this.heldPad.has(pad);
  }

  private keyDown(e: KeyboardEvent): void {
    if (Input.typing() || e.metaKey) {
      return;
    }
    const held = this.actions.filter((a) => a.held && this.keysOf(a.id).some((k) => webCode(parseKeyName(k)) === e.code));
    const pressed = this.actions.filter((a) => !a.held && this.keysOf(a.id).some((k) => keyMatches(e, k)));
    if (held.length === 0 && pressed.length === 0) {
      return;
    }
    e.preventDefault();
    this.down.add(e.code);
    if (e.repeat || pressed.length === 0) {
      return;
    }
    this.handlers.press(pressed.map((a) => a.id));
  }

  private keyUp(e: KeyboardEvent): void {
    this.down.delete(e.code);
  }

  private releaseAll(): void {
    this.down.clear();
    this.heldPad.clear();
    this.orbiting = false;
    this.handlers.released();
  }

  /** Reads the first connected controller once a frame: held directions, and presses on the frame a button goes down. */
  pollPad(): void {
    const pads = typeof navigator.getGamepads === 'function' ? navigator.getGamepads() : [];
    const pad = [...pads].find((p) => p !== null && p.connected) ?? null;
    if (pad === null) {
      if (this.heldPad.size > 0) {
        this.heldPad.clear();
      }
      return;
    }
    const now = new Set<string>();
    for (const [name, index] of Object.entries(PAD_BUTTONS)) {
      if (pad.buttons[index]?.pressed) {
        now.add(name);
      }
    }
    for (const [name, [axis, sign]] of Object.entries(PAD_AXES)) {
      if ((pad.axes[axis] ?? 0) * sign > DEAD_ZONE) {
        now.add(name);
      }
    }
    const rising = [...now].filter((n) => !this.heldPad.has(n));
    this.heldPad.clear();
    for (const n of now) {
      this.heldPad.add(n);
    }
    for (const name of rising) {
      const pressed = this.actions.filter((a) => !a.held && this.bindings.get(a.id)?.pad === name);
      if (pressed.length > 0) {
        this.handlers.press(pressed.map((a) => a.id));
      }
    }
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
