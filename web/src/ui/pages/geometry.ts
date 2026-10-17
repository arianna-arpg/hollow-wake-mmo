/**
 * Geometry the development pages share, kept free of the DOM so it can be tested: client points against boxes
 * on screen (the interface root is scaled with a CSS transform, so a box's screen size differs from its layout
 * size), a canvas's backing store for sharp lines on any screen, wheel steps, and boxes held inside an area.
 */

/** A box on screen in client pixels, as getBoundingClientRect reports it. */
export interface ScreenBox {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}

/** The scale an element is drawn at: its width on screen over its width in interface pixels. */
export const drawnScale = (screenWidth: number, layoutWidth: number): number =>
  layoutWidth > 0 && screenWidth > 0 ? screenWidth / layoutWidth : 1;

/** Whether a client point is inside a box on screen, grown by some interface pixels (scaled to the screen). */
export function inside(box: ScreenBox, x: number, y: number, grow = 0, scale = 1): boolean {
  const g = grow * scale;
  return x >= box.left - g && x < box.right + g && y >= box.top - g && y < box.bottom + g;
}

/** A client point in a box's own interface pixels. */
export function localPoint(box: ScreenBox, scale: number, x: number, y: number): [number, number] {
  const s = scale > 0 ? scale : 1;
  return [(x - box.left) / s, (y - box.top) / s];
}

/** The pixels a canvas holds for a size in interface pixels drawn at a scale on a screen of a pixel ratio. */
export function backingSize(width: number, height: number, scale: number, pixelRatio: number): [number, number] {
  const k = Math.max(0.01, scale) * Math.max(0.01, pixelRatio);
  return [Math.max(1, Math.round(width * k)), Math.max(1, Math.round(height * k))];
}

/** A box of a size placed at a point and held inside an area, as the desktop clamps a popup. */
export function clampBox(x: number, y: number, w: number, h: number, areaW: number, areaH: number): [number, number] {
  return [Math.min(Math.max(0, x), Math.max(0, areaW - w)), Math.min(Math.max(0, y), Math.max(0, areaH - h))];
}

/**
 * The zoom factor of a wheel event: one notch (100 pixels, 3 lines or a page) is one step, as the desktop zooms
 * one step a notch; a trackpad's small deltas zoom by part of a step. Held to three steps an event.
 */
export function wheelFactor(deltaY: number, deltaMode: number, step = 1.12): number {
  const notches = deltaMode === 1 ? deltaY / 3 : deltaMode === 2 ? deltaY : deltaY / 100;
  const held = Math.min(3, Math.max(-3, notches));
  return step ** -held;
}

/** Ordinal string order, as the desktop sorts ids. */
export const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

export const clamp01 = (v: number): number => Math.min(1, Math.max(0, v));
