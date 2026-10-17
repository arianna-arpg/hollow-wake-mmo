import type { ChunkDataMsg } from '../net/protocol';
import { CellFlag } from './chunks';

/** Builds chunk_data messages for tests the way the server's Snapshots.Chunk does. */
export function chunkMessage(
  x: number,
  y: number,
  size: number,
  height: (lx: number, ly: number) => number,
  options: { flags?: (lx: number, ly: number) => number; tags?: string[]; surface?: (lx: number, ly: number) => number; water?: number; layers?: ChunkDataMsg['layers'] } = {},
): ChunkDataMsg {
  const cells = size * size;
  const heights: number[] = new Array(cells);
  const flags = new Uint8Array(cells);
  const surface: number[] = new Array(cells);
  for (let ly = 0; ly < size; ly++) {
    for (let lx = 0; lx < size; lx++) {
      const i = ly * size + lx;
      heights[i] = height(lx, ly);
      flags[i] = options.flags?.(lx, ly) ?? CellFlag.walkable;
      surface[i] = options.surface?.(lx, ly) ?? (options.tags && options.tags.length > 1 ? 1 : 0);
    }
  }
  let binary = '';
  for (const b of flags) {
    binary += String.fromCharCode(b);
  }
  return {
    t: 'chunk_data',
    x,
    y,
    size,
    height: heights,
    flags: btoa(binary),
    tags: options.tags ?? ['', 'ground grass'],
    surface_tag: surface,
    layers: options.layers ?? [],
    gen_version: 1,
    water_level: options.water,
  };
}
