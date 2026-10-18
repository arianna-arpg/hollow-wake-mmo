import { presentation, type WispPart } from './content';
/** Mu's original depth well, nebulae and mote strata, isolated from world rendering. */
export function hash01(x: number, y: number, seed = 0): number {
  let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(seed, 69068069)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}

function withAlpha(color: string, alpha: number): string {
  return color.slice(0, 7) + Math.round(Math.max(0,Math.min(1,alpha))*255).toString(16).padStart(2,'0');
}
export function abyss(ctx: CanvasRenderingContext2D, w: number, h: number,
  t: number, k: number, color: string, camX: number, camY: number): void {
  ctx.save();
  const wrap = (v: number, span: number): number => ((v % span) + span) % span;
  // The depth well: one radial breath — heart lifted a touch below center,
  // rim sinking to true dark. Screen-space by construction (it follows the
  // camera; the abyss is wherever you look past).
  const cx = w / 2, cy = h * 0.56;
  const breathe = 1 + 0.06 * Math.sin(t * 0.35);
  const well = ctx.createRadialGradient(
    cx, cy, Math.min(w, h) * 0.1, cx, cy, Math.hypot(w, h) * 0.62);
  well.addColorStop(0, withAlpha(color, 0.05 * k * breathe));
  well.addColorStop(0.45, 'rgba(0,0,0,0)');
  well.addColorStop(1, `rgba(2,2,6,${(0.55 * k).toFixed(3)})`);
  ctx.fillStyle = well;
  ctx.fillRect(0, 0, w, h);
  // The nebular lobes — the deepest stratum (parallax 0.12): vast, slow,
  // barely there; the hint that the nothing has weather of its own.
  for (let i = 0; i < 4; i++) {
    const r = 180 + hash01(i, 41) * 240;
    const sx = w + r * 2, sy = h + r * 2;
    const x = wrap(hash01(i, 43) * sx + t * (1.5 + hash01(i, 45) * 2) - camX * 0.12, sx) - r;
    const y = wrap(hash01(i, 47) * sy + Math.sin(t * 0.05 + i * 2.1) * 30 - camY * 0.12, sy) - r;
    const lobe = ctx.createRadialGradient(x, y, 0, x, y, r);
    lobe.addColorStop(0, withAlpha(color, 0.035 * k));
    lobe.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = lobe;
    ctx.fillRect(x - r, y - r, r * 2, r * 2);
  }
  // Three mote strata: deeper = slower against the camera = further into
  // the back. Each mote wanders on its own sway and twinkles on its own
  // clock — the ledger's still pane, breathing.
  ctx.fillStyle = color;
  const strata = [
    { n: 26, par: 0.22, size: 0.9, a: 0.16, salt: 3 },
    { n: 18, par: 0.45, size: 1.5, a: 0.24, salt: 101 },
    { n: 10, par: 0.78, size: 2.2, a: 0.34, salt: 211 },
  ];
  for (const s of strata) {
    const n = Math.round(s.n * k);
    for (let i = 0; i < n; i++) {
      const x = wrap(hash01(i, s.salt) * (w + 40)
        + t * (2 + hash01(i, s.salt + 2) * 5) - camX * s.par, w + 40) - 20;
      const y = wrap(hash01(i, s.salt + 4) * (h + 40)
        + t * (1 + hash01(i, s.salt + 6) * 3)
        + Math.sin(t * (0.3 + hash01(i, s.salt + 8) * 0.4) + i) * 9
        - camY * s.par, h + 40) - 20;
      const tw = 0.6 + 0.4 * Math.sin(t * (0.4 + hash01(i, s.salt + 10) * 0.8) + i * 1.7);
      ctx.globalAlpha = s.a * k * tw;
      ctx.beginPath();
      ctx.arc(x, y, s.size * (0.8 + hash01(i, s.salt + 12) * 0.5), 0, Math.PI * 2);
      ctx.fill();
    }
  }
  ctx.restore();
  ctx.globalAlpha = 1;
}



/** The original spirit look, composed from the shared part recipe. */
type Painter = (ctx: CanvasRenderingContext2D, radius: number, part: WispPart, time: number, pal: Record<string,string>) => void;
const partPainters: Record<string, Painter> = {
  disc(c,R,_p,_t,pal) {
    const trace=()=>{ c.beginPath();c.arc(0,0,R,0,Math.PI*2); };
    trace();c.fillStyle=pal.base;c.fill();c.save();trace();c.clip();
    for(const [offset,extent,tone] of [[-.38,1.4,pal.light],[.45,1.5,pal.shadow]] as const){
      const g=c.createRadialGradient(R*offset,R*offset,R*.08,R*offset,R*offset,R*extent);
      g.addColorStop(0,withAlpha(tone,.5));g.addColorStop(1,withAlpha(tone,0));
      c.fillStyle=g;c.fillRect(-R*2,-R*2,R*4,R*4);
    }
    c.restore();trace();c.strokeStyle=withAlpha(pal.outline,.75);c.lineWidth=1.6;c.stroke();
  },
  halo(c,R,_p,_t,pal) {
    for(const [alpha,width] of [[.28,Math.max(3,R*.2)],[.75,Math.max(1.2,R*.06)]]){
      c.strokeStyle=withAlpha(pal.glow,alpha);c.lineWidth=width;
      c.beginPath();c.arc(0,0,R*.8,0,Math.PI*2);c.stroke();
    }
  },
  eyes(c,R,p,_t,pal) {
    const {n=2,spread=.5,dist=.72,size=.1}=p.params??{};
    for(let i=0;i<n;i++){
      const a=n===1?0:(i/(n-1)-.5)*spread*2,x=Math.cos(a)*R*dist,y=Math.sin(a)*R*dist;
      const g=c.createRadialGradient(x,y,0,x,y,R*size*2.6);
      g.addColorStop(0,withAlpha(pal.glow,.65));g.addColorStop(1,withAlpha(pal.glow,0));
      c.fillStyle=g;c.fillRect(x-R*size*3,y-R*size*3,R*size*6,R*size*6);
      c.fillStyle=pal.glow;c.beginPath();c.arc(x,y,R*size,0,Math.PI*2);c.fill();
      c.fillStyle='#ffffff';c.beginPath();c.arc(x,y,R*size*.38,0,Math.PI*2);c.fill();
    }
  },
  wisps(c,R,p,t,pal) {
    const n=p.params?.n??3;c.lineCap='round';
    for(let i=0;i<n;i++){
      const off=(i/Math.max(1,n-1)-.5)*R*.9,sway=Math.sin(t*2.2+i*2.1)*R*.28,len=R*(1+.25*Math.sin(t*1.7+i));
      c.strokeStyle=withAlpha(pal.glow,.4-i*.06);c.lineWidth=Math.max(1.5,R*(.16-i*.03));
      c.beginPath();c.moveTo(-R*.3,off);c.quadraticCurveTo(-R*.75,off+sway*.5,-R*.3-len,off+sway);c.stroke();
    }
  },
};
export function drawWisp(ctx: CanvasRenderingContext2D, time: number, look = presentation.wisp): void {
  const r=look.radius;
  for(const part of look.parts){
    ctx.save();ctx.translate((part.x??0)*r,(part.y??0)*r);
    partPainters[part.kind]?.(ctx,r*(part.scale??1),part,time,look.palette);ctx.restore();
  }
}
