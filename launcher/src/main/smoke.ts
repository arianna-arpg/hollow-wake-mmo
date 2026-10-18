// Headless self-checks and screenshots for CI and for people checking a change:
//   --smoke-test             boot, check the page and its bridge, print one SMOKE line, exit 0 or 1
//   --screenshot FILE        wait until the feed and the news have settled, save the window as PNG, exit
//   --screenshot-delay MS    wait this much longer first (to catch a download in progress)

import { writeFileSync } from 'node:fs';
import type { BrowserWindow } from 'electron';

export interface SmokeOptions {
  checks: boolean;
  screenshot: string | null;
  delayMs: number;
  version: string;
  settled: () => boolean;
  pageErrors: string[];
}

export async function runSmoke(win: BrowserWindow, o: SmokeOptions): Promise<number> {
  const started = Date.now();
  while (!o.settled() && Date.now() - started < 20_000) {
    await sleep(100);
  }
  await sleep(400 + o.delayMs);
  let failures: string[] = [];
  if (o.checks) {
    failures = await pageChecks(win, o.version);
    failures.push(...o.pageErrors.map((e) => `page error: ${e}`));
  }
  if (o.screenshot) {
    const image = await win.webContents.capturePage();
    writeFileSync(o.screenshot, image.toPNG());
    process.stdout.write(`SMOKE screenshot saved to ${o.screenshot} (${image.getSize().width}x${image.getSize().height})\n`);
  }
  if (o.checks) {
    process.stdout.write(failures.length ? `SMOKE launcher FAIL: ${failures.join('; ')}\n` : `SMOKE launcher PASS ${o.version}\n`);
  }
  return failures.length ? 1 : 0;
}

async function pageChecks(win: BrowserWindow, version: string): Promise<string[]> {
  const report = (await win.webContents.executeJavaScript(`(() => {
    const text = (id) => document.getElementById(id)?.textContent ?? null;
    return {
      bridge: typeof window.launcher === 'object' && typeof window.launcher.play === 'function',
      primary: text('primary'),
      buildPicker: !!document.getElementById('build-select'),
      version: text('launcher-version'),
      address: document.getElementById('address')?.value ?? null,
      channel: !!document.querySelector('[data-channel="stable"]') && !!document.querySelector('[data-channel="nightly"]'),
      developer: !!document.getElementById('developer'),
      news: document.querySelectorAll('#news-list .news-item').length,
      status: text('status-title'),
    };
  })()`)) as Record<string, unknown>;
  const failures: string[] = [];
  if (report['bridge'] !== true) {
    failures.push('the preload bridge is missing');
  }
  if (!report['primary']) {
    failures.push('the primary button is missing');
  }
  if (!report['buildPicker']) failures.push('the build picker is missing');
  const playable = await win.webContents.executeJavaScript(`(async () => {
    const state = await window.launcher.getState();
    return !state.installed || document.getElementById('primary')?.textContent === 'Play';
  })()`);
  if (!playable) failures.push('an installed build must have a primary Play button');
  if (typeof report['version'] !== 'string' || !report['version'].includes(version)) {
    failures.push(`the page does not show launcher version ${version}`);
  }
  if (!report['address']) {
    failures.push('the server address field is empty');
  }
  if (report['channel'] !== true || report['developer'] !== true) {
    failures.push('the channel toggle or the Developer mode toggle is missing');
  }
  if (typeof report['news'] !== 'number' || report['news'] < 1) {
    failures.push('no news items rendered');
  }
  if (!report['status']) {
    failures.push('no status line rendered');
  }
  return failures;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
