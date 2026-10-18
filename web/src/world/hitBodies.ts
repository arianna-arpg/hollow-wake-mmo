import { BufferGeometry, Float32BufferAttribute, Group, LineBasicMaterial, LineSegments } from 'three';
import type { EntityState, HitBodyInfo } from '../net/protocol';
import { toThree } from './coords';

const profiles: Record<string, (a: number) => [number, number]> = {
  ellipsoid: a => [Math.cos(a), Math.sin(a)],
  cylinder: a => [Math.cos(a) >= -.000001 ? 1 : -1, Math.sin(a)],
};
export const legacyHitBody: HitBodyInfo = { id: '', aim: 0, volumes: [{ shape: 'cylinder', forward: 0, left: 0, up: .5, radius: 1, half_height: .5 }] };
export function supportedHitBody(body: HitBodyInfo): boolean { return body.volumes.every(v => profiles[v.shape] !== undefined); }
/** Core-space line endpoints; these are presentation, never picking or client hit decisions. */
export function hitBodyLines(body: HitBodyInfo, radius: number, height: number, segments = 32): number[][] {
  const lines: number[][] = [];
  if (!supportedHitBody(body)) return lines;
  const horizontal = body.id === '' ? radius : height;
  for (const v of body.volumes) {
    const c = [v.forward * horizontal, v.left * horizontal, v.up * height], r = v.radius * horizontal, h = v.half_height * height;
    for (let plane = 0; plane < 3; plane++) for (let i = 0; i < segments; i++) for (let j = 0; j < 2; j++) {
      const a = (i + j) * Math.PI * 2 / segments, [x, y] = profiles[v.shape]!(a);
      const p = plane === 0 ? [Math.cos(a) * r, Math.sin(a) * r, 0] : plane === 1 ? [x * r, 0, y * h] : [0, x * r, y * h];
      lines.push(c.map((n, k) => n + p[k]!));
    }
  }
  return lines;
}
export class HitBodyDebug {
  readonly group = new Group();
  private key = '';
  private body: HitBodyInfo | undefined;
  private readonly damageMaterial = new LineBasicMaterial({ color: 0x33ffff });
  private readonly clearanceMaterial = new LineBasicMaterial({ color: 0xffbf19 });
  private clear(): void {
    for (const child of [...this.group.children]) { (child as LineSegments).geometry.dispose(); this.group.remove(child); }
  }
  update(state: EntityState, catalog: Map<string, HitBodyInfo>): void {
    const body = state.hit_body == null ? legacyHitBody : catalog.get(state.hit_body);
    const key = `${state.hit_body}:${state.radius}:${state.height}`;
    if (key !== this.key || body !== this.body) {
      this.key = key; this.body = body; this.clear();
      const add = (points: number[][], material: LineBasicMaterial) => {
        const positions = points.flatMap(p => toThree(p[0]!, p[1]!, p[2]!).toArray());
        const g = new BufferGeometry(); g.setAttribute('position', new Float32BufferAttribute(positions, 3));
        this.group.add(new LineSegments(g, material));
      };
      if (body) add(hitBodyLines(body, state.radius, state.height), this.damageMaterial);
      const clearance: number[][] = [];
      for(let i=0;i<32;i++)for(let j=0;j<2;j++) { const a=(i+j)*Math.PI*2/32; clearance.push([Math.cos(a)*state.radius,Math.sin(a)*state.radius,.01]); }
      add(clearance,this.clearanceMaterial);
    }
    toThree(state.x, state.y, state.z, this.group.position);
    this.group.rotation.y = state.yaw;
  }
  dispose(): void { this.clear(); this.group.removeFromParent(); this.damageMaterial.dispose(); this.clearanceMaterial.dispose(); }
}
