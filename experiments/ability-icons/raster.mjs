// Runs Godot's CPU SVG loader with no game window, server or input automation.
import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const project = resolve(root, 'experiments/ability-icons/native-raster');
const out = resolve(root, 'artifacts/ability-icons/native-raster');
const godot = process.argv[2] ?? process.env.GODOT;
if (!godot) throw new Error('Supply the Godot .NET executable as the first argument or GODOT.');
mkdirSync(out, {recursive: true});
const profile = resolve(out, 'profile');
mkdirSync(profile, {recursive: true});
const env = {...process.env, APPDATA: profile, LOCALAPPDATA: profile, XDG_DATA_HOME: profile, XDG_CONFIG_HOME: profile, XDG_CACHE_HOME: profile};
for (const [name, args] of [
  ['import', ['--headless', '--path', project, '--editor', '--import']],
  ['raster', ['--headless', '--path', project, '--', '--out', out]],
]) {
  const result = spawnSync(godot, args, {cwd: root, env, windowsHide: true, encoding: 'utf8', timeout: 120000});
  const output = (result.stdout ?? '') + (result.stderr ?? '');
  writeFileSync(resolve(out, name + '.txt'), output);
  process.stdout.write(output);
  if (result.error || result.status !== 0 || (name === 'raster' && !output.includes('ICON RASTER DONE 14/14 passed'))) {
    throw result.error ?? new Error(name + ' failed with exit ' + result.status);
  }
}
