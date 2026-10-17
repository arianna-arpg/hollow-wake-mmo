import { readFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const read = name => JSON.parse(readFileSync(resolve(here, name), 'utf8'));
export const loadAudit = () => {
  const census = read('evidence/prototype-census.json');
  return { census, coverage: read('coverage.json'),
    skills: census.skills_files.flatMap(file => read(`evidence/${file}`)), supports: read(`evidence/${census.supports_file}`) };
};

export function audit({ census, coverage, skills, supports }) {
  const errors = [];
  const unique = values => [...new Set(values)].sort();
  const observed = { ...census.vocabulary,
    skill_fields: unique(skills.flatMap(s => s.fields)), support_fields: unique(supports.flatMap(s => s.fields)),
    variant_paths: unique([...skills, ...supports].flatMap(s => s.variants.map(v => v.path.replace(/\.\d+/g, '.#')))) };
  const catalogIds = (rows, name) => {
    if (new Set(rows.map(s => s.id)).size !== rows.length) errors.push(`${name}: duplicate catalog ID`);
    if (rows.length !== census.counts[name]) errors.push(`${name}: census count mismatch`);
  };
  catalogIds(skills, 'skills'); catalogIds(supports, 'supports');
  for (const [name, values] of Object.entries(observed)) {
    const assigned = new Map();
    for (const [id, family] of Object.entries(coverage.families)) {
      if (!['metadata', 'no-decoration', 'shared-cues', 'partial', 'pending'].includes(family.status) || !family.coverage?.trim()) errors.push(`${id}: missing coverage decision`);
      for (const value of family[name] ?? []) {
        if (assigned.has(value)) errors.push(`${name}.${value}: duplicate family`);
        assigned.set(value, id);
      }
    }
    for (const value of values) if (!assigned.has(value)) errors.push(`${name}.${value}: UNCLASSIFIED; choose a shared family and document missing work`);
    for (const value of assigned.keys()) if (!values.includes(value)) errors.push(`${name}.${value}: stale classification`);
  }
  for (const skill of skills) {
    if (skill.delivery && !census.vocabulary.deliveries.includes(skill.delivery)) errors.push(`${skill.id}: unknown delivery ${skill.delivery}`);
    if (!census.vocabulary.cast_modes.includes(skill.mode)) errors.push(`${skill.id}: unknown cast mode ${skill.mode}`);
    for (const effect of skill.effects) if (!census.vocabulary.effects.includes(effect)) errors.push(`${skill.id}: unknown effect ${effect}`);
  }
  return { errors, skills: skills.length, supports: supports.length, families: Object.keys(coverage.families).length,
    pending: Object.entries(coverage.families).filter(([, f]) => f.status === 'pending').map(([id]) => id),
    partial: Object.entries(coverage.families).filter(([, f]) => f.status === 'partial').map(([id]) => id) };
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const result = audit(loadAudit()); console.log(JSON.stringify(result, null, 2));
  if (result.errors.length) process.exitCode = 1;
}
