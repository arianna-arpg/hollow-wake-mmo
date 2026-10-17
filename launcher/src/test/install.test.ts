import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { ChecksumError, downloadVerified } from '../main/download';
import { extractZip } from '../main/extract';
import { resolveRelease, type FeedConfig } from '../main/feed';
import { executablePath, installRelease, readInstalled } from '../main/installer';
import { launcherPaths } from '../main/paths';
import { bytes, makeZip, serve, sha256, tempDir, type TestServer } from './helpers';

const EXE = 'HollowWakeMMO.exe';

function clientZip(version: string): Buffer {
  return makeZip([
    { name: EXE, data: `client ${version}`, mode: 0o100755 },
    { name: `data_HollowWake.Client_windows_x86_64/`, mode: 0o040755 },
    { name: `data_HollowWake.Client_windows_x86_64/HollowWake.Core.dll`, data: 'dll' },
    { name: 'content/core/rules.yaml', data: `version: ${version}\n` },
  ]);
}

// A feed folder served over HTTP: manifest.json plus archives, in the layout scripts/make-manifest.mjs writes.
let server: TestServer;
const files = new Map<string, Buffer>();
let slow = false;

before(async () => {
  server = await serve((req, res) => {
    const name = decodeURIComponent((req.url ?? '').replace(/^\/+/, '').split('?')[0] ?? '');
    const data = files.get(name);
    if (!data) {
      res.writeHead(404).end();
      return;
    }
    if (slow) {
      res.writeHead(200, { 'content-length': String(data.length) });
      res.write(data.subarray(0, 8));
      return; // never finishes: a stalled connection
    }
    bytes(res, data);
  });
});

after(() => server.close());

function publish(version: string, opts: { corrupt?: boolean; wrongSize?: boolean } = {}): void {
  const zip = clientZip(version);
  const file = `HollowWakeMMO-client-${version}-windows-x64.zip`;
  files.set(file, zip);
  files.set('manifest.json', Buffer.from(JSON.stringify({
    schema: 1,
    product: 'Hollow Wake MMO',
    version,
    channel: version.includes('nightly') ? 'nightly' : 'stable',
    platforms: {
      'windows-x64': {
        file,
        size: opts.wrongSize ? zip.length + 1 : zip.length,
        sha256: opts.corrupt ? sha256('something else') : sha256(zip),
        executable: EXE,
      },
    },
  })));
}

function feed(): FeedConfig {
  return { owner: 'o', repo: 'r', api: 'http://127.0.0.1:9', override: `${server.url}/manifest.json`, token: null, userAgent: 'test', timeoutMs: 5000 };
}

test('download: verified bytes land at the destination with progress', async () => {
  const t = tempDir();
  try {
    const data = Buffer.alloc(300_000, 7);
    files.set('blob.bin', data);
    const seen: number[] = [];
    const dest = path.join(t.dir, 'blob.bin');
    const result = await downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: data.length, expectedSha256: sha256(data), fetchImpl: fetch, onProgress: (got) => seen.push(got) });
    assert.equal(result.bytes, data.length);
    assert.equal(readFileSync(dest).length, data.length);
    assert.equal(seen.at(-1), data.length);
    assert.equal(existsSync(`${dest}.part`), false);
  } finally {
    t.cleanup();
  }
});

test('download: a checksum mismatch throws and leaves nothing behind', async () => {
  const t = tempDir();
  try {
    files.set('blob.bin', Buffer.from('the real bytes'));
    const dest = path.join(t.dir, 'blob.bin');
    await assert.rejects(
      downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: 14, expectedSha256: sha256('other bytes...'), fetchImpl: fetch }),
      (e: unknown) => e instanceof ChecksumError && /checksum mismatch/.test(e.message),
    );
    assert.deepEqual(readdirSync(t.dir), []);
  } finally {
    t.cleanup();
  }
});

test('download: a size mismatch, a stall, a cancel and a file: source', async () => {
  const t = tempDir();
  try {
    const data = Buffer.from('twelve bytes');
    files.set('blob.bin', data);
    const dest = path.join(t.dir, 'blob.bin');
    await assert.rejects(downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: 11, expectedSha256: sha256(data), fetchImpl: fetch }), ChecksumError);
    await assert.rejects(downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: 13, expectedSha256: sha256(data), fetchImpl: fetch }), /manifest lists 13/);
    slow = true;
    await assert.rejects(downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: 12, expectedSha256: sha256(data), fetchImpl: fetch, stallMs: 300 }), /stalled/);
    const cancel = new AbortController();
    setTimeout(() => cancel.abort(new Error('cancelled by the test')), 100);
    await assert.rejects(downloadVerified({ url: `${server.url}/blob.bin`, headers: {}, dest, expectedSize: 12, expectedSha256: sha256(data), fetchImpl: fetch, signal: cancel.signal }), /cancelled by the test/);
    slow = false;
    assert.deepEqual(readdirSync(t.dir), []);
    const source = path.join(t.dir, 'source.bin');
    writeFileSync(source, data);
    const copy = path.join(t.dir, 'copy.bin');
    await downloadVerified({ url: pathToFileURL(source).href, headers: {}, dest: copy, expectedSize: 12, expectedSha256: sha256(data), fetchImpl: fetch });
    assert.equal(readFileSync(copy, 'utf8'), 'twelve bytes');
  } finally {
    slow = false;
    t.cleanup();
  }
});

test('extract: files, folders and Unix modes', async () => {
  const t = tempDir();
  try {
    const zip = path.join(t.dir, 'a.zip');
    writeFileSync(zip, clientZip('0.1.0'));
    const out = path.join(t.dir, 'out');
    const result = await extractZip(zip, out);
    assert.equal(result.files, 3);
    assert.equal(readFileSync(path.join(out, 'content', 'core', 'rules.yaml'), 'utf8'), 'version: 0.1.0\n');
    if (process.platform !== 'win32') {
      assert.equal(statSync(path.join(out, EXE)).mode & 0o777, 0o755);
    }
  } finally {
    t.cleanup();
  }
});

test('extract: climbing out of the folder, absolute paths and symbolic links are refused', async () => {
  const t = tempDir();
  try {
    const cases: Array<[string, Buffer, RegExp]> = [
      ['climb', makeZip([{ name: 'ok.txt', data: 'x' }, { name: '../evil.txt', data: 'x' }]), /invalid relative path|outside the install folder/],
      ['absolute', makeZip([{ name: '/etc/evil', data: 'x' }]), /absolute path|outside the install folder/],
      ['link', makeZip([{ name: 'link', data: '/etc/passwd', mode: 0o120777 }]), /symbolic link/],
    ];
    for (const [label, data, why] of cases) {
      const zip = path.join(t.dir, `${label}.zip`);
      writeFileSync(zip, data);
      await assert.rejects(extractZip(zip, path.join(t.dir, label)), why, label);
    }
    assert.equal(existsSync(path.join(t.dir, 'evil.txt')), false);
  } finally {
    t.cleanup();
  }
});

test('install: first install, update, and old builds pruned', async () => {
  const t = tempDir();
  try {
    const paths = launcherPaths(t.dir);
    publish('0.1.0');
    const first = await installRelease({ release: await resolveRelease(feed(), 'stable', fetch), platform: 'windows-x64', paths, fetchImpl: fetch });
    assert.equal(first.version, '0.1.0');
    assert.equal(readFileSync(executablePath(paths, first), 'utf8'), 'client 0.1.0');
    assert.deepEqual(await readInstalled(paths), first);

    publish('0.1.1-nightly.20261017.3');
    const phases = new Set<string>();
    const second = await installRelease({ release: await resolveRelease(feed(), 'nightly', fetch), platform: 'windows-x64', paths, fetchImpl: fetch, onProgress: (p) => phases.add(p.phase) });
    assert.equal(second.channel, 'nightly');
    assert.deepEqual([...phases], ['download', 'verify', 'extract', 'finish']);
    assert.equal((await readInstalled(paths))?.version, '0.1.1-nightly.20261017.3');
    assert.deepEqual(readdirSync(paths.clientDir).sort(), ['0.1.1-nightly.20261017.3', 'installed.json']);
    assert.deepEqual(readdirSync(paths.downloadsDir), [], 'the archive is removed once installed');
  } finally {
    t.cleanup();
  }
});

test('install: a checksum failure keeps the installed build and leaves no partial files', async () => {
  const t = tempDir();
  try {
    const paths = launcherPaths(t.dir);
    publish('0.1.0');
    await installRelease({ release: await resolveRelease(feed(), 'stable', fetch), platform: 'windows-x64', paths, fetchImpl: fetch });
    publish('0.2.0', { corrupt: true });
    await assert.rejects(
      installRelease({ release: await resolveRelease(feed(), 'stable', fetch), platform: 'windows-x64', paths, fetchImpl: fetch }),
      (e: unknown) => e instanceof ChecksumError,
    );
    assert.equal((await readInstalled(paths))?.version, '0.1.0');
    assert.deepEqual(readdirSync(paths.clientDir).sort(), ['0.1.0', 'installed.json']);
    assert.deepEqual(readdirSync(paths.downloadsDir), []);
  } finally {
    t.cleanup();
  }
});

test('install: an archive without its executable, or a build for another platform, is refused', async () => {
  const t = tempDir();
  try {
    const paths = launcherPaths(t.dir);
    publish('0.1.0');
    const release = await resolveRelease(feed(), 'stable', fetch);
    await assert.rejects(installRelease({ release, platform: 'linux-x64', paths, fetchImpl: fetch }), /no build for linux-x64/);
    const build = release.manifest.platforms['windows-x64'];
    assert.ok(build);
    build.executable = 'missing.exe';
    await assert.rejects(installRelease({ release, platform: 'windows-x64', paths, fetchImpl: fetch }), /does not contain its executable/);
    assert.equal(await readInstalled(paths), null);
    assert.deepEqual(readdirSync(paths.clientDir), []);
  } finally {
    t.cleanup();
  }
});

test('installed.json pointing at a missing build reads as not installed', async () => {
  const t = tempDir();
  try {
    const paths = launcherPaths(t.dir);
    publish('0.1.0');
    const record = await installRelease({ release: await resolveRelease(feed(), 'stable', fetch), platform: 'windows-x64', paths, fetchImpl: fetch });
    writeFileSync(paths.installedFile, JSON.stringify({ ...record, dir: '0.0.9' }));
    assert.equal(await readInstalled(paths), null);
    writeFileSync(paths.installedFile, JSON.stringify({ ...record, executable: '../../evil.exe' }));
    assert.equal(await readInstalled(paths), null);
  } finally {
    t.cleanup();
  }
});
