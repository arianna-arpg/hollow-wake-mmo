import { describe, expect, it } from 'vitest';
import { CellFlag, ChunkStore, chunkFromMessage, chunksAround, decodeBase64, floorDiv, mod } from './chunks';
import { chunkMessage } from './testChunks';

describe('chunks', () => {
  it('decodes the base64 flags byte array the server writes', () => {
    expect([...decodeBase64('AQID')]).toEqual([1, 2, 3]);
  });

  it('turns a chunk_data message into typed arrays, tag sets and layers', () => {
    const msg = chunkMessage(2, -1, 4, (lx, ly) => lx + ly * 10, {
      flags: (lx) => (lx === 0 ? CellFlag.water : CellFlag.walkable),
      tags: ['', 'ground grass', 'ground water shallow'],
      surface: (lx) => (lx === 0 ? 2 : 1),
      water: 3.5,
      layers: [{ index: 5, height: 9, walkable: true, tags: 'bridge structure' }],
    });
    const chunk = chunkFromMessage(msg);
    expect(chunk.x).toBe(2);
    expect(chunk.y).toBe(-1);
    expect(chunk.height[1 * 4 + 2]).toBe(12);
    expect(chunk.flags[0]).toBe(CellFlag.water);
    expect(chunk.tagTable[chunk.surface[0]].has('shallow')).toBe(true);
    expect(chunk.tagTable[chunk.surface[1]].has('grass')).toBe(true);
    expect(chunk.waterLevel).toBe(3.5);
    expect(chunk.layers.get(5)?.[0].tags.has('bridge')).toBe(true);
  });

  it('keeps out-of-range surface indexes from breaking lookups', () => {
    const msg = chunkMessage(0, 0, 2, () => 0, { tags: ['ground'], surface: () => 7 });
    const chunk = chunkFromMessage(msg);
    expect([...chunk.surface]).toEqual([0, 0, 0, 0]);
  });

  it('divides like WorldLayout for negative coordinates', () => {
    expect(floorDiv(-1, 32)).toBe(-1);
    expect(floorDiv(-32, 32)).toBe(-1);
    expect(floorDiv(-33, 32)).toBe(-2);
    expect(mod(-1, 32)).toBe(31);
  });

  it('samples heights bilinearly between cell centers, as Terrain.HeightAt', () => {
    const store = new ChunkStore(4);
    // A ramp rising one unit per cell eastwards, across two chunks.
    store.set(chunkFromMessage(chunkMessage(0, 0, 4, (lx) => lx)));
    store.set(chunkFromMessage(chunkMessage(1, 0, 4, (lx) => 4 + lx)));
    expect(store.heightAt(0.5, 0.5)).toBeCloseTo(0);
    expect(store.heightAt(1.0, 0.5)).toBeCloseTo(0.5);
    expect(store.heightAt(4.0, 0.5)).toBeCloseTo(3.5);
    // Below y = 0.5 the sample reaches into chunk (0, -1), which is not here.
    expect(store.heightAt(1.0, 0.2)).toBeUndefined();
    expect(store.chunkOf(-0.5, 7.9)).toEqual({ x: -1, y: 1 });
  });

  it('lists chunks around a center nearest first', () => {
    const list = chunksAround({ x: 5, y: 5 }, 1);
    expect(list).toHaveLength(9);
    expect(list[0]).toEqual({ x: 5, y: 5 });
    expect(Math.abs(list[1].x - 5) + Math.abs(list[1].y - 5)).toBe(1);
  });
});
