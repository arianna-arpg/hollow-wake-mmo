import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createServer } from '../../web/node_modules/vite/dist/node/index.js';
import { chromium } from '../../web/node_modules/playwright-core/index.mjs';
const root=fileURLToPath(new URL('../..',import.meta.url));
const out=path.join(root,'experiments/goblinkin/evidence'); fs.mkdirSync(out,{recursive:true});
const vite=await createServer({configFile:false,root,cacheDir:path.join(root,'experiments/goblinkin/artifacts/vite'),resolve:{alias:[{find:/^three$/,replacement:path.join(root,'web/node_modules/three/build/three.module.js')},{find:'three/addons',replacement:path.join(root,'web/node_modules/three/examples/jsm')}]} ,optimizeDeps:{entries:['experiments/goblinkin/gallery.html']},server:{host:'127.0.0.1',port:0,fs:{allow:[root]}},logLevel:'error'});
await vite.listen();
const port=vite.httpServer.address().port;
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1050},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(`http://127.0.0.1:${port}/experiments/goblinkin/gallery.html`);
 await page.waitForSelector('body[data-ready="true"]',{timeout:60000});
 await page.screenshot({path:path.join(out,'goblinkin-isometric.jpg'),fullPage:true,type:'jpeg',quality:85});
 await page.locator('#third').click();
 await page.screenshot({path:path.join(out,'goblinkin-third-person.jpg'),fullPage:true,type:'jpeg',quality:85});
 await page.locator('#mono').click();await page.locator('#walk').click();
 await page.screenshot({path:path.join(out,'goblinkin-grayscale-walk.jpg'),fullPage:true,type:'jpeg',quality:85});
 if(process.argv.includes('--write-fixture')) {
  const plans=await page.evaluate(()=>window.goblinkinPlans);
  const fixture=path.join(root,'tests/fixtures/goblinkin-appearance.json');
  fs.writeFileSync(fixture,'[\n'+plans.map(p=>JSON.stringify(p)).join(',\n')+'\n]\n');
 }
 const report=await page.evaluate(()=>window.goblinkinReport);
 fs.writeFileSync(path.join(out,'render-report.json'),JSON.stringify({errors,bodies:report},null,2)+'\n');
 if(errors.length)throw new Error(errors.join('\n'));
 console.log(`Rendered ${report.length} actual body plans in both camera angles and grayscale.`);
}finally{await browser.close();await vite.close();}
