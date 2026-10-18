// Authoring only: explicit rest-anatomy recipes reviewed against shared model fixtures.
// No cosmetic bounds, equipment, hats, ears, tusks, banners or saddles are imported.
import fs from 'node:fs';
const profiles=[],patches=[],round=n=>Math.round(n*100000)/100000;
function anatomy(id,H,R,W,D,family) {
  const volumes=[];
  function volume(x,z,y,r,h,shape='ellipsoid') {
    volumes.push({shape,forward:round(x/R),left:round(-z/R),up:round(y/H),radius:round(r/R),half_height:round(h/H)});
  }
  function oval(x,z,y,d,w,h) {
    const along=d>=w,r=Math.min(d,w)/2,major=Math.max(d,w);
    volume(x,z,y,r,h/2);
    const offset=Math.max(0,major/2-r*.65);
    if(major/(r*2)>1.12) for(const side of [-1,1]) volume(x+(along?offset*side:0),z+(along?0:offset*side),y,r*.65,h*.40);
  }
  if(family==='gnasher'||family==='warbeast') {
    oval(-D*.06,0,H*.60,D*.8,W*.95,H*.7);
    oval(D*.27,0,H*.67,D*(family==='warbeast'?.5:.4),W*(family==='warbeast'?.58:.8),H*(family==='warbeast'?.35:.5));
    for(const side of [-1,1]) {
      volume(-D*.22,side*W*.32,H*.23,W*.1,H*.2,'cylinder');
      if(family==='warbeast') volume(D*.2,side*W*.3,H*.26,W*.09,H*.25,'cylinder');
    }
  } else if(family==='orc') {
    oval(-D*.07,0,H*.60,D*.85,W*.73,H*.49);
    oval(-D*.16,0,H*.77,D*.75,W*.68,H*.30);
    oval(D*.176,0,H*.89,D*.48,W*.38,H*.22);
    oval(0,0,H*.35,D*.74,W*.60,H*.20);
    for(const side of [-1,1]) {
      volume(0,side*W*.4,H*.59,Math.min(D*.2,W*.115),H*.19);
      volume(0,side*W*.2,H*.18,W*.095,H*.17,'cylinder');
    }
  } else if(family==='goblinkin') {
    oval(-D*.08,0,H*.53,D*.72,W*.65,H*.34);
    oval(D*.10,0,H*.886,H*.198,H*.264,H*.22);
    oval(0,0,H*.36,D*.65,W*.58,H*.16);
    for(const side of [-1,1]) {
      volume(0,side*W*.39,H*.55,Math.min(D*.19,W*.105),H*.17);
      volume(0,side*W*.2,H*.18,W*.09,H*.17,'cylinder');
    }
  } else {
    oval(0,0,H*.61,D*.70,W*.58,H*.34);
    volume(0,0,H*.865,H*.075,H*.10);
    oval(0,0,H*.43,D*.67,W*.51,H*.15);
    for(const side of [-1,1]) {
      volume(0,side*W*.38,H*.57,W*.09,H*.18);
      volume(0,side*W*.17,H*.22,W*.075,H*.21,'cylinder');
    }
  }
  profiles.push({id,aim:0,volumes});
}
anatomy('humanoid',1.8,.4,.65,.42,'humanoid');
for(const file of ['patterns-goblinkin.json','patterns-orcs.json','patterns-gnashers.json','patterns-undead.json']) {
  for(const p of JSON.parse(fs.readFileSync(`content/core/entities/${file}`,'utf8')).entries) {
    const physical=p.components?.physical,tags=p.tags??[];
    if(!physical&&!tags.includes('family.skeleton'))continue;
    let family=tags.includes('family.orc')?'orc':tags.some(t=>t.includes('gnasher'))?'gnasher':physical?'goblinkin':'humanoid';
    if(tags.includes('family.warg'))family='warbeast';
    anatomy(p.id,p.height??1.8,p.radius??.4,physical?.width??.65,physical?.depth??.42,family);
    patches.push({id:p.id,patch:true,components:{hit_body:{profile:p.id}}});
  }
}
fs.writeFileSync('content/core/entities/hit-bodies.json',JSON.stringify({kind:'hit_bodies',entries:profiles},null,2)+'\n');
fs.writeFileSync('content/core/entities/patterns-hit-bodies.json',JSON.stringify({kind:'patterns',entries:patches},null,2)+'\n');
console.log(`Authored ${profiles.length} profiles and ${patches.length} bindings.`);
