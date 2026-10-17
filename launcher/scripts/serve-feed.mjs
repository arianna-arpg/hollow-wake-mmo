#!/usr/bin/env node
// Serves a folder over HTTP on loopback so the launcher can install from it, the way a tester tries
// a build without publishing a release:
//
//   node scripts/serve-feed.mjs FOLDER [--port 8787] [--rate KB_PER_SECOND]
//   HOLLOWWAKE_FEED=http://127.0.0.1:8787/manifest.json npm start
//
// FOLDER holds manifest.json and the archives it names (scripts/package-client.sh writes exactly
// that). --rate slows responses down, to watch the progress bar or try cancelling.

import { createReadStream, statSync } from 'node:fs';
import { createServer } from 'node:http';
import path from 'node:path';

const args = process.argv.slice(2);
const folder = path.resolve(args.find((a) => !a.startsWith('--') && !/^\d+$/.test(a)) ?? '.');
const option = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] ? Number(args[i + 1]) : fallback;
};
const port = option('--port', 8787);
const rate = option('--rate', 0) * 1024;

const server = createServer((req, res) => {
  const name = decodeURIComponent(new URL(req.url ?? '/', 'http://x').pathname).replace(/^\/+/, '');
  const file = path.join(folder, name);
  let stat;
  try {
    stat = path.relative(folder, file).startsWith('..') ? null : statSync(file);
  } catch {
    stat = null;
  }
  if (!stat || !stat.isFile()) {
    res.writeHead(404).end();
    console.log(`404 ${name}`);
    return;
  }
  console.log(`200 ${name} (${stat.size} bytes)`);
  res.writeHead(200, { 'content-length': String(stat.size), 'content-type': name.endsWith('.json') ? 'application/json' : 'application/octet-stream' });
  const stream = createReadStream(file, { highWaterMark: 64 * 1024 });
  if (!rate) {
    stream.pipe(res);
    return;
  }
  stream.on('data', (chunk) => {
    stream.pause();
    res.write(chunk);
    setTimeout(() => stream.resume(), (chunk.length / rate) * 1000);
  });
  stream.on('end', () => res.end());
  res.on('close', () => stream.destroy());
});

server.listen(port, '127.0.0.1', () => {
  console.log(`serving ${folder} at http://127.0.0.1:${port}/ ${rate ? `(${rate / 1024} KB/s)` : ''}`);
  console.log(`HOLLOWWAKE_FEED=http://127.0.0.1:${port}/manifest.json`);
});
