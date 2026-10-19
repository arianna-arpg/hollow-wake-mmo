import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { isDeepStrictEqual } from 'node:util';
const dir = 'experiments/golems/evidence/corrected-foundation/';
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const standard = read(dir + 'standard-actual.json');
const inheritedStandard = read('experiments/entity-review/evidence/34-final-union/standard-actual.json');
const proving = read(dir + 'proving.json');
const priorProving = read('experiments/golems/evidence/complete-foundation/proving-after.json');
const inheritedProving = read('experiments/entity-review/evidence/34-final-union/proving.json');
const oldBaseline = read(dir + 'baseline-before.json');
const baseline = read('experiments/combat-grammar/baselines/standard-duels.json');
const changes = standard.Pairs.flatMap(p => {
  const key = `${p.A}:${p.B}`;
  if (p.Hash !== baseline.Hashes[key]) throw Error(`Actual replay mismatch: ${key}`);
  return p.Hash === oldBaseline.Hashes[key] ? [] : [{pair:key, before:oldBaseline.Hashes[key], after:p.Hash}];
});
const literalKeys = path => readFileSync(path, 'utf8').replace(/^\uFEFF/, '').split(/\r?\n/).filter(x=>x && !x.startsWith('#')).sort();
const literals = literalKeys(dir + 'frozen-literals.txt');
const expected = literalKeys('tests/fixtures/grammar/frozen-literals.txt');
const worldUnchanged = isDeepStrictEqual(read(dir + 'world-before.json'), read(dir + 'world-after.json'));
const git = (...args) => execFileSync('git', args, {encoding:'utf8'}).trim();
const comparisons = {
  source:git('rev-parse','HEAD'), incoming:'1bbaa120730376e1297c4f4a7011291c1f69b542',
  standardEqualsIncomingCompleteReport:isDeepStrictEqual(standard,inheritedStandard),
  standardChangedPairs:changes, remainingStandardPairsUnchanged:standard.Pairs.length-changes.length,
  provingEqualsPriorCompleteReport:isDeepStrictEqual(proving,priorProving),
  provingEqualsIncomingCompleteReport:isDeepStrictEqual(proving,inheritedProving),
  provingHash:proving.Hash, provingPassed:proving.Passed,
  breaches:proving.Pairs.flatMap(p=>p.Breaches),
  fourZoneWorldAuditUnchanged:worldUnchanged,
  censusKeys:literals.length, censusExactlyEqualsFixture:isDeepStrictEqual(literals,expected),
  contentChangedSincePrior:git('diff','--name-only','e5565d07','HEAD','--','content').split('\n'),
  coreDifferenceFromIncoming:git('diff','--name-only','1bbaa120','HEAD','--','src/HollowWake.Core').split('\n'),
  sharedProtocolServerToolsDifference:git('diff','--name-only','1bbaa120','HEAD','--','src/HollowWake.Protocol','src/HollowWake.Server','src/HollowWake.Tools'),
  ownGenerationFixtureDifference:git('diff','--name-only','e5565d07','HEAD','--','tests/HollowWake.Core.Tests/WorldGenGoldenTests.cs'),
  replayFixtureEditedLocally:false,
  causalEvidence:'Incoming complete-source counterfactuals retained under experiments/entity-review/evidence/34-final-union and experiments/entity-hitboxes/evidence/layered-ground. Current whole standard report equals incoming. Those counterfactuals were not rerun locally.'
};
writeFileSync(dir+'comparison.json',JSON.stringify(comparisons,null,2)+'\n');
console.log(JSON.stringify(comparisons,null,2));
if (!comparisons.standardEqualsIncomingCompleteReport || !comparisons.provingEqualsPriorCompleteReport || !comparisons.provingEqualsIncomingCompleteReport || !worldUnchanged || !comparisons.censusExactlyEqualsFixture || literals.length !== 523 || changes.length !== 2) process.exitCode=1;
