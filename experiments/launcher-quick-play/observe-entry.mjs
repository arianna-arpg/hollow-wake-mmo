import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';

/** Observe the log beyond ENTRY so a later automatic selection cannot pass as explicit Wake. */
export async function observeEntry(readLog, { timeoutMs = 60000, observeMs = 3000, pollMs = 150,
  now = () => performance.now(), wait = delay } = {}) {
  const deadline = now() + timeoutMs;
  let enteredAt = null;
  for (;;) {
    const log = await readLog();
    const time = now();
    assert.doesNotMatch(log, /QUICKPLAY CROSSING|QUICKPLAY READY/, 'Play must keep waiting in Mu until the player chooses Wake');
    if (enteredAt === null && log.includes('QUICKPLAY ENTRY: account stage ready; awaiting player choice')) enteredAt = time;
    if (enteredAt !== null && time - enteredAt >= observeMs) return log;
    if (enteredAt === null && time >= deadline) assert.fail('Client did not reach the account entry stage before timeout');
    await wait(pollMs);
  }
}
