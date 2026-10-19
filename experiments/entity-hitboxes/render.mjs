import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createServer} from '../../web/node_modules/vite/dist/node/index.js';
import {chromium} from '../../web/node_modules/playwright-core/index.mjs';
const root=fileURLToPath(new URL('../..',import.meta.url)),out=path.join(root,'experiments/entity-hitboxes/evidence');
const server=await createServer({configFile:false,root,cacheDir:path.join(root,'experiments/entity-hitboxes/artifacts/vite'),resolve:{alias:[{find:/^three$/,replacement:path.join(root,'web/node_modules/three/build/three.module.js')},{find:'three/addons',replacement:path.join(root,'web/node_modules/three/examples/jsm')}]},optimizeDeps:{entries:['experiments/entity-hitboxes/gallery.html']},server:{host:'127.0.0.1',port:0,fs:{allow:[root]}},logLevel:'error'});
await server.listen();const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1080}}),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(`http://127.0.0.1:${server.httpServer.address().port}/experiments/entity-hitboxes/gallery.html`);
 await page.waitForSelector('body[data-ready="true"]');
 await page.screenshot({path:path.join(out,'bodies-front.jpg'),fullPage:true,type:'jpeg',quality:85});
 await page.locator('#turn').click();await page.screenshot({path:path.join(out,'bodies-turned.jpg'),fullPage:true,type:'jpeg',quality:85});
 if(process.argv.includes('--write-fixture'))fs.writeFileSync(path.join(root,'tests/fixtures/hit-body-lines.json'),JSON.stringify(await page.evaluate(()=>window.hitFixtures))+'\n');
 fs.writeFileSync(path.join(out,'render.json'),JSON.stringify({errors,captures:['bodies-front.jpg','bodies-turned.jpg']},null,2)+'\n');
 if(errors.length)throw Error(errors.join('\n'));console.log('Hit body gallery: both views rendered without errors.');
}finally{await browser.close();await server.close();}
