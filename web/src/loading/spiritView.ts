import { presentation } from './content';
import { abyss, hash01, drawWisp } from './spiritArt';
import { SPIRIT_RUN, SPIRIT_CURRENT_COLOR, SpiritRun, spiritLayout, spiritGateSolids } from './spiritRun';
// One Mu soul-flame, from a quiet sputter to a brighter, restless flare.
// These are visual envelopes only; every encounter keeps its existing hit radius.
const SOUL_FLAMES = presentation.pickups;

/** The Vault/vestige alphabet, baked once per glyph in the bounded sprite cache. */
const runeCache = new Map<string, HTMLCanvasElement>();
function gateRune(rune: string): HTMLCanvasElement {
  let sprite = runeCache.get(rune);
  if (sprite) return sprite;
  sprite = document.createElement('canvas'); sprite.width = sprite.height = 48;
  const ctx = sprite.getContext('2d')!;
  ctx.font = '30px "Segoe UI Symbol", "Noto Sans Runic", serif';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillStyle = '#c3dce8';
  ctx.fillText(rune, 24, 25); runeCache.set(rune, sprite); return sprite;
}

/** Mu crossing painter; both clients share its look recipe and collision model. */
export function drawSpiritRun(ctx: CanvasRenderingContext2D, width: number, height: number,
  run: SpiritRun, reducedMotion = false): void {
  ctx.clearRect(0, 0, width, height);
  const c = SPIRIT_RUN, layout = spiritLayout(width, height, run.direction), point = layout.point, scale = layout.scale;
  const time = reducedMotion ? 0 : run.time;
  const line = (u: number, v: number, u2: number, v2: number): void => {
    const a = point(u, v), b = point(u2, v2); ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
  };
  const path = (coords: readonly (readonly [number, number])[]): void => {
    ctx.beginPath(); coords.forEach(([u, v], i) => { const p = point(u, v); if (i) ctx.lineTo(p.x, p.y); else ctx.moveTo(p.x, p.y); });
  };
  const layers: Record<string, () => void> = {
  abyss: () => {
  const focus = point(650, 0), mist = ctx.createRadialGradient(focus.x, focus.y, 10, width / 2, height / 2, Math.max(width, height) * 0.7);
  mist.addColorStop(0, '#233442'); mist.addColorStop(0.45, '#131f2e'); mist.addColorStop(1, '#070b12');
  const mu = presentation.palette;
  ctx.fillStyle = mu.floor; ctx.fillRect(0, 0, width, height);
  ctx.globalAlpha = 0.65; ctx.fillStyle = mist; ctx.fillRect(0, 0, width, height); ctx.globalAlpha = 1;
  // The actual Mu depth well, nebular haze and three parallax mote strata, also
  // echoed by the website's abyss. Keep the crossing's ribbons and pillars above it.
  const travel = reducedMotion ? 0 : run.distance * scale * 0.18;
  const camX = run.direction === 'down' ? 0 : run.direction === 'left' ? -travel : travel;
  const camY = run.direction === 'down' ? travel : 0;
  abyss(ctx, width, height, time, 1, mu.abyss, camX, camY);
  },
  ribbons: () => {
  // Broad, quiet ribbons and far pillars give depth without resembling hazards.
  for (let i = 0; i < 5; i++) {
    ctx.strokeStyle = i % 2 ? '#78a39d' : '#8474bb'; ctx.globalAlpha = 0.035;
    ctx.lineWidth = (18 + i * 7) * scale; ctx.beginPath();
    for (let u = -40; u <= 1040; u += 24) {
      const p = point(u, Math.sin(u * 0.004 + i * 2 + time * 0.1) * 140 + (i - 2) * 54);
      if (u === -40) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y);
    }
    ctx.stroke();
  }
  ctx.strokeStyle = '#7baba1'; ctx.lineWidth = 1;
  for (const side of [-1, 1]) {
    ctx.globalAlpha = 0.17; line(0, side * 275, 1000, side * 275);
    for (let i = 0; i < 9; i++) {
      const u = ((i * 142 - (reducedMotion ? 0 : run.distance) * 0.26) % 1280 + 1280) % 1280 - 100;
      ctx.globalAlpha = 0.11;
      path([[u - 10, side * 280], [u + 16, side * 242], [u + 38, side * 236], [u + 61, side * 280]]);
      ctx.closePath(); ctx.fillStyle = '#354652'; ctx.fill(); ctx.stroke();
      ctx.globalAlpha = 0.19; line(u + 23, side * 251, u + 31, side * 251);
    }
  }
  for (let i = 0; i < 88; i++) {
    const u = ((i * 173.7 - (reducedMotion ? 0 : run.distance) * (0.18 + (i % 4) * 0.09)) % 1120 + 1120) % 1120 - 60;
    const v = Math.sin(i * 71.2) * 260;
    ctx.globalAlpha = 0.1 + (i % 5) * 0.038; ctx.strokeStyle = i % 3 ? '#97d8cb' : '#d5bb87';
    line(u, v, u + (reducedMotion ? 2 : 4 + run.speed * 4), v);
  }
  },
  gates: () => {
  for (const gate of run.gates) {
    ctx.save(); ctx.globalAlpha = gate.resolved ? (gate.hit ? 0.35 : 0.17) : 0.92;
    const narrow = gate.openings.some(o => o.width < 154), color = gate.hit ? presentation.palette.impact : narrow ? presentation.palette.narrowGate : presentation.palette.gate;
    for (const { low, high } of spiritGateSolids(gate)) {
      const half = c.gateThickness / 2, a = point(gate.u - half, low), b = point(gate.u + half, high);
      const start = point(gate.u, low), end = point(gate.u, high);
      const stone = ctx.createLinearGradient(start.x, start.y, end.x, end.y);
      stone.addColorStop(0, gate.hit ? '#674448' : '#456174'); stone.addColorStop(0.5, '#1d2e3e'); stone.addColorStop(1, gate.hit ? '#674448' : '#456174');
      ctx.fillStyle = stone; ctx.strokeStyle = color; ctx.lineWidth = Math.max(1, scale);
      ctx.fillRect(Math.min(a.x, b.x), Math.min(a.y, b.y), Math.abs(a.x - b.x), Math.abs(a.y - b.y));
      ctx.strokeRect(Math.min(a.x, b.x), Math.min(a.y, b.y), Math.abs(a.x - b.x), Math.abs(a.y - b.y));
      for (const [edge, side] of [[low, 1], [high, -1]]) if (Math.abs(edge) < c.halfWidth) {
        ctx.save(); ctx.shadowColor = color; ctx.shadowBlur = reducedMotion ? 0 : 13 * scale;
        ctx.lineWidth = 3 * scale; line(gate.u - half, edge, gate.u + half, edge); ctx.restore();
        const size = Math.min(14, (high - low) * 0.26); ctx.fillStyle = color;
        path([[gate.u, edge + side * 3], [gate.u + 5, edge + side * size * 0.65],
          [gate.u, edge + side * size], [gate.u - 5, edge + side * size * 0.65]]);
        ctx.closePath(); ctx.fill();
      }
      const span = high - low, count = span < 38 ? 0 : Math.max(1, Math.floor((span - 44) / 27) + 1);
      for (let j = 0; j < count; j++) {
        const v = count === 1 ? (low + high) / 2 : low + 22 + (span - 44) * j / (count - 1), at = point(gate.u, v);
        // Geometry supplies per-crossing variety without consuming either RNG.
        // Never include time or moving u: a gate keeps its inscription as it passes.
        const index = Math.floor(hash01(gate.id * 31 + j, Math.round(low * 100)) * presentation.runes.length);
        const glyph = gateRune(presentation.runes[index]), side = 24 * scale;
        ctx.drawImage(glyph, at.x - side / 2, at.y - side / 2, side, side);
      }
    }
    ctx.restore();
  }
  },
  currents: () => {
  // Ephemeral currents use close chevrons, all facing the actual travel direction.
  for (const current of run.currents) {
    if (current.taken) continue;
    const p = point(current.u, current.lane);
    ctx.save(); ctx.globalAlpha = 1; ctx.translate(p.x, p.y); ctx.scale(scale, scale);
    ctx.rotate(run.direction === 'down' ? Math.PI / 2 : run.direction === 'left' ? Math.PI : 0);
    const glow = ctx.createRadialGradient(0, 0, 2, 0, 0, 54);
    glow.addColorStop(0, '#a1ffdf35'); glow.addColorStop(1, '#a1ffdf00');
    ctx.fillStyle = glow; ctx.fillRect(-56, -56, 112, 112);
    ctx.strokeStyle = SPIRIT_CURRENT_COLOR; ctx.lineCap = 'round';
    for (let i = 0; i < 5; i++) {
      const x = (i - 2) * 11;
      ctx.globalAlpha = reducedMotion ? 0.8 : 0.52 + 0.34 * (0.5 + 0.5 * Math.sin(time * 6 - i));
      ctx.lineWidth = 2.4; ctx.beginPath(); ctx.moveTo(x - 5, -11);
      ctx.quadraticCurveTo(x + 1, -7, x + 6, 0); ctx.quadraticCurveTo(x + 1, 7, x - 5, 11); ctx.stroke();
    }
    ctx.lineWidth = 0.8;
    for (const side of [-1, 1]) {
      ctx.globalAlpha = 0.26; ctx.beginPath(); ctx.moveTo(-55, side * 18);
      ctx.bezierCurveTo(-28, side * 8, 10, side * 24, 40, side * 12); ctx.stroke();
    }
    ctx.restore();
  }

  },
  souls: () => {
  // Staggered spirits drift independently; a chosen companion releases its sibling.
  for (const pickup of run.pickups) {
    const def = SOUL_FLAMES[pickup.kind], p = point(pickup.u, pickup.lane), color = presentation.palette.wisp;
    if (pickup.state === 'taken') continue;
    ctx.save(); ctx.globalAlpha = pickup.state === 'released' ? 0.45 * (1 - pickup.fade / c.pickupFade) : 1;
    ctx.translate(p.x, p.y); ctx.scale(scale, scale);
    const phase = time * def.flicker + pickup.id * 2.4;
    const breath = 1 + Math.sin(phase) * 0.045 + Math.sin(phase * 1.7) * 0.025;
    const radius = def.radiance * breath;
    const glow = ctx.createRadialGradient(0, -4, 2, 0, -4, radius);
    glow.addColorStop(0, color + '80'); glow.addColorStop(0.35, color + '2b'); glow.addColorStop(1, color + '00');
    ctx.fillStyle = glow; ctx.fillRect(-radius, -radius - 4, radius * 2, radius * 2);
    // Flames rise in screen space in all three directions. Their bright heart
    // remains at the encounter center; the slender tips gutter and curl above it.
    ctx.scale(def.size, def.size * breath);
    const curl = Math.sin(phase * 0.8) * 2.4, lick = Math.sin(phase * 1.3) * 1.8;
    const flame = ctx.createLinearGradient(0, -26, 0, 10);
    flame.addColorStop(0, color + '18'); flame.addColorStop(0.45, color + 'b8'); flame.addColorStop(1, color + 'ef');
    ctx.fillStyle = flame; ctx.beginPath(); ctx.moveTo(0, 10);
    ctx.bezierCurveTo(-12, 9, -12, -1, -7, -10);
    ctx.quadraticCurveTo(-8, -3, -3, -4);
    ctx.bezierCurveTo(1, -8, -5 + curl, -17, 2 + curl, -26 - lick);
    ctx.bezierCurveTo(-1 + curl, -14, 12, -10, 9, -1);
    ctx.quadraticCurveTo(13, -3, 12, -8);
    ctx.bezierCurveTo(17, 5, 7, 11, 0, 10); ctx.fill();
    ctx.fillStyle = '#f0f7fa'; ctx.beginPath(); ctx.moveTo(0, 7);
    ctx.bezierCurveTo(-7, 5, -6, -2, -1 + curl * 0.25, -11);
    ctx.bezierCurveTo(-2, -3, 7, -1, 5, 4); ctx.quadraticCurveTo(3, 8, 0, 7); ctx.fill();
    ctx.fillStyle = '#ffffff'; ctx.beginPath(); ctx.ellipse(0, 2, 2.1, 3.2, 0, 0, Math.PI * 2); ctx.fill();
    // Faint threads lift off the same flame; no badges, gems or ranked colors.
    ctx.strokeStyle = color + '65'; ctx.lineWidth = 0.7;
    for (let i = 0; i < 2; i++) {
      const drift = (time * (0.4 + def.size * 0.15) + hash01(pickup.id, i + 51)) % 1;
      const x = (i ? 1 : -1) * (6 + Math.sin(phase + i) * 2), y = -9 - drift * 21;
      ctx.save(); ctx.globalAlpha *= (1 - drift) * 0.45;
      ctx.beginPath(); ctx.moveTo(x, y); ctx.quadraticCurveTo(x + curl, y - 4, x + curl * 0.6, y - 8); ctx.stroke(); ctx.restore();
    }
    ctx.restore();
  }
  },
  bursts: () => {
  for (const burst of run.bursts) {
    const f = burst.age / c.burstSeconds, p = point(burst.u, burst.lane);
    const color = burst.kind === 'impact' ? '#efa2a0' : burst.kind === 'gate' ? '#a2d9ce' : burst.kind === 'current' ? SPIRIT_CURRENT_COLOR : presentation.palette.wisp;
    ctx.save(); ctx.globalAlpha = 1 - f; ctx.strokeStyle = color; ctx.fillStyle = color; ctx.lineWidth = 1.3 * scale;
    if (!reducedMotion) {
      ctx.beginPath(); ctx.arc(p.x, p.y, (18 + f * 40) * scale, 0, Math.PI * 2); ctx.stroke();
      for (let i = 0; i < 9; i++) {
        const phase = i * Math.PI * 2 / 9, radius = 12 + f * 55;
        const at = point(burst.u + Math.cos(phase) * radius, burst.lane + Math.sin(phase) * radius);
        ctx.fillRect(at.x - 1, at.y - 1, 2 * scale, 2 * scale);
      }
    }
    ctx.restore();
  }
  },
  wisp: () => {
  ctx.globalAlpha = 1;
  const color = run.dash ? SPIRIT_CURRENT_COLOR : run.surge ? presentation.palette.wisp : presentation.palette.wisp, p = point(run.playerU, run.lane);
  // A wild soul lengthens the wake; impact visibly binds and extinguishes it.
  if (!reducedMotion) for (let i = 18; i > 0; i--) {
    const tr = point(run.playerU - i * (4 + run.speed * 2), run.lane + Math.sin(time * 4 - i * 0.3) * i * 0.35);
    ctx.globalAlpha = (1 - i / 19) * 0.28; ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(tr.x, tr.y, (14 - i * 0.55) * scale, 0, Math.PI * 2); ctx.fill();
  }
  ctx.globalAlpha = 1; ctx.save(); ctx.translate(p.x, p.y); ctx.scale(scale, scale);
  if ((run.surge || run.dash) && !reducedMotion) {
    ctx.strokeStyle = color; ctx.lineWidth = 1.2;
    for (const r of [30, 38]) { ctx.globalAlpha = Math.min(1, run.dash || run.surge / c.surgeSeconds) * 0.7; ctx.beginPath(); ctx.arc(0, 0, r, time * 3, time * 3 + Math.PI * 1.4); ctx.stroke(); }
    ctx.globalAlpha = 1;
  }
  if (run.hinder) {
    ctx.strokeStyle = '#e0a19a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(0, 0, 27, 0, Math.PI * 2); ctx.stroke(); ctx.globalAlpha = 0.7;
  }
  ctx.rotate(run.direction === 'down' ? Math.PI / 2 : run.direction === 'left' ? Math.PI : 0);
  drawWisp(ctx, time);
  ctx.restore(); ctx.globalAlpha = 1;
  },
  };
  for (const name of presentation.layers) layers[name]?.();
}
