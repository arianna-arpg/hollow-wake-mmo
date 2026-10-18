import assert from 'node:assert/strict';
import path from 'node:path';
import { test } from 'node:test';
import { parseAddress } from '../main/address';
import { decideAction } from '../main/decide';
import { isSafeRelativePath, parseManifest, platformKey } from '../main/manifest';
import { dataRoot, launcherPaths } from '../main/paths';
import { channelOfVersion, compareVersions, parseVersion } from '../main/version';

test('versions: tags and manifests normalise to semver', () => {
  assert.equal(parseVersion('v0.1.0'), '0.1.0');
  assert.equal(parseVersion('0.2.1-nightly.20261017.42'), '0.2.1-nightly.20261017.42');
  assert.equal(parseVersion('latest'), null);
  assert.equal(parseVersion(undefined), null);
});

test('versions: a nightly sorts above the stable before it and below the stable it leads to', () => {
  assert.ok(compareVersions('0.1.1-nightly.20261017.42', '0.1.0') > 0);
  assert.ok(compareVersions('0.1.1-nightly.20261017.42', '0.1.1') < 0);
  assert.ok(compareVersions('0.1.1-nightly.20261018.43', '0.1.1-nightly.20261017.42') > 0);
  assert.ok(compareVersions('0.1.1-nightly.20261017.9', '0.1.1-nightly.20261017.10') < 0);
  assert.equal(channelOfVersion('0.1.1-nightly.20261017.42'), 'nightly');
  assert.equal(channelOfVersion('0.1.1'), 'stable');
});

test('decide: the big button', () => {
  const stable = { version: '0.1.0', channel: 'stable' };
  const newer = { version: '0.1.1', channel: 'stable' };
  const nightly = { version: '0.1.1-nightly.20261017.1', channel: 'nightly' };
  assert.equal(decideAction(null, null, false), 'unavailable');
  assert.equal(decideAction(stable, null, false), 'play');
  assert.equal(decideAction(null, stable, false), 'install');
  assert.equal(decideAction(stable, newer, false), 'update');
  assert.equal(decideAction(stable, stable, false), 'play');
  assert.equal(decideAction(newer, stable, false), 'play', 'never moves down within a channel');
  assert.equal(decideAction(nightly, stable, false), 'switch', 'changing channel may move down');
  assert.equal(decideAction(stable, nightly, false), 'update');
  assert.equal(decideAction(stable, newer, true), 'busy');
  assert.equal(decideAction(newer, stable, false, true), 'switch', 'an explicitly selected older build may be installed');
});

test('address: host, port, IPv6 and mistakes', () => {
  const ok = (input: string) => {
    const r = parseAddress(input);
    assert.ok(r.ok, `${input} should parse`);
    return r.address;
  };
  assert.deepEqual(ok('127.0.0.1:7777'), { host: '127.0.0.1', port: 7777, text: '127.0.0.1:7777' });
  assert.deepEqual(ok('  play.example.org  '), { host: 'play.example.org', port: 7777, text: 'play.example.org:7777' });
  assert.deepEqual(ok('[::1]:8000'), { host: '::1', port: 8000, text: '[::1]:8000' });
  assert.deepEqual(ok('::1'), { host: '::1', port: 7777, text: '[::1]:7777' });
  assert.deepEqual(ok('localhost:1'), { host: 'localhost', port: 1, text: 'localhost:1' });
  for (const bad of ['', 'host:0', 'host:65536', 'host:abc', 'bad host', '-x.org', '999.1.1.1', '[nothost]:1', 'a:']) {
    assert.equal(parseAddress(bad).ok, false, `${JSON.stringify(bad)} should be refused`);
  }
});

const SHA = 'a'.repeat(64);
const manifest = (over: Record<string, unknown> = {}, platform: Record<string, unknown> = {}) => ({
  schema: 1,
  product: 'Hollow Wake MMO',
  version: '0.1.0',
  channel: 'stable',
  platforms: { 'windows-x64': { file: 'HollowWakeMMO-client-0.1.0-windows-x64.zip', size: 10, sha256: SHA, executable: 'HollowWakeMMO.exe', ...platform } },
  ...over,
});

test('manifest: a good one parses', () => {
  const m = parseManifest(manifest());
  assert.equal(m.version, '0.1.0');
  assert.equal(m.channel, 'stable');
  assert.equal(m.platforms['windows-x64']?.executable, 'HollowWakeMMO.exe');
  assert.equal(parseManifest(manifest({ channel: undefined, version: '0.2.0-nightly.1.2' })).channel, 'nightly');
  assert.equal(parseManifest(manifest({}, { sha256: SHA.toUpperCase() })).platforms['windows-x64']?.sha256, SHA);
});

test('manifest: anything unsafe or unknown is refused', () => {
  assert.throws(() => parseManifest(manifest({ schema: 2 })), /update the launcher/);
  assert.throws(() => parseManifest(manifest({ version: 'nope' })), /semantic version/);
  assert.throws(() => parseManifest(manifest({ channel: 'beta' })), /neither stable nor nightly/);
  assert.throws(() => parseManifest(manifest({ platforms: {} })), /no platforms/);
  assert.throws(() => parseManifest(manifest({}, { sha256: 'abc' })), /sha256/);
  assert.throws(() => parseManifest(manifest({}, { size: 0 })), /size/);
  assert.throws(() => parseManifest(manifest({}, { file: '../x.zip' })), /archive file/);
  assert.throws(() => parseManifest(manifest({}, { executable: '../../evil.exe' })), /executable/);
  assert.throws(() => parseManifest(manifest({}, { executable: 'C:/Windows/evil.exe' })), /executable/);
  assert.throws(() => parseManifest(manifest({}, { url: 'ftp://x/y.zip' })), /url/);
  assert.throws(() => parseManifest([]), /not a JSON object/);
});

test('manifest: relative paths and platform keys', () => {
  assert.ok(isSafeRelativePath('HollowWakeMMO.x86_64'));
  assert.ok(isSafeRelativePath('bin/game.exe'));
  for (const bad of ['', '/abs', 'a/../b', './a', 'a\\b', 'C:x', 'a//b']) {
    assert.equal(isSafeRelativePath(bad), false, bad);
  }
  assert.equal(platformKey('win32', 'x64'), 'windows-x64');
  assert.equal(platformKey('linux', 'x64'), 'linux-x64');
  assert.equal(platformKey('darwin', 'arm64'), 'macos-arm64');
});

test('paths: per-platform data roots and the override', () => {
  assert.equal(dataRoot({ LOCALAPPDATA: 'C:\\Users\\p\\AppData\\Local' }, 'win32', 'C:\\Users\\p').endsWith('HollowWakeMMO'), true);
  assert.equal(dataRoot({}, 'linux', '/home/p').replace(/\\/g, '/').endsWith('/home/p/.local/share/hollowwakemmo'), true);
  assert.equal(dataRoot({ XDG_DATA_HOME: '/data' }, 'linux', '/home/p').replace(/\\/g, '/').endsWith('/data/hollowwakemmo'), true);
  assert.equal(dataRoot({ HOLLOWWAKE_DATA_DIR: 'here' }, 'linux', '/home/p'), path.resolve('here'));
  const p = launcherPaths('/root');
  assert.equal(p.installedFile.replace(/\\/g, '/'), '/root/client/installed.json');
  assert.equal(p.settingsFile.replace(/\\/g, '/'), '/root/launcher/settings.json');
});
