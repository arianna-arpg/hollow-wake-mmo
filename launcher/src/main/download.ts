// THE VERIFIED DOWNLOAD. Streams an archive to disk, hashing as it goes, and keeps it only when its
// size and sha256 match the manifest. On any failure, a mismatch included, the partial file is
// deleted before the error leaves: nothing unverified is ever left on disk to be extracted or run.

import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, rename, rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FetchLike } from './http';

export class ChecksumError extends Error {
  override name = 'ChecksumError';
}

export interface DownloadOptions {
  url: string;
  headers: Record<string, string>;
  /** Final path; bytes land in dest + '.part' until verified. */
  dest: string;
  expectedSize: number;
  expectedSha256: string;
  fetchImpl: FetchLike;
  signal?: AbortSignal;
  /** No bytes for this long counts as a dead connection. */
  stallMs?: number;
  onProgress?: (received: number, total: number) => void;
}

export interface DownloadResult {
  bytes: number;
  sha256: string;
}

export async function downloadVerified(o: DownloadOptions): Promise<DownloadResult> {
  const part = `${o.dest}.part`;
  const stallMs = o.stallMs ?? 60_000;
  const stall = new AbortController();
  let stallTimer: NodeJS.Timeout | null = null;
  const beat = () => {
    if (stallTimer) {
      clearTimeout(stallTimer);
    }
    stallTimer = setTimeout(() => stall.abort(new Error(`the download stalled (no data for ${Math.round(stallMs / 1000)} s)`)), stallMs);
  };
  const signal = o.signal ? AbortSignal.any([o.signal, stall.signal]) : stall.signal;
  await mkdir(path.dirname(o.dest), { recursive: true });
  const file = createWriteStream(part);
  let fileError: Error | null = null;
  file.on('error', (error) => {
    fileError = error;
  });
  try {
    beat();
    const { chunks, total } = await openSource(o, signal);
    const hash = createHash('sha256');
    let received = 0;
    let lastReport = 0;
    for await (const chunk of chunks) {
      if (signal.aborted) {
        throw signal.reason instanceof Error ? signal.reason : new Error('cancelled');
      }
      beat();
      const bytes = chunk instanceof Uint8Array ? chunk : Buffer.from(chunk as string);
      received += bytes.byteLength;
      if (received > o.expectedSize) {
        throw new ChecksumError(`the download is larger than the ${o.expectedSize} bytes the manifest lists`);
      }
      hash.update(bytes);
      if (!file.write(bytes)) {
        await new Promise<void>((resolve) => file.once('drain', resolve));
      }
      if (fileError) {
        throw fileError;
      }
      const now = Date.now();
      if (o.onProgress && (now - lastReport > 100 || received === o.expectedSize)) {
        lastReport = now;
        o.onProgress(received, total || o.expectedSize);
      }
    }
    await new Promise<void>((resolve, reject) => file.end((error?: Error | null) => (error ? reject(error) : resolve())));
    if (fileError) {
      throw fileError;
    }
    if (received !== o.expectedSize) {
      throw new ChecksumError(`the download is ${received} bytes and the manifest lists ${o.expectedSize}`);
    }
    const sha256 = hash.digest('hex');
    if (sha256 !== o.expectedSha256.toLowerCase()) {
      throw new ChecksumError(
        `checksum mismatch: the download hashes to sha256 ${sha256.slice(0, 12)}... and the manifest lists ${o.expectedSha256.slice(0, 12)}...; the file was discarded`,
      );
    }
    await rm(o.dest, { force: true });
    await rename(part, o.dest);
    return { bytes: received, sha256 };
  } catch (error) {
    file.destroy();
    await new Promise((resolve) => file.close(() => resolve(null)));
    await rm(part, { force: true }).catch(() => undefined);
    if (signal.aborted && signal.reason instanceof Error && !(error instanceof ChecksumError)) {
      throw signal.reason;
    }
    throw error;
  } finally {
    if (stallTimer) {
      clearTimeout(stallTimer);
    }
  }
}

async function openSource(o: DownloadOptions, signal: AbortSignal): Promise<{ chunks: AsyncIterable<unknown>; total: number }> {
  const url = new URL(o.url);
  if (url.protocol === 'file:') {
    return { chunks: createReadStream(fileURLToPath(url), { signal }), total: o.expectedSize };
  }
  const response = await o.fetchImpl(url, { headers: o.headers, signal, redirect: 'follow' });
  if (!response.ok || !response.body) {
    throw new Error(`the download answered HTTP ${response.status}`);
  }
  const total = Number(response.headers.get('content-length')) || o.expectedSize;
  return { chunks: response.body as unknown as AsyncIterable<Uint8Array>, total };
}
