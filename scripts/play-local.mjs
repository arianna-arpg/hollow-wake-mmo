// One entry point for a checkout or a Codespace. No npm packages are needed to run this script.
import { spawn, spawnSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { closeSync, existsSync, mkdirSync, openSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const runtime = path.join(root, 'artifacts', 'playtest');
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const readJson = file => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return null; } };
const alive = pid => { try { process.kill(pid, 0); return true; } catch { return false; } };

/** Hash names and bytes, including deletions, ignoring generated output. */
export function fingerprint(base, inputs) {
  const hash = createHash('sha256');
  function visit(relative) {
    const file = path.join(base, relative);
    if (!existsSync(file)) { hash.update(`missing:${relative}\0`); return; }
    for (const entry of readdirSync(file, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      if (['node_modules', 'bin', 'obj', '.godot', 'dist', '.git'].includes(entry.name)) continue;
      const name = path.join(relative, entry.name);
      if (entry.isDirectory()) visit(name);
      else if (entry.isFile()) hash.update(name).update('\0').update(readFileSync(path.join(base, name))).update('\0');
    }
  }
  for (const input of inputs) {
    const full = path.join(base, input);
    if (!existsSync(full)) { hash.update(`missing:${input}\0`); continue; }
    // Inputs ending in / are directories; keeping this explicit also handles empty directories.
    if (input.endsWith('/')) visit(input);
    else hash.update(input).update('\0').update(readFileSync(full)).update('\0');
  }
  return hash.digest('hex');
}

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: root, stdio: 'inherit', windowsHide: true });
    child.once('error', error => reject(error.code === 'ENOENT'
      ? new Error(`${command} is not installed or is not on PATH. Install the prerequisites listed in README.md, then try Play again.`) : error));
    child.once('exit', code => code === 0 ? resolve() : reject(new Error(`${path.basename(command)} failed (${code}). See the output above.`)));
  });
}

function npm(args) {
  // Calling npm's JS entry point avoids cmd.exe quoting and works with spaces in checkout paths.
  const found = process.env.npm_execpath || (process.platform === 'win32'
    ? path.join(path.dirname(process.execPath), 'node_modules/npm/bin/npm-cli.js')
    : '/usr/local/lib/node_modules/npm/bin/npm-cli.js');
  if (existsSync(found)) return run(process.execPath, [found, ...args]);
  if (process.platform !== 'win32') return run('npm', args);
  throw new Error('npm was not found beside Node. Install the Node LTS distribution, then open Play.cmd again.');
}

export function validateConfig(value) {
  if (!value || !['browser', 'desktop'].includes(value.client)
      || ![value.port, value.webPort].every(p => Number.isInteger(p) && p > 0 && p <= 65535)
      || value.port === value.webPort || typeof value.account !== 'string' || !/^[A-Za-z0-9_-]{1,32}$/.test(value.account)
      || typeof value.profile !== 'string' || !/^[A-Za-z0-9_-]+$/.test(value.profile)) {
    throw new Error('Invalid config/playtest.json: choose browser or desktop, distinct valid ports, and a simple account/profile name.');
  }
  return value;
}

async function stopServer() {
  const state = readJson(path.join(runtime, 'server.json'));
  if (!state) return;
  if (typeof state.lease !== 'string' || !/^lease-[0-9a-f-]+$/.test(state.lease) || !Number.isInteger(state.pid) || state.pid <= 0) {
    throw new Error('The playtest server record is invalid. Inspect artifacts/playtest/server.json before retrying.');
  }
  const lease = path.join(runtime, state.lease);
  if (existsSync(lease)) rmSync(lease); // Only the file this playtest created, never an arbitrary process.
  const deadline = Date.now() + 15000;
  while (alive(state.pid) && Date.now() < deadline) await sleep(150);
  if (alive(state.pid)) throw new Error('The previous playtest server has not stopped yet. Check artifacts/playtest/server.log and retry.');
  rmSync(path.join(runtime, 'server.json'), { force: true });
}

async function ensureBuild(name, inputs, output, build) {
  const hash = fingerprint(root, inputs);
  const stamp = path.join(runtime, `${name}.json`);
  if (readJson(stamp)?.hash === hash && existsSync(path.join(root, output))) return;
  console.log(`Preparing ${name} (first run or files changed)...`);
  await build();
  // Godot's first import may create source UID sidecars. Include those in the settled stamp.
  writeFileSync(stamp, JSON.stringify({ hash: fingerprint(root, inputs) }));
}

export async function main(args = process.argv.slice(2)) {
  const value = flag => args[args.indexOf(flag) + 1];
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--godot') {
      if (!args[++i] || args[i].startsWith('--')) throw new Error('--godot needs the path to the Godot .NET executable.');
    } else if (!['--help', '--browser', '--desktop', '--stop', '--no-open'].includes(args[i])) {
      throw new Error(`Unknown option ${args[i]}. Run with --help for the available choices.`);
    }
  }
  if (args.includes('--browser') && args.includes('--desktop')) throw new Error('Choose either --browser or --desktop.');
  if (args.includes('--help')) {
    console.log('Play.cmd [--browser | --desktop] [--godot PATH] [--no-open] [--stop]\nDefaults: config/playtest.json. Builds are reused until inputs change. --stop saves and stops only this checkout\'s server.');
    return;
  }
  mkdirSync(runtime, { recursive: true });
  const lock = path.join(runtime, 'startup.lock');
  try {
    const fd = openSync(lock, 'wx');
    writeFileSync(fd, String(process.pid));
    closeSync(fd);
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
    const pid = Number(readFileSync(lock, 'utf8'));
    if (!Number.isInteger(pid) || pid <= 0 || alive(pid)) throw new Error('A playtest startup is already in progress. Wait for that window to finish.');
    rmSync(lock);
    return main(args);
  }
  try {
    if (args.includes('--stop')) { await stopServer(); console.log('Local playtest server stopped. Characters are saved.'); return; }
    const cfg = validateConfig(readJson(path.join(root, 'config/playtest.json')));
    const desktop = args.includes('--desktop') || (!args.includes('--browser') && cfg.client === 'desktop');
    let godot = null;
    if (desktop) {
      godot = args.includes('--godot') ? value('--godot') : process.env.HOLLOWWAKE_GODOT || readJson(path.join(runtime, 'desktop.json'))?.godot || 'godot';
      const check = spawnSync(godot, ['--version'], { encoding: 'utf8', windowsHide: true });
      const required = /Godot\.NET\.Sdk\/([\d.]+)/.exec(readFileSync(path.join(root, 'src/HollowWake.Client/HollowWake.Client.csproj'), 'utf8'))?.[1];
      if (check.status !== 0 || !check.stdout?.startsWith(`${required}.`) || !check.stdout.includes('mono')) {
        throw new Error(`Desktop play needs Godot ${required} .NET. Run Play.cmd --desktop --godot "path to Godot executable" once; the path is remembered. Play.cmd --browser works without Godot.`);
      }
      writeFileSync(path.join(runtime, 'desktop.json'), JSON.stringify({ godot }));
    }
    const serverInputs = ['src/HollowWake.Core/', 'src/HollowWake.Protocol/', 'src/HollowWake.Server/', 'src/HollowWake.Server.Host/', 'Directory.Build.props', 'global.json'];
    const config = existsSync(path.join(root, 'config/server.local.yaml')) ? 'config/server.local.yaml' : 'config/server.yaml';
    const sessionHash = fingerprint(root, [...serverInputs, 'content/', config, 'config/playtest.json', 'scripts/play-local.mjs']);
    let state = readJson(path.join(runtime, 'server.json'));
    const reusable = state && Number.isInteger(state.pid) && state.pid > 0 && alive(state.pid)
      && /^lease-[0-9a-f-]+$/.test(state.lease) && existsSync(path.join(runtime, state.lease))
      && Date.now() - statSync(path.join(runtime, state.lease)).mtimeMs < 5000 && state.hash === sessionHash;
    if (!reusable) await stopServer();
    const serverDll = 'artifacts/playtest/server/hollowwake-server.dll';
    if (!reusable) await ensureBuild('server', [...serverInputs, 'scripts/play-local.mjs'], serverDll, async () => {
      // The previous owned process has stopped. Replace only our private runtime, so a running
      // playtest never locks the solution's normal build outputs (particularly on Windows).
      rmSync(path.join(runtime, 'server'), { recursive: true, force: true });
      await run('dotnet', ['publish', 'src/HollowWake.Server.Host', '-c', 'Debug', '--no-self-contained', '--nologo', '-v', 'quiet', '-o', path.join(runtime, 'server')]);
    });
    if (desktop) {
      await ensureBuild('desktop client', [...serverInputs, 'src/HollowWake.Client/'], 'src/HollowWake.Client/.godot/mono/temp/bin/Debug/HollowWake.Client.dll', async () => {
        await run(godot, ['--headless', '--path', 'src/HollowWake.Client', '--editor', '--import']);
        await run('dotnet', ['build', 'src/HollowWake.Client', '--nologo', '-v', 'quiet']);
      });
    } else {
      await ensureBuild('web dependencies', ['web/package.json', 'web/package-lock.json'], 'web/node_modules/.package-lock.json', () => npm(['--prefix', 'web', 'ci']));
      await ensureBuild('browser client', ['web/', 'content/'], 'web/dist/index.html', () => npm(['--prefix', 'web', 'run', 'build']));
    }
    if (!reusable) {
      const lease = `lease-${randomUUID()}`;
      writeFileSync(path.join(runtime, lease), 'Remove this file to save and stop the local server.\n');
      const log = path.join(runtime, 'server.log');
      const fd = openSync(log, 'w');
      const child = spawn('dotnet', [serverDll, '--root', root, '--config', config, '--bind', '127.0.0.1', '--port', String(cfg.port), '--websocket', '--ws-port', String(cfg.webPort), '--web-root', path.join(root, 'web/dist'), '--lifetime-file', path.join(runtime, lease)], { cwd: root, detached: true, windowsHide: true, stdio: ['ignore', fd, fd] });
      try {
        await new Promise((resolve, reject) => { child.once('spawn', resolve); child.once('error', reject); });
      } finally { closeSync(fd); }
      child.unref();
      state = { pid: child.pid, lease, hash: sessionHash };
      writeFileSync(path.join(runtime, 'server.json'), JSON.stringify(state));
      const deadline = Date.now() + 60000;
      while (Date.now() < deadline) {
        if (!alive(state.pid)) throw new Error(`Server startup failed. Another server may already own these ports.\n${readFileSync(log, 'utf8').slice(-5000)}`);
        if (readFileSync(log, 'utf8').includes(' is up:')) break;
        await sleep(150);
      }
      if (!readFileSync(log, 'utf8').includes(' is up:')) { await stopServer(); throw new Error(`Server startup timed out. See ${log}`); }
    }
    const url = process.env.CODESPACE_NAME
      ? `https://${process.env.CODESPACE_NAME}-${cfg.webPort}.${process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN || 'app.github.dev'}/?play=1&account=${encodeURIComponent(cfg.account)}`
      : `http://localhost:${cfg.webPort}/?play=1&account=${encodeURIComponent(cfg.account)}`;
    console.log(`${reusable ? 'Reusing' : 'Started'} local server. Ready to play: ${url}`);
    console.log('The server stays ready for your next session. Play.cmd --stop saves and stops it. Logs: artifacts/playtest/server.log');
    if (args.includes('--no-open')) return;
    if (desktop) {
      const fd = openSync(path.join(runtime, 'client.log'), 'w');
      const child = spawn(godot, ['--path', path.join(root, 'src/HollowWake.Client'), '--', '--host', '127.0.0.1', '--port', String(cfg.port), '--quick-play', '--account', cfg.account, '--profile', cfg.profile], { cwd: root, detached: true, windowsHide: true, stdio: ['ignore', fd, fd] });
      try { await new Promise((resolve, reject) => { child.once('spawn', resolve); child.once('error', reject); }); } finally { closeSync(fd); }
      child.unref();
    } else if (process.platform === 'win32') {
      process.env.HOLLOWWAKE_PLAY_URL = url;
      await run('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', 'Start-Process -FilePath $env:HOLLOWWAKE_PLAY_URL']);
    } else {
      await run(process.platform === 'darwin' ? 'open' : 'xdg-open', [url]);
    }
  } finally { rmSync(lock, { force: true }); }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  // The URL is passed to PowerShell as data, not interpolated into shell code.
  main().catch(error => { console.error(`\nCould not start Hollow Wake: ${error.message}`); process.exitCode = 1; });
}
