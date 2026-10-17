// One-time, explicit import from the earlier project. Never edits that checkout.
import { readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = process.argv[2];
if (!source) throw new Error('Usage: node scripts/import-legacy-branding.mjs <prototype checkout>');
const expected = JSON.parse(readFileSync(join(root, 'assets/branding/provenance.json'), 'utf8')).commit;
const actual = execFileSync('git', ['-c', `safe.directory=${resolve(source).replaceAll('\\','/')}`, '-C', source, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
if (!actual.startsWith(expected)) throw new Error(`Brand import requires the recorded source commit ${expected}; found ${actual}`);
const out = join(root, 'assets/branding');
mkdirSync(out, { recursive: true });
for (const [from, to] of [['build/icon.png','icon.png'], ['build/icon.ico','icon.ico'], ['public/favicon.ico','favicon.ico']])
  copyFileSync(join(source, from), join(out, to));
const nav = readFileSync(join(source, 'site/assets/nav.js'), 'utf8');
const mark = nav.match(/var MARK = '([^']+)';/)?.[1];
if (!mark) throw new Error('Canonical site mark not found');
writeFileSync(join(out, 'logo.svg'), mark.replace('class="hwnav-mark"', 'xmlns="http://www.w3.org/2000/svg"').replace('aria-hidden="true"', 'role="img" aria-label="Hollow Wake"') + '\n');
const icons = readFileSync(join(source, 'src/ui/icons.ts'), 'utf8');
const aliases = { relic:'reliquary', sheet:'figure', tree:'star', journal:'scroll', coins:'coin', gear:'options' };
const paths = {}, fills = {};
const ellipse = (x,y,rx,ry) => `M${x-rx} ${y}a${rx} ${ry} 0 1 0 ${2*rx} 0a${rx} ${ry} 0 1 0 ${-2*rx} 0`;
for (const [,id,markup] of icons.matchAll(/^  (\w+): `([^`]+)`,/gm)) {
  const lines = [], solid = [];
  for (const [,tag,attrs] of markup.matchAll(/<(path|circle|ellipse)\s+([^>]+)\/>/g)) {
    const a = Object.fromEntries([...attrs.matchAll(/([\w-]+)="([^"]*)"/g)].map(m => [m[1], m[2]]));
    const path = tag === 'path' ? a.d : ellipse(+a.cx,+a.cy,+(a.rx ?? a.r),+(a.ry ?? a.r));
    lines.push(path);
    if (a.fill === 'currentColor') solid.push(path);
  }
  if (!lines.length) throw new Error(`No geometry in ${id}`);
  for (const key of new Set([id, aliases[id] ?? id])) {
    paths[key] = lines.join(' ');
    if (solid.length) fills[key] = solid.join(' ');
  }
}
const rows = map => Object.entries(map).map(([key,value]) => `      ${key}: ${JSON.stringify(value)}`).join('\n');
writeFileSync(join(root, 'content/core/ui/legacy-icons.yaml'), `# Imported from the earlier Hollow Wake icon registry; see assets/branding/provenance.json.
# Patch the existing vocabulary so its pool/status/weave mappings remain available.
kind: ui_icon_sets
entries:
  - id: runic
    patch: true
    name: Hollow Wake
    description: The shared Hollow Wake line glyphs, with the MMO's resource and combat vocabulary.
    stroke: 1.7
    icons:
${rows(paths)}
    accents:
${rows(fills)}
`);
console.log(`Imported the original insignia and ${Object.keys(paths).length} icon names.`);
