import { Scene, WebGLRenderer, OrthographicCamera, Color, Mesh, CircleGeometry, MeshBasicMaterial, SRGBColorSpace } from 'three';
import { bundledKinds } from '../../web/src/ui/logic/content';
import { AppearanceCatalog } from '../../web/src/world/appearance/looks';
import { buildBody } from '../../web/src/world/appearance/bodyFamilies';
import { BodyMesh } from '../../web/src/world/appearance/bodyMesh';
import { wear } from '../../web/src/world/appearance/gearFamilies';
import { animate, Pose, freshVisualState } from '../../web/src/world/appearance/animator';
import golems from '../../content/core/entities/patterns-golems.json';

import goblins from '../../content/core/entities/patterns-goblinkin.json';
import undead from '../../content/core/entities/patterns-undead.json';
const roster = golems;

import baseMaterials from '../../content/core/entities/body-materials.json';
import golemMaterials from '../../content/core/entities/body-materials-golems.json';
const materials = {entries:[...baseMaterials.entries,...golemMaterials.entries]};
const catalog = new AppearanceCatalog(bundledKinds());
const host = document.querySelector('#grid')!;
const renderer = new WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(1.5); renderer.setSize(360, 220); renderer.outputColorSpace = SRGBColorSpace;
const records = roster.entries.filter(e => e.id !== 'golem_body').map((e, index) => {
  const def = catalog.byIdOf(e.id)!;
  const plan = buildBody(def, index + 17, e.height!, e.radius!);
  for (const [slot, item] of catalog.defaultGear(def, e.id)) {
    const gear = catalog.gearFor(slot, item.itemClass, item.tags);
    if (gear) wear(plan, gear, slot, index + 17);
  }
  const body = new BodyMesh(plan);
  const scene = new Scene(); scene.background = new Color('#192027'); scene.add(body.root);
  const disc = new Mesh(new CircleGeometry(.8, 48), new MeshBasicMaterial({color:'#282e31'}));
  disc.rotation.x = -Math.PI / 2; disc.position.y = -.025; scene.add(disc);
  const article = document.createElement('article');
  const canvas = document.createElement('canvas'); canvas.width = 540; canvas.height = 330;
  article.append(canvas);
  const heading = document.createElement('h2'); heading.textContent = e.name; article.append(heading);
  const tags = document.createElement('div'); tags.className = 'tags'; tags.textContent = e.tags.filter(t => t.startsWith('body.') || t.startsWith('role.')).map(t => t.split('.')[1]).join(' / ').toUpperCase(); article.append(tags);
  const kit = document.createElement('p'); kit.textContent = e.components.weaves!.list.join(' / ').replaceAll('_',' '); article.append(kit);
  const habitat = document.createElement('p'); habitat.textContent = e.tags.filter(t => t.startsWith('habitat.')).map(t => t.slice(8).replaceAll('_',' ')).join(' / '); article.append(habitat);
  const physical = e.components.physical!;
  const density = physical.materials.reduce((sum, m) => sum + m.fraction * materials.entries.find(d => d.id === m.material)!.density, 0);
  const mass = physical.width * physical.depth * physical.height * physical.fill * physical.heft * density;
  const measure = document.createElement('p'); measure.textContent = `${physical.width}w x ${physical.depth}d x ${physical.height}h / ${mass.toFixed(1)} kg`; article.append(measure);
  host.append(article);
  return { e, plan, body, scene, canvas, index };
});
// Explicit fixture regeneration captures the same plans displayed in the visual evidence.
(window as unknown as {golemPlans:unknown}).golemPlans = records.map(({e,plan,index}) => ({
  id:e.id,key:index+17,height:e.height,radius:e.radius,
  plan:{family:plan.family,rig:plan.rig,height:plan.height,width:plan.width,hover:plan.hover,swings:plan.swings,
    bones:plan.bones,parts:plan.parts,sockets:Object.fromEntries(plan.sockets)}
}));
// All bodies in one orthographic scene use a single world scale.
const lineup = new Scene(); lineup.background = new Color('#192027');
const referenceIds = ['holdfast','skeleton_warrior','goblin_skirmisher',...roster.entries.filter(e=>e.id!=='golem_body').map(e=>e.id)];
const refBodies = referenceIds.map((id,i) => {
  const e = [...roster.entries,...goblins.entries,...undead.entries].find(e=>e.id===id);
  const def=catalog.byIdOf(id)!;
  const p=buildBody(def,17,e?.height ?? 1.8,e?.radius ?? .4);
  for(const [slot,item] of catalog.defaultGear(def,id)) { const gear=catalog.gearFor(slot,item.itemClass,item.tags); if(gear)wear(p,gear,slot,17); }
  const b=new BodyMesh(p);b.root.position.set(-11+i*2,0,0);b.root.rotation.y=-Math.PI/2;lineup.add(b.root);
  return {id,authoritativeHeight:e?.height ?? 1.8,modelHeight:p.height};
});
(window as unknown as {golemScale:unknown}).golemScale=refBodies;
const lineupCanvas = document.querySelector<HTMLCanvasElement>('#lineup')!;
const lineCamera = new OrthographicCamera(-12.4,12.4,3.27,-3.27,.01,100);
lineCamera.position.set(0,3.4,30); lineCamera.lookAt(0,1.35,0);
renderer.setSize(1400,369); renderer.render(lineup,lineCamera);
lineupCanvas.width=2100; lineupCanvas.height=554;
lineupCanvas.getContext('2d')!.drawImage(renderer.domElement,0,0,2100,554);
renderer.setSize(360,220);
let third = false, walk = false;
function draw() {
  const report = [];
  for (const { e, plan, body, scene, canvas, index } of records) {
    const state = {...freshVisualState(), rig:plan.rig, height:plan.height, hover:plan.hover, swings:plan.swings, key:index+17, reducedMotion:true, time:1, speed:walk?3:0, walkPhase:.8};
    const pose = new Pose(); animate(state,catalog.motion,pose); body.apply(pose,0);
    const viewHeight = Math.max(2.5, plan.height * 1.35);
    const aspect = 360/220;
    const cam = new OrthographicCamera(-viewHeight*aspect/2,viewHeight*aspect/2,viewHeight/2,-viewHeight/2,.01,100);
    cam.position.set(6,third?2.2:7,8); cam.lookAt(0,plan.height*.44,0); cam.updateProjectionMatrix();
    renderer.render(scene,cam);
    canvas.getContext('2d')!.drawImage(renderer.domElement,0,0,canvas.width,canvas.height);
    report.push({id:e.id,body:plan.family,rig:plan.rig,parts:plan.parts.length,triangles:body.triangles,height:plan.height});
  }
  (window as unknown as {golemReport:unknown}).golemReport = report;
  document.body.dataset.ready='true';
}
document.querySelector('#iso')!.addEventListener('click',()=>{third=false;document.querySelector('#iso')!.setAttribute('aria-pressed','true');document.querySelector('#third')!.setAttribute('aria-pressed','false');draw();});
document.querySelector('#third')!.addEventListener('click',()=>{third=true;document.querySelector('#iso')!.setAttribute('aria-pressed','false');document.querySelector('#third')!.setAttribute('aria-pressed','true');draw();});
document.querySelector('#mono')!.addEventListener('click',()=>{const on=document.body.classList.toggle('mono');document.querySelector('#mono')!.setAttribute('aria-pressed',String(on));});
document.querySelector('#walk')!.addEventListener('click',()=>{walk=!walk;document.querySelector('#walk')!.setAttribute('aria-pressed',String(walk));draw();});
draw();
