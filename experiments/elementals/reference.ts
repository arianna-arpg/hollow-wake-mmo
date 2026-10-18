import { bodySprite, drawLiveParts, lookOf } from 'prototype/render/vis/body';
import { MONSTERS } from 'prototype/data/monsters';
const rows = [
['Ember Elemental','ember_elemental'],
['Gale Elemental','gale_elemental'],
['Frost Elemental','frost_elemental'],
['Cinderling','cinderling'],
['Emberwisp','emberwisp'],
['Geode Shellback','geode_shellback'],
['Lumen Wisp','lumen_wisp'],
['Triune Elemental','triune_elemental'],
];
for(const [name,id] of rows) {
 const article=document.createElement('article'); const canvas=document.createElement('canvas');canvas.width=480;canvas.height=440;
 const ctx=canvas.getContext('2d')!;ctx.translate(240,220);ctx.scale(4.5,4.5);
 const source=MONSTERS[id];
 const look={look:source.look,shape:source.shape,color:source.color,material:source.material,radius:source.radius};const sprite=bodySprite(look);
 ctx.drawImage(sprite,-sprite.width/2,-sprite.height/2);const def=lookOf(source.look);if(def)drawLiveParts(ctx,look as never,def,1.2);
 article.append(canvas);const p=document.createElement('p');p.textContent=String(name);article.append(p);document.querySelector('main')!.append(article);
}
document.body.dataset.ready='true';
