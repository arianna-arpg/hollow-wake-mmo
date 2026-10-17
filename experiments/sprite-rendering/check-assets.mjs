// Lightweight semantic check of the illustrative export, without installing a validator.
// This checks local schema references and manifest arithmetic, not full JSON Schema conformance.
import { readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = dirname(fileURLToPath(import.meta.url));
const read = async name => JSON.parse(await readFile(resolve(root, name), 'utf8'));
const schema = await read('appearance.schema.json'), manifest = await read('appearance.example.json'), config = await read('study-config.json');
const checks = [];
const check = (name, predicate) => { assert.ok(predicate, name); checks.push(name); };
const visit = value => {
  if (!value || typeof value !== 'object') return;
  if (value.$ref) {
    check(`Schema reference ${value.$ref}`, value.$ref.startsWith('#/') && value.$ref.slice(2).split('/').reduce((v, key) => v?.[key], schema) !== undefined);
  }
  for (const child of Object.values(value)) visit(child);
};
visit(schema);
check('Manifest root required fields', schema.required.every(key => key in manifest));
const a = manifest.representations.sprite;
check('Grid matches pixel dimensions', a.grid.every((n, i) => n * a.cellPixels[i] === a.atlasPixels[i]));
check('Grid covers views and frames', a.grid[0] === a.directionsDegrees.length && a.grid[1] === a.pitchDegrees.length * a.framesPerPitch);
check('Clips stay inside pose set', a.clips.every(clip => clip.fps > 0 && clip.frameIndices.every(f => Number.isInteger(f) && f >= 0 && f < a.framesPerPitch)));
check('Foot pivot is normalized and matches the bake', a.footPivotNormalized.every(n => n >= 0 && n <= 1) && Math.abs(a.footPivotNormalized[1] - (1 + config.atlas.bottom / config.atlas.worldSize)) < 1e-8);
check('Manifest matches generated study views', a.directionsDegrees.length === config.atlas.directions && JSON.stringify(a.pitchDegrees) === JSON.stringify(config.atlas.pitchDegrees));
const file = resolve(root, a.atlas);
check('Atlas stays in experiment', !relative(root, file).startsWith('..'));
const png = await readFile(file);
check('Exported PNG dimensions match manifest', png.readUInt32BE(16) === a.atlasPixels[0] && png.readUInt32BE(20) === a.atlasPixels[1]);
check('All provided socket samples are in range', a.sockets.every(socket => socket.samples.every(s => s.directionIndex < a.directionsDegrees.length && s.pitchIndex < a.pitchDegrees.length && s.frameIndex < a.framesPerPitch)));
await writeFile(resolve(root, 'evidence/asset-checks.json'), JSON.stringify({ recordedAt: new Date().toISOString(), scope: 'JSON syntax, local schema references and example semantic checks only. Full JSON Schema validation was not run.', checks }, null, 2));
console.log(`PASS ${checks.length} asset-manifest checks`);
