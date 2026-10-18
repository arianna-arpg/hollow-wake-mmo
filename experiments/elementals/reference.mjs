import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
import {createServer} from '../../web/node_modules/vite/dist/node/index.js';
import {chromium} from '../../web/node_modules/playwright-core/index.mjs';
const root=fileURLToPath(new URL('../..',import.meta.url));
const source=process.env.HOLLOWWAKE_PROTOTYPE ?? 'D:/Games/Claude/ARPG Game - World';
const vite=await createServer({configFile:false,root,cacheDir:path.join(root,'experiments/elementals/artifacts/reference-vite'),resolve:{alias:{prototype:path.join(source,'src')}},optimizeDeps:{noDiscovery:true},server:{host:'127.0.0.1',port:0,fs:{allow:[root,source]}},logLevel:'error'});
await vite.listen();const browser=await chromium.launch({channel:'msedge',headless:true});
try{const page=await browser.newPage({viewport:{width:1120,height:700}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
await page.goto('http://127.0.0.1:'+vite.httpServer.address().port+'/experiments/elementals/reference.html');await page.waitForSelector('body[data-ready=true]',{timeout:60000});
await page.screenshot({path:path.join(root,'experiments/elementals/evidence/prototype-reference.jpg'),fullPage:true,type:'jpeg',quality:85});
if(errors.length)throw new Error(errors.join('\n'));console.log('Source sprites and live parts captured.');}finally{await browser.close();await vite.close();}
