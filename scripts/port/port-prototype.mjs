// Maps the prototype's combat catalogs onto Hollow Wake's vocabulary and writes drafts to author by hand.
//
//   node scripts/port/port-prototype.mjs <prototype.json> <out-folder>
//
// The snapshot comes from dump-prototype.mts. The drafts are YAML in our content shapes (forms, weaves,
// glyphs, statuses, procs, charges, patterns, passives, items, affixes, uniques), a ledger table and a report
// of what did not map. They are drafts: every entry is read, renamed and tuned by hand before it joins
// content/core, and nothing here runs at game time. Units convert once here (port-tables.mjs UNIT).

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import * as T from './port-tables.mjs';

const [snapPath, outDir] = process.argv.slice(2);
if (!snapPath || !outDir) {
  console.error('usage: node scripts/port/port-prototype.mjs <prototype.json> <out-folder>');
  process.exit(2);
}
const S = JSON.parse(readFileSync(snapPath, 'utf8'));
mkdirSync(outDir, { recursive: true });

// ---------------------------------------------------------------- helpers

const u = v => +(v / T.UNIT).toFixed(2);
const deg = r => Math.round(r * 180 / Math.PI);
const tenth = v => Math.round(v * 10) / 10;
const r2 = v => +(+v).toFixed(3);
const mean = ([lo, hi]) => (lo + hi) / 2;
const title = id => id.split('_').map(w => w ? w[0].toUpperCase() + w.slice(1) : w).join(' ');
const report = { unmappedStats: {}, unmappedStatuses: {}, shells: {}, blockedEffects: {}, notes: {}, patterns: {} };
const LATER = /later|lacks|fabric|rejected|not ported|not part of this port|outside this port/;
function statusOf(ctx) {
  if (ctx.blocked?.length) return ctx.blocked.every(b => LATER.test(b)) ? 'not ported' : 'blocked';
  if (ctx.seams?.length) return 'seam';
  return ctx.notes?.length ? 'ported (difference)' : 'ported';
}
const noteOf = ctx => [...(ctx.notes ?? []), ...(ctx.seams ?? []).map(b => 'seam: ' + b), ...(ctx.blocked ?? []).map(b => (LATER.test(b) ? 'later: ' : 'blocked: ') + b)].join('; ');
const bump = (bag, key) => { bag[key] = (bag[key] || 0) + 1; };

function tagOf(t) {
  if (T.DROPPED_TAGS.has(t)) return null;
  if (t.startsWith('vs:')) return 'vs.' + T.victim(t.slice(3));
  if (t.startsWith('minion:') || t.startsWith('body:')) return null;
  return T.TAGS[t] ?? t;
}
const tagsOf = ts => [...new Set((ts || []).map(tagOf).filter(Boolean))];

function mechanismOf(m) {
  if (T.MECHANISMS[m]) return T.MECHANISMS[m];
  const [name, param] = m.split(':');
  if (name === 'affliction' || name === 'status') return param ? `${name}:${T.status(param) ?? param}` : name;
  if (name === 'construct') return m;
  return null;
}

function conditionOf(id) {
  const c = T.CONDITIONS[id];
  return c === undefined ? null : c;
}

// A prototype modifier to one of ours; null when its stat has no attribute here (recorded in the report).
function modOf(m, ctx = {}) {
  let spec = T.STATS[m.stat];
  let note;
  if (spec === undefined) {
    for (const fam of T.STAT_FAMILIES) {
      const match = fam.re.exec(m.stat);
      if (!match) continue;
      spec = fam.to(match);
      if (spec === null) { bump(report.unmappedStats, `${m.stat} (${fam.why})`); return null; }
      note = spec.note;
      break;
    }
  }
  if (spec === undefined || spec === null) {
    bump(report.unmappedStats, m.stat);
    return null;
  }
  const out = { attribute: spec.attribute, form: spec.form ?? m.kind, value: r2(m.value * (spec.scale ?? 1)) };
  if (m.kind === 'link') {
    const from = T.STATS[m.fromStat ?? '']?.attribute;
    if (!from) { bump(report.unmappedStats, `${m.fromStat} (link source)`); return null; }
    out.form = 'link';
    out.from = from;
  } else if (spec.link) {
    out.form = 'link';
    out.from = spec.link;
  }
  const scope = [...tagsOf(m.tags), ...(spec.scope ?? [])];
  if (scope.length) out.scope = scope;
  if (m.when) {
    const c = conditionOf(m.when);
    if (c === null) { bump(report.unmappedStats, `condition ${m.when}`); return null; }
    out.condition = c;
  }
  if (m.gauge) {
    out.gauge = gaugeOf(m.gauge);
    if (m.gaugeAt != null) out.gauge_at = m.gaugeAt;
  }
  if (note) ctx.notes?.push(note);
  return out;
}

function gaugeOf(g) {
  const charge = /^charge[:_](.+)$/.exec(g);
  if (charge) return `charge.${charge[1]}`;
  const st = /^status[:_](.+)$/.exec(g);
  if (st) return `status.${T.status(st[1])}.stacks`;
  return g;
}

function modsOf(list, ctx) {
  const out = [];
  for (const m of list || []) {
    const r = modOf(m, ctx);
    if (r) out.push(r); else ctx?.dropped?.push(`modifier ${m.stat}`);
  }
  return out;
}

// ---------------------------------------------------------------- YAML

const keyOk = k => /^[A-Za-z_$][\w.$-]*$/.test(k);
const key = k => (keyOk(k) ? k : JSON.stringify(k));
function scalar(v) {
  if (v === null) return 'null';
  if (typeof v === 'number') return Number.isInteger(v) ? String(v) : String(+v.toFixed(4));
  if (typeof v === 'boolean') return String(v);
  const s = String(v);
  if (s.startsWith('$')) return s;
  if (/^[A-Za-z_][\w.-]*$/.test(s) && !/^(true|false|null|yes|no|on|off|y|n)$/i.test(s)) return s;
  return JSON.stringify(s);
}
function flow(v) {
  if (Array.isArray(v)) {
    const parts = v.map(flow);
    if (parts.some(p => p === null)) return null;
    const s = '[' + parts.join(', ') + ']';
    return s.length <= 118 ? s : null;
  }
  if (v && typeof v === 'object') {
    const parts = [];
    for (const [k, x] of Object.entries(v)) {
      if (x === undefined || k.startsWith('__')) continue;
      const f = flow(x);
      if (f === null) return null;
      parts.push(key(k) + ': ' + f);
    }
    const s = '{ ' + parts.join(', ') + ' }';
    return s.length <= 118 ? s : null;
  }
  return scalar(v);
}
function block(v, indent) {
  const pad = ' '.repeat(indent);
  if (Array.isArray(v)) {
    if (v.length === 0) return pad + '[]';
    return v.map(x => {
      const f = flow(x);
      if (f !== null) return pad + '- ' + f;
      if (x && typeof x === 'object' && !Array.isArray(x)) {
        const lines = block(x, indent + 2).split('\n');
        lines[0] = pad + '- ' + lines[0].slice(indent + 2);
        return lines.join('\n');
      }
      return pad + '-\n' + block(x, indent + 2);
    }).join('\n');
  }
  if (v && typeof v === 'object') {
    const out = [];
    for (const [k, x] of Object.entries(v)) {
      if (x === undefined || k.startsWith('__')) continue;
      const f = flow(x);
      out.push(f !== null ? pad + key(k) + ': ' + f : pad + key(k) + ':\n' + block(x, indent + 2));
    }
    return out.join('\n');
  }
  return pad + scalar(v);
}
function yamlFile(kind, header, entries) {
  const lines = [header.trim(), `kind: ${kind}`, 'entries:'];
  for (const e of entries) {
    for (const c of e.__comment ?? []) lines.push('  # ' + c);
    const b = block(e, 4).split('\n');
    b[0] = '  - ' + b[0].slice(4);
    lines.push(...b);
  }
  return lines.join('\n') + '\n';
}
const write = (name, text) => writeFileSync(join(outDir, name), text);

// ---------------------------------------------------------------- the closure of skills to port

const skills = S.skills;
const refsOf = k => {
  const d = k.delivery ?? {};
  const ids = [d.emit?.skillId, d.shatter?.skillId, d.sequel?.skillId, k.meta?.skillId, k.followUp?.skillId, k.convert?.skillId,
    d.castSkillId, d.pulse?.skillId, k.guard?.pulse?.skillId, ...(k.comboChain?.skills ?? [])];
  for (const e of k.effects ?? []) if (e.type === 'minionCast') ids.push(e.skillId);
  return ids.filter(Boolean);
};
const inPort = new Set(Object.values(skills).filter(k => !k.noDrop).map(k => k.id));
for (let changed = true; changed;) {
  changed = false;
  for (const id of [...inPort]) {
    for (const ref of refsOf(skills[id])) {
      if (skills[ref] && !inPort.has(ref)) { inPort.add(ref); changed = true; }
    }
  }
}

// ---------------------------------------------------------------- families

function familyOf(k) {
  const d = k.delivery?.type;
  const mode = k.castMode ?? 'cast';
  if (['channel', 'charge', 'guard', 'overcharge', 'perfect', 'timed', 'multitude', 'concentration'].includes(mode)) return 'channels';
  if (d === 'aura') return 'auras';
  if (d === 'summon') return 'summons';
  if (d === 'construct') return ['trap', 'mine', 'pod'].includes(k.delivery.kind) ? 'placements' : 'summons';
  if (['dash', 'leap', 'blink', 'mark', 'carom'].includes(d)) return 'movement';
  if (d === 'melee') return 'strikes';
  if (d === 'projectile') return 'projectiles';
  if (d === 'storm') return 'placements';
  if (d === 'ground') return (k.delivery.lingerDuration || k.delivery.delay) ? 'placements' : 'spells';
  if (['nova', 'cone', 'target'].includes(d)) return (k.tags ?? []).includes('melee') || (k.tags ?? []).includes('attack') ? 'strikes' : 'spells';
  if (d === 'detonate' || d === 'detonateProjectile') return 'utility';
  return 'utility';
}
const FAMILY_NAMES = {
  strikes: 'strikes and melee', projectiles: 'projectiles and bows', spells: 'spells per Thread', channels: 'channels and holds',
  auras: 'auras and toggles', summons: 'summons and constructs', movement: 'movement', placements: 'traps and placements', utility: 'utility and defense',
};

// ---------------------------------------------------------------- shells: the forms presets compose on

const shells = new Map();
function shell(base, k, parts, roleHint) {
  const attack = (k.tags ?? []).includes('attack');
  const instant = !k.useTime && (k.castMode ?? 'cast') === 'cast';
  const resource = attack ? 'stamina' : 'mana';
  const def = {
    id: '',
    name: '',
    description: '',
    targeting: parts.targeting,
    cast_mode: parts.cast_mode ?? (instant ? 'instant' : 'cast'),
    cast_time: parts.cast_mode || !instant ? 'weave.cast.time' : undefined,
    cooldown: 'weave.cooldown.base',
    costs: { [resource]: `weave.cost.base.${resource} + ${resource}.max * weave.cost.pct_max.${resource}` },
    attributes: { 'cast.time': 0.6, 'cooldown.base': 0, [`cost.base.${resource}`]: attack ? 6 : 8, range: 10 },
    mechanisms: [attack ? 'attack' : 'spell', ...(parts.mechanisms ?? [])],
    tags: [attack ? 'attack' : 'spell'],
    min_threads: attack ? 0 : 1,
    max_threads: attack ? 1 : 2,
    max_glyphs: 4,
    ai: { role: roleHint ?? 'damage', preferred_range: parts.range ?? 6, weight: 1 },
    ...parts.extra,
    effects: [],
  };
  if (k.lifeCost) def.costs.life = 'weave.cost.base.life';
  for (const t of parts.tags) if (!def.tags.includes(t)) def.tags.push(t);
  const mode = parts.cast_mode === 'channel' ? '_stream' : parts.cast_mode === 'guard' ? '_stance' : parts.cast_mode === 'charge' ? '_draw' : parts.cast_mode === 'overcharge' ? '_surge' : parts.extra.timing ? '_beat' : '';
  const sig = JSON.stringify({ base, attack, instant, cast_mode: def.cast_mode, targeting: def.targeting, tags: [...def.tags].sort(), extra: parts.extra, mech: def.mechanisms });
  let entry = shells.get(sig);
  if (!entry) {
    const stem = base + mode + (instant && !mode ? '_quick' : '');
    const same = [...shells.values()].filter(s => s.stem === stem).length;
    const id = stem + (same ? `_${same + 1}` : '');
    def.id = id;
    def.name = title(id);
    def.description = `A ${base} shell of the port: timing, cost and cooldown read the preset's attributes; the preset carries the graph.`;
    entry = { base, stem, def, users: [] };
    shells.set(sig, entry);
  }
  entry.users.push(k.id);
  return entry.def.id;
}

// ---------------------------------------------------------------- effects

function threadsOf(k) {
  const dmg = k.baseDamage ?? {};
  let types = Object.keys(dmg).sort((a, b) => mean(dmg[b]) - mean(dmg[a]));
  if (!types.length) types = (k.tags ?? []).filter(t => T.THREADS[t]);
  const words = `${k.id} ${k.name} ${(k.tags ?? []).join(' ')}`;
  const mapped = [...new Set(types.map(t => (t === 'chaos' ? (T.UMBRA_WORDS.test(words) ? 'umbra' : 'void') : T.THREADS[t])))];
  return { threads: mapped.slice(0, 2), dropped: mapped.slice(2), types };
}

function damageNode(k, ctx) {
  const dmg = k.baseDamage;
  if (!dmg) return null;
  const totals = Object.values(dmg).reduce((a, [lo, hi]) => [a[0] + lo, a[1] + hi], [0, 0]);
  const m = (totals[0] + totals[1]) / 2;
  const rlo = +(totals[0] / m).toFixed(2);
  const rhi = +(totals[1] / m).toFixed(2);
  const roll = rlo === 1 && rhi === 1 ? '' : ` * lerp(${rlo}, ${rhi}, roll)`;
  const growth = ' * (1 + 0.08 * (weave.level - 1))';
  const scale = ctx.scale && ctx.scale !== 1 ? ` * ${ctx.scale}` : '';
  const node = { type: 'damage', threads: '$threads' };
  if (ctx.attack) {
    node.amount = `lerp(weapon.damage.min, weapon.damage.max, roll) * weave.damage.effectiveness * attack.power${scale}`;
    node.tags = ['hit'];
  } else {
    node.amount = `weave.base_damage${roll} * spell.power${growth}${scale}`;
    const { types, threads } = threadsOf(k);
    if (types.length >= 2) {
      // Keep each share paired with its Thread's descending damage order. Fold any
      // omitted third type proportionally into the two retained Threads.
      const retained = types.slice(0, 2);
      const retainedMean = retained.reduce((sum, t) => sum + mean(dmg[t]), 0);
      const shares = retained.map(t => mean(dmg[t]) / retainedMean);
      if (threads.length >= 2 && Math.max(...shares) / Math.min(...shares) > 1.25) {
        node.amounts = {};
        retained.forEach((t, i) => { node.amounts[threads[i]] = `weave.base_damage * ${+shares[i].toFixed(2)}${roll} * spell.power${growth}${scale}`; });
        delete node.amount;
        delete node.threads;
      }
    }
  }
  if (ctx.noCrit) node.can_crit = false;
  return node;
}

const generatedStatuses = new Map();
function buffStatus(e, k, ctx) {
  const id = e.id;
  if (!generatedStatuses.has(id)) {
    const dropped = [];
    const st = {
      id, name: e.label ?? title(id), tags: ['buff'], duration: e.duration, beneficial: true,
      max_stacks: e.maxStacks && e.maxStacks > 1 ? e.maxStacks : undefined,
      stack_policy: e.maxStacks && e.maxStacks > 1 ? 'stack' : 'refresh',
      stack_clock: e.stackTimers === 'independent' ? 'independent' : undefined,
      modifiers: modsOf(e.mods, { dropped }),
      color: k.color,
      __comment: [`buff of ${k.id}${dropped.length ? ` (dropped: ${dropped.join(', ')})` : ''}`],
    };
    if (e.clearOnHit) st.consume_on = { event: 'hurt', count: 99 };
    if (e.consumeOnUse) st.consume_on = { event: 'use', tags: tagsOf(e.consumeOnUse.tags) };
    if (e.consumeOn) st.consume_on = { event: e.consumeOn.on === 'use' ? 'cast' : e.consumeOn.on, tags: tagsOf(e.consumeOn.tags) };
    if (e.nextHit) {
      const rider = [];
      if (e.nextHit.status) rider.push({ type: 'apply_status', status: T.status(e.nextHit.status) ?? e.nextHit.status, magnitude: e.nextHit.statusScale });
      if (e.nextHit.addedDamage) rider.push({ type: 'damage', amounts: Object.fromEntries(Object.entries(e.nextHit.addedDamage).map(([t, v]) => [T.THREADS[t], v])), tags: ['rider'] });
      if (e.nextHit.procId) rider.push({ type: 'cast', weave: `proc_${e.nextHit.procId}`, at: 'target' });
      st.on_bearer_hit = rider;
      st.consume_on ??= { event: 'hit', tags: tagsOf(e.nextHit.tags), count: 1 };
    }
    if (e.bond) ctx.notes.push('life bond (bondShare) is not ported');
    if (e.powerScaled) ctx.notes.push('power-scaled buff: the modifiers do not scale with the use');
    generatedStatuses.set(id, st);
  }
  const node = { type: 'apply_status', status: id, duration: e.duration };
  node.to = e.affects === 'minions' ? 'minions' : e.affects === 'allies' ? 'allies' : 'self';
  if (e.radius) node.radius = u(e.radius);
  if (e.stacksOnApply) node.stacks = e.stacksOnApply;
  if (e.powerStacks) { node.stacks = e.powerStacks; ctx.notes.push('gauge power stacks fixed to the full count'); }
  return node;
}

function riderOf(e, k, ctx) {
  switch (e.type) {
    case 'damage': return null;
    case 'status': {
      const s = T.status(e.status);
      if (s === null) { ctx.notes.push(`status ${e.status} is not ported`); return null; }
      const n = { type: 'apply_status', status: s, chance: e.chance };
      if (e.magnitude != null) n.magnitude = T.DOT_STATUSES.has(s) ? `last_damage * ${e.magnitude}` : e.magnitude;
      if (e.durationOverride != null) n.fixed_duration = e.durationOverride;
      return n;
    }
    case 'knockback': return { type: 'move', kind: e.mode === 'buffet' ? 'buffet' : 'knockback', distance: u(e.strength) };
    case 'pull': return e.stun ? [{ type: 'move', kind: 'pull', distance: 8 }, { type: 'apply_status', status: 'stunned', duration: e.stun }] : { type: 'move', kind: 'pull', distance: 8 };
    case 'buff': return buffStatus(e, k, ctx);
    case 'heal': {
      const parts = [];
      if (e.amount) parts.push(`${e.amount}`);
      if (e.pctMax) parts.push(`target.life.max * ${e.pctMax}`);
      const n = { type: 'heal', amount: parts.join(' + ') || '0', resource: 'life' };
      if (e.excludeCaster) n.exclude_self = true;
      if (e.chain) ctx.notes.push('the heal chain becomes a chain effect picking the most wounded ally');
      return n;
    }
    case 'cleanse': return { type: 'remove_status', kind: 'harmful', count: e.count ?? 1, order: 'newest', self: true };
    case 'shed': return { type: 'remove_status', status: T.status(e.status) ?? e.status, self: true };
    case 'gainCharge': return { type: 'charge', id: e.charge, gain: e.amount, max: e.max, self: true };
    case 'drainCharge': return { type: 'charge', id: e.charge, drain: e.perSec, self: true };
    case 'restoreSkillCharges': return { type: 'restore_uses', amount: e.amount, scope: e.scope === 'all' ? 'all' : e.scope === 'still' ? 'still' : 'host' };
    case 'reduceCooldowns': return { type: 'cooldown', seconds: e.seconds, fraction: e.fraction, scope: 'others' };
    case 'ward': { if (e.onHit) ctx.notes.push('ward on each hit becomes a ward on cast'); return { type: 'resource', resource: 'ward', amount: e.amount, self: true }; }
    case 'absorb': {
      const id = `${k.id}_shell`;
      generatedStatuses.set(id, { id, name: `${k.name} Shell`, tags: ['buff', 'pool'], duration: e.duration, beneficial: true, pool: e.amount, color: k.color, __comment: [`absorption pool of ${k.id}`] });
      return { type: 'apply_status', status: id, duration: e.duration, to: 'self' };
    }
    case 'restore': return { type: 'resource', resource: e.resource === 'es' ? 'shield' : e.resource, amount: e.amount, self: true };
    case 'restoreOverTime': {
      const id = `${k.id}_restoring`;
      const res = e.resource === 'es' ? 'shield' : e.resource;
      const amount = [e.amount ? `${+(e.amount / e.duration).toFixed(2)}` : null, e.amountPctMax ? `${res}.max * ${+(e.amountPctMax / e.duration).toFixed(3)}` : null, e.amountPerLevel ? `level * ${+(e.amountPerLevel / e.duration).toFixed(2)}` : null].filter(Boolean).join(' + ');
      generatedStatuses.set(id, { id, name: `${k.name} Draught`, tags: ['buff', 'restoring'], duration: e.duration, beneficial: true, color: k.color, tick: { interval: 1, effects: [{ type: 'resource', resource: res, amount, self: true }] }, __comment: [`restoration lane of ${k.id} (${e.lane ?? 'flat'})`] });
      if (e.perCharge) ctx.notes.push('per-charge restoration scales by charges_spent: author by hand');
      return { type: 'apply_status', status: id, duration: e.duration, to: 'self' };
    }
    case 'ironWard': return { type: 'apply_status', status: 'iron_ward', duration: e.duration, to: 'self' };
    case 'guardSurge': return { type: 'guard', amount: `mana * ${e.manaFraction} * ${e.ratio}`, self: true };
    case 'spreadStatus': return { type: 'spread_status', kind: 'harmful', radius: u(e.radius), duration: e.duration === 'refresh' ? 'refresh' : 'remaining' };
    case 'siphonStatus': return { type: 'move_status', kind: 'harmful', from: 'area', to: 'source', radius: u(e.radius), filter: e.from === 'allies' ? 'ally' : 'enemy' };
    case 'transfuseStatus': return { type: 'move_status', kind: 'harmful', from: 'source', to: 'target', splash: u(e.splash ?? 0) };
    case 'rupture': return { type: 'rupture', status: T.status(e.status) ?? e.status, fraction: e.fraction };
    case 'recallMinions': return { type: 'recall_minions', radius: 2.5 };
    case 'commandMinions': return { type: 'command_minions', command: e.command ?? 'assault', duration: e.duration, affects: e.affects, radius: e.radius ? u(e.radius) : undefined };
    case 'minionCast': return { type: 'minion_cast', weave: e.skillId, at: e.at === 'enemy' ? 'enemy' : 'point' };
    case 'detonateMinions': { ctx.notes.push('minions explode through consume_minion rather than a self-destruct charge'); return { type: 'consume_minion', count: 99, pick: 'nearest', radius: 20, effects: [{ type: 'area', radius: 2.5, center: 'point', filter: 'enemy', effects: [{ type: 'damage', amount: `consumed_life_max * ${e.fraction}`, threads: '$threads' }] }] }; }
    case 'spawnCorpse': return { type: 'spawn', pattern: patternOf(e.monsterId, ctx), count: e.count, as_corpse: true };
    case 'spawnZone': return { type: 'field', radius: u(e.radius), duration: e.duration, interval: e.tickInterval ?? 0.5, pulse: [damageNode(k, { ...ctx, scale: e.damageScale ?? 1, noCrit: true })].filter(Boolean) };
    case 'birth': { ctx.notes.push('a birth becomes a spawn of our pattern'); return { type: 'spawn', pattern: patternOf(e.monsterId ?? e.pool?.[0]?.id ?? 'sprig', ctx), count: e.count ?? 1, lifetime: e.duration ?? 20 }; }
    case 'shatterConstructs': return { type: 'detonate', tags: ['construct'], radius: e.radius ? u(e.radius) : undefined, effects: [{ type: 'area', radius: 2.5, center: 'point', filter: 'enemy', effects: [{ type: 'damage', amount: `body_life_max * ${e.fraction}`, threads: '$threads' }] }] };
    default:
      if (T.BLOCKED_EFFECTS[e.type]) { ctx.blocked.push(`${e.type}: ${T.BLOCKED_EFFECTS[e.type]}`); bump(report.blockedEffects, e.type); }
      else { ctx.blocked.push(`effect ${e.type} has no mapping`); bump(report.blockedEffects, e.type); }
      return null;
  }
}

function patternOf(monsterId, ctx) {
  const p = T.PATTERNS[monsterId];
  if (!p) { bump(report.patterns, monsterId); ctx.notes.push(`summons our remnant_shade for ${monsterId}`); return 'remnant_shade'; }
  ctx.notes.push(`summons our ${p} for ${monsterId}`);
  return p;
}

function ridersOf(k, ctx) {
  const out = [];
  for (const e of k.effects ?? []) {
    const r = riderOf(e, k, ctx);
    if (Array.isArray(r)) out.push(...r); else if (r) out.push(r);
  }
  return out;
}

function countExpr(c) {
  return Array.isArray(c) ? `randi(${c[0]}, ${c[1]})` : c;
}

function aiOf(k, family, range) {
  const ai = k.ai;
  const role = family === 'summons' || family === 'placements' ? 'summon' : family === 'movement' ? 'movement' : family === 'utility' ? (k.effects?.some(e => e.type === 'heal') ? 'heal' : 'defense') : family === 'auras' ? 'defense' : 'damage';
  if (!ai) return { role, preferred_range: range, weight: 1 };
  const h = { role, preferred_range: u(ai.range), weight: ai.weight ?? 1 };
  if (ai.minRange) h.min_range = u(ai.minRange);
  if (ai.nearWeight != null) h.near_weight = ai.nearWeight;
  if (ai.keepDistance) h.preferred_range = Math.max(h.preferred_range, u(ai.keepDistance));
  return h;
}

// ---------------------------------------------------------------- the graph per delivery

function graphOf(k, ctx) {
  const d = k.delivery;
  const dmg = damageNode(k, ctx);
  const riders = ridersOf(k, ctx);
  const hits = [dmg, ...riders].filter(Boolean);
  const drop = (field, why) => { if (d[field] !== undefined) ctx.notes.push(why ?? `${d.type}.${field} dropped`); };
  const blockedField = (field, why) => { if (d[field] !== undefined) ctx.blocked.push(why); };
  switch (d.type) {
    case 'melee': {
      ctx.attrs.range = u(d.range);
      ctx.attrs['area.angle'] = +(d.arcDeg / 100).toFixed(2);
      drop('shape', 'band swing drawn as a bar shape');
      const shape = d.shape === 'band' ? 'bar' : 'sector';
      return [{ type: 'area', shape, angle: 100, radius: 'weave.range', center: 'source', reach: 'edge', filter: 'enemy', effects: hits }];
    }
    case 'nova': {
      ctx.attrs['area.radius'] = u(d.radius);
      const n = { type: 'area', shape: 'circle', radius: 'weave.area.radius', center: k.targeting ? 'focus' : 'source' };
      n.filter = d.affects === 'all' ? (d.spareCaster ? 'not_self' : 'all') : d.affects === 'allies' ? (d.spareCaster ? 'ally' : 'ally_or_self') : 'enemy';
      if (d.edgeOnly) n.inner = d.edgeOnly;
      if (d.maxTargets) { n.max_targets = d.maxTargets; n.pick = 'nearest'; }
      if (d.requiresStatus) n.require = d.requiresStatus.map(s => `target.status.${T.status(s) ?? s} > 0`).join(' || ');
      n.effects = hits;
      return [n];
    }
    case 'cone': {
      ctx.attrs.range = u(d.range);
      ctx.attrs['area.angle'] = +(d.arcDeg / 60).toFixed(2);
      const n = { type: 'area', shape: 'sector', angle: 60, radius: 'weave.range', center: 'source', filter: 'enemy', effects: hits };
      if (d.edgeOnly) n.inner = d.edgeOnly;
      return [n];
    }
    case 'target': {
      const out = [...hits];
      if (d.splash) out.push({ type: 'area', shape: 'circle', radius: u(d.splash), center: 'focus', include_center: false, filter: 'enemy', effects: [dmg].filter(Boolean) });
      return out;
    }
    case 'projectile': {
      ctx.attrs.range = u(d.range);
      ctx.attrs['projectile.speed'] = u(d.speed);
      const n = { type: 'projectile', radius: u(d.radius) };
      if (d.count !== undefined) { if (Array.isArray(d.count)) n.count = countExpr(d.count); else ctx.attrs['projectile.count'] = d.count; }
      if (d.pierce) ctx.attrs['projectile.pierce'] = d.pierce;
      if (d.spreadDeg) n.spread = d.spreadDeg;
      if (d.shape && !['vortex', 'circle', 'octagon'].includes(d.shape)) n.shape = d.shape;
      if (d.duration) n.duration = d.duration;
      if (d.fire && d.fire !== 'fan') n.fire = d.fire;
      if (d.ring) { n.fire = 'ring'; n.ring_jitter = d.ring.phaseJitter === false ? 0 : 360 / (d.count ?? 8); }
      if (d.salvoInterval) n.salvo_interval = d.salvoInterval;
      if (d.volleySpacing) n.volley_spacing = u(d.volleySpacing);
      if (d.origin === 'cursor') { n.origin = 'point'; n.origin_range = u(d.originRange ?? 420); }
      if (d.forks) ctx.attrs['projectile.forks'] = d.forks;
      if (d.returns) n.return = d.returns;
      if (d.rehit) ctx.attrs['projectile.rehit'] = d.rehit;
      if (d.spreadByAim) n.spread_by_aim = { near: d.spreadByAim.near, far: d.spreadByAim.far, range: u(d.spreadByAim.range) };
      if (d.occlusion === 'free') n.occlusion = 'free';
      // The prototype turns in radians a second and ours in degrees; its erratic adds a uniform turn of up to
      // five times its rate a second; an orbit's rate is the flight's speed over its radius; accel is a fraction
      // of the speed a second; a weave of w sways w / pi times a second; a zigzag swings every interval seconds.
      const tr = d.trajectory ?? {};
      const speed = u(d.speed);
      if (tr.homing) ctx.attrs['projectile.homing'] = deg(tr.homing);
      if (tr.guide) ctx.attrs['projectile.guide'] = deg(tr.guide);
      if (tr.erratic) ctx.attrs['projectile.erratic'] = deg(tr.erratic * 5);
      if (tr.spin) ctx.attrs['projectile.spin'] = deg(tr.spin);
      if (tr.orbit) ctx.attrs['projectile.orbit'] = deg(tr.orbit * speed / u(tr.orbitRadius ?? 80));
      else if (tr.spiral) ctx.attrs['projectile.spiral'] = deg(tr.spiral);
      if (tr.orbit && tr.spiral) ctx.notes.push('an orbit that also spirals keeps the orbit');
      if (tr.accel) ctx.attrs['projectile.accel'] = tenth(tr.accel * speed);
      if (tr.bounce) ctx.attrs['projectile.bounce'] = tr.bounce;
      if (tr.weave) ctx.attrs['projectile.wave'] = tenth(tr.weave / Math.PI);
      if (tr.amplitude) n.amplitude = u(tr.amplitude);
      if (tr.orbitRadius) n.orbit_radius = u(tr.orbitRadius);
      if (tr.zigzag) { n.zigzag = tr.zigzag.angleDeg; n.zigzag_every = tenth(tr.zigzag.interval * speed); if (tr.zigzag.shed) ctx.notes.push('zigzag shards dropped'); }
      if (tr.recurve) ctx.attrs['projectile.recurve'] = Math.round(tr.recurve.chance * 90);
      if (tr.arcTo) ctx.attrs['projectile.converge'] = deg(tr.arcTo);
      if (tr.selectivePierce) ctx.notes.push('selective pierce dropped');
      if (tr.caromOnHit) ctx.blocked.push('carom on hit (row B25, carom is later)');
      if (tr.frameBounce) ctx.notes.push('frame bounces are rejected (question Q10)');
      n.on_hit = hits;
      const ends = [];
      if (d.explode) ends.push({ type: 'area', radius: u(d.explode.radius), center: 'point', filter: 'enemy', effects: [damageNode(k, { ...ctx, scale: d.explode.damageScale ?? 0.6 })].filter(Boolean) });
      if (d.endZone) ends.push({ type: 'field', radius: u(d.endZone.radius), duration: d.endZone.duration, interval: d.endZone.tickInterval ?? 0.5, seek: d.endZone.seek ? u(d.endZone.seek.speed) : undefined, pulse: [damageNode(k, { ...ctx, scale: d.endZone.damageScale ?? 0.5, noCrit: true })].filter(Boolean) });
      if (d.sequel) ends.push({ type: 'cast', weave: d.sequel.skillId, at: 'point', damage_scale: d.sequel.damageScale ?? 1 });
      if (ends.length) n.on_end = ends;
      if (d.shatter) n.on_first_hit = [{ type: 'projectile', count: d.shatter.count, spread: d.shatter.spreadDeg, speed: 'weave.projectile.speed', range: 'weave.range * 0.6', radius: u(d.radius), on_hit: [{ type: 'cast', weave: d.shatter.skillId, at: 'target' }] }];
      if (d.emit) { n.tick = d.emit.interval; n.on_tick = [{ type: 'cast', weave: d.emit.skillId, at: 'point', aim: d.emit.pattern === 'random' ? 'random' : d.emit.pattern === 'forward' ? 'onward' : 'rotating' }]; }
      if (d.zap) { n.tick = d.zap.interval; n.on_tick = [...(n.on_tick ?? []), { type: 'area', radius: u(d.zap.radius), center: 'point', filter: 'enemy', effects: [damageNode(k, { ...ctx, scale: d.zap.damageScale ?? 1 })].filter(Boolean) }]; }
      if (d.aura) { n.tick = 0.5; n.on_tick = [...(n.on_tick ?? []), { type: 'area', radius: u(d.aura.radius), center: 'point', filter: 'enemy', effects: [{ type: 'damage', amount: `${d.aura.dps * 0.5} * spell.power`, threads: [T.THREADS[d.aura.damageType ?? 'fire']], tags: ['dot'], can_crit: false }] }]; }
      if (d.trail) { n.travel = u(d.trail.every); n.on_travel = d.trail.blast ? [{ type: 'area', radius: u(d.trail.blast.radius), center: 'point', filter: 'enemy', effects: [damageNode(k, { ...ctx, scale: d.trail.blast.damageScale ?? 0.35 })].filter(Boolean) }] : [{ type: 'field', radius: u(d.trail.zone.radius), duration: d.trail.zone.duration, interval: d.trail.zone.tickInterval ?? 0.5, pulse: [damageNode(k, { ...ctx, scale: d.trail.zone.damageScale ?? 0.3, noCrit: true })].filter(Boolean) }]; }
      if (d.plantOnLand) ctx.blocked.push('planting where the flight ends (an embed: row B16 constructs, authored by hand)');
      if (d.catch || d.catchSpot) ctx.blocked.push('catching returned blades for charges: an on_return charge, authored by hand');
      if (d.caroms) ctx.blocked.push('caroming anchors (row B25, later)');
      if (d.fissureTrail) ctx.blocked.push('fissure trails (a fissure fabric this port lacks)');
      return [n];
    }
    case 'ground': {
      ctx.attrs.range = u(d.castRange);
      ctx.attrs['area.radius'] = u(d.radius);
      const out = [];
      const impact = { type: 'area', shape: d.shape === 'sector' ? 'sector' : d.shape ?? undefined, angle: d.arcDeg, radius: 'weave.area.radius', center: 'point', filter: 'enemy', effects: hits };
      if (d.lingerDuration) {
        ctx.attrs.duration = d.lingerDuration;
        const f = { type: 'field', radius: 'weave.area.radius', duration: 'weave.duration', interval: d.tickInterval ?? 0.5 };
        if (d.shape && d.shape !== 'circle') { f.shape = d.shape; if (d.arcDeg) f.angle = d.arcDeg; }
        if (d.delay) f.delay = d.delay;
        if (d.exposure) f.exposure = d.exposure;
        if (d.pull) f.pull = u(d.pull);
        if (d.hitOnce) f.hit_once = true;
        if (d.rearmOnExit) f.rearm_on_exit = true;
        if (d.drift) f.drift = u(d.drift);
        if (d.seek) f.seek = u(d.seek.speed);
        if (d.rotate) f.rotate = d.rotate;
        if (d.grow) f.radius_curve = { from: 1, to: 1 + d.grow / d.radius, curve: 'linear' };
        if (d.sizeOver) f.radius_curve = { from: d.sizeOver.from ?? 1, to: d.sizeOver.to ?? 1, curve: d.sizeOver.curve ?? 'linear' };
        if (d.domain) { f.domain = modsOf(d.domain.enemyMods ?? d.domain.allyMods ?? d.domain.minionMods, ctx); f.domain_filter = d.domain.enemyMods ? 'enemy' : d.domain.minionMods ? 'minions' : 'ally_or_self'; }
        if (d.exclusive) f.exclusive = k.id;
        if (d.follow) f.follow = true;
        if (d.line) f.segments = { count: d.line.segments, spacing: u(d.line.spacing), axis: 'along' };
        if (d.occlusion === 'free') f.occlusion = 'free';
        f.pulse = [damageNode(k, { ...ctx, noCrit: true }), ...riders].filter(Boolean);
        if (d.emit) f.on_tick = [{ type: 'cast', weave: d.emit.skillId, at: 'point', aim: d.emit.bearing === 'out' ? 'out' : 'random' }];
        if (d.endBurst) f.on_end = [{ type: 'area', radius: `weave.area.radius * ${d.endBurst.radiusScale ?? 1}`, center: 'point', filter: 'enemy', effects: [damageNode(k, { ...ctx, scale: d.endBurst.damageScale })].filter(Boolean) }];
        if (!d.noImpact) out.push(impact);
        out.push(f);
      } else if (d.delay) {
        out.push({ type: 'scatter', count: 1, radius: 0, telegraph: d.delay, effects: [impact] });
      } else {
        out.push(impact);
      }
      if (d.pulse) { ctx.attrs['area.pulses'] = d.pulse.count ?? 1; ctx.notes.push('ground pulses repeat the area where it stood (delay and steps folded into area.pulses)'); }
      if (d.cascade) { ctx.attrs['area.cascade'] = d.cascade.count; out[0] = { type: 'cascade', dir: d.cascade.dir ?? 'forward', step: d.cascade.step ? u(d.cascade.step) : undefined, scale_step: d.cascade.scaleStep, damage_step: d.cascade.dmgStep ? +(d.cascade.dmgStep - 1).toFixed(2) : undefined, interval: d.cascade.interval, effects: [out[0]] }; }
      for (const f of ['fissure', 'sweep', 'pendulum', 'strobe', 'retract', 'marker', 'fillFrom', 'fillTime', 'leaveTerrain']) blockedField(f, `ground.${f} (${f === 'leaveTerrain' ? 'row C11, open' : 'a placement motion this port lacks'})`);
      drop('impactDress', 'impact dress is presentation');
      drop('exposureDomain', 'exposure domain dropped');
      return out;
    }
    case 'storm': {
      ctx.attrs.range = u(d.castRange);
      ctx.attrs['area.radius'] = u(d.hitRadius);
      if (!Array.isArray(d.count)) ctx.attrs['area.strikes'] = d.count;
      const n = { type: 'scatter', count: Array.isArray(d.count) ? countExpr(d.count) : undefined, radius: u(d.areaRadius), interval: d.interval || undefined, at: d.atEnemies ? 'enemies' : undefined, jitter: d.scatter ? u(d.scatter) : undefined, telegraph: d.telegraph || undefined };
      n.effects = [{ type: 'area', radius: 'weave.area.radius', center: 'point', filter: d.sky ? 'all' : 'enemy', effects: hits }];
      if (d.awaitRelease) ctx.notes.push('strikes armed during a channel fire on release: author with a channel release by hand');
      if (d.lob) ctx.notes.push('lobbed strikes are drawn as telegraphs');
      if (d.onGround) ctx.notes.push('ground-kind filters dropped');
      return [n];
    }
    case 'self': return hits.length ? hits : [];
    case 'dash': {
      ctx.attrs.range = u(d.distance);
      const n = { type: 'move', kind: 'dash', distance: 'weave.range', speed: u(d.speed) };
      if (d.width) { n.width = u(d.width); n.on_contact = hits; }
      if (d.phase) n.phase = true;
      if (d.corridorScale) ctx.notes.push('corridor damage scale folded into the hit');
      for (const f of ['decoyDuration', 'trailZone', 'trailVent', 'trailConjure']) blockedField(f, `dash.${f} (a fabric this port lacks)`);
      if (d.onContact) ctx.notes.push('on-contact specifics authored by hand');
      return [n];
    }
    case 'leap': {
      ctx.attrs.range = u(d.range);
      ctx.attrs['area.radius'] = u(d.radius);
      const n = { type: 'move', kind: 'leap', distance: 'weave.range', air_time: d.airTime, telegraph: d.telegraph ? true : undefined };
      n.on_land = [{ type: 'area', radius: 'weave.area.radius', center: 'point', filter: 'enemy', effects: hits }];
      blockedField('vent', 'leap.vent (fog banks, row C36)');
      return [n];
    }
    case 'blink': {
      ctx.attrs.range = u(d.range);
      return [{ type: 'move', kind: 'blink', distance: 'weave.range', delay: d.delay, behind: d.behindTarget ? true : undefined }, ...riders];
    }
    case 'mark': return [{ type: 'mark', key: k.id, at: 'point' }, ...riders];
    case 'detonate': return [{ type: 'detonate', tags: ['construct', 'mine'], effects: hits }];
    case 'detonateProjectile': return [{ type: 'detonate', tags: ['projectile'], pick: 'nearest', radius: u(d.radius), effects: hits }];
    case 'carom': ctx.blocked.push('carom delivery (row B25, later)'); return [];
    case 'summon': {
      const n = { type: 'summon' };
      if (d.pool) n.pool = d.pool.map(p => ({ pattern: patternOf(p.id, ctx), weight: p.weight, tags: tagsOf(p.tags) }));
      else n.pattern = patternOf(d.monsterId, ctx);
      ctx.attrs['summon.count'] = d.count;
      ctx.attrs['summon.max'] = d.maxActive;
      n.count = 'weave.summon.count';
      n.max_active = 'weave.summon.max';
      if (d.duration) { ctx.attrs.duration = d.duration; n.duration = 'weave.duration'; } else n.duration = 0;
      n.group = k.id;
      if (d.poolGroup) n.pool_group = d.poolGroup;
      if (d.exclusiveGroup) n.exclusive_group = d.exclusiveGroup;
      if (d.placeAt) n.place = { at: d.placeAt.at === 'cursor' ? 'point' : 'caster', range: d.placeAt.range ? u(d.placeAt.range) : undefined, scatter: d.placeAt.scatter ? u(d.placeAt.scatter) : undefined };
      if (d.persistent) n.contract = { reserve: { mana: d.persistent.reserve >= 1 ? `${d.persistent.reserve} * weave.summon.max` : `mana.max * ${d.persistent.reserve}` }, respawn: d.persistent.respawnTime, toggle: d.persistent.toggle ?? true };
      if (d.decay) n.decay = { delay: d.decay.delay, fraction: d.decay.frac, growth: d.decay.growth };
      if (d.waves) n.waves = { count: d.waves.count, interval: d.waves.interval, track_aim: d.waves.trackAim, cost_factor: d.waves.costFactor };
      if (d.presence) n.presence = modsOf(d.presence.minionMods, ctx);
      if (d.replenish) n.replenish = { interval: d.replenish.interval };
      if (d.fromCorpse) { n.from_corpse = 'target'; delete n.pattern; }
      for (const f of ['crewRules', 'crewSkills', 'crewAuras', 'crewMods', 'crewInherit', 'selectPool']) drop(f, `summon.${f}: the crew is our pattern's own kit`);
      for (const f of ['devour', 'shell', 'grimoire', 'strikeRelease', 'escort']) blockedField(f, `summon.${f} (row B22, later)`);
      return [n, ...riders];
    }
    case 'construct': {
      ctx.attrs.range = u(d.placeRange ?? d.range ?? 0);
      ctx.attrs['summon.max'] = d.maxActive;
      ctx.attrs.duration = d.duration;
      const base = T.CONSTRUCTS[d.kind] ?? 'device';
      const pattern = `${k.id}_device`;
      report.constructPatterns ??= [];
      report.constructPatterns.push({ id: pattern, extends: base, kind: d.kind, weaves: d.castSkillId ? [d.castSkillId] : [], life: d.life, invulnerable: d.invulnerable, radius: d.radius ? u(d.radius) : undefined, look: d.look, interval: d.interval, taunt: d.taunt, follows: d.follows, castOnStruck: d.castOnStruck, from: k.id });
      const n = { type: 'deploy', pattern, count: d.wallSegments ?? d.ring?.segments ?? 1, max_active: 'weave.summon.max', group: k.id, lifetime: 'weave.duration', inherit_caster: true };
      if (d.wallSegments) n.segments = { count: d.wallSegments, spacing: 1.2, axis: 'across' };
      if (d.ring) n.segments = { count: d.ring.segments, radius: u(d.ring.radius), axis: 'ring' };
      if (d.domeRadius) n.intercept = { radius: u(d.domeRadius), mode: d.domeMode ?? 'dissolve', slow: d.domeSlow };
      if (d.aura) n.on_birth = [{ type: 'aura', radius: u(d.aura.radius), domain: modsOf(d.aura.allyMods ?? d.aura.selfMods, ctx), domain_filter: 'ally_or_self' }];
      if (d.deathBurst) n.on_death = [{ type: 'area', radius: u(d.deathBurst.radius), center: 'point', filter: 'enemy', effects: [{ type: 'damage', amount: `body_life_max * ${d.deathBurst.fraction ?? 0.5}`, threads: [T.THREADS[d.deathBurst.damageType ?? 'fire']] }] }];
      if (d.healBurst) n.on_death = [{ type: 'area', radius: u(d.healBurst.radius), center: 'point', filter: 'ally', effects: [{ type: 'heal', amount: `min(${d.healBurst.cap}, body_life_max * ${d.healBurst.ratio})` }] }];
      if (d.propel) n.on_birth = [...(n.on_birth ?? []), { type: 'event', name: 'propel_pad' }], ctx.blocked.push('propelling pads (a launch pad fabric this port lacks)');
      for (const f of ['echo', 'embed', 'hatch', 'breakable', 'clearway']) blockedField(f, `construct.${f} (rows B16/D24, later)`);
      return [n, ...riders];
    }
    case 'aura': {
      const a = d.aura ?? {};
      ctx.attrs['area.radius'] = u(a.radius ?? 100);
      const n = { type: 'aura', radius: 'weave.area.radius', toggle: d.mode === 'toggle' ? true : undefined };
      if (d.mode === 'duration') { ctx.attrs.duration = d.duration ?? 10; n.duration = 'weave.duration'; }
      const allies = modsOf(a.allyMods, ctx);
      if (allies.length) { n.domain = allies; n.domain_filter = 'ally_or_self'; }
      const enemies = modsOf(a.enemyMods, ctx);
      if (enemies.length) { if (n.domain) ctx.notes.push('ally and enemy domains: the enemy domain is authored as a second aura by hand'); else { n.domain = enemies; n.domain_filter = 'enemy'; } }
      const selfMods = modsOf(a.selfMods, ctx);
      if (selfMods.length) ctx.selfMods = selfMods;
      if (a.enemyDps) {
        n.interval = 0.5;
        const amount = a.enemyDps.drainLifeFraction ? `source.life.max * ${a.enemyDps.drainLifeFraction} * 0.5` : `${(a.enemyDps.amount ?? 1) * 0.5} * spell.power`;
        n.pulse = [{ type: 'damage', amount, threads: [T.THREADS[a.enemyDps.type]], tags: ['dot'], can_crit: false }];
        if (a.enemyDps.drainLifeFraction) ctx.notes.push('the life drain the aura pays is its upkeep');
      }
      if (a.pulse) { n.interval = a.pulse.interval; n.pulse = [...(n.pulse ?? []), ...(a.pulse.healAllies ? [{ type: 'heal', amount: `target.${a.pulse.healAllies.base === 'maxMana' ? 'mana' : 'life'}.max * ${a.pulse.healAllies.amount}` }] : [])]; n.filter = a.pulse.healAllies ? 'ally_or_self' : 'enemy'; }
      if (a.siphonFraction) ctx.notes.push('siphon dropped');
      if (a.deathSpawn) ctx.blocked.push('death spawns from an aura (row D26, authored by hand)');
      const up = d.upkeep ?? {};
      if (up.reserveMana) ctx.reserve = { mana: up.reserveMana };
      const per = {};
      if (up.manaPerSec) per.mana = up.manaPerSec;
      if (up.manaPctMaxPerSec) { per.mana = up.manaPctMaxPerSec; ctx.notes.push('upkeep as a fraction of maximum mana: an expression by hand'); }
      if (up.lifeFractionPerSec) { per.life = up.lifeFractionPerSec; ctx.notes.push('upkeep as a fraction of maximum life: an expression by hand'); }
      if (Object.keys(per).length) ctx.upkeep = { per_second: per, ramp: up.rampPerSec };
      if (up.charges) ctx.blocked.push('charge-fed upkeep (row A17 charge lane, authored by hand)');
      for (const f of ['seal', 'overdrive', 'ledger', 'lockAboveLife', 'shellGuard']) blockedField(f, `aura.${f} (row B26 seals and ledgers, later)`);
      if (d.onDeactivate) ctx.onDeactivate = [{ type: 'cast', weave: d.onDeactivate.skillId, at: 'caster' }];
      return [n];
    }
    default:
      ctx.blocked.push(`delivery ${d.type} has no mapping`);
      return [];
  }
}

// ---------------------------------------------------------------- form-level parts per skill

function formParts(k, ctx) {
  const d = k.delivery;
  const targeting = { melee: 'direction', projectile: 'direction', cone: 'direction', nova: 'self', self: 'self', target: 'enemy', ground: 'ground', storm: 'ground', dash: 'direction', leap: 'ground', blink: 'ground', mark: 'ground', summon: 'self', construct: 'ground', aura: 'self', detonate: 'self', detonateProjectile: 'ground', carom: 'direction' }[d.type] ?? 'self';
  const parts = { targeting, mechanisms: [], tags: [], extra: {} };
  const attack = (k.tags ?? []).includes('attack');
  if (d.type === 'melee') { parts.mechanisms.push('hit', 'melee'); parts.tags.push('melee'); }
  if (['projectile', 'nova', 'cone', 'target', 'ground', 'storm'].includes(d.type) && k.baseDamage) parts.mechanisms.push('hit');
  if (d.type === 'projectile') parts.tags.push('projectile');
  if (['nova', 'cone', 'ground', 'storm'].includes(d.type) || (d.type === 'melee' && d.arcDeg >= 90)) parts.tags.push('area');
  if (d.type === 'summon') { parts.tags.push('summon', 'minion'); if (d.persistent) parts.tags.push('persistent'); }
  if (d.type === 'construct') { parts.tags.push('construct', ['trap', 'mine', 'pod'].includes(d.kind) ? 'trap' : 'totem'); parts.mechanisms.push('construct'); }
  if (['dash', 'leap', 'blink', 'mark'].includes(d.type)) { parts.tags.push('movement', 'mobile'); parts.mechanisms.push('movement'); }
  if (d.type === 'aura') { parts.tags.push('aura', 'mobile'); }
  if (k.castMove) parts.extra.cast_move = k.castMove;
  if (k.cooldownAt === 'press') parts.extra.cooldown_at = 'press';
  if (k.reflex) parts.extra.reflex = true;
  if (k.usableWhileGuarding) ctx.notes.push('usable while guarding: a reflex here');
  if (k.requiresGuard) ctx.behaviors.gates = ['has("guarding")'];
  if (k.gate) {
    const g = k.gate; const gates = [];
    if (g.charge) gates.push(`charge.${g.charge.id} >= ${g.charge.amount}`);
    if (g.buff) gates.push(`has("${T.status(g.buff) ?? g.buff}")`);
    if (g.resource) gates.push(`${g.resource.kind === 'es' ? 'shield' : g.resource.kind} >= ${g.resource.amount}`);
    if (g.missing) gates.push(`${g.missing.kind === 'any' ? 'life' : g.missing.kind === 'es' ? 'shield' : g.missing.kind}_pct <= ${g.missing.pct != null ? 1 - g.missing.pct : 0.99}`);
    if (g.guard) gates.push('has("guarding")');
    if (g.active) gates.push(`has("toggle.${g.active}")`);
    if (g.recentDamage) gates.push('has("recently_hurt")');
    if (g.holding) ctx.blocked.push('a held-grab gate (row C37)');
    if (gates.length) ctx.behaviors.gates = [...(ctx.behaviors.gates ?? []), ...gates];
  }
  const mode = k.castMode ?? 'cast';
  if (mode === 'channel' && k.channel) {
    const c = k.channel;
    const ch = { interval: 'weave.channel.interval', windup: c.windup ? 'weave.channel.windup' : undefined, move: 'weave.channel.move', turn_rate: c.turnRate ? 'weave.channel.turn_rate' : undefined, track_aim: c.trackAim || undefined, cooldown_on_end: c.cooldownOnEnd || undefined, max_hold: c.maxHold ? 'weave.channel.max_hold' : undefined };
    ctx.attrs['channel.interval'] = c.interval;
    if (c.windup) ctx.attrs['channel.windup'] = c.windup;
    ctx.attrs['channel.move'] = c.move === 'immobile' ? 0 : c.move === 'normal' ? 1 : c.moveFactor ?? 0.5;
    if (c.turnRate) ctx.attrs['channel.turn_rate'] = c.turnRate;
    if (c.maxHold) ctx.attrs['channel.max_hold'] = c.maxHold;
    if (c.intervalJitter) ch.jitter = c.intervalJitter[1];
    const ramps = [];
    if (c.ramp) ramps.push({ attribute: 'damage', form: 'more', per: c.ramp.per, max: c.ramp.max, curve: c.ramp.curve });
    if (c.rampAoe) ramps.push({ attribute: 'area.radius', form: 'increased', per: c.rampAoe.per, max: c.rampAoe.max, curve: c.rampAoe.curve });
    if (c.rampArc) ramps.push({ attribute: 'area.angle', form: 'increased', per: c.rampArc.per, max: c.rampArc.max, curve: c.rampArc.curve });
    if (c.rampMove) ramps.push({ attribute: 'move.speed', form: 'more', per: c.rampMove.per, min: c.rampMove.max, on: 'caster' });
    if (c.costRamp) ramps.push({ attribute: 'cost', form: 'increased', per: c.costRamp.per, max: c.costRamp.max });
    if (ramps.length) ch.ramps = ramps;
    if (c.release) { ch.release = { min_hold: c.release.minHold, require_full: c.release.requireFull, pulses: c.release.pulses === false ? false : undefined, __draft: JSON.stringify(c.release) }; ctx.notes.push(`channel release authored by hand from ${JSON.stringify(c.release)}`); }
    if (c.brim) ch.brim = { fill_time: c.brim.fillTime, decay: c.brim.decay, bank_at: c.brim.bankAt, min_release: c.brim.minRelease, spend: c.brim.spend, auto_release: c.brim.autoRelease, scale: { at_empty: c.brim.minScale ?? 0.5, at_full: c.brim.maxScale ?? 2, curve: c.brim.curve } };
    if (c.persist) ch.persist = { per_held_second: c.persist.perHeldSec, max_duration: c.persist.maxDuration, fade: c.persist.fade, min_hold: c.persist.minHold };
    if (c.releaseDetonate) ctx.notes.push('release detonation of own projectiles: a detonate in the release, authored by hand');
    parts.cast_mode = 'channel';
    parts.extra.channel = ch;
    parts.tags.push('channel');
  }
  if (mode === 'charge' && k.chargeUp) {
    const c = k.chargeUp;
    parts.cast_mode = 'charge';
    ctx.attrs['charge.max_time'] = c.maxTime;
    parts.extra.charge = { max_time: 'weave.charge.max_time', scales: [{ attribute: 'damage', form: 'more', at_zero: (c.minScale ?? 0.5) - 1, at_full: (c.maxScale ?? 2) - 1 }, ...(c.aoeScaleMax ? [{ attribute: 'area.radius', form: 'increased', at_zero: 0, at_full: c.aoeScaleMax - 1 }] : []), ...(c.speedAtFull ? [{ attribute: 'projectile.speed', form: 'increased', at_zero: 0, at_full: c.speedAtFull - 1 }] : [])] };
    if (c.arcTaper) ctx.notes.push('arc taper dropped');
  }
  if (mode === 'guard' && k.guard) {
    const g = k.guard;
    parts.cast_mode = 'guard';
    parts.targeting = 'self';
    parts.tags.push('guard');
    const guard = { arc: g.arcDeg, pool: 'weave.guard.pool', move: 'weave.guard.move', turn_rate: g.turnRate ? 'weave.guard.turn_rate' : undefined, parry: g.parry ? 'weave.guard.parry' : undefined, max_duration: g.maxDuration ? 'weave.guard.max_duration' : undefined, end_on_parry: g.endOnParry };
    ctx.attrs['guard.pool'] = g.shieldLife;
    ctx.attrs['guard.move'] = g.moveFactor ?? 0.3;
    if (g.turnRate) ctx.attrs['guard.turn_rate'] = g.turnRate;
    if (g.parry) ctx.attrs['guard.parry'] = g.parry.window;
    if (g.maxDuration) ctx.attrs['guard.max_duration'] = g.maxDuration;
    if (g.bash) {
      const b = g.bash;
      guard.bash = { threshold: b.threshold, arm_time: b.armTime, on_break: g.bashOnBreak, effects: [{ type: 'area', shape: 'sector', angle: b.arcDeg, radius: u(b.range), center: 'source', reach: 'edge', filter: 'enemy', effects: [{ type: 'damage', amount: `(lerp(weapon.damage.min, weapon.damage.max, roll) * ${b.mult} + guard.absorbed * 0.3) * attack.power`, threads: '$threads' }, ...(b.stunChance ? [{ type: 'apply_status', status: 'stunned', chance: b.stunChance, duration: 1 }] : []), ...(b.knockback ? [{ type: 'move', kind: 'knockback', distance: u(b.knockback) }] : [])] }] };
    }
    if (g.pulse) guard.pulse = { weave: g.pulse.skillId, interval: g.pulse.interval };
    if (g.parry?.counterMult) ctx.notes.push('parry counter multiplier folded into the bash');
    parts.extra.guard = guard;
    if (k.guardArts) ctx.notes.push('guard arts authored by hand');
  }
  if (['perfect', 'timed', 'multitude'].includes(mode)) {
    parts.extra.timing = { kind: mode, window: mode === 'multitude' ? 4 : 0.25, bonus: k.timing?.bonus ?? 0.5 };
  }
  if (mode === 'overcharge' && k.overcharge) {
    const o = k.overcharge;
    parts.cast_mode = 'overcharge';
    parts.extra.overcharge = { stages: o.stages, time: o.time, cost_per_stage: o.costPerStage ? { mana: o.costPerStage } : undefined, per_stage: [{ attribute: 'damage', form: 'more', value: o.perStage }, ...(o.aoePerStage ? [{ attribute: 'area.radius', form: 'increased', value: o.aoePerStage }] : [])], spark: o.window ? { window: o.window, bonus: o.windowBonus ?? 0.3 } : undefined };
  }
  if (mode === 'concentration') ctx.blocked.push('concentration casts (row A13, open)');
  if (k.targeting) {
    const t = k.targeting;
    const target = { kind: t.target === 'enemy' ? undefined : t.target, search_radius: t.searchRadius ? u(t.searchRadius) : undefined, fallback: t.fallback, consume: t.consumesStatus || t.consumesCorpse || undefined };
    if (t.requiresStatus) target.condition = [].concat(t.requiresStatus).map(s => `target.status.${T.status(s) ?? s} > 0`).join(' || ');
    if (t.requiresMonsterTags) target.condition = t.requiresMonsterTags.map(s => `target.has("${s}")`).join(' || ');
    if (t.target !== 'enemy' || t.requiresStatus || t.consumesStatus || t.fallback) parts.extra.target = Object.fromEntries(Object.entries(target).filter(([, v]) => v !== undefined));
    if (t.target === 'ally') parts.targeting = 'ally';
    if (t.target === 'corpse' || t.target === 'minion') parts.targeting = 'any';
    if (t.plural) ctx.notes.push('plural targeting (several corpses) is one target here');
    if (t.corpseLifeDamage || t.corpseLifeRestore || t.drainsTargetLife) ctx.notes.push('corpse life as damage or restoration: an expression over the consumed body, authored by hand');
    ctx.attrs.range = u(t.castRange);
  }
  if (k.useCharges) {
    const uc = k.useCharges;
    // A magazine's refill is the skill's own cooldown there, so the cooldown moves into the bank; a bank that
    // names no clock is manual ammunition, refilled only by its reload.
    const uses = { max: uc.max, recharge: uc.recharge };
    if (uc.magazine) {
      uses.magazine = uc.magazine === true ? { refill: k.cooldown || undefined } : { refill: uc.magazine.refill, drip: uc.magazine.drip ? true : undefined };
      if (uc.magazine === true) ctx.magazineClock = true;
    } else if (uc.recharge === undefined && !uc.empower) {
      uses.manual = true;
    }
    if (uc.empower) uses.empower = uc.empower;
    if (uc.still) uses.still = { bleed: uc.still.bleed };
    if (uc.ventAll) uses.vent_all = true;
    if (uc.stepsFromBank) uses.steps_from_bank = true;
    ctx.behaviors.uses = uses;
  }
  if (k.chargeCost) {
    const c = k.chargeCost;
    const spend = { charge: c.charge, amount: c.amount === 'all' ? undefined : c.amount, all: c.amount === 'all' ? true : undefined, minimum: c.minimum, optional: c.optional };
    const scales = [];
    if (c.damagePerCharge) scales.push({ attribute: 'damage', form: 'more', per_charge: c.damagePerCharge });
    if (c.projectilesPerCharge) scales.push({ attribute: 'projectile.count', form: 'flat', per_charge: c.projectilesPerCharge });
    if (c.repeatsPerCharge) scales.push({ attribute: 'repeat.count', form: 'flat', per_charge: c.repeatsPerCharge });
    if (scales.length) spend.scales = scales;
    ctx.behaviors.spend = Object.fromEntries(Object.entries(spend).filter(([, v]) => v !== undefined));
  }
  if (k.convert) ctx.behaviors.convert = k.convert.when === 'chargesEmpty' ? { when: 'slot.uses < 1', weave: k.convert.skillId } : { when: k.convert.when, weave: k.convert.skillId, __note: `convert rule '${k.convert.when}' becomes an expression by hand` };
  if (k.meta) ctx.behaviors.meta_action = { weave: k.meta.skillId, label: k.meta.label };
  if (k.followUp) ctx.behaviors.follow_up = { weave: k.followUp.skillId, delay: k.followUp.delay, chance: k.followUp.chance };
  if (k.comboChain) ctx.behaviors.chain = { steps: k.comboChain.skills, window: k.comboChain.window };
  if (k.castCycle) ctx.behaviors.cycle = { count: k.castCycle.count, effects: [buffStatus(k.castCycle.buff, k, ctx)] };
  if (k.selfStack) ctx.behaviors.self_stack = { max: k.selfStack.maxStacks, duration: k.selfStack.duration, decay: k.selfStack.decay === 'peel' ? 0.5 : undefined, modifiers: modsOf(k.selfStack.mods, ctx) };
  if (k.equipMods) ctx.behaviors.equip_modifiers = modsOf(k.equipMods, ctx);
  if (k.requirements) ctx.notes.push('requirements: on the codex by hand (' + Object.entries(k.requirements).map(([a, v]) => `${T.CORE_ATTRIBUTES[a] ?? a} ${v}`).join(', ') + ')');
  if (k.delivery.type === 'aura') {
    parts.extra.reserve = { mana: 'weave.reserve.mana' };
    parts.extra.upkeep = { per_second: { mana: 'weave.upkeep.mana', life: 'weave.upkeep.life' } };
    if (ctx.reserve) ctx.attrs['reserve.mana'] = ctx.reserve.mana;
    if (ctx.upkeep) { for (const [res, v] of Object.entries(ctx.upkeep.per_second)) ctx.attrs[`upkeep.${res}`] = v; if (ctx.upkeep.ramp) ctx.notes.push(`upkeep ramps by ${ctx.upkeep.ramp} a second: a form variant by hand`); }
    if (k.delivery.mode === 'toggle') parts.extra.toggle = {};
    if (ctx.onDeactivate) ctx.behaviors.__on_deactivate = ctx.onDeactivate;
    if (k.delivery.moveFactor != null) parts.extra.cast_move = k.delivery.moveFactor;
  }
  for (const f of ['fuse']) if (k[f]) parts.extra.__fuse = k.fuse.delay;
  const fabrics = { throng: 'the throng fabric (row C38)', amalgam: 'the amalgam (row C48)', invokes: 'invocations (row C48)', mimic: 'mimicry (row C38)', chrono: 'time holds (question Q10)', ultimate: 'super-art cut-ins (question Q10): the price floor stays as a cooldown', linkedHexes: 'linked hexes (row C48)', retaliate: 'retaliation spawns (row C48, a proc by hand)', hivecall: 'the hivecall contract', gauge: 'the gauge (a charge bank by hand)', pool: 'damage pools (a bank status by hand)', discharge: 'static discharge (a bank status by hand)', conduits: 'conduits (row H7, open)', concentration: 'concentration (row A13, open)', empower: 'crowd empowerment (row C47, open)', selfCleanse: 'the toll (row C48)', bondFeed: 'life bonds (row C3 bond)', variance: 'per-cast variance (row B31, by hand)', contagion: 'contagion (authored as an on-hit cast by hand)', aim: 'aim transforms (an aim wrapper by hand)', tether: 'innate tethers (a tether effect by hand)', chargeGain: 'passive charge taps (a proc by hand)', thresholds: 'level thresholds (named difference: growth is the formula)', tree: 'per-skill trees (trees belong to forms here)', leveling: 'leveling table (named difference: growth is the formula)', innateMods: 'innate modifiers (preset modifiers)', shatterStatus: 'consume_status before the hit, by hand', poiseReap: 'an amount over target.poise.max, by hand', currentLifeDamage: 'a cannot_kill damage over target.life, by hand', siphon: 'leech on the damage node, by hand', backstabMult: 'a vs.behind modifier, by hand', costScaling: 'pool-scaled costs: cost.pct_max attributes', minionCombat: 'per-body handling is the pattern behavior' };
  for (const [field, why] of Object.entries(fabrics)) {
    if (k[field] === undefined) continue;
    if (['throng', 'amalgam', 'invokes', 'mimic', 'linkedHexes', 'hivecall', 'conduits', 'concentration', 'selfCleanse', 'bondFeed'].includes(field)) ctx.blocked.push(why);
    else ctx.notes.push(why);
  }
  if (k.innateMods) ctx.modifiers.push(...modsOf(k.innateMods, ctx));
  if (k.ultimate) parts.tags.push('ultimate');
  return parts;
}

// ---------------------------------------------------------------- presets

const presets = {};
const ledgerSkills = [];
for (const k of Object.values(skills)) {
  const family = familyOf(k);
  if (!inPort.has(k.id)) {
    ledgerSkills.push({ id: k.id, name: k.name, family, ours: '', mechanism: `${k.delivery?.type} kit skill`, status: 'not ported', note: 'a monster kit skill (noDrop); its monster is not in this port and the bestiary port brings its kit', proof: '' });
    continue;
  }
  const ctx = { attack: (k.tags ?? []).includes('attack'), notes: [], blocked: [], seams: [], attrs: {}, modifiers: [], behaviors: {} };
  const { threads, dropped: droppedThreads, types } = threadsOf(k);
  if (droppedThreads.length) ctx.notes.push(`a third damage type (${droppedThreads.join(', ')}) is folded into the first two`);
  const base = { melee: 'swing', projectile: ctx.attack ? 'shot' : 'missile', nova: ctx.attack ? 'whirl' : 'burst', cone: ctx.attack ? 'sweep' : 'breath', target: ctx.attack ? 'thrust' : 'smite', ground: ctx.attack ? 'slam' : 'lay', storm: ctx.attack ? 'barrage' : 'hail', self: ctx.attack ? 'cry' : 'boon', dash: 'stride', leap: 'vault', blink: 'flicker', mark: 'waymark', summon: k.delivery.persistent ? 'pact' : 'call', construct: ['trap', 'mine', 'pod'].includes(k.delivery.kind) ? 'trap' : 'plant', aura: 'mantle', detonate: 'spark', detonateProjectile: 'spark', carom: 'carom' }[k.delivery.type] ?? 'rite';
  const graph = graphOf(k, ctx);
  const parts = formParts(k, ctx);
  const formId = shell(base, k, parts, aiOf(k, family, 6).role);
  const attrs = { 'cast.time': k.useTime || undefined, 'cooldown.base': ctx.magazineClock ? undefined : (k.cooldown || undefined) };
  const resource = ctx.attack ? 'stamina' : 'mana';
  attrs[`cost.base.${resource}`] = ctx.attack ? k.manaCost * 3 : k.manaCost;
  if (k.lifeCost) attrs['cost.base.life'] = k.lifeCost;
  if (k.costScaling?.manaPctMax) attrs['cost.pct_max.mana'] = k.costScaling.manaPctMax;
  if (k.costScaling?.lifePctMax) attrs['cost.pct_max.life'] = k.costScaling.lifePctMax;
  if (k.baseDamage) {
    const totals = Object.values(k.baseDamage).reduce((a, [lo, hi]) => [a[0] + lo, a[1] + hi], [0, 0]);
    const m = (totals[0] + totals[1]) / 2;
    if (ctx.attack) attrs['damage.effectiveness'] = +(m / 9).toFixed(2); else attrs.base_damage = +m.toFixed(1);
  }
  if (k.addedEffectiveness != null) attrs['added.effectiveness'] = k.addedEffectiveness;
  Object.assign(attrs, ctx.attrs);
  const structural = new Set(['attack', 'spell', 'melee', 'projectile', 'area', 'summon', 'minion', 'construct', 'movement', 'mobile', 'aura', 'channel', 'guard', 'persistent', 'totem', 'trap', 'shout']);
  const themeTags = tagsOf(k.tags).filter(t => !structural.has(t) && !T.THREADS[t] && !['ember', 'tide', 'storm', 'stone', 'void', 'umbra'].includes(t));
  if (themeTags.length) ctx.seams.push(`preset tags (${themeTags.join(', ')})`);
  const preset = {
    id: k.id,
    name: k.name,
    description: k.description,
    form: formId,
    threads,
    tags: themeTags.length ? themeTags : undefined,
    color: k.color,
    attributes: Object.fromEntries(Object.entries(attrs).filter(([, v]) => v !== undefined)),
    modifiers: ctx.modifiers.length ? ctx.modifiers : undefined,
    ai: aiOf(k, family, attrs.range ?? 4),
    effects: graph,
    __comment: [`${k.id}: ${k.delivery.type}${k.castMode ? ' ' + k.castMode : ''}, prototype types ${types.join('+') || 'none'}${k.noDrop ? ', a payload of another skill' : ''}`],
  };
  if (ctx.selfMods) preset.modifiers = [...(preset.modifiers ?? []), ...ctx.selfMods.map(m => ({ ...m, __on: 'caster' }))], ctx.notes.push('aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand');
  if (k.cooldownAt === 'press') preset.cooldown_at = 'press';
  const behaviorKeys = Object.keys(ctx.behaviors).filter(b => !b.startsWith('__'));
  if (behaviorKeys.length) { Object.assign(preset, Object.fromEntries(Object.entries(ctx.behaviors).filter(([b]) => !b.startsWith('__')))); ctx.seams.push(`preset behaviors (${behaviorKeys.join(', ')})`); }
  if (ctx.behaviors.__on_deactivate) { preset.toggle = { on_deactivate: ctx.behaviors.__on_deactivate }; ctx.seams.push('preset behaviors (toggle on_deactivate)'); }
  if (ctx.notes.length) preset.__comment.push('notes: ' + ctx.notes.join('; '));
  if (ctx.seams.length) preset.__comment.push('SEAM: ' + ctx.seams.join('; '));
  if (ctx.blocked.length) preset.__comment.push('BLOCKED: ' + ctx.blocked.join('; '));
  (presets[family] ??= []).push(preset);
  const status = statusOf(ctx);
  ledgerSkills.push({ id: k.id, name: k.name, family, ours: status === 'not ported' ? '' : k.id, mechanism: `preset on ${formId}: ${graph.map(n => n.type).join(', ')}${threads.length ? ` (${threads.join('+')})` : ''}`, status, note: noteOf(ctx), proof: `fight port duel: ${family}` });
}

// ---------------------------------------------------------------- glyphs from supports

const glyphs = [];
const ledgerSupports = [];
const GRAFTS = {
  trigger: (g, s, ctx) => { const t = T.TRIGGER_KINDS[s.trigger.on]; if (!t) { ctx.blocked.push(`trigger kind ${s.trigger.on} (a high-roll trigger kind: seam)`); return; } g.trigger = { on: t.on, tags: t.tags, chance: s.trigger.chance, icd: s.trigger.icd, life_fraction: s.trigger.lifeFrac, status: s.trigger.status ? T.status([].concat(s.trigger.status)[0]) : undefined, power: s.trigger.power, max_cast_time: s.trigger.maxUseTime }; if (s.trigger.guaranteed) g.trigger.chance = 1; if (s.trigger.startsOff) ctx.notes.push('starts disarmed: every trigger arms by its key here'); },
  meta: (g, s) => { g.meta_action = { weave: s.meta.skillId, label: s.meta.label }; },
  chargeGain: (g, s, ctx) => { for (const c of s.chargeGain) { const hook = { hit: 'on_hit', kill: 'on_kill', use: 'on_cast', channelFinish: null, takeHit: null, block: null, enemyDeath: null, allyDeath: null, second: null }[c.on]; if (!hook) { ctx.notes.push(`charge tap on ${c.on} becomes a proc by hand`); continue; } (g.hooks ??= {})[hook] = [...(g.hooks[hook] ?? []), { type: 'charge', id: c.charge, gain: c.amount, max: c.max, self: true }]; } },
  tether: (g, s, ctx) => { const t = s.tether; if (t.link === 'network') ctx.notes.push('a network tether links each object to the caster here'); (g.adds_effects ??= []).push({ type: 'tether', link: t.link === 'target' ? 'target' : 'owner', width: u(t.width ?? 8), duration: t.duration ?? 'weave.duration', interval: 0.5, filter: t.affects === 'allies' ? 'ally' : 'enemy', pulse: t.dps ? [{ type: 'damage', amount: `${t.dps * 0.5} * spell.power`, threads: [T.THREADS[t.damageType ?? 'physical']], tags: ['dot'], can_crit: false }] : [{ type: 'heal', amount: `${(t.healPerSec ?? 1) * 0.5}` }] }); },
  followUp: (g, s) => { g.follow_up = { weave: s.followUp.skillId, delay: s.followUp.delay, chance: s.followUp.chance }; },
  cascade: (g, s) => { (g.patches ??= []).push({ type: 'area', where: 'root', wrap: { type: 'cascade', dir: s.cascade.dir ?? 'forward', interval: s.cascade.interval ?? 0.15, damage_step: s.cascade.dmgStep ? +(s.cascade.dmgStep - 1).toFixed(2) : undefined }, mode: 'first' }); (g.modifiers ??= []).push({ attribute: 'area.cascade', form: 'flat', value: s.cascade.count }); },
  trail: (g, s) => { const t = s.trail; (g.hooks ??= {}).on_travel = t.blast ? [{ type: 'area', radius: u(t.blast.radius), center: 'point', filter: 'enemy', effects: [{ type: 'damage', amount: `weave.base_damage * spell.power * ${t.blast.damageScale ?? 0.35}`, threads: '$threads' }] }] : [{ type: 'field', radius: u(t.zone.radius), duration: t.zone.duration, interval: t.zone.tickInterval ?? 0.5, pulse: [{ type: 'damage', amount: `weave.base_damage * spell.power * ${t.zone.damageScale ?? 0.3}`, threads: '$threads', tags: ['dot'], can_crit: false }] }]; (g.patches ??= []).push({ type: 'projectile', set: { travel: u(t.every) }, mode: 'first' }); },
  sequel: (g, s) => { (g.hooks ??= {}).on_end = [{ type: 'cast', weave: s.sequel.skillId, at: 'point', damage_scale: s.sequel.damageScale ?? 1 }]; },
  chargeCost: (g, s) => { const c = s.chargeCost; g.spend = { charge: c.charge, amount: c.amount === 'all' ? undefined : c.amount, all: c.amount === 'all' || undefined, optional: c.optional, scales: c.damagePerCharge ? [{ attribute: 'damage', form: 'more', per_charge: c.damagePerCharge }] : undefined }; },
  aim: (g, s) => { const a = s.aim; (g.patches ??= []).push({ type: 'area', where: 'root', wrap: a.sequence ? { type: 'aim', sequence: a.sequence.steps } : { type: 'aim', spread: a.random.spreadDeg, offset: a.random.offsetDeg }, mode: 'first' }); },
  useChargeGraft: (g, s) => { const c = s.useChargeGraft; g.uses = { max: c.rounds, recharge: c.recharge, empower: c.empower, still: c.still }; },
  munition: (g, s) => { g.uses = { max: s.munition.rounds, manual: true }; g.convert = { when: 'slot.uses < 1', weave: s.munition.reloadSkillId ?? 'quiver_restring' }; },
  gate: (g, s) => { if (s.gate.guard) g.gates = ['has("guarding")']; },
  selfStack: (g, s, ctx) => { g.self_stack = { max: s.selfStack.maxStacks, duration: s.selfStack.duration, modifiers: modsOf(s.selfStack.mods, ctx) }; },
  fuse: (g, s) => { (g.patches ??= []).push({ type: 'damage', wrap: { type: 'fuse', delay: s.fuse.delay } }); },
  triggerPermit: (g) => { g.adds_mechanisms = [...(g.adds_mechanisms ?? []), 'trigger_permit']; },
  spreadOnHit: (g, s) => { const c = s.spreadOnHit; (g.hooks ??= {}).on_hit = [...(g.hooks.on_hit ?? []), { type: 'condition', if: `rand() < ${c.chance}`, then: [{ type: 'spread_status', kind: 'harmful', count: 1, radius: u(c.radius), strength: c.strengthScale, duration: c.duration === 'refresh' ? 'refresh' : 'remaining', max_targets: 1 }] }]; },
  exposure: (g, s) => { (g.patches ??= []).push({ type: 'field', set: { exposure: s.exposure.after }, mode: 'first' }); },
  zoneGrow: (g, s) => { (g.patches ??= []).push({ type: 'field', set: { radius_curve: { from: 1, to: 1 + s.zoneGrow / 60, curve: 'linear' } }, mode: 'first' }); },
  zoneFollow: (g) => { (g.patches ??= []).push({ type: 'field', set: { follow: true }, mode: 'first' }); },
  zoneEmit: (g, s) => { (g.hooks ??= {}).on_tick = [{ type: 'cast', weave: s.zoneEmit.skillId, at: 'point', aim: 'random' }]; },
  pulse: (g, s) => { (g.modifiers ??= []).push({ attribute: 'area.pulses', form: 'flat', value: 1 }); g.__note = `pulse delay ${s.pulse.delay}: rule delivery.pulse_interval`; },
  auraDuration: (g, s) => { (g.patches ??= []).push({ type: 'aura', set: { toggle: false, duration: s.auraDuration.seconds }, mode: 'first' }); },
  summon: (g, s, ctx) => { (g.adds_effects ??= []).push({ type: 'summon', pattern: patternOf(s.summon.monsterId, ctx), count: s.summon.count, max_active: s.summon.maxActive, duration: s.summon.duration, group: s.id }); },
  dominate: (g, s) => { (g.hooks ??= {}).on_kill = [{ type: 'condition', if: `rand() < ${s.dominate.chance}`, then: [{ type: 'summon', from_corpse: 'target', count: 1, max_active: s.dominate.max, duration: s.dominate.duration, group: s.id }] }]; },
  sacrifice: (g, s) => { (g.hooks ??= {}).on_cast = [{ type: 'consume_minion', count: 1, pick: 'nearest', radius: u(s.sacrifice.radius), self: true }]; (g.modifiers ??= []).push({ attribute: 'damage', form: 'more', value: s.sacrifice.dmgPerLife * 40, __note: 'per point of consumed life: a gauge over consumed_life by hand' }); },
  spawnBuff: (g, s, ctx) => { (g.hooks ??= {}).on_birth = [buffStatus(s.spawnBuff, { id: s.id, name: s.name, color: s.color }, ctx)]; },
  healField: (g, s) => { (g.patches ??= []).push({ type: 'field', set: { filter: 'ally_or_self', pulse: [{ type: 'heal', amount: s.healField.amount }] }, mode: 'first' }); },
  constructFx: (g, s, ctx) => { ctx.notes.push('construct fx (a standing pulse or arrival burst) authored as on_birth and the device kit by hand'); },
  grantsTags: (g, s) => { g.adds_tags = tagsOf(s.grantsTags); },
  dropTags: () => {},
  maxLevel: (g, s, ctx) => { ctx.notes.push(`levels cap at ${s.maxLevel}: rule glyphs.max_level is global`); },
  levelBonus: (g, s) => { g.level_bonus = s.levelBonus; },
  levelBonusPer: (g, s) => { g.level_bonus_per_level = s.levelBonusPer; },
  minDropLevel: (g, s) => { g.min_level = s.minDropLevel; },
  weight: (g, s) => { g.drop_weight = s.weight; },
  targeting: (g, s, ctx) => { ctx.notes.push('a targeting spec is form-level here: a form variant by hand'); },
  overcharge: (g, s, ctx) => { ctx.blocked.push('an overcharge graft: cast modes are form-level (a form variant per host, named difference)'); },
  strikeTiming: (g, s, ctx) => { ctx.blocked.push('a timing graft: cast modes are form-level (named difference)'); },
  conduit: (g, s, ctx) => { ctx.blocked.push('conduits (row H7, open)'); },
  contagion: (g, s, ctx) => { ctx.blocked.push('contagion re-release needs cast { weave: host } (seam)'); },
  curseOnHit: (g, s, ctx) => { ctx.blocked.push('curse on hit needs cast { weave: host } at a scale plus a reservation (row D25 seam)'); },
  curseField: (g, s, ctx) => { ctx.blocked.push('curse fields need cast { weave: host } (row D25 seam)'); },
  rollBase: (g, s, ctx) => { g.__axes_draft = JSON.stringify(s.rollBase).slice(0, 600); ctx.notes.push('a chassis: axes authored by hand from rollBase'); },
  echo: (g, s, ctx) => { ctx.blocked.push('echo riders (row D24, later)'); },
  devour: (g, s, ctx) => { ctx.blocked.push('devouring (row B22, later)'); },
  brood: (g, s, ctx) => { ctx.blocked.push('broods born of damage (row D26, by hand)'); },
  birth: (g, s, ctx) => { ctx.blocked.push('birth grafts (a clutch fabric this port lacks)'); },
  corpseSpawn: (g, s, ctx) => { ctx.blocked.push('corpse spawns (row D26, by hand)'); },
  throngSource: (g, s, ctx) => { ctx.blocked.push('throng sources (row C38)'); },
  tameMod: (g, s, ctx) => { ctx.blocked.push('companion grafts (row C35)'); },
  madden: (g, s, ctx) => { ctx.blocked.push('maddening (a lash-out fabric, by hand)'); },
  massGraft: (g, s, ctx) => { ctx.blocked.push('the mass fabric is not ported'); },
  breakableGraft: (g, s, ctx) => { ctx.blocked.push('breakable constructs with death bursts (row B16, by hand)'); },
  guardBash: (g, s, ctx) => { ctx.blocked.push('a bash graft: guards are form-level (named difference)'); },
  guardCast: (g, s, ctx) => { g.reflex_note = true; ctx.notes.push('guarded casting: a reflex gated on guarding'); g.gates = ['has("guarding")']; },
  minionAuraPool: (g, s, ctx) => { ctx.blocked.push('random minion auras (row D26, by hand)'); },
  minionAura: (g, s, ctx) => { ctx.blocked.push('minion auras (row D26, by hand)'); },
  orbShedGraft: (g, s, ctx) => { ctx.blocked.push('resource orbs (row C23, open)'); },
  shellGraft: (g, s, ctx) => { ctx.blocked.push('shell guards (row B26, later)'); },
  fissureVolatile: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  fissureAftershock: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  fissureRoulette: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  fissureReclose: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  fissurePath: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  fissureTrail: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  meleeFissure: (g, s, ctx) => { ctx.blocked.push('fissures (a fabric this port lacks)'); },
  gather: (g, s, ctx) => { ctx.blocked.push('gather converts (a channel brim by hand)'); },
  releaseOrder: (g, s, ctx) => { ctx.notes.push('release order dropped'); },
  variance: (g, s, ctx) => { ctx.notes.push('variance: rand() in the patched parameters by hand'); },
  cadence: (g, s, ctx) => { ctx.notes.push('cadence authored by hand'); },
  healOverTime: (g, s, ctx) => { ctx.notes.push('heal over time: a restoring status by hand'); },
  reserveLife: (g, s, ctx) => { ctx.notes.push('life reservation is form-level: a form variant by hand'); },
  turret: (g, s, ctx) => { ctx.blocked.push('turrets (row B16, later)'); },
  pendulum: (g, s, ctx) => { ctx.blocked.push('pendulum placements (a placement motion this port lacks)'); },
  zoneSizeOver: (g, s, ctx) => { (g.patches ??= []).push({ type: 'field', set: { radius_curve: { from: s.zoneSizeOver.from ?? 1, to: s.zoneSizeOver.to ?? 1, curve: 'linear' } }, mode: 'first' }); },
  resonance: (g) => { g.adds_mechanisms = [...(g.adds_mechanisms ?? []), 'resonance']; },
};
for (const s of Object.values(S.supports)) {
  const ctx = { notes: [], blocked: [], dropped: [] };
  const g = { id: s.id, name: s.name, description: s.description, color: s.color };
  const req = tagsOf(s.requiresTags);
  if (req.length) g.requires = req;
  const exc = tagsOf(s.excludeTags);
  if (exc.length) g.excludes = exc;
  if (s.requiresMechanisms) {
    const ms = s.requiresMechanisms.map(m => ({ m, ours: mechanismOf(m) }));
    g.requires_mechanisms = ms.filter(x => x.ours).map(x => x.ours);
    for (const x of ms.filter(x => !x.ours)) ctx.blocked.push(`mechanism ${x.m} has no probe (flask lanes)`);
  }
  g.modifiers = modsOf(s.mods, ctx);
  if (s.perLevel?.length) g.per_level = modsOf(s.perLevel, ctx);
  for (const [field, fn] of Object.entries(GRAFTS)) if (s[field] !== undefined) fn(g, s, ctx);
  for (const field of Object.keys(s)) if (!['id', 'name', 'description', 'color', 'mods', 'perLevel', 'requiresTags', 'excludeTags', 'requiresMechanisms'].includes(field) && !GRAFTS[field]) ctx.notes.push(`field ${field} dropped`);
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  g.__comment = [`${s.id}${s.requiresTags ? ' fits ' + s.requiresTags.join('/') : ''}`];
  if (ctx.notes.length) g.__comment.push('notes: ' + ctx.notes.join('; '));
  if (ctx.blocked.length) g.__comment.push('BLOCKED: ' + ctx.blocked.join('; '));
  if (!g.modifiers.length) delete g.modifiers;
  glyphs.push(g);
  const mech = [g.trigger && 'trigger', g.meta_action && 'meta', g.hooks && `hooks ${Object.keys(g.hooks).join('/')}`, g.patches && 'patches', g.spend && 'spend', g.uses && 'uses', g.follow_up && 'follow_up', g.self_stack && 'self_stack', g.adds_effects && 'effects', (g.modifiers?.length || g.per_level?.length) && 'modifiers', g.adds_tags && 'tags'].filter(Boolean).join(', ') || 'modifiers';
  ledgerSupports.push({ id: s.id, name: s.name, ours: statusOf(ctx) === 'not ported' ? '' : s.id, mechanism: `glyph: ${mech}`, status: statusOf(ctx), note: noteOf(ctx), proof: 'weave report; glyph duel' });
}

// ---------------------------------------------------------------- statuses

const ourRoster = new Set(['burning', 'bleeding', 'chilled', 'frozen', 'shocked', 'sapped', 'withered', 'dazzled', 'unraveling', 'addled', 'stunned', 'rooted', 'silenced', 'hasted', 'fortified', 'regrowth', 'hidden', 'shield_delay', 'poise_broken', 'poise_rearming', 'stagger', 'death_cheated', 'poison', 'scalding', 'hemorrhage', 'impaled', 'doom', 'kindling_brand', 'vulnerable', 'weakened', 'brittle', 'exposed', 'blinded', 'disarmed', 'taunted', 'muddled', 'honed', 'searing_edge', 'restoring', 'warded_shell', 'shell_glow', 'iron_ward', 'battle_trance']);
const statuses = [];
const ledgerStatuses = [];
const magnitudeRules = {};
for (const [pid, st] of Object.entries(S.statuses)) {
  const ours = T.status(pid);
  const ctx = { notes: [], blocked: [], dropped: [] };
  if (ours === null) { ledgerStatuses.push({ id: pid, name: st.label, ours: '', mechanism: '', status: 'not ported', note: 'a world or fabric state (water, clouds, vents, the fourth wall, time) outside this port', proof: '' }); continue; }
  const thread = st.element ? T.THREADS[st.element] : undefined;
  const dotThread = st.dotType ? (st.dotType === 'chaos' ? (T.UMBRA_WORDS.test(pid) ? 'umbra' : 'void') : T.THREADS[st.dotType]) : undefined;
  const e = { id: ours, name: st.label, tags: [st.beneficial ? 'buff' : st.dotType ? 'ailment' : st.hardCC || st.forbidsTags ? 'control' : 'debuff'] };
  if (thread) e.tags.push(thread === 'void' && dotThread ? dotThread : thread); else if (dotThread) e.tags.push(dotThread);
  if (st.dotType) e.tags.push('dot');
  if (st.hardCC) e.tags.push('control');
  e.duration = st.duration;
  if (st.stacking) { e.stack_policy = 'stack'; e.max_stacks = st.maxStacks ?? 1; } else e.stack_policy = st.stackPolicy === 'strongest' ? 'strongest' : 'refresh';
  if (st.beneficial) e.beneficial = true;
  const mods = modsOf(st.mods, ctx);
  if (st.interruptChance) mods.push({ attribute: 'cast.fizzle_chance', form: 'flat', value: st.interruptChance });
  if (mods.length) e.modifiers = mods;
  if (st.modsPerStack) e.modifiers_per_stack = true;
  const flags = [];
  if (st.hardCC) flags.push('stunned');
  if (st.panic) flags.push('panicked');
  if (st.smellsOfPrey) flags.push('prey');
  if (st.neverRetreats) flags.push('never_retreats');
  if (st.flight) flags.push('flying');
  if (flags.length) e.flags = flags;
  if (st.forbidsTags) e.forbids_tags = tagsOf(st.forbidsTags);
  if (st.buildup) e.buildup_into = T.status(st.buildup.into) ?? st.buildup.into;
  if (st.propagateOnDeath) e.propagate_on_death = true;
  if (st.powerInert) e.scales_with_potency = false;
  if (st.baseline) e.baseline = `${st.baseline.dps} + level * ${st.baseline.perLevel}`;
  if (st.dotType) {
    let amount = st.stacking ? 'amount * stacks' : 'amount';
    if (st.dpsCurve === 'ramp') amount = `${amount} * 2 * elapsed / total`;
    if (st.dpsCurve === 'taper') amount = `${amount} * 2 * (1 - elapsed / total)`;
    e.tick = { interval: 0.5, effects: [{ type: 'damage', amount, threads: [dotThread], tags: ['dot'], can_crit: false }] };
    if (!st.refreshCaster) e.keep_source = true;
  }
  if (st.bank) e.bank = { from: 'applications', cap: st.bank.capMul };
  if (st.pop) e.on_reapply = [{ type: 'damage', amount: `remaining_damage * (${st.pop.fraction} + pop)`, threads: [dotThread ?? thread ?? 'stone'], tags: ['dot', 'pop'], can_crit: false }];
  if (st.cullsAtLethal) { e.bank ??= { from: 'applications' }; e.on_bank_lethal = [{ type: 'rupture', status: ours }]; }
  if (st.dischargeOnHit) { e.bank ??= { from: 'applications' }; e.consume_on = { event: 'hurt', count: 1 }; e.on_bearer_struck = [{ type: 'rupture', status: ours, fraction: 1 }]; if (st.dischargeMatchingType) ctx.notes.push('discharges on any hit, not only its own type'); }
  if (st.zapNearby) e.tick = { interval: st.zapNearby.interval, effects: [...(e.tick?.effects ?? []), { type: 'area', radius: u(st.zapNearby.radius), center: 'source', include_center: false, filter: 'enemy', effects: [{ type: 'damage', amount: `(${st.baseline?.dps ?? 2} + level * ${st.baseline?.perLevel ?? 0.5}) * ${st.zapNearby.factor}`, threads: [thread ?? 'storm'], tags: ['dot'], can_crit: false }] }] };
  if (st.lashOut) e.tick = { interval: st.lashOut.interval, effects: [{ type: 'area', radius: u(st.lashOut.radius), center: 'source', include_center: false, filter: 'all', max_targets: 1, pick: 'nearest', effects: [{ type: 'damage', amount: `(${st.baseline?.dps ?? 2} + level * ${st.baseline?.perLevel ?? 0.5}) * ${st.lashOut.factor}`, threads: [thread ?? 'mind'], tags: ['dot'], can_crit: false }] }] };
  if (st.boundToAbsorb) ctx.notes.push('bound to an absorption pool: bound_to names the pool status by hand');
  if (st.hitMagnitude && ours) magnitudeRules[ours] = `amount * ${st.hitMagnitude}`;
  for (const [f, why] of Object.entries({ timeScale: 'personal time scales (row E14, later)', invertMove: 'inverted movement (row E14, later)', frameLock: 'frame locks are rejected (question Q10)', scrambleChance: 'scrambled casts (row A21, later)', weakSpot: 'weak spots (row E15, open)', fickleSpan: 'attention lapses (an AI flag by hand)' })) if (st[f] !== undefined) ctx.blocked.push(why);
  const meta = {};
  for (const f of ['screenCue', 'bodyCue', 'bodyFx', 'voice', 'armedCue', 'conceals', 'ghostAlpha']) if (st[f] !== undefined) meta[f] = st[f];
  if (Object.keys(meta).length) e.meta = { presentation: 'from the prototype, for the clients', ...Object.fromEntries(Object.entries(meta).map(([k, v]) => [k, typeof v === 'object' ? JSON.stringify(v).slice(0, 120) : v])) };
  e.color = st.color;
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  const existing = ourRoster.has(ours);
  e.__comment = [`${pid}${existing ? ` is ${ours} of our roster already` : ''}`, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : []), ...(ctx.blocked.length ? ['BLOCKED: ' + ctx.blocked.join('; ')] : [])];
  if (!existing) statuses.push(e);
  if (existing) ctx.notes.unshift(`already in the roster as ${ours}`);
  ledgerStatuses.push({ id: pid, name: st.label, ours, mechanism: [e.tick && 'tick', e.bank && 'bank', e.on_reapply && 'pop', e.consume_on && 'consume_on', e.on_bearer_struck && 'on_bearer_struck', e.flags && `flags ${e.flags.join('/')}`, e.forbids_tags && 'forbids', e.buildup_into && 'buildup', e.modifiers && 'modifiers'].filter(Boolean).join(', ') || 'status', status: statusOf(ctx), note: noteOf(ctx), proof: 'status duel' });
}

// ---------------------------------------------------------------- procs

const procs = [];
const ledgerProcs = [];
function procEffect(p, e, ctx) {
  switch (e.type) {
    case 'extraHit': return { type: 'damage', amount: `last_damage * ${e.damageScale}`, threads: '$threads', tags: ['proc'] };
    case 'explosion': return { type: 'area', radius: u(e.radius), center: 'focus', filter: 'enemy', effects: [{ type: 'damage', amount: `last_damage * ${e.damageScale}`, threads: '$threads', tags: ['proc'] }] };
    case 'arc': return { type: 'chain', count: e.hops, range: u(e.range), effects: [{ type: 'damage', amount: `last_damage * ${e.damageScale}`, threads: '$threads', tags: ['proc'] }] };
    case 'displace': return { type: 'move', kind: e.force >= 0 ? 'knockback' : 'pull', distance: u(Math.abs(e.force)) };
    case 'collisionDamage': return { type: 'damage', amount: `last_damage * ${e.damageScale}`, threads: '$threads', tags: ['proc'] };
    case 'gainCharge': return { type: 'charge', id: e.charge, gain: e.amount, max: e.max, self: true };
    case 'buff': return buffStatus(e.buff, { id: p.id, name: p.name, color: p.color }, ctx);
    case 'summon': return { type: 'summon', pattern: patternOf(e.monsterId, ctx), count: 1, max_active: e.max, duration: e.duration, group: p.id };
    case 'birth': ctx.blocked.push('births (a clutch fabric this port lacks)'); return null;
    case 'status': return { type: 'apply_status', status: T.status(e.status) ?? e.status, magnitude: e.magnitude != null ? `baseline * ${e.magnitude}` : undefined };
    case 'heal': return { type: 'heal', amount: [e.flat, e.pctMax ? `life.max * ${e.pctMax}` : null].filter(v => v != null).join(' + ') || '0', self: true };
    case 'restore': return { type: 'resource', resource: e.resource === 'es' ? 'shield' : e.resource, amount: [e.flat, e.pctMax ? `${e.resource === 'es' ? 'shield' : e.resource}.max * ${e.pctMax}` : null].filter(v => v != null).join(' + ') || '0', self: true };
    case 'burst': return { type: 'area', radius: u(e.radius), center: 'source', filter: 'enemy', effects: [{ type: 'damage', amount: `${e.base} + level * ${e.perLevel}`, threads: [T.THREADS[e.damage]], tags: ['proc'] }] };
    case 'delayedBurst': return { type: 'delay', seconds: e.delay, effects: [{ type: 'area', radius: u(e.radius), center: e.at === 'self' ? 'source' : 'focus', filter: 'all', effects: [...(e.damage ? [{ type: 'condition', if: 'target.hostile', then: [{ type: 'damage', amount: `${e.damage.base} + level * ${e.damage.perLevel}`, threads: [T.THREADS[e.damage.type]], tags: ['proc'] }] }] : []), ...(e.healAllies ? [{ type: 'condition', if: '!target.hostile', then: [{ type: 'heal', amount: `${e.healAllies.base} + level * ${e.healAllies.perLevel}` }] }] : [])] }] };
    case 'fortify': ctx.blocked.push('endurance (a pool this port lacks)'); return null;
    case 'cooldown': return { type: 'cooldown', seconds: e.seconds, fraction: e.reset ? 1 : e.fraction, scope: e.tags ? tagsOf(e.tags) : e.exceptSelf ? 'others' : 'all' };
    case 'removeBuff': return { type: 'remove_status', status: [].concat(e.buff)[0], self: true };
    case 'ward': return { type: 'resource', resource: 'ward', amount: [e.flat, e.pctMaxLife ? `life.max * ${e.pctMaxLife}` : null].filter(v => v != null).join(' + ') || '0', self: true };
    case 'cleanse': return e.statuses ? { type: 'remove_status', statuses: e.statuses.map(s => T.status(s) ?? s), self: true } : { type: 'remove_status', kind: e.hardCC ? 'control' : 'harmful', count: 99, self: true };
    case 'kindle': case 'vent': ctx.blocked.push(`${e.type} (row C36, later)`); return null;
    case 'cast': return { type: 'cast', weave: e.cast.skillId, at: e.cast.at === 'self' ? 'caster' : 'target' };
    default: ctx.blocked.push(`proc effect ${e.type}`); return null;
  }
}
for (const p of Object.values(S.procs)) {
  const ctx = { notes: [], blocked: [], seams: [] };
  const on = T.PROC_TRIGGERS[p.trigger];
  const e = { id: p.id, name: p.name, description: p.description, color: p.color };
  if (on === null || on === undefined) { if (['orbPickup', 'surface', 'esRechargeStart', 'esFilled', 'poiseBracket', 'poiseRearmed'].includes(p.trigger)) ctx.blocked.push(`trigger ${p.trigger}: a fabric or pool event this port lacks`); else (ctx.seams ??= []).push(`trigger kind ${p.trigger} (a trigger kind to register)`); }
  e.on = on ?? p.trigger;
  if (p.crit && on === 'hit') e.on = 'crit';
  const tags = tagsOf(p.tags);
  if (p.hitType) tags.push(T.THREADS[p.hitType]);
  if (tags.length) e.tags = tags;
  const when = [];
  if (p.when) { const c = conditionOf(p.when); if (c) when.push(c); else ctx.notes.push(`condition ${p.when} dropped`); }
  if (p.unless) { const c = conditionOf(p.unless); if (c) when.push(`!(${c})`); else ctx.notes.push(`condition ${p.unless} dropped`); }
  for (const b of [].concat(p.requireBuff ?? [], p.requireStatus ?? [])) when.push(`has("${T.status(b) ?? b}")`);
  if (p.rollTop) when.push(`roll >= ${+(1 - p.rollTop).toFixed(2)}`);
  if (when.length) e.when = when.join(' && ');
  if (p.vs) e.victim = p.vs.map(v => `has("${T.victim(v)}")`).join(' && ');
  if (p.noCrit) e.no_crit = true;
  if (p.icd) e.icd = p.icd;
  if (p.ppm) e.ppm = p.ppm;
  if (p.every) e.icd = p.every;
  if (p.oncePerCast) ctx.notes.push('once per cast: an icd of a tick by hand');
  if (p.skills) ctx.notes.push(`skill gate (${p.skills.join(', ')}): tags of the named weaves by hand`);
  if (p.minionCarry) ctx.notes.push('minion carry: minion hits raise the minion\'s own triggers here');
  if (p.receivedTypes) ctx.notes.push('received-type gate dropped');
  for (const f of ['status', 'charge', 'buff', 'orb', 'bracket', 'condition']) if (p[f] !== undefined && f !== 'condition') ctx.notes.push(`${f} filter: the proc reads the event's ${f} through a seam (ProcDef.status) or by hand`);
  const fx = procEffect(p, p.effect, ctx);
  e.effects = [fx].filter(Boolean);
  if (['heal', 'restore', 'buff', 'gainCharge', 'ward', 'cleanse', 'fortify', 'cooldown', 'removeBuff', 'burst'].includes(p.effect.type)) e.target = 'owner';
  e.__comment = [`${p.id}: ${p.trigger} -> ${p.effect.type}`, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : []), ...(ctx.blocked.length ? ['BLOCKED: ' + ctx.blocked.join('; ')] : [])];
  procs.push(e);
  ledgerProcs.push({ id: p.id, name: p.name, ours: p.id, mechanism: `proc on ${e.on}: ${p.effect.type}`, status: statusOf(ctx), note: noteOf(ctx), proof: 'proc duel' });
}

// ---------------------------------------------------------------- charges

const charges = [];
const chargeAttributes = [];
const ledgerCharges = [];
for (const [id, c] of Object.entries(S.charges)) {
  const ctx = { notes: [], dropped: [] };
  const e = { id, name: c.label, color: c.color, duration: c.decay?.delay ?? 10, decay: c.decay?.perSec ?? 0 };
  if (c.baseCap && c.baseCap !== 3) chargeAttributes.push({ id: `charge.${id}.max`, name: `Maximum ${c.label}`, category: 'resource', base: c.baseCap, min: 0, decimals: 0, hidden: true });
  const per = modsOf(c.perCharge, ctx);
  if (per.length) { e.__per_charge = per.map(m => ({ ...m, gauge: `charge.${id}` })); ctx.notes.push('per-charge modifiers are granted by whoever uses the charge, with gauge charge.<id>'); }
  if (c.modsWhileDraining) ctx.notes.push('mods only while draining: a drain flag by hand');
  if (c.regen) ctx.notes.push(`regeneration of ${c.regen} per 10s: no accrual clock on a charge (seam or an interval proc)`);
  if (c.hud) e.meta = { hud: c.hud };
  if (id.startsWith('flask_')) ctx.notes.push('flask founts: the flask family is not ported');
  e.__comment = [id, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : [])];
  charges.push(e);
  ledgerCharges.push({ id, name: c.label, ours: id, mechanism: 'charge kind', status: id.startsWith('flask_') ? 'not ported' : ctx.notes.length ? 'ported (difference)' : 'ported', note: ctx.notes.join('; '), proof: 'charge duel' });
}

// ---------------------------------------------------------------- passives

const notables = [];
const ledgerPassives = [];
const passiveFamily = id => id.replace(/^spec_(fire|cold|lightning|physical|chaos|melee|spell|projectile|minion|aoe|summon|attack|bow|dot|curse)_/, 'spec_*_').replace(/_(fire|cold|lightning|physical|chaos)_/g, '_*_').replace(/_(fire|cold|lightning|physical|chaos)$/, '_*');
for (const n of Object.values(S.passiveNodes)) {
  if (!['notable', 'keystone', 'choice'].includes(n.kind)) continue;
  const ctx = { notes: [], blocked: [], dropped: [] };
  const e = { id: n.id, name: n.name, description: n.description, kind: n.kind === 'choice' ? 'notable' : n.kind, realm: n.realm, links: [] };
  const mods = modsOf(n.mods, ctx);
  if (n.attributes) for (const [a, v] of Object.entries(n.attributes)) mods.push({ attribute: T.CORE_ATTRIBUTES[a] ?? a, form: 'flat', value: v });
  if (n.attributesPct) for (const [a, v] of Object.entries(n.attributesPct)) mods.push({ attribute: T.CORE_ATTRIBUTES[a] ?? a, form: 'increased', value: v });
  if (mods.length) e.modifiers = mods;
  if (n.choice) e.choice = { group: n.choice.group, pick: n.choice.pick, default: n.choice.allocatedDefault };
  if (n.graft) e.glyph = { id: n.graft.support, level: n.graft.level ?? 1 };
  if (n.conduit) ctx.blocked.push('conduits (row H7, open)');
  if (n.vocation) e.realm = n.vocation;
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  e.__comment = [`${n.id} (${passiveFamily(n.id)})`, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : []), ...(ctx.blocked.length ? ['BLOCKED: ' + ctx.blocked.join('; ')] : [])];
  notables.push(e);
  ledgerPassives.push({ id: n.id, name: n.name, family: passiveFamily(n.id), kind: n.kind, ours: '', mechanism: [e.choice && 'choice', e.glyph && 'graft', e.modifiers?.some(m => m.attribute.startsWith('proc.')) && 'proc', e.modifiers && 'modifiers'].filter(Boolean).join(', ') || 'node', status: ctx.blocked.length ? 'blocked' : 'to author', note: [...ctx.notes, ...ctx.blocked.map(b => 'seam: ' + b)].join('; '), proof: 'passives lint' });
}

// ---------------------------------------------------------------- items

const SLOT_CLASS = { helmet: ['armor.head', 'head'], chest: ['armor.chest', 'chest'], gloves: ['armor.hands', 'hands'], boots: ['armor.feet', 'feet'], legs: ['armor.legs', 'legs'], belt: ['armor.belt', 'belt'], ring: ['jewelry.ring', 'ring'], amulet: ['jewelry.amulet', 'amulet'], relic: ['relic', 'relic'] };
const DEFENSE = { armor: 'armor', evasion: 'evasion', energyShield: 'shield', poise: 'poise', insight: 'insight', endurance: null };
const ITEM_TAGS = { helmet: 'armor.head', chest: 'armor.chest', gloves: 'armor.hands', boots: 'armor.feet', legs: 'armor.legs', belt: 'armor.belt', ring: 'jewelry.ring', amulet: 'jewelry.amulet', armour: 'armor', jewelry: 'jewelry', relic: 'relic', gem: 'glyph' };
const itemTag = t => ITEM_TAGS[t] ?? t;
function lineOf(l, ctx) {
  const m = modOf({ stat: l.stat, kind: l.kind, value: 1, tags: l.tags, when: l.when, fromStat: l.fromStat, gauge: l.gauge, gaugeAt: l.gaugeAt }, ctx);
  if (!m) return null;
  delete m.value;
  if (l.local) m.local = true;
  if (l.sharedRoll) m.shared_roll = 'shared';
  if (l.range) m.range = l.range.map(v => r2(v * (T.STATS[l.stat]?.scale ?? 1)));
  if (l.tierScale != null) m.tier_scale = l.tierScale;
  if (l.text) m.text = l.text;
  return m;
}
const items = [];
const ledgerItems = [];
for (const b of Object.values(S.itemBases)) {
  const ctx = { notes: [], blocked: [], dropped: [] };
  if (b.category === 'gem') { ledgerItems.push({ id: b.id, name: b.name, ours: b.id === 'skill_gem' ? 'weave_codex' : b.id === 'support_gem' ? 'glyph_stone' : '', mechanism: 'weave items', status: ['skill_gem', 'support_gem'].includes(b.id) ? 'ported (difference)' : 'not ported', note: ['skill_gem', 'support_gem'].includes(b.id) ? 'codices and stones carry the weave item model' : 'memories, writs and quest tokens belong to other fabrics (row I9)', proof: 'item tests' }); continue; }
  if (b.category === 'legs') ctx.notes.push('no legs slot here: dropped');
  const [cls, slot] = SLOT_CLASS[b.category] ?? ['relic', 'relic'];
  const e = { id: b.id, name: b.name, class: b.category === 'relic' ? `relic.${b.id.replace('relic_', '')}` : cls, slot: b.category === 'relic' ? undefined : slot, width: b.w, height: b.h, tags: b.tags.map(itemTag), drop_weight: b.dropWeight, min_level: b.minIlvl };
  if (b.defense) { e.defense = {}; for (const [k, v] of Object.entries(b.defense)) { const d = DEFENSE[k]; if (d === null) ctx.notes.push('endurance is not a pool here: the share is dropped'); else if (d) e.defense[d] = v; } }
  if (b.implicits) e.implicits = b.implicits.map(l => lineOf(l, ctx)).filter(Boolean);
  if (b.affinity) e.affinity = b.affinity;
  if (b.affixCap) e.affix_cap = b.affixCap;
  if (b.affixPool === 'explicit') ctx.notes.push('explicit affix pool: list the families by hand');
  if (b.minRarity) e.min_rarity = b.minRarity;
  if (b.namesByTier) e.names_by_tier = b.namesByTier;
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  e.__comment = [b.id, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : [])];
  items.push(e);
  ledgerItems.push({ id: b.id, name: b.name, ours: b.category === 'legs' ? '' : b.id, mechanism: `base: ${b.category}${b.defense ? ' ' + Object.keys(b.defense).join('+') : ''}`, status: b.category === 'legs' ? 'not ported' : ctx.notes.length ? 'ported (difference)' : 'ported', note: ctx.notes.join('; '), proof: 'item roll test' });
}

const affixFamilies = new Map();
for (const a of Object.values(S.affixes)) {
  const famId = a.id.replace(/^slotgraft_\d+_/, 'graft_');
  if (!affixFamilies.has(famId)) affixFamilies.set(famId, { a, seats: 0 });
  affixFamilies.get(famId).seats++;
}
const affixes = [];
const ledgerAffixes = [];
for (const [famId, { a, seats }] of affixFamilies) {
  const ctx = { notes: [], blocked: [], dropped: [] };
  const e = { id: famId, name: a.names?.[a.names.length - 1] ?? title(famId), kind: a.kind, group: famId, names: a.names, weight: a.weight };
  if (a.tags) e.requires = a.tags.map(itemTag);
  if (a.excludeTags) e.excludes = a.excludeTags.map(itemTag);
  if (a.themes) e.themes = a.themes;
  e.lines = a.lines.map(l => lineOf(l, ctx)).filter(Boolean);
  if (seats > 1) { e.lines = e.lines.map(l => ({ ...l, attribute: l.attribute.replace(/slot\d+/, 'slot1') })); ctx.notes.push(`${seats} per-seat affixes fold into one on seat 1`); }
  e.tiers = a.tiers.map(t => ({ level: t.ilvl, ranges: t.ranges.map(r => r.map(v => r2(v))), weight: t.weight, magic_only: t.magicOnly || undefined }));
  if (!e.lines.length) ctx.blocked.push('no line maps');
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  e.__comment = [famId, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : []), ...(ctx.blocked.length ? ['BLOCKED: ' + ctx.blocked.join('; ')] : [])];
  affixes.push(e);
  ledgerAffixes.push({ id: famId, name: e.name, ours: ctx.blocked.length ? '' : famId, mechanism: `affix ${a.kind}: ${a.lines.map(l => l.stat).join(', ')}`, status: ctx.blocked.length ? 'blocked' : ctx.notes.length ? 'ported (difference)' : 'ported', note: [...ctx.notes, ...ctx.blocked.map(b => 'seam: ' + b)].join('; '), proof: 'affix roll test' });
}

const uniques = [];
const ledgerUniques = [];
for (const uq of Object.values(S.uniques)) {
  const ctx = { notes: [], blocked: [], dropped: [] };
  const e = { id: uq.id, name: uq.name, base: uq.baseId, weight: uq.weight, min_level: uq.minIlvl, flavor: uq.flavor, max_per_container: uq.maxPerContainer };
  e.lines = uq.lines.map(l => lineOf(l, ctx)).filter(Boolean);
  if (uq.choices) { e.choices = uq.choices.map(c => ({ id: c.id, pick: c.pick, options: (c.options ?? []).slice(0, 8).map(o => ({ id: o.id, name: o.name, weight: o.weight, lines: (o.lines ?? []).map(l => lineOf(l, ctx)).filter(Boolean) })), __more: (c.options ?? []).length > 8 ? `${(c.options ?? []).length} options in the prototype` : undefined })); for (const c of uq.choices) if ((c.options ?? []).length > 8) ctx.notes.push(`choice ${c.id} draws from ${c.options.length} options (one per graft): a generated option set by hand`); }
  if (ctx.dropped.length) ctx.notes.push(`dropped: ${ctx.dropped.join(', ')}`);
  e.__comment = [uq.id, ...(ctx.notes.length ? ['notes: ' + ctx.notes.join('; ')] : [])];
  uniques.push(e);
  ledgerUniques.push({ id: uq.id, name: uq.name, ours: uq.id, mechanism: `unique on ${uq.baseId}: ${uq.lines.map(l => l.stat).join(', ')}`, status: ctx.dropped.length ? 'ported (difference)' : 'ported', note: ctx.notes.join('; '), proof: 'unique roll test' });
}

// ---------------------------------------------------------------- write the drafts

const shellDefs = [...shells.values()].map(s => ({ ...s.def, __comment: [`${s.base} shell used by ${s.users.length}: ${s.users.slice(0, 12).join(', ')}${s.users.length > 12 ? ', ...' : ''}`] }));
write('forms-port.yaml', yamlFile('forms', '# Draft: shells of the port. Each preset supplies its graph; timing, cost and cooldown read the preset attributes.', shellDefs));
for (const [family, list] of Object.entries(presets)) write(`weaves-${family}.yaml`, yamlFile('weaves', `# Draft: ${FAMILY_NAMES[family]}, ported from the prototype (${list.length} presets).`, list));
write('glyphs-port.yaml', yamlFile('glyphs', '# Draft: supports as glyphs.', glyphs));
write('statuses-port.yaml', yamlFile('statuses', '# Draft: statuses, plus the buffs skills and procs grant.', [...statuses, ...generatedStatuses.values()]));
write('procs-port.yaml', yamlFile('procs', '# Draft: procs.', procs));
write('charges-port.yaml', yamlFile('charges', '# Draft: charges.', charges));
write('attributes-port.yaml', yamlFile('attributes', '# Draft: charge caps and the shell attributes.', [
  { id: 'cast.time', name: 'Cast Time', category: 'weave', base: 0, min: 0, decimals: 2, hidden: true, description: 'Seconds a port shell takes to cast, read by its cast_time expression.' },
  { id: 'cooldown.base', name: 'Base Cooldown', category: 'weave', base: 0, min: 0, decimals: 2, hidden: true, description: 'Seconds of cooldown a port shell reads from its preset.' },
  { id: 'channel.interval', name: 'Channel Beat', category: 'weave', base: 0.5, min: 0.05, decimals: 2, hidden: true },
  { id: 'channel.windup', name: 'Channel Windup', category: 'weave', base: 0, min: 0, decimals: 2, hidden: true },
  { id: 'channel.move', name: 'Channel Mobility', category: 'weave', base: 0.5, min: 0, max: 1, hidden: true },
  { id: 'channel.turn_rate', name: 'Channel Turn Rate', category: 'weave', base: 180, min: 0, hidden: true },
  { id: 'channel.max_hold', name: 'Channel Hold', category: 'weave', base: 6, min: 0, hidden: true },
  { id: 'guard.pool', name: 'Guard Pool', category: 'weave', base: 40, min: 0, hidden: true },
  { id: 'guard.move', name: 'Guard Mobility', category: 'weave', base: 0.3, min: 0, max: 1, hidden: true },
  { id: 'guard.turn_rate', name: 'Guard Turn Rate', category: 'weave', base: 180, min: 0, hidden: true },
  { id: 'guard.parry', name: 'Parry Window', category: 'weave', base: 0.2, min: 0, hidden: true },
  { id: 'guard.max_duration', name: 'Guard Hold', category: 'weave', base: 3, min: 0, hidden: true },
  { id: 'charge.max_time', name: 'Draw Time', category: 'weave', base: 1.2, min: 0.1, hidden: true },
  { id: 'reserve.mana', name: 'Mana Reserved', category: 'weave', base: 0, min: 0, hidden: true },
  { id: 'upkeep.mana', name: 'Mana Upkeep', category: 'weave', base: 0, min: 0, hidden: true },
  { id: 'upkeep.life', name: 'Life Upkeep', category: 'weave', base: 0, min: 0, hidden: true },
  ...chargeAttributes,
]));
write('patterns-port.yaml', yamlFile('patterns', '# Draft: device patterns for ported constructs.', (report.constructPatterns ?? []).map(p => ({ id: p.id, name: title(p.id), extends: p.extends, tags: ['construct', p.kind], radius: p.radius, components: { stats: p.life ? { attributes: { 'life.max': p.life } } : undefined, weaves: p.weaves.length ? { list: p.weaves } : undefined }, __comment: [`${p.kind} of ${p.from}${p.invulnerable ? ', invulnerable' : ''}${p.interval ? `, every ${p.interval}s` : ''}${p.taunt ? ', taunts' : ''}${p.follows ? ', follows' : ''}${p.castOnStruck ? ', casts when struck' : ''}`] }))));
write('passives-port.yaml', yamlFile('passives', '# Draft: the prototype notables, keystones and choice nodes with their modifiers mapped; the constellation is authored in our themes.', notables));
write('items-port.yaml', yamlFile('items', '# Draft: item bases.', items));
write('affixes-port.yaml', yamlFile('affixes', '# Draft: affix families (per-seat grafts folded).', affixes));
write('uniques-port.yaml', yamlFile('uniques', '# Draft: uniques.', uniques));
write('rules-port.yaml', yamlFile('rules', '# Draft: ailment magnitudes of ported statuses.', [{ id: 'combat', patch: true, values: { ailment_magnitude: magnitudeRules } }]));

// ---------------------------------------------------------------- the ledger

const CODES = [
  [/requirements: on the codex by hand \([^)]*\)/g, 'req'],
  [/leveling table \(named difference: growth is the formula\)/g, 'grow'],
  [/per-skill trees \(trees belong to forms here\)/g, 'tree'],
  [/level thresholds \(named difference: growth is the formula\)/g, 'thr'],
  [/innate modifiers \(preset modifiers\)/g, 'inn'],
  [/a monster kit skill \(noDrop\); its monster is not in this port and the bestiary port brings its kit/g, 'kit'],
  [/8 per-seat affixes fold into one on seat 1/g, 'seat1'],
  [/summons our (\w+) for (\w+)/g, 'sum:$1<$2'],
  [/a world or fabric state \(water, clouds, vents, the fourth wall, time\) outside this port/g, 'fabric'],
  [/super-art cut-ins \(question Q10\): the price floor stays as a cooldown/g, 'ult'],
  [/passive charge taps \(a proc by hand\)/g, 'taps'],
  [/dropped: modifier /g, 'drop:'],
  [/dropped: /g, 'drop:'],
  [/already in the roster as (\w+)/g, 'roster:$1'],
  [/a third damage type \(([^)]*)\) is folded into the first two/g, 'third:$1'],
  [/pool-scaled costs: cost.pct_max attributes/g, 'pct-cost'],
];
const shorten = note => CODES.reduce((n, [re, to]) => n.replace(re, to), String(note ?? ''));
const cell = s => shorten(s).replace(/\|/g, '\\|').replace(/\n/g, ' ');
const table = (rows, cols) => ['| ' + cols.map(c => c[0]).join(' | ') + ' |', '|' + cols.map(() => '---').join('|') + '|', ...rows.map(r => '| ' + cols.map(c => cell(r[c[1]])).join(' | ') + ' |')].join('\n');
const tally = rows => { const m = {}; for (const r of rows) bump(m, r.status); return Object.entries(m).map(([k, v]) => `${k} ${v}`).join(', '); };
const L = [];
L.push(`<!-- Generated from the prototype snapshot of ${S.generated} by scripts/port/port-prototype.mjs; the statuses below are kept by hand as the slices land. -->`, '');
L.push('## Skills', '');
for (const family of Object.keys(FAMILY_NAMES)) {
  const rows = ledgerSkills.filter(r => r.family === family);
  L.push(`### ${FAMILY_NAMES[family]} (${rows.length}: ${tally(rows)})`, '', table(rows, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
}
L.push(`## Supports (${ledgerSupports.length}: ${tally(ledgerSupports)})`, '', table(ledgerSupports, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Statuses (${ledgerStatuses.length}: ${tally(ledgerStatuses)})`, '', table(ledgerStatuses, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Procs (${ledgerProcs.length}: ${tally(ledgerProcs)})`, '', table(ledgerProcs, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Charges (${ledgerCharges.length}: ${tally(ledgerCharges)})`, '', table(ledgerCharges, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Passive notables, keystones and choice nodes (${ledgerPassives.length}: ${tally(ledgerPassives)})`, '', table(ledgerPassives, [['Prototype', 'id'], ['Family', 'family'], ['Kind', 'kind'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Item bases (${ledgerItems.length}: ${tally(ledgerItems)})`, '', table(ledgerItems, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Affix families (${ledgerAffixes.length}: ${tally(ledgerAffixes)})`, '', table(ledgerAffixes, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
L.push(`## Uniques (${ledgerUniques.length}: ${tally(ledgerUniques)})`, '', table(ledgerUniques, [['Prototype', 'id'], ['Ours', 'ours'], ['Mechanism', 'mechanism'], ['Status', 'status'], ['Note', 'note'], ['Proof', 'proof']]), '');
write('port-ledger.md', L.join('\n') + '\n');

// ---------------------------------------------------------------- the report

for (const [sig, s] of shells) report.shells[s.def.id] = { users: s.users.length, base: s.base, cast_mode: s.def.cast_mode, targeting: s.def.targeting, tags: s.def.tags, extra: Object.keys(s.def).filter(k => ['channel', 'guard', 'charge', 'timing', 'target', 'uses', 'convert', 'meta_action', 'gates', 'reflex', 'toggle', 'reserve', 'upkeep', 'follow_up', 'cycle', 'self_stack', 'chain', 'equip_modifiers', 'requirements', 'cast_move', 'cooldown_at', 'spend', 'overcharge'].includes(k)) };
report.counts = {
  skills: { total: Object.keys(skills).length, inPort: inPort.size, byStatus: Object.fromEntries(Object.entries(ledgerSkills.reduce((m, r) => (bump(m, r.status), m), {}))) },
  supports: Object.fromEntries(Object.entries(ledgerSupports.reduce((m, r) => (bump(m, r.status), m), {}))),
  statuses: Object.fromEntries(Object.entries(ledgerStatuses.reduce((m, r) => (bump(m, r.status), m), {}))),
  procs: Object.fromEntries(Object.entries(ledgerProcs.reduce((m, r) => (bump(m, r.status), m), {}))),
  passives: ledgerPassives.length, items: ledgerItems.length, affixFamilies: ledgerAffixes.length, uniques: ledgerUniques.length,
  shells: shells.size, generatedStatuses: generatedStatuses.size,
};
report.unmappedStats = Object.fromEntries(Object.entries(report.unmappedStats).sort((a, b) => b[1] - a[1]));
write('report.json', JSON.stringify(report, null, 1));
console.log(JSON.stringify(report.counts, null, 1));
console.log('unmapped stats:', Object.keys(report.unmappedStats).length, 'blocked effects:', JSON.stringify(report.blockedEffects));
