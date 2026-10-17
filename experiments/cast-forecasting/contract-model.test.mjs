import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { ForecastView, directionAfterCommit, displayThread, progress } from './contract-model.mjs';

const config = JSON.parse(readFileSync(new URL('./forecast.example.json', import.meta.url)));
const cast = (overrides = {}) => ({ zoneEpoch: 'arena-a', sequence: 1, revision: 1, castId: '7',
  startsAt: 10, endsAt: 11, validUntil: 11, phase: 'preparing', emissionOrdinal: 0, ...overrides });

test('late start uses server elapsed time, never a new full-duration countdown', () => {
  const view = new ForecastView('arena-a');
  view.accept(cast());
  assert.equal(view.state('7', 10.75, .25).progress, .75);
  assert.equal(progress(cast(), 9), 0);
  assert.equal(progress(cast(), 12), 1);
});
test('deadline and expiry never imply release, impact or a known cancellation', () => {
  const view = new ForecastView('arena-a'); view.accept(cast());
  assert.equal(view.state('7', 11, .25).progress, 1);
  assert.equal(view.state('7', 11.3, .25).phase, 'unknown_end');
  assert.deepEqual(view.cues, []);
});
test('interruption wins against delayed start and no payload is inferred', () => {
  const view = new ForecastView('arena-a');
  view.accept(cast({ revision: 3, sequence: 3, phase: 'ended', reason: 'interrupted' }));
  assert.equal(view.accept(cast()), false);
  assert.equal(view.accept(cast({ revision: 4, sequence: 4 })), false);
  assert.equal(view.state('7', 10.6, .25).reason, 'interrupted');
  assert.deepEqual(view.cues, []);
});
test('confirmed release is deduplicated and never represented as damage', () => {
  const view = new ForecastView('arena-a'); view.accept(cast());
  const release = cast({ sequence: 2, revision: 2, emissionOrdinal: 1, phase: 'ended', reason: 'completed' });
  view.accept(release); view.accept(release);
  assert.deepEqual(view.cues, [{ castId: '7', kind: 'release', ordinal: 1 }]);
});
test('revision rejects old geometry, while independent casts survive packet reordering', () => {
  const view = new ForecastView('arena-a');
  view.accept(cast({ revision: 3, sequence: 8, radius: 3 }));
  assert.equal(view.accept(cast({ revision: 2, sequence: 7, radius: 9 })), false);
  assert.equal(view.accept(cast({ castId: '8', sequence: 6 })), true);
  assert.equal(view.state('7', 10.5, .25).radius, 3);
});
test('baseline reconstructs active states without replaying emissions, rejects old baseline', () => {
  const view = new ForecastView('arena-a'); view.accept(cast());
  assert.equal(view.baseline({ zoneEpoch: 'arena-a', sequence: 10, active: [cast({ castId: '8', revision: 4, emissionOrdinal: 2 })] }), true);
  assert.equal(view.state('7', 10.5, .25).phase, 'absent');
  assert.equal(view.state('8', 10.5, .25).progress, .5);
  assert.equal(view.accept(cast({ sequence: 9, revision: 9 })), false);
  assert.equal(view.baseline({ zoneEpoch: 'arena-a', sequence: 7, active: [] }), false);
  assert.deepEqual(view.cues, []);
});
test('lost terminal repairs from empty baseline; old epoch never leaks after transfer', () => {
  const view = new ForecastView('arena-a'); view.accept(cast());
  view.baseline({ zoneEpoch: 'arena-a', sequence: 3, active: [] });
  assert.equal(view.state('7', 10.5, .25).phase, 'absent');
  assert.equal(view.accept(cast({ zoneEpoch: 'arena-b', sequence: 20 })), false);
});
test('indefinite holds do not invent completion or suppress later pulse emissions', () => {
  const view = new ForecastView('arena-a');
  view.accept(cast({ phase: 'holding', endsAt: null }));
  assert.equal(view.state('7', 10.5, .25).progress, null);
  view.accept(cast({ phase: 'holding', endsAt: null, revision: 2, sequence: 2, emissionOrdinal: 1 }));
  view.accept(cast({ phase: 'holding', endsAt: null, revision: 3, sequence: 3, emissionOrdinal: 2 }));
  assert.equal(view.cues.length, 2);
});
test('proposed tracking has a rate cap; committed direction refuses later aim', () => {
  const direction = { facing: 0, requested: Math.PI / 2, committed: false, maxTurnRadiansPerSecond: Math.PI };
  assert.equal(directionAfterCommit(direction, .1), Math.PI * .1);
  assert.equal(directionAfterCommit({ ...direction, committed: true }, .1), 0);
});
test('Flame display keeps the persisted ember key; reduced motion retains information', () => {
  assert.equal(displayThread('ember', config.threadDisplay), 'Flame');
  assert.equal(config.threadDisplay.ember.element, 'Fire');
  assert.equal(config.meta.forecast.reducedMotion.sweep, false);
  for (const field of ['boundary', 'direction', 'relation', 'timing', 'surface'])
    assert.ok(config.meta.forecast.preserve.includes(field));
});
