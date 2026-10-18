// Cursor-independent smoke runner. See docs/design/testing.md.
import { spawn } from 'node:child_process';
import { createWriteStream, existsSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import { setTimeout as delay } from 'node:timers/promises';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

export function options(args) {
  const { values } = parseArgs({ args, options: {
    suite: { type: 'string', default: 'web' },
    out: { type: 'string', default: join(root, 'artifacts', 'testing') },
    godot: { type: 'string', ...(process.env.GODOT ? { default: process.env.GODOT } : {}) },
    browser: { type: 'string' }, executable: { type: 'string' }, calling: { type: 'string' },
    'no-build': { type: 'boolean', default: false },
    timeout: { type: 'string', default: '600' }, help: { type: 'boolean' },
  } });
  if (!['web', 'godot', 'all'].includes(values.suite)) throw new Error('--suite must be web, godot, or all');
  values.timeout = Number(values.timeout) * 1000;
  if (!Number.isFinite(values.timeout) || values.timeout < 1000 || values.timeout > 3600000)
    throw new Error('--timeout must be between 1 and 3600 seconds per stage');
  return values;
}

async function godotExecutable(explicit) {
  if (explicit) return explicit;
  // WinGet's versioned executable is not always exposed as `godot` on PATH.
  if (process.platform === 'win32' && process.env.LOCALAPPDATA) {
    const project = await readFile(join(root, 'src/HollowWake.Client/HollowWake.Client.csproj'), 'utf8');
    const version = /Godot\.NET\.Sdk\/([^"\s]+)/.exec(project)?.[1];
    const packages = join(process.env.LOCALAPPDATA, 'Microsoft/WinGet/Packages');
    for (const pkg of await readdir(packages).catch(() => [])) {
      if (!pkg.startsWith('GodotEngine.GodotEngine.Mono_')) continue;
      const stem = `Godot_v${version}-stable_mono_win64`;
      // Pipe the engine's output directly; no console-wrapper process is needed.
      const candidate = join(packages, pkg, stem, `${stem}.exe`);
      if (existsSync(candidate)) return candidate;
    }
  }
  return 'godot';
}

// Preserve content and gameplay settings from the committed config; replace only scalar runner settings.
export function serverConfig(source, overrides) {
  let result = source;
  for (const [key, value] of Object.entries(overrides)) {
    const line = `${key}: ${JSON.stringify(value)}`;
    const pattern = new RegExp(`^${key}:.*$`, 'm');
    result = pattern.test(result) ? result.replace(pattern, () => line) : `${result.trimEnd()}\n${line}\n`;
  }
  return result;
}

export async function reservePorts() {
  const sockets = [];
  try {
    for (let i = 0; i < 2; i++) {
      const socket = createServer();
      sockets.push(socket);
      await new Promise((done, fail) => {
        socket.once('error', fail);
        socket.listen(0, '127.0.0.1', done);
      });
    }
    return { ports: sockets.map(s => s.address().port), release: async () => {
      await Promise.all(sockets.map(s => new Promise((done, fail) => s.close(e => e ? fail(e) : done()))));
    } };
  } catch (error) {
    for (const socket of sockets) socket.close();
    throw error;
  }
}

export function startProcess(command, args, { cwd, log, env = process.env }) {
  const output = createWriteStream(log);
  const child = spawn(command, args, { cwd, env, windowsHide: true,
    detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] });
  const run = { child, log, done: false, result: null };
  const finished = new Promise(resolveResult => {
    let error;
    child.once('error', e => { error = e.message; });
    child.stdout?.pipe(output, { end: false });
    child.stderr?.pipe(output, { end: false });
    child.once('close', (code, signal) => {
      run.done = true;
      run.result = { code, signal, ...(error ? { error } : {}) };
      output.end(() => resolveResult(run.result));
    });
  });
  run.finished = finished;
  return run;
}

export async function stopProcess(run) {
  if (run.done || !run.child.pid) return run.finished;
  if (process.platform === 'win32') {
    // Kill only the process tree started by this invocation, never by executable name or port.
    await new Promise((done, fail) => {
      const killer = spawn('taskkill.exe', ['/PID', String(run.child.pid), '/T', '/F'],
        { windowsHide: true, stdio: 'ignore' });
      killer.once('error', fail);
      killer.once('exit', done);
    });
  } else {
    try { process.kill(-run.child.pid, 'SIGKILL'); }
    catch (error) { if (error.code !== 'ESRCH') throw error; }
  }
  let timer;
  try {
    return await Promise.race([run.finished, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(`Could not stop owned process ${run.child.pid}`)), 10000);
    })]);
  } finally { clearTimeout(timer); }
}

export async function waitProcess(run, timeout, signal) {
  let timer;
  let aborted;
  try {
    if (signal?.aborted) throw new Error('Run interrupted');
    return await Promise.race([
      run.finished,
      new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Stage timed out')), timeout); }),
      new Promise((_, reject) => {
        aborted = () => reject(new Error('Run interrupted'));
        signal?.addEventListener('abort', aborted, { once: true });
      }),
    ]);
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', aborted);
    if (!run.done) await stopProcess(run);
  }
}

export async function waitForServer(url, name, server, signal, timeout = 60000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (signal?.aborted) throw new Error('Run interrupted');
    if (server.done) throw new Error(`Private server exited: ${JSON.stringify(server.result)}`);
    try {
      const response = await fetch(`${url}/ws`, { signal: AbortSignal.timeout(1000) });
      const description = await response.json();
      if (description.service === 'hollow-wake' && description.name === name) return;
    } catch { /* Wait for this invocation's server, never reuse a listener found on the chosen port. */ }
    await delay(200);
  }
  throw new Error('Private server did not become ready');
}

export function smokeCommand(suite, opts, { url, tcp, directory, id }) {
  const calling = opts.calling ? ['--calling', opts.calling] : [];
  const account = `${id}-${suite}`;
  if (suite === 'web') return [process.execPath, [join(root, 'web/scripts/smoke.mjs'),
    '--url', `${url}/?server=${encodeURIComponent(url)}`, '--shots', join(directory, 'shots'),
    '--account', account, '--isolated-input', ...calling,
    ...(opts.browser ? ['--browser', opts.browser] : []),
    ...(opts.executable ? ['--executable', opts.executable] : []),
  ]];
  return [opts.godot, ['--headless', '--path', join(root, 'src/HollowWake.Client'),
    '--', '--smoke', '--host', '127.0.0.1', '--port', String(tcp), '--account', account,
    '--profile', account, ...calling]];
}

export function smokeOutcome(log) {
  const match = /^SMOKE DONE (\d+)\/(\d+) passed\b/m.exec(log);
  if (!match || Number(match[2]) === 0 || match[1] !== match[2] || /^SMOKE (FAIL|CRASH)\b/m.test(log))
    throw new Error('Smoke did not report a completed, passing play-through');
  return { passed: Number(match[1]), total: Number(match[2]),
    skipped: [...log.matchAll(/^SMOKE SKIP (.+)$/gm)].map(m => m[1].trim()) };
}

export async function main(args) {
  const opts = options(args);
  if (opts.help) {
    console.log('node scripts/test-background.mjs [--suite web|godot|all] [--no-build]\n' +
      '  [--out DIR] [--godot PATH] [--browser msedge|chrome|chromium] [--executable PATH]\n' +
      '  [--calling ID] [--timeout SECONDS]\n' +
      'Runs without windows or OS input. Install web dependencies first with npm --prefix web ci.\n' +
      'Native headless checks do not produce rendering screenshots. See docs/design/testing.md.');
    return 0;
  }
  const base = resolve(opts.out);
  await mkdir(base, { recursive: true });
  const directory = await mkdtemp(join(base, 'run-'));
  const id = `harness-${directory.split(/[\\/]/).at(-1)}`;
  const report = { started: new Date().toISOString(), root, directory, suite: opts.suite,
    status: 'running', stages: [], nativeRendering: 'not covered by headless mode' };
  const save = () => writeFile(join(directory, 'result.json'), JSON.stringify(report, null, 2) + '\n');
  const owned = [];
  const controller = new AbortController();
  const interrupt = () => controller.abort();
  process.on('SIGINT', interrupt);
  process.on('SIGTERM', interrupt);
  const start = (name, command, arguments_, cwd = root, env) => {
    if (controller.signal.aborted) throw new Error('Run interrupted');
    const log = join(directory, `${name}.log`);
    const run = startProcess(command, arguments_, { cwd, log, env });
    owned.push(run);
    return run;
  };
  const stage = async (name, command, arguments_, cwd = root, env) => {
    console.log(`TEST ${name}; evidence: ${directory}`);
    const entry = { name, status: 'running', started: new Date().toISOString() };
    report.stages.push(entry);
    await save();
    try {
      const result = await waitProcess(start(name, command, arguments_, cwd, env), opts.timeout, controller.signal);
      Object.assign(entry, result);
      if (result.code !== 0) throw new Error(`${name} failed; see ${name}.log`);
      if (name.startsWith('smoke-')) Object.assign(entry, smokeOutcome(await readFile(join(directory, `${name}.log`), 'utf8')));
      entry.status = 'passed';
    } catch (error) {
      entry.status = 'failed';
      entry.error = error.message;
      throw error;
    } finally {
      entry.finished = new Date().toISOString();
      await save();
    }
  };
  console.log(`TEST evidence: ${directory}`);
  try {
    const web = opts.suite !== 'godot';
    const native = opts.suite !== 'web';
    if (native) {
      opts.godot = await godotExecutable(opts.godot);
      report.godot = opts.godot;
    }
    if (web && !existsSync(join(root, 'web/node_modules/playwright-core/package.json')))
      throw new Error('Install pinned web dependencies first: npm --prefix web ci');
    if (!opts['no-build']) {
      await stage('build-server', 'dotnet', ['build', 'src/HollowWake.Server.Host', '--nologo']);
      if (web) {
        // These are the two commands in web/package.json's build script, without a shell or npm.cmd window.
        await stage('typecheck-web', process.execPath, ['node_modules/typescript/bin/tsc', '--noEmit'], join(root, 'web'));
        await stage('build-web', process.execPath, ['node_modules/vite/bin/vite.js', 'build'], join(root, 'web'));
      }
      if (native) await stage('build-godot', 'dotnet', ['build', 'src/HollowWake.Client', '--nologo']);
    }
    const serverDll = join(root, 'src/HollowWake.Server.Host/bin/Debug/net10.0/hollowwake-server.dll');
    if (!existsSync(serverDll) || (web && !existsSync(join(root, 'web/dist/index.html'))))
      throw new Error('Build outputs missing; run without --no-build');
    const reserved = await reservePorts();
    const [tcp, http] = reserved.ports;
    const url = `http://127.0.0.1:${http}`;
    report.server = { tcp, url, name: id, data: join(directory, 'data') };
    try {
      const config = serverConfig(await readFile(join(root, 'config/server.yaml'), 'utf8'), {
        name: id, bind: '127.0.0.1', port: tcp, websocket_port: http, websocket_enabled: true,
        data_dir: join(directory, 'data'), web_root: join(root, 'web/dist'), auth: 'dev',
      });
      await writeFile(join(directory, 'server.yaml'), config);
    } finally { await reserved.release(); }
    await save();
    const server = start('server', 'dotnet', [serverDll, '--root', root, '--config', join(directory, 'server.yaml')]);
    await waitForServer(url, id, server, controller.signal, Math.min(opts.timeout, 60000));
    if (web) await stage('smoke-web', ...smokeCommand('web', opts, { url, tcp, directory, id }));
    if (native) {
      // Keep native user:// data private too, including the interface layout outside the settings profile.
      const userData = join(directory, 'native-user');
      await mkdir(userData, { recursive: true });
      const env = { ...process.env, APPDATA: userData, XDG_DATA_HOME: userData };
      if (process.platform === 'darwin')
        throw new Error('Native user-data isolation on macOS is not supported yet; use the web suite');
      await stage('import-godot', opts.godot, ['--headless', '--path', join(root, 'src/HollowWake.Client'), '--editor', '--import'], root, env);
      await stage('smoke-godot', ...smokeCommand('godot', opts, { url, tcp, directory, id }), root, env);
    }
    if (server.done) throw new Error('Private server exited during testing; see server.log');
    report.status = 'passed';
  } catch (error) {
    report.status = 'failed';
    report.error = error.message;
    console.error(`TEST FAIL ${error.message}`);
  } finally {
    const cleanup = await Promise.allSettled(owned.map(stopProcess));
    report.cleanupErrors = cleanup.filter(r => r.status === 'rejected').map(r => r.reason.message);
    if (report.cleanupErrors.length) report.status = 'failed';
    report.finished = new Date().toISOString();
    await save();
    process.off('SIGINT', interrupt);
    process.off('SIGTERM', interrupt);
  }
  console.log(`TEST ${report.status.toUpperCase()}; report: ${join(directory, 'result.json')}`);
  return report.status === 'passed' ? 0 : 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main(process.argv.slice(2)).then(code => { process.exitCode = code; }).catch(error => {
    console.error(`TEST FAIL ${error.message}`);
    process.exitCode = 1;
  });
}
