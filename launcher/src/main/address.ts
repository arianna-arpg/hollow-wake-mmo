// The server address field: host, host:port, [IPv6]:port or a bare IPv6 address. The launcher never
// connects to it; it only checks the shape and hands host and port to the client.

import { isIP } from 'node:net';

export const DEFAULT_ADDRESS = '127.0.0.1:7777';
export const DEFAULT_PORT = 7777;

export interface ServerAddress {
  host: string;
  port: number;
  /** host:port, with IPv6 hosts in brackets. */
  text: string;
}

export type AddressResult = { ok: true; address: ServerAddress } | { ok: false; error: string };

const HOST_LABEL = /^(?!-)[A-Za-z0-9-]{1,63}(?<!-)$/;

export function parseAddress(input: string, defaultPort = DEFAULT_PORT): AddressResult {
  const text = input.trim();
  if (!text) {
    return { ok: false, error: 'Enter a server address, such as 127.0.0.1:7777.' };
  }
  let host = text;
  let portText: string | null = null;
  const bracketed = /^\[([^\]]+)\](?::(.*))?$/.exec(text);
  if (bracketed) {
    host = bracketed[1] ?? '';
    portText = bracketed[2] ?? null;
    if (isIP(host) !== 6) {
      return { ok: false, error: `${host} is not an IPv6 address.` };
    }
  } else if ((text.match(/:/g) ?? []).length === 1) {
    const colon = text.lastIndexOf(':');
    host = text.slice(0, colon);
    portText = text.slice(colon + 1);
  }
  if (!isHost(host)) {
    return { ok: false, error: `${host || text} is not a host name or IP address.` };
  }
  let port = defaultPort;
  if (portText !== null) {
    if (!/^\d{1,5}$/.test(portText) || Number(portText) < 1 || Number(portText) > 65535) {
      return { ok: false, error: `${portText || '(empty)'} is not a port between 1 and 65535.` };
    }
    port = Number(portText);
  }
  const shown = isIP(host) === 6 ? `[${host}]` : host;
  return { ok: true, address: { host, port, text: `${shown}:${port}` } };
}

function isHost(host: string): boolean {
  if (isIP(host)) {
    return true;
  }
  if (host.length === 0 || host.length > 253 || /^[\d.]+$/.test(host)) {
    return false;
  }
  return host.replace(/\.$/, '').split('.').every((label) => HOST_LABEL.test(label));
}
