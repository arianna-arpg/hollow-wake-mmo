// Rebuild the layout fixture from the actual prototype function, never from either new client.
import { readFile, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import ts from '../web/node_modules/typescript/lib/typescript.js';
const repository = process.argv[2];
if (!repository) throw new Error('Usage: node scripts/mu-reference.mjs <prototype-checkout>');
const repo = resolve(repository).replaceAll('\\', '/'), source = '118b510f';
let code = execFileSync('git', ['-c', `safe.directory=${repo}`, '-C', repo, 'show', `${source}:src/engine/muRing.ts`], { encoding: 'utf8' });
code = code.replace(/import .*?;\r?\n/g, '');
const spec = JSON.parse(await readFile('content/core/ui/entry.json', 'utf8')).entries[0].meta;
const js = ts.transpileModule(code, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
const exports = {}; new Function('exports', 'MU_CFG', js)(exports, { arc: { from: spec.arcFrom, to: spec.arcTo } });
const cases = [0, 1, 2, 6, 8, 12, 24, 80].map(count => ({ count, ...exports.muRankRing(count, spec.radius, spec.seatGap, { from: spec.arcFrom, to: spec.arcTo }) }));
await writeFile('tests/fixtures/mu-rings.json', JSON.stringify({ source, file: 'src/engine/muRing.ts', cases }, null, 2) + '\n');
