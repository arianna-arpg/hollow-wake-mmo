import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { parseManifest } from '../main/manifest';
import { sha256, tempDir } from './helpers';

// scripts/make-manifest.mjs (what the release workflow runs) and manifest.ts (what the launcher
// reads) must agree; this writes a manifest the way CI does and reads it back.

const SCRIPT = path.join(__dirname, '..', '..', 'scripts', 'make-manifest.mjs');

test('the release manifest writer and the launcher agree', () => {
  const t = tempDir();
  try {
    const win = path.join(t.dir, 'HollowWakeMMO-client-0.2.1-nightly.20261017.4-windows-x64.zip');
    const linux = path.join(t.dir, 'HollowWakeMMO-client-0.2.1-nightly.20261017.4-linux-x64.zip');
    writeFileSync(win, 'windows archive');
    writeFileSync(linux, 'linux archive!');
    const out = path.join(t.dir, 'manifest.json');
    const run = spawnSync(process.execPath, [
      SCRIPT, '--version', 'v0.2.1-nightly.20261017.4', '--commit', 'abc123', '--out', out,
      '--build', 'windows-x64', win, 'HollowWakeMMO.exe',
      '--build', 'linux-x64', linux, 'HollowWakeMMO.x86_64',
    ], { encoding: 'utf8' });
    assert.equal(run.status, 0, run.stderr);
    const manifest = parseManifest(JSON.parse(readFileSync(out, 'utf8')));
    assert.equal(manifest.version, '0.2.1-nightly.20261017.4');
    assert.equal(manifest.channel, 'nightly');
    assert.equal(manifest.commit, 'abc123');
    assert.deepEqual(manifest.platforms['windows-x64'], {
      file: path.basename(win), url: null, size: 15, sha256: sha256('windows archive'), executable: 'HollowWakeMMO.exe',
    });
    assert.equal(manifest.platforms['linux-x64']?.executable, 'HollowWakeMMO.x86_64');

    const bad = spawnSync(process.execPath, [SCRIPT, '--version', 'latest', '--out', out, '--build', 'windows-x64', win, 'a.exe'], { encoding: 'utf8' });
    assert.notEqual(bad.status, 0);
    assert.match(bad.stderr, /not a semantic version/);
  } finally {
    t.cleanup();
  }
});
