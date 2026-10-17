import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { FeedError, newsRequest, resolveRelease, type FeedConfig } from '../main/feed';
import { bytes, json, serve, sha256, tempDir, type TestServer } from './helpers';

// A stand-in for the GitHub REST API (owner o, repo r) plus a second origin playing the storage host
// that asset downloads redirect to. Every request is recorded, so the tests can check which host saw
// the token.

const ARCHIVE = Buffer.from('pretend archive');

function manifestFor(version: string, channel = 'stable') {
  return {
    schema: 1,
    product: 'Hollow Wake MMO',
    version,
    channel,
    platforms: {
      'windows-x64': { file: `client-${version}-windows-x64.zip`, size: ARCHIVE.length, sha256: sha256(ARCHIVE), executable: 'HollowWakeMMO.exe' },
    },
  };
}

interface FakeRelease {
  tag: string;
  prerelease?: boolean;
  draft?: boolean;
  manifest?: unknown;
  manifestState?: string;
}

let api: TestServer;
let storage: TestServer;
let releases: FakeRelease[] = [];
let repoVisible = true;
let requireToken = false;
let rateLimited = false;

function releaseJson(r: FakeRelease, index: number) {
  const assets = [];
  if (r.manifest !== undefined) {
    assets.push({
      id: index * 10 + 1,
      name: 'manifest.json',
      url: `${api.url}/repos/o/r/releases/assets/${index * 10 + 1}`,
      browser_download_url: `${api.url}/download/${r.tag}/manifest.json`,
      state: r.manifestState ?? 'uploaded',
      size: 100,
    });
    const version = r.tag.replace(/^v/, '');
    assets.push({
      id: index * 10 + 2,
      name: `client-${version}-windows-x64.zip`,
      url: `${api.url}/repos/o/r/releases/assets/${index * 10 + 2}`,
      browser_download_url: `${api.url}/download/${r.tag}/client-${version}-windows-x64.zip`,
      state: 'uploaded',
      size: ARCHIVE.length,
    });
  }
  return { tag_name: r.tag, html_url: `https://example.invalid/${r.tag}`, draft: !!r.draft, prerelease: !!r.prerelease, published_at: '2026-10-16T00:00:00Z', assets };
}

before(async () => {
  storage = await serve((req, res) => {
    const url = new URL(req.url ?? '', storage.url);
    const name = decodeURIComponent(url.pathname.split('/').pop() ?? '');
    const rel = releases.find((x) => x.tag === url.searchParams.get('tag'));
    if (!rel) {
      res.writeHead(404).end();
    } else if (name === 'manifest.json') {
      json(res, 200, rel.manifest ?? {});
    } else {
      bytes(res, ARCHIVE);
    }
  });
  api = await serve((req, res) => {
    const url = new URL(req.url ?? '', api.url);
    const authed = req.headers['authorization'] === 'Bearer good-token-000000000000';
    if (req.headers['authorization'] && !authed) {
      json(res, 401, { message: 'Bad credentials' });
      return;
    }
    if (rateLimited) {
      json(res, 403, { message: 'API rate limit exceeded' }, { 'x-ratelimit-remaining': '0', 'x-ratelimit-reset': String(Math.floor(Date.now() / 1000) + 600) });
      return;
    }
    const visible = repoVisible && (!requireToken || authed);
    const list = releases.map(releaseJson);
    if (url.pathname === '/repos/o/r') {
      json(res, visible ? 200 : 404, visible ? { full_name: 'o/r' } : { message: 'Not Found' });
    } else if (!visible && url.pathname.startsWith('/repos/o/r/')) {
      json(res, 404, { message: 'Not Found' });
    } else if (url.pathname === '/repos/o/r/releases/latest') {
      const latest = list.filter((r) => !r.draft && !r.prerelease)[0];
      json(res, latest ? 200 : 404, latest ?? { message: 'Not Found' });
    } else if (url.pathname === '/repos/o/r/releases') {
      json(res, 200, list);
    } else if (url.pathname.startsWith('/repos/o/r/releases/assets/')) {
      // The API answers an asset request with a redirect to storage on another origin.
      const id = Number(url.pathname.split('/').pop());
      const rel = releases[Math.floor(id / 10)];
      const name = id % 10 === 1 ? 'manifest.json' : `client-${rel?.tag.replace(/^v/, '')}-windows-x64.zip`;
      res.writeHead(302, { location: `${storage.url}/blob/${name}?tag=${rel?.tag}` }).end();
    } else if (url.pathname.startsWith('/download/')) {
      const [, , tag, name] = url.pathname.split('/');
      const rel = releases.find((x) => x.tag === tag);
      if (name === 'manifest.json') {
        json(res, 200, rel?.manifest ?? {});
      } else {
        bytes(res, ARCHIVE);
      }
    } else {
      json(res, 404, { message: 'Not Found' });
    }
  });
});

after(async () => {
  await api.close();
  await storage.close();
});

function cfg(over: Partial<FeedConfig> = {}): FeedConfig {
  return { owner: 'o', repo: 'r', api: api.url, override: null, token: null, userAgent: 'test', timeoutMs: 5000, ...over };
}

function reset(list: FakeRelease[]) {
  releases = list;
  repoVisible = true;
  requireToken = false;
  rateLimited = false;
  api.requests.length = 0;
  storage.requests.length = 0;
}

test('stable takes /releases/latest and its manifest', async () => {
  reset([
    { tag: 'v0.2.0-nightly.20261017.5', prerelease: true, manifest: manifestFor('0.2.0-nightly.20261017.5', 'nightly') },
    { tag: 'v0.1.0', manifest: manifestFor('0.1.0') },
  ]);
  const r = await resolveRelease(cfg(), 'stable', fetch);
  assert.equal(r.manifest.version, '0.1.0');
  assert.equal(r.tag, 'v0.1.0');
  assert.match(r.archives['windows-x64']?.url ?? '', /\/download\/v0\.1\.0\/client-0\.1\.0-windows-x64\.zip$/);
  assert.ok(api.requests.every((q) => !q.headers['authorization']), 'no token, no authorization header');
});

test('nightly takes the newest published release, pre-releases included, and skips drafts and releases without a manifest', async () => {
  reset([
    { tag: 'v0.3.0', draft: true, manifest: manifestFor('0.3.0') },
    { tag: 'v0.2.1-nightly.20261018.7', prerelease: true },
    { tag: 'v0.2.1-nightly.20261018.6', prerelease: true, manifest: manifestFor('0.2.1-nightly.20261018.6', 'nightly'), manifestState: 'open' },
    { tag: 'v0.2.1-nightly.20261017.5', prerelease: true, manifest: manifestFor('0.2.1-nightly.20261017.5', 'nightly') },
    { tag: 'v0.2.0', manifest: manifestFor('0.2.0') },
    { tag: 'not-a-version', manifest: manifestFor('9.9.9') },
  ]);
  const r = await resolveRelease(cfg(), 'nightly', fetch);
  assert.equal(r.manifest.version, '0.2.1-nightly.20261017.5');
  assert.equal(r.prerelease, true);
});

test('a stable cut above the nightlies wins on the nightly channel too', async () => {
  reset([
    { tag: 'v0.2.1-nightly.20261017.5', prerelease: true, manifest: manifestFor('0.2.1-nightly.20261017.5', 'nightly') },
    { tag: 'v0.2.1', manifest: manifestFor('0.2.1') },
  ]);
  assert.equal((await resolveRelease(cfg(), 'nightly', fetch)).manifest.version, '0.2.1');
});

test('a private repository without a token is explained, not reported as empty', async () => {
  reset([{ tag: 'v0.1.0', manifest: manifestFor('0.1.0') }]);
  requireToken = true;
  await assert.rejects(resolveRelease(cfg(), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'private' && /token/i.test(e.hint ?? ''));
  await assert.rejects(resolveRelease(cfg(), 'nightly', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'private');
});

test('with a token the private repository works, and the token never leaves the API host', async () => {
  reset([{ tag: 'v0.1.0', manifest: manifestFor('0.1.0') }]);
  requireToken = true;
  const r = await resolveRelease(cfg({ token: 'good-token-000000000000' }), 'stable', fetch);
  assert.equal(r.manifest.version, '0.1.0');
  const archive = r.archives['windows-x64'];
  assert.ok(archive);
  assert.match(archive.url, /\/releases\/assets\/\d+$/, 'archives come through the API asset endpoint');
  assert.equal(archive.headers['authorization'], 'Bearer good-token-000000000000');
  assert.ok(api.requests.length > 0 && api.requests.every((q) => q.headers['authorization'] === 'Bearer good-token-000000000000'));
  assert.ok(storage.requests.length > 0, 'the manifest download was redirected to storage');
  assert.ok(storage.requests.every((q) => q.headers['authorization'] === undefined), 'the redirect hop carried no token');
});

test('a token that cannot see the repository, a bad token and an empty stable channel each say so', async () => {
  reset([]);
  repoVisible = false;
  await assert.rejects(resolveRelease(cfg({ token: 'good-token-000000000000' }), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'auth');
  reset([]);
  await assert.rejects(resolveRelease(cfg({ token: 'wrong-token-00000000000' }), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'auth' && /rejected/.test(e.message));
  reset([{ tag: 'v0.1.1-nightly.20261017.1', prerelease: true, manifest: manifestFor('0.1.1-nightly.20261017.1', 'nightly') }]);
  await assert.rejects(resolveRelease(cfg(), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'no-release' && /nightly/.test(e.hint ?? ''));
});

test('rate limiting and an unreachable API are named', async () => {
  reset([]);
  rateLimited = true;
  await assert.rejects(resolveRelease(cfg(), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'rate-limit' && /refills in about/.test(e.message));
  reset([]);
  await assert.rejects(resolveRelease(cfg({ api: 'http://127.0.0.1:9' }), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'offline');
});

test('a broken manifest is reported against its release', async () => {
  reset([{ tag: 'v0.1.0', manifest: { schema: 1, version: '0.1.0', platforms: { 'windows-x64': { file: 'x.zip', size: 1, sha256: 'nope', executable: 'a.exe' } } } }]);
  await assert.rejects(resolveRelease(cfg(), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.kind === 'bad-release' && /v0\.1\.0/.test(e.message));
});

test('the override reads a manifest from a file or a URL and resolves archives beside it', async () => {
  reset([{ tag: 'v0.1.0', manifest: manifestFor('0.4.0') }]);
  const t = tempDir();
  try {
    const file = path.join(t.dir, 'manifest.json');
    writeFileSync(file, JSON.stringify(manifestFor('0.4.0')));
    const fromFile = await resolveRelease(cfg({ override: file }), 'stable', fetch);
    assert.equal(fromFile.manifest.version, '0.4.0');
    assert.match(fromFile.archives['windows-x64']?.url ?? '', /^file:.*client-0\.4\.0-windows-x64\.zip$/);
    const fromHttp = await resolveRelease(cfg({ override: `${api.url}/download/v0.1.0/manifest.json`, token: 'good-token-000000000000' }), 'nightly', fetch);
    assert.equal(fromHttp.archives['windows-x64']?.url, `${api.url}/download/v0.1.0/client-0.4.0-windows-x64.zip`);
    assert.equal(api.requests.at(-1)?.headers['authorization'], undefined, 'the token is not sent to an override');
    await assert.rejects(resolveRelease(cfg({ override: path.join(t.dir, 'missing.json') }), 'stable', fetch), (e: unknown) => e instanceof FeedError && e.status === 404);
  } finally {
    t.cleanup();
  }
});

test('news goes to raw.githubusercontent.com without a token and to the API with one', () => {
  const plain = newsRequest(cfg({ api: 'https://api.github.com' }), null);
  assert.equal(plain.url, 'https://raw.githubusercontent.com/o/r/main/launcher/news.json');
  assert.equal(plain.headers['authorization'], undefined);
  const authed = newsRequest(cfg({ api: 'https://api.github.com', token: 'good-token-000000000000' }), null);
  assert.equal(authed.url, 'https://api.github.com/repos/o/r/contents/launcher/news.json?ref=main');
  assert.equal(authed.headers['authorization'], 'Bearer good-token-000000000000');
  const override = newsRequest(cfg({ token: 'good-token-000000000000' }), 'http://127.0.0.1:1/news.json');
  assert.equal(override.headers['authorization'], undefined);
});
