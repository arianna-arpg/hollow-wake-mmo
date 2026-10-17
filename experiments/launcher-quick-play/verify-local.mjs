import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const runCommand = (command, args) => new Promise((resolve, reject) => {
  const start = performance.now();
  const child = spawn(command, args, { cwd: root, windowsHide: true });
  let output = '';
  child.stdout.on('data', b => output += b);
  child.stderr.on('data', b => output += b);
  child.once('error', reject);
  child.once('exit', code => resolve({ code, output, ms: Math.round(performance.now() - start) }));
});
const run = args => runCommand(process.execPath, ['scripts/play-local.mjs', ...args]);
const output = path.join(root, 'experiments/launcher-quick-play/output');
mkdirSync(output, { recursive: true });
const stateFile = path.join(root, 'artifacts/playtest/server.json');
const results = {};
assert.equal((await run(['--stop'])).code, 0);
const config = JSON.parse(readFileSync(path.join(root, 'config/playtest.json')));
const conflict = createServer();
await new Promise((resolve, reject) => { conflict.once('error', reject); conflict.listen(config.port, '127.0.0.1', resolve); });
try {
  const result = await run(['--browser', '--no-open']);
  assert.equal(result.code, 1);
  assert.match(result.output, /Server startup failed/);
  assert.equal(conflict.listening, true);
  results.portConflict = 'Reported failure without killing the unrelated listener';
} finally { await new Promise(resolve => conflict.close(resolve)); }
assert.equal((await run(['--browser', '--no-open'])).code, 0);
const first = JSON.parse(readFileSync(stateFile));
const warm = await run(['--browser', '--no-open']);
assert.equal(warm.code, 0);
assert.match(warm.output, /Reusing local server/);
assert.equal(JSON.parse(readFileSync(stateFile)).pid, first.pid);
results.warmPreparationMilliseconds = warm.ms;
// Force actual compiler output replacement, not an incremental no-op build.
const rebuild = await runCommand('dotnet', ['build', 'HollowWake.sln', '--no-restore', '-t:Rebuild', '--nologo', '-v', 'quiet']);
assert.equal(rebuild.code, 0, rebuild.output);
assert.equal(JSON.parse(readFileSync(stateFile)).pid, first.pid);
process.kill(first.pid, 0);
results.liveBuild = 'Full solution rebuild succeeds while the same managed server stays running';
assert.equal((await run(['--stop'])).code, 0);
assert.equal(existsSync(stateFile), false);
const shutdownLog = readFileSync(path.join(root, 'artifacts/playtest/server.log'), 'utf8');
assert.match(shutdownLog, /shutting down/);
assert.doesNotMatch(shutdownLog, /ObjectDisposedException|Unhandled exception/);
results.shutdown = 'Lease removal requests the server save/shutdown path and clears the owned record';

// Own a child directly as well, so the host's exit code is observable after lease removal.
const lease = path.join(output, 'shutdown.lease');
const hostConfig = path.join(output, 'shutdown-server.yaml');
writeFileSync(lease, 'shutdown regression');
writeFileSync(hostConfig, readFileSync(path.join(root, 'config/server.yaml'), 'utf8')
  .replace('data_dir: data', 'data_dir: experiments/launcher-quick-play/output/shutdown-data'));
const host = spawn('dotnet', ['artifacts/playtest/server/hollowwake-server.dll', '--root', root, '--config', hostConfig,
  '--bind', '127.0.0.1', '--port', String(config.port), '--no-websocket', '--lifetime-file', lease], { cwd: root, windowsHide: true });
let hostLog = '';
host.stdout.on('data', b => hostLog += b);
host.stderr.on('data', b => hostLog += b);
const exited = new Promise((resolve, reject) => { host.once('error', reject); host.once('exit', code => resolve(code)); });
try {
  const deadline = Date.now() + 15000;
  while (!hostLog.includes(' is up:') && host.exitCode === null && Date.now() < deadline) await new Promise(r => setTimeout(r, 100));
  assert.match(hostLog, / is up:/);
} finally { rmSync(lease, { force: true }); }
const exitCode = await exited;
assert.equal(exitCode, 0, hostLog);
assert.doesNotMatch(hostLog, /ObjectDisposedException|Unhandled exception/);
results.hostExitCode = exitCode;
writeFileSync(path.join(output, 'shutdown-host.log'), hostLog);
writeFileSync(path.join(root, 'experiments/launcher-quick-play/output/local-results.json'), JSON.stringify(results, null, 2));
console.log(JSON.stringify(results, null, 2));
