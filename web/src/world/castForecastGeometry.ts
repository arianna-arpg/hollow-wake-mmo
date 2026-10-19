import type { CastForecastRegion } from '../net/protocol';
import { forecastOutline, forecastSegments, forecastVerticalBounds } from './castForecastTimeline';

export interface ForecastGeometryStyle {
  segments: number; max_segments: number; max_chord_error: number;
  dash_fraction: number; direction_size: number; height_marker_size: number;
}
/** Segment endpoints in the region's local core frame (Z up), before world position, heading and lift. */
export type ForecastSegment = [number, number, number, number, number, number];

/** Inputs have passed the timeline's geometry gate; style is fixed for a renderer's lifetime.
 * Eligibility participates because the reused meshes also carry its color. */
export function sameForecastMesh(previous: CastForecastRegion | undefined, previousCommitted: boolean,
  current: CastForecastRegion | undefined, committed: boolean): boolean {
  if (!previous || !current) return !previous && !current;
  const a = previous.size, b = current.size;
  const [oldBottom, oldTop] = forecastVerticalBounds(previous), [bottom, top] = forecastVerticalBounds(current);
  return previousCommitted === committed && previous.shape === current.shape &&
    a.radius === b.radius && a.angle === b.angle && a.width === b.width && a.inner === b.inner && a.curvature === b.curvature &&
    previous.target_test === current.target_test && (previous.center_allowance ?? 0) === (current.center_allowance ?? 0) &&
    previous.viewer_eligibility === current.viewer_eligibility && oldBottom === bottom && oldTop === top;
}

export function forecastGeometry(r: CastForecastRegion, committed: boolean, s: ForecastGeometryStyle): ForecastSegment[] {
  const segments = forecastSegments(r, s.segments, s.max_segments, s.max_chord_error);
  if (segments === 0) return [];
  const outline = forecastOutline(r, segments), parts: ForecastSegment[] = [];
  const [bottom, top] = forecastVerticalBounds(r);
  const levels = r.target_test === 'body' ? (bottom === top ? [bottom] : [bottom, top]) : [0];
  const arrow = s.direction_size;
  parts.push([0, -arrow / 2, levels[0], arrow, 0, levels[0]], [arrow, 0, levels[0], 0, arrow / 2, levels[0]]);
  if (committed) parts.push([0, arrow / 2, levels[0], 0, -arrow / 2, levels[0]]);
  // Broken perimeters describe live candidate coverage, including holes, at each sampled level.
  for (const level of levels) for (const loop of [outline.outer, ...outline.holes]) for (let i = 0; i < loop.length; i++) {
    const a = loop[i], b = loop[(i + 1) % loop.length];
    parts.push([a[0], a[1], level, a[0] + (b[0] - a[0]) * s.dash_fraction, a[1] + (b[1] - a[1]) * s.dash_fraction, level]);
  }
  for (let i = 0; i < outline.outer.length; i += Math.max(1, Math.floor(outline.outer.length / 4))) {
    const p = outline.outer[i], marker = Math.min((top - bottom) / 2, s.height_marker_size);
    parts.push([p[0], p[1], bottom, p[0], p[1], bottom + marker], [p[0], p[1], top - marker, p[0], p[1], top]);
  }
  return parts;
}
