import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PATTERNS } from './port-tables.mjs';

test('source golem bindings resolve to dedicated current bodies, not the legacy fallback', () => {
  const patterns = JSON.parse(readFileSync(new URL('../../content/core/entities/patterns-golem-prototype.json', import.meta.url))).entries;
  const bindings = JSON.parse(readFileSync(new URL('../../content/core/combat/weaves-golem-prototype.json', import.meta.url))).entries;
  for (const source of ['stone_golem', 'fire_golem', 'ice_golem', 'blood_golem']) {
    const body = patterns.find(p => p.id === PATTERNS[source]);
    assert.equal(body?.id, source);
    assert.ok(bindings.find(w => w.id === 'summon_' + source)?.effects.some(e => e.type === 'summon' && e.pattern === body.id));
  }
  assert.equal(PATTERNS.lightning_golem, undefined, 'Lightning is an original adaptation, not a fabricated source entry');
});
