import { describe, it, expect } from 'vitest';
import { Scene, Mesh } from 'three';
import { CastVisuals } from './castVisuals';
import { cueSettings } from './castCueTimeline';
import { bundledKinds } from '../ui/logic/content';
import type { EntityLayer } from './entities';
import type { CastCueMsg } from '../net/protocol';

describe('cast presentation renderer', () => {
  it('anchors outcomes to the affected body and preserves an authored lethal contact cue', () => {
    const scene = new Scene();
    const target = { state: { dead: true, height: 2, yaw: 0 }, position: { x: 8, y: 9, z: 10 } };
    const entities = { views: new Map([[2, target]]) } as unknown as EntityLayer;
    const visuals = new CastVisuals(scene, entities, cueSettings(bundledKinds().get('ui_cast_visuals')?.[0]));
    const event: CastCueMsg = { t: 'cast_cue', sequence: 1, cast_id: 0, phase: 'damage', source: 1, target: 2,
      weave: '', name: '', mode: 'outcome', tags: [], server_time: 10, started_at: 10, duration: 0,
      x: 4, y: 5, z: 6, aim_x: 4, aim_y: 5, aim_z: 6, fired: false, outcome: true, reason: 'damage' };
    visuals.receive(event, 10); visuals.update(10.01, true);
    const mesh = scene.children[0] as Mesh;
    expect(mesh.visible).toBe(true);
    expect(mesh.position.toArray()).toEqual([8, 11.1, -9]);
    expect(mesh.geometry.getAttribute('position').count).toBe(5 * 12);
    target.position.x = 9; visuals.update(10.02, true);
    expect(mesh.position.x).toBe(9);
    visuals.clear();
    expect(scene.children).toHaveLength(0);
  });
  it('keeps confirmed contact fixed, readable in still mode, depth-tested and disposable', () => {
    const scene = new Scene();
    const body = { state: { dead: true, height: 2, yaw: 0 }, position: { x: 100, y: 100, z: 100 } };
    const entities = { views: new Map([[2, body]]) } as unknown as EntityLayer;
    const visuals = new CastVisuals(scene, entities, cueSettings(bundledKinds().get('ui_cast_visuals')?.[0]));
    const event: CastCueMsg = { t: 'cast_cue', sequence: 1, cast_id: 1, phase: 'impact', source: 1, target: 2,
      weave: 'any', name: 'Any', mode: 'cast', tags: ['ember'], server_time: 10, started_at: 9, duration: 1,
      x: 4, y: 5, z: 6, aim_x: 4, aim_y: 5, aim_z: 6, fired: true, outcome: false, reason: 'damage' };
    visuals.receive(event, 10);
    visuals.update(10.05, true);
    const mesh = scene.children[0] as Mesh;
    expect(mesh.position.toArray()).toEqual([4, 7.1, -5]);
    expect(mesh.scale.toArray()).toEqual([1, 1, 1]);
    expect(mesh.rotation.y).toBe(0);
    expect(mesh.visible).toBe(true); // lethal confirmed contacts still show
    expect(mesh.geometry.getAttribute('position').count).toBe(7 * 12);
    const material = Array.isArray(mesh.material) ? mesh.material[0] : mesh.material;
    expect(material.depthTest).toBe(true);
    expect(material.depthWrite).toBe(false);
    const opacity = material.opacity;
    visuals.update(10.1, true);
    expect(material.opacity).toBe(opacity);
    let released = false;
    mesh.geometry.addEventListener('dispose', () => { released = true; });
    visuals.update(11, true);
    expect(scene.children).toHaveLength(0);
    expect(released).toBe(true);
  });
});
