import { DirectionalLight, FogExp2, HemisphereLight, MathUtils, Scene, SRGBColorSpace, Vector3, WebGLRenderer } from 'three';
import type { Action, ClientConfig } from './config';
import { Input } from './input/input';
import { Connection } from './net/connection';
import { PROTOCOL_VERSION, type CombatEventMsg, type EntityState, type Int64, type Message } from './net/protocol';
import { Hud } from './ui/hud';
import { parseServerInput, sameOrigin, socketUrl, type ServerAddress } from './ui/address';
import { el, storageGet, storageSet } from './ui/dom';
import { LoginPanel } from './ui/login';
import { CameraRig } from './view/camera';
import { chunkFromMessage } from './world/chunks';
import { linearColor } from './world/colors';
import { groundToCore, toThree, type CorePoint } from './world/coords';
import { EntityLayer, isDrop, type EntityView } from './world/entities';
import { ServerClock } from './world/interpolation';
import { SurfacePalette } from './world/palette';
import { TerrainView } from './world/terrain';

export const CLIENT_NAME = 'hollow-wake-web 0.1';

const STORAGE_SERVER = 'hollowwake.server';
const STORAGE_ACCOUNT = 'hollowwake.account';

/**
 * The client's root, the browser twin of the desktop Main: owns the connection, the terrain and entity views,
 * the camera rig and the UI, handles messages and turns input into intents. Everything the player sees comes
 * from server state; everything the player does is a request the server may refuse.
 */
export class App {
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly sun: DirectionalLight;
  private readonly sunOffset: Vector3;
  readonly rig: CameraRig;
  readonly terrain: TerrainView;
  readonly entities: EntityLayer;
  readonly hud: Hud;
  readonly login: LoginPanel;
  private readonly input: Input;
  readonly connection: Connection;
  private readonly clock = new ServerClock();
  private readonly canvasHost: HTMLElement;
  private width = 1;
  private height = 1;
  inWorld = false;
  private zoneName = '';
  private worldKey = '';
  private lastMoveDir: [number, number] = [0, 0];
  private lastPingAt = 0;
  private lastFrame = 0;
  private rtt: number | null = null;
  /** Smoothed time spent in one frame's work, in milliseconds, for scripted checks. */
  private frameMs = 0;
  hoveredKey: Int64 = 0;
  private lastRejected = '';
  lastInteracted = '';
  combatLines = 0;
  /** Damage events the player's own entity dealt. */
  hitsDealt = 0;
  lastHit = '';
  questOffers = 0;
  characterLists = 0;

  constructor(root: HTMLElement, readonly config: ClientConfig) {
    this.canvasHost = el('div', { class: 'world' });
    const labels = el('div', { class: 'labels' });
    root.append(this.canvasHost, labels);

    this.renderer = new WebGLRenderer({ antialias: config.render.antialias, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, config.render.maxPixelRatio));
    this.renderer.outputColorSpace = SRGBColorSpace;
    this.renderer.shadowMap.enabled = config.render.shadows;
    this.canvasHost.append(this.renderer.domElement);

    this.scene.background = linearColor(config.render.background);
    if (config.render.fogDensity > 0) {
      this.scene.fog = new FogExp2(linearColor(config.render.fogColor), config.render.fogDensity);
    }
    this.scene.add(new HemisphereLight(linearColor(config.render.ambientSky), linearColor(config.render.ambientGround), config.render.ambientIntensity));
    this.sun = new DirectionalLight(linearColor(config.render.sunColor), config.render.sunIntensity);
    // The light shines along its -Z turned by pitch then yaw, as the desktop sun's rotation does.
    const pitch = MathUtils.degToRad(config.render.sunPitchDegrees);
    const yaw = MathUtils.degToRad(config.render.sunYawDegrees);
    const shine = new Vector3(0, Math.sin(pitch), -Math.cos(pitch)).applyAxisAngle(new Vector3(0, 1, 0), yaw);
    this.sunOffset = shine.clone().multiplyScalar(-80);
    this.sun.castShadow = config.render.shadows;
    if (config.render.shadows) {
      const s = this.sun.shadow.camera;
      s.left = -60;
      s.right = 60;
      s.top = 60;
      s.bottom = -60;
      s.far = 300;
      this.sun.shadow.mapSize.set(2048, 2048);
    }
    this.scene.add(this.sun, this.sun.target);

    this.rig = new CameraRig(config.camera);
    const palette = new SurfacePalette(config.terrain);
    this.terrain = new TerrainView(config.world, palette, (x, y) => this.send({ t: 'request_chunk', x, y }));
    this.scene.add(this.terrain.group);
    this.entities = new EntityLayer(config.entities, config.motion, labels);
    this.scene.add(this.entities.group);

    this.hud = new Hud(config.ui, {
      slot: (slot) => this.useSlot(slot),
      chat: (text) => this.send({ t: 'chat', channel: 'zone', text }),
      accept: (quest, giver) => this.send({ t: 'accept_quest', quest, giver }),
      turnIn: (quest, giver) => this.send({ t: 'turn_in_quest', quest, giver }),
    });
    this.login = new LoginPanel({
      connect: (address, account, token) => void this.connect(address, account, token),
      create: (name, calling) => this.send({ t: 'create_character', name, calling }),
      enter: (characterId) => this.send({ t: 'select_character', character_id: characterId }),
    });
    root.append(this.hud.root, this.login.root);

    this.connection = new Connection((m) => this.handle(m), (reason) => this.closed(reason));
    this.input = new Input(config.input.bindings, this.renderer.domElement, {
      action: (a) => this.action(a),
      click: (x, y) => this.click(x, y),
      zoom: (steps) => this.rig.zoom(steps),
      orbit: (dx, dy) => this.rig.orbit(dx, dy),
      released: () => this.handleMovement(true),
    }, config.camera.wheelStepPixels);

    window.addEventListener('resize', () => this.resize());
    this.resize();
  }

  // ------------------------------------------------------------ startup and connection

  /** Fills the login panel: a ?server= link, then config.json, then a game server at this page's origin, then the last one used. */
  async prefill(): Promise<void> {
    const params = new URLSearchParams(window.location.search);
    const s = this.config.server;
    const pageSecure = window.location.protocol === 'https:';
    const configured: ServerAddress = {
      host: s.host,
      port: s.port > 0 ? s.port : 0,
      secure: s.secure ?? pageSecure,
      path: s.path,
    };
    let address: ServerAddress | null = null;
    let note = '';
    const fromLink = params.get('server');
    if (fromLink) {
      address = parseServerInput(fromLink, { ...configured, port: configured.port || 7778 });
      note = 'Server from the link.';
    } else if (s.host) {
      address = { ...configured, port: configured.port || (configured.secure ? 443 : 7778) };
    } else if (s.probeSameOrigin && (await this.gameServerHere(s.path))) {
      address = sameOrigin(window.location, s.path);
      note = 'This page is served by the game server.';
    } else {
      const remembered = storageGet(STORAGE_SERVER);
      if (remembered) {
        address = parseServerInput(remembered, { ...configured, port: 7778 });
        note = 'Last server used.';
      }
    }
    if (address === null) {
      address = { host: '', port: 7778, secure: pageSecure, path: s.path };
      note = 'Enter the address of a running server: a host and port, or the wss:// address of a Codespace.';
    }
    this.login.setAddress(address);
    this.login.setAccount(params.get('account') ?? storageGet(STORAGE_ACCOUNT) ?? s.account);
    this.login.setStatus(note || 'Not connected.');
  }

  /** A game server answers a plain GET on its socket path with a description naming the service. */
  private async gameServerHere(path: string): Promise<boolean> {
    if (!window.location.protocol.startsWith('http')) {
      return false;
    }
    try {
      const response = await fetch(new URL(path, window.location.origin), { cache: 'no-store' });
      if (!response.ok) {
        return false;
      }
      const about = (await response.json()) as { service?: unknown; protocol?: unknown };
      if (about.service !== 'hollow-wake') {
        return false;
      }
      if (about.protocol !== PROTOCOL_VERSION) {
        console.warn(`the server here speaks protocol ${String(about.protocol)}, this client ${PROTOCOL_VERSION}`);
      }
      return true;
    } catch {
      return false;
    }
  }

  private async connect(address: ServerAddress, account: string, token: string): Promise<void> {
    const url = socketUrl(address);
    this.login.setBusy(true);
    this.login.setStatus(`Connecting to ${url}...`);
    try {
      await this.connection.connect(url);
    } catch (error) {
      this.login.setStatus(`Could not connect: ${error instanceof Error ? error.message : String(error)}`);
      this.login.setBusy(false);
      return;
    }
    storageSet(STORAGE_SERVER, url);
    storageSet(STORAGE_ACCOUNT, account);
    this.send({ t: 'hello', client_version: CLIENT_NAME, protocol: PROTOCOL_VERSION });
    this.send({ t: 'login', account, token });
    this.login.setStatus('Logging in...');
  }

  private closed(reason: string): void {
    this.inWorld = false;
    this.hud.show(false);
    this.login.show(true);
    this.login.setBusy(false);
    this.login.setStatus(`Disconnected: ${reason}`);
    this.entities.clear();
    this.lastMoveDir = [0, 0];
  }

  send(message: Message): void {
    this.connection.send(message);
  }

  // ------------------------------------------------------------ messages

  private handle(m: Message): void {
    const local = performance.now() / 1000;
    switch (m.t) {
      case 'welcome':
        this.login.setStatus(`Connected to ${m.server_name}: ${m.motd}`);
        break;
      case 'login_result':
        this.login.setStatus(m.ok ? 'Logged in. Pick a character.' : `Login failed: ${m.error ?? 'refused'}`);
        if (!m.ok) {
          this.login.setBusy(false);
        }
        break;
      case 'character_list':
        this.characterLists++;
        this.login.setBusy(false);
        this.login.showCharacters(m);
        break;
      case 'error':
        if (m.code === 'chunk_out_of_range') {
          console.debug(m.text);
          break;
        }
        this.hud.notify(m.text, 'error');
        this.login.setStatus(m.text);
        break;
      case 'disconnect':
        this.connection.close(m.reason);
        this.closed(m.reason);
        break;
      case 'enter_world': {
        const key = `${m.world.chunk_size}:${m.world.content_hash}:${m.world.seed}`;
        if (key !== this.worldKey) {
          this.worldKey = key;
          this.terrain.reset(m.world.chunk_size);
        }
        this.entities.clear();
        this.entities.selfKey = m.entity_key;
        this.clock.reset();
        this.clock.observe(m.server_time, local);
        this.inWorld = true;
        this.login.show(false);
        this.hud.show(true);
        this.rig.follow(toThree(m.x, m.y, m.z));
        this.hud.line(`Entered the world at ${Math.round(m.x)}, ${Math.round(m.y)} (zone ${m.zone}).`, 'system');
        break;
      }
      case 'zone_changed':
        this.zoneName = `${m.zone_name} (L${m.level}${m.safe ? ', safe' : ''})`;
        this.hud.notify(m.zone_name, 'discovery');
        break;
      case 'entity_spawn':
        this.entities.spawn(m.state, this.clock.now(local), local);
        break;
      case 'entity_update':
        this.clock.observe(m.server_time, local);
        for (const state of m.states) {
          this.entities.update(state, m.server_time, local);
        }
        break;
      case 'entity_despawn':
        this.entities.despawn(m.key);
        break;
      case 'self_state':
        this.hud.applySelf(m, this.zoneName);
        break;
      case 'combat_event':
        this.showCombat(m);
        break;
      case 'chat_broadcast':
        this.hud.chat(m.from, m.channel, m.text);
        break;
      case 'notice':
        this.hud.notify(m.text, m.level);
        break;
      case 'quest_offers':
        this.questOffers++;
        this.hud.showQuestOffers(m);
        break;
      case 'intent_rejected':
        this.lastRejected = `${m.intent}: ${m.reason}`;
        this.hud.notify(this.lastRejected, 'error');
        break;
      case 'chunk_data':
        this.terrain.receive(chunkFromMessage(m));
        break;
      case 'craft_challenge':
        this.hud.line(`Crafting ${m.recipe}: ${m.type} challenge (${Math.round(m.time_limit)}s)`, 'system');
        break;
      case 'craft_result':
        this.hud.notify(m.success ? `Crafted ${m.item?.name ?? m.recipe} (quality ${m.quality.toFixed(2)})` : `Craft failed: ${m.reason}`, m.success ? 'quest' : 'error');
        break;
      case 'pong':
        this.rtt = performance.now() - Number(m.nonce);
        break;
      default:
        break;
    }
  }

  private showCombat(c: CombatEventMsg): void {
    const self = this.entities.selfKey;
    if (c.source !== self && c.target !== self) {
      return;
    }
    const name = (key: Int64) => this.entities.views.get(key)?.state.name ?? '?';
    const source = name(c.source);
    const target = name(c.target);
    const text = c.kind === 'damage' ? `${source} hits ${target} for ${Math.round(c.amount)}${c.crit ? ' (crit)' : ''}`
      : c.kind === 'evade' ? `${target} evades ${source}`
      : c.kind === 'block' ? `${target} blocks ${source}`
      : c.kind === 'heal' ? `${source} heals ${target} for ${Math.round(c.amount)}`
      : c.kind === 'status' ? `${target} is ${c.status ?? ''}`
      : c.kind === 'death' ? `${target} dies`
      : c.kind;
    this.hud.line(text, 'combat');
    this.combatLines++;
    if (c.kind === 'damage' && c.source === self) {
      this.hitsDealt++;
      this.lastHit = target;
    }
  }

  // ------------------------------------------------------------ input

  private action(action: Action): void {
    if (!this.inWorld) {
      return;
    }
    switch (action) {
      case 'chat':
        this.hud.focusChat();
        return;
      case 'jump':
        this.send({ t: 'jump' });
        return;
      case 'camera_toggle':
        this.rig.toggle();
        return;
      case 'camera_rotate_left':
        this.rig.rotateStep(1);
        return;
      case 'camera_rotate_right':
        this.rig.rotateStep(-1);
        return;
      case 'interact':
        this.interactNearest('interact');
        return;
      case 'pick_up':
        this.interactNearest('pick_up');
        return;
      case 'respawn':
        this.send({ t: 'respawn' });
        return;
      case 'toggle_sheet':
        this.hud.toggleSheet();
        return;
      case 'cancel':
        this.send({ t: 'cancel_cast' });
        this.hud.hideQuests();
        return;
      default:
        if (action.startsWith('slot_')) {
          this.useSlot(Number(action.slice(5)) - 1);
        }
    }
  }

  /** Held movement relative to the camera, sent only when the direction changes. */
  private handleMovement(released = false): void {
    if (!this.inWorld) {
      return;
    }
    let ix = 0;
    let iy = 0;
    if (!released && !Input.typing()) {
      iy += (this.input.isHeld('move_forward') ? 1 : 0) - (this.input.isHeld('move_back') ? 1 : 0);
      ix += (this.input.isHeld('move_right') ? 1 : 0) - (this.input.isHeld('move_left') ? 1 : 0);
    }
    let dir: [number, number] = [0, 0];
    if (ix !== 0 || iy !== 0) {
      const [fx, fz] = this.rig.groundForward();
      const [rx, rz] = this.rig.groundRight();
      let gx = fx * iy + rx * ix;
      let gz = fz * iy + rz * ix;
      const len = Math.hypot(gx, gz);
      gx /= len;
      gz /= len;
      const core = groundToCore(gx, gz);
      // Rounded so a camera still easing into place does not send a stream of nearly equal directions.
      dir = [Math.round(core.x * 1000) / 1000, Math.round(core.y * 1000) / 1000];
    }
    if (dir[0] !== this.lastMoveDir[0] || dir[1] !== this.lastMoveDir[1]) {
      this.lastMoveDir = dir;
      this.send(dir[0] === 0 && dir[1] === 0 ? { t: 'stop' } : { t: 'move_dir', x: dir[0], y: dir[1] });
    }
  }

  private mouseRay() {
    return this.rig.ray(this.input.mouse.x, this.input.mouse.y, this.width, this.height);
  }

  private click(x: number, y: number): void {
    if (!this.inWorld) {
      return;
    }
    const { origin, direction } = this.rig.ray(x, y, this.width, this.height);
    const picked = this.entities.pick(origin, direction);
    if (picked !== null && picked.state.interactable != null && picked.state.interactable !== 'opened') {
      this.send(isDrop(picked.state) ? { t: 'pick_up', drop: picked.key } : { t: 'interact', target: picked.key });
      return;
    }
    if (picked !== null && isDrop(picked.state)) {
      this.send({ t: 'pick_up', drop: picked.key });
      return;
    }
    const ground = this.terrain.rayToGround(origin, direction);
    if (ground !== null) {
      this.send({ t: 'move_to', x: ground.x, y: ground.y, z: ground.z });
      this.lastMoveDir = [0, 0];
    }
  }

  private updateHover(): void {
    if (!this.input.mouse.over) {
      return;
    }
    const { origin, direction } = this.mouseRay();
    const hovered = this.entities.pick(origin, direction);
    this.hoveredKey = hovered?.key ?? 0;
    if (hovered === null) {
      this.hud.setTarget('');
      return;
    }
    const s = hovered.state;
    const life = s.resources.life !== undefined && s.resource_max.life !== undefined ? `  ${Math.round(s.resources.life)}/${Math.round(s.resource_max.life)}` : '';
    this.hud.setTarget(`${s.name}${s.level > 0 ? ` [${s.level}]` : ''}${life}${s.interactable != null ? `  (${s.interactable})` : ''}`);
  }

  /** Weave slots aim at the ground under the cursor and target whatever is hovered, as on the desktop. */
  useSlot(slot: number): void {
    if (!this.inWorld) {
      return;
    }
    const { origin, direction } = this.mouseRay();
    const ground = this.terrain.rayToGround(origin, direction);
    const self = this.entities.self;
    const at = self?.position ?? { x: 0, y: 0, z: 0 };
    const point = ground ?? at;
    this.send({ t: 'use_weave', slot, target: this.hoveredKey, x: point.x, y: point.y, z: point.z, dir_x: point.x - at.x, dir_y: point.y - at.y });
  }

  private interactNearest(kind: 'interact' | 'pick_up'): void {
    const self = this.entities.self;
    if (self === undefined) {
      return;
    }
    let best: EntityView | null = null;
    let bestDist = Number.POSITIVE_INFINITY;
    for (const view of this.entities.views.values()) {
      if (view.key === this.entities.selfKey) {
        continue;
      }
      const drop = isDrop(view.state);
      if (kind === 'pick_up' ? !drop : view.state.interactable == null || drop) {
        continue;
      }
      const d = view.root.position.distanceTo(self.root.position);
      if (d < bestDist && d < this.config.input.interactReach) {
        bestDist = d;
        best = view;
      }
    }
    if (best === null) {
      this.lastInteracted = 'nothing within reach';
      return;
    }
    this.lastInteracted = `${best.state.name} (${best.state.interactable ?? 'drop'}) at ${bestDist.toFixed(1)}`;
    this.send(kind === 'pick_up' ? { t: 'pick_up', drop: best.key } : { t: 'interact', target: best.key });
  }

  takeRejected(): string {
    const r = this.lastRejected;
    this.lastRejected = '';
    return r;
  }

  // ------------------------------------------------------------ frame

  private resize(): void {
    this.width = Math.max(1, window.innerWidth);
    this.height = Math.max(1, window.innerHeight);
    this.renderer.setSize(this.width, this.height);
    this.rig.resize(this.width, this.height);
  }

  start(): void {
    this.lastFrame = performance.now() / 1000;
    this.renderer.setAnimationLoop((time) => this.frame(time / 1000));
  }

  private frame(local: number): void {
    const started = performance.now();
    this.work(local);
    this.frameMs += (performance.now() - started - this.frameMs) * 0.05;
  }

  private work(local: number): void {
    const dt = Math.min(0.1, Math.max(0, local - this.lastFrame));
    this.lastFrame = local;
    const renderTime = this.clock.now(local) - this.config.motion.interpolationDelayMs / 1000;
    this.entities.animate(dt, local, renderTime);
    if (this.inWorld) {
      const self = this.entities.self;
      if (self !== undefined) {
        this.rig.follow(self.root.position);
        this.sun.target.position.copy(self.root.position);
        this.sun.position.copy(self.root.position).add(this.sunOffset);
        this.terrain.update(self.position, local);
      }
      this.handleMovement();
      this.updateHover();
      if (local - this.lastPingAt > this.config.world.pingSeconds) {
        this.lastPingAt = local;
        this.send({ t: 'ping', nonce: Math.round(performance.now()) });
      }
      this.hud.setNet(`${this.rtt === null ? '' : `${Math.round(this.rtt)} ms  `}${this.terrain.builtCount} chunks  ${this.entities.views.size} entities`);
    }
    this.rig.update(dt);
    this.entities.placeLabels(this.rig.camera, this.width, this.height);
    this.hud.frame(local);
    this.renderer.render(this.scene, this.rig.camera);
  }

  // ------------------------------------------------------------ scripted checks

  /** Read-only views for the scripted smoke run and the browser console; nothing here changes the game. */
  probe() {
    const app = this;
    const stateOf = (v: EntityView) => ({ ...v.state, drawn: v.position });
    return {
      get inWorld() {
        return app.inWorld;
      },
      get status() {
        return app.login.statusText;
      },
      get counts() {
        return {
          characterLists: app.characterLists,
          questOffers: app.questOffers,
          combatLines: app.combatLines,
          hitsDealt: app.hitsDealt,
          chunksBuilt: app.terrain.builtCount,
          chunksPending: app.terrain.pendingCount,
          chunksRequested: app.terrain.requested,
          entities: app.entities.views.size,
          frameMs: Math.round(app.frameMs * 100) / 100,
        };
      },
      get ui() {
        return { questsVisible: app.hud.questsVisible, sheetVisible: app.hud.sheetVisible, target: app.hud.targetText, cameraMode: app.rig.mode, cameraYaw: app.rig.yaw };
      },
      get hoveredKey() {
        return app.hoveredKey;
      },
      get lastInteracted() {
        return app.lastInteracted;
      },
      get lastHit() {
        return app.lastHit;
      },
      characters: () => app.login.characters,
      callings: () => app.login.callings,
      self: (): (EntityState & { drawn: CorePoint }) | null => {
        const s = app.entities.self;
        return s === undefined ? null : stateOf(s);
      },
      entities: () => [...app.entities.views.values()].map(stateOf),
      /** Where an entity's middle is on screen, in CSS pixels. */
      screenOf: (key: Int64) => {
        const v = app.entities.views.get(key);
        if (v === undefined) {
          return null;
        }
        const p = v.root.position.clone();
        p.y += v.state.height * 0.5;
        return app.rig.project(p, app.width, app.height);
      },
      screenOfPoint: (x: number, y: number, z: number) => app.rig.project(toThree(x, y, z), app.width, app.height),
      heightAt: (x: number, y: number) => app.terrain.heightAt(x, y) ?? null,
      takeRejected: () => app.takeRejected(),
      /** Travel helper for scripts (the desktop smoke sends move_to the same way); players use input. */
      moveTo: (x: number, y: number) => app.send({ t: 'move_to', x, y, z: app.terrain.heightAt(x, y) ?? 0 }),
      stop: () => app.send({ t: 'stop' }),
    };
  }
}
