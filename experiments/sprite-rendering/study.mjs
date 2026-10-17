import * as T from 'three';
import { CameraRig } from '/runtime/camera';
import { actorGeometry, bakeActorAtlases, makeActorViews } from './actors.mjs';
import { buildFixture, samplePoses } from './fixture.mjs';

const $ = id => document.getElementById(id);
const [config, runtime] = await Promise.all(['./study-config.json', '/runtime-config.json'].map(url => fetch(url).then(r => r.json())));
const state = { mode: 'split', camera: 'isometric', quality: 'balanced', count: config.defaultActors, gear: 'staff', time: 0, paused: false };
const stage = $('stage');
const renderer = new T.WebGLRenderer({ antialias: true });
renderer.setClearColor('#72958f'); renderer.info.autoReset = false; renderer.outputColorSpace = T.SRGBColorSpace;
stage.prepend(renderer.domElement);
const scene = new T.Scene(); scene.fog = new T.Fog('#72958f', 135, 240);
scene.add(new T.HemisphereLight('#cde3da', '#46555d', 2));
const sun = new T.DirectionalLight('#fff1cc', 2.2); sun.position.set(8, 12, 6); scene.add(sun);
const rig = new CameraRig({ ...runtime.camera, isoSize: 28, thirdDistance: 9 });
const fixture = buildFixture(scene, runtime, config);
const geometries = Object.fromEntries(['staff', 'crown'].map(gear => [gear, Array.from({ length: config.atlas.frames }, (_, frame) => actorGeometry(frame, gear))]));
const startBake = performance.now();
const atlases = await bakeActorAtlases(config, geometries, message => $('loading').textContent = message);
const bakeMs = performance.now() - startBake;
const actors = makeActorViews(config, geometries, atlases); scene.add(actors.sprite, actors.mesh);
const poses = [];
const gl = renderer.getContext(), debug = gl.getExtension('WEBGL_debug_renderer_info');
const environment = {
  userAgent: navigator.userAgent,
  renderer: debug ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER),
  vendor: debug ? gl.getParameter(debug.UNMASKED_VENDOR_WEBGL) : gl.getParameter(gl.VENDOR),
  threeRevision: T.REVISION,
  maxTextureSize: gl.getParameter(gl.MAX_TEXTURE_SIZE),
  gpuTimerAvailable: Boolean(gl.getExtension('EXT_disjoint_timer_query_webgl2')),
};
let width = 1, height = 1, lastFrame = 0, lastUI = 0, measurement = null, lastResult = null, latest = null, actorStats = null;
let pendingResize = true;
new ResizeObserver(() => pendingResize = true).observe(stage);
const resize = () => {
  width = stage.clientWidth; height = stage.clientHeight;
  const tier = config.tiers[state.quality];
  renderer.setPixelRatio(Math.min(devicePixelRatio, tier.maxPixelRatio) * tier.resolutionScale);
  renderer.setSize(width, height, false); rig.resize(state.mode === 'split' ? width / 2 : width, height); pendingResize = false;
};
const quantile = (values, q) => { if (!values.length) return null; const sorted = [...values].sort((a, b) => a - b); return sorted[Math.min(sorted.length - 1, Math.floor((sorted.length - 1) * q))]; };
const summarize = values => ({ median: quantile(values, .5), p95: quantile(values, .95), max: values.length ? Math.max(...values) : null });
function controlsLocked(locked) { for (const el of document.querySelectorAll('aside button,aside select,aside input')) el.disabled = locked; $('export').disabled = locked || !lastResult; $('pitch').disabled = locked || state.camera !== 'third_person'; }
function measure({ warmupMs = 1000, durationMs = 5000 } = {}) {
  if (measurement) return Promise.reject(new Error('A measurement is already active'));
  if (!(durationMs >= 100 && durationMs <= 120000 && warmupMs >= 0 && warmupMs <= 60000)) return Promise.reject(new Error('Invalid measurement duration'));
  controlsLocked(true);
  return new Promise(resolve => { measurement = { start: performance.now(), warmupMs, durationMs, raf: [], cpu: [], calls: [], triangles: [], hidden: document.hidden, resolve }; });
}
function finishMeasurement() {
  const m = measurement;
  lastResult = {
    recordedAt: new Date().toISOString(), environment, state: { ...state, time: undefined },
    viewport: { width, height, devicePixelRatio, renderPixelRatio: renderer.getPixelRatio(), drawingWidth: renderer.domElement.width, drawingHeight: renderer.domElement.height },
    warmupMs: m.warmupMs, requestedDurationMs: m.durationMs, samples: m.cpu.length, hiddenDuringRun: m.hidden,
    observed: { rafIntervalMs: summarize(m.raf), cpuUpdateAndSubmissionMs: summarize(m.cpu), drawCalls: summarize(m.calls), submittedTriangles: summarize(m.triangles), resourceCounts: { ...renderer.info.memory }, bakeMs },
    estimates: { atlasBaseBytesPerOutfit: atlases.staff.baseBytes, atlasMipBytesPerOutfit: Math.round(atlases.staff.baseBytes * 4 / 3), outfitAtlases: 2 },
    unmeasured: ['GPU elapsed time', 'GPU bytes resident', 'power', 'network and gameplay', 'Godot parity', 'minimum hardware', 'production artwork and skinning'],
    notes: ['Split view draws the world twice; compare single modes for timing.', 'RAF includes browser scheduling and display pacing, not pure GPU work.', 'CPU timing includes state sampling, camera, uploads, and render submission; GPU is asynchronous.', 'Both alternatives are instanced. Mesh uses four static animation-pose buckets; no skeleton or skinning.', 'The full atlas remains allocated when Low selects four directions.'],
  };
  measurement = null; controlsLocked(false); m.resolve(lastResult);
}
function render(now) {
  requestAnimationFrame(render);
  const interval = lastFrame ? now - lastFrame : 0; lastFrame = now;
  const begin = performance.now();
  if (pendingResize) resize();
  if (!state.paused) state.time += Math.min(interval, 100) / 1000;
  samplePoses(state.count, state.time, poses);
  const hero = poses[0];
  rig.follow(state.camera === 'third_person' ? new T.Vector3(hero.x, hero.z, -hero.y) : new T.Vector3(0, 1.7, -1));
  rig.update(0, true);
  const tier = config.tiers[state.quality];
  fixture.canopy.visible = $('canopy').checked; fixture.effects.visible = $('effects').checked;
  fixture.update(state.time, poses, tier);
  actorStats = actors.update(poses, rig.camera, state.gear, tier.spriteDirections, state.mode !== 'mesh', state.mode !== 'sprite', state.time);
  renderer.info.reset(); renderer.setScissorTest(true);
  if (state.mode === 'split') {
    actors.sprite.visible = true; actors.mesh.visible = false;
    renderer.setViewport(0, 0, width / 2, height); renderer.setScissor(0, 0, width / 2, height); renderer.render(scene, rig.camera);
    actors.sprite.visible = false; actors.mesh.visible = true;
    renderer.setViewport(width / 2, 0, width / 2, height); renderer.setScissor(width / 2, 0, width / 2, height); renderer.render(scene, rig.camera);
  } else {
    renderer.setViewport(0, 0, width, height); renderer.setScissor(0, 0, width, height); renderer.render(scene, rig.camera);
  }
  const cpu = performance.now() - begin;
  latest = { cpuMs: cpu, intervalMs: interval, calls: renderer.info.render.calls, triangles: renderer.info.render.triangles, actorStats, count: poses.length };
  if (measurement) {
    const elapsed = now - measurement.start;
    measurement.hidden ||= document.hidden;
    if (elapsed >= measurement.warmupMs) { measurement.raf.push(interval); measurement.cpu.push(cpu); measurement.calls.push(latest.calls); measurement.triangles.push(latest.triangles); }
    if (elapsed >= measurement.warmupMs + measurement.durationMs) finishMeasurement();
  }
  if (now - lastUI > 250) {
    lastUI = now;
    $('metrics').textContent = `${state.count} actors · ${latest.calls} calls · ${latest.triangles.toLocaleString()} triangles · ${cpu.toFixed(2)} ms CPU submission\nSprite view: ${tier.spriteDirections} directions / ${actorStats.sampledPitch.toFixed(1)}° bake · 8 MiB estimated GPU atlas per outfit`;
    $('status').textContent = measurement ? 'Measuring… keep this view visible and unchanged.' : lastResult ? `Last run: ${lastResult.samples} frames · CPU p95 ${lastResult.observed.cpuUpdateAndSubmissionMs.p95.toFixed(2)} ms · RAF p95 ${lastResult.observed.rafIntervalMs.p95.toFixed(2)} ms` : 'Ready · Drag or use the controls to inspect the crossing.';
  }
}
function setMode(mode) {
  state.mode = mode; pendingResize = true;
  for (const b of document.querySelectorAll('[data-mode]')) b.classList.toggle('active', b.dataset.mode === mode);
  $('tagLeft').style.display = mode === 'mesh' ? 'none' : 'block';
  $('tagRight').style.display = mode === 'sprite' ? 'none' : 'block';
  $('tagRight').style.left = mode === 'mesh' ? '20px' : 'calc(50% + 20px)';
  $('divider').style.display = mode === 'split' ? 'block' : 'none';
}
function setCamera(mode) { state.camera = mode; rig.mode = mode; $('camera').value = mode; $('pitch').disabled = mode !== 'third_person'; }
for (const b of document.querySelectorAll('[data-mode]')) b.onclick = () => setMode(b.dataset.mode);
$('camera').onchange = () => setCamera($('camera').value);
$('left').onclick = () => rig.rotateStep(-1); $('right').onclick = () => rig.rotateStep(1);
let lastPitch = 28;
$('pitch').oninput = () => { const value = Number($('pitch').value); rig.orbit(0, (value - lastPitch) * Math.PI / 180 / runtime.camera.pitchSensitivity); lastPitch = value; $('pitchValue').textContent = `${value}°`; };
$('count').oninput = () => { state.count = Number($('count').value); $('countValue').textContent = state.count; };
$('quality').onchange = () => { state.quality = $('quality').value; pendingResize = true; };
$('gear').onchange = () => state.gear = $('gear').value;
$('pause').onchange = () => state.paused = $('pause').checked;
$('benchmark').onclick = () => measure();
$('export').onclick = () => {
  const url = URL.createObjectURL(new Blob([JSON.stringify(lastResult, null, 2)], { type: 'application/json' }));
  const link = document.createElement('a'); link.href = url; link.download = `hollow-wake-rendering-${state.mode}.json`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 500);
};
renderer.domElement.addEventListener('contextmenu', e => e.preventDefault());
renderer.domElement.addEventListener('pointerdown', e => { if (!measurement) renderer.domElement.setPointerCapture(e.pointerId); });
renderer.domElement.addEventListener('pointermove', e => {
  if (!measurement && e.buttons && state.camera === 'third_person') {
    rig.orbit(e.movementX, 0);
    const next = Math.max(5, Math.min(80, lastPitch + e.movementY * runtime.camera.pitchSensitivity * 180 / Math.PI));
    $('pitch').value = next; $('pitch').oninput();
  }
});
renderer.domElement.addEventListener('wheel', e => { e.preventDefault(); if (!measurement) rig.zoom(-e.deltaY / runtime.camera.wheelStepPixels); }, { passive: false });
document.addEventListener('keydown', e => {
  if (measurement || /INPUT|SELECT|BUTTON/.test(e.target.tagName)) return;
  if (e.code === 'KeyQ') rig.rotateStep(-1); if (e.code === 'KeyE') rig.rotateStep(1);
  if (e.code === 'KeyV') setCamera(state.camera === 'isometric' ? 'third_person' : 'isometric');
});
// Inspection and benchmark seam. This is local fixture state, never a game client API.
window.spriteStudy = {
  ready: true, measure, setMode, setCamera,
  setTime(time) { if (measurement) throw new Error('Measurement active'); state.paused = true; state.time = time; $('pause').checked = true; },
  snapshot: () => ({ state: { ...state }, latest, environment, atlas: { width: atlases.staff.canvas.width, height: atlases.staff.canvas.height }, poses: poses.slice(0, 8).map(p => ({ ...p })), layerCount: [...fixture.chunks.values()].reduce((n, c) => n + c.layers.size, 0) }),
  atlasData: gear => atlases[gear].canvas.toDataURL(),
};
$('pitch').disabled = true;
$('loading').remove(); requestAnimationFrame(render);
