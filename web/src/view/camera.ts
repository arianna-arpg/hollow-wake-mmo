import { MathUtils, OrthographicCamera, PerspectiveCamera, Raycaster, Vector2, Vector3, type Camera } from 'three';
import type { CameraSettings } from '../config';

export type CameraMode = 'isometric' | 'third_person';

/**
 * Two ways of looking at the same fully 3D world, like the desktop CameraRig. Isometric: orthographic, fixed
 * pitch, yaw in steps, follows the player, scroll to zoom. Third person: perspective behind the character,
 * orbit with the right mouse button, scroll for distance. Presentation only; nothing else knows which is on.
 */
export class CameraRig {
  readonly ortho: OrthographicCamera;
  readonly perspective: PerspectiveCamera;
  mode: CameraMode;
  yaw: number;
  private readonly pitch: number;
  private isoSize: number;
  private distance: number;
  private thirdPitch: number;
  private readonly target = new Vector3();
  private hasTarget = false;
  private aspect = 16 / 9;
  private readonly raycaster = new Raycaster();
  private readonly ndc = new Vector2();

  constructor(private readonly settings: CameraSettings) {
    this.ortho = new OrthographicCamera(-1, 1, 1, -1, settings.near, settings.far);
    this.perspective = new PerspectiveCamera(settings.thirdFovDegrees, this.aspect, settings.near, settings.far);
    this.mode = settings.mode === 'third_person' ? 'third_person' : 'isometric';
    this.yaw = MathUtils.degToRad(settings.isoYawDegrees);
    this.pitch = MathUtils.degToRad(settings.isoPitchDegrees);
    this.isoSize = settings.isoSize;
    this.distance = settings.thirdDistance;
    this.thirdPitch = MathUtils.degToRad(settings.thirdPitchDegrees);
  }

  get camera(): Camera {
    return this.mode === 'isometric' ? this.ortho : this.perspective;
  }

  resize(width: number, height: number): void {
    this.aspect = width / Math.max(1, height);
    this.perspective.aspect = this.aspect;
    this.perspective.updateProjectionMatrix();
    this.applyOrthoSize();
  }

  private applyOrthoSize(): void {
    const half = this.isoSize / 2;
    this.ortho.top = half;
    this.ortho.bottom = -half;
    this.ortho.left = -half * this.aspect;
    this.ortho.right = half * this.aspect;
    this.ortho.updateProjectionMatrix();
  }

  /** Follow a point in three.js space. The first call places the camera at once. */
  follow(point: Vector3): void {
    const first = !this.hasTarget;
    this.target.copy(point);
    this.hasTarget = true;
    if (first) {
      this.update(0, true);
    }
  }

  toggle(): void {
    this.mode = this.mode === 'isometric' ? 'third_person' : 'isometric';
    this.update(0, true);
  }

  rotateStep(direction: number): void {
    const step = this.mode === 'isometric' ? this.settings.isoRotateStepDegrees : this.settings.thirdRotateStepDegrees;
    this.yaw += MathUtils.degToRad(step) * direction;
  }

  zoom(steps: number): void {
    const s = this.settings;
    if (this.mode === 'isometric') {
      this.isoSize = MathUtils.clamp(this.isoSize - steps * s.isoZoomStep, s.isoSizeMin, s.isoSizeMax);
      this.applyOrthoSize();
    } else {
      this.distance = MathUtils.clamp(this.distance - steps * s.thirdZoomStep, s.thirdDistanceMin, s.thirdDistanceMax);
    }
  }

  /** Right-button drag in third person. */
  orbit(dx: number, dy: number): void {
    if (this.mode !== 'third_person') {
      return;
    }
    const s = this.settings;
    this.yaw -= dx * s.orbitSensitivity;
    this.thirdPitch = MathUtils.clamp(this.thirdPitch + dy * s.pitchSensitivity, MathUtils.degToRad(s.thirdPitchMinDegrees), MathUtils.degToRad(s.thirdPitchMaxDegrees));
  }

  update(dt: number, force = false): void {
    if (!this.hasTarget) {
      return;
    }
    const s = this.settings;
    const camera = this.mode === 'isometric' ? this.ortho : this.perspective;
    const desired = new Vector3();
    const lookAt = new Vector3();
    if (this.mode === 'isometric') {
      desired.set(Math.cos(this.pitch) * Math.sin(this.yaw), Math.sin(this.pitch), Math.cos(this.pitch) * Math.cos(this.yaw)).multiplyScalar(s.isoDistance).add(this.target);
      lookAt.copy(this.target);
    } else {
      const eye = this.target.clone();
      eye.y += s.thirdEyeHeight;
      desired.set(Math.cos(this.thirdPitch) * Math.sin(this.yaw), Math.sin(this.thirdPitch), Math.cos(this.thirdPitch) * Math.cos(this.yaw)).multiplyScalar(this.distance).add(eye);
      lookAt.copy(eye);
    }
    if (force || dt <= 0) {
      camera.position.copy(desired);
    } else {
      const rate = this.mode === 'isometric' ? s.isoFollowRate : s.thirdFollowRate;
      camera.position.lerp(desired, 1 - Math.exp(-dt * rate));
    }
    camera.lookAt(lookAt);
    camera.updateMatrixWorld();
  }

  /** Camera-relative forward on the ground plane (three.js x, z), for held movement. */
  groundForward(): [number, number] {
    const f = new Vector3();
    this.camera.getWorldDirection(f);
    const len = Math.hypot(f.x, f.z);
    return len < 1e-4 ? [0, -1] : [f.x / len, f.z / len];
  }

  groundRight(): [number, number] {
    const [x, z] = this.groundForward();
    return [-z, x];
  }

  /** A ray through a point on the canvas (CSS pixels), in three.js space. */
  ray(x: number, y: number, width: number, height: number): { origin: Vector3; direction: Vector3 } {
    this.ndc.set((x / Math.max(1, width)) * 2 - 1, -(y / Math.max(1, height)) * 2 + 1);
    this.raycaster.setFromCamera(this.ndc, this.camera);
    return { origin: this.raycaster.ray.origin.clone(), direction: this.raycaster.ray.direction.clone() };
  }

  /** Where a three.js point appears on the canvas, in CSS pixels. */
  project(point: Vector3, width: number, height: number): { x: number; y: number; visible: boolean } {
    const p = point.clone().project(this.camera);
    return { x: (p.x * 0.5 + 0.5) * width, y: (-p.y * 0.5 + 0.5) * height, visible: p.z > -1 && p.z < 1 };
  }
}
