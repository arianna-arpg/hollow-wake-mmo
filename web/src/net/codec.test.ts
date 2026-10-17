import { describe, expect, it } from 'vitest';
import { decode, encode } from './codec';
import { MESSAGE_TYPES, type Message } from './protocol';

const text = (bytes: Uint8Array) => new TextDecoder().decode(bytes);

describe('codec', () => {
  it('writes the discriminator first, as the server reader requires', () => {
    const json = text(encode({ x: 1, y: 2, t: 'move_dir' } as unknown as Message));
    expect(json).toBe('{"t":"move_dir","x":1,"y":2}');
  });

  it('round-trips a message', () => {
    const message: Message = { t: 'use_weave', slot: 2, target: 42, x: 1.5, y: -2, z: 3, dir_x: 0.5, dir_y: -0.5 };
    expect(decode(encode(message))).toEqual(message);
  });

  it('keeps 64-bit integers past 2^53 exact as strings and leaves safe ones as numbers', () => {
    const decoded = decode('{"t":"entity_despawn","key":9007199254740993}');
    expect(decoded).toEqual({ t: 'entity_despawn', key: '9007199254740993' });
    expect(decode('{"t":"entity_despawn","key":4294969829}')).toEqual({ t: 'entity_despawn', key: 4294969829 });
  });

  it('does not turn long fractions into strings', () => {
    const decoded = decode('{"t":"pong","nonce":1,"server_time":1.2345678901234567}') as { server_time: unknown };
    expect(typeof decoded.server_time).toBe('number');
  });

  it('refuses things that are not messages', () => {
    expect(() => decode('[1,2]')).toThrow();
    expect(() => decode('{"x":1}')).toThrow();
  });

  it('knows every discriminator once', () => {
    expect(new Set(MESSAGE_TYPES).size).toBe(MESSAGE_TYPES.length);
    expect(MESSAGE_TYPES).toContain('chunk_data');
    expect(MESSAGE_TYPES).toContain('request_chunk');
  });
});
