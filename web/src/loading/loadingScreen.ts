import { presentation } from './content';
import { SpiritRun, spiritDirection, spiritLayout, type SpiritDirection } from './spiritRun';
import { drawSpiritRun } from './spiritView';
import { SeededRng } from './rng';


export interface LoadingStatus { label: string; detail?: string; completed?: number; total?: number }
export interface LoadingOptions extends LoadingStatus {
  kind: 'entry' | 'travel'; direction?: SpiritDirection;
  cancel?: () => void;
}
export interface LoadingLease {
  readonly current: boolean;
  update(status: LoadingStatus): void;
  /** Yield a paint before a bounded CPU stage; this does not make that stage asynchronous. */
  paint(): Promise<void>;
  fail(message: string, retry?: () => void): void;
  finish(): void;
}

/** A token-owned loading surface. Old jobs cannot dismiss or rewrite a new job.
 * No fake percentage, minimum duration, gameplay rewards or save writes. */
export class LoadingScreen {
  readonly activities = new Map<string, (direction: SpiritDirection, seed: number) => SpiritRun>([
    ['spirit_run', (d, seed) => { const rng = new SeededRng(seed); return new SpiritRun(d, () => rng.next()); }],
  ]);
  private sequence = 0;
  private failed = false;
  private root?: HTMLDivElement;
  private canvas?: HTMLCanvasElement;
  private label?: HTMLElement;
  private detail?: HTMLElement;
  private progress?: HTMLProgressElement;
  private actions?: HTMLElement;
  private cancel?: () => void;
  private retry?: () => void;
  private run?: SpiritRun;
  private token = 0;
  private raf = 0;
  private last = 0;
  private keys = new Set<string>();
  private releasedKeys = new Set<string>();
  private pointer?: number;
  private inert: { el: HTMLElement; was: boolean }[] = [];
  private focus?: HTMLElement;
  private padHeld = false;
  private padConfirm = false;
  private statusKey = '';
  private media = matchMedia('(prefers-reduced-motion: reduce)');
  constructor(private clearWorldInput: () => void, private reducedMotion: () => boolean = () => false) {
    window.addEventListener('keydown', this.keyDown, true);
    window.addEventListener('keyup', this.keyUp, true);
    window.addEventListener('blur', () => { this.keys.clear(); this.releasedKeys.clear(); this.pointer = undefined; });
    document.addEventListener('visibilitychange', () => { this.last = 0; this.keys.clear(); this.pointer = undefined; });
  }
  get active(): boolean { return !!this.root; }
  /** Controller buttons held in the toy must be released before they can cast. */
  get padQuarantined(): boolean {
    if (!this.padHeld) return false;
    this.padHeld = this.pads().some(p => p.axes.some(a => Math.abs(a) > presentation.controls.deadzone) || p.buttons.some(b => b.pressed));
    return this.padHeld;
  }
  /** Read-only diagnostics for the real-client acceptance course. */
  get snapshot() {
    const r = this.run;
    return { active: this.active, direction: r?.direction, time: r?.time, lane: r?.lane,
      passed: r?.passed, streak: r?.streak, hits: r?.hits, speed: r?.speed, dash: r?.dash, currentsTaken: r?.currentsTaken, collected: r?.collected, boostGates: r?.boostGates,
      pickups: r?.pickups.map(p => ({ ...p })), currents: r?.currents.map(b => ({ ...b })),
      gates: r?.gates.map(g => ({ ...g, openings: g.openings.map(o => ({ ...o })) })), label: this.label?.textContent, failed: this.failed };
  }
  begin(options: LoadingOptions): LoadingLease {
    this.close(); const token = ++this.token;
    const seed = (presentation.seed + Math.imul(++this.sequence, presentation.seedStep)) >>> 0;
    const directionRng = new SeededRng(seed);
    this.run = this.activities.get(presentation.activity)!(options.direction ?? spiritDirection(options.kind, () => directionRng.next()), seed);
    this.failed = false;
    this.cancel = options.cancel; this.statusKey = '';
    this.clearWorldInput(); this.keys.clear(); this.pointer = undefined;
    this.focus = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
    this.inert = Array.from(document.body.children).filter((el): el is HTMLElement => el instanceof HTMLElement)
      .map(el => ({ el, was: el.inert }));
    for (const row of this.inert) row.el.inert = true;
    const root = this.root = document.createElement('div'); root.id = 'mu-loading-screen';
    root.setAttribute('role', 'dialog'); root.setAttribute('aria-modal', 'true'); root.setAttribute('aria-label', presentation.labels.accessibleTitle); root.tabIndex = -1;
    root.style.cssText = `position:fixed;inset:0;z-index:10000;background:#080e16;color:#d9e9e3;font-family:Georgia,serif;overflow:hidden;touch-action:none;user-select:none`;
    root.innerHTML = `<style>
      #mu-loading-screen *{box-sizing:border-box}
      #mu-loading-screen .mu-head{position:absolute;top:clamp(20px,5vh,52px);left:clamp(22px,5vw,76px);right:clamp(22px,5vw,76px);display:flex;justify-content:space-between;gap:20px;pointer-events:none}
      #mu-loading-screen .mu-eyebrow{font:11px system-ui;letter-spacing:.3em;color:#86a59e}
      #mu-loading-screen h1{font-weight:400;font-size:clamp(24px,3vw,42px);letter-spacing:.14em;margin:9px 0;color:#bcd4e8;text-shadow:0 0 22px #9ab8dc38}
      #mu-loading-screen canvas{position:absolute;inset:108px 0 162px;width:100%;height:calc(100% - 270px)}
      #mu-loading-screen footer{position:absolute;bottom:clamp(18px,4vh,42px);left:clamp(22px,5vw,76px);right:clamp(22px,5vw,76px);display:grid;grid-template-columns:1fr auto;gap:12px 26px;align-items:end;font-family:system-ui}
      #mu-loading-screen .mu-status{font-size:14px;letter-spacing:.04em}
      #mu-loading-screen .mu-detail{font-size:11px;color:#8ba5a4;min-height:16px;margin-top:6px;max-width:65ch}
      #mu-loading-screen progress{display:block;width:min(320px,100%);height:3px;margin:12px 0 0;accent-color:#8acbbc}
      #mu-loading-screen .mu-pickups{display:flex;flex-wrap:wrap;gap:6px 18px;font-size:10px;letter-spacing:.04em;grid-column:1/-1}
      #mu-loading-screen .mu-controls{font-size:10px;letter-spacing:.08em;color:#809695;grid-column:1/-1}
      #mu-loading-screen button{border:1px solid #69877f;background:#17282c;color:#dfebe3;padding:9px 17px;font:12px system-ui;cursor:pointer;margin-left:8px}
      #mu-loading-screen button:focus-visible{outline:2px solid #e8c992;outline-offset:4px}
      @media(max-height:480px){#mu-loading-screen canvas{inset:80px 0 145px;height:calc(100% - 225px)}#mu-loading-screen .mu-head{top:14px}#mu-loading-screen h1{font-size:22px;margin:4px 0}#mu-loading-screen footer{bottom:12px;gap:5px}#mu-loading-screen .mu-detail{margin-top:2px}}
      </style><canvas aria-label="${presentation.labels.canvas}"></canvas>
      <header class="mu-head"><div><div class="mu-eyebrow"><img src="./icon.svg" alt="" width="24" height="24" style="vertical-align:middle;margin-right:12px">${presentation.labels.brand}</div><h1 class="runescript" aria-label="${presentation.labels.accessibleTitle}">${presentation.labels.title}</h1></div></header>
      <footer><div><div class="mu-status" role="status" aria-live="polite"></div><div class="mu-detail"></div><progress aria-label="${presentation.labels.progress}"></progress></div><div class="mu-actions"></div><div class="mu-pickups">${presentation.labels.hint}</div><div class="mu-controls">${presentation.labels.controls}</div></footer>`;
    this.canvas = root.querySelector('canvas')!; this.label = root.querySelector('.mu-status')!;
    this.detail = root.querySelector('.mu-detail')!;
    this.progress = root.querySelector('progress')!; this.actions = root.querySelector('.mu-actions')!;
    root.addEventListener('pointermove', e => this.point(e));
    root.addEventListener('pointerdown', e => { if (!(e.target instanceof HTMLButtonElement)) { this.point(e); root.focus({ preventScroll: true }); } });
    root.addEventListener('contextmenu', e => e.preventDefault());
    document.body.append(root); root.focus({ preventScroll: true });
    this.update(options); this.buttons(); this.last = 0; this.padConfirm = !!this.pads()[0]?.buttons[0]?.pressed;
    this.raf = requestAnimationFrame(this.frame);
    const current = (): boolean => this.active && this.token === token;
    return { get current() { return current(); },
      update: status => { if (current()) { this.failed = false; this.retry = undefined; this.update(status); this.buttons(); } },
      paint: () => new Promise(resolve => {
        // Background tabs throttle rAF. Never strand a preparation on visibility.
        let done = false; const finish = (): void => { if (!done) { done = true; resolve(); } };
        const fallback = setTimeout(finish, 100);
        requestAnimationFrame(() => setTimeout(() => { clearTimeout(fallback); finish(); }, 0));
      }),
      fail: (message, retry) => { if (current()) { this.failed = true; this.retry = retry; this.update({ label: presentation.labels.failed, detail: message }); this.buttons(); } },
      finish: () => { if (current()) this.close(); },
    };
  }
  private update(status: LoadingStatus): void {
    const key = JSON.stringify(status); if (key === this.statusKey) return; this.statusKey = key;
    this.label!.textContent = status.label; this.detail!.textContent = status.detail ?? '';
    if (Number.isFinite(status.total) && status.total! > 0 && Number.isFinite(status.completed)) {
      this.progress!.max = status.total!; this.progress!.value = Math.max(0, Math.min(status.completed!, status.total!));
    } else this.progress!.removeAttribute('value');
  }
  private buttons(): void {
    const actions = this.actions!;
    // Do not replace a focused button every time a readiness poll repeats.
    const key = `${!!this.retry}:${!!this.cancel}`; if (actions.dataset.key === key) return;
    actions.dataset.key = key; actions.replaceChildren();
    for (const [id, action] of [['retry', this.retry], ['cancel', this.cancel]] as const) if (action) {
      const button = document.createElement('button'); button.textContent = presentation.labels[id];
      button.onclick = () => id === 'retry' ? this.retry?.() : this.cancel?.(); actions.append(button);
    }
  }
  close(): void {
    if (!this.root) return;
    cancelAnimationFrame(this.raf);
    for (const key of this.keys) this.releasedKeys.add(key);
    this.keys.clear(); this.padHeld = true; this.clearWorldInput();
    this.root.remove(); this.root = undefined; this.run = undefined; this.retry = undefined; this.failed = false;
    if (this.canvas) this.canvas.width = this.canvas.height = 1;
    this.canvas = undefined; this.label = this.detail = this.actions = undefined;
    this.progress = undefined; this.cancel = undefined;
    for (const row of this.inert) row.el.inert = row.was;
    this.inert = []; if (this.focus?.isConnected) this.focus.focus({ preventScroll: true });
  }
  private pads(): Gamepad[] { return Array.from(navigator.getGamepads?.() ?? []).filter((p): p is Gamepad => !!p && p.connected); }
  private keyDown = (e: KeyboardEvent): void => {
    if (!this.active) { if (this.releasedKeys.has(e.code)) { e.preventDefault(); e.stopImmediatePropagation(); } return; }
    if (e.code === 'Tab') {
      const buttons = Array.from(this.actions!.querySelectorAll('button'));
      const i = buttons.indexOf(document.activeElement as HTMLButtonElement);
      if (buttons.length) buttons[(i + (e.shiftKey ? buttons.length - 1 : 1)) % buttons.length].focus();
      e.preventDefault(); e.stopImmediatePropagation(); return;
    }
    if ((e.code === 'Enter' || e.code === 'Space') && e.target instanceof HTMLButtonElement) { e.stopPropagation(); return; }
    this.keys.add(e.code); this.pointer = undefined; e.preventDefault(); e.stopImmediatePropagation();
  };
  private keyUp = (e: KeyboardEvent): void => {
    const quarantined = this.releasedKeys.delete(e.code); this.keys.delete(e.code);
    if (this.active || quarantined) { e.preventDefault(); e.stopImmediatePropagation(); }
  };
  private point(e: PointerEvent): void {
    if (!this.run || !this.canvas) return;
    const rect = this.canvas.getBoundingClientRect();
    this.pointer = spiritLayout(rect.width, rect.height, this.run.direction).lane(e.clientX - rect.left, e.clientY - rect.top);
  }
  private frame = (now: number): void => {
    if (!this.root || !this.run) return;
    const canvas = this.canvas!, rect = canvas.getBoundingClientRect(), dpr = Math.min(2, devicePixelRatio || 1);
    const width = Math.max(1, rect.width), height = Math.max(1, rect.height);
    if (canvas.width !== Math.round(width * dpr) || canvas.height !== Math.round(height * dpr)) {
      canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
    }
    const down = this.run.direction === 'down', keys = this.keys, controls = presentation.controls;
    let axis = Number((down ? controls.positiveX : controls.positiveY).some(k => keys.has(k)))
      - Number((down ? controls.negativeX : controls.negativeY).some(k => keys.has(k)));
    const pad = this.pads()[0], stick = pad?.axes[down ? 0 : 1] ?? 0;
    const dpad = pad ? Number(pad.buttons[down ? 15 : 13]?.pressed) - Number(pad.buttons[down ? 14 : 12]?.pressed) : 0;
    if (Math.abs(stick) > controls.deadzone || dpad) { axis = dpad || stick; this.pointer = undefined; }
    const confirm = !!pad?.buttons[0]?.pressed;
    if (confirm && !this.padConfirm) this.actions?.querySelector('button')?.click();
    this.padConfirm = confirm;
    if (!this.active) return;
    this.run.step(this.last && !document.hidden ? (now - this.last) / 1000 : 0, { axis, target: this.pointer }); this.last = now;
    const ctx = canvas.getContext('2d');
    if (ctx) { ctx.setTransform(dpr, 0, 0, dpr, 0, 0); drawSpiritRun(ctx, width, height, this.run, this.media.matches || this.reducedMotion()); }
    this.raf = requestAnimationFrame(this.frame);
  };
}
