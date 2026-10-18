// Exercises the built client against a private server, using the existing background-test lifecycle.
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from '../../web/node_modules/playwright-core/index.mjs';
import { reservePorts, serverConfig, startProcess, stopProcess, waitForServer, waitProcess } from '../../scripts/test-background.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const out = join(root, 'experiments/mu-selection/artifacts'); await mkdir(out, { recursive: true });
const dir = await mkdtemp(join(out, 'entry-')); const name = 'mu-' + dir.split(/[\\/]/).at(-1);
const ports = await reservePorts(), [tcp, http] = ports.ports, url = `http://127.0.0.1:${http}`;
await writeFile(join(dir, 'server.yaml'), serverConfig(await readFile(join(root, 'config/server.yaml'), 'utf8'), {
  name, bind: '127.0.0.1', port: tcp, websocket_port: http, websocket_enabled: true,
  data_dir: join(dir, 'data'), web_root: join(root, 'web/dist'), auth: 'dev',
}));
await ports.release();
const server = startProcess('dotnet', [join(root, 'src/HollowWake.Server.Host/bin/Debug/net10.0/hollowwake-server.dll'), '--root', root, '--config', join(dir, 'server.yaml')], { cwd: root, log: join(dir, 'server.log') });
let browser; const errors = [], checks = [];
const check = (label, value) => { assert.ok(value, label); checks.push(label); console.log('MU PASS ' + label); };
const signal = new AbortController().signal;
try {
  await waitForServer(url, name, server, signal);
  browser = await chromium.launch({ channel: 'msedge', headless: true, args: ['--ignore-gpu-blocklist', '--enable-unsafe-swiftshader'] });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, hasTouch: true });
  await context.addInitScript(() => {
    window.muPad = null;
    Object.defineProperty(navigator, 'getGamepads', { value: () => [window.muPad] });
    window.muSent = [];
    const send = WebSocket.prototype.send;
    WebSocket.prototype.send = function(data) { try { window.muSocket = this; window.muSent.push(JSON.parse(typeof data === 'string' ? data : new TextDecoder().decode(data))); } catch {} return send.call(this, data); };
  });
  const page = await context.newPage(); page.on('pageerror', e => errors.push(String(e)));
  let hold = false; const held = []; let downstream;
  await page.routeWebSocket('**/ws', ws => {
    const upstream = ws.connectToServer(); downstream = ws;
    upstream.onMessage(message => {
      const text = typeof message === 'string' ? message : message.toString();
      if (hold && JSON.parse(text).t === 'character_list') held.push(message); else ws.send(message);
    });
  });
  const address = `${url}/?play=1&account=${name}&server=${encodeURIComponent(`ws://127.0.0.1:${http}/ws`)}&fresh-settings`;
  await page.goto(address);
  await page.waitForFunction(() => window.hollowWake?.entry?.seats.length > 0);
  await page.waitForTimeout(250);
  const entry = () => page.evaluate(() => window.hollowWake.entry);
  check('quick play waits in Mu with every Calling available', (await entry()).seats.every(s => s.available) && !(await page.evaluate(() => window.hollowWake.inWorld)));
  check('Mu sends no gameplay movement or casts', !(await page.evaluate(() => window.muSent.some(m => ['move_dir', 'move_to', 'use_weave'].includes(m.t)))));
  await page.screenshot({ path: join(dir, 'mu-wide.jpg'), type: 'jpeg', quality: 82 });
  const seat = (await entry()).seats.find(s => s.calling === 'holdfast');
  await page.touchscreen.tap(seat.screen.x, seat.screen.y);
  await page.waitForFunction(() => window.hollowWake.entry.opened >= 0);
  check('touch-to-drift and stillness open the correct vessel', await page.locator('.entry-card h2').innerText() === seat.name);
  await page.screenshot({ path: join(dir, 'mu-card.jpg'), type: 'jpeg', quality: 82 });
  await page.keyboard.press('Escape'); await page.waitForTimeout(1200);
  check('dismissed dwell card stays closed until step-out', (await entry()).opened === -1);
  const before = await entry(); await page.keyboard.down('KeyD'); await page.waitForTimeout(350); await page.keyboard.up('KeyD');
  check('bound keyboard movement drifts the wisp', (await entry()).x > before.x + 20);
  await page.getByRole('button', { name: 'Options', exact: true }).click();
  const paused = await entry(); await page.keyboard.down('KeyD'); await page.waitForTimeout(200); await page.keyboard.up('KeyD');
  check('Options suspends Mu movement', Math.abs((await entry()).x - paused.x) < .001);
  await page.keyboard.press('Escape');
  await page.waitForFunction(() => !window.hollowWake.ui.optionsOpen);
  await page.click('[data-vessel="calling:holdfast"]');
  for (let i = 0; i < 10; i++) { await page.keyboard.press(i % 2 ? 'Shift+Tab' : 'Tab'); check(`card focus remains contained ${i}`, await page.evaluate(() => document.activeElement.closest('.entry-card') !== null)); }
  await page.fill('.entry-card input', 'Bad9'); await page.locator('.entry-card button').first().click();
  await page.waitForFunction(() => window.hollowWake.status.includes('Names are'));
  check('server refusal leaves an editable card', await page.locator('.entry-card input').isEnabled());
  hold = true;
  await page.fill('.entry-card input', 'Wren'); await page.locator('.entry-card button').first().click();
  for (let i = 0; i < 100 && held.length === 0; i++) await delay(30);
  assert.ok(held.length > 0, 'held creation reply');
  check('pending creation disables duplicate submission', await page.locator('.entry-card button').first().isDisabled());
  await page.locator('.entry-card button').last().click();
  hold = false; for (const message of held.splice(0)) downstream.send(message);
  await page.waitForFunction(() => window.hollowWake.characters().length === 1);
  check('cancelled creation reply never wakes a character', !(await page.evaluate(() => window.hollowWake.inWorld)) && (await entry()).opened === -1);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.click('[data-vessel="calling:kindler"]');
  const box = await page.locator('.entry-card').boundingBox();
  check('portrait card remains on screen', box.x >= 0 && box.y >= 0 && box.x + box.width <= 390 && box.y + box.height <= 844);
  await page.screenshot({ path: join(dir, 'mu-portrait.jpg'), type: 'jpeg', quality: 82 });
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Leave Mu', exact: true }).click();
  check('leaving Mu restores the connection form', await page.locator('#login-connect').isVisible());
  await page.click('#login-connect'); await page.waitForFunction(() => window.hollowWake.entry?.seats.some(s => s.character));
  await page.setViewportSize({ width: 1280, height: 800 });
  const character = (await entry()).seats.find(s => s.character);
  await page.locator(`[data-vessel="${character.id}"]`).focus();
  const pad = async button => { await page.evaluate(index => { window.muPad = { connected: true, axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: i === index, value: i === index ? 1 : 0 })) }; }, button); await page.waitForTimeout(120); };
  await pad(-1); await pad(0); await pad(-1);
  check('controller opens remembered vessel', (await entry()).opened >= 0 && await page.locator('.entry-card h2').innerText() === 'Wren');
  await pad(0); await pad(-1);
  await page.waitForFunction(() => window.hollowWake.inWorld && !window.hollowWake.loading.active, { timeout: 30000 });
  check('explicit controller Wake completes the real loading handoff', await page.evaluate(() => window.hollowWake.self() !== null));
  await page.evaluate(() => window.muSocket.send(new TextEncoder().encode(JSON.stringify({ t: 'leave_world' }))));
  await page.waitForFunction(() => !window.hollowWake.inWorld && window.hollowWake.entry?.seats.some(s => s.character));
  check('server return routes back through the same Mu scene', await page.locator('.entry-scene').isVisible() && await page.evaluate(() => window.hollowWake.self() === null));
  await page.click(`[data-vessel="${character.id}"]`);
  check('returned roster remains interactive', (await entry()).opened >= 0);
  check('no browser exceptions', errors.length === 0);
  const probe = startProcess('dotnet', ['run', '--no-build', '--project', 'src/HollowWake.Tools', '--', 'probe', '--host', '127.0.0.1', '--port', String(tcp), '--account', 'mu-probe', '--seconds', '2', '--features', 'entry_flow,interface'], { cwd: root, log: join(dir, 'probe.log') });
  const probeResult = await waitProcess(probe, 60000, signal); check('live protocol probe', probeResult.code === 0);
  const startup = startProcess('node', ['experiments/launcher-quick-play/verify-crossing-startup.mjs', '--url', url, '--output', join(dir, 'startup')], { cwd: root, log: join(dir, 'startup.log') });
  const startupResult = await waitProcess(startup, 120000, signal); check('delayed startup, held input, cancellation and remembered Wake', startupResult.code === 0);
  await writeFile(join(dir, 'result.json'), JSON.stringify({ passed: checks, errors }, null, 2) + '\n');
  console.log(`MU DONE ${checks.length} checks; evidence ${dir}`);
} finally { await browser?.close(); await stopProcess(server); }
