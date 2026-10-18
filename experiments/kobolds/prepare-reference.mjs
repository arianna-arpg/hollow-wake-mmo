import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const source = process.argv[2];
if (!source) throw new Error('Supply the local prototype repository path. It is only read.');
const revision = '118b510f8b5bfb781163c5bc13ea4908f698e75b';
const out = fileURLToPath(new URL('./artifacts/prototype', import.meta.url));
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'source.tar'), execFileSync('git', ['archive', revision, 'src'], { cwd: source, maxBuffer: 100 * 1024 * 1024, windowsHide: true }));
execFileSync('tar', ['-xf', 'source.tar'], { cwd: out, windowsHide: true });
fs.writeFileSync(path.join(out, 'index.html'), `<!doctype html><html><body style="background:#1c2427;color:#ddd;font:20px Georgia"><h1>Prototype visual reference, revision 118b510f</h1><p>Source sprites: grounded dragon / cave gnasher / goblin prodder. Kobolds are a new faction.</p><div id="out" style="display:flex"></div><script type="module">
import {bodySprite} from './src/render/vis/body.ts';
for(const [look,color] of [['roost_dragon','#a75539'],['cave_gnasher','#9a9451'],['gnasher_prodder','#83964f']]) {
 const d=document.createElement('div'); d.style.width='350px';
 const h=document.createElement('h2'); h.textContent=look; d.append(h);
 const c=document.createElement('canvas'); c.width=340; c.height=340;
 const sprite=bodySprite({look,color,radius:65,shape:'circle'});
 c.getContext('2d').drawImage(sprite,170-sprite.width/2,170-sprite.height/2);
 d.append(c); document.querySelector('#out').append(d);
}
document.body.dataset.ready='true';
</script></body></html>`);
console.log('Prepared exact reference sprites under ignored artifacts/prototype. Run render.mjs --reference to capture.');
