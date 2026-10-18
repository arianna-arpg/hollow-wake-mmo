import {
  ExtrudeGeometry,
  Group,
  Mesh,
  MeshBasicMaterial,
  MeshLambertMaterial,
  Path,
  Shape,
  ShapeGeometry,
  Vector2,
  Vector3,
  type BufferGeometry,
  type Camera,
  type WebGLRenderer,
} from 'three';
import type { EntityPaletteSettings, MotionSettings } from '../config';
import type { EntityState, Int64, ItemInfo, HitBodyInfo } from '../net/protocol';
import { HitBodyDebug } from './hitBodies';
import type { Obj } from '../ui/logic/content';
import { animate, freshVisualState, Pose, type VisualState } from './appearance/animator';
import { buildBody } from './appearance/bodyFamilies';
import { BodyMesh, type BodyView } from './appearance/bodyMesh';
import type { BodyPlan } from './appearance/bodyPlan';
import { CardAtlas, CardBaker, CardView } from './appearance/cards';
import { lay, wear } from './appearance/gearFamilies';
import { AppearanceCatalog, type AppearanceDef } from './appearance/looks';
import { ContactMark, markStyle } from './appearance/marks';
import { linearColor } from './colors';
import { toThree, type CorePoint } from './coords';
import { pushSample, sampleAt, lerpAngle, type Sample } from './interpolation';
import { regionOf, specOf, type ShapeSpec } from './shapes';

/**
 * Server entities as drawn, the twin of the desktop EntityView: a built body from the appearance content
 * (painted geometry or a directional card, chosen through the bodies option and the camera) posed by the
 * shared animator from the state the server sends, a contact mark on the ground, a label kept over it in
 * screen space, and snapshot interpolation. A projectile, area or field the server names a shape for is
 * drawn as that shape (a slab for a projectile, a flat mark for the rest), facing its heading and turned by
 * its spin; a telegraph is a ground mark that fades in, pulses, and fades out when the strike lands. Nothing
 * here decides anything: every pose and flash follows a fact on the wire.
 */

export type BodiesMode = 'auto' | 'geometry' | 'cards';

interface ShapeBody {
  key: string;
  build: () => BufferGeometry;
  color: string;
  opacity: number;
  lift: number;
  telegraph: boolean;
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

/** Whether a body is drawn by a delivery shape rather than a built body. */
export function isShaped(s: EntityState): boolean {
  return s.shape != null || lookOf(s) === 'telegraph';
}

/** The colour of a drawn shape (a flight, an area, a field): its Thread's colour when the look names one, else by faction. */
export function shapeColor(s: EntityState, palette: EntityPaletteSettings): string {
  const look = lookOf(s);
  if (look === 'telegraph') {
    return palette.telegraph;
  }
  if (s.is_player) {
    return palette.player;
  }
  if (look === 'field') {
    return palette.field;
  }
  if (/^#[0-9a-fA-F]{6}$/.test(look)) {
    return look;
  }
  return palette.factions[s.faction ?? ''] ?? palette.projectile;
}

function shapeFor(s: EntityState, palette: EntityPaletteSettings, precise = false): ShapeBody {
  const look = lookOf(s);
  const spec = specOf(s.shape ?? 'circle', s.shape_size, Math.max(0.1, s.radius));
  const kind = look === 'telegraph' ? 'telegraph' : look === 'projectile' || s.pattern === 'projectile' ? 'projectile' : 'area';
  const thickness = kind === 'projectile' ? (precise ? s.height : r2(Math.min(.4,Math.max(.12,s.height*.4)))) : 0;
  return {
    key: `shape:${spec.name}:${r2(spec.radius)}:${r2(spec.angle)}:${r2(spec.width)}:${r2(spec.inner)}:${thickness}`,
    build: () => shapeGeometry(spec, thickness),
    color: shapeColor(s, palette),
    opacity: kind === 'telegraph' ? palette.telegraphOpacity : kind === 'projectile' ? 1 : palette.fieldOpacity,
    lift: kind === 'projectile' ? (precise ? 0 : Math.max(.1,s.height*.5)) - thickness / 2 : kind === 'telegraph' ? 0.07 : 0.05,
    telegraph: kind === 'telegraph',
  };
}

/** What a state says a player wears; presence also changes the body when legacy defaults become an empty outfit. */
export function appearanceKey(s: EntityState): string {
  return s.appearance === undefined ? '' : `:${s.appearance.join('|')}`;
}

/** The label line over a body, as the desktop client writes it. */
export function titleFor(s: EntityState): string {
  let title = s.name;
  if (s.level > 0 && hasResources(s) && !s.is_player) {
    title = `${s.name} [${s.level}]`;
  }
  if (s.dead) {
    title += ' (dead)';
  }
  if (s.casting != null) {
    title += `  ${s.casting} ${Math.floor(s.cast_progress * 100)}%`;
  }
  return title;
}

/**
 * The appearance pipeline the layer draws with: the catalog, the baker, and the settings every body reads.
 * Plans are built per entity, since their seeded variation is the entity's own; atlases are shared per look and gear.
 */
export class AppearanceService {
  readonly catalog: AppearanceCatalog;
  readonly baker: CardBaker;
  mode: BodiesMode = 'auto';
  reducedMotion = false;
  shadows = false;
  cardZoom = 36;
  isometric = true;
  orthoSize = 28;
  cameraYaw = 0;
  /** The self's worn equipment, by slot, so its body shows what it wears. */
  selfEquipment: (() => Record<string, ItemInfo>) | null = null;
  private readonly atlases = new Map<string, CardAtlas>();

  constructor(kinds: Map<string, Obj[]>, renderer: WebGLRenderer) {
    this.catalog = new AppearanceCatalog(kinds);
    this.baker = new CardBaker(renderer);
  }

  wantsCard(policy: string, isCardNow: boolean, height: number): boolean {
    if (policy === 'never' || height > 4) {
      return false;
    }
    switch (this.mode) {
      case 'geometry':
        return false;
      case 'cards':
        return true;
      default: {
        if (!this.isometric) {
          return false;
        }
        if (policy === 'prefer') {
          return true;
        }
        const threshold = isCardNow ? this.cardZoom - 4 : this.cardZoom + 4;
        return this.orthoSize >= threshold;
      }
    }
  }

  lookFor(s: EntityState): AppearanceDef | null {
    return this.catalog.resolve(s.look ?? s.pattern, s.pattern);
  }

  /**
   * A built plan for an entity with the gear it wears: the self's own equipment, another player's public appearance
   * from the wire, a monster's grants, a Calling's kit (AppearanceCatalog.wornBy).
   */
  planFor(s: EntityState, look: AppearanceDef, isSelf: boolean, key: number | string): { plan: BodyPlan; gearKey: string } {
    const plan = buildBody(look, key, s.height, s.radius);
    let gearKey = look.id;
    const dropLook = s.look ?? '';
    if (look.body === 'drop' && dropLook.startsWith('drop:')) {
      const gear = this.catalog.gearForItem(dropLook.slice(5));
      if (gear) {
        lay(plan, gear, key);
        gearKey += `+${gear.id}`;
      }
    } else if (plan.sockets.size > 0) {
      const worn = this.catalog.wornBy(look, s.pattern, s.is_player ? s.appearance : undefined, isSelf ? this.selfEquipment?.() : undefined);
      for (const slot of [...worn.keys()].sort()) {
        const item = worn.get(slot)!;
        const gear = this.catalog.gearFor(slot, item.itemClass, item.tags);
        if (!gear) {
          continue;
        }
        wear(plan, gear, slot, key);
        gearKey += `+${slot}=${gear.id}`;
      }
    }
    return { plan, gearKey };
  }

  atlasFor(key: string, unjitteredPlan: () => BodyPlan): CardAtlas {
    let atlas = this.atlases.get(key);
    if (!atlas) {
      atlas = new CardAtlas(key);
      this.atlases.set(key, atlas);
      this.baker.enqueue(atlas, unjitteredPlan());
    }
    return atlas;
  }

  get atlasBytes(): number {
    let bytes = 0;
    for (const a of this.atlases.values()) {
      bytes += a.bytes;
    }
    return bytes;
  }

  get atlasCount(): number {
    return [...this.atlases.values()].filter((a) => a.ready).length;
  }
}

export class EntityView {
  private hitDebug: HitBodyDebug | null = null;
  readonly root = new Group();
  readonly label: HTMLDivElement;
  private readonly nameEl: HTMLDivElement;
  private readonly statusEl: HTMLDivElement;
  private readonly lifeEl: HTMLDivElement;
  private readonly lifeFill: HTMLDivElement;
  private shapeMesh: Mesh | null = null;
  /** Drawn by a delivery shape, facing its heading. */
  private shaped = false;
  /** A telegraph's own material, whose opacity fades; null for every other body. */
  private fade: MeshBasicMaterial | null = null;
  private fill: Mesh | null = null;
  private fillMaterial: MeshBasicMaterial | null = null;
  private fadeOpacity = 0;
  private bornAt = 0;
  private bornServer = 0;
  dyingAt: number | null = null;
  pickSize: { radius: number; height: number } | null = null;
  private readonly samples: Sample[] = [];
  private readonly target = new Vector3();
  private readonly velocity = new Vector3();
  private receivedAt = 0;
  private labelX = Number.NaN;
  private labelY = Number.NaN;
  private labelShown = false;
  private labelAlpha = 1;
  state: EntityState;

  // the built body
  private body: BodyView | null = null;
  private plan: BodyPlan | null = null;
  private look: AppearanceDef | null = null;
  private gearKey = '';
  private atlas: CardAtlas | null = null;
  private mark: ContactMark | null = null;
  private readonly visual: VisualState = freshVisualState();
  private readonly pose = new Pose();
  private readonly lastDrawn = new Vector3();
  private hasDrawn = false;
  private speed = 0;
  private releasedAt = Number.NEGATIVE_INFINITY;
  private hitAt = Number.NEGATIVE_INFINITY;
  private hitStrength = 0;
  private hitFrom = Number.NaN;
  private diedAt = Number.NEGATIVE_INFINITY;
  private wasCasting = false;
  private wasDead = false;
  private settled = false;
  private selfEquipmentKey = '';

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

  /** Triangles the body draws, for the measurement line. */
  get triangles(): number {
    return this.body?.triangles ?? 0;
  }

  get isCard(): boolean {
    return this.body instanceof CardView;
  }

  /** The drawn body's height, for labels and picking. */
  get drawnHeight(): number {
    return this.plan?.height ?? this.state.height;
  }

  apply(state: EntityState, serverTime: number, localSeconds: number, snap: boolean): void {
    const previous = this.state;
    const now = this.layer.serverNow;
    if (state.casting == null && previous.casting != null) {
      this.releasedAt = now;
    }
    if (state.dead && !previous.dead) {
      this.diedAt = now;
    } else if (!state.dead && previous.dead) {
      this.diedAt = Number.NEGATIVE_INFINITY;
    }
    if (snap && state.dead) {
      this.diedAt = Math.min(this.diedAt, now - 5);
    }
    this.state = state;
    pushSample(this.samples, { t: serverTime, x: state.x, y: state.y, z: state.z, yaw: state.yaw, vx: state.vx, vy: state.vy, vz: state.vz });
    toThree(state.x, state.y, state.z, this.target);
    toThree(state.vx, state.vy, state.vz, this.velocity);
    this.receivedAt = localSeconds;
    if (snap) {
      this.bornAt = localSeconds;
      this.bornServer = this.layer.serverNow;
    }
    const rebuild = snap || state.look !== previous.look || Math.abs(state.height - previous.height) > 0.01 || (state.interactable == null) !== (previous.interactable == null)
      || state.shape !== previous.shape || JSON.stringify(state.shape_size) !== JSON.stringify(previous.shape_size) || state.faction !== previous.faction
      || appearanceKey(state) !== appearanceKey(previous);
    if (rebuild) {
      this.rebuild();
    }
    if (snap) {
      this.root.position.copy(this.target);
      this.root.rotation.y = this.facing(state.yaw);
      this.lastDrawn.copy(this.target);
      this.hasDrawn = true;
    }
    this.refreshLabel();
  }

  /** The body's turn about the vertical: a shape faces its heading (else the yaw) plus its spin; a body faces its yaw, forward along its x. */
  private facing(yaw: number): number {
    return this.shaped ? (this.state.heading ?? yaw) + (this.state.spin ?? 0) : yaw;
  }

  private dropBody(): void {
    if (this.body !== null) {
      this.root.remove(this.body.root);
      this.body.dispose();
      this.body = null;
    }
    this.plan = null;
    this.atlas = null;
    if (this.mark !== null) {
      this.root.remove(this.mark.mesh);
      this.mark = null;
    }
  }

  private rebuild(): void {
    const s = this.state;
    this.shaped = isShaped(s);
    if (this.shaped) {
      this.buildShape();
      return;
    }
    this.fade?.dispose();
    this.fade = null;
    this.fillMaterial?.dispose();
    this.fillMaterial = null;
    if (this.fill !== null) {
      this.fill.visible = false;
    }
    if (this.shapeMesh !== null) {
      this.shapeMesh.visible = false;
    }
    this.dropBody();
    const service = this.layer.appearance;
    this.look = service.lookFor(s) ?? { id: 'block', match: [], priority: 0, body: 'block', slot: null, shape: {}, palette: {}, features: [], gear: {}, variation: { hue: 0, value: 0, scale: 0 }, card: 'never', scale: 1, glow: null };
    const built = service.planFor(s, this.look, this.isSelf, s.key);
    this.plan = built.plan;
    this.gearKey = built.gearKey;
    this.selfEquipmentKey = this.isSelf ? this.equipmentKey() : '';
    this.visual.rig = this.plan.rig;
    this.visual.swings = this.plan.swings;
    this.visual.height = this.plan.height;
    this.visual.hover = this.plan.hover;
    this.visual.key = s.key;
    const geometry = new BodyMesh(this.plan);
    this.root.add(geometry.root);
    this.body = geometry;
    const drop = isDrop(s);
    this.pickSize = this.look.body === 'orb' ? null : { radius: Math.max(0.3, this.plan.width * 0.5), height: Math.max(0.3, this.plan.height + this.plan.hover) };
    const creature = hasResources(s);
    if (creature || drop || s.interactable != null || this.plan.rig === 'tree') {
      const relation = service.catalog.relationOf(s.faction);
      const contact = service.catalog.motion.contact;
      const style = !creature || this.isSelf ? 'plain' : relation === 'hostile' ? contact.hostile : relation === 'friendly' || relation === 'allied' ? contact.friendly : contact.neutral;
      if (style !== 'none') {
        const size = drop ? 0.9 : Math.min(4, Math.max(0.5, Math.max(this.plan.width, 0.5) * (this.plan.rig === 'tree' ? 0.5 : 1.25)));
        this.mark = new ContactMark(size, markStyle(style), contact.opacity);
        this.mark.setFade(creature ? 1 : 0.55);
        this.root.add(this.mark.mesh);
      }
    }
  }

  private equipmentKey(): string {
    const equipment = this.layer.appearance.selfEquipment?.();
    return equipment ? ':' + Object.entries(equipment).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([slot, item]) => `${slot}:${item.base}`).join(',') : '';
  }

  /** The self's worn gear changed: its body is built again with it. */
  refreshEquipment(): void {
    if (this.isSelf && !this.shaped && this.equipmentKey() !== this.selfEquipmentKey) {
      this.rebuild();
    }
  }

  private buildShape(): void {
    const s = this.state;
    this.pickSize = null;
    this.dropBody();
    const shape = shapeFor(s, this.layer.palette, this.layer.preciseProjectileSlabs);
    const opacity = s.dead ? Math.min(shape.opacity, this.layer.palette.deadOpacity) : shape.opacity;
    let material: MeshLambertMaterial | MeshBasicMaterial = this.layer.material(shape.color, opacity);
    if (shape.telegraph) {
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
    if (this.shapeMesh === null) {
      this.shapeMesh = new Mesh(this.layer.geometry(shape.key, shape.build), material);
      this.root.add(this.shapeMesh);
    } else {
      this.shapeMesh.geometry = this.layer.geometry(shape.key, shape.build);
      this.shapeMesh.material = material;
    }
    this.shapeMesh.visible = true;
    this.shapeMesh.castShadow = false;
    this.shapeMesh.renderOrder = shape.telegraph ? 2 : 0;
    this.shapeMesh.position.y = shape.lift;
  }

  /** A blow landed on this body: the animator flinches it away from where it came and flashes it. */
  hit(amount: number, from: Vector3 | null): void {
    this.hitAt = this.layer.serverNow;
    this.layer.cue?.('hit', this.plan?.rig ?? null, this.root.position);
    const max = this.state.resource_max.life ?? 0;
    this.hitStrength = Math.min(1, Math.max(0.25, (amount / (max > 0 ? max : 100)) * 4));
    if (from !== null) {
      const dx = from.x - this.root.position.x;
      const dz = from.z - this.root.position.z;
      const toSource = Math.atan2(-dz, dx);
      let rel = toSource - this.root.rotation.y;
      rel = ((rel + Math.PI) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2) - Math.PI;
      this.hitFrom = rel;
    } else {
      this.hitFrom = Number.NaN;
    }
  }

  /** This body landed a blow: its swing comes through now even if the snapshot missed the cast. */
  struck(): void {
    if (!this.wasCasting) {
      this.releasedAt = this.layer.serverNow;
    }
    this.layer.cue?.('swing', this.plan?.swings ? 'weapon' : this.plan?.rig ?? null, this.root.position);
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
    this.nameEl.textContent = titleFor(s);
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
    if(this.layer.showHitBodies && this.layer.hitBodyCatalog && hasResources(this.state) && !this.state.dead) {
      if(!this.hitDebug) { this.hitDebug=new HitBodyDebug(); this.layer.group.add(this.hitDebug.group); }
      this.hitDebug.group.visible=true;
      this.hitDebug.update(this.state,this.layer.hitBodyCatalog);
      this.label.title='Cyan: server hit body. Yellow: movement clearance. Missing cyan: unknown volume.';
    } else if(this.hitDebug) this.hitDebug.group.visible=false;
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
    } else if (this.dyingAt === null) {
      const pose = sampleAt(this.samples, renderServerTime, motion.maxExtrapolationMs / 1000);
      if (pose !== null) {
        toThree(pose.x, pose.y, pose.z, this.root.position);
        this.root.rotation.y = this.facing(pose.yaw);
      }
    }
    if (this.body !== null && this.plan !== null) {
      this.animateBody(dt);
    }
  }

  /** Feeds the animator the body's state and applies the pose; swaps the representation when the camera asks. */
  private animateBody(dt: number): void {
    const service = this.layer.appearance;
    const s = this.state;
    const serverNow = this.layer.serverNow;
    const v = this.visual;
    const plan = this.plan!;
    // a settled static prop (a rock, a chest, a marker) keeps its pose until something happens to it
    const still = plan.rig === 'static' && !s.dead && serverNow - this.hitAt > 1 && this.settled;
    if (still) {
      this.chooseRepresentation();
      return;
    }
    this.settled = plan.rig === 'static';
    if (this.hasDrawn) {
      const dx = this.root.position.x - this.lastDrawn.x;
      const dz = this.root.position.z - this.lastDrawn.z;
      const distance = Math.hypot(dx, dz);
      const speed = dt > 1e-4 ? distance / dt : 0;
      this.speed += (speed - this.speed) * Math.min(1, dt * 10);
      const stride = service.catalog.motion.walk('stride', 1.5) * Math.min(2.5, Math.max(0.4, plan.height / 1.8));
      const before = v.walkPhase;
      v.walkPhase = (v.walkPhase + (distance / Math.max(0.2, stride)) * Math.PI * 2) % (Math.PI * 2);
      // a foot plants each half cycle: the footstep cue, with the surface underfoot as its variant
      if (this.speed > 0.5 && s.grounded && !s.dead && (plan.rig === 'biped' || plan.rig === 'quadruped') && Math.floor(before / Math.PI) !== Math.floor(v.walkPhase / Math.PI)) {
        this.layer.cue?.('footstep', this.layer.surfaceAt?.(this.root.position) ?? null, this.root.position);
      }
      const fx = Math.cos(this.root.rotation.y);
      const fz = -Math.sin(this.root.rotation.y);
      const along = dt > 1e-4 ? (dx * fx + dz * fz) / dt : 0;
      const target = Math.min(8, Math.max(-8, (along - this.speed) * 2));
      v.leanForward += (target - v.leanForward) * Math.min(1, dt * 6);
    }
    this.lastDrawn.copy(this.root.position);
    this.hasDrawn = true;
    v.speed = this.speed;
    v.time = serverNow;
    v.grounded = s.grounded;
    v.leaping = s.flags.includes('leaping');
    v.dashing = s.flags.includes('dashing');
    v.dead = s.dead;
    v.deadFor = s.dead ? Math.max(0, serverNow - this.diedAt) : 0;
    if (s.dead && !this.wasDead) {
      this.layer.cue?.('death', plan.rig, this.root.position);
    }
    this.wasDead = s.dead;
    let casting = s.casting != null && s.casting.length > 0;
    let mode = 'cast';
    let progress = s.cast_progress;
    const own = this.isSelf ? this.layer.selfCast?.() : null;
    if (own) {
      casting = true;
      mode = own.mode;
      progress = own.progress;
    }
    v.casting = casting;
    v.castMode = mode;
    v.castProgress = progress;
    this.wasCasting = casting;
    v.releaseAge = Math.max(0, serverNow - this.releasedAt);
    v.hitAge = Math.max(0, serverNow - this.hitAt);
    v.hitStrength = this.hitStrength;
    v.hitFrom = this.hitFrom;
    v.reducedMotion = this.layer.reducedMotion;
    animate(v, service.catalog.motion, this.pose);
    this.chooseRepresentation();
    this.body!.apply(this.pose, service.cameraYaw);
    this.mark?.setFade((hasResources(s) ? 1 : 0.55) * this.pose.opacity);
  }

  /** Geometry or a card, as the bodies option and the camera decide; a card waits for its atlas. */
  private chooseRepresentation(): void {
    const service = this.layer.appearance;
    const plan = this.plan;
    const look = this.look;
    if (plan === null || look === null) {
      return;
    }
    const isCard = this.body instanceof CardView;
    const wantCard = service.wantsCard(look.card, isCard, plan.height);
    if (wantCard && !isCard) {
      const state = this.state;
      this.atlas ??= service.atlasFor(this.gearKey, () => service.planFor(state, { ...look, variation: { hue: 0, value: 0, scale: 0 } }, this.isSelf, 0).plan);
      if (this.atlas.ready) {
        this.body!.root.removeFromParent();
        this.body!.dispose();
        this.body = new CardView(plan, this.atlas, plan.height / Math.max(0.1, this.atlas.height));
        this.root.add(this.body.root);
      }
    } else if (!wantCard && isCard) {
      this.body!.root.removeFromParent();
      this.body!.dispose();
      this.body = new BodyMesh(plan);
      this.root.add(this.body.root);
    }
  }

  /** Whether the body is a telegraph mark, which lingers to fade out when it goes. */
  get isTelegraph(): boolean {
    return this.fade !== null;
  }

  placeLabel(camera: Camera, width: number, height: number, scratch: Vector3, dt: number): void {
    if (!this.labelShown) {
      return;
    }
    const s = this.state;
    // a creature's name always shows; a prop's shows under the cursor or within reach of the hero, fading
    if (!hasResources(s) && s.interactable != null) {
      let near = this.layer.hoveredKey !== null && String(this.layer.hoveredKey) === String(this.key);
      if (!near && this.layer.propLabelReach <= 0) {
        near = true;
      } else if (!near) {
        const hero = this.layer.self;
        if (hero !== undefined && hero !== this) {
          const dx = hero.root.position.x - this.root.position.x;
          const dz = hero.root.position.z - this.root.position.z;
          near = Math.hypot(dx, dz) <= this.layer.propLabelReach;
        }
      }
      this.labelAlpha += ((near ? 1 : 0) - this.labelAlpha) * Math.min(1, dt * 6);
      if (this.labelAlpha < 0.02) {
        this.label.style.display = 'none';
        return;
      }
      this.label.style.opacity = this.labelAlpha.toFixed(2);
    } else {
      this.label.style.opacity = '';
    }
    scratch.copy(this.root.position);
    scratch.y += Math.max(this.drawnHeight, this.pickSize?.height ?? 0) + 0.5;
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
    this.hitDebug?.dispose();
    this.fade?.dispose();
    this.fillMaterial?.dispose();
    this.body?.dispose();
  }
}

export class EntityLayer {
  hitBodyCatalog: Map<string, HitBodyInfo> | null = null;
  preciseProjectileSlabs = false;
  showHitBodies = false;
  readonly group = new Group();
  readonly views = new Map<Int64, EntityView>();
  selfKey: Int64 | null = null;
  propLabels = true;
  /** How near the hero a prop must be for its name to show; zero shows them all. */
  propLabelReach = 7;
  hoveredKey: Int64 | null = null;
  reducedMotion = false;
  serverNow = 0;
  /** The self's cast as the interface knows it (mode and progress), richer than the entity state's. */
  selfCast: (() => { mode: string; progress: number } | null) | null = null;
  /** Where the animator's moments go as sound cues (a footstep on a surface, a swing, a hit, a death); null raises none. */
  cue: ((cue: string, variant: string | null, position: Vector3) => void) | null = null;
  /** The surface tag under a drawn point (grass, sand, stone ...), for the footstep variant; null when unknown. */
  surfaceAt: ((position: Vector3) => string | null) | null = null;
  static readonly fadeSeconds = 0.35;
  private readonly fading: EntityView[] = [];
  private readonly geometries = new Map<string, BufferGeometry>();
  private readonly materials = new Map<string, MeshLambertMaterial>();
  private readonly scratch = new Vector3();

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
    readonly appearance: AppearanceService,
  ) {
    this.group.name = 'entities';
    this.propLabelReach = appearance.catalog.motion.labels('prop_reach', 7);
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

  /** Triangles the drawn bodies hold, how many are cards, and how many bodies there are, for the measurement line. */
  get bodyCounts(): { triangles: number; cards: number; bodies: number } {
    let triangles = 0;
    let cards = 0;
    let bodies = 0;
    for (const view of this.views.values()) {
      triangles += view.triangles;
      if (view.triangles > 0) {
        bodies++;
      }
      if (view.isCard) {
        cards++;
      }
    }
    return { triangles, cards, bodies };
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

  /** A blow landed: the struck body flinches and flashes, the striker's swing comes through. */
  combatHit(kind: string, source: Int64, target: Int64, amount: number): void {
    const struck = this.views.get(target);
    const striker = this.views.get(source);
    if (kind === 'damage' && struck !== undefined) {
      struck.hit(amount, striker?.root.position ?? null);
    }
    if ((kind === 'damage' || kind === 'evade' || kind === 'block') && striker !== undefined) {
      striker.struck();
    }
  }

  /** The self's worn gear changed: its body shows it. */
  refreshSelfEquipment(): void {
    this.self?.refreshEquipment();
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
  placeLabels(camera: Camera, width: number, height: number, dt: number): void {
    for (const view of this.views.values()) {
      view.placeLabel(camera, width, height, this.scratch, dt);
    }
  }

  /**
   * The entity whose body a ray passes through, ignoring the player's own. Creatures and interactables win
   * over plain props; among overlapping bodies the one the ray passes closest to the middle of wins.
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
