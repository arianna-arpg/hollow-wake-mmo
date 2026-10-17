// THE INSTALL. Download, verify, extract into a staging folder beside the target, check the
// executable is there, then move it into place and record it in installed.json. Builds live side by
// side (client/<version>/), so an install never touches the build that is running; older builds are
// removed afterwards, best effort, and a locked one is simply left for next time.

import { randomBytes } from 'node:crypto';
import { constants } from 'node:fs';
import { access, chmod, mkdir, readdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { downloadVerified } from './download';
import { extractZip } from './extract';
import type { FeedRelease } from './feed';
import type { FetchLike } from './http';
import { isSafeRelativePath } from './manifest';
import type { LauncherPaths } from './paths';
import { parseVersion } from './version';

export interface InstalledRecord {
  version: string;
  channel: string;
  platform: string;
  /** Folder under client/ holding this build. */
  dir: string;
  executable: string;
  sha256: string;
  tag: string | null;
  installedAt: string;
}

export interface InstallProgress {
  phase: 'download' | 'verify' | 'extract' | 'finish';
  received: number;
  total: number;
}

export interface InstallOptions {
  release: FeedRelease;
  platform: string;
  paths: LauncherPaths;
  fetchImpl: FetchLike;
  signal?: AbortSignal;
  onProgress?: (p: InstallProgress) => void;
  log?: (text: string) => void;
  /** Remove older builds afterwards. Off while the game runs, since that build is still in use. */
  prune?: boolean;
}

export async function installRelease(o: InstallOptions): Promise<InstalledRecord> {
  const { manifest } = o.release;
  const build = manifest.platforms[o.platform];
  if (!build) {
    throw new Error(`release ${manifest.version} has no build for ${o.platform}`);
  }
  const download = o.release.archives[o.platform];
  if (!download) {
    throw new Error(`release ${manifest.version} lists ${build.file} but does not carry it`);
  }
  const log = o.log ?? (() => undefined);
  const archive = path.join(o.paths.downloadsDir, build.file);
  await mkdir(o.paths.clientDir, { recursive: true });

  log(`downloading ${build.file} (${build.size} bytes) from ${redact(download.url)}`);
  o.onProgress?.({ phase: 'download', received: 0, total: build.size });
  const result = await downloadVerified({
    url: download.url,
    headers: download.headers,
    dest: archive,
    expectedSize: build.size,
    expectedSha256: build.sha256,
    fetchImpl: o.fetchImpl,
    signal: o.signal,
    onProgress: (received, total) => o.onProgress?.({ phase: 'download', received, total }),
  });
  o.onProgress?.({ phase: 'verify', received: result.bytes, total: build.size });
  log(`verified ${build.file}: sha256 ${result.sha256}`);

  const dirName = manifest.version;
  const target = path.join(o.paths.clientDir, dirName);
  const staging = path.join(o.paths.clientDir, `.staging-${dirName}-${randomBytes(4).toString('hex')}`);
  try {
    o.onProgress?.({ phase: 'extract', received: 0, total: build.size });
    const extracted = await extractZip(archive, staging, {
      signal: o.signal,
      onProgress: (files) => o.onProgress?.({ phase: 'extract', received: files, total: 0 }),
    });
    log(`extracted ${extracted.files} files (${extracted.bytes} bytes)`);
    const exe = path.join(staging, ...build.executable.split('/'));
    try {
      await access(exe);
    } catch {
      throw new Error(`the archive does not contain its executable ${build.executable}`);
    }
    if (process.platform !== 'win32') {
      await chmod(exe, 0o755);
    }
    o.onProgress?.({ phase: 'finish', received: build.size, total: build.size });
    await removeDir(target);
    await rename(staging, target);
  } catch (error) {
    await rm(staging, { recursive: true, force: true }).catch(() => undefined);
    throw error;
  } finally {
    await rm(archive, { force: true }).catch(() => undefined);
  }

  const record: InstalledRecord = {
    version: manifest.version,
    channel: manifest.channel,
    platform: o.platform,
    dir: dirName,
    executable: build.executable,
    sha256: build.sha256,
    tag: o.release.tag,
    installedAt: new Date().toISOString(),
  };
  await writeJsonAtomic(o.paths.installedFile, record);
  log(`installed ${record.version} (${record.channel}) in ${target}`);
  if (o.prune !== false) {
    await pruneBuilds(o.paths, record.dir, log);
  }
  return record;
}

/** The installed build, or null when there is none or its files are gone. */
export async function readInstalled(paths: LauncherPaths, log?: (text: string) => void): Promise<InstalledRecord | null> {
  let data: unknown;
  try {
    data = JSON.parse(await readFile(paths.installedFile, 'utf8'));
  } catch {
    return null;
  }
  const r = data as Partial<InstalledRecord>;
  const version = parseVersion(r.version);
  if (!version || typeof r.dir !== 'string' || typeof r.executable !== 'string' || !isSafeRelativePath(r.dir) || !isSafeRelativePath(r.executable)) {
    log?.('installed.json is not usable; treating the client as not installed');
    return null;
  }
  const record: InstalledRecord = {
    version,
    channel: typeof r.channel === 'string' ? r.channel : 'stable',
    platform: typeof r.platform === 'string' ? r.platform : '',
    dir: r.dir,
    executable: r.executable,
    sha256: typeof r.sha256 === 'string' ? r.sha256 : '',
    tag: typeof r.tag === 'string' ? r.tag : null,
    installedAt: typeof r.installedAt === 'string' ? r.installedAt : '',
  };
  try {
    await access(executablePath(paths, record), constants.F_OK);
  } catch {
    log?.(`the installed build ${record.version} is missing its executable; treating the client as not installed`);
    return null;
  }
  return record;
}

export function executablePath(paths: LauncherPaths, record: InstalledRecord): string {
  return path.join(paths.clientDir, record.dir, ...record.executable.split('/'));
}

export function buildDir(paths: LauncherPaths, record: InstalledRecord): string {
  return path.join(paths.clientDir, record.dir);
}

/** Remove builds other than the current one and any staging left by an interrupted install. */
export async function pruneBuilds(paths: LauncherPaths, keep: string, log?: (text: string) => void): Promise<void> {
  let names: string[];
  try {
    names = await readdir(paths.clientDir);
  } catch {
    return;
  }
  for (const name of names) {
    if (name === keep || name === path.basename(paths.installedFile)) {
      continue;
    }
    try {
      await rm(path.join(paths.clientDir, name), { recursive: true, force: true, maxRetries: 2 });
      log?.(`removed old build folder ${name}`);
    } catch (error) {
      log?.(`could not remove ${name} yet (${(error as Error).message}); it will be retried`);
    }
  }
}

async function removeDir(dir: string): Promise<void> {
  try {
    await access(dir);
  } catch {
    return;
  }
  // Move it aside first so a half-deleted folder never sits at the target name.
  const trash = `${dir}.old-${randomBytes(4).toString('hex')}`;
  await rename(dir, trash);
  await rm(trash, { recursive: true, force: true, maxRetries: 2 }).catch(() => undefined);
}

async function writeJsonAtomic(file: string, value: unknown): Promise<void> {
  await mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.${randomBytes(4).toString('hex')}.tmp`;
  await writeFile(tmp, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
  await rename(tmp, file);
}

/** URLs for the log: no query string (signed storage URLs carry credentials there). */
export function redact(url: string): string {
  try {
    const u = new URL(url);
    return `${u.origin}${u.pathname}`;
  } catch {
    return url;
  }
}
