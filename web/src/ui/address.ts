/** Where the game socket is, and how the login panel's fields turn into a WebSocket URL. */

export interface ServerAddress {
  host: string;
  port: number;
  secure: boolean;
  path: string;
}

export function defaultPort(secure: boolean): number {
  return secure ? 443 : 80;
}

export function socketUrl(a: ServerAddress): string {
  const scheme = a.secure ? 'wss' : 'ws';
  const host = a.host.includes(':') && !a.host.startsWith('[') ? `[${a.host}]` : a.host;
  const port = a.port > 0 && a.port !== defaultPort(a.secure) ? `:${a.port}` : '';
  const path = a.path.startsWith('/') ? a.path : `/${a.path}`;
  return `${scheme}://${host}${port}${path}`;
}

/**
 * The host field also takes a whole address (ws://, wss://, http:// or https://, with port and path), which is
 * what a Codespace or a shared link gives you; anything it names overrides the other fields.
 */
export function parseServerInput(text: string, fallback: ServerAddress): ServerAddress {
  const trimmed = text.trim();
  if (!trimmed.includes('://')) {
    return { ...fallback, host: trimmed };
  }
  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return { ...fallback, host: trimmed };
  }
  const secure = url.protocol === 'wss:' || url.protocol === 'https:';
  const port = url.port ? Number(url.port) : defaultPort(secure);
  const path = url.pathname && url.pathname !== '/' ? url.pathname : fallback.path;
  return { host: url.hostname.replace(/^\[|\]$/g, ''), port, secure, path };
}

/** The page's own origin as a server address (used when a game server serves the page). */
export function sameOrigin(location: Pick<Location, 'protocol' | 'hostname' | 'port'>, path: string): ServerAddress {
  const secure = location.protocol === 'https:';
  return { host: location.hostname || 'localhost', port: location.port ? Number(location.port) : defaultPort(secure), secure, path };
}
