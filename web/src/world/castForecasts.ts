import { BufferGeometry, Float32BufferAttribute, MeshBasicMaterial, Mesh, Scene, DoubleSide, Camera } from 'three';
import type { CastForecastInfo, CastForecastRegion, Int64 } from '../net/protocol';
import type { EntityLayer } from './entities';
import { toThree } from './coords';
import { linearColor } from './colors';
import { CastForecastTimeline, forecastCaption, forecastProgress, forecastSegments } from './castForecastTimeline';
import { forecastGeometry, sameForecastMesh } from './castForecastGeometry';
import type { ForecastGeometryStyle } from './castForecastGeometry';

export interface ForecastStyle extends ForecastGeometryStyle {
  stroke: number; underlay_scale: number; lift: number; bar_width: number; bar_height: number;
  label_height: number; font_size: number; color: string; candidate_color: string; unknown_color: string;
  underlay_color: string; labels: Record<string, string>;
}
export const forecastStyle = (raw: unknown): ForecastStyle => {
  const defaults: ForecastStyle = { segments: 64, max_segments: 256, max_chord_error: .025, stroke: .045, dash_fraction: .7, underlay_scale: 2.4, direction_size: .45, lift: .08, bar_width: 98, bar_height: 4,
    label_height: .5, height_marker_size: .22, font_size: 12, color: '#f5db97', candidate_color: '#fff5d7', unknown_color: '#bdc7dc', underlay_color: '#182231',
    labels: { primary: '', unavailable: 'Area unknown', ready: 'Ready · held', channel: 'Channeling', waiting: 'Awaiting resolution', stale: 'Forecast delayed', overflow: 'Additional casts nearby' } };
  const r = raw && typeof raw === 'object' ? raw as Partial<ForecastStyle> : {};
  return { ...defaults, ...r, labels: { ...defaults.labels, ...r.labels } };
};

/** A sampled candidate envelope, not a damage promise. Ground boundaries use the authoritative frame;
 * vertical brackets show the actual center-height tolerance instead of pretending this is floor clipping. */
export class CastForecasts {
  readonly timeline = new CastForecastTimeline();
  private drawn = new Map<Int64, { region?: CastForecastRegion; committed: boolean; meshes: Mesh<BufferGeometry, MeshBasicMaterial>[]; label: HTMLDivElement; bar: HTMLDivElement; text: HTMLDivElement }>();
  private readonly warning = document.createElement('div');
  constructor(private scene: Scene, private entities: EntityLayer, readonly style: ForecastStyle) {
    this.warning.style.cssText = 'position:fixed;left:50%;top:18%;transform:translateX(-50%);color:white;background:#182231dd;padding:6px;pointer-events:none;z-index:12;display:none';
    document.body.append(this.warning);
  }
  clear(epoch?: string): void { this.timeline.reset(epoch); this.clean(); this.warning.style.display = 'none'; }
  private clean(): void {
    for (const [id, v] of this.drawn) if (!this.timeline.active.has(id)) {
      for (const m of v.meshes) { this.scene.remove(m); m.geometry.dispose(); m.material.dispose(); }
      v.label.remove(); this.drawn.delete(id);
    }
  }
  update(now: number, camera: Camera): void {
    this.clean();
    const stale = this.timeline.stale(now), s = this.style;
    this.warning.textContent = this.timeline.omitted > 0 ? `${s.labels.overflow} (${this.timeline.omitted})` : '';
    this.warning.style.display = this.timeline.omitted > 0 ? '' : 'none';
    for (const [id, c] of this.timeline.active) {
      const body = this.entities.views.get(c.source);
      if (!body) continue;
      let v = this.drawn.get(id);
      if (!v) {
        const label = document.createElement('div'), text = document.createElement('div'), bar = document.createElement('div');
        label.style.cssText = `position:fixed;pointer-events:none;transform:translate(-50%,-100%);text-align:center;color:${s.color};font:${s.font_size}px sans-serif;text-shadow:0 1px 3px black;z-index:11;background:${s.underlay_color}b0;padding:3px 5px;border-radius:3px`;
        const track = document.createElement('div'); track.style.cssText = `height:${s.bar_height}px;background:${s.underlay_color};width:${s.bar_width}px;margin:3px auto 0`;
        bar.style.cssText = `height:100%;background:${s.color};transform-origin:left`; track.append(bar); label.append(text, track); document.body.append(label);
        v = { committed: false, meshes: [], label, text, bar }; this.drawn.set(id, v);
      }
      const region = !stale && forecastSegments(c.region, s.segments, s.max_segments, s.max_chord_error) > 0 ? c.region : undefined;
      if (!sameForecastMesh(v.region, v.committed, region, c.direction_committed)) {
        for (const m of v.meshes) { this.scene.remove(m); m.geometry.dispose(); m.material.dispose(); }
        v.meshes = region ? this.boundary(c) : []; v.region = region; v.committed = c.direction_committed;
      }
      let caption = forecastCaption(c, now, stale);
      if (caption === 'primary' && region === undefined) caption = 'unavailable';
      const coverage = ['primary', 'unavailable', 'stale'].includes(caption) ? '' : s.labels[region !== undefined ? 'primary' : 'unavailable'];
      v.text.textContent = [c.name, s.labels[caption], coverage].filter(Boolean).join(' · ');
      v.label.style.color = region !== undefined ? s.color : s.unknown_color;
      v.bar.style.transform = `scaleX(${stale ? 0 : forecastProgress(c, now)})`;
      const p = toThree(c.x, c.y, c.z + body.state.height + s.label_height).project(camera);
      v.label.style.display = body.state.dead || p.z < -1 || p.z > 1 ? 'none' : '';
      v.label.style.left = `${(p.x + 1) / 2 * window.innerWidth}px`;
      v.label.style.top = `${(1 - p.y) / 2 * window.innerHeight}px`;
      for (const m of v.meshes) {
        m.visible = !body.state.dead;
        if (region) { m.position.copy(toThree(region.x, region.y, region.z + s.lift)); m.rotation.y = region.heading; }
      }
    }
  }
  private boundary(c: CastForecastInfo): Mesh<BufferGeometry, MeshBasicMaterial>[] {
    const r = c.region!, s = this.style, parts = forecastGeometry(r, c.direction_committed, s);
    const color = r.viewer_eligibility === 'candidate' ? s.candidate_color : s.color;
    return [this.lines(parts, r, s.stroke * s.underlay_scale, s.underlay_color, 10), this.lines(parts, r, s.stroke, color, 11)];
  }
  private lines(parts: number[][], r: NonNullable<CastForecastInfo['region']>, width: number, color: string, order: number): Mesh<BufferGeometry, MeshBasicMaterial> {
    const vertices: number[] = [];
    for (const p of parts) {
      const a = toThree(p[0], p[1], p[2]), b = toThree(p[3], p[4], p[5]);
      const d = b.clone().sub(a); if (d.lengthSq() === 0) continue;
      const offset = d.clone().cross(Math.abs(d.y) > .99 * d.length() ? toThree(1, 0, 0) : toThree(0, 0, 1)).normalize().multiplyScalar(width / 2);
      const corners = [a.clone().sub(offset), a.clone().add(offset), b.clone().add(offset), b.clone().sub(offset)];
      for (const i of [0, 1, 2, 0, 2, 3]) vertices.push(...corners[i].toArray());
    }
    const mesh = new Mesh(new BufferGeometry().setAttribute('position', new Float32BufferAttribute(vertices, 3)),
      new MeshBasicMaterial({ color: linearColor(color), depthTest: false, depthWrite: false, side: DoubleSide, toneMapped: false }));
    mesh.position.copy(toThree(r.x, r.y, r.z + this.style.lift)); mesh.rotation.y = r.heading; mesh.renderOrder = order;
    mesh.frustumCulled = false; this.scene.add(mesh); return mesh;
  }
}
