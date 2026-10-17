import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { createStudyServer } from './serve.mjs';

const root = dirname(fileURLToPath(import.meta.url)), repo = resolve(root, '../..');
const require = createRequire(resolve(repo, 'web/package.json'));
const { chromium } = require('playwright-core');
const output = resolve(root, 'evidence'); await mkdir(output, { recursive: true });
const server = await createStudyServer(0);
const errors = [], checks = [], results = [];
const browser = await chromium.launch({ headless: true, channel: 'msedge', args: ['--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 1 });
page.on('pageerror', e => errors.push(e.message));
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const check = (name, pass, detail) => { checks.push({ name, pass, detail }); if (!pass) throw new Error(`${name}: ${detail}`); };
const tick = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
try {
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  await page.waitForFunction(() => window.spriteStudy?.ready, { timeout: 60000 });
  await tick(); await page.evaluate(() => window.spriteStudy.setTime(0)); await tick();
  // Let the status panel refresh beyond the initial shader-compilation frame.
  await page.waitForTimeout(400);
  const initial = await page.evaluate(() => window.spriteStudy.snapshot());
  check('Existing terrain mesher includes elevated deck', initial.layerCount === 42, initial.layerCount);
  check('Fixture draws actors at both elevations', initial.poses.some(p => p.z === 0) && initial.poses.some(p => p.z === 3.2), initial.poses);
  check('Atlas is eight directions, four frames, three pitches', initial.atlas.width === 1024 && initial.atlas.height === 1536, initial.atlas);
  check('Split mode submits actual geometry', initial.latest.triangles > 5000, initial.latest);
  await page.screenshot({ path: resolve(output, 'comparison-isometric.png') });
  await page.getByLabel('Equipment silhouette').selectOption('crown'); await tick();
  await page.screenshot({ path: resolve(output, 'comparison-equipment.png') });
  await page.locator('#camera').selectOption('third_person'); await tick();
  await page.screenshot({ path: resolve(output, 'comparison-third-person.png') });
  await page.locator('#pitch').fill('80'); await page.locator('#pitch').dispatchEvent('input'); await tick();
  const high = await page.evaluate(() => window.spriteStudy.snapshot());
  check('Extreme pitch uses nearest documented bake', high.latest.actorStats.sampledPitch === 65, high.latest.actorStats);
  await page.screenshot({ path: resolve(output, 'comparison-high-pitch.png') });
  await page.locator('#quality').selectOption('low'); await tick();
  await page.screenshot({ path: resolve(output, 'comparison-low.png') });
  await page.getByLabel('Foreground canopy').uncheck(); await page.getByLabel('Ground warnings & motes').uncheck(); await tick();
  check('No rendering errors after visual controls', errors.length === 0, errors);
  for (const gear of ['staff', 'crown']) {
    const data = await page.evaluate(gear => window.spriteStudy.atlasData(gear), gear);
    await writeFile(resolve(output, `atlas-${gear}.png`), Buffer.from(data.split(',')[1], 'base64'));
  }
  await page.locator('#camera').selectOption('isometric');
  await page.locator('#quality').selectOption('balanced');
  await page.getByLabel('Equipment silhouette').selectOption('staff');
  await page.getByLabel('Foreground canopy').check(); await page.getByLabel('Ground warnings & motes').check();
  await page.getByLabel('Freeze the scene').uncheck();
  const full = process.argv.includes('--benchmark');
  for (const count of (full ? [48, 256, 512] : [48])) {
    await page.locator('#count').fill(String(count)); await page.locator('#count').dispatchEvent('input');
    for (let repetition = 0; repetition < (full ? 3 : 1); repetition++) {
      // Reverse order on alternate repetitions to reduce a persistent warm-order bias.
      for (const mode of (repetition % 2 ? ['mesh', 'sprite'] : ['sprite', 'mesh'])) {
        await page.locator(`[data-mode="${mode}"]`).click();
        await page.evaluate(() => { window.spriteStudy.setTime(0); });
        await page.getByLabel('Freeze the scene').uncheck();
        const result = await page.evaluate(full => window.spriteStudy.measure({ warmupMs: full ? 1000 : 300, durationMs: full ? 2000 : 700 }), full);
        results.push({ repetition, ...result });
        check(`${mode} ${count} repetition ${repetition} sampled visible frames`, result.samples > 5 && !result.hiddenDuringRun, result.samples);
        console.log(`${mode} ${count} run ${repetition + 1}: CPU p95 ${result.observed.cpuUpdateAndSubmissionMs.p95.toFixed(2)} ms, RAF p95 ${result.observed.rafIntervalMs.p95.toFixed(2)} ms, ${result.observed.drawCalls.median} calls`);
      }
    }
  }
  check('No browser or shader errors', errors.length === 0, errors);
  const hashes = {};
  for (const path of ['web/src/world/terrainMesh.ts', 'web/src/world/chunks.ts', 'web/src/world/palette.ts', 'web/src/world/coords.ts', 'web/src/view/camera.ts', 'web/public/config.json', 'web/package-lock.json', ...['study.mjs', 'actors.mjs', 'fixture.mjs', 'study-config.json', 'serve.mjs', 'validate.mjs', 'index.html', 'study.css'].map(name => `experiments/sprite-rendering/${name}`)]) hashes[path] = createHash('sha256').update(await readFile(resolve(repo, path))).digest('hex');
  await writeFile(resolve(output, 'results.json'), JSON.stringify({ context: 'Headless Edge, isolated local fixture. No hardware recommendation can be inferred.', recordedAt: new Date().toISOString(), sourceHashes: hashes, checks, errors, results }, null, 2));
  console.log(`PASS ${checks.length} checks; evidence: ${output}`);
} finally {
  await browser.close(); await new Promise(resolve => server.close(resolve));
  if (errors.length) console.error(errors.join('\n'));
}
