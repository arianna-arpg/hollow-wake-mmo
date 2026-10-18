// Local preview of a prepared archive and packaged launcher, using the checkout's managed server.
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { main as prepareServer, root } from '../../scripts/play-local.mjs';

try {
  const feed = path.join(root, 'artifacts/quick-play/combined-feed/manifest.json');
  const executable = path.join(root, 'launcher/release/win-unpacked/Hollow Wake MMO.exe');
  if (!existsSync(feed) || !existsSync(executable)) {
    throw new Error('Prepare the combined game feed and Windows launcher first. See docs/reviews/2026-10-17-combined-startup.md.');
  }
  await prepareServer(['--browser', '--no-open']);
  const config = JSON.parse(readFileSync(path.join(root, 'config/playtest.json'), 'utf8'));
  const data = path.join(root, 'artifacts/playtest/launcher-preview');
  const settingsFile = path.join(data, 'launcher/settings.json');
  const settings = existsSync(settingsFile) ? JSON.parse(readFileSync(settingsFile, 'utf8')) : {};
  mkdirSync(path.dirname(settingsFile), { recursive: true });
  writeFileSync(settingsFile, JSON.stringify({ ...settings, address: '127.0.0.1:' + config.port, autoUpdate: true }, null, 2));
  console.log('Combined launcher ready. Preview installs: ' + data);
  if (!process.argv.includes('--prepare-only')) {
    const env = { ...process.env, HOLLOWWAKE_FEED: feed, HOLLOWWAKE_DATA_DIR: data, HOLLOWWAKE_NEWS: path.join(root, 'launcher/news.json') };
    delete env.ELECTRON_RUN_AS_NODE;
    delete env.HOLLOWWAKE_GITHUB_TOKEN;
    const child = spawn(executable, ['--play'], { cwd: root, env, detached: true, stdio: 'ignore', windowsHide: true });
    await new Promise((resolve, reject) => { child.once('spawn', resolve); child.once('error', reject); });
    child.unref();
  }
} catch (error) {
  console.error('Could not open the combined launcher: ' + error.message);
  process.exitCode = 1;
}
