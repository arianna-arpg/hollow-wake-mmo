'use strict';
// Standalone authored diagram. No game runtime, simulated authority, or network.
const THREADS = [
  {id:'ember',name:'Flame',color:'#edac69',path:'M-11 17 Q-22 3 -6 -7 Q-9 1 -2 4 Q8 -6 4 -20 Q26 1 13 16 Q3 25 -11 17Z',grain:'Rising hooks. Heat catches and climbs.'},
  {id:'tide',name:'Tide',color:'#81c3c9',path:'M-22 -8 Q-11 -20 0 -8 T22 -8 M-22 7 Q-11 -5 0 7 T22 7 M-19 20 Q-9 9 1 20',grain:'Long paired curves. A pull, then a return.'},
  {id:'storm',name:'Storm',color:'#c5b7ef',path:'M5 -23 L-13 -1 L-2 -1 L-10 22 L16 -7 L4 -7Z M-11 -1 L-21 -8 M13 -8 L23 -15',grain:'Forked angles. Direction survives without flicker.'},
  {id:'stone',name:'Stone',color:'#c5ae82',path:'M-20 15 L-20 -9 L0 -21 L20 -9 L20 15Z M-20 -9 L0 2 L20 -9 M0 2 L0 15',grain:'Stacked facets. Weight compresses into a footing.'},
  {id:'bloom',name:'Bloom',color:'#a5c58b',path:'M0 22 L0 -13 M0 2 Q-23 4 -19 -14 Q-2 -15 0 2 M0 -5 Q19 -24 22 -5 Q13 7 0 8',grain:'Paired leaves. A branching spine unfurls.'},
  {id:'umbra',name:'Umbra',color:'#c1a1c8',path:'M-20 -19 Q-3 -7 20 -19 L15 5 Q4 22 -12 17 Q-22 5 -20 -19Z M-13 -2 Q0 7 14 -2 M-9 8 Q0 14 9 8',grain:'A soft folded veil. The grain settles downward.'},
  {id:'lumen',name:'Lumen',color:'#eee0a3',path:'M0 -23 L20 0 L0 23 L-20 0Z M0 -14 L12 0 L0 14 L-12 0Z M-25 0 L25 0 M0 -25 L0 25',grain:'Straight rays and nested facets. Steady alignment.'},
  {id:'void',name:'Void',color:'#bcc5d5',path:'M-7 -21 L-20 -21 L-20 21 L-7 21 M7 -21 L20 -21 L20 21 L7 21',grain:'Separated brackets around absence. No purple smoke.'},
  {id:'mind',name:'Mind',color:'#d4dcd8',path:'M-22 0 C-22 -19 0 -19 0 0 C0 19 22 19 22 0 C22 -19 0 -19 0 0 C0 19 -22 19 -22 0 M-4 -23 L4 -23',grain:'A returning double loop. Pattern recognises itself.'}
];
const PHASES = ['Anticipation','Release','Impact','Recovery'];
const COPIES = [
  'Reach / Draw: weight settles; Thread gathers from shoulders to hands. A filled hand-knot means readiness only when the server confirms it.',
  'Cast: one narrow directional stroke leaves the hand. A release is not proof of a hit. Instant casts may skip anticipation.',
  'Contact: a tight burst belongs at the confirmed target. Block, evade and immunity require different contact signs.',
  'Fall: the hand opens and the trace breaks. This decorative tail is separate from any server-enforced recovery lock.'
];
const TERRITORY = [
  'Unbound: open ties, irregular growth and an unsmoothed approach. The Fount remains a place of possibility, not an enemy.',
  'Vigil: a partial joining strand and a distinct segmented progress mark. The rite is still in progress; the land has not changed owner.',
  'Bound: the tie closes at the Fount. Tended beds and a repaired threshold show care; the owner emblem is a separate identity cue.',
  'Contested: the existing owner emblem remains while a second broken strand approaches. A split-knot badge marks the challenge; crops do not instantly die.'
];
const $ = id => document.getElementById(id);
const combat = $('combat'), territory = $('territory');
const ctx = combat.getContext('2d'), land = territory.getContext('2d');
let state = {thread:0,phase:0,density:'solo',ground:'dark',territory:0,reduced:matchMedia('(prefers-reduced-motion: reduce)').matches,playing:false};
let animation = 0, lastStep = 0;
function line(c,points,color,width=2,close=false,fill=null){c.beginPath();points.forEach((p,i)=>i?c.lineTo(...p):c.moveTo(...p));if(close)c.closePath();if(fill){c.fillStyle=fill;c.fill();}if(color){c.strokeStyle=color;c.lineWidth=width;c.stroke();}}
function ellipse(c,x,y,rx,ry,fill,stroke=null,width=2){c.beginPath();c.ellipse(x,y,rx,ry,0,0,Math.PI*2);if(fill){c.fillStyle=fill;c.fill();}if(stroke){c.strokeStyle=stroke;c.lineWidth=width;c.stroke();}}
function text(c,s,x,y,size=13,color='#c4d0c7',align='left'){c.font=`${size}px system-ui, sans-serif`;c.fillStyle=color;c.textAlign=align;c.fillText(s,x,y);}
function glyph(c,t,x,y,size=20,alpha=1){c.save();c.translate(x,y);c.scale(size/24,size/24);c.globalAlpha=alpha;c.lineJoin='round';c.lineCap='round';const p=new Path2D(t.path);c.strokeStyle='#142025';c.lineWidth=7;c.stroke(p);c.strokeStyle=t.color;c.lineWidth=3;c.stroke(p);c.restore();}
function stroke(c,draw,color,width=3,alpha=1){c.save();c.globalAlpha=alpha;c.lineCap='round';c.lineJoin='round';c.beginPath();draw(c);c.strokeStyle='#132025';c.lineWidth=width+4;c.stroke();c.strokeStyle=color;c.lineWidth=width;c.stroke();c.restore();}
function actor(c,x,y,scale=1,role='hero',opacity=1,pose=0){
  c.save();c.translate(x,y);c.scale(scale,scale);c.globalAlpha=opacity;
  ellipse(c,0,3,35,12,'#07111466');
  const cloak=role==='hero'?'#263d42':role==='ally'?'#5e746d':'#73665c';
  line(c,[[-12,-34],[-19,-5],[-6,-3],[2,-30]],'#152326',3,true,'#1c292b');
  line(c,[[6,-37],[18,-3],[32,-4],[21,-40]],'#152326',3,true,'#263337');
  line(c,[[-11,-90],[18,-87],[34,-35],[20,-22],[-36,-27],[-26,-59]],'#101c20',3,true,cloak);
  line(c,[[-8,-79],[-19,-34],[4,-42],[18,-85]],'#85968a',1.5);
  ellipse(c,3,-98,13,16,'#bda989','#18282c',3);
  line(c,[[-11,-103],[-6,-118],[13,-115],[20,-104],[11,-108],[0,-103]],'#142126',3,true,'#16292d');
  ellipse(c,19,-81,4,4,'#ccaa73');
  let hand=pose===3?[35,-43]:pose===0?[49,-65]:[61,-64];
  line(c,[[16,-80],[29,-65],hand],'#122025',15);
  line(c,[[16,-80],[29,-65],hand],'#c7c6ad',10);
  ellipse(c,hand[0],hand[1],5,5,'#d3b58d','#17262a',1);
  if(role==='target'){line(c,[[27,-80],[54,-69],[47,-37],[29,-43]],'#d8d5bc',2,true,'#485654');line(c,[[32,-68],[46,-62],[41,-49]],'#d8d5bc',2);}
  c.restore();
}
function boundary(c,x,y,rx,ry,kind,light){
  // Decoration cannot change the fixed footprint. Teeth/squares encode meaning.
  const ink=light?'#263b40':'#e4ddc6';
  ellipse(c,x,y,rx,ry,null,light?'#e9e6d0':'#0a171b',7);
  ellipse(c,x,y,rx,ry,null,ink,2.2);
  for(let i=0;i<14;i++){
    const a=i/14*Math.PI*2,xx=x+Math.cos(a)*rx,yy=y+Math.sin(a)*ry;
    if(kind==='hostile'){
      const tx=-Math.sin(a)*6,ty=Math.cos(a)*4;
      line(c,[[xx+tx,yy+ty],[x+Math.cos(a)*(rx-12),y+Math.sin(a)*(ry-10)],[xx-tx,yy-ty]],ink,2,true,light?'#c3b69a':'#25383b');
    }else{c.fillStyle=light?'#d9ddc7':'#182b2e';c.strokeStyle=ink;c.lineWidth=2;c.fillRect(xx-4,yy-4,8,8);c.strokeRect(xx-4,yy-4,8,8);}
  }
}
function base(c,w,h,light=false){
  c.fillStyle=light?'#b5b6a1':'#17282c';c.fillRect(0,0,w,h);
  const colors=light?['#afb19b','#bdbeaa','#b6baa4']:['#1e3437','#21383b','#243b3b'];
  for(let r=-2;r<9;r++)for(let col=-2;col<13;col++){
    const x=col*115+r*57,y=130+r*58;
    line(c,[[x,y],[x+100,y-37],[x+199,y],[x+100,y+37]],light?'#919a8970':'#43544a45',1,true,colors[((r+col)%3+3)%3]);
  }
  c.fillStyle=light?'#b5b6a1':'#17282c';c.fillRect(0,0,w,86);
}
function drawCombat(){
  const t=THREADS[state.thread],light=state.ground==='light';base(ctx,1120,660,light);
  const ink=light?'#263c40':'#d3dbca';
  text(ctx,'THE VISIBLE WEAVE',35,37,12,ink);text(ctx,`${t.name.toUpperCase()} / ${PHASES[state.phase].toUpperCase()}`,1085,37,12,ink,'right');
  text(ctx,state.density==='crowd'?'Crowd: preserve critical boundaries; quiet the ornament.':'A body, a direction, a contact.',35,66,13,ink);
  // Quiet framing elements, never high-value combat detail.
  for(let i=0;i<7;i++){const x=80+i*156,y=170+(i%2)*25;line(ctx,[[x-18,y],[x-18,y-30],[x+12,y-42],[x+29,y-13],[x+18,y+6]],light?'#7b8e79':'#50675b',1,true,light?'#98a38c':'#2c4844');}
  boundary(ctx,240,329,85,38,'support',light);
  boundary(ctx,800,306,140,60,'hostile',light);
  if(state.density==='crowd'){
    for(let i=0;i<20;i++){
      const x=90+(i%7)*146,y=208+Math.floor(i/7)*124;
      if((x>290&&x<500&&y>300)||(x>690&&x<870&&y>240&&y<350))continue;
      actor(ctx,x,y,.52,i%3?'ally':'target',.64);
      // Reduced decoration, but all three critical footprints remain drawn.
    }
    boundary(ctx,589,466,119,52,'hostile',light);
    text(ctx,'second danger / same teeth',590,550,13,ink,'center');
  }
  text(ctx,'support / spaced squares',238,403,13,ink,'center');
  text(ctx,'hostile / inward teeth',800,400,13,ink,'center');
  // Preparation behind the body, then in front at the hands.
  if(state.phase===0){
    stroke(ctx,c=>{c.moveTo(323,305);c.bezierCurveTo(288,229,412,250,439,329);c.moveTo(349,286);c.bezierCurveTo(373,231,447,266,444,328);},t.color,3);
    glyph(ctx,t,321,285,15);glyph(ctx,t,402,267,13);
  }
  actor(ctx,390,397,1,'hero',1,state.phase);actor(ctx,790,306,.9,'target');
  if(state.phase===0){
    stroke(ctx,c=>{c.moveTo(373,323);c.quadraticCurveTo(402,343,439,332);c.moveTo(395,312);c.quadraticCurveTo(411,311,439,332);},t.color,3);
    glyph(ctx,t,449,331,22);
    line(ctx,[[454,350],[499,409],[530,409]],ink,1);text(ctx,'hand-knot / readiness',540,414,13,ink);
  }
  if(state.phase===1){
    stroke(ctx,c=>{c.moveTo(458,330);c.quadraticCurveTo(549,306,682,263);},t.color,5);
    glyph(ctx,t,682,263,24);line(ctx,[[673,278],[707,254],[665,255]],t.color,2);
    text(ctx,'a narrow path, not a ground-area promise',570,439,13,ink,'center');
  }
  if(state.phase===2){
    const x=821,y=252;
    stroke(ctx,c=>{for(let i=0;i<7;i++){const a=i/7*Math.PI*2;c.moveTo(x+Math.cos(a)*25,y+Math.sin(a)*25);c.lineTo(x+Math.cos(a)*44,y+Math.sin(a)*44);}},t.color,3);
    glyph(ctx,t,x,y,25);ellipse(ctx,x,y,4,4,'#faf0c7','#253438',2);
    text(ctx,'confirmed contact / bounded bloom',800,451,13,ink,'center');
  }
  if(state.phase===3){
    stroke(ctx,c=>{c.moveTo(431,354);c.quadraticCurveTo(461,366,481,352);c.moveTo(494,346);c.lineTo(506,339);},t.color,2,.65);
    glyph(ctx,t,467,366,10,.5);
    text(ctx,'open trace / no lingering threat fill',480,446,13,ink,'center');
  }
  text(ctx,'YOU',389,446,11,ink,'center');
  ctx.fillStyle=light?'#d5d7be':'#102125';ctx.fillRect(30,590,1060,47);
  glyph(ctx,t,59,613,14);text(ctx,t.grain,88,619,14,ink);
  text(ctx,state.reduced?'STILL CUES / MOTION REDUCED':'ILLUSTRATIVE PHASE / NO GAME CLOCK',1060,619,11,ink,'right');
  combat.setAttribute('aria-label',`${t.name} ${PHASES[state.phase]}. ${t.grain} ${COPIES[state.phase]} ${state.density==='crowd'?'Crowded fight with two hostile boundaries and one support boundary.':'One hostile boundary and one support boundary.'}`);
}
function drawTerritory(){
  const c=land,phase=state.territory,ink='#d7dfcb';base(c,1120,460,false);
  text(c,'FOUNT / THE SAME PLACE, CHANGED BY STATE',35,38,12,ink);
  text(c,['UNBOUND','VIGIL','BOUND','CONTESTED'][phase],1080,38,12,ink,'right');
  // Quiet inspect contour; the footprint is not shifted by decorative growth.
  c.setLineDash([10,15]);ellipse(c,560,283,440,132,null,'#798d7888',1.5);c.setLineDash([]);
  // Tended crop order is an environmental consequence, never an owner icon.
  for(let row=0;row<3;row++)for(let col=0;col<6;col++){
    const orderly=phase>=2,xx=690+col*29+row*18,yy=268+row*37+(orderly?0:Math.sin(col*4+row)*19);
    if(orderly)line(c,[[xx-15,yy+12],[xx+9,yy+6]],'#a09170',5);
    line(c,[[xx,yy+7],[xx,yy-15]],'#92af7e',3);line(c,[[xx,yy-2],[xx-8,yy-9],[xx,yy],[xx+9,yy-7]],'#92af7e',3);
  }
  // Path and woven-up threshold.
  line(c,[[243,407],[302,375],[404,329],[453,309]],'#8c927c',25);
  for(let i=0;i<5;i++){const x=315+i*26,y=380-i*13;line(c,[[x-14,y],[x+5,y-9],[x+20,y],[x,y+10]],'#4e6159',1,true,phase>=2?'#a9ac93':'#768578');}
  if(phase>=2)line(c,[[314,355],[314,316],[344,304],[359,313],[359,335]],'#bbc0a2',4);
  ellipse(c,502,273,112,55,'#456b68','#a5b0a0',8);ellipse(c,502,271,88,39,'#314f52','#98b9a7',2);
  for(let i=0;i<3;i++){
    const x=430+i*72,y=263+(i%2)*20;
    line(c,[[x-14,y],[x-15,y-78],[x,y-100],[x+13,y-80],[x+13,y]],'#b2b5a0',2,true,'#7d9288');
    line(c,[[x,y-76],[x-5,y-51],[x+5,y-31],[x,y-11]],'#e2dcb6',2);
  }
  const tie='#d5ca9a';
  if(phase===0){stroke(c,k=>{k.moveTo(506,218);k.quadraticCurveTo(557,230,580,206);k.moveTo(600,201);k.lineTo(611,195);},tie,2);}
  if(phase===1){stroke(c,k=>{k.moveTo(348,317);k.quadraticCurveTo(405,263,485,231);},tie,3);for(let i=0;i<6;i++){c.fillStyle=i<3?'#e3ce93':'#415e5a';c.fillRect(419+i*28,337,20,7);}}
  if(phase>=2){
    stroke(c,k=>{k.moveTo(343,319);k.quadraticCurveTo(419,292,502,229);k.quadraticCurveTo(601,246,733,270);},tie,3);
    // A distinct placeholder owner seal, never one of the nine Thread marks.
    line(c,[[487,212],[517,212],[515,233],[502,245],[489,233]],'#d5ca9a',3,true,'#233b3e');
    line(c,[[496,219],[496,232],[508,232],[508,219]],'#e6dfbd',2);
  }
  if(phase===3){stroke(c,k=>{k.moveTo(891,187);k.quadraticCurveTo(736,146,586,204);k.moveTo(560,214);k.lineTo(540,219);},'#c9b4cd',3);line(c,[[795,154],[810,165],[824,154]],ink,3);line(c,[[795,183],[810,171],[824,183]],ink,3);text(c,'split knot / challenge',877,132,12,ink,'center');}
  actor(c,330,337,.55,'hero',1,phase===1?0:3);
  text(c,phase>=2?'owner / closed tie':'no owner seal',500,394,13,ink,'center');
  text(c,phase>=2?'tended beds':'irregular growth',816,395,13,ink,'center');
  text(c,'inspectable Reach contour',180,440,12,'#a7b7a7');
  territory.setAttribute('aria-label',TERRITORY[phase]);
}
function refresh(){
  drawCombat();drawTerritory();
  document.querySelectorAll('[data-phase]').forEach(b=>b.setAttribute('aria-pressed',String(+b.dataset.phase===state.phase)));
  document.querySelectorAll('[data-territory]').forEach(b=>b.setAttribute('aria-pressed',String(+b.dataset.territory===state.territory)));
  const t=THREADS[state.thread];$('thread-note').textContent=t.grain;
  $('thread-symbol').innerHTML=`<path d="${t.path}" fill="none" stroke="${t.color}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>`;
  $('phase-copy').textContent=COPIES[state.phase];$('territory-copy').textContent=TERRITORY[state.territory];
  $('play').textContent=state.reduced?'Step through the phases above':state.playing?'Pause sequence':'Play sequence';$('play').disabled=state.reduced;
}
function stop(){state.playing=false;cancelAnimationFrame(animation);}
function tick(now){if(!state.playing)return;if(now-lastStep>=1500){lastStep=now;state.phase=(state.phase+1)%4;refresh();}animation=requestAnimationFrame(tick);}
THREADS.forEach((t,i)=>{const o=document.createElement('option');o.value=i;o.textContent=t.name;$('thread').append(o);});
$('reduced').checked=state.reduced;
$('thread').addEventListener('change',e=>{state.thread=+e.target.value;refresh();});
['density','ground'].forEach(id=>$(id).addEventListener('change',e=>{state[id]=e.target.value;refresh();}));
$('gray').addEventListener('change',e=>document.body.classList.toggle('grayscale',e.target.checked));
$('reduced').addEventListener('change',e=>{state.reduced=e.target.checked;stop();refresh();});
document.querySelectorAll('[data-phase]').forEach(b=>b.addEventListener('click',()=>{stop();state.phase=+b.dataset.phase;refresh();}));
document.querySelectorAll('[data-territory]').forEach(b=>b.addEventListener('click',()=>{state.territory=+b.dataset.territory;refresh();}));
$('play').addEventListener('click',()=>{if(state.playing)stop();else{state.playing=true;lastStep=performance.now();animation=requestAnimationFrame(tick);}refresh();});
document.addEventListener('visibilitychange',()=>{if(document.hidden){stop();refresh();}});
matchMedia('(prefers-reduced-motion: reduce)').addEventListener('change',e=>{state.reduced=e.matches;$('reduced').checked=e.matches;stop();refresh();});
refresh();
