import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { dirname, resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = dirname(fileURLToPath(import.meta.url));
const repo = resolve(root, '../..');
const require = createRequire(resolve(repo, 'web/package.json'));
const ts = require('typescript');
const sources = new Map([
  ['/runtime/terrainMesh', 'web/src/world/terrainMesh.ts'],
  ['/runtime/chunks', 'web/src/world/chunks.ts'],
  ['/runtime/palette', 'web/src/world/palette.ts'],
  ['/runtime/coords', 'web/src/world/coords.ts'],
  ['/runtime/camera', 'web/src/view/camera.ts'],
]);
const vendor = new Map([
  ['/vendor/three.module.js', 'web/node_modules/three/build/three.module.js'],
  ['/vendor/three.core.js', 'web/node_modules/three/build/three.core.js'],
  ['/vendor/BufferGeometryUtils.js', 'web/node_modules/three/examples/jsm/utils/BufferGeometryUtils.js'],
]);
const files = new Set(['index.html', 'study.css', 'study.mjs', 'actors.mjs', 'fixture.mjs', 'study-config.json', 'appearance.example.json']);
const mime = { '.html': 'text/html', '.css': 'text/css', '.mjs': 'text/javascript', '.js': 'text/javascript', '.json': 'application/json' };

export function createStudyServer(port = 8791) {
  const server = http.createServer(async (req, res) => {
    try {
      if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405).end(); return; }
      const path = new URL(req.url, 'http://127.0.0.1').pathname;
      if (path === '/favicon.ico') { res.writeHead(204).end(); return; }
      let body, type;
      const source = sources.get(path.replace(/\.js$/, ''));
      if (source) {
        body = ts.transpileModule(await readFile(resolve(repo, source), 'utf8'), {
          compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 },
        }).outputText;
        type = 'text/javascript';
      } else if (vendor.has(path)) {
        body = await readFile(resolve(repo, vendor.get(path)));
        type = 'text/javascript';
      } else if (path === '/runtime-config.json') {
        body = await readFile(resolve(repo, 'web/public/config.json'));
        type = 'application/json';
      } else {
        const name = path === '/' ? 'index.html' : path.slice(1);
        if (!files.has(name)) { res.writeHead(404).end('Not found'); return; }
        body = await readFile(resolve(root, name));
        type = mime[extname(name)];
      }
      res.writeHead(200, { 'Content-Type': `${type}; charset=utf-8`, 'Cache-Control': 'no-store' });
      res.end(req.method === 'HEAD' ? undefined : body);
    } catch (error) {
      res.writeHead(500).end('Study dependency unavailable. See the local terminal.');
      console.error(error.message);
    }
  });
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', () => accept(server));
  });
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const port = Number(process.env.SPRITE_STUDY_PORT ?? 8791);
  await createStudyServer(port);
  console.log(`Hollow Wake rendering study: http://127.0.0.1:${port}/ (Ctrl+C to stop)`);
}
