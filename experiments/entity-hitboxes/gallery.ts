import {Scene,WebGLRenderer,OrthographicCamera,Color} from 'three';
import {bundledKinds} from '../../web/src/ui/logic/content';
import {AppearanceCatalog} from '../../web/src/world/appearance/looks';
import {buildBody} from '../../web/src/world/appearance/bodyFamilies';
import {BodyMesh} from '../../web/src/world/appearance/bodyMesh';
import {wear} from '../../web/src/world/appearance/gearFamilies';
import {HitBodyDebug,hitBodyLines} from '../../web/src/world/hitBodies';
import type {EntityState,HitBodyInfo} from '../../web/src/net/protocol';
import profiles from '../../content/core/entities/hit-bodies.json';
import goblins from '../../content/core/entities/patterns-goblinkin.json';
import orcs from '../../content/core/entities/patterns-orcs.json';
import gnashers from '../../content/core/entities/patterns-gnashers.json';
import undead from '../../content/core/entities/patterns-undead.json';
const ids=['holdfast','skeleton_warrior','goblin_skirmisher','orc_grunt','orc_warlord','gnasher_whelp','great_gnasher','saddle_gnasher','warg'];
const patterns=[...goblins.entries,...orcs.entries,...gnashers.entries,...undead.entries];
const catalog=new AppearanceCatalog(bundledKinds());
const hits=new Map(profiles.entries.map(p=>[p.id,p as HitBodyInfo]));
const renderer=new WebGLRenderer({antialias:true,preserveDrawingBuffer:true});renderer.setPixelRatio(1);
const records=ids.map((id,i)=>{
 const p=patterns.find(p=>p.id===id),height=p?.height??1.8,radius=p?.radius??.4;
 const look=catalog.byIdOf(id)!,plan=buildBody(look,17,height,radius);
 for(const [slot,item] of catalog.defaultGear(look,id)){const gear=catalog.gearFor(slot,item.itemClass,item.tags);if(gear)wear(plan,gear,slot,17);}
 const body=new BodyMesh(plan),scene=new Scene();scene.background=new Color('#1c2730');scene.add(body.root);
 const state={x:0,y:0,z:0,yaw:0,radius,height,hit_body:id==='holdfast'?'humanoid':id} as EntityState;
 const debug=new HitBodyDebug();scene.add(debug.group);debug.update(state,hits);
 const article=document.createElement('article'),canvas=document.createElement('canvas');canvas.width=440;canvas.height=300;article.append(canvas);
 const h=document.createElement('h2');h.textContent=`${id.replaceAll('_',' ')} · ${height} m`;article.append(h);document.querySelector('#grid')!.append(article);
 return{id,body,scene,debug,state,canvas,plan};
});
let turn=0;
function draw(){
 const line=new Scene();line.background=new Color('#151c23');
 for(const [i,r] of records.entries()){
  r.body.root.position.set(0,0,0);r.body.root.rotation.y=turn;r.scene.add(r.body.root);r.state.x=0;r.state.yaw=turn;r.debug.update(r.state,hits);r.scene.add(r.debug.group);
  const h=Math.max(.6,r.state.height*1.4),cam=new OrthographicCamera(-h*.73,h*.73,h*.5,-h*.5,.01,100);
  cam.position.set(4,1.7,5);cam.lookAt(0,r.state.height*.5,0);renderer.setSize(440,300);renderer.render(r.scene,cam);r.canvas.getContext('2d')!.drawImage(renderer.domElement,0,0);
  r.body.root.position.set(-9+i*2.25,0,0);r.body.root.rotation.y=turn;line.add(r.body.root);
  r.state.x=-9+i*2.25;r.debug.update(r.state,hits);line.add(r.debug.group);
 }
 const cam=new OrthographicCamera(-10.5,10.5,2.3,-2.3,.01,100);cam.position.set(0,3,30);cam.lookAt(0,1.5,0);
 renderer.setSize(1400,307);renderer.render(line,cam);const canvas=document.querySelector<HTMLCanvasElement>('#lineup')!;canvas.width=1400;canvas.height=307;canvas.getContext('2d')!.drawImage(renderer.domElement,0,0);
 document.body.dataset.ready='true';
}
document.querySelector('#turn')!.addEventListener('click',()=>{turn+=Math.PI/2;draw();});
(window as any).hitFixtures=records.filter(r=>['gnasher_whelp','orc_grunt','saddle_gnasher'].includes(r.id)).map(r=>({id:r.id,radius:r.state.radius,height:r.state.height,body:hits.get(r.id),lines:hitBodyLines(hits.get(r.id)!,r.state.radius,r.state.height,16).map(p=>p.map(n=>Math.round(n*1e6)/1e6))}));
draw();
