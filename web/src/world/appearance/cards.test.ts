import { describe, expect, it } from 'vitest';
import { Color, WebGLRenderTarget, type WebGLRenderer } from 'three';
import { BodyPlan } from './bodyPlan';
import { CardAtlas, CardBaker, CELLS } from './cards';

describe('card baking renderer isolation', () => {
  it.each([false, true])('restores the caller and uses target-local cells, failed bake=%s', fail => {
    const original = new WebGLRenderTarget(256, 128);
    let target: WebGLRenderTarget | null = original, face = 3, mip = 2, alpha = 0.7;
    const color = new Color('#123456'), originalColor = color.clone();
    const cells: number[][] = [];
    let bakeDisposed = false;
    const renderer = {
      autoClear: true,
      getRenderTarget: () => target,
      getActiveCubeFace: () => face,
      getActiveMipmapLevel: () => mip,
      getClearColor: (out: Color) => out.copy(color),
      getClearAlpha: () => alpha,
      setRenderTarget: (next: WebGLRenderTarget | null, nextFace = 0, nextMip = 0) => {
        if (next !== original && next !== target) next?.addEventListener('dispose', () => { bakeDisposed = true; });
        target = next; face = nextFace; mip = nextMip;
      },
      setClearColor: (next: Color, nextAlpha: number) => { color.copy(next); alpha = nextAlpha; },
      clear: () => { expect(target?.scissorTest).toBe(false); },
      render: () => {
        expect(target?.scissorTest).toBe(true);
        expect(target?.scissor).toEqual(target?.viewport);
        cells.push(target!.viewport.toArray());
        if (fail) throw new Error('render failed');
      },
      // Canvas setViewport/setScissor must never be called by an offscreen bake.
    };
    const baker = new CardBaker(renderer as unknown as WebGLRenderer), atlas = new CardAtlas('test');
    const plan = new BodyPlan(); plan.height = 1; plan.width = 1;
    baker.enqueue(atlas, plan);
    if (fail) expect(() => baker.step()).toThrow('render failed');
    else baker.step();
    expect(target).toBe(original);
    expect([face, mip, alpha, renderer.autoClear]).toEqual([3, 2, 0.7, true]);
    expect(color).toEqual(originalColor);
    expect(atlas.ready).toBe(!fail);
    expect(bakeDisposed).toBe(fail);
    const pixels = Math.ceil(1.08 * baker.pixelsPerUnit);
    expect(cells).toEqual(Array.from({ length: fail ? 1 : CELLS }, (_, i) => [i * pixels, 0, pixels, pixels]));
    atlas.dispose(); original.dispose();
  });
});
