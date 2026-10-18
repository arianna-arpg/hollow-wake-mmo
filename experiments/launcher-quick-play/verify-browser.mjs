// Run after scripts/play-local.mjs --browser --no-open. Uses the same browser runtime as web smoke.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const require = createRequire(path.join(root, 'web/package.json'));
const { chromium } = require('playwright-core');
const output = path.join(here, 'output');
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ channel: process.platform === 'win32' ? 'msedge' : 'chrome', args: ['--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const result = {};
try {
  await page.route('**/play-catalog.json', route => route.fulfill({ json: { enabled: true, repository: 'arianna-arpg/hollow-wake-mmo', branch: 'main', commit: '1234567890', branches: ['main', 'codex/launcher-quick-play'] } }));
  await page.goto('http://localhost:7778/?branch=codex%2Flauncher-quick-play');
  await page.locator('.play-ribbon').waitFor();
  assert.equal(await page.locator('.play-branch-link').getAttribute('href'), 'https://codespaces.new/arianna-arpg/hollow-wake-mmo/tree/codex%2Flauncher-quick-play?quickstart=1');
  await page.screenshot({ path: path.join(output, 'browser-branch-picker.png') });
  await page.setViewportSize({ width: 700, height: 700 });
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await page.screenshot({ path: path.join(output, 'browser-branch-picker-narrow.png') });
  result.branchLinks = 'Selected branch and resumable Codespace link agree; narrow layout fits';
  await page.unroute('**/play-catalog.json');
  await page.setViewportSize({ width: 1280, height: 900 });
  const account = `quickplay-${Date.now()}`;
  const url = `http://localhost:7778/?play=1&account=${account}`;
  await page.goto(url);
  await page.locator('#character-list').waitFor();
  assert.equal(await page.locator('#login').isVisible(), true);
  await page.locator('#login-new-name').fill('Quickplay');
  await page.locator('#login-create').click();
  await page.waitForFunction(() => window.hollowWake?.inWorld === true, null, { timeout: 30000 });
  result.firstRun = 'Automatic login stops for creation; creating one character enters the world';
  const start = performance.now();
  await page.reload();
  await page.waitForFunction(() => window.hollowWake?.inWorld === true, null, { timeout: 30000 });
  result.resumeMilliseconds = Math.round(performance.now() - start);
  await page.screenshot({ path: path.join(output, 'browser-resumed.png') });
  assert.deepEqual(errors, []);
  result.pageErrors = errors;
  writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
} finally { await browser.close(); }
