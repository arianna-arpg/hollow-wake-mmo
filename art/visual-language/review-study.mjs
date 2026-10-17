// Local artifact verification. Uses the project's already installed browser tooling.
import { chromium } from '../../web/node_modules/playwright-core/index.mjs';
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

const root=fileURLToPath(new URL('../../experiments/visual-language/',import.meta.url));
mkdirSync(join(root,'evidence'),{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1});
const errors=[],checks=[];
page.on('pageerror',e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
const record=(name,pass)=>{checks.push({name,pass});if(!pass)throw new Error(name);};
try{
  await page.goto(new URL('./study.html',import.meta.url).href);
  record('Study loaded without optional local imagery',await page.locator('#combat').count() === 1);
  for(let t=0;t<9;t++){
    await page.selectOption('#thread',String(t));
    for(let p=0;p<4;p++){
      await page.locator(`[data-phase="${p}"]`).click();
      record(`Thread ${t}, phase ${p} is selected`,await page.locator(`[data-phase="${p}"]`).getAttribute('aria-pressed')==='true');
    }
  }
  await page.selectOption('#thread','0');
  await page.locator('[data-phase="0"]').click();
  await page.locator('.workbench').screenshot({path:join(root,'evidence','combat-ember.png')});
  await page.selectOption('#density','crowd');
  await page.selectOption('#ground','light');
  await page.check('#gray');
  await page.check('#reduced');
  await page.selectOption('#thread','7');
  await page.locator('[data-phase="2"]').click();
  record('Reduced motion prevents autoplay',await page.locator('#play').isDisabled());
  record('Grayscale applies to canvas',await page.locator('#combat').evaluate(c=>getComputedStyle(c).filter==='grayscale(1)'));
  await page.locator('.workbench').screenshot({path:join(root,'evidence','combat-crowded-grayscale.png')});
  for(let p=0;p<4;p++){
    await page.locator(`[data-territory="${p}"]`).click();
    record(`Territory ${p} selected`,await page.locator(`[data-territory="${p}"]`).getAttribute('aria-pressed')==='true');
    await page.locator('.territory-wrap').screenshot({path:join(root,'evidence',`territory-${p}.png`)});
  }
  await page.uncheck('#gray');await page.uncheck('#reduced');
  await page.selectOption('#density','solo');await page.selectOption('#ground','dark');await page.selectOption('#thread','0');
  await page.locator('[data-phase="0"]').click();await page.locator('[data-territory="2"]').click();
  await page.locator('#play').click();
  await page.waitForTimeout(1650);
  record('Playback advances phase',await page.locator('[data-phase="1"]').getAttribute('aria-pressed')==='true');
  await page.locator('#play').click();
  await page.locator('[data-phase="0"]').click();
  await page.screenshot({path:join(root,'evidence','study-desktop.png'),fullPage:true});
  for(const width of [1440,768,390]){
    await page.setViewportSize({width,height:1000});
    record(`No page overflow at ${width}px`,await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  }
  await page.screenshot({path:join(root,'evidence','study-narrow.png'),fullPage:true});
  await page.emulateMedia({reducedMotion:'reduce'});await page.reload();
  record('OS reduced motion is respected',await page.locator('#reduced').isChecked()&&await page.locator('#play').isDisabled());
  record('No browser errors',errors.length===0);
  writeFileSync(join(root,'evidence','browser-checks.json'),JSON.stringify({kind:'Standalone artifact verification, not game runtime tests',checks,errors},null,2)+'\n');
  console.log(`PASS: ${checks.length} artifact checks; desktop, narrow, crowd/grayscale and territory captures saved.`);
}finally{await browser.close();}
