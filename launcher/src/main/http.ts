// Small helpers shared by the feed, the news and the downloader: one place that turns paths into
// URLs, reads file: URLs from disk (fetch has no file: support), and turns network failures into
// words a player can read.

import { readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

export type FetchLike = (input: string | URL, init?: RequestInit) => Promise<Response>;

/** A path or a URL from the environment or a manifest, as a URL. Relative input resolves against base. */
export function toUrl(text: string, base?: URL): URL {
  const trimmed = text.trim();
  if (/^(https?|file):/i.test(trimmed)) {
    return new URL(trimmed);
  }
  if (base) {
    return new URL(trimmed, base);
  }
  return pathToFileURL(path.resolve(trimmed));
}

export interface TextResponse {
  status: number;
  ok: boolean;
  text: string;
  headers: Headers;
}

/** GET a small text resource over http(s), or read it from disk for a file: URL. */
export async function getText(
  url: URL,
  init: { headers?: Record<string, string>; timeoutMs: number },
  fetchImpl: FetchLike,
): Promise<TextResponse> {
  if (url.protocol === 'file:') {
    try {
      const text = await readFile(fileURLToPath(url), 'utf8');
      return { status: 200, ok: true, text, headers: new Headers() };
    } catch (error) {
      if (errorCode(error) === 'ENOENT') {
        return { status: 404, ok: false, text: '', headers: new Headers() };
      }
      throw error;
    }
  }
  const response = await fetchImpl(url, {
    headers: init.headers,
    signal: AbortSignal.timeout(init.timeoutMs),
    redirect: 'follow',
  });
  const text = await response.text();
  return { status: response.status, ok: response.ok, text, headers: response.headers };
}

/** The error code behind a failed request or file operation (ENOTFOUND, ECONNREFUSED, ...), if any. */
export function errorCode(error: unknown): string | null {
  let current: unknown = error;
  for (let depth = 0; depth < 4 && current; depth++) {
    if (typeof current === 'object' && current !== null) {
      const code = (current as { code?: unknown }).code;
      if (typeof code === 'string') {
        return code;
      }
      current = (current as { cause?: unknown }).cause;
    } else {
      break;
    }
  }
  return null;
}

const NETWORK_CODES = new Set([
  'ENOTFOUND', 'EAI_AGAIN', 'ECONNREFUSED', 'ECONNRESET', 'ETIMEDOUT', 'EHOSTUNREACH',
  'ENETUNREACH', 'ENETDOWN', 'EPIPE', 'ECONNABORTED',
]);

/** True when an error means the request never got an answer: offline, DNS, refused, timed out. */
export function isNetworkError(error: unknown): boolean {
  if (error instanceof Error && (error.name === 'TimeoutError' || error.message === 'fetch failed')) {
    return true;
  }
  const code = errorCode(error);
  return code !== null && (NETWORK_CODES.has(code) || code.startsWith('UND_ERR_'));
}

/** A short description of why a request failed, for the status line and the log. */
export function describeError(error: unknown): string {
  if (error instanceof Error && error.name === 'TimeoutError') {
    return 'the request timed out';
  }
  if (error instanceof Error && error.name === 'AbortError') {
    return 'cancelled';
  }
  const code = errorCode(error);
  switch (code) {
    case 'ENOTFOUND':
    case 'EAI_AGAIN':
      return `the host could not be found (${code}); the computer may be offline`;
    case 'ECONNREFUSED':
      return 'the connection was refused (ECONNREFUSED)';
    case 'ECONNRESET':
      return 'the connection was reset (ECONNRESET)';
    case 'ETIMEDOUT':
    case 'UND_ERR_CONNECT_TIMEOUT':
    case 'UND_ERR_HEADERS_TIMEOUT':
      return `the connection timed out (${code})`;
    case 'ENOSPC':
      return 'the disk is full (ENOSPC)';
    case 'EACCES':
    case 'EPERM':
      return `access was denied (${code})`;
    case 'EBUSY':
      return 'a file is in use (EBUSY); close the game and try again';
    default:
      break;
  }
  if (error instanceof Error) {
    return code ? `${error.message} (${code})` : error.message;
  }
  return String(error);
}
