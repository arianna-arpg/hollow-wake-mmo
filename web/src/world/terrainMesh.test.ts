import { describe, expect, it } from 'vitest';
import { CellFlag, ChunkStore, chunkFromMessage } from './chunks';
import { bundledKinds } from '../ui/logic/content';
import { AppearanceCatalog } from './appearance/looks';
import { SurfaceLook } from './appearance/surfaceLook';
import { buildGround, hasWater } from './terrainMesh';
import { chunkMessage } from './testChunks';

const palette = new SurfaceLook(new AppearanceCatalog(bundledKinds()).surfaces);

function storeOf(size: number, height: (wx: number, wy: number) => number, flags?: (wx: number, wy: number) => number): ChunkStore {
  const store = new ChunkStore(size);
  for (let cy = -1; cy <= 1; cy++) {
    for (let cx = -1; cx <= 1; cx++) {
      store.set(chunkFromMessage(chunkMessage(cx, cy, size, (lx, ly) => height(cx * size + lx, cy * size + ly), {
        flags: flags ? (lx, ly) => flags(cx * size + lx, cy * size + ly) : undefined,
      })));
    }
  }
  return store;
}

describe('terrain mesh', () => {
  it('has one quad per cell on smooth ground, facing up', () => {
    const store = storeOf(4, () => 2);
    const mesh = buildGround(store.get(0, 0)!, (x, y) => store.get(x, y), palette);
    expect(mesh.quads).toBe(16);
    expect(mesh.positions.length).toBe(16 * 4 * 3);
    expect(mesh.indices.length).toBe(16 * 6);
    for (let i = 0; i < mesh.normals.length; i += 3) {
      expect(mesh.normals[i + 1]).toBeCloseTo(1);
    }
    // Simulation (x, y, z) lands at three.js (x, z, -y), relative to the chunk origin.
    expect([...mesh.positions.slice(0, 3)]).toEqual([0, 2, -0]);
    expect(Math.max(...mesh.positions.filter((_, i) => i % 3 === 2))).toBeCloseTo(0);
    expect(Math.min(...mesh.positions.filter((_, i) => i % 3 === 2))).toBeCloseTo(-4);
  });

  it('winds triangles counter-clockwise seen from above (three.js front faces)', () => {
    const store = storeOf(2, () => 0);
    const mesh = buildGround(store.get(0, 0)!, (x, y) => store.get(x, y), palette);
    const p = (i: number) => [mesh.positions[i * 3], mesh.positions[i * 3 + 1], mesh.positions[i * 3 + 2]];
    const [a, b, c] = [p(mesh.indices[0]), p(mesh.indices[1]), p(mesh.indices[2])];
    const u = [b[0] - a[0], b[1] - a[1], b[2] - a[2]];
    const v = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
    const normalY = u[2] * v[0] - u[0] * v[2];
    expect(normalY).toBeGreaterThan(0);
  });

  it('meets its neighbours: corner heights average across the border', () => {
    const store = storeOf(4, (wx) => (wx >= 4 ? 10 : 0));
    const west = buildGround(store.get(0, 0)!, (x, y) => store.get(x, y), palette);
    const east = buildGround(store.get(1, 0)!, (x, y) => store.get(x, y), palette);
    // The shared edge at x = 4 sits at the average of 0 and 10 on both sides.
    const edgeHeights = (mesh: typeof west, localX: number) => {
      const out: number[] = [];
      for (let i = 0; i < mesh.positions.length; i += 3) {
        if (Math.abs(mesh.positions[i] - localX) < 1e-6) {
          out.push(mesh.positions[i + 1]);
        }
      }
      return out;
    };
    expect(new Set(edgeHeights(west, 4))).toEqual(new Set([5]));
    expect(new Set(edgeHeights(east, 0))).toEqual(new Set([5]));
  });

  it('adds skirts where a flattened cell steps against its neighbour', () => {
    const store = storeOf(4, (wx, wy) => (wx === 1 && wy === 1 ? 3 : 0), (wx, wy) => (wx === 1 && wy === 1 ? CellFlag.flattened | CellFlag.walkable : CellFlag.walkable));
    const mesh = buildGround(store.get(0, 0)!, (x, y) => store.get(x, y), palette);
    // 16 ground quads plus the flattened cell's east and north skirts, and its neighbours' skirts toward it.
    expect(mesh.quads).toBe(16 + 4);
  });

  it('draws layered surfaces as extra quads at their height', () => {
    const store = storeOf(4, () => 0);
    const bridged = chunkFromMessage(chunkMessage(0, 0, 4, () => 0, { layers: [{ index: 5, height: 4, walkable: true, tags: 'bridge structure' }] }));
    store.set(bridged);
    const mesh = buildGround(bridged, (x, y) => store.get(x, y), palette);
    expect(mesh.quads).toBe(17);
    expect(Math.max(...mesh.positions.filter((_, i) => i % 3 === 1))).toBe(4);
  });

  it('knows which chunks need a water plane', () => {
    expect(hasWater(chunkFromMessage(chunkMessage(0, 0, 2, () => 0, { water: 1, flags: () => CellFlag.water })))).toBe(true);
    expect(hasWater(chunkFromMessage(chunkMessage(0, 0, 2, () => 0, { water: 1 })))).toBe(false);
    expect(hasWater(chunkFromMessage(chunkMessage(0, 0, 2, () => 0, { flags: () => CellFlag.water })))).toBe(false);
  });
});
