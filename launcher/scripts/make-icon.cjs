// Reuse the original hand-sized icon frames; do not downsample the SVG.
const { execFileSync } = require('node:child_process');
const path = require('node:path');
execFileSync(process.execPath, [path.join(__dirname, '../../scripts/sync-branding.mjs')], { stdio: 'inherit' });
