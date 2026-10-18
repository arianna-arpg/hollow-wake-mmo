// Regenerate golden traces against the owner's exact reference, never against the port.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import ts from '../web/node_modules/typescript/lib/typescript.js';
const repo = process.argv[2];
if (!repo) throw new Error('Usage: node scripts/loading-reference.mjs <prototype checkout>');
const commit = '64819bd4351001a34c92eed13b63e1f55f0c52ef';
const source = file => execFileSync('git', ['-c', 'safe.directory=' + repo.replaceAll('\\', '/'), '-C', repo, 'show', commit + ':' + file], { encoding: 'utf8' });
const js = code => ts.transpileModule(code, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText;
const url = code => 'data:text/javascript;base64,' + Buffer.from(js(code)).toString('base64');
const rngSource = source('src/core/rng.ts'), modelSource = source('src/loading/spiritRun.ts');
const rngUrl = url(rngSource);
const { Rng } = await import(rngUrl);
const { SpiritRun } = await import(url(modelSource.replace("'../core/rng'", JSON.stringify(rngUrl))));
const cases = [];
for (const [seed, direction] of [[160, 'down'], [8273, 'right'], [42639, 'left']]) {
  const rng = new Rng(seed), run = new SpiritRun(direction, () => rng.next()), samples = [];
  for (let frame = 0; frame < 1800; frame++) {
    const phase = frame % 240;
    run.step(1 / 30, { axis: phase < 60 ? 1 : phase < 120 ? -1 : 0, target: phase >= 120 ? (Math.floor(frame / 240) % 2 ? -150 : 150) : undefined });
    if ([0, 29, 119, 299, 599, 1199, 1799].includes(frame))
      samples.push({ frame, time: run.time, lane: run.lane, distance: run.distance, speed: run.speed,
        passed: run.passed, hits: run.hits, collected: run.collected, boostGates: run.boostGates, currentsTaken: run.currentsTaken,
        dash: run.dash, hinder: run.hinder, gates: run.gates.map(g => ({ ...g, openings: g.openings.map(o => ({ ...o })) })) });
  }
  cases.push({ seed, direction, samples });
}
mkdirSync('tests/fixtures', { recursive: true });
writeFileSync('tests/fixtures/loading-reference.json', JSON.stringify({
  commit, sourceSha256: createHash('sha256').update(modelSource).digest('hex'), cases,
}, null, 2) + '\n');
console.log('Wrote independent reference traces for ' + cases.length + ' directions/seeds');
