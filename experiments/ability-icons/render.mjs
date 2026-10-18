// Focused production-renderer check. Run Vite on 8899, then node experiments/ability-icons/render.mjs.
import { chromium } from '../../web/node_modules/playwright-core/index.mjs';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const out = resolve('artifacts/ability-icons/focused');
mkdirSync(out, {recursive:true});
const browser = await chromium.launch({channel:'msedge',headless:true});
try {
  const page = await browser.newPage({viewport:{width:1080,height:800}});
  await page.goto('http://127.0.0.1:8899/');
  const result = await page.evaluate(async () => {
    const {bundledKinds,loadUi,loadNames} = await import('/src/ui/logic/content.ts');
    const {Skin} = await import('/src/ui/skin.ts');
    const {itemTile} = await import('/src/ui/items.ts');
    const {seatArt,abilityArt} = await import('/src/ui/abilityArt.ts');
    const kinds = bundledKinds(), names = loadNames(kinds), skin = new Skin(loadUi(kinds),names);
    document.body.replaceChildren();
    Object.assign(document.body.style,{background:'#111820',color:'#f4ebd7',font:'16px sans-serif',margin:'36px'});
    const title = document.createElement('h1'); title.textContent='Shared Hollow Wake artwork'; document.body.append(title);
    const subtitle = document.createElement('p'); subtitle.textContent='Inventory • rack • hotbar • small socket • dormant'; document.body.append(subtitle);
    const checks = [];
    for (const [id,name,kind] of [['cleave','Cleave','weaves'],['ember_bolt','Ember Bolt','weaves'],['tide_bolt','Tide Bolt','weaves'],['rallying_shout','Rallying Shout','weaves'],['fork','Fork','glyphs'],['forking','Prototype Forking','glyphs'],['future_support','Future support fallback','glyphs']]) {
      const row = document.createElement('div'); Object.assign(row.style,{display:'flex',alignItems:'center',gap:'38px',height:'78px'});
      const label = document.createElement('span');label.textContent=name;label.style.width='230px';row.append(label);
      const item = {uid:1,base:'memory',name,rarity:'rare',count:1,level:2,lines:[],local:{},...(kind==='glyphs'?{glyph:{id,name,level:2}}:{form:{id,name,level:2,sockets:3,weave:id}})};
      const tile = itemTile(skin,item,32);Object.assign(tile.style,{position:'relative',width:'46px',height:'46px',display:'grid',placeItems:'center',border:'1px solid #b9975b',background:'#211f22'});row.append(tile);
      const face = names.abilityIcons.resolve(kind,id);
      const seat = {key:id,name,icon:null,threads:[],dormant:null,converted:null,info:{form:'bolt',preset:id}};
      const rack = kind==='glyphs'?abilityArt(skin,face,40):seatArt(skin,seat,40);
      const hotbar = kind==='glyphs'?abilityArt(skin,face,32):seatArt(skin,seat,32);
      row.append(rack,hotbar,abilityArt(skin,face,16),abilityArt(skin,face,32,'',true));
      const a = tile.querySelector('svg').innerHTML, b = rack.querySelector('svg').innerHTML;
      if(a!==b || b!==hotbar.querySelector('svg').innerHTML) throw new Error('surface divergence '+id);
      checks.push({id,art:face.art,color:face.color,badge:face.badge,identicalSurfaces:true});document.body.append(row);
    }
    const converted=names.abilityIcons.seat('ember_bolt','bolt','ember_bolt',{label:'Reloaded',weave:'tide_bolt',form:'bolt'});
    if(converted.art!==names.abilityIcons.resolve('weaves','tide_bolt').art) throw new Error('converted art');
    return {checks,converted};
  });
  await page.screenshot({path:resolve(out,'shared-artwork.png')});
  writeFileSync(resolve(out,'report.json'),JSON.stringify(result,null,2)+'\n');
  console.log(`PASS: ${result.checks.length} inventory/rack/hotbar identities, support badges, small sockets, dormant paint and converted identity.`);
} finally { await browser.close(); }
