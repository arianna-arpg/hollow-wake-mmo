import { Color, DoubleSide, Group, LinearSRGBColorSpace, Mesh, OrthographicCamera, PlaneGeometry, Scene, ShaderMaterial, Vector3, WebGLRenderTarget, type WebGLRenderer } from 'three';
import type { Pose } from './animator';
import { BodyMesh, type BodyView } from './bodyMesh';
import type { BodyPlan } from './bodyPlan';

/**
 * Directional cards, the twin of the desktop's CardAtlas, CardBaker and CardNode: eight azimuths of a look's
 * rest pose rendered from the isometric pitch into one row of a render target, then drawn as one foot-anchored
 * quad per body that turns to face the camera about the vertical and samples the cell nearest the camera's
 * bearing from the body's yaw. Depth-tested and depth-written, like the geometry it stands in for.
 */

export const CELLS = 8;

const DEG = Math.PI / 180;

export class CardAtlas {
  target: WebGLRenderTarget | null = null;
  material: ShaderMaterial | null = null;
  height = 0;
  width = 0;
  cellPixels = 0;

  constructor(readonly key: string) {}

  get ready(): boolean {
    return this.target !== null;
  }

  /** Bytes the atlas holds on the GPU, counted from its dimensions (RGBA8 with mipmaps). */
  get bytes(): number {
    return this.target === null ? 0 : (this.target.width * this.target.height * 4 * 4) / 3;
  }

  dispose(): void {
    this.target?.dispose();
    this.target = null;
    this.material?.dispose();
    this.material = null;
  }
}

const CARD_VERTEX = `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const CARD_FRAGMENT = `
uniform sampler2D atlas;
uniform float cells;
uniform float cell;
uniform float flash;
uniform float fade;
varying vec2 vUv;
void main() {
  vec2 uv = vec2((vUv.x + cell) / cells, vUv.y);
  vec4 tex = texture2D(atlas, uv);
  if (tex.a < 0.5) { discard; }
  vec3 flashColor = vec3(1.0, 0.94, 0.82);
  vec3 col = mix(tex.rgb, flashColor, flash) + flashColor * flash * 0.3;
  gl_FragColor = vec4(col, tex.a * fade);
  #include <colorspace_fragment>
}
`;

/**
 * Bakes atlases one per frame in the renderer: an orthographic camera at the isometric pitch looks at the body,
 * which is turned to each azimuth in turn, and each frame is drawn into its cell of the render target.
 */
export class CardBaker {
  pixelsPerUnit = 56;
  maxCellPixels = 192;
  private readonly queue: Array<{ atlas: CardAtlas; plan: BodyPlan }> = [];
  private readonly scene = new Scene();
  private readonly camera = new OrthographicCamera(-1, 1, 1, -1, 0.05, 100);

  constructor(private readonly renderer: WebGLRenderer) {
    this.scene.background = null;
  }

  get pending(): number {
    return this.queue.length;
  }

  enqueue(atlas: CardAtlas, plan: BodyPlan): void {
    this.queue.push({ atlas, plan });
  }

  /** Bakes one queued atlas; call once per frame before the main render so the renderer's state is restored. */
  step(): void {
    const job = this.queue.shift();
    if (!job) {
      return;
    }
    const { atlas, plan } = job;
    const size = Math.max(plan.height, plan.width) * 1.08;
    const pixels = Math.min(this.maxCellPixels, Math.max(32, Math.ceil(size * this.pixelsPerUnit)));
    const target = new WebGLRenderTarget(pixels * CELLS, pixels, { generateMipmaps: true, samples: 4, colorSpace: LinearSRGBColorSpace });
    target.texture.generateMipmaps = true;
    const body = new BodyMesh(plan);
    this.scene.add(body.root);
    const pitch = 35.264 * DEG;
    const yaw = 45 * DEG;
    const centre = new Vector3(0, plan.height * 0.5, 0);
    const dir = new Vector3(Math.cos(pitch) * Math.sin(yaw), Math.sin(pitch), Math.cos(pitch) * Math.cos(yaw));
    this.camera.left = -size / 2;
    this.camera.right = size / 2;
    this.camera.top = size / 2;
    this.camera.bottom = -size / 2;
    this.camera.updateProjectionMatrix();
    this.camera.position.copy(centre).add(dir.clone().multiplyScalar(40));
    this.camera.lookAt(centre);
    const previousTarget = this.renderer.getRenderTarget();
    const previousFace = this.renderer.getActiveCubeFace();
    const previousMip = this.renderer.getActiveMipmapLevel();
    const previousColor = this.renderer.getClearColor(new Color());
    const previousAlpha = this.renderer.getClearAlpha();
    const previousAuto = this.renderer.autoClear;
    let baked = false;
    try {
      this.renderer.setRenderTarget(target);
      this.renderer.setClearColor(new Color(0, 0, 0), 0);
      this.renderer.clear();
      this.renderer.autoClear = false;
      for (let cell = 0; cell < CELLS; cell++) {
        body.root.rotation.y = yaw + (cell * Math.PI * 2) / CELLS;
        body.root.updateMatrixWorld(true);
        // Target-local physical pixels: do not mutate the canvas viewport or apply its pixel ratio.
        target.viewport.set(cell * pixels, 0, pixels, pixels);
        target.scissor.copy(target.viewport);
        target.scissorTest = true;
        this.renderer.setRenderTarget(target);
        this.renderer.render(this.scene, this.camera);
      }
      baked = true;
    } finally {
      this.renderer.autoClear = previousAuto;
      this.renderer.setRenderTarget(previousTarget, previousFace, previousMip);
      this.renderer.setClearColor(previousColor, previousAlpha);
      this.scene.remove(body.root);
      body.dispose();
      if (!baked) target.dispose();
    }
    atlas.target = target;
    atlas.height = plan.height;
    atlas.width = plan.width;
    atlas.cellPixels = pixels;
    atlas.material = new ShaderMaterial({
      vertexShader: CARD_VERTEX,
      fragmentShader: CARD_FRAGMENT,
      uniforms: { atlas: { value: target.texture }, cells: { value: CELLS }, cell: { value: 0 }, flash: { value: 0 }, fade: { value: 1 } },
      transparent: false,
      side: DoubleSide,
    });
  }
}

const quadGeometries = new Map<number, PlaneGeometry>();

export class CardView implements BodyView {
  readonly root = new Group();
  readonly plan: BodyPlan;
  readonly triangles = 2;
  private readonly pivot = new Group();
  private readonly quad: Mesh;
  private cell = -1;
  private flash = 0;
  private fade = 1;

  constructor(plan: BodyPlan, atlas: CardAtlas, scale: number) {
    this.plan = plan;
    this.root.name = 'card';
    const w = Math.max(atlas.height, atlas.width) * 1.08 * scale;
    const key = Math.round(w * 100);
    let geometry = quadGeometries.get(key);
    if (!geometry) {
      geometry = new PlaneGeometry(w, w);
      geometry.translate(0, w / 2, 0);
      quadGeometries.set(key, geometry);
    }
    this.quad = new Mesh(geometry, atlas.material ?? undefined);
    this.quad.castShadow = false;
    this.quad.onBeforeRender = () => {
      const m = this.quad.material as ShaderMaterial;
      m.uniforms.cell.value = this.cell;
      m.uniforms.flash.value = this.flash;
      m.uniforms.fade.value = this.fade;
    };
    this.pivot.add(this.quad);
    this.root.add(this.pivot);
  }

  /** Faces the camera about the vertical and picks the cell for the body's facing seen from the camera. */
  apply(pose: Pose, cameraYaw: number): void {
    const bodyYaw = this.root.parent?.rotation.y ?? 0;
    this.pivot.rotation.y = cameraYaw - bodyYaw + Math.PI;
    const tau = Math.PI * 2;
    const relative = (((bodyYaw - cameraYaw + Math.PI * 0.25) % tau) + tau) % tau;
    this.cell = Math.round((relative / tau) * CELLS) % CELLS;
    const root = pose.of('root');
    this.quad.rotation.set(root.turn[0] * DEG, 0, root.turn[1] * DEG);
    this.quad.position.set(0, root.offset[1], 0);
    this.quad.scale.setScalar(Math.max(0.05, root.scale));
    this.flash = pose.flash;
    this.fade = pose.opacity;
  }

  dispose(): void {}
}
