import { describe, expect, it } from 'vitest';
import { Vector3 } from 'three';
import { ChunkStore, chunkFromMessage } from './chunks';
import { TerrainView } from './terrain';
import { chunkMessage } from './testChunks';
import { toThree } from './coords';

function terrain(height: number): TerrainView {
  const view = Object.create(TerrainView.prototype) as TerrainView;
  const store = new ChunkStore(4);
  store.set(chunkFromMessage(chunkMessage(0, 0, 4, () => 0, {
    layers: [{ index: 5, height, walkable: true, tags: 'structure bridge' }],
  })));
  Object.assign(view, { store, scratch: new Vector3() });
  view.isBuilt = (x, y) => x === 0 && y === 0;
  return view;
}

describe('cursor surface ray', () => {
  for (const height of [1.2, 3]) {
    it(`picks the visible ${height}m deck instead of base terrain`, () => {
      const hit = terrain(height).rayToGround(toThree(1.5, 1.5, 10), toThree(0, 0, -1));
      expect(hit).not.toBeNull();
      expect(hit!.z).toBeCloseTo(height);
    });
    it(`can still pick underneath the ${height}m deck`, () => {
      const hit = terrain(height).rayToGround(toThree(1.5, 1.5, height / 2), toThree(0, 0, -1));
      expect(hit!.z).toBeCloseTo(0);
    });
  }
  it('hits oblique rays only inside the deck cell', () => {
    const view = terrain(3);
    const hit = view.rayToGround(toThree(.5, 1.5, 4), toThree(1, 0, -1).normalize());
    expect(hit!.x).toBeCloseTo(1.5);
    expect(hit!.z).toBeCloseTo(3);
    const under = view.rayToGround(toThree(.9, 1.5, 2.9), toThree(.1, 0, -1).normalize());
    expect(under!.z).toBeCloseTo(0);
  });
  it('ignores invisible and missing chunks without adding data', () => {
    const view = terrain(3);
    expect(view.rayToGround(toThree(10, 10, 10), toThree(0, 0, -1), 12)).toBeNull();
    view.isBuilt = () => false;
    expect(view.rayToGround(toThree(1.5, 1.5, 10), toThree(0, 0, -1))).toBeNull();
    expect(view.store.size).toBe(1);
  });
});
