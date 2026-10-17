import * as T from 'three';
import { buildGround } from '/runtime/terrainMesh';
import { chunkFromMessage, CellFlag } from '/runtime/chunks';
import { SurfacePalette } from '/runtime/palette';
import { toThree } from '/runtime/coords';

export function buildFixture(scene, baseConfig, config) {
  const paletteConfig = structuredClone(baseConfig.terrain);
  paletteConfig.base = [{ tags: ['grass'], color: '#5c8275' }, { tags: ['rock'], color: '#687d83' }];
  paletteConfig.layers = [{ tags: ['bridge'], color: '#d3cfad' }];
  const palette = new SurfacePalette(paletteConfig);
  const chunks = new Map(), size = 16;
  for (let cy = -1; cy <= 0; cy++) for (let cx = -1; cx <= 0; cx++) {
    const height = [], surface = [], flags = [], layers = [];
    for (let ly = 0; ly < size; ly++) for (let lx = 0; lx < size; lx++) {
      const x = cx * size + lx, y = cy * size + ly, i = ly * size + lx;
      const terrace = y >= 8 || (Math.abs(x) >= 7 && Math.abs(x) <= 10 && y >= -2 && y <= 2);
      height.push(terrace ? 3.2 : 0); surface.push(terrace ? 2 : 1);
      flags.push(CellFlag.walkable | CellFlag.flattened);
      if (x >= -7 && x < 7 && y >= -1 && y < 2) layers.push({ index: i, height: 3.2, walkable: true, tags: 'bridge' });
    }
    const chunk = chunkFromMessage({ x: cx, y: cy, size, height, surface_tag: surface, flags: btoa(String.fromCharCode(...flags)), tags: ['', 'grass', 'rock'], layers, gen_version: 1 });
    chunks.set(`${cx},${cy}`, chunk);
  }
  const groundMaterial = new T.MeshLambertMaterial({ vertexColors: true, side: T.DoubleSide });
  for (const chunk of chunks.values()) {
    const data = buildGround(chunk, (x, y) => chunks.get(`${x},${y}`), palette);
    const g = new T.BufferGeometry();
    g.setAttribute('position', new T.BufferAttribute(data.positions, 3)); g.setAttribute('normal', new T.BufferAttribute(data.normals, 3)); g.setAttribute('color', new T.BufferAttribute(data.colors, 3)); g.setIndex(new T.BufferAttribute(data.indices, 1));
    const m = new T.Mesh(g, groundMaterial); toThree(chunk.x * size, chunk.y * size, 0, m.position); scene.add(m);
  }
  const materials = new Map();
  const mat = color => { if (!materials.has(color)) materials.set(color, new T.MeshLambertMaterial({ color, flatShading: true })); return materials.get(color); };
  const prop = (g, color, x, y, z, parent = scene) => { const m = new T.Mesh(g, mat(color)); toThree(x, y, z, m.position); parent.add(m); return m; };
  // Structural deck thickness and supports complement the existing mesher's top surfaces.
  prop(new T.BoxGeometry(14, .28, 3), '#7f8c82', 0, .5, 3.05);
  for (const x of [-6, 6]) for (const y of [-.8, 1.8]) {
    prop(new T.CylinderGeometry(.3, .5, 3, 7), '#9eaa96', x, y, 1.5);
    prop(new T.BoxGeometry(.64, .25, .64), '#d7d2b6', x, y, 3.4);
    prop(new T.OctahedronGeometry(.23), '#b5e8cb', x, y, 3.8);
  }
  for (const x of [-4, 4]) {
    prop(new T.CylinderGeometry(.4, .55, 4.5, 8), '#b4beb0', x, 10, 5.45);
    prop(new T.BoxGeometry(1.15, .35, 1.15), '#d9d3b6', x, 10, 7.8);
  }
  const arch = prop(new T.TorusGeometry(4, .38, 6, 20, Math.PI), '#c2c8ac', 0, 10, 7.8); arch.rotation.z = 0;
  const rune = prop(new T.TorusGeometry(1.05, .04, 4, 32), '#aee9d3', 0, 10, 8.5); rune.rotation.z = Math.PI / 4;
  prop(new T.OctahedronGeometry(.65), '#d1eccb', 0, 10, 8.5);
  for (let j = 0; j < 13; j++) {
    const x = Math.sin(j * 7.2) * 13, y = -10 + (j % 3) * 1.3;
    const rock = prop(new T.DodecahedronGeometry(.45 + (j % 4) * .15), '#8b9f8e', x, y, .35); rock.scale.set(1.4, .6, 1);
  }
  const canopy = new T.Group(); scene.add(canopy);
  for (const [x, y, h, tint] of [[-10, 6, 6, '#5b8f81'], [11, 7, 7, '#688988'], [-11, -5, 5.8, '#76829a'], [10, -6, 5.5, '#648a77']]) {
    prop(new T.CylinderGeometry(.18, .4, h, 6), '#5e7069', x, y, h / 2, canopy);
    for (let k = 0; k < 4; k++) {
      const crown = prop(new T.IcosahedronGeometry(2, 1), tint, x + Math.cos(k * 2) * .9, y + Math.sin(k * 2) * .8, h + (k % 2) * .7, canopy); crown.scale.set(1.25, .7, 1);
    }
  }
  const effects = new T.Group(); scene.add(effects);
  const ring = (x, y, z, color, radius) => { const r = new T.Mesh(new T.RingGeometry(radius - .065, radius, 48), new T.MeshBasicMaterial({ color, side: T.DoubleSide })); toThree(x, y, z, r.position); r.rotation.x = -Math.PI / 2; effects.add(r); return r; };
  ring(1, .5, 3.22, '#f0d391', 1.7); ring(1, .5, .025, '#f69a84', 2.5);
  // Radial ticks make the warning distinct without relying only on color.
  for (let i = 0; i < 8; i++) {
    const angle = i * Math.PI / 4;
    const tick = new T.Mesh(new T.BoxGeometry(.08, .02, .4), new T.MeshBasicMaterial({ color: '#f69a84' })); toThree(1 + Math.sin(angle) * 2.25, .5 + Math.cos(angle) * 2.25, .03, tick.position); tick.rotation.y = -angle; effects.add(tick);
  }
  const motes = new T.InstancedMesh(new T.OctahedronGeometry(.045), new T.MeshBasicMaterial({ color: '#d4e8b5' }), 72); motes.frustumCulled = false; effects.add(motes);
  const shadows = new T.InstancedMesh(new T.CircleGeometry(.36, 12).rotateX(-Math.PI / 2), new T.MeshBasicMaterial({ color: '#334e4c', side: T.DoubleSide }), config.maxActors); shadows.frustumCulled = false; scene.add(shadows);
  const temp = new T.Object3D();
  return { chunks, canopy, effects, shadows, update(time, poses, tier) {
    shadows.visible = tier.contactShadows; shadows.count = poses.length;
    if (shadows.visible) {
      for (let i = 0; i < poses.length; i++) { const p = poses[i]; toThree(p.x, p.y, p.z + .015, temp.position); temp.rotation.set(0, 0, 0); temp.updateMatrix(); shadows.setMatrixAt(i, temp.matrix); }
      shadows.instanceMatrix.needsUpdate = true;
    }
    motes.count = tier.decorativeMotes;
    if (effects.visible) {
      for (let i = 0; i < motes.count; i++) { temp.position.set(Math.sin(i * 13.1) * 12, 1 + (i % 9) * .6 + Math.sin(time + i) * .25, Math.cos(i * 4.7) * 12); temp.rotation.set(time, i, 0); temp.updateMatrix(); motes.setMatrixAt(i, temp.matrix); }
      motes.instanceMatrix.needsUpdate = true;
    }
  } };
}

// Deterministic presentation fixture, NOT a replacement simulation or network prediction.
export function samplePoses(count, time, out = []) {
  out.length = count;
  for (let i = 0; i < count; i++) {
    const p = out[i] ??= { x: 0, y: 0, z: 0, yaw: 0 };
    if (i === 0) { p.x = -3; p.y = -4; p.z = 0; p.yaw = .6; continue; }
    if (i % 6 === 1) { p.x = Math.sin(time * .28 + i) * 5.5; p.y = .5 + Math.sin(i * 5) * .7; p.z = 3.2; p.yaw = Math.cos(time * .28 + i) >= 0 ? 0 : Math.PI; }
    else { p.x = Math.sin(i * 12.9898) * 6.5; p.y = -6 + ((i * 7.17) % 12); p.z = 0; p.yaw = time * .12 + i * 1.7; }
  }
  return out;
}
