import { Mesh, PlaneGeometry, ShaderMaterial } from 'three';

/**
 * The mark where a body meets the ground, the twin of the desktop's ContactMark: a soft disc that gives every
 * body its contact and, for creatures, an edge that says the relation without colour (inward teeth for a
 * hostile, spaced ticks for a friend, a plain rim for the rest). Unlit, never written to depth, drawn just
 * above the ground under the body's root.
 */

const VERTEX = `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const FRAGMENT = `
uniform float opacity;
uniform float style;
uniform float fade;
varying vec2 vUv;
void main() {
  vec2 p = vUv * 2.0 - 1.0;
  float r = length(p);
  float aa = fwidth(r) * 1.5;
  float disc = 1.0 - smoothstep(0.78 - aa, 0.9, r);
  float a = disc * opacity * (1.0 - r * 0.35);
  float ring = smoothstep(0.86, 0.9, r) * (1.0 - smoothstep(0.98, 1.0, r));
  float angle = atan(p.y, p.x);
  float mark = 0.0;
  if (style > 0.5 && style < 1.5) {
    float tooth = abs(fract(angle / 6.2831853 * 12.0) - 0.5) * 2.0;
    float inward = 1.0 - smoothstep(0.78, 0.98, r);
    mark = ring * step(tooth, 0.55) + (1.0 - smoothstep(0.72, 0.9, r)) * step(0.8, r) * step(tooth, 0.3) * inward;
  } else if (style >= 1.5) {
    float tick = step(0.7, abs(fract(angle / 6.2831853 * 8.0) - 0.5) * 2.0);
    mark = ring * tick;
  }
  vec3 col = mix(vec3(0.08, 0.09, 0.1), vec3(0.92, 0.9, 0.84), mark);
  gl_FragColor = vec4(col, max(a, mark * 0.9) * fade);
  #include <colorspace_fragment>
}
`;

let material: ShaderMaterial | null = null;
const geometries = new Map<number, PlaneGeometry>();

export function markStyle(style: string): number {
  return style === 'teeth' ? 1 : style === 'ticks' ? 2 : 0;
}

/** A contact mark of a size and style; the opacity is the content's, the fade the body's. */
export class ContactMark {
  readonly mesh: Mesh;
  private fade = 1;

  constructor(size: number, readonly style: number, opacity: number) {
    material ??= new ShaderMaterial({ vertexShader: VERTEX, fragmentShader: FRAGMENT, uniforms: { opacity: { value: opacity }, style: { value: 0 }, fade: { value: 1 } }, transparent: true, depthWrite: false });
    const key = Math.round(size * 20);
    let geometry = geometries.get(key);
    if (!geometry) {
      geometry = new PlaneGeometry(size, size);
      geometry.rotateX(-Math.PI / 2);
      geometries.set(key, geometry);
    }
    this.mesh = new Mesh(geometry, material);
    this.mesh.position.y = 0.04;
    this.mesh.renderOrder = 1;
    this.mesh.onBeforeRender = () => {
      const m = this.mesh.material as ShaderMaterial;
      m.uniforms.style.value = this.style;
      m.uniforms.fade.value = this.fade;
    };
  }

  setFade(fade: number): void {
    this.fade = fade;
  }
}
