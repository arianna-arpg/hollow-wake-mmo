import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const dir = path.dirname(fileURLToPath(import.meta.url));
const report = JSON.parse(fs.readFileSync(path.join(dir, 'snapshot-profile-frames.json'), 'utf8').replace(/^\uFEFF/, ''));
const rows = JSON.parse(fs.readFileSync(path.join(dir, 'snapshot-profile-bench.json'), 'utf8').replace(/^\uFEFF/, ''));
const stages = ['Entities', 'Self', 'Panels', 'EntityEncoding', 'SelfEncoding', 'PanelEncoding', 'ExtensionCapture', 'ExtensionEncoding'];
const sum = (values) => values.reduce((a, b) => a + b, 0);
const mean = (values) => sum(values) / values.length;
function stats(values) {
  const sorted = [...values].sort((a, b) => a - b);
  return { mean: mean(values), p50: sorted[Math.floor(sorted.length * .5)], p95: sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * .95))], max: sorted.at(-1) };
}
function describe(samples) {
  return {
    count: samples.length,
    totalMs: stats(samples.map(s => s.Total.Milliseconds)),
    meanBytes: mean(samples.map(s => s.Total.EncodedBytes)),
    meanAllocation: mean(samples.map(s => s.Total.AllocatedBytes)),
    meanKnown: mean(samples.map(s => s.KnownAfter)),
    observations: sum(samples.map(s => s.Observations)),
    stages: Object.fromEntries(stages.map(stage => [stage, {
      ms: stats(samples.map(s => s[stage].Milliseconds)),
      meanAllocation: mean(samples.map(s => s[stage].AllocatedBytes)),
      messages: sum(samples.map(s => s[stage].Messages)),
      bytes: sum(samples.map(s => s[stage].EncodedBytes))
    }])),
    meanUnassignedProbeMs: mean(samples.map(s => s.Total.Milliseconds - sum(stages.map(k => s[k].Milliseconds)))),
    gcSamples: samples.filter(s => s.Gc.Gen0 || s.Gc.Gen1 || s.Gc.Gen2).map(s => ({ round: s.Round, viewer: s.ViewerIndex, tick: s.Tick, gc: s.Gc, ms: s.Total.Milliseconds })),
    emitted: Object.fromEntries(Object.keys(samples[0].Emitted).map(k => [k, sum(samples.map(s => s.Emitted[k]))]))
  };
}
const phaseNames = ['full', 'delta', 'interface', 'combined'];
const rowNames = ['snapshot.build', 'snapshot.delta.build', 'snapshot.interface.build', 'snapshot.combined.build'];
const byteNames = ['snapshot.bytes', 'snapshot.delta.bytes', 'snapshot.interface.bytes', 'snapshot.combined.bytes'];
const phases = phaseNames.map((name, index) => {
  const samples = report.Samples.filter(s => s.Phase === name);
  const result = describe(samples);
  const row = rows.find(r => r.Name === rowNames[index]);
  const bytes = rows.find(r => r.Name === byteNames[index]);
  if (Math.abs(result.totalMs.mean - row.Value) > 1e-9 || Math.abs(result.meanBytes / 1024 - bytes.Value) > 1e-9)
    throw new Error(`Raw/summary accounting differs for ${name}`);
  for (const sample of samples) {
    const encodings = ['EntityEncoding', 'SelfEncoding', 'PanelEncoding', 'ExtensionEncoding'];
    if (sample.Total.EncodedBytes !== sum(encodings.map(k => sample[k].EncodedBytes)) || sample.Total.Messages !== sum(encodings.map(k => sample[k].Messages)))
      throw new Error(`Frame accounting differs at ${name}/${sample.Round}/${sample.ViewerIndex}`);
  }
  return { name, context: report.Phases.find(p => p.Name === name), ...result,
    viewers: [...new Set(samples.map(s => s.ViewerIndex))].map(viewer => ({ viewer, ...describe(samples.filter(s => s.ViewerIndex === viewer)) })),
    selfCadence: [false, true].map(due => ({ due, ...describe(samples.filter(s => s.SelfDue === due)) })) };
});
console.log(JSON.stringify({ diagnosticOnly: true, rawSummaryAndFrameAccountingMatch: true, workload: report.Workload, environment: report.Environment, phases }, null, 2));
