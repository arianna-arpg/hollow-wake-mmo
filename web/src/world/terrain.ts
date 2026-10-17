import {
  BufferAttribute,
  BufferGeometry,
  DoubleSide,
  Group,
  Mesh,
  MeshLambertMaterial,
  MeshStandardMaterial,
  PlaneGeometry,
  Vector3,
} from 'three';
import type { WorldSettings } from '../config';
import { ChunkStore, chunkKey, chunksAround, ring, type Chunk, type ChunkCoord } from './chunks';
import { toCore, toThree, type CorePoint } from './coords';
import { linearColor } from './colors';
import type { SurfacePalette } from './palette';
import { buildGround, hasWater } from './terrainMesh';

/**
 * The land around the player, streamed from the server: chunks are requested nearest first within the view
 * radius plus one ring (so every mesh is built knowing its neighbours), a few at a time; meshes are built a
 * few per frame once a chunk and its eight neighbours are here; far meshes and data are dropped. The browser
 * cannot run the generator, so where the desktop client generates a chunk this one asks for it.
 */
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
  private readonly water: MeshStandardMaterial;
  private readonly scratch = new Vector3();
  requested = 0;

  constructor(
    private readonly settings: WorldSettings,
    private readonly palette: SurfacePalette,
    private readonly request: (x: number, y: number) => void,
  ) {
    this.group.name = 'terrain';
    this.ground = new MeshLambertMaterial({ vertexColors: true, side: DoubleSide });
    this.water = new MeshStandardMaterial({
      color: linearColor(palette.water),
      transparent: true,
      opacity: palette.waterOpacity,
      roughness: 0.15,
      depthWrite: false,
    });
  }

  get builtCount(): number {
    return this.meshes.size;
  }

  /** Chunks in view that still have no mesh. */
  get pendingCount(): number {
    return this.wantedMesh.length;
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

  /** A chunk arrived: keep it, and rebuild it and any built neighbour whose edges blend into it. */
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

  /** Keeps chunks requested and built around a simulation position. Call every frame. */
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
        // No answer: give up on it for a while; meshes treat it as missing rather than wait forever.
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

    const arrays = buildGround(chunk, (x, y) => this.store.get(x, y), this.palette);
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
    this.group.add(node);
    return node;
  }

  private dropMesh(key: string): void {
    const node = this.meshes.get(key);
    if (node === undefined) {
      return;
    }
    this.meshes.delete(key);
    this.group.remove(node);
    node.traverse((o) => {
      if (o instanceof Mesh) {
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

  /**
   * Marches a camera ray against the height field and returns the ground point in simulation space. Only
   * built chunks count: nothing else is visible, so nothing else can be clicked.
   */
  rayToGround(origin: Vector3, direction: Vector3, maxDistance = 400): CorePoint | null {
    const below = (p: Vector3): boolean => {
      const core = toCore(p);
      const c = this.store.chunkOf(core.x, core.y);
      if (!this.isBuilt(c.x, c.y)) {
        return false;
      }
      const h = this.store.heightAt(core.x, core.y);
      return h !== undefined && core.z <= h;
    };
    const step = 0.5;
    const last = origin.clone();
    const p = this.scratch;
    for (let d = 0; d < maxDistance; d += step) {
      p.copy(direction).multiplyScalar(d).add(origin);
      if (below(p)) {
        const lo = last.clone();
        const hi = p.clone();
        const mid = new Vector3();
        for (let i = 0; i < 6; i++) {
          mid.addVectors(lo, hi).multiplyScalar(0.5);
          if (below(mid)) {
            hi.copy(mid);
          } else {
            lo.copy(mid);
          }
        }
        const core = toCore(hi);
        return { x: core.x, y: core.y, z: this.store.heightAt(core.x, core.y) ?? core.z };
      }
      last.copy(p);
    }
    return null;
  }

  dispose(): void {
    this.reset(this.store.chunkSize);
    this.ground.dispose();
    this.water.dispose();
  }
}
