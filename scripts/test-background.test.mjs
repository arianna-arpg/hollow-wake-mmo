import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createServer } from 'node:http';
import { setTimeout as delay } from 'node:timers/promises';
import { options, serverConfig, reservePorts, startProcess, stopProcess, waitProcess,
  waitForServer, smokeCommand, smokeOutcome } from './test-background.mjs';

async function workspace(t) {
  const dir = await mkdtemp(join(tmpdir(), 'hollowwake-background-test-'));
  t.after(() => rm(dir, { recursive: true, force: true }));
  return dir;
}

test('both suites enforce background input and reject headed or unbounded runs', () => {
  const opts = options([]);
  const context = { url: 'http://127.0.0.1:43210', tcp: 43211, directory: 'evidence', id: 'private-test' };
  assert.equal(opts.suite, 'web');
  const [, web] = smokeCommand('web', opts, context);
  assert.ok(!web.includes('--headed'));
  assert.ok(web.includes('private-test-web'));
  const [, native] = smokeCommand('godot', opts, context);
  assert.equal(native[0], '--headless');
  assert.ok(native.includes('--profile'));
  assert.ok(native.includes('private-test-godot'), 'the native suite must not inherit the web character position');
  for (const suite of ['web', 'godot']) {
    const [, args] = smokeCommand(suite, options(['--forecasts', '--cast-cues', '--calling', 'kindler']), context);
    assert.ok(args.includes('--forecasts') && args.includes('--cast-cues') && args.includes('kindler'));
  }
  for (const args of [['--headed'], ['--timeout', 'NaN'], ['--timeout', '0'], ['--suite', 'desktop']])
    assert.throws(() => options(args));
});

test('private config preserves gameplay settings and quotes Windows paths literally', () => {
  const config = serverConfig('bind: 0.0.0.0\nport: 7777\nsnapshots:\n  self_seconds: 0.5\ncontent_layers:\n  - content/core\n',
    { bind: '127.0.0.1', port: 40000, data_dir: 'C:\\test path\\$&\\data' });
  assert.ok(config.includes('snapshots:\n  self_seconds: 0.5\ncontent_layers:\n  - content/core\n'));
  assert.ok(config.includes('bind: "127.0.0.1"'));
  assert.ok(!config.includes('7777'));
  assert.equal(JSON.parse(config.split('\n').find(l => l.startsWith('data_dir:')).slice(10)), 'C:\\test path\\$&\\data');
});

test('simultaneous reservations use distinct ports', async () => {
  const first = await reservePorts();
  const second = await reservePorts();
  try { assert.equal(new Set([...first.ports, ...second.ports]).size, 4); }
  finally { await first.release(); await second.release(); }
});

test('server readiness refuses an unrelated server on the selected port', async t => {
  let name = 'someone-else';
  const server = createServer((_, response) => response.end(JSON.stringify({ service: 'hollow-wake', name })));
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  t.after(() => new Promise(done => server.close(done)));
  const url = `http://127.0.0.1:${server.address().port}`;
  await assert.rejects(waitForServer(url, 'ours', { done: false }, undefined, 250), /did not become ready/);
  name = 'ours';
  await waitForServer(url, 'ours', { done: false }, undefined, 1000);
});

test('a clean exit with no completed smoke summary cannot pass', () => {
  for (const log of ['', 'SMOKE DONE 0/0 passed', 'SMOKE DONE 1/2 passed', 'SMOKE FAIL bad\nSMOKE DONE 2/2 passed'])
    assert.throws(() => smokeOutcome(log));
  assert.deepEqual(smokeOutcome('SMOKE DONE 2/2 passed in 3.0s'), { passed: 2, total: 2, skipped: [] });
  assert.deepEqual(smokeOutcome('SMOKE SKIP pixels: headless\nSMOKE DONE 2/2 passed').skipped, ['pixels: headless']);
});

test('process failures preserve output and return a failing status', async t => {
  const dir = await workspace(t);
  const run = startProcess(process.execPath, ['-e', 'console.log("evidence"); process.exitCode = 7'],
    { cwd: dir, log: join(dir, 'child.log') });
  assert.equal((await waitProcess(run, 5000)).code, 7);
  assert.match(await readFile(run.log, 'utf8'), /evidence/);
  const missing = startProcess(join(dir, 'no-such-executable'), [], { cwd: dir, log: join(dir, 'missing.log') });
  assert.match((await waitProcess(missing, 5000)).error, /ENOENT/);
});

test('timeout stops the owned process and its descendant without stopping a neighbour', async t => {
  const dir = await workspace(t);
  const neighbour = startProcess(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { cwd: dir, log: join(dir, 'neighbour.log') });
  const run = startProcess(process.execPath, ['-e',
    'const {spawn}=require("node:child_process"); const child=spawn(process.execPath,["-e","setInterval(()=>{},1000)"],{windowsHide:true}); console.log(child.pid); setInterval(()=>{},1000);'],
  { cwd: dir, log: join(dir, 'tree.log') });
  try {
    let pid;
    for (let attempt = 0; attempt < 100 && !pid; attempt++) {
      await delay(50);
      pid = Number((await readFile(run.log, 'utf8')).trim());
    }
    assert.ok(pid > 0, 'descendant started');
    await assert.rejects(waitProcess(run, 200), /timed out/);
    assert.equal(run.done, true);
    // A killed descendant can briefly remain as a zombie on Linux until its new parent reaps it.
    if (process.platform === 'linux') {
      const status = await readFile(`/proc/${pid}/stat`, 'utf8').catch(error => {
        if (error.code !== 'ENOENT') throw error;
        return null;
      });
      assert.ok(status === null || /\) Z /.test(status), 'descendant exited');
    } else assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' });
    assert.doesNotThrow(() => process.kill(neighbour.child.pid, 0));
  } finally {
    await stopProcess(run);
    await stopProcess(neighbour);
  }
});

test('interruption stops an owned running process', async t => {
  const dir = await workspace(t);
  const run = startProcess(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { cwd: dir, log: join(dir, 'interrupted.log') });
  const controller = new AbortController();
  const pending = waitProcess(run, 5000, controller.signal);
  controller.abort();
  await assert.rejects(pending, /interrupted/);
  assert.equal(run.done, true);
});
