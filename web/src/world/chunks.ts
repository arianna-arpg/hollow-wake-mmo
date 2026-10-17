import type { ChunkDataMsg } from '../net/protocol';

/**
 * Terrain as the server sends it: one chunk_data per chunk, kept in a store keyed by chunk coordinate.
 * Pure data and arithmetic, mirroring HollowWake.Core.World (ChunkData, WorldLayout, Terrain.HeightAt),
 * so it can be tested without a browser.
 */

/** CellFlags in HollowWake.Core.World.ChunkData. */
export const CellFlag = {
  walkable: 1,
  water: 2,
  blocked: 4,
  road: 8,
  structure: 16,
  flattened: 32,
} as const;

export type CellFlagName = keyof typeof CellFlag;

export interface Surface {
  height: number;
  walkable: boolean;
  tags: ReadonlySet<string>;
}

export interface Chunk {
  readonly x: number;
  readonly y: number;
  readonly size: number;
  readonly height: Float32Array;
  readonly flags: Uint8Array;
  /** The chunk's interned surface tag sets; surface[i] indexes into it. */
  readonly tagTable: ReadonlyArray<ReadonlySet<string>>;
  readonly surface: Uint16Array;
  /** Extra surfaces (bridges, floors) by cell index, lowest first. */
  readonly layers: ReadonlyMap<number, Surface[]>;
  readonly waterLevel: number | null;
}

export interface ChunkCoord {
  x: number;
  y: number;
}

export function floorDiv(a: number, b: number): number {
  return Math.floor(a / b);
}

export function mod(a: number, b: number): number {
  return ((a % b) + b) % b;
}

export function chunkKey(x: number, y: number): string {
  return `${x},${y}`;
}

export function decodeBase64(text: string): Uint8Array {
  const binary = atob(text);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

export function parseTags(text: string): Set<string> {
  return new Set(text.split(/\s+/).filter((t) => t.length > 0));
}

export function chunkFromMessage(msg: ChunkDataMsg): Chunk {
  const cells = msg.size * msg.size;
  const height = new Float32Array(cells);
  height.set(msg.height.slice(0, cells));
  const flags = new Uint8Array(cells);
  flags.set(decodeBase64(msg.flags).subarray(0, cells));
  const surface = new Uint16Array(cells);
  surface.set(msg.surface_tag.slice(0, cells));
  const tagTable = msg.tags.length > 0 ? msg.tags.map(parseTags) : [new Set<string>()];
  for (let i = 0; i < cells; i++) {
    if (surface[i] >= tagTable.length) {
      surface[i] = 0;
    }
  }
  const layers = new Map<number, Surface[]>();
  for (const layer of msg.layers) {
    const list = layers.get(layer.index) ?? [];
    list.push({ height: layer.height, walkable: layer.walkable, tags: parseTags(layer.tags) });
    list.sort((a, b) => a.height - b.height);
    layers.set(layer.index, list);
  }
  return { x: msg.x, y: msg.y, size: msg.size, height, flags, tagTable, surface, layers, waterLevel: msg.water_level ?? null };
}

/** Every chunk coordinate within a square ring radius of a center, nearest first. */
export function chunksAround(center: ChunkCoord, radius: number): ChunkCoord[] {
  const list: ChunkCoord[] = [];
  for (let dy = -radius; dy <= radius; dy++) {
    for (let dx = -radius; dx <= radius; dx++) {
      list.push({ x: center.x + dx, y: center.y + dy });
    }
  }
  const dist = (c: ChunkCoord) => (c.x - center.x) ** 2 + (c.y - center.y) ** 2;
  return list.sort((a, b) => dist(a) - dist(b));
}

export function ring(a: ChunkCoord, b: ChunkCoord): number {
  return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y));
}

export class ChunkStore {
  private readonly chunks = new Map<string, Chunk>();

  constructor(public chunkSize = 32) {}

  get size(): number {
    return this.chunks.size;
  }

  get(x: number, y: number): Chunk | undefined {
    return this.chunks.get(chunkKey(x, y));
  }

  has(x: number, y: number): boolean {
    return this.chunks.has(chunkKey(x, y));
  }

  set(chunk: Chunk): void {
    this.chunks.set(chunkKey(chunk.x, chunk.y), chunk);
  }

  delete(x: number, y: number): void {
    this.chunks.delete(chunkKey(x, y));
  }

  clear(): void {
    this.chunks.clear();
  }

  values(): IterableIterator<Chunk> {
    return this.chunks.values();
  }

  chunkOf(x: number, y: number): ChunkCoord {
    return { x: floorDiv(Math.floor(x), this.chunkSize), y: floorDiv(Math.floor(y), this.chunkSize) };
  }

  /** The base height of a cell, or undefined when its chunk is not here. */
  groundHeight(cellX: number, cellY: number): number | undefined {
    const size = this.chunkSize;
    const chunk = this.get(floorDiv(cellX, size), floorDiv(cellY, size));
    return chunk?.height[mod(cellY, size) * size + mod(cellX, size)];
  }

  /** Bilinear base height between cell centers, exactly as Terrain.HeightAt; undefined until the cells are known. */
  heightAt(x: number, y: number): number | undefined {
    const fx = x - 0.5;
    const fy = y - 0.5;
    const x0 = Math.floor(fx);
    const y0 = Math.floor(fy);
    const tx = fx - x0;
    const ty = fy - y0;
    const h00 = this.groundHeight(x0, y0);
    const h10 = this.groundHeight(x0 + 1, y0);
    const h01 = this.groundHeight(x0, y0 + 1);
    const h11 = this.groundHeight(x0 + 1, y0 + 1);
    if (h00 === undefined || h10 === undefined || h01 === undefined || h11 === undefined) {
      return undefined;
    }
    const a = h00 + (h10 - h00) * tx;
    const b = h01 + (h11 - h01) * tx;
    return a + (b - a) * ty;
  }
}
