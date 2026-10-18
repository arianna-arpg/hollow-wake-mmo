// Tests of the porting script's mapping on a small synthetic snapshot: units, Threads by role and theme,
// statuses onto the roster, a melee skill onto a swing shell, a projectile's riders and hooks, a support's
// trigger and generated stat families, a status pop and a proc.
//
//   node --test scripts/port/

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const skill = (id, extra) => ({ id, name: id, description: '', tags: [], color: '#fff', manaCost: 2, cooldown: 0, useTime: 0.7, effects: [{ type: 'damage' }], ...extra });

const snapshot = {
  generated: 'test',
  skills: {
    cleave: skill('cleave', { tags: ['attack', 'melee', 'physical', 'aoe'], baseDamage: { physical: [7, 11] }, delivery: { type: 'melee', range: 55, arcDeg: 130 }, ai: { range: 60, weight: 2 } }),
    firebolt: skill('firebolt', { tags: ['spell', 'projectile', 'fire'], manaCost: 6, useTime: 0.75, baseDamage: { fire: [10, 16] },
      delivery: { type: 'projectile', speed: 360, radius: 8, range: 460, explode: { radius: 60, damageScale: 0.5 }, sequel: { skillId: 'ember_payload', damageScale: 0.7 } },
      effects: [{ type: 'damage' }, { type: 'status', status: 'burn', chance: 0.3, magnitude: 0.3 }, { type: 'knockback', strength: 64 }] }),
    bone_bolt: skill('bone_bolt', { tags: ['spell', 'projectile', 'chaos'], baseDamage: { chaos: [4, 6] }, delivery: { type: 'projectile', speed: 320, radius: 8, range: 400 } }),
    spark: skill('spark', { tags: ['spell', 'projectile', 'lightning'], baseDamage: { lightning: [5, 9] }, delivery: { type: 'projectile', speed: 320, radius: 7, range: 400, trajectory: { homing: 3, erratic: 2, orbit: 1, orbitRadius: 80 } } }),
    scattergun: skill('scattergun', { tags: ['attack', 'projectile', 'physical', 'munition'], manaCost: 0, useCharges: { max: 3 }, convert: { when: 'chargesEmpty', skillId: 'reload_shells' }, baseDamage: { physical: [4, 7] }, delivery: { type: 'projectile', speed: 520, radius: 5, range: 240 } }),
    bolt_repeater: skill('bolt_repeater', { tags: ['attack', 'projectile', 'physical', 'munition'], manaCost: 0, cooldown: 3.5, useCharges: { max: 8, magazine: true }, baseDamage: { physical: [7, 11] }, delivery: { type: 'projectile', speed: 640, radius: 6, range: 470 } }),
    reload_shells: skill('reload_shells', { noDrop: true, tags: ['reload', 'munition'], manaCost: 0, delivery: { type: 'self' }, effects: [{ type: 'restoreSkillCharges', amount: 1 }] }),
    ember_payload: skill('ember_payload', { noDrop: true, tags: ['spell', 'aoe', 'fire'], baseDamage: { fire: [3, 5] }, delivery: { type: 'nova', radius: 64 } }),
    hive_bite: skill('hive_bite', { noDrop: true, tags: ['attack', 'melee'], baseDamage: { physical: [1, 2] }, delivery: { type: 'melee', range: 40, arcDeg: 60 } }),
  },
  supports: {
    cast_on_crit: { id: 'cast_on_crit', name: 'Cast on Crit', description: '', color: '#fff', excludeTags: ['channel'], mods: [], trigger: { on: 'crit', chance: 0.7, icd: 0.15 }, weight: 6 },
    fire_hunger: { id: 'fire_hunger', name: 'Fire Hunger', description: '', color: '#fff', requiresTags: ['fire'], requiresMechanisms: ['flight:mints'], mods: [{ stat: 'damageVs_burn', kind: 'more', value: 0.2 }, { stat: 'apply_burn', kind: 'flat', value: 0.1 }], perLevel: [{ stat: 'damage', kind: 'increased', value: 0.03, tags: ['fire'] }], weight: 6 },
  },
  statuses: {
    hemorrhage: { label: 'Hemorrhage', color: '#f00', duration: 14, element: 'physical', dotType: 'physical', hitMagnitude: 0.45, baseline: { dps: 2, perLevel: 0.9 }, pop: { fraction: 0.4 } },
    chill: { label: 'Chilled', color: '#0ff', duration: 3, element: 'cold', stacking: true, maxStacks: 5, buildup: { into: 'frozen' }, mods: [{ stat: 'moveSpeed', kind: 'more', value: -0.3 }] },
    stasis: { label: 'Stasis', color: '#fff', duration: 2, timeScale: 0 },
  },
  procs: { brutal_strike: { id: 'brutal_strike', name: 'Brutal Strike', color: '#fff', trigger: 'hit', effect: { type: 'extraHit', damageScale: 0.6 } } },
  charges: { fury: { label: 'Fury', color: '#fff', baseCap: 5, decay: { perSec: 1, delay: 8 } } },
  passiveNodes: {}, choiceGroups: {}, realms: {}, vocations: {}, classes: [], itemBases: {}, affixes: {}, uniques: {}, statDefs: {},
};

snapshot.skills.mobile_bolt = skill('mobile_bolt', { ...snapshot.skills.firebolt, id: 'mobile_bolt', castMove: 0.5 });
snapshot.skills.mixed_bolt = skill('mixed_bolt', { ...snapshot.skills.bone_bolt, id: 'mixed_bolt', name: 'Mixed Bolt',
  baseDamage: { fire: [1, 1], cold: [9, 9] }, tags: ['spell', 'projectile', 'fire', 'cold'] });
snapshot.skills.three_types = skill('three_types', { ...snapshot.skills.bone_bolt, id: 'three_types', name: 'Three Types',
  baseDamage: { fire: [1, 1], cold: [6, 6], lightning: [3, 3] }, tags: ['spell', 'projectile', 'fire', 'cold', 'lightning'] });

const out = mkdtempSync(join(tmpdir(), 'hollow-wake-port-'));
const snap = join(out, 'snapshot.json');
writeFileSync(snap, JSON.stringify(snapshot));
execFileSync(process.execPath, [join(here, 'port-prototype.mjs'), snap, out], { stdio: 'pipe' });
const read = name => readFileSync(join(out, name), 'utf8');

test('a melee skill becomes a preset on the swing shell in world units', () => {
  const strikes = read('weaves-strikes.yaml');
  assert.match(strikes, /id: cleave\n/);
  assert.match(strikes, /form: swing\n/);
  assert.match(strikes, /cost\.base\.stamina: 6/);
  assert.match(strikes, /range: 1\.72/);
  assert.match(strikes, /area\.angle: 1\.3/);
  assert.match(strikes, /shape: sector/);
  assert.match(strikes, /reach: edge/);
  assert.match(strikes, /weapon\.damage\.min/);
});

test('a projectile keeps its riders, hooks and Thread', () => {
  const projectiles = read('weaves-projectiles.yaml');
  assert.match(projectiles, /threads: \[ember\]/);
  assert.match(projectiles, /projectile\.speed: 11\.25/);
  assert.match(projectiles, /range: 14\.38/);
  assert.match(projectiles, /status: burning, chance: 0\.3, magnitude: "last_damage \* 0\.3"/);
  assert.match(projectiles, /kind: knockback, distance: 2/);
  assert.match(projectiles, /on_end:/);
  assert.match(projectiles, /radius: 1\.88/);
  assert.match(projectiles, /weave: ember_payload/);
});

test('trajectory axes come out in degrees a second and banks keep their kind', () => {
  const projectiles = read('weaves-projectiles.yaml');
  assert.match(projectiles, /projectile\.homing: 172/);
  assert.match(projectiles, /projectile\.erratic: 573/);
  assert.match(projectiles, /projectile\.orbit: 229/);
  assert.match(projectiles, /uses: \{ max: 3, manual: true \}/);
  assert.match(projectiles, /convert: \{ when: "?slot\.uses < 1"?, weave: reload_shells \}/);
  assert.match(projectiles, /uses: \{ max: 8, magazine: \{ refill: 3\.5 \} \}/);
});

test('chaos goes to Umbra by theme and the port closure carries referenced kit skills', () => {
  assert.match(read('weaves-projectiles.yaml'), /id: bone_bolt[\s\S]*?threads: \[umbra\]/);
  const ledger = read('port-ledger.md');
  assert.match(ledger, /\| ember_payload \| ember_payload \|/);
  assert.match(ledger, /\| hive_bite \| {2}\| melee kit skill \| not ported \| kit/);
});

test('supports become glyphs with triggers, mechanism probes and generated families', () => {
  const glyphs = read('glyphs-port.yaml');
  assert.match(glyphs, /trigger: \{ on: crit, chance: 0\.7, icd: 0\.15 \}/);
  assert.match(glyphs, /excludes: \[channel\]/);
  assert.match(glyphs, /requires_mechanisms: \["flight:children"\]/);
  assert.match(glyphs, /attribute: damage, form: more, value: 0\.2, scope: \[vs\.burning\]/);
  assert.match(glyphs, /attribute: status\.burning\.chance, form: flat, value: 0\.1/);
  assert.match(glyphs, /per_level: \[\{ attribute: damage, form: increased, value: 0\.03, scope: \[ember\] \}\]/);
});

test('statuses map onto the roster, pops become on_reapply and time scales are not ported', () => {
  const statuses = read('statuses-port.yaml');
  assert.doesNotMatch(statuses, /id: hemorrhage\n/, 'a roster status is not re-emitted');
  const ledger = read('port-ledger.md');
  assert.match(ledger, /\| hemorrhage \| hemorrhage \| tick, pop/);
  assert.match(ledger, /\| chill \| chilled \|/);
  assert.match(ledger, /\| stasis \| {2}\| {2}\| not ported \| fabric/);
  assert.match(read('rules-port.yaml'), /hemorrhage: "amount \* 0\.45"/);
});

test('procs and charges keep their payloads', () => {
  assert.match(read('procs-port.yaml'), /on: hit\n\s+effects: \[\{ type: damage, amount: "last_damage \* 0\.6", threads: \$threads, tags: \[proc\] \}\]/);
  assert.match(read('charges-port.yaml'), /id: fury\n\s+name: Fury\n\s+color: "#fff"\n\s+duration: 8\n\s+decay: 1\n/);
  assert.match(read('attributes-port.yaml'), /id: charge\.fury\.max[\s\S]{0,80}base: 5\n/);
});

test('structurally different shells have unique ids and presets reference them', () => {
  const ids = [...read('forms-port.yaml').matchAll(/^  - id: (.+)$/gm)].map(m => m[1]);
  assert.equal(ids.length, new Set(ids).size, JSON.stringify(ids));
  assert.match(read('weaves-projectiles.yaml'), /id: mobile_bolt[\s\S]*?form: missile_2\n/);
  assert.match(read('forms-port.yaml'), /id: missile_2[\s\S]*?cast_move: 0\.5/);
});

test('mixed damage pairs shares with sorted Threads and preserves the folded total', () => {
  const mixed = read('weaves-projectiles.yaml').split('  - id: mixed_bolt')[1].split('  - id: three_types')[0];
  assert.match(mixed, /tide: "weave.base_damage \* 0\.9/);
  assert.match(mixed, /ember: "weave.base_damage \* 0\.1/);
  const folded = read('weaves-projectiles.yaml').split('  - id: three_types')[1];
  assert.match(folded, /threads: \[tide, storm\]/);
  assert.match(folded, /base_damage: 10/);
  assert.match(folded, /tide: "weave.base_damage \* 0\.67/);
  assert.match(folded, /storm: "weave.base_damage \* 0\.33/);
  assert.doesNotMatch(folded, /ember: "weave.base_damage/);
});
