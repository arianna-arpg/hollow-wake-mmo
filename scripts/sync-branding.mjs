// Canonical assets have one home. Every shipped adapter receives byte-identical copies.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const check = process.argv.includes('--check');
const destinations = {
  'icon.png': ['src/HollowWake.Client/Branding/icon.png', 'launcher/build/icon.png'],
  'icon.ico': ['src/HollowWake.Client/Branding/icon.ico', 'launcher/build/icon.ico'],
  'favicon.ico': ['web/public/favicon.ico'],
  'logo.svg': ['src/HollowWake.Client/icon.svg', 'web/public/icon.svg', 'launcher/build/logo.svg'],
};
for (const [asset, targets] of Object.entries(destinations)) {
  const source = readFileSync(join(root, 'assets/branding', asset));
  for (const target of targets) {
    const path = join(root, target);
    if (check) {
      if (!source.equals(readFileSync(path))) throw new Error(`Brand asset is stale: ${target}`);
    } else {
      mkdirSync(dirname(path), { recursive: true });
      writeFileSync(path, source);
    }
  }
}
console.log(check ? 'Brand parity PASS' : 'Brand assets synchronized');
