import { Scene, OrthographicCamera, WebGLRenderer, Vector2, Vector3, Plane, Raycaster } from 'three';
import type { CharacterListMsg, EntityState } from '../../net/protocol';
import type { AppearanceService } from '../../world/entities';
import { BodyMesh } from '../../world/appearance/bodyMesh';
import { Pose } from '../../world/appearance/animator';
import { Input } from '../../input/input';
import type { Settings } from '../logic/options';
import type { Obj } from '../logic/content';
import { el, clear } from '../dom';
import { SpatialSelection, type EntrySceneSpec } from './spatialSelection';
import { entryChoices } from './entryChoices';
import './entry.css';
import { abyss, drawWisp } from '../../loading/spiritArt';
import { presentation } from '../../loading/content';

export interface EntryHandlers {
  create(name: string, calling: string, request: string): void;
  enter(id: string): void;
  leave(): void;
  options(): void;
}

/** Spatial-choice activity adapter. Body construction and input bindings are the world's existing mechanisms. */
export class EntryScene {
  readonly root = el('section', { class: 'entry-scene', tabindex: '-1' });
  readonly model: SpatialSelection;
  private readonly scene = new Scene();
  private readonly camera = new OrthographicCamera();
  private readonly renderer = new WebGLRenderer({ alpha: true, antialias: true });
  private readonly bodies: (BodyMesh | null)[] = [];
  private readonly atmosphere = el('canvas', { class: 'entry-atmosphere' });
  private readonly spirit = el('canvas', { class: 'entry-spirit' });
  private time = 0;
  private readonly names = el('div', { class: 'entry-names', 'aria-hidden': 'true' });
  private readonly roster = el('nav', { class: 'entry-roster' });
  private readonly card = el('div', { class: 'entry-card', hidden: '', role: 'dialog', 'aria-modal': 'true' });
  private readonly status = el('p', { role: 'status', class: 'entry-status' });
  private readonly progress = el('progress', { max: '1', value: '0' });
  private readonly input: Input;
  private frame = 0; private last = 0; private shown = -1; private request = 0; private pending: string | null = null;
  private wasActive = false; private movementArmed = false; private padArmed = false; private previousButtons: boolean[] = []; private focusBeforeCard: HTMLElement | null = null;
  private alive = true;
  private notice: string | null = null;
  private readonly unbindSettings: () => void;
  readonly spec: EntrySceneSpec;

  constructor(spec: EntrySceneSpec, private readonly kinds: Map<string, Obj[]>, private readonly appearance: AppearanceService,
    settings: Settings, private readonly handlers: EntryHandlers, private readonly suspended: () => boolean) {
    this.spec = spec; this.model = new SpatialSelection(spec);
    this.root.setAttribute('aria-label', spec.labels.title!);
    this.roster.setAttribute('aria-label', `${spec.labels.saved} / ${spec.labels.new}`);
    this.progress.setAttribute('aria-label', spec.labels.prompt!);
    for (const [key, value] of Object.entries(spec.palette)) this.root.style.setProperty(`--entry-${key}`, value);
    for (const [key, value] of Object.entries(spec.layout)) this.root.style.setProperty(`--entry-${key}`, `${value}px`);
    const button = (label: string, action: () => void) => { const b = el('button', { type: 'button', text: label }); b.onclick = action; return b; };
    const header = el('header', {}, el('div', {}, el('span', { text: spec.labels.brand, class: 'entry-overline' }), el('h1', { text: spec.labels.title })),
      el('div', {}, button(spec.labels.options!, () => { this.input.releaseAll(); this.model.suspend(); handlers.options(); }), button(spec.labels.disconnect!, () => handlers.leave())));
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.domElement.className = 'entry-canvas'; this.renderer.domElement.tabIndex = 0;
    this.renderer.domElement.setAttribute('aria-label', spec.labels.prompt!);
    this.root.append(this.atmosphere, this.renderer.domElement, this.spirit, this.names, header,
      el('footer', {}, this.status, this.progress, el('p', { text: spec.labels.prompt }), this.roster), this.card);
    this.input = new Input(this.renderer.domElement, {
      press: () => {}, click: (x, y) => this.ground(x, y), zoom: () => {}, orbit: () => {}, released: () => this.model.suspend(),
    }, 100);
    const bind = () => this.input.bind(settings.catalog.actions.filter(a => a.held && a.id.startsWith('move_')), a => settings.bindings(a));
    bind(); this.unbindSettings = settings.onChange(bind);
    this.input.enabled = () => this.active() && this.model.opened < 0;
    this.root.addEventListener('keydown', this.key);
    this.frame = requestAnimationFrame(this.tick);
  }

  // DOM focus can be prepared while the browser window is in the background; input must still wait.
  private visible(): boolean { return this.alive && this.root.isConnected && this.root.getClientRects().length > 0 && !this.root.closest('[inert]') && !this.suspended(); }
  private active(): boolean { return this.visible() && !document.hidden && document.hasFocus(); }
  show(list: CharacterListMsg): void {
    // Preserve the newer card when a dismissed creation request replies late.
    if (this.pending && list.request_id != null && list.request_id !== this.pending) return;
    this.notice = null;
    const wake = this.pending && list.request_id === this.pending ? list.created_character_id : null;
    this.pending = null; this.clearBodies(); this.model.setChoices(entryChoices(list, this.kinds.get('callings') ?? [], this.spec));
    clear(this.names); clear(this.roster); this.setCardModal(false); this.shown = -1;
    for (const [i, seat] of this.model.seats.entries()) {
      const body = this.body(seat.choice.look, i + 1, this.spec.bodyHeight);
      if (body) { body.root.position.set(seat.x / this.spec.units, 0, seat.y / this.spec.units); } this.bodies.push(body);
      const label = el('span', { text: seat.choice.name, class: seat.choice.available ? '' : 'unavailable' });
      this.names.append(label);
      const b = el('button', { type: 'button', text: seat.choice.name, 'data-vessel': seat.choice.id, 'aria-label': `${seat.choice.character ? this.spec.labels.saved : this.spec.labels.new}: ${seat.choice.name}${seat.choice.available ? '' : ' · ' + seat.choice.description}` });
      b.onclick = () => { this.model.open(i); this.updateCard(); }; this.roster.append(b);
    }
    this.status.textContent = this.model.seats.length ? '' : this.spec.labels.empty!;
    if (this.visible()) this.root.focus();
    if (wake && list.characters.some(c => c.id === wake)) this.handlers.enter(wake);
  }

  focusCharacter(id: string): void { const i = this.model.seats.findIndex(s => s.choice.character === id); if (i >= 0 && this.model.opened < 0 && this.visible()) (this.roster.children[i] as HTMLElement).focus(); }
  error(text: string): void {
    this.notice = text; this.status.textContent = text;
    if (!this.pending) return;
    this.pending = null; this.shown = -1; this.updateCard();
  }
  private closeCard(): void { this.notice = null; this.status.textContent = ''; this.pending = null; this.model.close(); this.updateCard(); }
  private body(look: string, key: number, height: number): BodyMesh | null {
    const def = this.appearance.catalog.resolve(look, look, ['humanoid']);
    if (!def) return null;
    const state = { look, pattern: look, height, radius: this.spec.bodyWidth } as EntityState;
    const body = new BodyMesh(this.appearance.planFor(state, def, false, key).plan);
    const pose = new Pose(); pose.opacity = this.spec.apparitionOpacity; body.apply(pose);
    this.scene.add(body.root); return body;
  }
  private clearBodies(): void { for (const b of this.bodies) { if (b) { this.scene.remove(b.root); b.dispose(); } } this.bodies.length = 0; }

  private ground(x: number, y: number): void {
    if (!this.active() || this.model.opened >= 0) return;
    const ray = new Raycaster(); ray.setFromCamera(new Vector2(x / this.root.clientWidth * 2 - 1, 1 - y / this.root.clientHeight * 2), this.camera);
    const p = ray.ray.intersectPlane(new Plane(new Vector3(0, 1, 0), 0), new Vector3());
    if (p) this.model.moveTo(p.x * this.spec.units, p.z * this.spec.units);
  }
  private updateCard(): void {
    if (this.shown === this.model.opened) return;
    if (this.model.opened >= 0 && this.card.hidden) this.focusBeforeCard = document.activeElement as HTMLElement;
    this.shown = this.model.opened; this.input.releaseAll(); this.movementArmed = false; this.padArmed = false; clear(this.card);
    this.setCardModal(this.shown >= 0);
    if (this.shown < 0) { this.focusBeforeCard?.focus(); return; }
    const choice = this.model.seats[this.shown]!.choice;
    const name = el('input', { placeholder: this.spec.labels.name, 'aria-label': this.spec.labels.name, maxlength: '24', autocomplete: 'off' });
    const wake = el('button', { type: 'button', text: choice.character ? this.spec.labels.wake : this.spec.labels.create });
    wake.disabled = !choice.available || !!this.pending;
    const close = el('button', { type: 'button', text: this.spec.labels.back });
    close.onclick = () => this.closeCard();
    wake.onclick = () => {
      if (this.pending || !choice.available) return;
      this.notice = null; this.status.textContent = '';
      if (choice.character) { this.handlers.enter(choice.character); return; }
      if (name.value.trim().length < 2) { name.reportValidity(); name.focus(); return; }
      this.pending = `entry-${++this.request}`; wake.disabled = true; name.disabled = true;
      this.status.textContent = this.spec.labels.pending!;
      this.handlers.create(name.value.trim(), choice.calling, this.pending);
    };
    name.required = true; name.minLength = 2;
    name.onkeydown = e => { if (e.key === 'Enter') { e.preventDefault(); wake.click(); } };
    this.card.append(el('h2', { text: choice.name }), el('p', { text: choice.description }));
    if (!choice.character && choice.available) this.card.append(name);
    this.card.append(wake, close); (choice.character && choice.available ? wake : !choice.available ? close : name).focus();
  }
  private setCardModal(open: boolean): void {
    this.card.hidden = !open;
    for (const child of this.root.children) if (child instanceof HTMLElement && child !== this.card) child.inert = open;
  }
  private key = (e: KeyboardEvent): void => {
    if (!this.active()) return;
    if (e.key === 'Escape' && this.model.opened >= 0) { e.preventDefault(); this.closeCard(); }
    if (e.key === 'Tab' && this.model.opened >= 0) {
      const controls = [...this.card.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled)')];
      const first = controls[0], last = controls.at(-1);
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last?.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first?.focus(); }
    }
  };
  private pollMenuPad(): void {
    const pad = [...(navigator.getGamepads?.() ?? [])].find(p => p?.connected);
    if (!pad) { this.previousButtons = []; this.padArmed = false; return; }
    const buttons = pad.buttons.map(b => b.pressed);
    if (!buttons.some(Boolean)) this.padArmed = true;
    const pressed = (i: number) => this.padArmed && buttons[i] && !this.previousButtons[i];
    const controls = this.model.opened >= 0
      ? [...this.card.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled)')]
      : [...this.roster.querySelectorAll<HTMLElement>('button')];
    const step = pressed(15) || pressed(13) ? 1 : pressed(14) || pressed(12) ? -1 : 0;
    if (step && controls.length) { const i = controls.indexOf(document.activeElement as HTMLElement); controls[(i + step + controls.length) % controls.length]?.focus(); }
    if (pressed(0) && document.activeElement instanceof HTMLButtonElement && this.root.contains(document.activeElement)) document.activeElement.click();
    if (pressed(1) && this.model.opened >= 0) this.closeCard();
    this.previousButtons = buttons;
  }
  private tick = (now: number): void => {
    if (!this.alive) return;
    const dt = (now - this.last) / 1000; this.last = now;
    const active = this.active();
    if (!active) { if (this.wasActive) this.input.releaseAll(); this.wasActive = false; this.padArmed = this.movementArmed = false; this.frame = requestAnimationFrame(this.tick); return; }
    if (!this.wasActive) this.input.releaseAll(); this.wasActive = true;
    this.input.pollPad();
    const dx = Number(this.input.isHeld('move_right')) - Number(this.input.isHeld('move_left')), dy = Number(this.input.isHeld('move_back')) - Number(this.input.isHeld('move_forward'));
    if (dx === 0 && dy === 0 && this.model.opened < 0) this.movementArmed = true;
    this.model.step(dt, dx, dy, Input.typing() || !this.movementArmed);
    this.pollMenuPad();
    this.updateCard();
    const width = this.root.clientWidth, height = this.root.clientHeight, span = this.spec.cameraSpan / this.spec.units;
    this.renderer.setSize(width, height, false);
    this.camera.left = -span * width / height / 2; this.camera.right = -this.camera.left; this.camera.top = span / 2; this.camera.bottom = -span / 2;
    const x = this.model.x / this.spec.units, z = this.model.y / this.spec.units;
    this.camera.position.set(x, this.spec.cameraHeight, z + this.spec.cameraOffset); this.camera.lookAt(x, 0, z - this.spec.cameraLead); this.camera.updateProjectionMatrix(); this.camera.updateMatrixWorld();
    this.model.seats.forEach((s, i) => {
      const b = this.bodies[i]; if (b) b.root.rotation.y = -s.yaw;
      const p = new Vector3(s.x / this.spec.units, this.spec.bodyHeight + 0.2, s.y / this.spec.units).project(this.camera);
      const label = this.names.children[i] as HTMLElement; label.style.left = `${(p.x + 1) * width / 2}px`; label.style.top = `${(1 - p.y) * height / 2}px`;
    });
    this.time += Math.min(dt, this.spec.maxFrame);
    const still = this.appearance.reducedMotion, time = still ? 0 : this.time;
    for (const canvas of [this.atmosphere, this.spirit]) { if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; } }
    const ambient = this.atmosphere.getContext('2d')!; ambient.clearRect(0, 0, width, height);
    abyss(ambient, width, height, time, 1, this.spec.palette.ink!, still ? 0 : this.model.x, still ? 0 : this.model.y);
    const soul = this.spirit.getContext('2d')!; soul.clearRect(0, 0, width, height);
    const at = new Vector3(x, .1, z).project(this.camera);
    const source = this.kinds.get('ui_loading_screens')?.find(d => d.id === this.spec.wispSource)?.meta as unknown as typeof presentation;
    soul.save(); soul.translate((at.x + 1) * width / 2, (1 - at.y) * height / 2); soul.rotate(-Math.PI / 2);
    drawWisp(soul, time, { ...source.wisp, radius: this.spec.wispRadius * height / this.spec.cameraSpan }); soul.restore();
    this.progress.value = this.model.progress; this.progress.hidden = this.model.engaged < 0 || this.model.opened >= 0;
    if (!this.notice && this.model.engaged >= 0 && this.model.opened < 0) this.status.textContent = this.model.seats[this.model.engaged]!.choice.name;
    this.renderer.render(this.scene, this.camera);
    this.frame = requestAnimationFrame(this.tick);
  };
  snapshot() {
    return { x: this.model.x, y: this.model.y, opened: this.model.opened, pending: this.pending, progress: this.model.progress,
      seats: this.model.seats.map(s => { const p = new Vector3(s.x / this.spec.units, 0, s.y / this.spec.units).project(this.camera); return {
        ...s.choice, x: s.x, y: s.y, screen: { x: (p.x + 1) * this.root.clientWidth / 2, y: (1 - p.y) * this.root.clientHeight / 2 },
      }; }) };
  }
  dispose(): void { this.alive = false; cancelAnimationFrame(this.frame); this.unbindSettings(); this.input.dispose(); this.clearBodies(); this.renderer.dispose(); this.renderer.forceContextLoss(); this.root.removeEventListener('keydown', this.key); this.root.remove(); }
}
