// settings.json under the launcher folder: the server address, the channel, Developer mode,
// automatic updates and an optional GitHub token. The token is encrypted with the operating system's
// key store when one is available (Electron safeStorage, handed in as a Cipher) and stored plain
// otherwise, which the page says.

import { randomBytes } from 'node:crypto';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { DEFAULT_ADDRESS, parseAddress } from './address';

export interface Settings {
  address: string;
  channel: Channel;
  developer: boolean;
  autoUpdate: boolean;
  token: StoredToken | null;
}

export interface StoredToken {
  encrypted: boolean;
  value: string;
}

export interface Cipher {
  available(): boolean;
  encrypt(text: string): string;
  decrypt(data: string): string;
}

export const DEFAULT_SETTINGS: Readonly<Settings> = Object.freeze({
  address: DEFAULT_ADDRESS,
  channel: 'stable',
  developer: false,
  autoUpdate: true,
  token: null,
});

export async function loadSettings(file: string): Promise<Settings> {
  let data: Record<string, unknown> = {};
  try {
    const parsed: unknown = JSON.parse(await readFile(file, 'utf8'));
    if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
      data = parsed as Record<string, unknown>;
    }
  } catch {
    // Missing or unreadable settings: start from the defaults.
  }
  const address = typeof data['address'] === 'string' && parseAddress(data['address']).ok ? data['address'] : DEFAULT_SETTINGS.address;
  const channel = data['channel'] === 'nightly' ? 'nightly' : 'stable';
  const tokenData = data['token'];
  let token: StoredToken | null = null;
  if (typeof tokenData === 'object' && tokenData !== null) {
    const t = tokenData as Record<string, unknown>;
    if (typeof t['value'] === 'string' && t['value']) {
      token = { encrypted: t['encrypted'] === true, value: t['value'] };
    }
  }
  return {
    address,
    channel,
    developer: data['developer'] === true,
    autoUpdate: data['autoUpdate'] !== false,
    token,
  };
}

export async function saveSettings(file: string, settings: Settings): Promise<void> {
  await mkdir(path.dirname(file), { recursive: true });
  const tmp = `${file}.${randomBytes(4).toString('hex')}.tmp`;
  await writeFile(tmp, `${JSON.stringify(settings, null, 2)}\n`, { encoding: 'utf8', mode: 0o600 });
  await rename(tmp, file);
}

export function sealToken(token: string, cipher: Cipher): StoredToken {
  return cipher.available() ? { encrypted: true, value: cipher.encrypt(token) } : { encrypted: false, value: token };
}

/** The token in clear, or null when it cannot be decrypted (another user, a reset key store). */
export function openToken(stored: StoredToken | null, cipher: Cipher): string | null {
  if (!stored) {
    return null;
  }
  if (!stored.encrypted) {
    return stored.value;
  }
  try {
    return cipher.available() ? cipher.decrypt(stored.value) : null;
  } catch {
    return null;
  }
}
