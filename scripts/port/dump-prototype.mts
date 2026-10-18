// Reads the prototype's combat catalogs (arianna-arpg/arpg-game-world) and writes them as one JSON snapshot.
//
//   npx --yes tsx@4 scripts/port/dump-prototype.mts <clone-folder> <out.json>
//
// The clone is a shallow checkout of the prototype outside this repository. The snapshot is the input of
// port-prototype.mjs, which maps it onto Hollow Wake's vocabulary; nothing here runs at game time.
// Functions are dropped, Sets and Maps become arrays and objects, and every catalog is keyed by id.

import { pathToFileURL } from 'node:url';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const [cloneArg, outArg] = process.argv.slice(2);
if (!cloneArg || !outArg) {
  console.error('usage: tsx scripts/port/dump-prototype.mts <clone-folder> <out.json>');
  process.exit(2);
}
const clone = resolve(cloneArg);
const load = async (rel: string) => import(pathToFileURL(resolve(clone, rel)).href);

const skills = await load('src/data/skills.ts');
const supports = await load('src/data/supports.ts');
const status = await load('src/engine/status.ts');
const procs = await load('src/data/procs.ts');
const charges = await load('src/engine/charges.ts');
const passives = await load('src/data/passives.ts');
const choices = await load('src/data/passiveChoices.ts');
const realms = await load('src/data/passiveRealms.ts');
const vocations = await load('src/data/vocations.ts');
const classes = await load('src/data/classes.ts');
const bases = await load('src/data/itembases.ts');
const affixes = await load('src/data/itemaffixes.ts');
const uniques = await load('src/data/uniques.ts');
const stats = await load('src/engine/stats.ts');
const skillsEngine = await load('src/engine/skills.ts');
const items = await load('src/engine/items.ts');

function plain(value: unknown): unknown {
  return JSON.parse(JSON.stringify(value, (_key, v) => {
    if (typeof v === 'function') return undefined;
    if (v instanceof Set) return [...v];
    if (v instanceof Map) return Object.fromEntries(v);
    if (typeof v === 'bigint') return Number(v);
    return v;
  }));
}

const snapshot = {
  generated: new Date().toISOString().slice(0, 10),
  skills: plain(skills.SKILLS),
  supports: plain(supports.SUPPORTS),
  statuses: plain(status.STATUS_DEFS),
  procs: plain(procs.PROCS),
  charges: plain(charges.CHARGE_DEFS),
  passiveNodes: plain(passives.PASSIVE_NODES),
  choiceGroups: plain(choices.CHOICE_GROUPS),
  realms: plain(realms.PASSIVE_REALMS),
  vocations: plain(vocations.VOCATIONS),
  classes: plain(classes.CLASSES),
  itemBases: plain(bases.ITEM_BASES),
  affixes: plain(affixes.ITEM_AFFIXES),
  uniques: plain(uniques.UNIQUES),
  statDefs: plain(stats.STAT_DEFS),
  conditionIds: plain(stats.CONDITION_IDS),
  mechanisms: Object.keys(skillsEngine.SUPPORT_MECHANISMS ?? {}),
  aoeShapes: plain(skillsEngine.AOE_SHAPE),
  projReturn: plain(skillsEngine.PROJ_RETURN),
  triggerCfg: plain(skillsEngine.TRIGGER_CFG),
  categorySize: plain(bases.CATEGORY_SIZE),
  equipSlots: plain(items.EQUIP_SLOTS),
  itemRarities: plain(items.ITEM_RARITIES),
};

writeFileSync(outArg, JSON.stringify(snapshot, null, 1));
const count = (o: unknown) => Object.keys((o as object) ?? {}).length;
console.log(`skills ${count(snapshot.skills)}, supports ${count(snapshot.supports)}, statuses ${count(snapshot.statuses)}, procs ${count(snapshot.procs)}, charges ${count(snapshot.charges)}, passive nodes ${count(snapshot.passiveNodes)}, choice groups ${count(snapshot.choiceGroups)}, realms ${count(snapshot.realms)}, vocations ${count(snapshot.vocations)}, classes ${(snapshot.classes as unknown[]).length}, item bases ${count(snapshot.itemBases)}, affixes ${count(snapshot.affixes)}, uniques ${count(snapshot.uniques)}`);
