// Built browser client, real server, ordinary quick-play events. Delay only network work in this test.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const { chromium } = createRequire(path.join(root, 'web/package.json'))('playwright-core');
const config = JSON.parse(readFileSync(path.join(root, 'config/playtest.json'), 'utf8'));
const out = path.join(root, 'experiments/launcher-quick-play/output/combined-browser');
mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ channel: process.platform === 'win32' ? 'msedge' : 'chrome', args: ['--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errors = [], results = {};
page.on('pageerror', error => errors.push(error.message));
let holdNext = false, hold = false, pending = [], selections = 0, movement = 0;
await page.routeWebSocket('**/ws', socket => {
  const server = socket.connectToServer();
  socket.onMessage(bytes => {
    const message = JSON.parse(bytes.toString());
    if (message.t === 'select_character') { selections++; if (holdNext) { holdNext = false; hold = true; } }
    if (message.t === 'move_dir') movement++;
    server.send(bytes);
  });
  server.onMessage(bytes => { if (hold) pending.push({ socket, bytes }); else socket.send(bytes); });
});
const release = () => {
  hold = false;
  for (const { socket, bytes } of pending.splice(0)) { try { socket.send(bytes); } catch {} }
};
const ready = () => page.waitForFunction(() => window.hollowWake?.inWorld && window.hollowWake.self() && !window.hollowWake.loading.active);
const waiting = () => page.waitForFunction(() => window.hollowWake?.counts.characterLists > 0 && window.hollowWake.loading.active && !window.hollowWake.inWorld);
try {
  let releaseBoot;
  const bootWork = new Promise(resolve => { releaseBoot = resolve; });
  await page.route('**/assets/app-*.js', async route => { await bootWork; await route.continue(); });
  const account = `crossing-${Date.now()}`;
  const url = `http://localhost:${config.webPort}/?play=1&account=${account}&profile=combined-startup&fresh-settings`;
  await page.goto(url, { waitUntil: 'commit' });
  await page.locator('#mu-loading-screen canvas').waitFor();
  await page.screenshot({ path: path.join(out, '01-boot.png') });
  releaseBoot();
  await page.waitForFunction(() => window.hollowWake?.counts.characterLists > 0 && !window.hollowWake.loading.active);
  await page.unroute('**/assets/app-*.js');
  assert.equal(await page.locator('#login').isVisible(), true);
  assert.equal(selections, 0, 'a new account must stop for character creation');
  results.boot = 'Crossing appears while the client module loads; quick login then waits for first-time creation';

  holdNext = true;
  await page.locator('#login-new-name').fill('Crossing');
  await page.locator('#login-create').click();
  await waiting();
  assert.equal(selections, 1, 'one automatic character selection');
  const beforeMovement = movement;
  const lane = await page.evaluate(() => window.hollowWake.loading.lane);
  await page.keyboard.down('KeyD');
  await page.waitForTimeout(300);
  assert.notEqual(await page.evaluate(() => window.hollowWake.loading.lane), lane, 'the wisp responds during delayed entry');
  assert.equal(movement, beforeMovement, 'steering the wisp sends no world movement');
  await page.screenshot({ path: path.join(out, '02-entry.png') });
  release();
  await ready();
  await page.waitForTimeout(400);
  assert.equal(movement, beforeMovement, 'a key held through completion cannot move the hero');
  await page.keyboard.up('KeyD');
  const position = await page.evaluate(() => { const s = window.hollowWake.self(); return [s.x, s.y]; });
  await page.keyboard.down('KeyD');
  await page.waitForTimeout(750);
  await page.keyboard.up('KeyD');
  await page.waitForTimeout(250);
  const moved = await page.evaluate(([x, y]) => { const s = window.hollowWake.self(); return Math.hypot(s.x - x, s.y - y); }, position);
  assert.ok(moved > .5, `fresh input moves the hero (${moved})`);
  await page.screenshot({ path: path.join(out, '03-playing.png') });
  results.entry = 'Automatic selection shows a playable Crossing, waits for hero and terrain, quarantines held input, then accepts fresh gameplay input';

  holdNext = true;
  const beforeResume = selections;
  await page.reload();
  await waiting();
  assert.equal(selections, beforeResume + 1, 'remembered character resumes exactly once');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await page.waitForFunction(() => !window.hollowWake.loading.active && !window.hollowWake.inWorld);
  release();
  await page.waitForTimeout(400);
  assert.equal(await page.evaluate(() => window.hollowWake.inWorld), false, 'late cancelled entry cannot reopen the world');
  assert.equal(selections, beforeResume + 1, 'cancel does not start another attempt');
  const lists = await page.evaluate(() => window.hollowWake.counts.characterLists);
  holdNext = true;
  await page.locator('#login-connect').click();
  await page.waitForFunction(n => window.hollowWake.counts.characterLists > n, lists);
  await waiting();
  assert.equal(selections, beforeResume + 2);
  release();
  await ready();
  results.resume = 'Remembered resume, cancel, ignored late frames and explicit reconnect all use the same loading lifecycle';
  assert.deepEqual(errors, []);
  results.pageErrors = errors;
  writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results, null, 2));
} finally { release(); await browser.close(); }
