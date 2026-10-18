// Run against Vite: npm run dev -- --port 17880, then node scripts/loading-smoke.mjs.
// Uses real browser input and the production loading module, never a second toy implementation.
import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
const arg=(key,fallback)=>{const i=process.argv.indexOf('--'+key);return i<0?fallback:process.argv[i+1];};
const url=arg('url','http://127.0.0.1:17880'),shots=resolve(arg('shots','artifacts/loading-browser-preview'));
mkdirSync(shots,{recursive:true});
const browser=await chromium.launch({channel:arg('browser',process.platform==='win32'?'msedge':'chrome'),headless:true});
const context=await browser.newContext({viewport:{width:1600,height:900},hasTouch:true});
const page=await context.newPage(),errors=[];
page.on('pageerror',e=>errors.push(e.message));
const check=(name,value)=>{console.log(`LOADING ${value?'PASS':'FAIL'} ${name}`);if(!value)throw new Error(name);};
const wait=ms=>page.waitForTimeout(ms);
try {
  await page.route('**/loading-check.html',r=>r.fulfill({contentType:'text/html',body:'<button id="origin">Return here</button><section id="game">Underlying world</section>'}));
  await page.goto(url+'/loading-check.html');
  await page.evaluate(async()=>{
    const {LoadingScreen}=await import('/src/loading/loadingScreen.ts');
    window.clears=0;window.leaks=0;window.pad={connected:true,axes:[0,0],buttons:Array.from({length:16},()=>({pressed:false}))};
    Object.defineProperty(navigator,'getGamepads',{value:()=>[window.pad]});
    window.cover=new LoadingScreen(()=>window.clears++);
    window.addEventListener('keydown',()=>window.leaks++);
    document.getElementById('origin').focus();
  });
  for(const direction of ['down','right','left']){
    await page.evaluate(d=>{window.lease=window.cover.begin({kind:'travel',direction:d,label:'Gathering the Weft',cancel:()=>window.cover.close()});},direction);
    await wait(200);
    check(direction+' input isolation',await page.evaluate(()=>document.getElementById('game').inert&&window.clears>0));
    await page.keyboard.down(direction==='down'?'ArrowRight':'ArrowDown');await wait(220);
    await page.keyboard.up(direction==='down'?'ArrowRight':'ArrowDown');
    check(direction+' keyboard steering',await page.evaluate(()=>window.cover.snapshot.lane>20&&window.leaks===0));
    const bounds=await page.locator('canvas').boundingBox();
    await page.mouse.move(direction==='down'?bounds.x+bounds.width*.1:bounds.x+bounds.width*.5,
      direction==='down'?bounds.y+bounds.height*.5:bounds.y+bounds.height*.1);
    await wait(450);
    check(direction+' mouse steering',await page.evaluate(()=>window.cover.snapshot.lane<0));
    await page.touchscreen.tap(direction==='down'?bounds.x+bounds.width*.9:bounds.x+bounds.width*.5,
      direction==='down'?bounds.y+bounds.height*.5:bounds.y+bounds.height*.9);
    await wait(700);
    check(direction+' touch steering',await page.evaluate(()=>window.cover.snapshot.lane>0));
    await page.evaluate(d=>{window.pad.axes[d==='down'?0:1]=-1;},direction);await wait(700);
    check(direction+' controller steering',await page.evaluate(()=>window.cover.snapshot.lane<0));
    await page.evaluate(()=>window.pad.axes=[0,0]);
    await wait(1000);await page.screenshot({path:join(shots,direction+'.jpg'),type:'jpeg',quality:85});
    await page.getByRole('button',{name:'Cancel'}).click();
    check(direction+' cancel restores focus',await page.evaluate(()=>!window.cover.active&&!document.getElementById('game').inert&&document.activeElement.id==='origin'));
  }
  await page.evaluate(()=>{
    window.old=window.cover.begin({kind:'entry',label:'Old'});
    window.lease=window.cover.begin({kind:'entry',label:'New',cancel:()=>window.cover.close()});
    window.old.update({label:'Wrong'});window.old.fail('Wrong');window.old.finish();
  });
  check('stale work cannot change current cover',await page.evaluate(()=>window.cover.active&&window.cover.snapshot.label==='New'));
  await page.evaluate(()=>window.lease.update({label:'Land',completed:3,total:9}));
  check('progress reports supplied work',await page.locator('progress').evaluate(p=>p.value===3&&p.max===9));
  await page.evaluate(()=>window.lease.update({label:'Unknown'}));
  check('unknown progress stays indeterminate',await page.locator('progress').evaluate(p=>!p.hasAttribute('value')));
  await page.evaluate(()=>window.lease.fail('Disconnected'));
  check('failure without retry is reported',await page.evaluate(()=>window.cover.snapshot.failed));
  await page.evaluate(()=>window.lease.fail('Retry the stage',()=>window.lease.update({label:'Retrying'})));
  await page.locator('#mu-loading-screen').focus();await page.keyboard.press('Shift+Tab');
  check('reverse tab selects the last loading action',await page.evaluate(()=>document.activeElement.textContent==='Cancel'));
  await page.keyboard.press('Tab');
  check('tab cycles only loading actions',await page.evaluate(()=>document.activeElement.textContent==='Retry'));
  await page.getByRole('button',{name:'Retry'}).click();
  check('retry clears failure',await page.evaluate(()=>!window.cover.snapshot.failed&&window.cover.snapshot.label==='Retrying'));
  await page.keyboard.down('KeyD');
  await page.evaluate(()=>window.lease.finish());
  await page.keyboard.down('KeyD');
  check('held key cannot leak after completion',await page.evaluate(()=>window.leaks===0));
  await page.keyboard.up('KeyD');await page.keyboard.press('KeyD');
  check('released key returns to world',await page.evaluate(()=>window.leaks===1));
  await page.evaluate(()=>{
    window.pad.buttons[0].pressed=true;
    window.lease=window.cover.begin({kind:'entry',label:'Controller',cancel:()=>window.cover.close()});
  });await wait(100);
  check('held confirm cannot dismiss new cover',await page.evaluate(()=>window.cover.active));
  await page.evaluate(()=>window.pad.buttons[0].pressed=false);await wait(100);
  await page.evaluate(()=>window.pad.buttons[0].pressed=true);await wait(100);
  check('controller confirm cancels and stays quarantined',await page.evaluate(()=>!window.cover.active&&window.cover.padQuarantined));
  await page.evaluate(()=>window.pad.buttons[0].pressed=false);
  check('neutral controller returns to world',await page.evaluate(()=>!window.cover.padQuarantined));
  await page.emulateMedia({reducedMotion:'reduce'});await page.setViewportSize({width:390,height:844});
  await page.evaluate(()=>window.lease=window.cover.begin({kind:'entry',label:'Gathering the Weft',cancel:()=>window.cover.close()}));
  await wait(200);await page.keyboard.down('ArrowRight');await wait(200);await page.keyboard.up('ArrowRight');
  check('reduced motion preserves steering',await page.evaluate(()=>window.cover.snapshot.lane>0));
  await page.screenshot({path:join(shots,'portrait-reduced.png')});
  check('portrait controls remain visible',await page.getByRole('button',{name:'Cancel'}).isVisible());
  await page.getByRole('button',{name:'Cancel'}).tap();
  check('touch cancel',await page.evaluate(()=>!window.cover.active));
  check('no browser errors',errors.length===0);
  console.log('LOADING SMOKE PASS');
} finally {await browser.close();}
