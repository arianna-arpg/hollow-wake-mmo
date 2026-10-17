#!/usr/bin/env node
// Writes a release's manifest.json: the version, the channel and, per platform, the client archive's
// file name, size, sha256 and the executable inside it. The launcher's src/main/manifest.ts reads
// exactly this shape (schema 1); the round trip is tested in src/test/manifest-writer.test.ts.
// No dependencies, so the release workflow runs it with plain node before any npm install.
//
//   node launcher/scripts/make-manifest.mjs --version 0.1.0 --channel stable --commit <sha> \
//     --build windows-x64 out/HollowWakeMMO-client-0.1.0-windows-x64.zip HollowWakeMMO.exe \
//     --build linux-x64 out/HollowWakeMMO-client-0.1.0-linux-x64.zip HollowWakeMMO.x86_64 \
//     --out out/manifest.json

import { createHash } from 'node:crypto';
import { createReadStream, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const options = { version: '', channel: '', commit: '', out: '', builds: [] };
for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (arg === '--build') {
    options.builds.push({ platform: args[i + 1], archive: args[i + 2], executable: args[i + 3] });
    i += 3;
  } else if (arg === '--version' || arg === '--channel' || arg === '--commit' || arg === '--out') {
    options[arg.slice(2)] = args[i + 1] ?? '';
    i += 1;
  } else {
    fail(`unknown argument ${arg}`);
  }
}

const SEMVER = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$/;
options.version = options.version.replace(/^v/, '');
if (!SEMVER.test(options.version)) fail(`--version ${options.version} is not a semantic version`);
if (!options.channel) options.channel = options.version.includes('-nightly') ? 'nightly' : 'stable';
if (options.channel !== 'stable' && options.channel !== 'nightly') fail('--channel must be stable or nightly');
if (!options.out) fail('--out is required');
if (options.builds.length === 0) fail('at least one --build PLATFORM ARCHIVE EXECUTABLE is required');

const platforms = {};
for (const build of options.builds) {
  if (!/^[a-z0-9]+-[a-z0-9_]+$/.test(build.platform ?? '')) fail(`platform ${build.platform} is not of the form os-arch`);
  if (!build.archive || !build.executable) fail(`--build ${build.platform} needs an archive and an executable`);
  if (build.executable.includes('\\') || build.executable.startsWith('/') || build.executable.split('/').includes('..')) {
    fail(`executable ${build.executable} must be a relative path with forward slashes`);
  }
  platforms[build.platform] = {
    file: path.basename(build.archive),
    size: statSync(build.archive).size,
    sha256: await sha256(build.archive),
    executable: build.executable,
  };
}

const manifest = {
  schema: 1,
  product: 'Hollow Wake MMO',
  version: options.version,
  channel: options.channel,
  commit: options.commit || null,
  publishedAt: new Date().toISOString(),
  platforms,
};
writeFileSync(options.out, `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`wrote ${options.out}: ${options.version} (${options.channel}) for ${Object.keys(platforms).join(', ')}`);

function sha256(file) {
  return new Promise((resolve, reject) => {
    const hash = createHash('sha256');
    createReadStream(file)
      .on('data', (chunk) => hash.update(chunk))
      .on('error', reject)
      .on('end', () => resolve(hash.digest('hex')));
  });
}

function fail(message) {
  console.error(`make-manifest: ${message}`);
  process.exit(1);
}
