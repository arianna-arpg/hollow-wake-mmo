import { Vector3 } from 'three';

/**
 * The simulation is X east, Y north, Z up; three.js is X east, Y up, Z south. Every conversion between the
 * two goes through here and nowhere else (the desktop client's Coords.cs makes the same mapping for Godot).
 */

export interface CorePoint {
  x: number;
  y: number;
  z: number;
}

export function toThree(x: number, y: number, z: number, out: Vector3 = new Vector3()): Vector3 {
  return out.set(x, z, -y);
}

export function toCore(v: Vector3): CorePoint {
  return { x: v.x, y: -v.z, z: v.y };
}

/** A direction on three's ground plane (x, z) as a simulation ground direction (x, y). */
export function groundToCore(gx: number, gz: number): { x: number; y: number } {
  return { x: gx, y: -gz };
}
