import { BufferGeometry, Float32BufferAttribute, MeshBasicMaterial, Mesh, Scene, Vector3, DoubleSide } from 'three';
import type { CastCueMsg, Int64 } from '../net/protocol';
import type { EntityLayer } from './entities';
import { toThree } from './coords';
import { linearColor } from './colors';
import { CastCueTimeline, type CueInstance, type CueSettings } from './castCueTimeline';
import { castPrimitive } from './castVisualPrimitives';

/** Renderer adapter for content-authored, depth-tested cosmetic lines; never draws a gameplay hit region. */
export class CastVisuals {
  readonly timeline: CastCueTimeline;
  private readonly drawn = new Map<CueInstance, Mesh<BufferGeometry, MeshBasicMaterial>>();
  constructor(private readonly scene: Scene, private readonly entities: EntityLayer, config: CueSettings) {
    this.timeline = new CastCueTimeline(config);
    this.timeline.onLifecycle = event => this.entities.views.get(event.source)?.observeCastCue(event);
  }
  receive(event: CastCueMsg, now: number): void { this.timeline.receive(event, now); }
  removeEntity(key: Int64): void { this.timeline.removeEntity(key); }
  clear(): void { this.timeline.clear(); this.clean(); }
  private clean(): void {
    if (this.drawn.size === 0) return;
    const active = new Set(this.timeline.active);
    for (const [cue, mesh] of this.drawn) if (!active.has(cue)) {
      this.scene.remove(mesh); mesh.geometry.dispose(); mesh.material.dispose(); this.drawn.delete(cue);
    }
  }
  update(now: number, reduced: boolean): void {
    this.timeline.advance(now);
    this.clean();
    for (const cue of this.timeline.active) {
      const e = cue.event, style = cue.style;
      const anchor = this.entities.views.get(cue.phase === 'impact' || e.outcome ? e.target : e.source);
      if (anchor === undefined || (anchor.state.dead && cue.phase !== 'impact' && !style.show_on_dead)) continue;
      let mesh = this.drawn.get(cue);
      if (mesh === undefined) {
        const vertices: number[] = [];
        for (const s of castPrimitive(style)) {
          const a = toThree(s[0], s[1], s[2]), b = toThree(s[3], s[4], s[5]);
          const direction = b.clone().sub(a).normalize();
          if (direction.lengthSq() === 0) continue;
          const axis = Math.abs(direction.y) < 0.9 ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
          const side = direction.clone().cross(axis).normalize().multiplyScalar(style.stroke / 2);
          const other = direction.clone().cross(side).normalize().multiplyScalar(style.stroke / 2);
          // Crossed ribbons retain thickness in both cameras without one draw call per stroke.
          for (const offset of [side, other]) {
            const corners = [a.clone().sub(offset), a.clone().add(offset), b.clone().add(offset), b.clone().sub(offset)];
            for (const i of [0, 1, 2, 0, 2, 3]) vertices.push(...corners[i].toArray());
          }
        }
        const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(vertices, 3));
        mesh = new Mesh(geometry, new MeshBasicMaterial({ color: linearColor(cue.color), transparent: true, depthTest: true, depthWrite: false, toneMapped: false, side: DoubleSide }));
        mesh.frustumCulled = false;
        this.scene.add(mesh); this.drawn.set(cue, mesh);
      }
      const age = Math.max(0, now - e.server_time);
      const progress = cue.phase === 'start' ? 0 : Math.min(1, age / style.seconds);
      // Contact stays at its confirmed location; preparation and the cosmetic tail follow the body.
      mesh.position.copy(cue.phase === 'impact' ? toThree(e.x, e.y, e.z) : toThree(anchor.position.x, anchor.position.y, anchor.position.z));
      mesh.position.y += anchor.state.height * style.height;
      const scale = style.size * (reduced ? 1 : 1 + style.expand * progress);
      mesh.scale.setScalar(scale);
      const yaw = e.aim_x === e.x && e.aim_y === e.y ? anchor.state.yaw : Math.atan2(e.aim_y - e.y, e.aim_x - e.x);
      mesh.rotation.y = yaw + (reduced ? 0 : age * style.spin);
      mesh.material.opacity = style.opacity * (reduced || cue.phase === 'start' ? 1 : 1 - progress);
      mesh.visible = true;
    }
    for (const [cue, mesh] of this.drawn) {
      const anchor = this.entities.views.get(cue.phase === 'impact' || cue.event.outcome ? cue.event.target : cue.event.source);
      if (anchor === undefined || (anchor.state.dead && cue.phase !== 'impact' && !cue.style.show_on_dead)) mesh.visible = false;
    }
  }
}
