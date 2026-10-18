import { Color, Vector4, WebGLRenderTarget, type WebGLRenderer } from 'three';
import { expect, it } from 'vitest';
import { BodyPlan } from './bodyPlan';
import { CardAtlas, CardBaker, CELLS } from './cards';

// Stateful renderer boundary: baking must leave a later world draw with its original full viewport.
class RendererState {
  target: WebGLRenderTarget | null = null;
  viewport = new Vector4(7, 11, 1280, 720);
  scissor = new Vector4(20, 30, 900, 600);
  scissorTest = true;
  color = new Color('#7191b0');
  alpha = 0.7;
  autoClear = true;
  currentViewport = this.viewport.clone().multiplyScalar(2);
  currentScissor = this.scissor.clone().multiplyScalar(2);
  currentScissorTest = this.scissorTest;
  draws = 0;
  readonly drawViewports: number[][] = [];
  getPixelRatio() { return 2; }
  fail = false;
  getRenderTarget() { return this.target; }
  setRenderTarget(target: WebGLRenderTarget | null) {
    this.target = target;
    this.currentViewport.copy(target?.viewport ?? this.viewport.clone().multiplyScalar(this.getPixelRatio()));
    this.currentScissor.copy(target?.scissor ?? this.scissor.clone().multiplyScalar(this.getPixelRatio()));
    this.currentScissorTest = target?.scissorTest ?? this.scissorTest;
  }
  getViewport(out: Vector4) { return out.copy(this.viewport); }
  getScissor(out: Vector4) { return out.copy(this.scissor); }
  getScissorTest() { return this.scissorTest; }
  setViewport(x: Vector4 | number, y?: number, width?: number, height?: number) {
    if (typeof x === 'number') this.viewport.set(x, y!, width!, height!);
    else this.viewport.copy(x);
    this.currentViewport.copy(this.viewport).multiplyScalar(this.getPixelRatio());
  }
  setScissor(x: Vector4 | number, y?: number, width?: number, height?: number) {
    if (typeof x === 'number') this.scissor.set(x, y!, width!, height!);
    else this.scissor.copy(x);
    this.currentScissor.copy(this.scissor).multiplyScalar(this.getPixelRatio());
  }
  setScissorTest(value: boolean) { this.scissorTest = value; this.currentScissorTest = value; }
  getClearColor(out: Color) { return out.copy(this.color); }
  getClearAlpha() { return this.alpha; }
  setClearColor(color: Color, alpha: number) { this.color.copy(color); this.alpha = alpha; }
  clear() {}
  render() {
    this.draws++;
    this.drawViewports.push(this.currentViewport.toArray());
    if (this.fail && this.draws === 3) throw new Error('interrupted atlas');
  }
}

it.each([[false, false], [false, true], [true, false], [true, true]])(
  'preserves the next world draw (offscreen=%s, interrupted=%s)', (offscreen, interrupted) => {
    const renderer = new RendererState();
    const previousTarget = offscreen ? new WebGLRenderTarget(320, 180) : null;
    renderer.setRenderTarget(previousTarget);
    const currentViewport = renderer.currentViewport.clone();
    const currentScissor = renderer.currentScissor.clone();
    const currentScissorTest = renderer.currentScissorTest;
    renderer.fail = interrupted;
    const viewport = renderer.viewport.clone();
    const scissor = renderer.scissor.clone();
    const color = renderer.color.clone();
    const plan = new BodyPlan();
    plan.height = 1.8; plan.width = 1;
    plan.bone('root', null, [0, 0, 0]);
    plan.add('root', 'box', [0.4, 1.8, 0.4], [0, 0.9, 0], [1, 1, 1]);
    const atlas = new CardAtlas('test');
    const baker = new CardBaker(renderer as unknown as WebGLRenderer);
    baker.enqueue(atlas, plan);
    if (interrupted) expect(() => baker.step()).toThrow('interrupted atlas');
    else baker.step();
    expect(renderer.target).toBe(previousTarget);
    expect(renderer.viewport.toArray()).toEqual(viewport.toArray());
    expect(renderer.scissor.toArray()).toEqual(scissor.toArray());
    expect(renderer.scissorTest).toBe(true);
    expect(renderer.currentViewport.toArray()).toEqual(currentViewport.toArray());
    expect(renderer.currentScissor.toArray()).toEqual(currentScissor.toArray());
    expect(renderer.currentScissorTest).toBe(currentScissorTest);
    expect(renderer.color.toArray()).toEqual(color.toArray());
    expect(renderer.alpha).toBe(0.7);
    expect(renderer.autoClear).toBe(true);
    expect(renderer.draws).toBe(interrupted ? 3 : CELLS);
    expect(atlas.ready).toBe(!interrupted);
    if (!interrupted) {
      for (const [cell, viewport] of renderer.drawViewports.entries()) {
        expect(viewport).toEqual([cell * atlas.cellPixels, 0, atlas.cellPixels, atlas.cellPixels]);
      }
    }
    atlas.dispose(); previousTarget?.dispose();
  },
);
