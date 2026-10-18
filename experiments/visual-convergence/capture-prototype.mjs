// Read-only census of the original game's actual exported catalog and schema.
// Run with the prototype's tsx loader; write only to this experiment.
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

const root = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3]);
function fingerprint() {
  const hash = createHash('sha256');
  function walk(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) walk(file);
      else if (entry.name.endsWith('.ts')) hash.update(path.relative(root, file).replaceAll('\\', '/') + '\0').update(fs.readFileSync(file));
    }
  }
  walk(path.join(root, 'src')); return hash.digest('hex');
}
const before = fingerprint();
const load = relative => import(pathToFileURL(path.join(root, relative)).href);
const tsModule = await load('node_modules/typescript/lib/typescript.js');
const ts = tsModule.default ?? tsModule;
const schemaPath = 'src/engine/skills.ts';
const source = ts.createSourceFile(schemaPath, fs.readFileSync(path.join(root, schemaPath), 'utf8'), ts.ScriptTarget.Latest, true);
const declarations = new Map(source.statements.filter(s => s.name).map(s => [s.name.text, s]));
for (const statement of source.statements) {
  if (!ts.isImportDeclaration(statement) || !statement.moduleSpecifier.text.startsWith('.')) continue;
  const bindings = statement.importClause?.namedBindings;
  if (!bindings || !ts.isNamedImports(bindings)) continue;
  const file = path.resolve(root, 'src/engine', statement.moduleSpecifier.text + '.ts');
  const imported = ts.createSourceFile(file, fs.readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
  for (const binding of bindings.elements) {
    const name = (binding.propertyName ?? binding.name).text;
    const declaration = imported.statements.find(s => s.name?.text === name);
    if (declaration) declarations.set(binding.name.text, declaration);
  }
}
const literal = node => ts.isLiteralTypeNode(node) && ts.isStringLiteral(node.literal) ? node.literal.text : undefined;
function vocabulary(name) {
  const declaration = declarations.get(name);
  const result = [];
  for (const member of declaration.type.types) {
    const value = literal(member);
    if (value) result.push(value);
    else if (ts.isTypeReferenceNode(member)) {
      const def = declarations.get(member.typeName.getText(source));
      const field = def?.members?.find(m => m.name?.text === 'type');
      if (field && literal(field.type)) result.push(literal(field.type));
      else throw new Error(`Unresolved ${name} member ${member.getText(source)}`);
    } else throw new Error(`Unresolved ${name} member ${member.getText(source)}`);
  }
  return result.sort();
}
const { SKILLS } = await load('src/data/skills.ts');
const { SUPPORTS } = await load('src/data/supports.ts');
const { CASTING_CUE_STYLES } = await load('src/data/castingCues.ts');
const { COMBAT_CUE_STYLES } = await load('src/data/combatCues.ts');
const { DEFENSE_CUE_STYLES } = await load('src/data/defenseCues.ts');
if (!SKILLS || !SUPPORTS) throw new Error('Catalog export missing');
const entries = rows => Object.entries(rows).sort(([a], [b]) => a.localeCompare(b));
const references = [schemaPath, 'src/data/skills.ts', 'src/data/supports.ts', 'src/data/castingCues.ts',
  'src/data/combatCues.ts', 'src/data/defenseCues.ts', 'src/engine/castingCues.ts',
  'src/render/vis/castingCueLayer.ts', 'src/render/vis/combatCueLayer.ts', 'src/render/vis/defenseCueLayer.ts',
  'src/render/vis/effectVoice.ts', 'src/render/vis/aoeTrace.ts'];
const voices = [...fs.readFileSync(path.join(root, 'src/render/vis/effectVoice.ts'), 'utf8')
  .matchAll(/registerEffectVoice\('([^']+)'/g)].map(m => m[1]).sort();
function variants(value, at = '', found = []) {
  if (!value || typeof value !== 'object') return found;
  for (const [key, entry] of Object.entries(value)) {
    const location = at ? `${at}.${key}` : key;
    if (['delivery', 'effects', 'castingCue', 'fx', 'channel', 'guard', 'overcharge', 'tether', 'sequel', 'followUps'].includes(key)) {
      found.push({ path: location, value: entry });
    }
    if (entry && typeof entry === 'object') variants(entry, location, found);
  }
  return found;
}
const data = {
  repository: 'arianna-arpg/arpg-game-world',
  revision: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
  // Hashes are the source of provenance even when another engineer has uncommitted work.
  sources: references.map(file => ({ file, sha256: createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex') })),
  vocabulary: { deliveries: vocabulary('Delivery'), effects: vocabulary('SkillEffect'), cast_modes: vocabulary('CastMode'), effect_voices: voices },
  casting_profiles: CASTING_CUE_STYLES, combat_profiles: COMBAT_CUE_STYLES, defense_profiles: DEFENSE_CUE_STYLES,
  skills: entries(SKILLS).map(([id, s]) => ({ id, name: s.name, tags: s.tags, color: s.color,
    mode: s.castMode ?? 'cast', delivery: s.delivery?.type, effects: (s.effects ?? []).map(e => e.type),
    fields: Object.keys(s).sort(), variants: variants(s).map(v => ({ path: v.path,
      kind: v.value?.type ?? (Array.isArray(v.value) ? v.value.map(x => x?.type).filter(Boolean) : typeof v.value) })) })),
  supports: entries(SUPPORTS).map(([id, s]) => ({ id, tags: s.requiresTags ?? [], mechanisms: s.requiresMechanisms ?? [],
    fields: Object.keys(s).sort(), variants: variants(s).map(v => ({ path: v.path,
      kind: v.value?.type ?? (Array.isArray(v.value) ? v.value.map(x => x?.type).filter(Boolean) : typeof v.value) })) })),
};
data.source_tree_sha256 = fingerprint();
if (before !== data.source_tree_sha256) throw new Error('Original source changed during capture; retry before relying on the census.');
const skills = data.skills, supports = data.supports;
delete data.skills; delete data.supports;
const write = (name, value) => { fs.writeFileSync(path.join(path.dirname(output), name), '[\n' + value.map(v => JSON.stringify(v)).join(',\n') + '\n]\n'); return name; };
data.skills_files = [];
for (let i = 0; i < skills.length; i += 200) data.skills_files.push(write(`prototype-skills-${i / 200}.json`, skills.slice(i, i + 200)));
data.supports_file = write('prototype-supports.json', supports);
data.counts = { skills: skills.length, supports: supports.length };
fs.writeFileSync(output, JSON.stringify(data, null, 2) + '\n');
console.log(JSON.stringify({ ...data.counts, vocabulary: data.vocabulary, source_tree_sha256: data.source_tree_sha256 }, null, 2));
