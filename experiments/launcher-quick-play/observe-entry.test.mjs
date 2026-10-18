import assert from 'node:assert/strict';
import { test } from 'node:test';
import { observeEntry } from './observe-entry.mjs';

const entry = 'QUICKPLAY ENTRY: account stage ready; awaiting player choice\n';
function course(logAt, options = {}) {
  let time = 0;
  const readings = [];
  return { readings, run: () => observeEntry(() => { readings.push(time); return logAt(time); }, {
    timeoutMs: 6000, observeMs: 3000, pollMs: 150, now: () => time, wait: async ms => { time += ms; }, ...options,
  }) };
}

test('keeps observing after ENTRY, then accepts a client still waiting in Mu', async () => {
  const c = course(t => t < 300 ? '' : entry);
  assert.equal(await c.run(), entry);
  assert.equal(c.readings.at(-1), 3300);
});

for (const marker of ['QUICKPLAY CROSSING: preparing character entry', 'QUICKPLAY READY: entered the world; Crossing complete']) {
  test(`rejects ${marker.split(':')[0]} arriving in a later read`, async () => {
    const c = course(t => entry + (t >= 1500 ? marker : ''));
    await assert.rejects(c.run(), /must keep waiting in Mu/);
    assert.equal(c.readings.at(-1), 1500);
  });
}

test('checks the final observation read instead of accepting at the boundary unseen', async () => {
  await assert.rejects(course(t => entry + (t >= 3000 ? 'QUICKPLAY CROSSING' : '')).run(), /must keep waiting in Mu/);
});

test('a late ENTRY still gets the full post-entry observation window', async () => {
  const c = course(t => t >= 5850 ? entry : '');
  await c.run(); assert.equal(c.readings.at(-1), 8850);
});

test('missing ENTRY fails within the bounded startup timeout', async () => {
  const c = course(() => 'connecting\n');
  await assert.rejects(c.run(), /before timeout/);
  assert.equal(c.readings.at(-1), 6000);
});
