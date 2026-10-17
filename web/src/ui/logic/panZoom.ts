/** Pan and zoom of a canvas such as the passive constellation: the desktop client's UI/Logic/PanZoom.cs. */
export class PanZoom {
  zoom = 1;
  offsetX = 0;
  offsetY = 0;

  constructor(
    readonly scale: number,
    readonly minZoom: number,
    readonly maxZoom: number,
  ) {}

  /** Content y grows north, screen y grows down. */
  toScreen(x: number, y: number): [number, number] {
    return [x * this.scale * this.zoom + this.offsetX, -y * this.scale * this.zoom + this.offsetY];
  }

  toContent(sx: number, sy: number): [number, number] {
    return [(sx - this.offsetX) / (this.scale * this.zoom), -(sy - this.offsetY) / (this.scale * this.zoom)];
  }

  pan(dx: number, dy: number): void {
    this.offsetX += dx;
    this.offsetY += dy;
  }

  zoomAt(sx: number, sy: number, factor: number): void {
    const [cx, cy] = this.toContent(sx, sy);
    this.zoom = Math.min(this.maxZoom, Math.max(this.minZoom, this.zoom * factor));
    const [nx, ny] = this.toScreen(cx, cy);
    this.offsetX += sx - nx;
    this.offsetY += sy - ny;
  }

  fit(minX: number, minY: number, maxX: number, maxY: number, width: number, height: number, margin = 24): void {
    const w = Math.max(1e-3, maxX - minX) * this.scale;
    const h = Math.max(1e-3, maxY - minY) * this.scale;
    this.zoom = Math.min(this.maxZoom, Math.max(this.minZoom, Math.min((width - margin * 2) / w, (height - margin * 2) / h)));
    const cx = (minX + maxX) / 2;
    const cy = (minY + maxY) / 2;
    this.offsetX = width / 2 - cx * this.scale * this.zoom;
    this.offsetY = height / 2 + cy * this.scale * this.zoom;
  }
}
