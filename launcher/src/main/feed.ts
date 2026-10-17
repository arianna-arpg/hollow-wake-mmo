// THE RELEASE FEED. Finds the build this launcher should install for a channel and returns its
// manifest plus where to download its archives. Pure: no Electron, fetch is handed in, so the tests
// drive it against a local server.
//
//   stable   GET /repos/{owner}/{repo}/releases/latest. By GitHub's definition this is never a
//            draft and never a pre-release.
//   nightly  GET /repos/{owner}/{repo}/releases. The newest published release by version,
//            pre-releases included, so a stable cut above the nightlies wins there too.
//   override HOLLOWWAKE_FEED names a manifest.json directly (a path, file: or http(s) URL); its
//            archives resolve beside it. GitHub is not asked at all.
//
// A release counts only when it carries a fully uploaded manifest.json. A private repository answers
// 404 to a request without a token; that case gets its own explanation instead of "no releases".

import { getText, isNetworkError, describeError, toUrl, type FetchLike } from './http';
import { ManifestError, parseManifest, type Manifest } from './manifest';
import { compareVersions, parseVersion } from './version';

export const MANIFEST_ASSET = 'manifest.json';

export interface FeedConfig {
  owner: string;
  repo: string;
  /** REST root; https://api.github.com unless HOLLOWWAKE_GITHUB_API points elsewhere. */
  api: string;
  /** HOLLOWWAKE_FEED: a manifest to read instead of asking GitHub. */
  override: string | null;
  token: string | null;
  userAgent: string;
  timeoutMs: number;
}

/** How to fetch one file: the URL plus the headers it needs (auth only ever goes to the API host). */
export interface Download {
  url: string;
  headers: Record<string, string>;
}

export interface FeedRelease {
  manifest: Manifest;
  tag: string | null;
  pageUrl: string | null;
  publishedAt: string | null;
  prerelease: boolean;
  /** Where each archive the manifest names can be downloaded from. */
  archives: Record<string, Download>;
}

export type FeedErrorKind = 'offline' | 'private' | 'auth' | 'rate-limit' | 'no-release' | 'bad-release' | 'http';

export class FeedError extends Error {
  override name = 'FeedError';
  constructor(
    readonly kind: FeedErrorKind,
    message: string,
    readonly hint: string | null = null,
    readonly status = 0,
  ) {
    super(message);
  }
}

interface GitHubAsset {
  id: number;
  name: string;
  url: string;
  browser_download_url: string;
  state?: string;
  size?: number;
}

interface GitHubRelease {
  tag_name: string;
  html_url?: string;
  published_at?: string | null;
  draft?: boolean;
  prerelease?: boolean;
  assets?: GitHubAsset[];
}

/** Resolve the build to offer on a channel. Throws FeedError with a player-facing message. */
export async function resolveRelease(cfg: FeedConfig, channel: Channel, fetchImpl: FetchLike): Promise<FeedRelease> {
  if (cfg.override) {
    return resolveOverride(cfg, fetchImpl);
  }
  const releases = await listReleases(cfg, channel, fetchImpl);
  const candidates = releases
    .filter((r) => !r.draft && (channel === 'nightly' || !r.prerelease))
    .map((r) => ({ release: r, version: parseVersion(r.tag_name), manifest: findAsset(r, MANIFEST_ASSET) }))
    .filter((c): c is { release: GitHubRelease; version: string; manifest: GitHubAsset } => c.version !== null && c.manifest !== null)
    .sort((a, b) => compareVersions(b.version, a.version));
  const best = candidates[0];
  if (!best) {
    throw new FeedError(
      'no-release',
      channel === 'stable' ? 'No stable release with a game build has been published yet.' : 'No release with a game build has been published yet.',
      channel === 'stable' ? 'Switch to the nightly channel to try pre-releases.' : null,
    );
  }
  const text = await fetchFile(cfg, assetDownload(cfg, best.manifest), fetchImpl, 'manifest.json');
  const manifest = parseManifestText(text, best.release.tag_name);
  const archives: Record<string, Download> = {};
  for (const [key, build] of Object.entries(manifest.platforms)) {
    const asset = findAsset(best.release, build.file);
    if (asset) {
      archives[key] = assetDownload(cfg, asset);
    } else if (build.url) {
      archives[key] = { url: build.url, headers: {} };
    }
  }
  return {
    manifest,
    tag: best.release.tag_name,
    pageUrl: best.release.html_url ?? null,
    publishedAt: best.release.published_at ?? manifest.publishedAt,
    prerelease: best.release.prerelease === true,
    archives,
  };
}

async function resolveOverride(cfg: FeedConfig, fetchImpl: FetchLike): Promise<FeedRelease> {
  const manifestUrl = toUrl(cfg.override ?? '');
  let response;
  try {
    response = await getText(manifestUrl, { headers: { 'user-agent': cfg.userAgent }, timeoutMs: cfg.timeoutMs }, fetchImpl);
  } catch (error) {
    throw new FeedError('offline', `The feed override ${manifestUrl.href} could not be read: ${describeError(error)}.`, 'Check HOLLOWWAKE_FEED.');
  }
  if (!response.ok) {
    throw new FeedError('http', `The feed override ${manifestUrl.href} answered HTTP ${response.status}.`, 'Check HOLLOWWAKE_FEED.', response.status);
  }
  const manifest = parseManifestText(response.text, null);
  const archives: Record<string, Download> = {};
  for (const [key, build] of Object.entries(manifest.platforms)) {
    archives[key] = { url: (build.url ? toUrl(build.url) : toUrl(build.file, manifestUrl)).href, headers: {} };
  }
  return { manifest, tag: null, pageUrl: null, publishedAt: manifest.publishedAt, prerelease: manifest.channel === 'nightly', archives };
}

async function listReleases(cfg: FeedConfig, channel: Channel, fetchImpl: FetchLike): Promise<GitHubRelease[]> {
  const base = `${cfg.api}/repos/${cfg.owner}/${cfg.repo}`;
  const url = channel === 'stable' ? `${base}/releases/latest` : `${base}/releases?per_page=30`;
  const response = await apiGet(cfg, url, fetchImpl);
  if (response.status === 404) {
    // Either the repository has no stable release yet, or this caller cannot see the repository at
    // all, which is what a private repository looks like without a token. Asking for the repository
    // itself tells the two apart.
    const repo = await apiGet(cfg, base, fetchImpl);
    if (repo.status === 404) {
      throw cfg.token
        ? new FeedError('auth', `The GitHub token cannot see ${cfg.owner}/${cfg.repo}.`, 'Use a token with read access to the repository (Contents: read).', 404)
        : new FeedError(
            'private',
            `GitHub shows no release feed for ${cfg.owner}/${cfg.repo} to this launcher.`,
            'The repository is probably private. Add a GitHub token under Settings or set HOLLOWWAKE_GITHUB_TOKEN.',
            404,
          );
    }
    return [];
  }
  const body = parseJson(response.text, 'the release list');
  if (Array.isArray(body)) {
    return body as GitHubRelease[];
  }
  return body && typeof body === 'object' ? [body as GitHubRelease] : [];
}

async function apiGet(cfg: FeedConfig, url: string, fetchImpl: FetchLike) {
  let response;
  try {
    response = await getText(new URL(url), { headers: apiHeaders(cfg, 'application/vnd.github+json'), timeoutMs: cfg.timeoutMs }, fetchImpl);
  } catch (error) {
    if (isNetworkError(error)) {
      throw new FeedError('offline', `GitHub could not be reached: ${describeError(error)}.`, 'Check the connection. Play still works with the installed build.');
    }
    throw new FeedError('http', `The GitHub request failed: ${describeError(error)}.`);
  }
  checkStatus(cfg, response.status, response.headers);
  return response;
}

function checkStatus(cfg: FeedConfig, status: number, headers: Headers): void {
  if (status === 401) {
    throw new FeedError('auth', 'GitHub rejected the token (expired, revoked or mistyped).', 'Replace or clear the token under Settings.', status);
  }
  if ((status === 403 || status === 429) && headers.get('x-ratelimit-remaining') === '0') {
    const reset = Number(headers.get('x-ratelimit-reset')) * 1000;
    const minutes = Number.isFinite(reset) && reset > Date.now() ? Math.ceil((reset - Date.now()) / 60_000) : 0;
    throw new FeedError(
      'rate-limit',
      `GitHub's request allowance for this network is used up${minutes ? `; it refills in about ${minutes} min` : ''}.`,
      cfg.token ? null : 'A GitHub token raises the allowance.',
      status,
    );
  }
  if (status === 403) {
    throw new FeedError('auth', 'GitHub refused access to the release feed (HTTP 403).', cfg.token ? 'The token may lack read access to the repository.' : null, status);
  }
  if (status >= 400 && status !== 404) {
    throw new FeedError('http', `GitHub answered HTTP ${status}.`, 'Try again in a moment.', status);
  }
}

function findAsset(release: GitHubRelease, name: string): GitHubAsset | null {
  for (const asset of release.assets ?? []) {
    if (asset && asset.name === name && (asset.state === undefined || asset.state === 'uploaded')) {
      return asset;
    }
  }
  return null;
}

/**
 * Without a token, the public download URL (served by github.com, outside the API allowance). With a
 * token, the API asset endpoint, which also works for private repositories; GitHub redirects it to
 * storage and fetch drops the Authorization header on that cross-origin hop.
 */
function assetDownload(cfg: FeedConfig, asset: GitHubAsset): Download {
  if (cfg.token) {
    return { url: asset.url, headers: apiHeaders(cfg, 'application/octet-stream') };
  }
  return { url: asset.browser_download_url, headers: { 'user-agent': cfg.userAgent, accept: 'application/octet-stream' } };
}

async function fetchFile(cfg: FeedConfig, download: Download, fetchImpl: FetchLike, what: string): Promise<string> {
  let response;
  try {
    response = await getText(new URL(download.url), { headers: download.headers, timeoutMs: cfg.timeoutMs }, fetchImpl);
  } catch (error) {
    throw new FeedError(isNetworkError(error) ? 'offline' : 'http', `${what} could not be downloaded: ${describeError(error)}.`);
  }
  if (!response.ok) {
    throw new FeedError('http', `${what} answered HTTP ${response.status}.`, null, response.status);
  }
  return response.text;
}

/**
 * Where the news panel reads launcher/news.json. Without a token, the raw file on
 * raw.githubusercontent.com; with one, the contents API on the API host, which serves private
 * repositories and keeps the token on the one host it is ever sent to. HOLLOWWAKE_NEWS overrides both.
 */
export function newsRequest(cfg: FeedConfig, override: string | null, branch = 'main'): Download {
  if (override) {
    return { url: toUrl(override).href, headers: { 'user-agent': cfg.userAgent } };
  }
  if (cfg.token) {
    return {
      url: `${cfg.api}/repos/${cfg.owner}/${cfg.repo}/contents/launcher/news.json?ref=${encodeURIComponent(branch)}`,
      headers: apiHeaders(cfg, 'application/vnd.github.raw+json'),
    };
  }
  return {
    url: `https://raw.githubusercontent.com/${cfg.owner}/${cfg.repo}/${encodeURIComponent(branch)}/launcher/news.json`,
    headers: { 'user-agent': cfg.userAgent, accept: 'application/json' },
  };
}

/** Headers for the REST API. The token is attached here and nowhere else. */
function apiHeaders(cfg: FeedConfig, accept: string): Record<string, string> {
  const headers: Record<string, string> = {
    accept,
    'user-agent': cfg.userAgent,
    'x-github-api-version': '2022-11-28',
  };
  if (cfg.token) {
    headers['authorization'] = `Bearer ${cfg.token}`;
  }
  return headers;
}

function parseManifestText(text: string, tag: string | null): Manifest {
  try {
    return parseManifest(parseJson(text, 'manifest.json'));
  } catch (error) {
    if (error instanceof ManifestError || error instanceof FeedError) {
      throw new FeedError('bad-release', `The release${tag ? ` ${tag}` : ''} has an unusable manifest: ${error.message}.`);
    }
    throw error;
  }
}

function parseJson(text: string, what: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    throw new FeedError('bad-release', `${what} is not valid JSON.`);
  }
}
