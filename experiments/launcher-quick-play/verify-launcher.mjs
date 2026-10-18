// End-to-end launcher checks against a private local API and a real package-client.sh archive.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'web/package.json'));
const { _electron: electron } = require('playwright-core');
const { serve, json } = require(path.join(root, 'launcher/dist/test/helpers.js'));
const arg = name => { const at = process.argv.indexOf(name); return at < 0 ? null : process.argv[at + 1]; };
const feed = path.resolve(root, arg('--feed') ?? 'artifacts/quick-play/feed');
const manifest = JSON.parse(readFileSync(path.join(feed, 'manifest.json'), 'utf8'));
const build = manifest.platforms['windows-x64'];
const archive = readFileSync(path.join(feed, build.file));
const output = path.join(root, 'experiments/launcher-quick-play/output');
const data = path.join(output, `install-${Date.now()}`);
mkdirSync(path.join(data, 'launcher'), { recursive: true });
writeFileSync(path.join(data, 'launcher/settings.json'), JSON.stringify({ autoUpdate: false, address: arg('--server') ?? '127.0.0.1:7777' }));
let holdFeed = false;
let slowDownload = false;
const versions = ['0.1.1', '0.1.0'];
const api = await serve((req, res, self) => {
  const url = new URL(req.url, self.url);
  const release = version => ({ tag_name: `v${version}`, assets: [{ name: 'manifest.json', state: 'uploaded', browser_download_url: `${self.url}/manifest/${version}` }, { name: build.file, state: 'uploaded', browser_download_url: `${self.url}/archive` }] });
  if (url.pathname.endsWith('/releases/latest')) {
    if (holdFeed) setTimeout(() => json(res, 200, release(versions[0])), 8000).unref();
    else json(res, 200, release(versions[0]));
  } else if (url.pathname.endsWith('/releases')) json(res, 200, versions.map(release));
  else if (url.pathname.includes('/releases/tags/')) json(res, 200, release(url.pathname.split('/').pop().slice(1)));
  else if (url.pathname.startsWith('/manifest/')) json(res, 200, { ...manifest, version: url.pathname.split('/').pop(), channel: 'stable' });
  else if (url.pathname === '/archive') {
    res.writeHead(200, { 'content-length': archive.length });
    if (slowDownload) { res.write(archive.subarray(0, 1024)); setTimeout(() => res.end(archive.subarray(1024)), 4000).unref(); }
    else res.end(archive);
  } else json(res, 404, {});
});
const env = { ...process.env, HOLLOWWAKE_DATA_DIR: data, HOLLOWWAKE_GITHUB_API: api.url, HOLLOWWAKE_REPO: 'o/r', HOLLOWWAKE_NEWS: path.join(root, 'launcher/news.json') };
delete env.ELECTRON_RUN_AS_NODE;
delete env.HOLLOWWAKE_FEED;
delete env.HOLLOWWAKE_GITHUB_TOKEN;
const packaged = process.argv.includes('--packaged');
const exe = path.join(root, packaged ? 'launcher/release/win-unpacked/Hollow Wake MMO.exe' : 'launcher/node_modules/electron/dist/electron.exe');
let app;
let gamePid;
const result = { feed: manifest.version };
// IPC returns a Promise. Poll it from Node: a browser predicate treats that Promise as truthy.
const waitState = async (page, predicate, timeout = 30000) => {
  const deadline = Date.now() + timeout;
  let state;
  do {
    state = await page.evaluate(() => window.launcher.getState());
    if (predicate(state)) return state;
    await new Promise(resolve => setTimeout(resolve, 100));
  } while (Date.now() < deadline);
  throw new Error('Launcher state did not settle: ' + JSON.stringify(state));
};
const verifyEntry = async name => {
  if (!process.argv.includes('--entry')) return;
  const logFile = path.join(data, 'launcher/logs/client.log');
  const deadline = Date.now() + 60000;
  let log = '';
  while (Date.now() < deadline) {
    if (existsSync(logFile)) log = readFileSync(logFile, 'utf8');
    if (log.includes('QUICKPLAY READY: entered the world; Crossing complete')) break;
    await new Promise(resolve => setTimeout(resolve, 150));
  }
  writeFileSync(path.join(output, 'launcher-' + name + '-client.log'), log);
  assert.match(log, /QUICKPLAY READY: entered the world; Crossing complete/);
  assert.equal(log.match(/QUICKPLAY CROSSING/g)?.length, 1, 'one real character entry per Play');
  result[name] = 'Installed combined client resumed its character through the Crossing and reached real world readiness';
};
const launch = async extra => {
  app = await electron.launch({ executablePath: exe, args: [...(packaged ? [] : [path.join(root, 'launcher')]), '--disable-gpu', ...extra], env });
  return app.firstWindow();
};
const killGame = () => { if (gamePid) { try { process.kill(gamePid); } catch {} gamePid = null; } };
try {
  let page = await launch([]);
  await page.waitForFunction(() => {
    const mark = document.querySelector('img.mark');
    return mark?.complete && mark.naturalWidth > 0;
  });
  const appPath = await app.evaluate(({ app }) => app.getAppPath());
  const { extractFile } = createRequire(path.join(root, 'launcher/package.json'))('@electron/asar');
  for (const file of ['logo.svg', 'icon.ico', 'icon.png']) {
    const entry = file === 'logo.svg' ? path.join('dist', 'renderer', file) : path.join('dist', file);
    const bundled = packaged ? extractFile(appPath, entry) : readFileSync(path.join(appPath, entry));
    assert.deepEqual(bundled, readFileSync(path.join(root, 'assets/branding', file)), `bundled ${file} matches the canonical brand`);
  }
  result.brandArtwork = `${packaged ? 'Packaged' : 'Source'} launcher renders the shared insignia and bundles byte-identical PNG/ICO assets`;
  await waitState(page, s => s.builds.length === 2);
  await waitState(page, s => s.feed.remote?.version === '0.1.1');
  await page.evaluate(() => Promise.all([window.launcher.selectBuild('v0.1.0'), window.launcher.install()]));
  await waitState(page, s => s.task.kind === 'idle', 60000);
  const afterSelection = await page.evaluate(() => window.launcher.getState());
  assert.notEqual(afterSelection.installed?.version, '0.1.1', 'an immediate Install must never use the previous selection');
  result.selectionRace = 'Changing selection invalidates the previous release before asynchronous settings persistence';
  await waitState(page, s => s.feed.remote?.version === '0.1.0');
  if (!afterSelection.installed) await page.locator('#primary').click();
  await waitState(page, s => s.installed?.version === '0.1.0', 60000);
  result.selectedInstall = 'Pinned older release downloaded, verified and installed';
  await page.waitForFunction(() => document.getElementById('build-select').value === 'v0.1.0' && !document.getElementById('build-select').disabled);
  await page.locator('#build-select').selectOption('');
  await waitState(page, s => s.settings.releaseTag === null);
  await waitState(page, s => s.action === 'update');
  await page.locator('#secondary').filter({ hasText: 'Update to 0.1.1' }).waitFor();
  assert.equal(await page.locator('#primary').textContent(), 'Play');
  await page.screenshot({ path: path.join(output, 'launcher-ready.png') });
  slowDownload = true;
  await page.locator('#secondary').click();
  await waitState(page, s => s.task.kind === 'download');
  const outcomes = await page.evaluate(() => Promise.all([window.launcher.play(), window.launcher.play()]));
  assert.equal(outcomes.filter(o => o.ok).length, 1);
  gamePid = await page.evaluate(async () => (await window.launcher.getState()).game.pid);
  assert.ok(gamePid);
  await waitState(page, s => s.installed?.version === '0.1.1', 60000);
  assert.ok(existsSync(path.join(data, 'client/0.1.0', build.executable)));
  result.concurrentPlay = 'Play works during download, duplicate Play spawns once, running build survives install';
  await verifyEntry('play');
  killGame();
  await app.close();
  app = null;
  holdFeed = true;
  const started = performance.now();
  page = await launch(['--play']);
  await waitState(page, s => s.game.running);
  const state = await page.evaluate(() => window.launcher.getState());
  gamePid = state.game.pid;
  assert.equal(state.feed.status, 'checking');
  result.shortcutMilliseconds = Math.round(performance.now() - started);
  result.shortcut = 'Installed game starts before the deliberately stalled feed answers';
  await verifyEntry('shortcutEntry');
  writeFileSync(path.join(output, packaged ? 'packaged-launcher-results.json' : 'launcher-results.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
} finally {
  killGame();
  if (app) await app.close();
  await api.close();
}
