export interface EntrySceneSpec {
  activity: string; radius: number; seatGap: number; rankGap: number; arcFrom: number; arcTo: number;
  dwellRadius: number; bodyRadius: number; dwellSeconds: number; speed: number; turnRate: number;
  wrapRadius: number; wrapReentry: number; wrapClear: number; units: number; cameraSpan: number;
  bodyHeight: number; bodyWidth: number; wispRadius: number; maxFrame: number; wispSource: string; cameraHeight: number; cameraOffset: number; cameraLead: number; apparitionOpacity: number;
  palette: Record<string, string>; labels: Record<string, string>;
}
export interface VesselChoice { id: string; calling: string; name: string; description: string; look: string; character: string | null; available: boolean; rank: number }
export interface VesselSeat { choice: VesselChoice; x: number; y: number; yaw: number }
export function selectionRing(n: number, radius: number, gap: number, from: number, to: number): { radius: number; angles: number[]; closed: boolean } {
  const span = to - from, center = from + span / 2;
  if (n <= 0) return { radius, angles: [], closed: false };
  if (n === 1) return { radius, angles: [center], closed: false };
  const angles = (start: number, width: number) => Array.from({ length: n }, (_, i) => start + width * i / (n - 1));
  if (2 * radius * Math.sin(span / (2 * (n - 1))) >= gap - 1e-9) return { radius, angles: angles(from, span), closed: false };
  const need = 2 * (n - 1) * Math.asin(Math.min(1, gap / (2 * radius))), full = 2 * Math.PI * (n - 1) / n;
  if (need <= full) return { radius, angles: angles(center - need / 2, need), closed: false };
  return { radius: Math.max(radius, gap / (2 * Math.sin(Math.PI / n))), angles: angles(center - full / 2, full), closed: true };
}

/** Twin of UI/Logic/SpatialSelection.cs. Pure presentation, never a gameplay world. */
export class SpatialSelection {
  seats: VesselSeat[] = []; x = 0; y = 0; progress = 0; engaged = -1; opened = -1; rim = 0;
  private latched = -1; private target: { x: number; y: number } | null = null;
  constructor(readonly spec: EntrySceneSpec) {}
  setChoices(choices: VesselChoice[]): void {
    this.seats = []; this.x = this.y = this.progress = 0; this.engaged = this.opened = this.latched = -1; this.target = null;
    let radius = this.spec.radius, outer = 0;
    for (const rank of [...new Set(choices.map(c => c.rank))].sort((a, b) => a - b)) {
      const list = choices.filter(c => c.rank === rank), ring = selectionRing(list.length, radius, this.spec.seatGap, this.spec.arcFrom, this.spec.arcTo);
      list.forEach((choice, i) => { const x = Math.cos(ring.angles[i]!) * ring.radius, y = Math.sin(ring.angles[i]!) * ring.radius; this.seats.push({ choice, x, y, yaw: Math.atan2(-y, -x) }); });
      outer = ring.radius; radius = ring.radius + this.spec.rankGap;
    }
    this.rim = Math.max(this.spec.wrapRadius, outer + this.spec.wrapClear);
  }
  moveTo(x: number, y: number): void { this.target = { x, y }; }
  open(index: number): void { if (index >= 0 && index < this.seats.length) { this.opened = this.latched = index; this.progress = 0; this.target = null; } }
  close(): void { this.opened = -1; this.progress = 0; this.target = null; }
  suspend(): void { this.target = null; this.progress = 0; }
  step(dt: number, dx: number, dy: number, suspended = false): void {
    if (suspended || this.opened >= 0) { this.suspend(); return; }
    dt = Math.max(0, Math.min(dt, this.spec.maxFrame));
    let length = Math.hypot(dx, dy);
    if (length > 0) this.target = null;
    if (length === 0 && this.target) {
      dx = this.target.x - this.x; dy = this.target.y - this.y; length = Math.hypot(dx, dy);
      if (length <= this.spec.speed * dt) { this.x = this.target.x; this.y = this.target.y; this.target = null; dx = dy = length = 0; }
    }
    if (length > 0) { this.x += dx / length * this.spec.speed * dt; this.y += dy / length * this.spec.speed * dt; }
    const distance = Math.hypot(this.x, this.y);
    if (distance > this.rim) { const back = this.rim - (this.spec.wrapRadius - this.spec.wrapReentry); this.x *= -back / distance; this.y *= -back / distance; this.target = null; }
    let nearest = -1, best = this.spec.dwellRadius + this.spec.bodyRadius;
    this.seats.forEach((s, i) => {
      const vx = this.x - s.x, vy = this.y - s.y, d = Math.hypot(vx, vy), desired = Math.atan2(vy, vx), delta = Math.atan2(Math.sin(desired - s.yaw), Math.cos(desired - s.yaw));
      s.yaw += Math.max(-this.spec.turnRate * dt, Math.min(delta, this.spec.turnRate * dt));
      if (d < best) { best = d; nearest = i; }
    });
    if (this.latched >= 0 && nearest !== this.latched) this.latched = -1;
    if (nearest !== this.engaged || length > 0) this.progress = 0;
    this.engaged = nearest;
    if (nearest < 0 || length > 0 || nearest === this.latched || !this.seats[nearest]!.choice.available) return;
    this.progress += dt / this.spec.dwellSeconds;
    if (this.progress >= 1) this.open(nearest);
  }
}
