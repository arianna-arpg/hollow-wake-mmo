import type { Message } from './protocol';

/**
 * Message encoding, the browser side of MessageSerializer: JSON text, one message per binary WebSocket frame.
 * The server's polymorphic reader finds the "t" discriminator only when it comes first, so encode puts it there.
 */

const encoder = new TextEncoder();
const decoder = new TextDecoder();

export function encode(message: Message): Uint8Array<ArrayBuffer> {
  const { t, ...fields } = message;
  return encoder.encode(JSON.stringify({ t, ...fields })) as Uint8Array<ArrayBuffer>;
}

type Reviver = (this: unknown, key: string, value: unknown, context?: { source?: string }) => unknown;

/** With JSON.parse source access, integers past 2^53 keep their exact digits as strings (see Int64). */
const keepBigIntegers: Reviver = (_key, value, context) =>
  typeof value === 'number' && Number.isInteger(value) && !Number.isSafeInteger(value) && context?.source !== undefined && /^-?\d+$/.test(context.source)
    ? context.source
    : value;

export function decode(data: ArrayBuffer | ArrayBufferView | string): Message {
  const text = typeof data === 'string' ? data : decoder.decode(data);
  // Only a text with a run of 16 digits can hold an unsafe integer, so the reviver costs nothing otherwise.
  const parse = JSON.parse as (text: string, reviver?: Reviver) => unknown;
  const value = /\d{16}/.test(text) ? parse(text, keepBigIntegers) : JSON.parse(text);
  if (typeof value !== 'object' || value === null || typeof (value as { t?: unknown }).t !== 'string') {
    throw new Error('not a protocol message');
  }
  return value as Message;
}
