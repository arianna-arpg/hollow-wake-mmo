import { presentation } from './loading/content';
import type { LoadingScreen, LoadingLease } from './loading/loadingScreen';
import { Color, DirectionalLight, FogExp2, HemisphereLight, MathUtils, Scene, SRGBColorSpace, Vector3, WebGLRenderer } from 'three';
import type { ClientConfig } from './config';
import { Input } from './input/input';
import { Connection } from './net/connection';
import { applyDelta } from './net/delta';
import { CAST_CUES_FEATURE, ENTITY_DELTA_FEATURE, INTERFACE_FEATURE, PROTOCOL_VERSION, type CombatEventMsg, type EntityState, type Int64, type Message } from './net/protocol';
import { CastVisuals } from './world/castVisuals';
import { cueSettings } from './world/castCueTimeline';
import { parseServerInput, sameOrigin, socketUrl, type ServerAddress } from './ui/address';
import { el, storageGet, storageSet } from './ui/dom';
import { Hud } from './ui/hud';
import { bundledKinds, loadNames, loadUi } from './ui/logic/content';
import { gateFacts, OptionsCatalog, Settings, type OptionEntry } from './ui/logic/options';
import { LoginPanel } from './ui/login';
import { CameraRig } from './view/camera';
import { bodyLight } from './world/appearance/bodyMesh';
import { AudioCues } from './world/audioCues';
import { DayCycle, type SkyState } from './world/appearance/dayCycle';
import { SurfaceLook } from './world/appearance/surfaceLook';
import { chunkFromMessage } from './world/chunks';
import { linearColor } from './world/colors';
import { groundToCore, toThree, type CorePoint } from './world/coords';
import { AppearanceService, EntityLayer, isDrop, type BodiesMode, type EntityView } from './world/entities';
import { ServerClock } from './world/interpolation';
import { toLinear } from './world/palette';
import { TerrainView } from './world/terrain';

export const CLIENT_NAME = 'hollow-wake-web 0.1';

const STORAGE_SERVER = 'hollowwake.server';
const STORAGE_ACCOUNT = 'hollowwake.account';
const STORAGE_SETTINGS = 'hollowwake.settings.';

/**
 * The client's root, the browser twin of the desktop Main: owns the connection, the terrain and entity views,
 * the camera rig, the interface and the player's settings, handles messages and turns input into intents.
 * Everything the player sees comes from server state; everything the player does is a request the server may
 * refuse. Preferences are the Options content (ui_options) stored per profile in this browser, and every binding
 * is an input action from content with the player's rebinds over it.
 */
export class App {
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly sun: DirectionalLight;
  private readonly hemisphere: HemisphereLight;
  private sunOffset: Vector3;
  private readonly fog: FogExp2 | null = null;
  private readonly day: DayCycle;
  /** A held fraction of the day for previews (the developer options), or null for the server's clock. */
  dayOverride: number | null = null;
  private lastDayFraction = -1;
  private fogOn = true;
  readonly rig: CameraRig;
  readonly terrain: TerrainView;
  readonly entities: EntityLayer;
  readonly appearance: AppearanceService;
  readonly audio = new AudioCues();
  readonly castVisuals: CastVisuals;
  readonly hud: Hud;
  readonly login: LoginPanel;
  readonly settings: Settings;
  readonly catalog: OptionsCatalog;
  private readonly input: Input;
  readonly connection: Connection;
  private readonly clock = new ServerClock();
  private readonly canvasHost: HTMLElement;
  private readonly profile: string;
  private readonly developer: boolean;
  private readonly appliers = new Map<string, () => void>();
  private width = 1;
  private height = 1;
  private entryLease: LoadingLease | null = null;
  private entryStarted = 0;
  private entryPosition = { x: 0, y: 0 };
  private entryFailed = false;
  releaseLoadingInput(): void {
    this.input?.releaseAll();
    if (this.inWorld) this.send({ t: 'stop' });
    this.lastMoveDir = [0, 0];
  }
  get reducedLoadingMotion(): boolean { return this.settings?.toggle('reduce_motion') ?? false; }

  private beginEntry(id: string): void {
    this.entryFailed = false;
    this.entryStarted = performance.now() / 1000;
    this.entryLease = this.loading.begin({ kind: 'entry', label: presentation.labels.entry, cancel: () => this.cancelEntry() });
    this.send({ t: 'select_character', character_id: id });
  }
  private cancelEntry(): void {
    this.entryLease?.finish(); this.entryLease = null;
    this.connection.close('Loading cancelled');
    this.closed('Loading cancelled');
  }
  private retryEntry(): void {
    this.entryFailed = false;
    this.entryStarted = performance.now() / 1000;
    if (!this.connection.open) { this.cancelEntry(); return; }
    this.terrain.retryPreparation();
    // The reliable socket already owns delivery; a slow response must not select twice.
    this.entryLease?.update({ label: presentation.labels.entry });
  }
  private checkEntry(now: number): void {
    if (!this.entryLease?.current || this.entryFailed) return;
    const progress = this.terrain.readiness(this.entryPosition.x, this.entryPosition.y, Math.min(presentation.readyRadius, this.config.world.chunkRadius));
    const self = this.entities.self;
    if (this.inWorld) {
      this.entryLease.update({ label: presentation.labels.terrain, completed: progress.completed + Number(!!self), total: progress.total + 1 });
      if (self && progress.completed === progress.total) {
        this.entryLease.finish(); this.entryLease = null; return;
      }
    }
    if (now - this.entryStarted > presentation.timeoutSeconds) {
      this.entryFailed = true;
      this.entryLease.fail(presentation.labels.timeout, () => this.retryEntry());
    }
  }

  inWorld = false;
  /** The zone the hero stands in, as the server names it. */
  zoneName = '';
  private worldKey = '';
  private lastMoveDir: [number, number] = [0, 0];
  private lastPingAt = 0;
  private lastFrame = 0;
  private rtt: number | null = null;
  /** Smoothed time spent in one frame's work, in milliseconds, for scripted checks. */
  private frameMs = 0;
  hoveredKey: Int64 = 0;
  private lastRejected = '';
  /** Hello features the server granted (welcome.features). */
  features: string[] = [];
  lastInteracted = '';
  combatLines = 0;
  /** Damage events the player's own entity dealt. */
  hitsDealt = 0;
  lastHit = '';
  questOffers = 0;
  characterLists = 0;
  /** Interface states received, by message type, for scripted checks. */
  readonly received = new Map<string, number>();

  constructor(root: HTMLElement, readonly config: ClientConfig, readonly loading: LoadingScreen) {
    const params = new URLSearchParams(window.location.search);
    this.developer = params.has('developer');
    // A profile keeps its own settings; ?fresh-settings starts it from the content's defaults (the smoke's way).
    this.profile = (params.get('profile') ?? 'default').replace(/[^A-Za-z0-9_-]/g, '') || 'default';
    const kinds = bundledKinds();
    this.catalog = OptionsCatalog.load(kinds);
    this.settings = Settings.load(this.catalog, params.has('fresh-settings') ? null : storageGet(STORAGE_SETTINGS + this.profile));
    const theme = params.get('theme');
    if (theme !== null) {
      this.settings.set('theme', theme);
    }

    this.canvasHost = el('div', { class: 'world' });
    const labels = el('div', { class: 'labels' });
    root.append(this.canvasHost, labels);

    this.renderer = new WebGLRenderer({ antialias: config.render.antialias, powerPreference: 'high-performance' });
    // The pixel ratio cap applies after a restart, as the option says.
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, this.settings.number('pixel_ratio', config.render.maxPixelRatio)));
    this.renderer.outputColorSpace = SRGBColorSpace;
    this.renderer.shadowMap.enabled = config.render.shadows;
    this.canvasHost.append(this.renderer.domElement);

    this.scene.background = linearColor(config.render.background);
    if (config.render.fogDensity > 0) {
      this.fog = new FogExp2(linearColor(config.render.fogColor), config.render.fogDensity);
      this.scene.fog = this.fog;
    }
    this.hemisphere = new HemisphereLight(linearColor(config.render.ambientSky), linearColor(config.render.ambientGround), config.render.ambientIntensity);
    this.scene.add(this.hemisphere);
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
    // The appearance pipeline: looks, bodies, cards, the surfaces, the day and the motion timings, all from content.
    this.appearance = new AppearanceService(kinds, this.renderer);
    const feel = this.appearance.catalog.motion;
    this.rig.zoomEase = feel.camera('zoom_ease', 9);
    this.rig.shakeSeconds = feel.camera('shake_time', 0.28);
    this.rig.shakeRate = feel.camera('shake_rate', 28);
    this.day = new DayCycle(this.appearance.catalog.world);
    const surfaces = new SurfaceLook(this.appearance.catalog.surfaces);
    this.terrain = new TerrainView(config.world, surfaces, this.day.foliage, (x, y) => this.send({ t: 'request_chunk', x, y }));
    this.scene.add(this.terrain.group);
    this.entities = new EntityLayer(config.entities, config.motion, labels, this.appearance);
    this.castVisuals = new CastVisuals(this.scene, this.entities, cueSettings(kinds.get('ui_cast_visuals')?.[0], kinds.get('threads') ?? []));
    this.scene.add(this.entities.group);
    // the sound hooks: the bodies' moments raise cues at their places; nothing plays until an asset is registered
    this.entities.cue = (cue, variant, position) => {
      const hero = this.entities.self?.root.position;
      this.audio.play(cue, variant, hero ? hero.distanceTo(position) : 0);
    };
    this.entities.surfaceAt = (position) => this.terrain.surfaceAt(position);

    const facts = (name: string): number | null => {
      switch (name) {
        case 'client.web':
          return 1;
        case 'client.desktop':
          return 0;
        case 'client.developer':
          return this.developer ? 1 : 0;
        case 'client.controller':
          return typeof navigator.getGamepads === 'function' && [...navigator.getGamepads()].some((p) => p !== null && p.connected) ? 1 : 0;
        default:
          return null;
      }
    };
    this.hud = new Hud(loadUi(kinds), loadNames(kinds), this.settings, gateFacts(this.settings, facts));
    this.hud.send = (m) => this.send(m);
    this.hud.slotPressed = (slot) => this.useSlot(slot);
    this.hud.casting = () => this.hud.hero.cast !== null;
    this.appearance.selfEquipment = () => this.hud.hero.equipment;
    this.entities.selfCast = () => {
      const cast = this.hud.hero.cast;
      return cast === null ? null : { mode: cast.mode, progress: cast.progress };
    };
    this.hud.heroScreen = () => this.heroScreen();
    this.hud.distanceTo = (key) => this.distanceTo(key);
    this.hud.optionsClosed = () => this.optionsClosed();
    this.login = new LoginPanel({
      connect: (address, account, token) => void this.connect(address, account, token),
      create: (name, calling) => this.send({ t: 'create_character', name, calling }),
      enter: (characterId) => this.beginEntry(characterId),
      options: () => this.hud.openOptions(),
    });
    root.append(this.login.root, this.hud.root);
    // The login panel wears the interface's look and follows it when the theme changes.
    this.hud.skin.apply(this.login.root);
    this.hud.restyled = (skin) => skin.apply(this.login.root);

    this.connection = new Connection((m) => this.handle(m), (reason) => this.closed(reason));
    this.input = new Input(this.renderer.domElement, {
      press: (actions) => this.press(actions),
      click: (x, y) => this.click(x, y),
      zoom: (steps) => this.rig.zoom(steps),
      orbit: (dx, dy) => this.rig.orbit(dx, dy),
      released: () => this.handleMovement(true),
    }, config.camera.wheelStepPixels);
    this.rebind();

    this.registerAppliers();
    this.settings.onChange((entry) => this.settingChanged(entry));
    for (const apply of new Set(this.appliers.values())) {
      apply();
    }
    window.addEventListener('resize', () => this.resize());
    this.resize();
  }

  // ------------------------------------------------------------ options

  /** What each option does when it changes, by entry id; entries without one are read where they are used. */
  private registerAppliers(): void {
    const s = this.settings;
    const camera = () => this.rig.configure({
      isoRotateStep: s.number('iso_rotate_step', 45),
      isoZoomMax: s.number('iso_zoom_max', 90),
      sensitivity: s.number('third_sensitivity', 1),
      invert: s.toggle('third_invert'),
      fov: s.number('third_fov', 65),
    });
    for (const id of ['iso_rotate_step', 'iso_zoom_max', 'third_sensitivity', 'third_invert', 'third_fov']) {
      this.appliers.set(id, camera);
    }
    this.appliers.set('camera_mode', () => this.rig.setMode(s.text('camera_mode') === 'third_person' ? 'third_person' : 'isometric'));
    this.appliers.set('view_distance', () => {
      this.config.world.chunkRadius = Math.round(s.number('view_distance', 3));
    });
    this.appliers.set('shadows', () => {
      this.sun.castShadow = this.config.render.shadows && s.toggle('shadows', true);
    });
    this.appliers.set('fog', () => {
      this.fogOn = s.toggle('fog', true);
      this.lastDayFraction = -1;
      if (this.fog !== null) {
        this.fog.density = this.fogOn ? this.config.render.fogDensity : 0;
      }
    });
    this.appliers.set('prop_labels', () => this.entities.setPropLabels(s.toggle('prop_labels', true)));
    this.appliers.set('reduce_motion', () => {
      this.entities.reducedMotion = s.toggle('reduce_motion');
      this.appearance.reducedMotion = s.toggle('reduce_motion');
      this.rig.reducedMotion = s.toggle('reduce_motion');
    });
    this.appliers.set('bodies', () => {
      const mode = s.text('bodies');
      this.appearance.mode = mode === 'geometry' || mode === 'cards' ? (mode as BodiesMode) : 'auto';
    });
    this.appliers.set('water_detail', () => this.terrain.setWaterDetail(s.toggle('water_detail', true)));
    this.appliers.set('foliage_density', () => this.terrain.setFoliageDensity(s.number('foliage_density', 1)));
    this.appliers.set('screen_shake', () => {
      this.rig.shakeEnabled = s.toggle('screen_shake', true);
    });
    this.appliers.set('master_volume', () => this.audio.setMasterVolume(s.number('master_volume', 80) / 100));
    this.appliers.set('grayscale', () => {
      this.canvasHost.classList.toggle('grayscale', s.toggle('grayscale'));
      this.hud.root.classList.toggle('grayscale', s.toggle('grayscale'));
    });
    const day = () => {
      this.dayOverride = s.toggle('day_lock') ? s.number('day_time', 12) / 24 : null;
      this.lastDayFraction = -1;
    };
    this.appliers.set('day_lock', day);
    this.appliers.set('day_time', day);
  }

  private settingChanged(entry: OptionEntry): void {
    storageSet(STORAGE_SETTINGS + this.profile, this.settings.serialize());
    if (entry.kind === 'key') {
      this.rebind();
      return;
    }
    if (entry.applies === 'live') {
      this.appliers.get(entry.id)?.();
    }
  }

  /** The Options pane closed: entries that apply on close take effect now. */
  private optionsClosed(): void {
    for (const entry of this.catalog.entries.values()) {
      if (entry.applies === 'on_close') {
        this.appliers.get(entry.id)?.();
      }
    }
  }

  private rebind(): void {
    this.input.bind(this.catalog.actions, (id) => this.settings.bindings(id));
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

  /**
   * The hello features: the interface states always, and entity deltas unless the snapshot stream option (or a
   * ?full-states link) asks for full states.
   */
  private helloFeatures(): string[] {
    const full = new URLSearchParams(window.location.search).has('full-states') || this.settings.text('snapshot_stream') === 'full';
    const wanted = new Set(this.config.server.features);
    wanted.add(INTERFACE_FEATURE);
    wanted.add(CAST_CUES_FEATURE);
    if (full) {
      wanted.delete(ENTITY_DELTA_FEATURE);
    }
    return [...wanted];
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
    const features = this.helloFeatures();
    this.send({ t: 'hello', client_version: CLIENT_NAME, protocol: PROTOCOL_VERSION, features: features.length > 0 ? features : undefined });
    this.send({ t: 'login', account, token });
    this.login.setStatus('Logging in...');
  }

  private closed(reason: string): void {
    if (this.entryLease?.current) {
      this.entryFailed = true;
      this.entryLease.fail(reason);
    }
    this.inWorld = false;
    this.hud.inWorld = false;
    this.hud.hero.clear();
    this.login.show(true);
    this.login.setBusy(false);
    this.login.setStatus(`Disconnected: ${reason}`);
    this.entities.clear();
    this.castVisuals.clear();
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
        this.features = m.features ?? [];
        this.hud.hero.interface = this.features.includes(INTERFACE_FEATURE);
        this.login.setStatus(`Connected to ${m.server_name}: ${m.motd}`);
        console.info(`server granted features: ${this.features.length > 0 ? this.features.join(', ') : 'none'}`);
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
        if (this.entryLease?.current) {
          this.entryFailed = true;
          this.entryLease.fail(m.text, () => this.retryEntry());
        }
        this.hud.notice(m.text, 'error');
        this.login.setStatus(m.text);
        break;
      case 'disconnect':
        this.connection.close(m.reason);
        this.closed(m.reason);
        break;
      case 'enter_world': {
        if (!this.entryLease?.current && !this.inWorld) break;
        if (!this.entryLease?.current) {
          this.entryStarted = performance.now() / 1000;
          this.entryLease = this.loading.begin({ kind: 'travel', label: presentation.labels.entry, cancel: () => this.cancelEntry() });
        }
        this.entryPosition = { x: m.x, y: m.y };
        this.entryFailed = false;
        const key = `${m.world.chunk_size}:${m.world.content_hash}:${m.world.seed}`;
        if (key !== this.worldKey) {
          this.worldKey = key;
          this.terrain.reset(m.world.chunk_size);
        }
        this.entities.clear();
        this.castVisuals.clear();
        this.entities.selfKey = m.entity_key;
        this.clock.reset();
        this.clock.observe(m.server_time, local);
        this.hud.hero.observeServerTime(m.server_time, local);
        this.inWorld = true;
        this.hud.inWorld = true;
        this.login.show(false);
        this.rig.follow(toThree(m.x, m.y, m.z));
        this.hud.line(`Entered the world at ${Math.round(m.x)}, ${Math.round(m.y)} (zone ${m.zone}).`, 'system');
        break;
      }
      case 'zone_changed':
        this.zoneName = `${m.zone_name} (L${m.level}${m.safe ? ', safe' : ''})`;
        this.hud.notice(m.zone_name, 'discovery');
        break;
      case 'entity_spawn':
        this.entities.spawn(m.state, this.clock.now(local), local);
        break;
      case 'entity_update':
        this.clock.observe(m.server_time, local);
        this.hud.hero.observeServerTime(m.server_time, local);
        for (const state of m.states) {
          this.entities.update(state, m.server_time, local);
        }
        break;
      case 'entity_delta':
        this.clock.observe(m.server_time, local);
        this.hud.hero.observeServerTime(m.server_time, local);
        for (const delta of m.deltas) {
          // A delta for an entity this client does not know is ignored; the next refresh brings its full state.
          const view = this.entities.views.get(delta.key);
          if (view !== undefined) {
            this.entities.update(applyDelta(view.state, delta), m.server_time, local);
          }
        }
        break;
      case 'entity_despawn':
        this.castVisuals.removeEntity(m.key);
        this.entities.despawn(m.key);
        break;
      case 'self_state':
        this.hud.applySelf(m);
        break;
      case 'weave_slots':
        this.count(m.t);
        this.hud.applySlots(m);
        break;
      case 'inventory_state':
        this.count(m.t);
        this.hud.applyInventory(m);
        this.entities.refreshSelfEquipment();
        break;
      case 'passive_state':
        this.count(m.t);
        this.hud.applyPassives(m);
        break;
      case 'page_state':
        this.count(m.t);
        this.hud.applyPages(m);
        break;
      case 'dialogue_state':
        this.count(m.t);
        this.hud.showDialogue(m);
        break;
      case 'vendor_state':
        this.count(m.t);
        this.hud.showVendor(m);
        break;
      case 'station_state':
        this.count(m.t);
        this.hud.showStation(m);
        break;
      case 'combat_event':
        this.showCombat(m);
        break;
      case 'cast_cue':
        this.clock.observe(m.server_time, local);
        this.castVisuals.receive(m, this.clock.now(local));
        this.count(m.t);
        break;
      case 'chat_broadcast':
        this.hud.chat(m.from, m.channel, m.text);
        break;
      case 'notice':
        this.hud.notice(m.text, m.level);
        break;
      case 'quest_offers':
        this.questOffers++;
        this.hud.showQuestOffers(m);
        break;
      case 'intent_rejected':
        this.lastRejected = `${m.intent}: ${m.reason}`;
        this.hud.notice(this.lastRejected, 'error');
        break;
      case 'chunk_data':
        this.terrain.receive(chunkFromMessage(m));
        break;
      case 'craft_challenge':
        this.hud.line(`Crafting ${m.recipe}: ${m.type} challenge (${Math.round(m.time_limit)}s)`, 'system');
        break;
      case 'craft_result':
        this.hud.notice(m.success ? `Crafted ${m.item?.name ?? m.recipe} (quality ${m.quality.toFixed(2)})` : `Craft failed: ${m.reason}`, m.success ? 'quest' : 'error');
        break;
      case 'pong':
        this.rtt = performance.now() - Number(m.nonce);
        break;
      default:
        break;
    }
  }

  private count(type: string): void {
    this.received.set(type, (this.received.get(type) ?? 0) + 1);
  }

  private showCombat(c: CombatEventMsg): void {
    // every body in view reacts: the struck flinch and flash, the strikers' swings come through
    this.entities.combatHit(c.kind, c.source, c.target, c.amount);
    const self = this.entities.selfKey;
    const mine = (k: Int64) => self !== null && String(k) === String(self);
    if (!mine(c.source) && !mine(c.target)) {
      return;
    }
    const name = (key: Int64) => this.entities.views.get(key)?.state.name ?? '?';
    const source = name(c.source);
    const target = name(c.target);
    const feel = this.appearance.catalog.motion;
    const now = performance.now() / 1000;
    if (mine(c.target) && c.kind === 'damage') {
      this.rig.shakeBy(c.crit ? feel.camera('shake_crit', 0.25) : feel.camera('shake_hit', 0.12), now);
    } else if (mine(c.source) && c.kind === 'death') {
      this.rig.shakeBy(feel.camera('shake_death', 0.35), now);
    }
    const maxLife = this.entities.views.get(c.target)?.state.resource_max.life ?? 0;
    const weight = maxLife > 0 && (c.kind === 'damage' || c.kind === 'heal') ? Math.min(1, Math.max(0, c.amount / maxLife / Math.max(0.01, feel.combatText('heavy_fraction', 0.12)))) : 0;
    const text = c.kind === 'damage' ? `${source} hits ${target} for ${Math.round(c.amount)}${c.crit ? ' (crit)' : ''}`
      : c.kind === 'evade' ? `${target} evades ${source}`
      : c.kind === 'block' ? `${target} blocks ${source}`
      : c.kind === 'heal' ? `${source} heals ${target} for ${Math.round(c.amount)}`
      : c.kind === 'status' ? `${target} is ${c.status ?? ''}`
      : c.kind === 'death' ? `${target} dies`
      : c.kind;
    this.hud.line(text, 'combat');
    this.hud.combatText(c.kind, c.amount, c.crit, mine(c.target), weight);
    this.combatLines++;
    if (c.kind === 'damage' && mine(c.source)) {
      this.hitsDealt++;
      this.lastHit = target;
    }
  }

  // ------------------------------------------------------------ input

  /** A press of one or more actions: the interface first (Escape, the book walk, the tray, pages), then the world. */
  private press(actions: readonly string[]): void {
    if (this.loading.active || this.loading.padQuarantined) return;
    if (this.hud.press(actions)) {
      return;
    }
    for (const action of actions) {
      this.action(action);
    }
  }

  private action(action: string): void {
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
      case 'cancel':
        this.send({ t: 'cancel_cast' });
        return;
      default:
        if (/^slot_\d+$/.test(action)) {
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
    if (this.loading.active) return;
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
    if (!this.settings.toggle('click_to_move', true)) {
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
    this.entities.hoveredKey = hovered?.key ?? null;
    this.hud.setTarget(hovered?.state ?? null);
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

  /** The ground distance from the hero to an entity this client knows, for world panes' departure. */
  private distanceTo(key: Int64): number | null {
    const self = this.entities.self;
    const other = this.entities.views.get(key);
    if (self === undefined || other === undefined) {
      return null;
    }
    const a = self.position;
    const b = other.position;
    return Math.hypot(a.x - b.x, a.y - b.y);
  }

  /** The hero's head on screen this frame, in client pixels, or null when it is not on screen. */
  private heroScreen(): { x: number; y: number } | null {
    const self = this.entities.self;
    if (self === undefined) {
      return null;
    }
    const p = self.root.position.clone();
    p.y += self.state.height + 0.3;
    const s = this.rig.project(p, this.width, this.height);
    return s.visible ? { x: s.x, y: s.y } : null;
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
    if (!this.loading.active && !this.loading.padQuarantined) this.input.pollPad();
    const renderTime = this.clock.now(local) - this.config.motion.interpolationDelayMs / 1000;
    this.entities.serverNow = this.clock.now(local);
    this.appearance.isometric = this.rig.mode === 'isometric';
    this.appearance.orthoSize = this.rig.orthoSize;
    this.appearance.cameraYaw = this.rig.yawCurrent;
    this.appearance.baker.step();
    this.entities.animate(dt, local, renderTime);
    this.castVisuals.update(this.clock.now(local), this.settings.toggle('reduce_motion'));
    if (this.inWorld) {
      this.followDay(this.clock.now(local));
      const self = this.entities.self;
      if (self !== undefined) {
        this.rig.follow(self.root.position);
        this.sun.target.position.copy(self.root.position);
        this.sun.position.copy(self.root.position).add(this.sunOffset);
        this.terrain.update(self.position, local);
      }
      this.terrain.animateWater(this.clock.now(local), bodyLight.sunDirection);
      this.hud.setSelfEntity(self?.state ?? null);
      if (!this.loading.active && !this.loading.padQuarantined) { this.handleMovement(); this.updateHover(); }
      if (local - this.lastPingAt > this.config.world.pingSeconds) {
        this.lastPingAt = local;
        this.send({ t: 'ping', nonce: Math.round(performance.now()) });
      }
      this.hud.netText = `${this.rtt === null ? '' : `${Math.round(this.rtt)} ms  `}${this.terrain.builtCount} chunks  ${this.entities.views.size} entities`;
    }
    this.checkEntry(local);
    this.rig.update(dt, false, local);
    this.entities.placeLabels(this.rig.camera, this.width, this.height, dt);
    this.hud.frame(performance.now() / 1000, this.width, this.height);
    this.renderer.render(this.scene, this.rig.camera);
  }

  /** The fraction of the day the sky shows now, for the smoke and the overlay. */
  get timeOfDay(): number {
    return this.dayOverride ?? this.day.timeOfDay(this.clock.now(performance.now() / 1000));
  }

  /** The sky, sun, ambient light and haze follow the server's clock through the content's day. */
  private followDay(serverNow: number): void {
    const fraction = this.dayOverride ?? this.day.timeOfDay(serverNow);
    if (Math.abs(fraction - this.lastDayFraction) < 0.0005) {
      return;
    }
    this.lastDayFraction = fraction;
    const sky = this.day.at(fraction);
    this.applySky(sky);
  }

  private applySky(sky: SkyState): void {
    const lin = (c: [number, number, number]) => new Color(...toLinear(c));
    this.sun.color.copy(lin(sky.sun));
    this.sun.intensity = sky.sunEnergy * this.config.render.sunIntensity / 1.2;
    this.sun.visible = sky.sunPitch > 0.5;
    // the sun shines down from its height over its bearing (degrees clockwise from north), as the desktop sun does
    const pitch = MathUtils.degToRad(-Math.max(2, sky.sunPitch));
    const yaw = MathUtils.degToRad(sky.sunYaw);
    const shine = new Vector3(0, Math.sin(pitch), -Math.cos(pitch)).applyAxisAngle(new Vector3(0, 1, 0), yaw);
    this.sunOffset = shine.clone().multiplyScalar(-80);
    bodyLight.sunDirection.copy(shine).negate().normalize();
    bodyLight.sunColor.copy(lin(sky.sun)).multiplyScalar(sky.sunEnergy);
    this.hemisphere.color.copy(lin(sky.ambient)).multiplyScalar(1.2);
    this.hemisphere.groundColor.copy(lin(sky.fog)).multiplyScalar(0.5);
    this.hemisphere.intensity = sky.ambientEnergy * this.config.render.ambientIntensity / 0.55;
    bodyLight.ambientSky.copy(lin(sky.ambient)).multiplyScalar(sky.ambientEnergy * 1.6);
    bodyLight.ambientGround.copy(lin(sky.fog)).multiplyScalar(sky.ambientEnergy * 0.7);
    this.scene.background = lin(sky.sky);
    if (this.fog !== null) {
      this.fog.color.copy(lin(sky.fog));
      this.fog.density = this.fogOn ? sky.fogDensity : 0;
    }
  }

  // ------------------------------------------------------------ scripted checks

  /** Read-only views for the scripted smoke run and the browser console; nothing here changes the game. */
  probe() {
    const app = this;
    const stateOf = (v: EntityView) => ({ ...v.state, drawn: v.position });
    return {
      get loading() { return app.loading.snapshot; },
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
          castVisuals: app.castVisuals.timeline.active.length,
          hitsDealt: app.hitsDealt,
          chunksBuilt: app.terrain.builtCount,
          chunksPending: app.terrain.pendingCount,
          chunksRequested: app.terrain.requested,
          entities: app.entities.views.size,
          frameMs: Math.round(app.frameMs * 100) / 100,
          states: Object.fromEntries(app.received),
        };
      },
      /** The renderer's counts and the bodies drawn, for the measurement line. */
      get render() {
        const info = app.renderer.info;
        const bodies = app.entities.bodyCounts;
        return {
          drawCalls: info.render.calls,
          triangles: info.render.triangles,
          textures: info.memory.textures,
          geometries: info.memory.geometries,
          bodies: bodies.bodies,
          cards: bodies.cards,
          bodyTriangles: bodies.triangles,
          atlases: app.appearance.atlasCount,
          atlasBytes: app.appearance.atlasBytes,
          bakesPending: app.appearance.baker.pending,
          timeOfDay: app.timeOfDay,
          cues: Object.fromEntries(app.audio.raised),
          light: {
            sun: app.sun.intensity,
            sunVisible: app.sun.visible,
            sunColor: app.sun.color.getHexString(),
            sunAt: app.sun.position.toArray().map((v) => Math.round(v * 10) / 10),
            hemisphere: app.hemisphere.intensity,
            hemisphereColor: app.hemisphere.color.getHexString(),
            fog: app.fog?.density ?? 0,
            background: app.scene.background instanceof Color ? app.scene.background.getHexString() : null,
          },
        };
      },
      get ui() {
        const hud = app.hud;
        return {
          questsVisible: hud.questsVisible,
          sheetVisible: hud.sheetVisible,
          target: hud.targetLine,
          cameraMode: app.rig.mode,
          cameraYaw: app.rig.yaw,
          coreOpen: hud.coreOpen,
          optionsOpen: hud.optionsOpen,
          trayOpen: hud.workspace.trayOpen,
          shownPages: hud.trayRows.filter((r) => hud.isShown(r.page.id)).map((r) => r.page.id),
          ribbonPages: hud.ribbonPages,
          worldPanes: hud.workspace.book.within('world').map((l) => l.kind),
          bagItems: hud.bagItems,
          uiScale: hud.uiScale,
          theme: hud.skin.theme.id,
          cluster: hud.clusterRect,
          rects: hud.workspaceRects,
          notice: hud.noticeText,
        };
      },
      get hoveredKey() {
        return app.hoveredKey;
      },
      get lastInteracted() {
        return app.lastInteracted;
      },
      get features() {
        return [...app.features];
      },
      get lastHit() {
        return app.lastHit;
      },
      castCues: () => app.castVisuals.timeline.active.map(c => ({ phase: c.phase, source: c.event.source, target: c.event.target, name: c.event.name, color: c.color })),
      /** The player's effective bindings for an action, as Options shows them. */
      bindings: (action: string) => app.settings.bindings(action),
      setting: (id: string) => app.settings.value(id),
      /** Sets a player option as the Options pane would (the smoke's way to the look's stages). */
      setSetting: (id: string, value: unknown) => app.settings.set(id, value as never),
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
