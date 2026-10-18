import {
  BoxGeometry,
  CapsuleGeometry,
  CylinderGeometry,
  ExtrudeGeometry,
  Group,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  Path,
  Shape,
  ShapeGeometry,
  SphereGeometry,
  Vector2,
  Vector3,
  type BufferGeometry,
  type Camera,
} from 'three';
import type { EntityPaletteSettings, MotionSettings } from '../config';
import type { EntityState, Int64 } from '../net/protocol';
import { linearColor } from './colors';
import { toThree, type CorePoint } from './coords';
import { pushSample, sampleAt, lerpAngle, type Sample } from './interpolation';
import { regionOf, specOf, type ShapeSpec } from './shapes';

/**
 * Placeholder bodies for server entities, chosen by what they are like the desktop client's EntityView:
 * capsules for creatures, a trunk and canopy for trees, boxes for other props and structures, spheres for
 * projectiles, cylinders for drops, fields, markers and founts. A projectile, area or field the server names a
 * shape for is drawn as that shape (a slab for a projectile, a flat mark for the rest), facing its heading and
 * turned by its spin; a telegraph is a ground mark that fades in, pulses, and fades out when the strike lands.
 * Coloured by faction and rarity from config, with a name, level and life bar label kept over each body in
 * screen space.
 */

interface BodyShape {
  key: string;
  build: () => BufferGeometry;
  color: string;
  opacity: number;
  /** Lift so the body stands on the entity's position rather than straddling it. */
  lift: number;
  canopy?: { radius: number; y: number; color: string };
  /**
   * The size the mouse picks, matching what is drawn rather than the simulation radius (a marker's radius is
   * its whole place). Null: never under the cursor (ground fields, projectiles in flight).
   */
  pick: { radius: number; height: number } | null;
  /** Drawn by a delivery shape: faces its heading and spin rather than the body's yaw, and casts no shadow. */
  shaped?: boolean;
  /** A telegraph mark: fades in, pulses, and fades out when it goes. */
  telegraph?: boolean;
}

/** A shape's region as a mesh on the ground, x along its facing and y to its left; a thickness extrudes it upward. */
export function shapeGeometry(spec: ShapeSpec, thickness: number): BufferGeometry {
  const region = regionOf(spec);
  const outline = new Shape(region.outer.map(([x, y]) => new Vector2(x, y)));
  for (const hole of region.holes) {
    outline.holes.push(new Path(hole.map(([x, y]) => new Vector2(x, y))));
  }
  const geometry = thickness > 0 ? new ExtrudeGeometry(outline, { depth: thickness, bevelEnabled: false, curveSegments: 4 }) : new ShapeGeometry(outline);
  // The shape's plane (x, y) lies on the ground as three's (x, -z), and the extrusion rises along y.
  geometry.rotateX(-Math.PI / 2);
  return geometry;
}

const r2 = (n: number) => Math.round(n * 100) / 100;

export function lookOf(s: EntityState): string {
  return s.look ?? s.pattern ?? '';
}

export function isDrop(s: EntityState): boolean {
  return lookOf(s).startsWith('drop:');
}

function hasResources(s: EntityState): boolean {
  return Object.keys(s.resources).length > 0;
}

function floraRule(palette: EntityPaletteSettings, look: string) {
  return palette.flora.find((rule) => rule.match.some((m) => look.includes(m)));
}

/** Which colour a body gets, mirroring EntityView.ColorFor on the desktop. */
export function colorFor(s: EntityState, palette: EntityPaletteSettings): string {
  const look = lookOf(s);
  if (s.is_player) {
    return palette.player;
  }
  if (look.startsWith('drop:')) {
    return palette.drops[s.rarity ?? 'normal'] ?? palette.drops.normal ?? palette.creatureFallback;
  }
  if (look === 'projectile') {
    return palette.projectile;
  }
  if (look === 'field') {
    return palette.field;
  }
  if (look.startsWith('poi:')) {
    return palette.poi;
  }
  if (look === 'fount') {
    return palette.fount;
  }
  if (!hasResources(s)) {
    return floraRule(palette, look)?.color ?? palette.floraFallback;
  }
  return palette.factions[s.faction ?? ''] ?? palette.rarities[s.rarity ?? ''] ?? palette.creatureFallback;
}

export function shapeFor(s: EntityState, palette: EntityPaletteSettings): BodyShape {
  const look = lookOf(s);
  const color = colorFor(s, palette);
  const radius = s.radius;
  const height = s.height;
  if (s.shape || look === 'telegraph') {
    const spec = specOf(s.shape ?? 'circle', s.shape_size, Math.max(0.1, radius));
    const kind = look === 'telegraph' ? 'telegraph' : look === 'projectile' || s.pattern === 'projectile' ? 'projectile' : 'area';
    const thickness = kind === 'projectile' ? r2(Math.min(0.4, Math.max(0.12, height * 0.4))) : 0;
    const key = `shape:${spec.name}:${r2(spec.radius)}:${r2(spec.angle)}:${r2(spec.width)}:${r2(spec.inner)}:${thickness}`;
    return {
      key,
      build: () => shapeGeometry(spec, thickness),
      color: kind === 'telegraph' ? palette.telegraph : color,
      opacity: kind === 'telegraph' ? palette.telegraphOpacity : kind === 'projectile' ? 1 : palette.fieldOpacity,
      lift: kind === 'projectile' ? Math.max(0.1, height * 0.5) - thickness / 2 : kind === 'telegraph' ? 0.07 : 0.05,
      pick: null,
      shaped: true,
      telegraph: kind === 'telegraph',
    };
  }
  if (look.startsWith('drop:')) {
    return { key: 'drop', build: () => new CylinderGeometry(0.25, 0.25, 0.25, 12), color, opacity: 1, lift: 0.125, pick: { radius: 0.25, height: 0.25 } };
  }
  if (look === 'projectile' || s.pattern === 'projectile') {
    const r = r2(Math.max(0.15, radius * 0.6));
    return { key: `sphere:${r}`, build: () => new SphereGeometry(r, 12, 8), color, opacity: 1, lift: height * 0.5, pick: null };
  }
  if (look === 'field') {
    const r = r2(Math.max(0.1, radius));
    return { key: `field:${r}`, build: () => new CylinderGeometry(r, r, 0.1, 24), color, opacity: palette.fieldOpacity, lift: 0.05, pick: null };
  }
  if (look.startsWith('poi:')) {
    return { key: 'poi', build: () => new CylinderGeometry(0.15, 0.6, 3.5, 10), color, opacity: 1, lift: 1.75, pick: { radius: 0.6, height: 3.5 } };
  }
  if (look === 'fount') {
    const h = r2(Math.max(0.5, height));
    return { key: `fount:${h}`, build: () => new CylinderGeometry(0.2, 1.0, h, 12), color, opacity: 1, lift: h / 2, pick: { radius: 1, height: h } };
  }
  if (s.interactable != null && !hasResources(s)) {
    const w = r2(Math.max(0.2, radius * 2));
    const h = r2(Math.max(0.2, height));
    return { key: `box:${w}:${h}`, build: () => new BoxGeometry(w, h, w), color, opacity: 1, lift: h / 2, pick: { radius: w / 2, height: h } };
  }
  if (!hasResources(s) && floraRule(palette, look)?.tree) {
    // A trunk with a round canopy on top, so tall flora reads as a tree and not a pillar.
    const trunk = r2(Math.max(0.25, radius * 0.6));
    const th = r2(Math.max(0.3, height * 0.6));
    const canopy = r2(Math.min(4, Math.max(0.8, height * 0.28)));
    return {
      key: `trunk:${trunk}:${th}`,
      build: () => new BoxGeometry(trunk, th, trunk),
      color: palette.trunk,
      opacity: 1,
      lift: th / 2,
      canopy: { radius: canopy, y: height - canopy, color },
      pick: { radius: canopy, height },
    };
  }
  if (!hasResources(s)) {
    const w = r2(Math.max(0.3, radius * 1.4));
    const h = r2(Math.max(0.2, height));
    return { key: `box:${w}:${h}`, build: () => new BoxGeometry(w, h, w), color, opacity: 1, lift: h / 2, pick: { radius: w / 2, height: h } };
  }
  const r = r2(Math.max(0.2, radius));
  const h = r2(Math.max(0.6, height));
  return { key: `capsule:${r}:${h}`, build: () => new CapsuleGeometry(r, Math.max(0.01, h - 2 * r), 6, 12), color, opacity: 1, lift: h / 2, pick: { radius: r, height: h } };
}

/** The label line over a body, as the desktop client writes it. */
export function titleFor(s: EntityState, showCasting = true): string {
  let title = s.name;
  if (s.level > 0 && hasResources(s) && !s.is_player) {
    title = `${s.name} [${s.level}]`;
  }
  if (s.dead) {
    title += ' (dead)';
  }
  if (showCasting && s.casting != null) {
    title += `  ${s.casting} ${Math.floor(s.cast_progress * 100)}%`;
  }
  return title;
}

export class EntityView {
  readonly root = new Group();
  readonly label: HTMLDivElement;
  private readonly nameEl: HTMLDivElement;
  private readonly statusEl: HTMLDivElement;
  private readonly lifeEl: HTMLDivElement;
  private readonly lifeFill: HTMLDivElement;
  private body: Mesh | null = null;
  private canopy: Mesh | null = null;
  private shapeKey = '';
  /** Drawn by a delivery shape, facing its heading. */
  private shaped = false;
  /** A telegraph's own material, whose opacity fades; null for every other body. */
  private fade: MeshBasicMaterial | null = null;
  /** A telegraph's fill, growing from its anchor to the whole mark as the strike nears. */
  private fill: Mesh | null = null;
  private fillMaterial: MeshBasicMaterial | null = null;
  private fadeOpacity = 0;
  private bornAt = 0;
  /** The server time the client first saw the body, for a telegraph's progress toward its until. */
  private bornServer = 0;
  /** When a telegraph's strike landed (it despawned): it fades out from then. */
  dyingAt: number | null = null;
  /** What the mouse picks; see BodyShape.pick. */
  pickSize: { radius: number; height: number } | null = null;
  private readonly samples: Sample[] = [];
  private readonly target = new Vector3();
  private readonly velocity = new Vector3();
  private receivedAt = 0;
  private labelX = Number.NaN;
  private labelY = Number.NaN;
  private labelShown = false;
  state: EntityState;

  constructor(
    state: EntityState,
    readonly isSelf: boolean,
    private readonly layer: EntityLayer,
  ) {
    this.state = state;
    this.root.name = `e${state.key}`;
    this.label = document.createElement('div');
    this.label.className = 'entity-label';
    this.nameEl = document.createElement('div');
    this.nameEl.className = 'entity-name';
    this.statusEl = document.createElement('div');
    this.statusEl.className = 'entity-statuses';
    this.lifeEl = document.createElement('div');
    this.lifeEl.className = 'entity-life';
    this.lifeFill = document.createElement('div');
    this.lifeEl.append(this.lifeFill);
    this.label.append(this.nameEl, this.statusEl, this.lifeEl);
    this.label.style.display = 'none';
  }

  get key(): Int64 {
    return this.state.key;
  }

  /** Where the body is drawn now, in simulation space. */
  get position(): CorePoint {
    return { x: this.root.position.x, y: -this.root.position.z, z: this.root.position.y };
  }

  apply(state: EntityState, serverTime: number, localSeconds: number, snap: boolean): void {
    const previous = this.state;
    this.state = state;
    pushSample(this.samples, { t: serverTime, x: state.x, y: state.y, z: state.z, yaw: state.yaw, vx: state.vx, vy: state.vy, vz: state.vz });
    toThree(state.x, state.y, state.z, this.target);
    toThree(state.vx, state.vy, state.vz, this.velocity);
    this.receivedAt = localSeconds;
    if (snap) {
      this.bornAt = localSeconds;
      this.bornServer = this.layer.serverNow;
    }
    const shape = shapeFor(state, this.layer.palette);
    if (snap || shape.key !== this.shapeKey || state.dead !== previous.dead || colorFor(state, this.layer.palette) !== colorFor(previous, this.layer.palette)) {
      this.rebuild(shape);
    }
    if (snap) {
      this.root.position.copy(this.target);
      this.root.rotation.y = this.facing(state.yaw);
    }
    this.refreshLabel();
  }

  /** The body's turn about the vertical: a shape faces its heading (else the yaw) plus its spin; a body its yaw. */
  private facing(yaw: number): number {
    return this.shaped ? (this.state.heading ?? yaw) + (this.state.spin ?? 0) : -yaw + Math.PI / 2;
  }

  private rebuild(shape: BodyShape): void {
    const s = this.state;
    this.shapeKey = shape.key;
    this.pickSize = shape.pick;
    this.shaped = shape.shaped === true;
    const opacity = s.dead ? Math.min(shape.opacity, this.layer.palette.deadOpacity) : shape.opacity;
    let material: MeshLambertMaterial | MeshBasicMaterial = this.layer.material(shape.color, opacity);
    if (shape.telegraph) {
      // A telegraph's opacity is its own to fade, so it takes its own materials: the mark, and its growing fill.
      this.fade?.dispose();
      this.fillMaterial?.dispose();
      this.fade = new MeshBasicMaterial({ color: linearColor(shape.color), transparent: true, opacity: 0, depthWrite: false });
      this.fillMaterial = new MeshBasicMaterial({ color: linearColor(shape.color), transparent: true, opacity: 0, depthWrite: false });
      this.fadeOpacity = opacity;
      material = this.fade;
      const geometry = this.layer.geometry(shape.key, shape.build);
      if (this.fill === null) {
        this.fill = new Mesh(geometry, this.fillMaterial);
        this.fill.renderOrder = 3;
        this.root.add(this.fill);
      } else {
        this.fill.geometry = geometry;
        this.fill.material = this.fillMaterial;
      }
      this.fill.position.y = shape.lift + 0.01;
      this.fill.visible = true;
    } else {
      this.fade?.dispose();
      this.fade = null;
      this.fillMaterial?.dispose();
      this.fillMaterial = null;
      if (this.fill !== null) {
        this.fill.visible = false;
      }
    }
    if (this.body === null) {
      this.body = new Mesh(this.layer.geometry(shape.key, shape.build), material);
      this.root.add(this.body);
    } else {
      this.body.geometry = this.layer.geometry(shape.key, shape.build);
      this.body.material = material;
    }
    this.body.castShadow = !this.shaped;
    this.body.renderOrder = shape.telegraph ? 2 : 0;
    this.body.position.y = shape.lift;
    if (shape.canopy) {
      const c = shape.canopy;
      const geometry = this.layer.geometry(`canopy:${c.radius}`, () => new SphereGeometry(c.radius, 12, 6));
      if (this.canopy === null) {
        this.canopy = new Mesh(geometry, this.layer.material(c.color, opacity));
        this.canopy.castShadow = true;
        this.root.add(this.canopy);
      } else {
        this.canopy.geometry = geometry;
        this.canopy.material = this.layer.material(c.color, opacity);
      }
      this.canopy.position.y = c.y;
      this.canopy.visible = true;
    } else if (this.canopy !== null) {
      this.canopy.visible = false;
    }
  }

  refreshLabel(): void {
    const s = this.state;
    const palette = this.layer.palette;
    // Creatures always wear a label; props wear one while the names-over-props option is on.
    const shown = hasResources(s) || (s.interactable != null && this.layer.propLabels);
    this.labelShown = shown;
    if (!shown) {
      this.label.style.display = 'none';
      return;
    }
    this.nameEl.textContent = titleFor(s, this.layer.showLegacyCast);
    this.statusEl.textContent = s.statuses.join(' ');
    this.statusEl.style.display = s.statuses.length > 0 ? '' : 'none';
    this.label.style.color = s.dead ? palette.labelColors.dead ?? palette.labelDefault
      : s.is_player ? palette.labelColors.player ?? palette.labelDefault
      : palette.labelColors[s.rarity ?? ''] ?? palette.labelDefault;
    const life = s.resources.life;
    const max = s.resource_max.life;
    const showLife = life !== undefined && max !== undefined && s.interactable == null;
    this.lifeEl.style.display = showLife ? '' : 'none';
    if (showLife) {
      const frac = max > 0 ? Math.min(1, Math.max(0, life / max)) : 0;
      this.lifeFill.style.width = `${(frac * 100).toFixed(1)}%`;
    }
  }

  frame(dt: number, localSeconds: number, renderServerTime: number, motion: MotionSettings): void {
    if (this.fade !== null) {
      // A telegraph fades in, breathes while its strike is coming, its fill growing from the anchor to the whole
      // mark as the server time nears its until, and fades out once it lands or its time is up.
      const age = localSeconds - this.bornAt;
      const reduced = this.layer.reducedMotion;
      const pulse = reduced ? 1 : 0.78 + 0.22 * Math.sin(age * 6);
      const fadeIn = Math.min(1, age / 0.35);
      const until = this.state.until;
      const span = until !== undefined ? until - this.bornServer : 0;
      const progress = span > 0.01 ? Math.min(1, Math.max(0, (this.layer.serverNow - this.bornServer) / span)) : 1;
      if (until !== undefined && this.dyingAt === null && this.layer.serverNow > until + 0.1) {
        this.dyingAt = localSeconds;
      }
      const fadeOut = this.dyingAt === null ? 1 : Math.max(0, 1 - (localSeconds - this.dyingAt) / EntityLayer.fadeSeconds);
      this.fade.opacity = this.fadeOpacity * 0.55 * fadeIn * fadeOut * pulse;
      if (this.fill !== null && this.fillMaterial !== null) {
        const grown = Math.max(0.02, progress);
        this.fill.scale.set(grown, 1, grown);
        this.fillMaterial.opacity = this.fadeOpacity * (0.45 + 0.4 * progress) * fadeIn * fadeOut;
      }
    }
    if (this.isSelf) {
      // The player's own body leads: last known position plus velocity, smoothed, as the desktop client does.
      const age = Math.min(localSeconds - this.receivedAt, motion.maxExtrapolationMs / 1000);
      const t = 1 - Math.exp(-dt * motion.selfSmoothing);
      const predicted = this.velocity.clone().multiplyScalar(age).add(this.target);
      this.root.position.lerp(predicted, t);
      this.root.rotation.y = lerpAngle(this.root.rotation.y, this.facing(this.state.yaw), t);
      return;
    }
    if (this.dyingAt !== null) {
      return;
    }
    const pose = sampleAt(this.samples, renderServerTime, motion.maxExtrapolationMs / 1000);
    if (pose !== null) {
      toThree(pose.x, pose.y, pose.z, this.root.position);
      this.root.rotation.y = this.facing(pose.yaw);
    }
  }

  /** Whether the body is a telegraph mark, which lingers to fade out when it goes. */
  get isTelegraph(): boolean {
    return this.fade !== null;
  }

  placeLabel(camera: Camera, width: number, height: number, scratch: Vector3): void {
    if (!this.labelShown) {
      return;
    }
    scratch.copy(this.root.position);
    scratch.y += Math.max(this.state.height, this.pickSize?.height ?? 0) + 0.5;
    scratch.project(camera);
    const visible = scratch.z > -1 && scratch.z < 1 && Math.abs(scratch.x) < 1.1 && Math.abs(scratch.y) < 1.1;
    if (!visible) {
      this.label.style.display = 'none';
      return;
    }
    const x = Math.round((scratch.x * 0.5 + 0.5) * width);
    const y = Math.round((-scratch.y * 0.5 + 0.5) * height);
    if (this.label.style.display === 'none') {
      this.label.style.display = '';
    }
    if (x !== this.labelX || y !== this.labelY) {
      this.labelX = x;
      this.labelY = y;
      this.label.style.transform = `translate(${x}px, ${y}px) translate(-50%, -100%)`;
    }
  }

  dispose(): void {
    this.label.remove();
    this.fade?.dispose();
    this.fillMaterial?.dispose();
  }
}

export class EntityLayer {
  showLegacyCast = true;
  readonly group = new Group();
  readonly views = new Map<Int64, EntityView>();
  selfKey: Int64 | null = null;
  /** Labels over resource nodes, chests and stations as well as creatures (the names-over-props option). */
  propLabels = true;
  /** Still marks: telegraphs do not breathe (the reduce motion option). */
  reducedMotion = false;
  /** The server's clock now, as the client estimates it, for marks that end at a server time. */
  serverNow = 0;
  /** How long a telegraph takes to fade once its strike lands. */
  static readonly fadeSeconds = 0.35;
  /** Telegraphs whose strike landed, fading out before they go. */
  private readonly fading: EntityView[] = [];
  private readonly geometries = new Map<string, BufferGeometry>();
  private readonly materials = new Map<string, MeshLambertMaterial>();
  private readonly scratch = new Vector3();

  /** Shows or hides the props' labels. */
  setPropLabels(on: boolean): void {
    if (on === this.propLabels) {
      return;
    }
    this.propLabels = on;
    for (const view of this.views.values()) {
      view.refreshLabel();
    }
  }

  constructor(
    readonly palette: EntityPaletteSettings,
    private readonly motion: MotionSettings,
    private readonly labels: HTMLElement,
  ) {
    this.group.name = 'entities';
  }

  geometry(key: string, build: () => BufferGeometry): BufferGeometry {
    let g = this.geometries.get(key);
    if (g === undefined) {
      g = build();
      this.geometries.set(key, g);
    }
    return g;
  }

  material(color: string, opacity: number): MeshLambertMaterial {
    const key = `${color}:${opacity}`;
    let m = this.materials.get(key);
    if (m === undefined) {
      m = new MeshLambertMaterial({ color: linearColor(color), transparent: opacity < 1, opacity, depthWrite: opacity >= 1 });
      this.materials.set(key, m);
    }
    return m;
  }

  get self(): EntityView | undefined {
    return this.selfKey === null ? undefined : this.views.get(this.selfKey);
  }

  spawn(state: EntityState, serverTime: number, localSeconds: number): EntityView {
    const existing = this.views.get(state.key);
    if (existing !== undefined) {
      existing.apply(state, serverTime, localSeconds, true);
      return existing;
    }
    const view = new EntityView(state, state.key === this.selfKey, this);
    this.views.set(state.key, view);
    this.group.add(view.root);
    this.labels.append(view.label);
    view.apply(state, serverTime, localSeconds, true);
    return view;
  }

  update(state: EntityState, serverTime: number, localSeconds: number): void {
    const view = this.views.get(state.key);
    if (view === undefined) {
      this.spawn(state, serverTime, localSeconds);
    } else {
      view.apply(state, serverTime, localSeconds, false);
    }
  }

  despawn(key: Int64, fade = true): void {
    const view = this.views.get(key);
    if (view !== undefined) {
      this.views.delete(key);
      if (fade && view.isTelegraph) {
        // The strike landed: the mark lingers to fade out.
        view.dyingAt = performance.now() / 1000;
        this.fading.push(view);
        view.label.style.display = 'none';
        return;
      }
      this.group.remove(view.root);
      view.dispose();
    }
  }

  clear(): void {
    for (const key of [...this.views.keys()]) {
      this.despawn(key, false);
    }
    for (const view of this.fading.splice(0)) {
      this.group.remove(view.root);
      view.dispose();
    }
  }

  /** Moves every body to where it should be drawn this frame, and lets faded telegraphs go. */
  animate(dt: number, localSeconds: number, renderServerTime: number): void {
    for (const view of this.views.values()) {
      view.frame(dt, localSeconds, renderServerTime, this.motion);
    }
    for (let i = this.fading.length - 1; i >= 0; i--) {
      const view = this.fading[i];
      view.frame(dt, localSeconds, renderServerTime, this.motion);
      if (view.dyingAt !== null && localSeconds - view.dyingAt > EntityLayer.fadeSeconds) {
        this.fading.splice(i, 1);
        this.group.remove(view.root);
        view.dispose();
      }
    }
  }

  /** Puts labels over the bodies; call after the camera has moved for the frame. */
  placeLabels(camera: Camera, width: number, height: number): void {
    for (const view of this.views.values()) {
      view.placeLabel(camera, width, height, this.scratch);
    }
  }

  /**
   * The entity whose body a ray passes through, ignoring the player's own. Creatures and interactables win
   * over plain props, so a canopy in front of a hob does not take the hover (and with it the weave target)
   * from the hob behind it; among overlapping bodies the one the ray passes closest to the middle of wins,
   * so a quest giver standing beside a marker is picked when the cursor is on the quest giver.
   */
  pick(origin: Vector3, direction: Vector3, maxDistance = 400): EntityView | null {
    let best: EntityView | null = null;
    let bestScore = Number.POSITIVE_INFINITY;
    let bestProp: EntityView | null = null;
    let bestPropScore = Number.POSITIVE_INFINITY;
    const center = new Vector3();
    const closest = new Vector3();
    for (const view of this.views.values()) {
      if (view.key === this.selfKey) {
        continue;
      }
      const size = view.pickSize;
      if (size === null) {
        continue;
      }
      center.copy(view.root.position);
      center.y += size.height * 0.5;
      const along = closest.copy(center).sub(origin).dot(direction);
      if (along < 0 || along > maxDistance) {
        continue;
      }
      closest.copy(direction).multiplyScalar(along).add(origin);
      const reach = Math.max(size.radius, 0.5) + size.height * 0.35;
      const off = center.distanceTo(closest);
      if (off > reach) {
        continue;
      }
      // How far from the body's middle the ray passes, as a share of the body's reach; nearer bodies win ties.
      const score = off / reach + along * 1e-4;
      if (hasResources(view.state) || view.state.interactable != null) {
        if (score < bestScore) {
          bestScore = score;
          best = view;
        }
      } else if (score < bestPropScore) {
        bestPropScore = score;
        bestProp = view;
      }
    }
    return best ?? bestProp;
  }
}
