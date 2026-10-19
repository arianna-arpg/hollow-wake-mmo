import { type ChunkStore, type ChunkCoord } from './chunks';
import type { CorePoint } from './coords';

/** First visible base or walkable layer hit. Twin of native World/Logic/GroundRay. */
export function rayToGround(store: ChunkStore, origin: CorePoint, direction: CorePoint,
  visible: (coord: ChunkCoord) => boolean, maxDistance = 400): CorePoint | null {
  const at = (distance: number): CorePoint => ({
    x: origin.x + direction.x * distance, y: origin.y + direction.y * distance, z: origin.z + direction.z * distance,
  });
  const height = (point: CorePoint): number | undefined =>
    visible(store.chunkOf(point.x, point.y)) ? store.heightAt(point.x, point.y) : undefined;
  const step = 0.5;
  let previous = origin;
  for (let from = 0; from < maxDistance; from += step) {
    const to = Math.min(maxDistance, from + step);
    const next = at(to);
    let best = Infinity;
    let hit: CorePoint | null = null;
    // Intersect each layer's top plane inside its cell. A ray below a deck can reach ground beneath it.
    if (direction.z < -1e-6) {
      for (let y = Math.floor(Math.min(previous.y, next.y)); y <= Math.floor(Math.max(previous.y, next.y)); y++) {
        for (let x = Math.floor(Math.min(previous.x, next.x)); x <= Math.floor(Math.max(previous.x, next.x)); x++) {
          const coord = store.chunkOf(x, y);
          const chunk = visible(coord) ? store.get(coord.x, coord.y) : undefined;
          if (!chunk) continue;
          const index = (y - coord.y * chunk.size) * chunk.size + x - coord.x * chunk.size;
          for (const surface of chunk.layers.get(index) ?? []) {
            if (!surface.walkable || surface.tags.has('ground')) continue;
            const distance = (surface.height - origin.z) / direction.z;
            if (distance < from || distance > to || distance >= best) continue;
            const point = at(distance);
            if (Math.floor(point.x) !== x || Math.floor(point.y) !== y) continue;
            best = distance;
            hit = { ...point, z: surface.height };
          }
        }
      }
    }
    const baseHeight = height(next);
    if (baseHeight !== undefined && next.z <= baseHeight) {
      let lo = from;
      let hi = to;
      for (let i = 0; i < 6; i++) {
        const mid = (lo + hi) * 0.5;
        const point = at(mid);
        const h = height(point);
        if (h !== undefined && point.z <= h) hi = mid;
        else lo = mid;
      }
      if (hi < best) {
        const point = at(hi);
        const h = height(point);
        if (h !== undefined) hit = { ...point, z: h };
      }
    }
    if (hit !== null) return hit;
    previous = next;
  }
  return null;
}
