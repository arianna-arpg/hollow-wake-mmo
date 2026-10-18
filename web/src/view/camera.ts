import { MathUtils, OrthographicCamera, PerspectiveCamera, Raycaster, Vector2, Vector3, type Camera } from 'three';
import type { CameraSettings } from '../config';

export type CameraMode = 'isometric' | 'third_person';

/**
 * Two ways of looking at the same fully 3D world, like the desktop CameraRig. Isometric: orthographic, fixed
 * pitch, yaw in steps, follows the player, scroll to zoom. Third person: perspective behind the character,
 * orbit with the right mouse button, scroll for distance. The zoom and the turn ease toward their targets and
 * a shake (a hit taken, a kill) rides on top, timed by the motion content and still under reduced motion.
 * Presentation only; nothing else knows which is on.
 */
export class CameraRig {
  readonly ortho: OrthographicCamera;
  readonly perspective: PerspectiveCamera;
  mode: CameraMode;
  yaw: number;
  private yawNow: number;
  private readonly pitch: number;
  private isoSize: number;
  private isoNow: number;
  private distance: number;
  private distanceNow: number;
  private thirdPitch: number;
  private readonly target = new Vector3();
  private hasTarget = false;
  private aspect = 16 / 9;
  private readonly raycaster = new Raycaster();
  private readonly ndc = new Vector2();
  private shake = 0;
  private shakeStarted = 0;
  private readonly offset = new Vector3();

  private readonly settings: CameraSettings;
  private readonly baseOrbit: number;
  private readonly basePitch: number;

  /** The feel, from the motion content. */
  zoomEase = 9;
  shakeSeconds = 0.28;
  shakeRate = 28;
  shakeEnabled = true;
  reducedMotion = false;

  constructor(settings: CameraSettings) {
    this.settings = { ...settings };
    this.baseOrbit = settings.orbitSensitivity;
    this.basePitch = settings.pitchSensitivity;
    this.ortho = new OrthographicCamera(-1, 1, 1, -1, settings.near, settings.far);
    this.perspective = new PerspectiveCamera(settings.thirdFovDegrees, this.aspect, settings.near, settings.far);
    this.mode = settings.mode === 'third_person' ? 'third_person' : 'isometric';
    this.yaw = MathUtils.degToRad(settings.isoYawDegrees);
    this.yawNow = this.yaw;
    this.pitch = MathUtils.degToRad(settings.isoPitchDegrees);
    this.isoSize = settings.isoSize;
    this.isoNow = settings.isoSize;
    this.distance = settings.thirdDistance;
    this.distanceNow = settings.thirdDistance;
    this.thirdPitch = MathUtils.degToRad(settings.thirdPitchDegrees);
  }

  get camera(): Camera {
    return this.mode === 'isometric' ? this.ortho : this.perspective;
  }

  /** The orthographic size now, after easing, for the representation choice. */
  get orthoSize(): number {
    return this.isoNow;
  }

  /** The yaw the camera has now, after easing. */
  get yawCurrent(): number {
    return this.yawNow;
  }

  resize(width: number, height: number): void {
    this.aspect = width / Math.max(1, height);
    this.perspective.aspect = this.aspect;
    this.perspective.updateProjectionMatrix();
    this.applyOrthoSize();
  }

  private applyOrthoSize(): void {
    const half = this.isoNow / 2;
    this.ortho.top = half;
    this.ortho.bottom = -half;
    this.ortho.left = -half * this.aspect;
    this.ortho.right = half * this.aspect;
    this.ortho.updateProjectionMatrix();
  }

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

  setMode(mode: CameraMode): void {
    if (mode !== this.mode) {
      this.toggle();
    }
  }

  configure(o: { isoRotateStep: number; isoZoomMax: number; sensitivity: number; invert: boolean; fov: number }): void {
    const s = this.settings;
    s.isoRotateStepDegrees = o.isoRotateStep;
    s.isoSizeMax = Math.max(s.isoSizeMin + 1, o.isoZoomMax);
    this.isoSize = MathUtils.clamp(this.isoSize, s.isoSizeMin, s.isoSizeMax);
    s.orbitSensitivity = this.baseOrbit * o.sensitivity;
    s.pitchSensitivity = this.basePitch * o.sensitivity * (o.invert ? -1 : 1);
    s.thirdFovDegrees = o.fov;
    this.perspective.fov = o.fov;
    this.perspective.updateProjectionMatrix();
  }

  rotateStep(direction: number): void {
    const step = this.mode === 'isometric' ? this.settings.isoRotateStepDegrees : this.settings.thirdRotateStepDegrees;
    this.yaw += MathUtils.degToRad(step) * direction;
  }

  zoom(steps: number): void {
    const s = this.settings;
    if (this.mode === 'isometric') {
      // a zoom step is a fraction of the size, so the near end moves gently and the far end briskly
      this.isoSize = MathUtils.clamp(this.isoSize * 0.88 ** steps, s.isoSizeMin, s.isoSizeMax);
    } else {
      this.distance = MathUtils.clamp(this.distance * 0.88 ** steps, s.thirdDistanceMin, s.thirdDistanceMax);
    }
  }

  orbit(dx: number, dy: number): void {
    if (this.mode !== 'third_person') {
      return;
    }
    const s = this.settings;
    this.yaw -= dx * s.orbitSensitivity;
    this.thirdPitch = MathUtils.clamp(this.thirdPitch + dy * s.pitchSensitivity, MathUtils.degToRad(s.thirdPitchMinDegrees), MathUtils.degToRad(s.thirdPitchMaxDegrees));
  }

  /** A shake of the given strength (world units at full), decaying over the shake time; the strongest wins. */
  shakeBy(strength: number, now: number): void {
    if (!this.shakeEnabled || this.reducedMotion || strength <= 0) {
      return;
    }
    const remaining = this.shakeSeconds <= 0 ? 0 : Math.max(0, 1 - (now - this.shakeStarted) / this.shakeSeconds) * this.shake;
    if (strength >= remaining) {
      this.shake = strength;
      this.shakeStarted = now;
    }
  }

  update(dt: number, force = false, now = 0): void {
    if (!this.hasTarget) {
      return;
    }
    const s = this.settings;
    if (force || dt <= 0) {
      this.isoNow = this.isoSize;
      this.distanceNow = this.distance;
      this.yawNow = this.yaw;
    } else {
      const ease = 1 - Math.exp(-dt * this.zoomEase);
      this.isoNow += (this.isoSize - this.isoNow) * ease;
      this.distanceNow += (this.distance - this.distanceNow) * ease;
      const turn = 1 - Math.exp(-dt * this.zoomEase * 1.4);
      let d = this.yaw - this.yawNow;
      d = Math.atan2(Math.sin(d), Math.cos(d));
      this.yawNow += d * turn;
    }
    this.applyOrthoSize();
    const camera = this.mode === 'isometric' ? this.ortho : this.perspective;
    const desired = new Vector3();
    const lookAt = new Vector3();
    if (this.mode === 'isometric') {
      desired.set(Math.cos(this.pitch) * Math.sin(this.yawNow), Math.sin(this.pitch), Math.cos(this.pitch) * Math.cos(this.yawNow)).multiplyScalar(s.isoDistance).add(this.target);
      lookAt.copy(this.target);
    } else {
      const eye = this.target.clone();
      eye.y += s.thirdEyeHeight;
      desired.set(Math.cos(this.thirdPitch) * Math.sin(this.yawNow), Math.sin(this.thirdPitch), Math.cos(this.thirdPitch) * Math.cos(this.yawNow)).multiplyScalar(this.distanceNow).add(eye);
      lookAt.copy(eye);
    }
    if (force || dt <= 0) {
      camera.position.copy(desired);
    } else {
      const rate = this.mode === 'isometric' ? s.isoFollowRate : s.thirdFollowRate;
      camera.position.lerp(desired, 1 - Math.exp(-dt * rate));
    }
    camera.lookAt(lookAt);
    if (this.shake > 0 && now > 0) {
      const k = this.shakeSeconds <= 0 ? 0 : 1 - (now - this.shakeStarted) / this.shakeSeconds;
      if (k <= 0) {
        this.shake = 0;
      } else {
        const a = now * this.shakeRate;
        const amount = this.shake * k * k;
        camera.updateMatrixWorld();
        const right = new Vector3().setFromMatrixColumn(camera.matrixWorld, 0);
        const upward = new Vector3().setFromMatrixColumn(camera.matrixWorld, 1);
        this.offset.copy(right).multiplyScalar((Math.sin(a) * 0.6 + Math.sin(a * 2.3) * 0.4) * amount).addScaledVector(upward, Math.sin(a * 1.7 + 1.3) * 0.7 * amount);
        camera.position.add(this.offset);
      }
    }
    camera.updateMatrixWorld();
  }

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

  ray(x: number, y: number, width: number, height: number): { origin: Vector3; direction: Vector3 } {
    this.ndc.set((x / Math.max(1, width)) * 2 - 1, -(y / Math.max(1, height)) * 2 + 1);
    this.raycaster.setFromCamera(this.ndc, this.camera);
    return { origin: this.raycaster.ray.origin.clone(), direction: this.raycaster.ray.direction.clone() };
  }

  project(point: Vector3, width: number, height: number): { x: number; y: number; visible: boolean } {
    const p = point.clone().project(this.camera);
    return { x: (p.x * 0.5 + 0.5) * width, y: (-p.y * 0.5 + 0.5) * height, visible: p.z > -1 && p.z < 1 };
  }
}
