// Test helpers: a minimal zip writer (so tests build their own archives, including hostile ones),
// local HTTP servers that record what reached them, and temporary folders.

import { createHash } from 'node:crypto';
import { mkdtempSync, rmSync } from 'node:fs';
import { createServer, type IncomingHttpHeaders, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { crc32, deflateRawSync } from 'node:zlib';

export interface ZipEntry {
  name: string;
  data?: string | Buffer;
  /** Unix mode bits (permissions and type); written with "made by Unix" so extractors honour them. */
  mode?: number;
}

/** Build a zip archive in memory. Directories are entries whose names end with '/'. */
export function makeZip(entries: ZipEntry[]): Buffer {
  const locals: Buffer[] = [];
  const centrals: Buffer[] = [];
  let offset = 0;
  for (const entry of entries) {
    const name = Buffer.from(entry.name, 'utf8');
    const isDir = entry.name.endsWith('/');
    const raw = Buffer.isBuffer(entry.data) ? entry.data : Buffer.from(entry.data ?? '', 'utf8');
    const deflate = !isDir && raw.length > 0;
    const body = deflate ? deflateRawSync(raw) : raw;
    const crc = crc32(raw);
    const method = deflate ? 8 : 0;
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(method, 8);
    local.writeUInt16LE(0, 10);
    local.writeUInt16LE(0x21, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(body.length, 18);
    local.writeUInt32LE(raw.length, 22);
    local.writeUInt16LE(name.length, 26);
    local.writeUInt16LE(0, 28);
    locals.push(local, name, body);

    const mode = entry.mode ?? (isDir ? 0o040755 : 0o100644);
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE((3 << 8) | 20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(method, 10);
    central.writeUInt16LE(0, 12);
    central.writeUInt16LE(0x21, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(body.length, 20);
    central.writeUInt32LE(raw.length, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt16LE(0, 30);
    central.writeUInt16LE(0, 32);
    central.writeUInt16LE(0, 34);
    central.writeUInt16LE(0, 36);
    central.writeUInt32LE(((mode << 16) | (isDir ? 0x10 : 0)) >>> 0, 38);
    central.writeUInt32LE(offset, 42);
    centrals.push(central, name);
    offset += local.length + name.length + body.length;
  }
  const directory = Buffer.concat(centrals);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, directory, end]);
}

export function sha256(data: Buffer | string): string {
  return createHash('sha256').update(data).digest('hex');
}

export interface Recorded {
  method: string;
  url: string;
  headers: IncomingHttpHeaders;
}

export interface TestServer {
  url: string;
  requests: Recorded[];
  close(): Promise<void>;
}

export type Handler = (req: IncomingMessage, res: ServerResponse, server: TestServer) => void;

/** An HTTP server on a free loopback port that records every request's URL and headers. */
export async function serve(handler: Handler): Promise<TestServer> {
  const requests: Recorded[] = [];
  const server = createServer();
  const self: TestServer = {
    url: '',
    requests,
    close: () => new Promise((resolve) => {
      server.closeAllConnections();
      server.close(() => resolve());
    }),
  };
  server.on('request', (req, res) => {
    requests.push({ method: req.method ?? 'GET', url: req.url ?? '', headers: req.headers });
    handler(req, res, self);
  });
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  self.url = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  return self;
}

export function json(res: ServerResponse, status: number, body: unknown, headers: Record<string, string> = {}): void {
  res.writeHead(status, { 'content-type': 'application/json', ...headers });
  res.end(JSON.stringify(body));
}

export function bytes(res: ServerResponse, data: Buffer): void {
  res.writeHead(200, { 'content-type': 'application/octet-stream', 'content-length': String(data.length) });
  res.end(data);
}

export function tempDir(prefix = 'hwl-test-'): { dir: string; cleanup: () => void } {
  const dir = mkdtempSync(path.join(os.tmpdir(), prefix));
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) };
}
