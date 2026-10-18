// Production item/seat surfaces in a private server and headless browser. No desktop input.
import { chromium } from '../../web/node_modules/playwright-core/index.mjs';
import { createServer } from '../../web/node_modules/vite/dist/node/index.js';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const out = resolve('artifacts/ability-icons/focused');
mkdirSync(out, {recursive:true});
const server = await createServer({root:resolve('web'),configFile:resolve('web/vite.config.ts'),server:{host:'127.0.0.1',port:0}});
let browser;
try {
  await server.listen();
  const port = server.httpServer.address().port;
  browser = await chromium.launch({channel:'msedge',headless:true});
  const page = await browser.newPage({viewport:{width:1080,height:1000}});
  // Load only the surface modules: no login bootstrap, public config import or game socket.
  await page.route('**/__icon_gallery', route => route.fulfill({contentType:'text/html',body:'<!doctype html><html><head><link rel="stylesheet" href="/src/style.css"></head><body></body></html>'}));
  await page.goto('http://127.0.0.1:'+port+'/__icon_gallery');
  const result = await page.evaluate(async () => {
    await (await import('/src/ui/logic/contentFiles.ts')).preloadAbilityArtwork();
    const {bundledKinds,loadUi,loadNames} = await import('/src/ui/logic/content.ts');
    const {Skin} = await import('/src/ui/skin.ts');
    const {itemTile} = await import('/src/ui/items.ts');
    const {seatArt,abilityArt} = await import('/src/ui/abilityArt.ts');
    const kinds = bundledKinds(), names = loadNames(kinds), skin = new Skin(loadUi(kinds),names);
    document.body.replaceChildren();
    Object.assign(document.body.style,{background:'#111820',color:'#f4ebd7',font:'16px sans-serif',margin:'36px'});
    const title = document.createElement('h1'); title.textContent='Shared Hollow Wake artwork'; document.body.append(title);
    const subtitle = document.createElement('p'); subtitle.textContent='Inventory · rack · hotbar · small socket · dormant'; document.body.append(subtitle);
    const checks = [];
    for (const [id,name,kind] of [['cleave','Cleave','weaves'],['ember_bolt','Ember Bolt','weaves'],['tide_bolt','Tide Bolt','weaves'],['rallying_shout','Rallying Shout','weaves'],['fork','Fork','glyphs'],['future_support','Future support fallback','glyphs']]) {
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
    const composed = ['ember','tide'].map(thread => {
      const face = names.abilityIcons.seat('bolt[]+','bolt',null,null,[thread]);
      const row = document.createElement('div'); row.textContent = 'Composed Bolt: '+thread+' ';
      row.append(abilityArt(skin,face,40)); document.body.append(row);
      return {thread,...face};
    });
    if(composed[0].color === composed[1].color) throw new Error('composed Thread colors lost');
    const override = itemTile(skin,{uid:2,base:'memory',name:'Override',icon:'blade',rarity:'rare',count:1,level:1,lines:[],local:{},form:{id:'bolt',level:1,sockets:1}},32);
    if(override.querySelector('.ability-art')) throw new Error('item icon override ignored');
    const converted=names.abilityIcons.seat('ember_bolt','bolt','ember_bolt',{label:'Reloaded',weave:'tide_bolt',form:'bolt'});
    if(converted.art!==names.abilityIcons.resolve('weaves','tide_bolt').art) throw new Error('converted art');
    return {checks,converted,composed,itemOverride:true};
  });
  await page.screenshot({path:resolve(out,'shared-artwork.png')});
  writeFileSync(resolve(out,'report.json'),JSON.stringify(result,null,2)+'\n');
  console.log('PASS: '+result.checks.length+' browser inventory/rack/hotbar identities, composed Thread palettes, item override and converted identity.');
} finally { await browser?.close(); await server.close(); }
