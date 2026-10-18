import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fingerprint, validateConfig } from './play-local.mjs';

test('build cache observes edits and deletions, but ignores generated output', () => {
  const dir = mkdtempSync(path.join(os.tmpdir(), 'hollowwake-play-cache-'));
  try {
    mkdirSync(path.join(dir, 'src/bin'), { recursive: true });
    writeFileSync(path.join(dir, 'src/file.cs'), 'first');
    const initial = fingerprint(dir, ['src/']);
    writeFileSync(path.join(dir, 'src/bin/output'), 'generated');
    assert.equal(fingerprint(dir, ['src/']), initial);
    writeFileSync(path.join(dir, 'src/file.cs'), 'edited');
    assert.notEqual(fingerprint(dir, ['src/']), initial);
    rmSync(path.join(dir, 'src/file.cs'));
    assert.notEqual(fingerprint(dir, ['src/']), initial);
  } finally { rmSync(dir, { recursive: true, force: true }); }
});

test('local launch rejects ambiguous ports and invalid account values', () => {
  const good = { client: 'browser', port: 7777, webPort: 7778, account: 'marcher', profile: 'playtest' };
  assert.equal(validateConfig(good), good);
  assert.throws(() => validateConfig({ ...good, webPort: 7777 }));
  assert.throws(() => validateConfig({ ...good, port: 0 }));
  assert.throws(() => validateConfig({ ...good, account: 'user&injected=1' }));
});
