import {
  BufferAttribute,
  BufferGeometry,
  Color,
  DoubleSide,
  Group,
  InstancedMesh,
  Matrix4,
  Mesh,
  MeshLambertMaterial,
  PlaneGeometry,
  Quaternion,
  ShaderMaterial,
  Vector3,
} from 'three';
import type { WorldSettings } from '../config';
import type { FoliageRule } from './appearance/dayCycle';
import { hash2, type SurfaceLook } from './appearance/surfaceLook';
import { hasTag } from './appearance/looks';
import { CellFlag, ChunkStore, chunkKey, chunksAround, ring, type Chunk, type ChunkCoord } from './chunks';
import { toCore, toThree, type CorePoint } from './coords';
import { lerp, toLinear } from './palette';
import { buildGround, hasWater } from './terrainMesh';
import { rayToGround } from './terrainRay';

/**
 * The land around the player, streamed from the server: chunks are requested nearest first within the view
 * radius plus one ring (so every mesh is built knowing its neighbours), a few at a time; meshes are built a
 * few per frame once a chunk and its eight neighbours are here; far meshes and data are dropped. The browser
 * cannot run the generator, so where the desktop client generates a chunk this one asks for it. Colours come
 * from the surfaces content, water is a lit rippling plane, and decorative tufts are scattered by the world
 * content's foliage rules, seeded from the cells so both clients agree.
 */

const WATER_VERTEX = `
varying vec3 vWorld;
void main() {
  vWorld = (modelMatrix * vec4(position, 1.0)).xyz;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const WATER_FRAGMENT = `
uniform vec3 shallowColor;
uniform vec3 deepColor;
uniform vec3 lightColor;
uniform float opacity;
uniform float ripple;
uniform float speed;
uniform float detail;
uniform float time;
uniform vec3 sunDirection;
varying vec3 vWorld;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
void main() {
  vec2 p = vWorld.xz;
  float t = time * speed * detail;
  float n1 = noise(p * 0.9 + vec2(t * 0.6, t * 0.4));
  float n2 = noise(p * 2.3 - vec2(t * 0.3, t * 0.7));
  float wave = n1 * 0.65 + n2 * 0.35;
  float crest = smoothstep(0.55, 0.8, wave) * ripple * detail;
  vec3 base = mix(deepColor, shallowColor, wave * 0.6 + 0.2);
  vec3 n = normalize(vec3((n1 - 0.5) * 0.35 * ripple * detail, 1.0, (n2 - 0.5) * 0.35 * ripple * detail));
  float lit = 0.65 + 0.35 * max(dot(n, sunDirection), 0.0);
  vec3 col = mix(base, lightColor, crest * 0.5) * lit + lightColor * crest * 0.12;
  gl_FragColor = vec4(col, opacity + crest * 0.2);
  #include <colorspace_fragment>
}
`;

export class TerrainView {
  readonly group = new Group();
  readonly store = new ChunkStore();
  private readonly meshes = new Map<string, Group>();
  private readonly inFlight = new Map<string, number>();
  private readonly retryAt = new Map<string, number>();
  private wantedData: ChunkCoord[] = [];
  private wantedMesh: ChunkCoord[] = [];
  private center: ChunkCoord | null = null;
  private readonly ground: MeshLambertMaterial;
  private readonly water: ShaderMaterial;
  private readonly tuft: BufferGeometry;
  private readonly tuftMaterial: MeshLambertMaterial;
  private foliageDensity = 1;
  requested = 0;

  constructor(
    private readonly settings: WorldSettings,
    private readonly look: SurfaceLook,
    private readonly foliage: FoliageRule[],
    private readonly request: (x: number, y: number) => void,
  ) {
    this.group.name = 'terrain';
    this.ground = new MeshLambertMaterial({ vertexColors: true, side: DoubleSide });
    const lin = (c: [number, number, number]) => new Color(...toLinear(c));
    this.water = new ShaderMaterial({
      vertexShader: WATER_VERTEX,
      fragmentShader: WATER_FRAGMENT,
      uniforms: {
        shallowColor: { value: lin(look.water) },
        deepColor: { value: lin(look.waterDeep) },
        lightColor: { value: lin(look.waterLight) },
        opacity: { value: look.waterOpacity },
        ripple: { value: look.waterRipple },
        speed: { value: look.waterSpeed },
        detail: { value: 1 },
        time: { value: 0 },
        sunDirection: { value: new Vector3(0.4, 0.8, 0.3).normalize() },
      },
      transparent: true,
      depthWrite: false,
      side: DoubleSide,
    });
    this.tuft = TerrainView.tuftGeometry();
    this.tuftMaterial = new MeshLambertMaterial({ vertexColors: true, side: DoubleSide });
  }

  /** A tuft: three crossed blades, white so the instance colour paints it, darker at the base. */
  private static tuftGeometry(): BufferGeometry {
    const positions: number[] = [];
    const normals: number[] = [];
    const colors: number[] = [];
    const indices: number[] = [];
    for (let i = 0; i < 3; i++) {
      const a = (Math.PI * i) / 3;
      const dx = Math.cos(a) * 0.12;
      const dz = Math.sin(a) * 0.12;
      const start = positions.length / 3;
      positions.push(-dx, 0, -dz, dx, 0, dz, dx * 0.4 + dz * 0.3, 1, dz * 0.4 - dx * 0.3, -dx * 0.4 + dz * 0.3, 1, -dz * 0.4 - dx * 0.3);
      const n = new Vector3(-dz, 0.3, dx).normalize();
      for (let k = 0; k < 4; k++) {
        normals.push(n.x, n.y, n.z);
        const shade = k < 2 ? 0.72 : 1;
        colors.push(shade, shade, shade);
      }
      indices.push(start, start + 1, start + 2, start, start + 2, start + 3);
    }
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new BufferAttribute(Float32Array.from(positions), 3));
    geometry.setAttribute('normal', new BufferAttribute(Float32Array.from(normals), 3));
    geometry.setAttribute('color', new BufferAttribute(Float32Array.from(colors), 3));
    geometry.setIndex(indices);
    return geometry;
  }

  get builtCount(): number {
    return this.meshes.size;
  }

  get pendingCount(): number {
    return this.wantedMesh.length;
  }

  /** The water's time and light, set each frame from the day. */
  animateWater(time: number, sunDirection: Vector3): void {
    this.water.uniforms.time.value = time;
    (this.water.uniforms.sunDirection.value as Vector3).copy(sunDirection);
  }

  setWaterDetail(on: boolean): void {
    this.water.uniforms.detail.value = on ? 1 : 0;
  }

  /** How many tufts the chunks grow, as a multiplier; a change rebuilds the chunks in view. */
  setFoliageDensity(density: number): void {
    const clamped = Math.min(3, Math.max(0, density));
    if (Math.abs(clamped - this.foliageDensity) < 0.001) {
      return;
    }
    this.foliageDensity = clamped;
    for (const key of [...this.meshes.keys()]) {
      this.dropMesh(key);
    }
    this.center = null;
  }

  readiness(x: number, y: number, radius: number): { completed: number; total: number } {
    const wanted = chunksAround(this.store.chunkOf(x, y), radius);
    return { completed: wanted.filter(c => this.meshes.has(chunkKey(c.x, c.y))).length, total: wanted.length };
  }

  retryPreparation(): void {
    this.retryAt.clear();
    this.inFlight.clear();
  }

  /** Forget everything (a new world layout); chunks will be requested again around the player. */
  reset(chunkSize: number): void {
    for (const key of [...this.meshes.keys()]) {
      this.dropMesh(key);
    }
    this.store.clear();
    this.store.chunkSize = chunkSize;
    this.inFlight.clear();
    this.retryAt.clear();
    this.wantedData = [];
    this.wantedMesh = [];
    this.center = null;
  }

  receive(chunk: Chunk): void {
    const key = chunkKey(chunk.x, chunk.y);
    this.inFlight.delete(key);
    this.retryAt.delete(key);
    this.store.set(chunk);
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const c = { x: chunk.x + dx, y: chunk.y + dy };
        const k = chunkKey(c.x, c.y);
        const built = this.meshes.has(k);
        if (built) {
          this.dropMesh(k);
        }
        if ((built || (dx === 0 && dy === 0)) && this.center !== null && ring(c, this.center) <= this.settings.chunkRadius
          && !this.wantedMesh.some((w) => w.x === c.x && w.y === c.y)) {
          this.wantedMesh.push(c);
        }
      }
    }
  }

  update(around: CorePoint, nowSeconds: number): void {
    const center = this.store.chunkOf(around.x, around.y);
    if (this.center === null || this.center.x !== center.x || this.center.y !== center.y) {
      this.center = center;
      this.replan(center);
    }
    this.requestMissing(nowSeconds);
    this.buildMeshes();
  }

  private replan(center: ChunkCoord): void {
    const radius = this.settings.chunkRadius;
    this.wantedData = chunksAround(center, radius + 1).filter((c) => !this.store.has(c.x, c.y));
    this.wantedMesh = chunksAround(center, radius).filter((c) => !this.meshes.has(chunkKey(c.x, c.y)));
    for (const key of [...this.meshes.keys()]) {
      const [x, y] = key.split(',').map(Number);
      if (ring({ x, y }, center) > radius + 2) {
        this.dropMesh(key);
      }
    }
    for (const chunk of [...this.store.values()]) {
      if (ring(chunk, center) > radius + 3) {
        this.store.delete(chunk.x, chunk.y);
      }
    }
  }

  private requestMissing(now: number): void {
    const timeout = this.settings.chunkRequestTimeoutSeconds;
    for (const [key, at] of this.inFlight) {
      if (now - at > timeout) {
        this.inFlight.delete(key);
        this.retryAt.set(key, now + this.settings.chunkRetrySeconds);
      }
    }
    this.wantedData = this.wantedData.filter((c) => !this.store.has(c.x, c.y));
    for (const c of this.wantedData) {
      if (this.inFlight.size >= this.settings.maxChunkRequestsInFlight) {
        return;
      }
      const key = chunkKey(c.x, c.y);
      if (this.inFlight.has(key) || (this.retryAt.get(key) ?? 0) > now) {
        continue;
      }
      this.inFlight.set(key, now);
      this.requested++;
      this.request(c.x, c.y);
    }
  }

  private neighboursReady(c: ChunkCoord): boolean {
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const x = c.x + dx;
        const y = c.y + dy;
        if (!this.store.has(x, y) && !this.retryAt.has(chunkKey(x, y))) {
          return false;
        }
      }
    }
    return true;
  }

  private buildMeshes(): void {
    let budget = this.settings.meshBuildsPerFrame;
    for (let i = 0; i < this.wantedMesh.length && budget > 0;) {
      const c = this.wantedMesh[i];
      const key = chunkKey(c.x, c.y);
      if (this.meshes.has(key)) {
        this.wantedMesh.splice(i, 1);
        continue;
      }
      const chunk = this.store.get(c.x, c.y);
      if (chunk === undefined || !this.neighboursReady(c)) {
        i++;
        continue;
      }
      this.meshes.set(key, this.buildChunk(chunk));
      this.wantedMesh.splice(i, 1);
      budget--;
    }
  }

  private buildChunk(chunk: Chunk): Group {
    const node = new Group();
    node.name = `chunk_${chunk.x}_${chunk.y}`;
    const originX = chunk.x * chunk.size;
    const originY = chunk.y * chunk.size;
    toThree(originX, originY, 0, node.position);

    const arrays = buildGround(chunk, (x, y) => this.store.get(x, y), this.look);
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new BufferAttribute(arrays.positions, 3));
    geometry.setAttribute('normal', new BufferAttribute(arrays.normals, 3));
    geometry.setAttribute('color', new BufferAttribute(arrays.colors, 3));
    geometry.setIndex(new BufferAttribute(arrays.indices, 1));
    geometry.computeBoundingSphere();
    const ground = new Mesh(geometry, this.ground);
    ground.name = 'ground';
    ground.receiveShadow = true;
    node.add(ground);

    if (hasWater(chunk)) {
      const plane = new PlaneGeometry(chunk.size, chunk.size);
      plane.rotateX(-Math.PI / 2);
      const water = new Mesh(plane, this.water);
      water.name = 'water';
      water.renderOrder = 1;
      toThree(chunk.size / 2, chunk.size / 2, chunk.waterLevel ?? 0, water.position);
      node.add(water);
    }
    const tufts = this.buildFoliage(chunk);
    if (tufts !== null) {
      node.add(tufts);
    }
    this.group.add(node);
    return node;
  }

  /** The tufts of a chunk at the density, seeded from the cells; null when the chunk grows none. */
  private buildFoliage(chunk: Chunk): InstancedMesh | null {
    if (this.foliageDensity <= 0 || this.foliage.length === 0) {
      return null;
    }
    const size = chunk.size;
    const originX = chunk.x * size;
    const originY = chunk.y * size;
    const transforms: Matrix4[] = [];
    const colors: Color[] = [];
    const position = new Vector3();
    const quaternion = new Quaternion();
    const scale = new Vector3();
    const up = new Vector3(0, 1, 0);
    for (let ly = 0; ly < size; ly++) {
      for (let lx = 0; lx < size; lx++) {
        const i = ly * size + lx;
        const flags = chunk.flags[i];
        if ((flags & (CellFlag.structure | CellFlag.road | CellFlag.water | CellFlag.blocked)) !== 0) {
          continue;
        }
        const tags = chunk.tagTable[chunk.surface[i]] ?? chunk.tagTable[0];
        const rule = this.foliage.find((r) => r.tags.every((t) => hasTag(tags, t)));
        if (!rule) {
          continue;
        }
        const wx = originX + lx;
        const wy = originY + ly;
        const want = rule.density * this.foliageDensity;
        let count = Math.floor(want);
        if (hash2(wx * 3 + 1, wy * 5 + 2) < want - count) {
          count++;
        }
        for (let k = 0; k < count && k < 4; k++) {
          const ox = hash2(wx * 7 + k * 13, wy * 11 + 3);
          const oy = hash2(wx * 17 + 5, wy * 19 + k * 7);
          const s = 0.7 + 0.6 * hash2(wx * 23 + k, wy * 29 + 1);
          const turn = hash2(wx * 31 + k * 3, wy * 37 + 2) * Math.PI * 2;
          const h = this.tuftHeight(chunk, lx, ly, ox, oy);
          // positions are relative to the chunk's origin, like the ground's
          toThree(lx + ox, ly + oy, h, position);
          quaternion.setFromAxisAngle(up, turn);
          scale.set(s, rule.height * s, s);
          transforms.push(new Matrix4().compose(position, quaternion, scale));
          const mix = hash2(wx * 41 + k, wy * 43 + 9);
          colors.push(new Color(...toLinear(lerp(rule.color, rule.tip, mix * 0.6))));
        }
      }
    }
    if (transforms.length === 0) {
      return null;
    }
    const mesh = new InstancedMesh(this.tuft, this.tuftMaterial, transforms.length);
    for (let i = 0; i < transforms.length; i++) {
      mesh.setMatrixAt(i, transforms[i]);
      mesh.setColorAt(i, colors[i]);
    }
    mesh.name = 'foliage';
    mesh.frustumCulled = false;
    return mesh;
  }

  private tuftHeight(chunk: Chunk, lx: number, ly: number, ox: number, oy: number): number {
    const size = chunk.size;
    const h = chunk.height[ly * size + lx];
    const nx = ox > 0.5 ? Math.min(size - 1, lx + 1) : Math.max(0, lx - 1);
    const ny = oy > 0.5 ? Math.min(size - 1, ly + 1) : Math.max(0, ly - 1);
    const hx = chunk.height[ly * size + nx];
    const hy = chunk.height[ny * size + lx];
    return h + (hx - h) * Math.abs(ox - 0.5) * 0.5 + (hy - h) * Math.abs(oy - 0.5) * 0.5 - 0.02;
  }

  private dropMesh(key: string): void {
    const node = this.meshes.get(key);
    if (node === undefined) {
      return;
    }
    this.meshes.delete(key);
    this.group.remove(node);
    node.traverse((o) => {
      if (o instanceof InstancedMesh) {
        o.dispose();
      } else if (o instanceof Mesh && o.name !== 'foliage') {
        o.geometry.dispose();
      }
    });
  }

  isBuilt(cx: number, cy: number): boolean {
    return this.meshes.has(chunkKey(cx, cy));
  }

  heightAt(x: number, y: number): number | undefined {
    return this.store.heightAt(x, y);
  }

  /** The first surface tag of the cell under a drawn point (grass, sand, stone ...), water when flooded, or null off the known world. */
  surfaceAt(position: Vector3): string | null {
    const core = toCore(position);
    const c = this.store.chunkOf(core.x, core.y);
    const chunk = this.store.get(c.x, c.y);
    if (chunk === undefined) {
      return null;
    }
    const lx = Math.floor(core.x) - c.x * chunk.size;
    const ly = Math.floor(core.y) - c.y * chunk.size;
    if (lx < 0 || ly < 0 || lx >= chunk.size || ly >= chunk.size) {
      return null;
    }
    const i = ly * chunk.size + lx;
    if ((chunk.flags[i] & CellFlag.water) !== 0) {
      return 'water';
    }
    const tags = chunk.tagTable[chunk.surface[i]] ?? chunk.tagTable[0];
    for (const t of tags) {
      return t;
    }
    return null;
  }

  rayToGround(origin: Vector3, direction: Vector3, maxDistance = 400): CorePoint | null {
    return rayToGround(this.store, toCore(origin), toCore(direction), c => this.isBuilt(c.x, c.y), maxDistance);
  }
  dispose(): void {
    this.reset(this.store.chunkSize);
    this.ground.dispose();
    this.water.dispose();
    this.tuft.dispose();
    this.tuftMaterial.dispose();
  }
}
