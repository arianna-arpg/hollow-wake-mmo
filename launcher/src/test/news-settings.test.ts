import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { gameEnvironment } from '../main/game';
import { loadNews, sanitizeNews } from '../main/news';
import { DEFAULT_SETTINGS, loadSettings, openToken, saveSettings, sealToken, type Cipher } from '../main/settings';
import { json, serve, tempDir } from './helpers';

test('news: only bounded plain text and https links reach the page', () => {
  const news = sanitizeNews({
    motd: '  Hello\u0007 there  ',
    items: [
      { date: '2026-10-16', title: 'Fine', body: 'Body', url: 'https://example.org/a' },
      { date: 'yesterday', title: 'Bad link', body: 'x', url: 'javascript:alert(1)' },
      { title: 'x'.repeat(500) },
      { nothing: true },
      'not an object',
    ],
  });
  assert.equal(news.motd, 'Hello there');
  assert.equal(news.items.length, 3);
  assert.deepEqual(news.items[0], { date: '2026-10-16', title: 'Fine', body: 'Body', url: 'https://example.org/a' });
  assert.equal(news.items[1]?.url, null);
  assert.equal(news.items[1]?.date, null);
  assert.equal(news.items[2]?.title.length, 120);
  assert.deepEqual(sanitizeNews('garbage'), { motd: null, items: [] });
});

test('news: live, then the cached copy when offline, then the bundled copy', async () => {
  const t = tempDir();
  const server = await serve((_req, res) => json(res, 200, { motd: 'live news', items: [{ title: 'One' }] }));
  try {
    const bundled = path.join(t.dir, 'bundled.json');
    writeFileSync(bundled, JSON.stringify({ motd: 'bundled news', items: [{ title: 'Shipped' }] }));
    const cacheFile = path.join(t.dir, 'cache', 'news-cache.json');
    const source = { headers: {}, cacheFile, bundledFile: bundled, timeoutMs: 3000, fetchImpl: fetch };

    const live = await loadNews({ ...source, url: new URL(`${server.url}/news.json`) });
    assert.equal(live.status, 'live');
    assert.equal(live.motd, 'live news');
    assert.ok(readFileSync(cacheFile, 'utf8').includes('live news'));

    const cached = await loadNews({ ...source, url: new URL('http://127.0.0.1:9/news.json') });
    assert.equal(cached.status, 'cached');
    assert.equal(cached.items[0]?.title, 'One');
    assert.match(cached.note ?? '', /could not be refreshed/);

    const fresh = await loadNews({ ...source, cacheFile: path.join(t.dir, 'none.json'), url: null });
    assert.equal(fresh.status, 'bundled');
    assert.equal(fresh.items[0]?.title, 'Shipped');

    const fromFile = await loadNews({ ...source, cacheFile: path.join(t.dir, 'other.json'), url: pathToFileURL(bundled) });
    assert.equal(fromFile.status, 'live');
  } finally {
    await server.close();
    t.cleanup();
  }
});

test('settings: defaults, repairs, round trip and the token', async () => {
  const t = tempDir();
  try {
    const file = path.join(t.dir, 'settings.json');
    assert.deepEqual(await loadSettings(file), { ...DEFAULT_SETTINGS });
    writeFileSync(file, JSON.stringify({ address: 'not an address!', channel: 'beta', developer: 'yes', autoUpdate: false }));
    assert.deepEqual(await loadSettings(file), { ...DEFAULT_SETTINGS, autoUpdate: false });

    const cipher: Cipher = { available: () => true, encrypt: (s) => Buffer.from(s).toString('base64'), decrypt: (s) => Buffer.from(s, 'base64').toString() };
    const sealed = sealToken('ghp_abcdefghijklmnopqrstuvwxyz', cipher);
    assert.equal(sealed.encrypted, true);
    assert.notEqual(sealed.value, 'ghp_abcdefghijklmnopqrstuvwxyz');
    await saveSettings(file, { address: 'play.example.org:7000', channel: 'nightly', developer: true, autoUpdate: true, releaseTag: 'v0.1.0', token: sealed });
    const loaded = await loadSettings(file);
    assert.equal(loaded.address, 'play.example.org:7000');
    assert.equal(loaded.channel, 'nightly');
    assert.equal(loaded.developer, true);
    assert.equal(loaded.releaseTag, 'v0.1.0');
    assert.equal(openToken(loaded.token, cipher), 'ghp_abcdefghijklmnopqrstuvwxyz');

    const none: Cipher = { available: () => false, encrypt: () => '', decrypt: () => '' };
    assert.deepEqual(sealToken('plain-token', none), { encrypted: false, value: 'plain-token' });
    assert.equal(openToken(sealed, none), null, 'an encrypted token without a key store is unusable, not garbage');
  } finally {
    t.cleanup();
  }
});

test('the game gets the environment minus the token and Electron switches', () => {
  const env = gameEnvironment({ PATH: '/bin', HOLLOWWAKE_GITHUB_TOKEN: 'secret', ELECTRON_RUN_AS_NODE: '1', HOLLOWWAKE_DATA_DIR: '/d' });
  assert.deepEqual(env, { PATH: '/bin', HOLLOWWAKE_DATA_DIR: '/d' });
});
