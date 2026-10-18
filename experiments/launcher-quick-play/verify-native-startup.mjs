// Run against a local server (the managed playtest ports by default). A TCP proxy delays the real entry response, not the UI.
import assert from 'node:assert/strict';
import { startProcess, waitProcess } from '../../scripts/test-background.mjs';
import { createServer, connect } from 'node:net';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const arg = name => { const at = process.argv.indexOf(name); return at < 0 ? null : process.argv[at + 1]; };
const executable = arg('--executable');
const godot = arg('--godot') ?? process.env.HOLLOWWAKE_GODOT;
assert.ok(executable || godot, 'pass --godot PATH or --executable PATH');
const config = JSON.parse(readFileSync(path.join(root, 'config/playtest.json'), 'utf8'));
const port = Number(arg('--port') ?? config.port);
assert.ok(Number.isInteger(port) && port > 0 && port <= 65535, 'pass a valid --port');
const out = path.resolve(root, arg('--output') ?? `experiments/launcher-quick-play/output/${executable ? 'combined-export' : 'combined-native'}`);
mkdirSync(out, { recursive: true });
const graphical = process.argv.includes('--isolated-graphical');
const userData = path.join(out, 'native-user'); mkdirSync(userData, { recursive: true });
let runId = 0;
const run = async (command, args) => {
  const log = path.join(out, `process-${++runId}.log`);
  const process = startProcess(command, args, { cwd: root, log, env: { ...globalThis.process.env, APPDATA: userData, XDG_DATA_HOME: userData } });
  const result = await waitProcess(process, 120000);
  return { code: result.code, text: readFileSync(log, 'utf8') };
};
const account = `native-${Date.now()}`;
const seed = await run('dotnet', ['run', '--project', 'src/HollowWake.Tools', '--no-build', '--', 'probe', '--host', '127.0.0.1', '--port', String(port), '--account', account, '--seconds', '3']);
assert.equal(seed.code, 0, seed.text);
assert.match(seed.text, /PROBE OK/);
let selections = 0;
const sockets = new Set();
const proxy = createServer(client => {
  const server = connect(port, '127.0.0.1');
  sockets.add(client); sockets.add(server);
  let clientBytes = Buffer.alloc(0), held = false, queued = [], timer;
  client.on('data', bytes => {
    clientBytes = Buffer.concat([clientBytes, bytes]);
    while (clientBytes.length >= 4) {
      const length = clientBytes.readInt32LE(0);
      if (clientBytes.length < length + 4) break;
      const frame = clientBytes.subarray(0, length + 4); clientBytes = clientBytes.subarray(length + 4);
      const msg = JSON.parse(frame.subarray(4).toString());
      if (msg.t === 'select_character') {
        selections++; held = true;
        timer = setTimeout(() => { held = false; for (const packet of queued.splice(0)) client.write(packet); }, 1800);
      }
      server.write(frame);
    }
  });
  server.on('data', bytes => { if (held) queued.push(bytes); else client.write(bytes); });
  for (const socket of [client, server]) {
    socket.on('error', () => { client.destroy(); server.destroy(); });
    socket.on('close', () => { clearTimeout(timer); client.destroy(); server.destroy(); sockets.delete(socket); });
  }
});
await new Promise(resolve => proxy.listen(0, '127.0.0.1', resolve));
try {
  const args = [...(graphical ? [] : ['--headless']), ...(executable ? [] : ['--path', path.join(root, 'src/HollowWake.Client')]), '--',
    '--host', '127.0.0.1', '--port', String(proxy.address().port), '--account', account,
    '--profile', 'combined-native', '--isolated-input', '--quick-play-smoke', '--shots', out];
  const result = await run(executable ?? godot, args);
  writeFileSync(path.join(out, 'client.log'), result.text);
  assert.equal(result.code, 0, result.text);
  assert.match(result.text, /QUICKPLAY CROSSING/);
  assert.match(result.text, /QUICKPLAY READY: entered the world; Crossing complete/);
  assert.match(result.text, /QUICKPLAY SMOKE PASS/);
  assert.equal(selections, 1, 'one explicit Wake passes through the real server');
  const results = { selections, result: 'Play waits in Mu; explicit Wake, delayed Crossing, terrain readiness and gameplay movement passed', client: executable ? 'exported' : 'source', rendering: graphical ? 'captured' : 'not covered in headless mode' };
  writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results, null, 2));
} finally {
  for (const socket of sockets) socket.destroy();
  await new Promise(resolve => proxy.close(resolve));
}
