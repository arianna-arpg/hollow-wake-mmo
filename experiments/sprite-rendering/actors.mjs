import * as T from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

// Original procedural proxy art. No game asset or reference-game pixels are used.
export function actorGeometry(frame = 0, gear = 'staff') {
  const parts = [];
  const add = (geometry, color, x, y, z, rx = 0, ry = 0, rz = 0) => {
    let g = geometry.index ? geometry.toNonIndexed() : geometry;
    if (g !== geometry) geometry.dispose();
    g.deleteAttribute('uv');
    const c = new T.Color(color), n = g.attributes.position.count;
    g.setAttribute('color', new T.Float32BufferAttribute(Array.from({ length: n * 3 }, (_, i) => [c.r, c.g, c.b][i % 3]), 3));
    g.applyMatrix4(new T.Matrix4().compose(new T.Vector3(x, y, z), new T.Quaternion().setFromEuler(new T.Euler(rx, ry, rz)), new T.Vector3(1, 1, 1)));
    parts.push(g);
  };
  const phase = Math.sin(frame * Math.PI / 2), step = phase * .14;
  add(new T.BoxGeometry(.18, .48, .27), '#393448', -.18, .27, step);
  add(new T.BoxGeometry(.18, .48, .27), '#393448', .18, .27, -step);
  add(new T.CylinderGeometry(.27, .47, .9, 7), '#476f70', 0, .9, 0);
  add(new T.CylinderGeometry(.37, .34, .55, 7), '#d7d6b3', 0, 1.42, 0);
  add(new T.ConeGeometry(.5, .85, 7, 1, true), '#768a77', 0, 1.08, -.18, -.12);
  add(new T.BoxGeometry(.15, .96, .035), '#c5a86a', 0, 1.03, .33);
  add(new T.CylinderGeometry(.39, .41, .1, 7), '#d7bb7a', 0, 1.18, 0);
  add(new T.SphereGeometry(.24, 8, 5), '#dbbfa4', 0, 1.86, .07);
  add(new T.SphereGeometry(.3, 7, 5, 0, Math.PI * 2, 0, Math.PI * .66), '#414958', 0, 1.9, -.03);
  add(new T.ConeGeometry(.29, .4, 7), '#62716d', 0, 2.12, -.08, -.3);
  for (const side of [-1, 1]) {
    add(new T.SphereGeometry(.24, 6, 4), '#d1c797', side * .36, 1.56, 0);
    add(new T.CylinderGeometry(.12, .1, .47, 6), '#54787a', side * .43, 1.25, .05, side * phase * .2, 0, side * .15);
    add(new T.SphereGeometry(.105, 6, 4), '#dbc6a6', side * .48, 1.04, .09);
  }
  if (gear === 'staff') {
    add(new T.CylinderGeometry(.033, .05, 1.85, 6), '#a08965', .58, 1.02, .09);
    add(new T.TorusGeometry(.14, .03, 5, 8), '#e9ce83', .58, 1.98, .09);
    add(new T.OctahedronGeometry(.11), '#9af4d1', .58, 1.98, .09);
  } else {
    add(new T.BoxGeometry(.16, 1.2, .055), '#d6e1db', .6, 1.18, .08, 0, 0, -.15);
    add(new T.BoxGeometry(.45, .07, .12), '#c8a561', .52, .66, .08);
    for (const side of [-1, 1]) {
      add(new T.ConeGeometry(.055, .6, 4), '#d7b66c', side * .28, 2.22, 0, 0, 0, side * -.48);
      add(new T.ConeGeometry(.04, .28, 4), '#d7b66c', side * .44, 2.26, 0, 0, 0, side * -.9);
    }
  }
  const merged = mergeGeometries(parts);
  for (const p of parts) p.dispose();
  return merged;
}

export async function bakeActorAtlases(config, geometries, onProgress) {
  const a = config.atlas, columns = a.directions, rows = a.frames * a.pitchDegrees.length;
  const renderer = new T.WebGLRenderer({ alpha: true, antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(a.cellPixels, a.cellPixels);
  renderer.setClearColor(0x000000, 0);
  const scene = new T.Scene();
  scene.add(new T.HemisphereLight('#cde3da', '#46555d', 2));
  const sun = new T.DirectionalLight('#fff1cc', 2.2); sun.position.set(8, 12, 6); scene.add(sun);
  const material = new T.MeshLambertMaterial({ vertexColors: true, flatShading: true });
  const body = new T.Mesh(geometries.staff[0], material); scene.add(body);
  const camera = new T.OrthographicCamera(-a.worldSize / 2, a.worldSize / 2, a.bottom + a.worldSize, a.bottom, .1, 40);
  const result = {};
  try {
    for (const gear of Object.keys(geometries)) {
      const canvas = document.createElement('canvas'); canvas.width = columns * a.cellPixels; canvas.height = rows * a.cellPixels;
      const ctx = canvas.getContext('2d');
      for (let p = 0; p < a.pitchDegrees.length; p++) {
        const pitch = a.pitchDegrees[p] * Math.PI / 180;
        for (let f = 0; f < a.frames; f++) {
          body.geometry = geometries[gear][f];
          for (let d = 0; d < columns; d++) {
            const yaw = d * Math.PI * 2 / columns;
            camera.position.set(Math.sin(yaw) * Math.cos(pitch) * 15, Math.sin(pitch) * 15, Math.cos(yaw) * Math.cos(pitch) * 15);
            camera.lookAt(0, 0, 0); renderer.render(scene, camera);
            ctx.drawImage(renderer.domElement, d * a.cellPixels, (p * a.frames + f) * a.cellPixels);
          }
        }
        onProgress?.(`Baking ${gear}, elevation ${p + 1}/${a.pitchDegrees.length}…`);
        await new Promise(requestAnimationFrame);
      }
      const texture = new T.CanvasTexture(canvas); texture.colorSpace = T.SRGBColorSpace;
      texture.minFilter = T.LinearMipmapLinearFilter; texture.magFilter = T.LinearFilter;
      result[gear] = { texture, canvas, baseBytes: canvas.width * canvas.height * 4 };
    }
  } finally { material.dispose(); renderer.dispose(); renderer.forceContextLoss(); }
  return result;
}

export function makeActorViews(config, geometries, atlases) {
  const sprite = new T.Group(), mesh = new T.Group();
  const a = config.atlas, rows = a.frames * a.pitchDegrees.length;
  const views = {};
  for (const gear of Object.keys(geometries)) {
    const plane = new T.PlaneGeometry(a.worldSize, a.worldSize); plane.translate(0, a.bottom + a.worldSize / 2, 0);
    const cells = new T.InstancedBufferAttribute(new Float32Array(config.maxActors * 2), 2).setUsage(T.DynamicDrawUsage);
    plane.setAttribute('atlasCell', cells);
    const material = new T.MeshBasicMaterial({ map: atlases[gear].texture, alphaTest: .5, transparent: false, depthWrite: true, side: T.DoubleSide });
    material.onBeforeCompile = shader => {
      shader.vertexShader = 'attribute vec2 atlasCell;\n' + shader.vertexShader;
      shader.vertexShader = shader.vertexShader.replace('#include <uv_vertex>', `#include <uv_vertex>\n vMapUv = (vMapUv + atlasCell) / vec2(${a.directions}.0, ${rows}.0);`);
    };
    const cards = new T.InstancedMesh(plane, material, config.maxActors); cards.frustumCulled = false; cards.count = 0; cards.instanceMatrix.setUsage(T.DynamicDrawUsage); sprite.add(cards);
    const solidMaterial = new T.MeshLambertMaterial({ vertexColors: true, flatShading: true });
    const solids = geometries[gear].map(g => {
      const m = new T.InstancedMesh(g, solidMaterial, config.maxActors); m.count = 0; m.frustumCulled = false; m.instanceMatrix.setUsage(T.DynamicDrawUsage); mesh.add(m); return m;
    });
    views[gear] = { cards, cells, solids };
  }
  const temp = new T.Object3D();
  const update = (poses, camera, gear, directionCount, showSprite, showMesh, time) => {
    sprite.visible = showSprite; mesh.visible = showMesh;
    for (const [key, view] of Object.entries(views)) {
      view.cards.visible = key === gear;
      for (const solid of view.solids) solid.visible = key === gear;
    }
    const view = views[gear], counts = new Array(a.frames).fill(0);
    const worldDirection = new T.Vector3(); camera.getWorldDirection(worldDirection);
    const nominalPitch = Math.asin(-worldDirection.y) * 180 / Math.PI;
    const pitchIndex = a.pitchDegrees.reduce((best, p, i) => Math.abs(p - nominalPitch) < Math.abs(a.pitchDegrees[best] - nominalPitch) ? i : best, 0);
    for (let i = 0; i < poses.length; i++) {
      const p = poses[i], frame = Math.floor(time * a.frameRate + i % a.frames) % a.frames;
      temp.position.set(p.x, p.z, -p.y); temp.scale.setScalar(1);
      const rotation = Math.PI / 2 - p.yaw;
      if (showSprite) {
        // Orthographic rays are parallel. Perspective actors each see a different azimuth.
        const bearing = camera.isOrthographicCamera ? Math.atan2(-worldDirection.x, -worldDirection.z) : Math.atan2(camera.position.x - p.x, camera.position.z + p.y);
        const relative = bearing - rotation;
        const bucket = ((Math.round(relative / (Math.PI * 2) * directionCount) % directionCount) + directionCount) % directionCount;
        const direction = bucket * a.directions / directionCount;
        view.cells.setXY(i, direction, rows - 1 - (pitchIndex * a.frames + frame));
        temp.quaternion.copy(camera.quaternion); temp.updateMatrix(); view.cards.setMatrixAt(i, temp.matrix);
      }
      if (showMesh) {
        temp.rotation.set(0, rotation, 0); temp.updateMatrix(); view.solids[frame].setMatrixAt(counts[frame]++, temp.matrix);
      }
    }
    view.cards.count = poses.length;
    if (showSprite) { view.cells.needsUpdate = true; view.cards.instanceMatrix.needsUpdate = true; }
    if (showMesh) for (let f = 0; f < a.frames; f++) { view.solids[f].count = counts[f]; view.solids[f].instanceMatrix.needsUpdate = true; }
    return { pitchIndex, sampledPitch: a.pitchDegrees[pitchIndex], spriteTriangles: poses.length * 2, meshTriangles: counts.reduce((sum, count, f) => sum + count * geometries[gear][f].attributes.position.count / 3, 0) };
  };
  return { sprite, mesh, update, views };
}
