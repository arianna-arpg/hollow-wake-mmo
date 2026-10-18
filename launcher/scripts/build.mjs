// npm run build: compile the main process, preload and tests (tsconfig.json) and the page
// (tsconfig.renderer.json) into dist/, then copy what is not TypeScript: the page's HTML and CSS,
// the window icon and the news that ships inside the launcher.

import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dist = path.join(root, 'dist');
const tsc = path.join(root, 'node_modules', 'typescript', 'bin', 'tsc');

rmSync(dist, { recursive: true, force: true });
for (const project of ['tsconfig.json', 'tsconfig.renderer.json']) {
  const result = spawnSync(process.execPath, [tsc, '-p', path.join(root, project)], { stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

const copies = [
  ['src/renderer/index.html', 'dist/renderer/index.html'],
  ['src/renderer/style.css', 'dist/renderer/style.css'],
  ['build/icon.png', 'dist/icon.png'],
  ['build/icon.ico', 'dist/icon.ico'],
  ['build/logo.svg', 'dist/renderer/logo.svg'],
  ['news.json', 'dist/news.json'],
];
for (const [from, to] of copies) {
  mkdirSync(path.dirname(path.join(root, to)), { recursive: true });
  copyFileSync(path.join(root, from), path.join(root, to));
}
console.log('built dist/');
