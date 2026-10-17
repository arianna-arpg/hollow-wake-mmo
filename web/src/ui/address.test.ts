import { describe, expect, it } from 'vitest';
import { parseServerInput, sameOrigin, socketUrl } from './address';

const base = { host: 'localhost', port: 7778, secure: false, path: '/ws' };

describe('server address', () => {
  it('builds socket urls, leaving out default ports', () => {
    expect(socketUrl(base)).toBe('ws://localhost:7778/ws');
    expect(socketUrl({ host: 'abc-7778.app.github.dev', port: 443, secure: true, path: '/ws' })).toBe('wss://abc-7778.app.github.dev/ws');
    expect(socketUrl({ host: '::1', port: 7778, secure: false, path: 'ws' })).toBe('ws://[::1]:7778/ws');
  });

  it('takes a pasted Codespace address apart', () => {
    expect(parseServerInput('https://abc-7778.app.github.dev/', base)).toEqual({ host: 'abc-7778.app.github.dev', port: 443, secure: true, path: '/ws' });
    expect(parseServerInput('wss://abc-7778.app.github.dev/ws', base)).toEqual({ host: 'abc-7778.app.github.dev', port: 443, secure: true, path: '/ws' });
    expect(parseServerInput('ws://10.0.0.5:9000/game', base)).toEqual({ host: '10.0.0.5', port: 9000, secure: false, path: '/game' });
  });

  it('treats a bare host as just the host', () => {
    expect(parseServerInput('  example.org ', base)).toEqual({ ...base, host: 'example.org' });
  });

  it('reads the page origin', () => {
    expect(sameOrigin({ protocol: 'https:', hostname: 'abc-7778.app.github.dev', port: '' }, '/ws')).toEqual({ host: 'abc-7778.app.github.dev', port: 443, secure: true, path: '/ws' });
    expect(sameOrigin({ protocol: 'http:', hostname: 'localhost', port: '7778' }, '/ws')).toEqual(base);
  });
});
