import { Color, SRGBColorSpace } from 'three';
import { parseHex, type Rgb } from './palette';

/** A config colour (sRGB hex or tuple) as a three.js Color, which stores linear values. */
export function linearColor(c: Rgb | string): Color {
  const rgb = typeof c === 'string' ? parseHex(c) : c;
  return new Color().setRGB(rgb[0], rgb[1], rgb[2], SRGBColorSpace);
}
