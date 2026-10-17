// The news panel reads launcher/news.json from the repository. Whatever arrives is reduced to plain
// strings of bounded length and https links before the page sees it, so a bad or hostile file can
// only ever show text. Offline, the last copy that loaded is shown; with no copy yet, the news that
// shipped inside the launcher.

import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { describeError, getText, type FetchLike } from './http';

export interface News {
  motd: string | null;
  items: NewsItemView[];
}

export interface NewsSource {
  /** Null skips the network (offline mode) and goes straight to the cached or bundled copy. */
  url: URL | null;
  /** From newsRequest() in feed.ts, which decides whether the token goes along. */
  headers: Record<string, string>;
  cacheFile: string;
  bundledFile: string;
  timeoutMs: number;
  fetchImpl: FetchLike;
}

const MAX_ITEMS = 12;

export function sanitizeNews(data: unknown): News {
  const record = typeof data === 'object' && data !== null && !Array.isArray(data) ? (data as Record<string, unknown>) : {};
  const items: NewsItemView[] = [];
  for (const raw of Array.isArray(record['items']) ? record['items'] : []) {
    if (items.length >= MAX_ITEMS || typeof raw !== 'object' || raw === null) {
      continue;
    }
    const item = raw as Record<string, unknown>;
    const title = text(item['title'], 120);
    const body = text(item['body'], 1200);
    if (!title && !body) {
      continue;
    }
    const date = typeof item['date'] === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(item['date']) ? item['date'] : null;
    items.push({ date, title: title ?? '', body: body ?? '', url: httpsUrl(item['url']) });
  }
  return { motd: text(record['motd'], 400), items };
}

export async function loadNews(source: NewsSource): Promise<NewsView> {
  try {
    if (!source.url) {
      throw new Error('offline mode');
    }
    const response = await getText(source.url, { headers: source.headers, timeoutMs: source.timeoutMs }, source.fetchImpl);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const news = sanitizeNews(JSON.parse(response.text));
    await mkdir(path.dirname(source.cacheFile), { recursive: true });
    await writeFile(source.cacheFile, `${JSON.stringify({ fetchedAt: new Date().toISOString(), news }, null, 2)}\n`, 'utf8');
    return { status: 'live', ...news, note: null };
  } catch (error) {
    const why = describeError(error);
    try {
      const cached = JSON.parse(await readFile(source.cacheFile, 'utf8')) as { fetchedAt?: string; news?: unknown };
      const when = typeof cached.fetchedAt === 'string' ? cached.fetchedAt.slice(0, 10) : 'earlier';
      return { status: 'cached', ...sanitizeNews(cached.news), note: `News could not be refreshed (${why}); showing the copy from ${when}.` };
    } catch {
      // No cache yet: fall through to the copy that shipped with the launcher.
    }
    try {
      const bundled = sanitizeNews(JSON.parse(await readFile(source.bundledFile, 'utf8')));
      return { status: 'bundled', ...bundled, note: `News could not be loaded (${why}); showing the news this launcher shipped with.` };
    } catch {
      return { status: 'bundled', motd: null, items: [], note: `News could not be loaded (${why}).` };
    }
  }
}

function text(value: unknown, max: number): string | null {
  if (typeof value !== 'string') {
    return null;
  }
  // Control characters out, whitespace runs kept as written (bodies may hold paragraph breaks).
  const cleaned = value.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g, '').trim();
  if (!cleaned) {
    return null;
  }
  return cleaned.length > max ? `${cleaned.slice(0, max - 3)}...` : cleaned;
}

function httpsUrl(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }
  try {
    const url = new URL(value);
    return url.protocol === 'https:' ? url.href : null;
  } catch {
    return null;
  }
}
