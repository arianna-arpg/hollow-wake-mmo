// Import authored vector art, never prototype gameplay, from a pinned Git revision.
// node scripts/import-ability-icons.mjs <prototype checkout> [revision]
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const [checkout, revision = 'codex/seamless-world-foundation'] = process.argv.slice(2);
if (!checkout) throw new Error('usage: node scripts/import-ability-icons.mjs <prototype checkout> [revision]');
const repo = resolve(checkout);
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${repo.replaceAll('\\', '/')}`, '-C', repo, ...args], { maxBuffer: 64 * 1024 * 1024 });
const commit = git('rev-parse', revision).toString().trim();
const lock = JSON.parse(git('show', commit + ':package-lock.json').toString());
const expectedTsx = lock.packages['node_modules/tsx'].version;
const installedTsx = JSON.parse(readFileSync(join(repo, 'node_modules/tsx/package.json'), 'utf8')).version;
const expectedEsbuild = lock.packages['node_modules/esbuild'].version;
const installedEsbuild = JSON.parse(readFileSync(join(repo, 'node_modules/esbuild/package.json'), 'utf8')).version;
if (installedTsx !== expectedTsx || installedEsbuild !== expectedEsbuild) throw new Error('Install the pinned prototype lockfile with npm ci before importing.');
const scratch = mkdtempSync(join(tmpdir(), 'hollow-wake-icons-'));
try {
  writeFileSync(join(scratch, 'source.tar'), git('archive', commit, 'src'));
  execFileSync('tar', ['-xf', join(scratch, 'source.tar'), '-C', scratch]);
  const extract = `import { SKILL_ICONS, SKILL_ICON_VIEW, skillIconKey } from './src/render/skillIcons';
import { SKILLS } from './src/data/skills';
import { SUPPORTS } from './src/data/supports';
import { SUPPORT_BADGE } from './src/render/itemIcons';
import { writeFileSync } from 'node:fs';
const entries = Object.entries(SKILL_ICONS).sort(([a],[b])=>a < b ? -1 : a > b ? 1 : 0).map(([id, art])=>({id, layers:art.layers}));
for(const id of Object.keys(SKILLS)) if(!SKILL_ICONS['skill:'+id]) throw new Error('Missing authored skill illustration: '+id);
for(const id of Object.keys(SKILL_ICONS).filter(id=>id.startsWith('skill:'))) if(!SKILLS[id.slice(6)]) throw new Error('Stale skill illustration: '+id);
for(const [id, skill] of Object.entries(SKILLS)) entries.push({id:'weaves.'+id, art:skillIconKey(skill), color:skill.color});
entries.push({id:'badge.support',layers:[{path:'M12 2L15 9L23 9L17 14L19 22L12 17L5 22L7 14L1 9L9 9Z',fill:'ink'}]});
for(const [id, support] of Object.entries(SUPPORTS)) entries.push({id:'glyphs.'+id, art:'support', color:support.color, badge:'badge.support'});
writeFileSync('icons.json', JSON.stringify({entries, view:SKILL_ICON_VIEW, badge:SUPPORT_BADGE, skills:Object.keys(SKILLS).length, supports:Object.keys(SUPPORTS).length}));`;
  writeFileSync(join(scratch, 'extract.mts'), extract);
  execFileSync(process.execPath, [join(repo, 'node_modules/tsx/dist/cli.mjs'), join(scratch, 'extract.mts')], { cwd: scratch, stdio: 'inherit' });
  const { entries, skills, supports } = JSON.parse(readFileSync(join(scratch, 'icons.json'), 'utf8'));
  const namespaced = entries.map(e => ({...e, id:'prototype/'+e.id, ...(e.art ? {art:'prototype/'+e.art} : {}), ...(e.badge ? {badge:'prototype/'+e.badge} : {})}));
  const source = new Map(namespaced.map(e => [e.id,e]));
  const bindings = JSON.parse(readFileSync(join(root,'content/core/ui/ability-bindings.json'),'utf8')).entries;
  const selected = new Set();
  function visit(id) {
    if (!id || !id.startsWith('prototype/') || selected.has(id)) return;
    const entry = source.get(id);
    if (!entry) throw new Error('Authored binding references missing prototype art: '+id);
    selected.add(id); visit(entry.art); visit(entry.badge);
  }
  for (const entry of bindings) { visit(entry.art); visit(entry.badge); }
  const shipped = namespaced.filter(e => selected.has(e.id));
  // JSON is content. Keep each vector entry on one line so regeneration diffs remain reviewable.
  writeFileSync(join(root, 'content/core/ui/ability-icons.json'), '{"kind":"ui_ability_icons","entries":[\n' + shipped.map(e => JSON.stringify(e)).join(',\n') + '\n]}\n');
  writeFileSync(join(root, 'assets/branding/ability-icons-provenance.json'), JSON.stringify({
    source:'https://github.com/arianna-arpg/arpg-game-world', commit, skills, supports, shipped_entries: shipped.length, toolchain: {tsx:expectedTsx, esbuild:expectedEsbuild, node:process.version},
    files:['src/render/skillIconArt.ts','src/render/skillIconCatalog.ts','src/render/skillIcons.ts','src/render/itemIcons.ts','src/data/skills.ts','src/data/supports.ts'],
    regeneration:`node scripts/import-ability-icons.mjs <prototype checkout> ${commit}`
  }, null, 2)+'\n');
  console.log(`Imported ${shipped.length} reachable entries from ${skills} skill faces and ${supports} support faces from ${commit}.`);
} finally {
  const rel = relative(tmpdir(), scratch);
  if (!rel.startsWith('..') && rel.startsWith('hollow-wake-icons-')) rmSync(scratch, {recursive:true, force:true});
}
