// Safe extraction of a verified client archive. yauzl already refuses absolute names and ".."
// segments; every target path is checked again against the destination, symbolic links are refused,
// and Unix permission bits recorded in the archive are restored so the Linux executable stays
// executable.

import { createWriteStream } from 'node:fs';
import { chmod, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { pipeline } from 'node:stream/promises';
import yauzl from 'yauzl';

export interface ExtractResult {
  files: number;
  bytes: number;
}

const S_IFMT = 0o170000;
const S_IFLNK = 0o120000;
const S_IFDIR = 0o040000;
/** "Version made by" high byte for archives written on Unix, where external attributes carry a mode. */
const MADE_BY_UNIX = 3;

export async function extractZip(
  zipPath: string,
  destDir: string,
  o: { signal?: AbortSignal; onProgress?: (files: number, bytes: number) => void } = {},
): Promise<ExtractResult> {
  const root = path.resolve(destDir);
  await mkdir(root, { recursive: true });
  const zip = await yauzl.openPromise(zipPath, { validateEntrySizes: true, strictFileNames: false });
  let files = 0;
  let bytes = 0;
  try {
    for await (const entry of zip.eachEntry()) {
      if (o.signal?.aborted) {
        throw o.signal.reason instanceof Error ? o.signal.reason : new Error('cancelled');
      }
      const name = entry.fileName;
      const target = path.resolve(root, ...name.split('/').filter((part) => part.length > 0));
      const relative = path.relative(root, target);
      if (relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
        throw new Error(`the archive entry ${JSON.stringify(name)} points outside the install folder`);
      }
      const unixMode = entry.versionMadeBy >> 8 === MADE_BY_UNIX ? (entry.externalFileAttributes >>> 16) & 0xffff : 0;
      if ((unixMode & S_IFMT) === S_IFLNK) {
        throw new Error(`the archive entry ${JSON.stringify(name)} is a symbolic link, which the launcher does not install`);
      }
      if (name.endsWith('/') || (unixMode & S_IFMT) === S_IFDIR) {
        await mkdir(target, { recursive: true });
        continue;
      }
      if (relative === '') {
        throw new Error(`the archive entry ${JSON.stringify(name)} has no file name`);
      }
      if (entry.isEncrypted()) {
        throw new Error(`the archive entry ${JSON.stringify(name)} is encrypted`);
      }
      await mkdir(path.dirname(target), { recursive: true });
      const input = await zip.openReadStreamPromise(entry);
      await pipeline(input, createWriteStream(target), { signal: o.signal });
      // Recorded permissions, minus group and world write (an archive made on Windows says 0666).
      const permissions = unixMode & 0o755;
      if (permissions && process.platform !== 'win32') {
        await chmod(target, permissions);
      }
      files++;
      bytes += entry.uncompressedSize;
      o.onProgress?.(files, bytes);
    }
  } finally {
    zip.close();
  }
  return { files, bytes };
}
