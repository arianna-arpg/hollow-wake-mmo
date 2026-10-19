// Provenance only: this program never starts a benchmark, server, build or trace.
import { readFileSync, readdirSync, statSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, relative, join } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const root = process.cwd();
const out = resolve('experiments/golems/artifacts/corrected-foundation-no-run');
mkdirSync(out, { recursive:true });
const git = (...args) => execFileSync('git', args, {encoding:'utf8'}).trim();
const sha = data => createHash('sha256').update(data).digest('hex');
const sourcePaths = ['src','content','web','scripts','tests','HollowWake.sln','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config'];
const runtime = 'b17ebccae418e5e63ca54e2b53f9365863816b7a';
const head = git('rev-parse','HEAD');
const sourceDiff = git('diff','--name-only',runtime,head,'--',...sourcePaths);
if(sourceDiff) throw Error(`Publication source differs from tested runtime: ${sourceDiff}`);
const dirty = git('status','--porcelain','--untracked-files=no','--',...sourcePaths);
if(dirty) throw Error(`Tracked source changed: ${dirty}`);
const sourceManifest = git('ls-tree','-r',head,'--',...sourcePaths)+'\n';
writeFileSync(join(out,'source-manifest.txt'),sourceManifest);
const binaryRoot = resolve('src/HollowWake.Tools/bin/Release/net10.0');
function walk(dir) { return readdirSync(dir,{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name)).flatMap(entry => entry.isDirectory() ? walk(join(dir,entry.name)) : [join(dir,entry.name)]); }
const binaries = walk(binaryRoot).map(path=>({path:relative(root,path).replaceAll('\\','/'),bytes:statSync(path).size,sha256:sha(readFileSync(path))}));
const testedBinaries = JSON.parse(readFileSync('experiments/golems/evidence/corrected-foundation/runtime-provenance.json','utf8')).binaries;
for (const tested of testedBinaries) {
  const current = binaries.find(entry=>entry.path===tested.path);
  if (!current || current.sha256 !== tested.sha256.toLowerCase()) throw Error(`Release binary changed after verification: ${tested.path}`);
}
const manifestText=JSON.stringify(binaries,null,2)+'\n';
writeFileSync(join(out,'release-manifest.json'),manifestText);
const metadata={
  status:'NO-RUN',executed:false,performanceMeasured:false,traceCaptured:false,
  publicationHead:head,testedRuntime:runtime,incomingFoundation:'1bbaa120730376e1297c4f4a7011291c1f69b542',
  createdUtc:new Date().toISOString(),platform:process.platform,architecture:process.arch,workingDirectory:root,
  configuration:'Release',publicationSourceEqualsTestedRuntime:true,testedKeyBinariesUnchanged:true,sourceManifestSha256:sha(sourceManifest),
  releaseManifestSha256:sha(manifestText),releaseFileCount:binaries.length,
  keyBinaries:binaries.filter(x=>/\/(hollowwake|HollowWake.Core|HollowWake.Server|HollowWake.Protocol)\.dll$/.test(x.path)),
  proposedCommands:[
    {executable:'dotnet',arguments:['src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll','bench','--json']},
    {executable:'dotnet',arguments:['src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll','bench','--only','hit-bodies','--json']}
  ],
  defaults:{seed:'aureth',monsters:300,players:4,ticks:600,minions:0,castCues:false,workloadOverrides:false},
  processPriority:'Normal when separately authorized',
  executionRequirements:['Coordinator grants a bounded shared-machine quiet window for this exact head and these Release hashes.','Recheck source and all binary hashes before execution.','Use first rows only; retain stdout, stderr, timestamps, process priority, PIDs and every failure.','No automatic retry, trace, workload substitution or budget change.'],
  openAcceptance:{historicalPlanningMs:43.9282,planningBudgetMs:40,historicalExpressionAllocationBytesPerEvaluation:0.0168,provingCeilingFailures:['proving_bulwark','proving_plaguer','proving_pitfighter'],combined20and50:'coordinator-owned'},
  note:'Prepared metadata and command arguments only. The earlier source build is retained; publication changes evidence only. No performance process was started.'
};
writeFileSync(join(out,'metadata.json'),JSON.stringify(metadata,null,2)+'\n');
console.log(JSON.stringify({status:metadata.status,head,sourceManifestSha256:metadata.sourceManifestSha256,releaseManifestSha256:metadata.releaseManifestSha256,releaseFileCount:binaries.length,metadataPath:join(out,'metadata.json')},null,2));
