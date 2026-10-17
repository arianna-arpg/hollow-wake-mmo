// manifest.json is the contract between the release pipeline and the launcher: one per release,
// written by scripts/make-manifest.mjs, listing a client archive per platform with its size, its
// sha256 and the executable inside it. The launcher trusts nothing in it it has not checked here.

import { channelOfVersion, parseVersion } from './version';

/** The manifest schema this launcher reads. A newer schema means a newer launcher is needed. */
export const MANIFEST_SCHEMA = 1;

export interface PlatformBuild {
  /** Archive file name; on GitHub it is the release asset name, elsewhere it resolves beside the manifest. */
  file: string;
  /** Optional absolute location, for feeds that keep archives somewhere else. */
  url: string | null;
  size: number;
  sha256: string;
  /** The client executable, relative to the extracted archive. */
  executable: string;
}

export interface Manifest {
  schema: number;
  product: string;
  version: string;
  channel: Channel;
  commit: string | null;
  publishedAt: string | null;
  platforms: Record<string, PlatformBuild>;
}

export class ManifestError extends Error {
  override name = 'ManifestError';
}

/** The key a release uses for this machine, such as windows-x64 or linux-x64. */
export function platformKey(platform: string = process.platform, arch: string = process.arch): string {
  const os = platform === 'win32' ? 'windows' : platform === 'darwin' ? 'macos' : platform;
  return `${os}-${arch}`;
}

const FILE_NAME = /^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$/;
const SHA256 = /^[0-9a-f]{64}$/;
const PLATFORM = /^[a-z0-9]+-[a-z0-9_]+$/;

/** Validate parsed JSON as a manifest; throws ManifestError naming the first problem found. */
export function parseManifest(data: unknown): Manifest {
  if (!isRecord(data)) {
    throw new ManifestError('the manifest is not a JSON object');
  }
  const schema = data['schema'];
  if (typeof schema !== 'number' || !Number.isInteger(schema)) {
    throw new ManifestError('the manifest has no schema number');
  }
  if (schema > MANIFEST_SCHEMA) {
    throw new ManifestError(`the release uses manifest schema ${schema}, and this launcher reads schema ${MANIFEST_SCHEMA}: update the launcher`);
  }
  if (schema < 1) {
    throw new ManifestError(`manifest schema ${schema} is not valid`);
  }
  const version = parseVersion(data['version']);
  if (!version) {
    throw new ManifestError(`manifest version ${JSON.stringify(data['version'])} is not a semantic version`);
  }
  const channelText = data['channel'];
  let channel: Channel;
  if (channelText === undefined) {
    channel = channelOfVersion(version);
  } else if (channelText === 'stable' || channelText === 'nightly') {
    channel = channelText;
  } else {
    throw new ManifestError(`manifest channel ${JSON.stringify(channelText)} is neither stable nor nightly`);
  }
  const platformsData = data['platforms'];
  if (!isRecord(platformsData) || Object.keys(platformsData).length === 0) {
    throw new ManifestError('the manifest lists no platforms');
  }
  const platforms: Record<string, PlatformBuild> = {};
  for (const [key, value] of Object.entries(platformsData)) {
    if (!PLATFORM.test(key)) {
      throw new ManifestError(`platform key ${JSON.stringify(key)} is not of the form os-arch`);
    }
    platforms[key] = parsePlatform(key, value);
  }
  return {
    schema,
    product: typeof data['product'] === 'string' ? data['product'] : 'Hollow Wake MMO',
    version,
    channel,
    commit: typeof data['commit'] === 'string' && data['commit'] ? data['commit'] : null,
    publishedAt: typeof data['publishedAt'] === 'string' && data['publishedAt'] ? data['publishedAt'] : null,
    platforms,
  };
}

function parsePlatform(key: string, value: unknown): PlatformBuild {
  if (!isRecord(value)) {
    throw new ManifestError(`platform ${key} is not an object`);
  }
  const file = value['file'];
  if (typeof file !== 'string' || !FILE_NAME.test(file)) {
    throw new ManifestError(`platform ${key} names no valid archive file`);
  }
  const url = value['url'];
  if (url !== undefined && (typeof url !== 'string' || !/^(https?|file):\/\//i.test(url))) {
    throw new ManifestError(`platform ${key} has a url that is not http, https or file`);
  }
  const size = value['size'];
  if (typeof size !== 'number' || !Number.isSafeInteger(size) || size <= 0) {
    throw new ManifestError(`platform ${key} has no valid size`);
  }
  const sha256 = typeof value['sha256'] === 'string' ? value['sha256'].toLowerCase() : '';
  if (!SHA256.test(sha256)) {
    throw new ManifestError(`platform ${key} has no valid sha256`);
  }
  const executable = value['executable'];
  if (typeof executable !== 'string' || !isSafeRelativePath(executable)) {
    throw new ManifestError(`platform ${key} names no valid executable`);
  }
  return { file, url: typeof url === 'string' ? url : null, size, sha256, executable };
}

/** A relative path with forward slashes that cannot climb out of the folder it is resolved in. */
export function isSafeRelativePath(text: string): boolean {
  if (text.length === 0 || text.length > 260 || text.includes('\\') || text.includes('\0')) {
    return false;
  }
  if (text.startsWith('/') || /^[A-Za-z]:/.test(text)) {
    return false;
  }
  return text.split('/').every((part) => part !== '' && part !== '.' && part !== '..');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
