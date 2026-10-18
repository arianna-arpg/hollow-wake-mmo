// Real server entry with delayed frames: cancellation, timeout/retry, and safe input handoff.
// Run Vite on 17880 and the local WebSocket server on 17878 (override with flags).
import { chromium } from 'playwright-core';
const arg=(key,fallback)=>{const i=process.argv.indexOf('--'+key);return i<0?fallback:process.argv[i+1];};
const browser=await chromium.launch({channel:arg('browser',process.platform==='win32'?'msedge':'chrome'),headless:true,
  args:['--ignore-gpu-blocklist','--enable-unsafe-swiftshader']});
const page=await browser.newPage();
const errors=[];page.on('pageerror',e=>errors.push(e.message));
let hold=false,pending=[],route,upstream,selects=0;
const check=(name,value)=>{console.log(`ENTRY ${value?'PASS':'FAIL'} ${name}`);if(!value)throw new Error(name);};
await page.routeWebSocket('**/ws',ws=>{
  route=ws;upstream=ws.connectToServer();const server=upstream;
  ws.onMessage(msg=>{if(JSON.parse(msg.toString()).t==='select_character')selects++;server.send(msg);});
  server.onMessage(msg=>{if(hold)pending.push({ws,msg});else ws.send(msg);});
});
try {
  const server=arg('server','ws://localhost:17878');
  await page.goto(`${arg('url','http://127.0.0.1:17880')}/?server=${encodeURIComponent(server)}&account=loading-entry-smoke&profile=loading-entry&fresh-settings`);
  await page.waitForFunction(()=>window.hollowWake&&!window.hollowWake.loading.active);
  await page.click('#login-connect');
  await page.waitForFunction(()=>window.hollowWake.counts.characterLists>0);
  if(!await page.evaluate(()=>window.hollowWake.characters().length)){
    await page.fill('#login-new-name','Crossing');await page.click('#login-create');
    await page.waitForFunction(()=>window.hollowWake.characters().length>0);
  }
  await page.locator('.character').first().click();hold=true;
  await page.click('#login-enter');await page.waitForTimeout(300);
  check('entry waits for actual world work',await page.evaluate(()=>window.hollowWake.loading.active&&!window.hollowWake.inWorld));
  await page.keyboard.down('KeyD');await page.waitForTimeout(250);await page.keyboard.up('KeyD');
  check('toy remains playable during slow entry',await page.evaluate(()=>window.hollowWake.loading.lane>0));
  await page.getByRole('button',{name:'Cancel',exact:true}).click();
  check('cancel returns to login',await page.evaluate(()=>!window.hollowWake.loading.active&&!window.hollowWake.inWorld));
  // Frames belonging to the cancelled socket cannot enter a later session.
  for(const frame of pending.splice(0))try{frame.ws.send(frame.msg);}catch{}
  hold=false;await page.waitForTimeout(300);
  check('late cancelled frames are ignored',await page.evaluate(()=>!window.hollowWake.inWorld));
  const count=await page.evaluate(()=>window.hollowWake.counts.characterLists);
  await page.click('#login-connect');await page.waitForFunction(n=>window.hollowWake.counts.characterLists>n,count);
  await page.locator('.character').first().click();hold=true;await page.click('#login-enter');
  // The test changes presentation's timeout, not production's timer or loading implementation.
  await page.evaluate(async()=>{const {presentation}=await import('/src/loading/content.ts');presentation.timeoutSeconds=.5;});
  await page.waitForFunction(()=>window.hollowWake.loading.failed);
  check('timeout offers retry and cancel',await page.getByRole('button',{name:'Retry',exact:true}).isVisible());
  const before=selects;
  await page.evaluate(async()=>{const {presentation}=await import('/src/loading/content.ts');presentation.timeoutSeconds=45;});
  await page.getByRole('button',{name:'Retry',exact:true}).click();
  await page.waitForTimeout(150);
  check('retry clears failure without selecting twice',selects===before&&await page.evaluate(()=>!window.hollowWake.loading.failed));
  // Release the original authoritative response and complete the local terrain preparation.
  hold=false;for(const frame of pending.splice(0))frame.ws.send(frame.msg);
  await page.waitForTimeout(300);
  if(await page.getByRole('button',{name:'Retry',exact:true}).isVisible())await page.getByRole('button',{name:'Retry',exact:true}).click();
  await page.waitForFunction(()=>window.hollowWake.inWorld&&window.hollowWake.self()&&!window.hollowWake.loading.active);
  check('entry closes when terrain and self are ready',await page.evaluate(()=>window.hollowWake.loading.active===false));
  check('no browser errors',errors.length===0);
  console.log('ENTRY SMOKE PASS');
}catch(error){console.error('Browser errors:',errors);console.error((await page.locator('body').innerText()).slice(-1800));throw error;}
finally{await browser.close();}
