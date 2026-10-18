import { BufferAttribute, BufferGeometry, Color, DoubleSide, Group, Matrix4, Mesh, Quaternion, ShaderMaterial, Vector3 } from 'three';
import { srgbToLinear } from '../palette';
import type { Pose } from './animator';
import { buildPart, type BodyPart, type BodyPlan, type Vec3 } from './bodyPlan';

/**
 * A body plan as painted geometry in three.js, the twin of the desktop's BodyNode: a Group per bone in the
 * plan's hierarchy, each with one mesh of its parts (lit facets in one material, glowing parts in another),
 * posed each frame by the animator. Body space is x forward, y up, z right, and the view turns it by the yaw.
 * One shared material of each kind serves every body; the flash and fade are set per mesh just before it
 * draws, so the draw calls stay sortable and the materials stay two.
 */

const DEG = Math.PI / 180;

export interface BodyView {
  readonly root: Group;
  readonly plan: BodyPlan;
  readonly triangles: number;
  apply(pose: Pose, cameraYaw: number): void;
  dispose(): void;
}

const PAINTED_VERTEX = `
varying vec3 vColor;
varying vec3 vNormalW;
void main() {
  vColor = color;
  vNormalW = normalize(mat3(modelMatrix) * normal);
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const PAINTED_FRAGMENT = `
uniform vec3 sunDirection;
uniform vec3 sunColor;
uniform vec3 ambientSky;
uniform vec3 ambientGround;
uniform float flash;
uniform float fade;
uniform float unlit;
varying vec3 vColor;
varying vec3 vNormalW;
void main() {
  vec3 n = normalize(vNormalW);
  float up = n.y * 0.5 + 0.5;
  vec3 ambient = mix(ambientGround, ambientSky, up);
  float diffuse = max(dot(n, sunDirection), 0.0);
  vec3 lit = vColor * (ambient + sunColor * diffuse);
  vec3 col = mix(lit, vColor, unlit);
  vec3 flashColor = vec3(1.0, 0.94, 0.82);
  col = mix(col, flashColor, flash) + flashColor * flash * 0.3;
  gl_FragColor = vec4(col, fade);
  #include <colorspace_fragment>
}
`;

/** The light every body shares: set by the app from the day cycle each frame. */
export const bodyLight = {
  sunDirection: new Vector3(0.4, 0.8, 0.3).normalize(),
  sunColor: new Color(1, 0.96, 0.9),
  ambientSky: new Color(0.55, 0.58, 0.66),
  ambientGround: new Color(0.3, 0.28, 0.25),
};

function makeMaterial(unlit: number, transparent: boolean): ShaderMaterial {
  return new ShaderMaterial({
    vertexShader: PAINTED_VERTEX,
    fragmentShader: PAINTED_FRAGMENT,
    vertexColors: true,
    uniforms: {
      sunDirection: { value: bodyLight.sunDirection },
      sunColor: { value: bodyLight.sunColor },
      ambientSky: { value: bodyLight.ambientSky },
      ambientGround: { value: bodyLight.ambientGround },
      flash: { value: 0 },
      fade: { value: 1 },
      unlit: { value: unlit },
    },
    transparent,
    side: DoubleSide,
  });
}

let painted: ShaderMaterial | null = null;
let glow: ShaderMaterial | null = null;
let paintedFading: ShaderMaterial | null = null;
let glowFading: ShaderMaterial | null = null;

export function bodyMaterials(): { painted: ShaderMaterial; glow: ShaderMaterial; paintedFading: ShaderMaterial; glowFading: ShaderMaterial } {
  painted ??= makeMaterial(0, false);
  glow ??= makeMaterial(1, false);
  paintedFading ??= makeMaterial(0, true);
  glowFading ??= makeMaterial(1, true);
  return { painted, glow, paintedFading, glowFading };
}

/** Merges a bone's parts into one geometry (lit or glowing), positioned relative to the bone's pivot. */
function mergeParts(parts: BodyPart[], pivot: Vec3, glowing: boolean): BufferGeometry | null {
  const positions: number[] = [];
  const normals: number[] = [];
  const colors: number[] = [];
  for (const part of parts) {
    if (part.glow !== glowing) {
      continue;
    }
    const built = buildPart(part);
    const r = srgbToLinear(part.color[0]);
    const g = srgbToLinear(part.color[1]);
    const b = srgbToLinear(part.color[2]);
    for (let i = 0; i < built.positions.length; i += 3) {
      positions.push(built.positions[i] - pivot[0], built.positions[i + 1] - pivot[1], built.positions[i + 2] - pivot[2]);
      normals.push(built.normals[i], built.normals[i + 1], built.normals[i + 2]);
      colors.push(r, g, b);
    }
  }
  if (positions.length === 0) {
    return null;
  }
  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new BufferAttribute(Float32Array.from(positions), 3));
  geometry.setAttribute('normal', new BufferAttribute(Float32Array.from(normals), 3));
  geometry.setAttribute('color', new BufferAttribute(Float32Array.from(colors), 3));
  geometry.computeBoundingSphere();
  return geometry;
}

const scratchQ = new Quaternion();
const scratchQ2 = new Quaternion();
const scratchM = new Matrix4();
const AXIS_X = new Vector3(1, 0, 0);
const AXIS_Y = new Vector3(0, 1, 0);
const AXIS_Z = new Vector3(0, 0, 1);

export class BodyMesh implements BodyView {
  readonly root = new Group();
  readonly plan: BodyPlan;
  triangles = 0;
  private readonly bones = new Map<string, Group>();
  private readonly localPivots = new Map<string, Vec3>();
  private readonly rests = new Map<string, Vec3>();
  private readonly meshes: Mesh[] = [];
  private readonly geometries: BufferGeometry[] = [];
  private flash = 0;
  private fade = 1;

  constructor(plan: BodyPlan) {
    this.plan = plan;
    this.root.name = 'body';
    const byName = new Map(plan.bones.map((b) => [b.name, b]));
    for (const bone of plan.bones) {
      const node = new Group();
      node.name = bone.name;
      const parent = bone.parent !== null ? byName.get(bone.parent) : undefined;
      const parentPivot = parent?.pivot ?? [0, 0, 0];
      const local: Vec3 = [bone.pivot[0] - parentPivot[0], bone.pivot[1] - parentPivot[1], bone.pivot[2] - parentPivot[2]];
      this.localPivots.set(bone.name, local);
      this.rests.set(bone.name, bone.rest);
      node.position.set(local[0], local[1], local[2]);
      const parentNode = bone.parent !== null ? this.bones.get(bone.parent) : undefined;
      (parentNode ?? this.root).add(node);
      this.bones.set(bone.name, node);
    }
    const materials = bodyMaterials();
    const grouped = new Map<string, BodyPart[]>();
    for (const part of plan.parts) {
      const list = grouped.get(part.bone) ?? [];
      list.push(part);
      grouped.set(part.bone, list);
    }
    for (const [name, parts] of grouped) {
      const node = this.bones.get(name);
      const bone = byName.get(name);
      if (!node || !bone) {
        continue;
      }
      for (const glowing of [false, true]) {
        const geometry = mergeParts(parts, bone.pivot, glowing);
        if (geometry === null) {
          continue;
        }
        const mesh = new Mesh(geometry, glowing ? materials.glow : materials.painted);
        mesh.castShadow = !glowing;
        mesh.frustumCulled = true;
        mesh.onBeforeRender = () => {
          const m = mesh.material as ShaderMaterial;
          m.uniforms.flash.value = this.flash;
          m.uniforms.fade.value = this.fade;
        };
        node.add(mesh);
        this.meshes.push(mesh);
        this.geometries.push(geometry);
        this.triangles += (geometry.getAttribute('position').count / 3) | 0;
      }
    }
  }

  /** Moves every bone to its pose: the rest turn plus the animator's, about the bone's pivot. */
  apply(pose: Pose): void {
    for (const [name, node] of this.bones) {
      const p = pose.of(name);
      const rest = this.rests.get(name) ?? [0, 0, 0];
      const roll = (p.turn[0] + rest[0]) * DEG;
      const pitch = (p.turn[1] + rest[1]) * DEG;
      const yaw = (p.turn[2] + rest[2]) * DEG;
      // roll about x, then pitch about z, then yaw about y: the plan's convention, applied right to left
      scratchQ.setFromAxisAngle(AXIS_Y, yaw);
      scratchQ2.setFromAxisAngle(AXIS_Z, pitch);
      scratchQ.multiply(scratchQ2);
      scratchQ2.setFromAxisAngle(AXIS_X, roll);
      scratchQ.multiply(scratchQ2);
      node.quaternion.copy(scratchQ);
      const local = this.localPivots.get(name) ?? [0, 0, 0];
      node.position.set(local[0] + p.offset[0], local[1] + p.offset[1], local[2] + p.offset[2]);
      node.scale.setScalar(Math.max(0.05, p.scale));
    }
    this.flash = pose.flash;
    if (pose.opacity !== this.fade) {
      this.fade = pose.opacity;
      const materials = bodyMaterials();
      for (const mesh of this.meshes) {
        const glowing = mesh.material === materials.glow || mesh.material === materials.glowFading;
        mesh.material = this.fade < 0.999 ? (glowing ? materials.glowFading : materials.paintedFading) : glowing ? materials.glow : materials.painted;
      }
    }
    scratchM.identity();
  }

  dispose(): void {
    for (const g of this.geometries) {
      g.dispose();
    }
  }
}
