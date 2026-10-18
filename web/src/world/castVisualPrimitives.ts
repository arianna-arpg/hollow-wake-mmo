import type { CuePhase } from './castCueTimeline';

export type CastPrimitive = (phase: CuePhase) => number[][];
const builders = new Map<string, CastPrimitive>();
export const registerCastPrimitive = (kind: string, build: CastPrimitive): void => { builders.set(kind, build); };
export const castPrimitiveKinds = (): string[] => [...builders.keys()].sort();
export const castPrimitive = (phase: CuePhase): number[][] => builders.get(phase.primitive)?.(phase) ?? [];

registerCastPrimitive('segments', p => p.segments);
for (const kind of ['ring', 'rays']) registerCastPrimitive(kind, p => {
  const lines: number[][] = [], arc = p.arc * Math.PI / 180;
  for (let i = 0; i < p.pieces; i++) {
    const a = -arc / 2 + arc * i / p.pieces;
    const b = kind === 'ring' ? -arc / 2 + arc * (i + 1) / p.pieces : a;
    const inner = kind === 'ring' ? 1 : 0.35;
    lines.push([Math.cos(a) * inner, Math.sin(a) * inner, 0, Math.cos(b), Math.sin(b), 0]);
  }
  return lines;
});
registerCastPrimitive('corners', p => {
  const lines: number[][] = [];
  for (let i = 0; i < p.pieces; i++) {
    const a = Math.PI * 2 * i / p.pieces, c = Math.cos(a), s = Math.sin(a);
    lines.push([c + s * 0.18, s - c * 0.18, 0, c - s * 0.18, s + c * 0.18, 0]);
  }
  return lines;
});
