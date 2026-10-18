import type { BodyBuild } from './bodyFamilies';
import { v3 as V, ZERO, type PartShape } from './bodyPlan';

// Twin of GolemFamilies.cs. Assemblies are features and proportions, never entity IDs.
export function assembledGolem(b: BodyBuild): void {
  const p = b.plan;
  p.rig = "biped";
  const H = b.height;
  const W = H * b.s("breadth", .6);
  const D = H * b.s("depth", .4);
  const hip = H * b.s("legs", .32);
  const shoulder = H * .76;
  const reach = H * .36 * b.s("arms", 1);
  const head = H * .18 * b.s("head", 1);
  const fist = b.s("fists", 1);
  const body = b.c("body", "#8e968b");
  const edge = b.c("edge", "#d5c8a4");
  const joint = b.c("joint", "#302f2d");
  const metal = b.c("metal", "#645d4d");
  const core = b.c("core", "#e9dba2");
  const round = b.has("round") || b.has("vessel");
  const logs = b.has("logs");
  const prisms = b.has("prisms");
  const limb: PartShape = round ? "ellipsoid" : logs ? "cylinder" : prisms ? "cone" : "box";
  p.bone("hips", "root", V(0, hip, 0));
  p.bone("torso", "hips", V(0, hip, 0));
  p.bone("head", "torso", V(D * .1, H - head, 0));
  p.add("hips", "box", V(D * .6, H * .09, W * .52), V(0, hip, 0), joint);
  if (round) {
    p.add("torso", "ellipsoid", V(D * .94, H * .46, W * .76), V(0, H * .58, 0), body, {segments:8});
    p.add("torso", "cylinder", V(D * .62, H * .10, W * .5), V(0, H * .8, 0), edge, {segments:8});
  } else if (logs) {
    for (let i = -1; i <= 1; i++)
      p.add("torso", "cylinder", V(D * .7, H * .43, W * .24), V(0, H * .64, i * W * .2), body, {segments:7});
  } else if (prisms) {
    p.add("torso", "cone", V(D * .95, H * .5, W * .72), V(0, H * .64, 0), body, {segments:5});
    p.add("torso", "cone", V(D * .85, H * .27, W * .66), V(0, H * .43, 0), edge, {turn:V(180,0,0),segments:5});
  } else {
    p.add("torso", "wedge", V(D * .88, H * .36, W * .7), V(0, H * .64, 0), body);
    p.add("torso", "box", V(D * .7, H * .10, W * .5), V(0, H * .41, 0), edge);
  }
  p.add("head", prisms ? "cone" : logs ? "cylinder" : round ? "ellipsoid" : "box",
    V(D * .48, head, W * .28), V(D * .1, H - head * .5, 0), body, {segments:6});
  // Dark face recess stays readable even when emissive accents lose their colour.
  p.add("head", "box", V(D * .035, head * .22, W * .23), V(D * .35, H - head * .48, 0), joint);
  for (const side of [-1, 1]) {
    const arm = side < 0 ? "arm_l" : "arm_r";
    const leg = side < 0 ? "leg_l" : "leg_r";
    const z = side * W * .38;
    p.bone(arm, "torso", V(0, shoulder, z));
    p.bone(leg, "hips", V(0, hip, side * W * .22));
    p.add(arm, "ellipsoid", V(D * .32, H * .10, W * .18), V(0, shoulder, z), joint, {segments:6});
    p.add(arm, limb, V(D * .58, reach * .47, W * .25), V(0, shoulder - reach * .23, z), body, {segments:6});
    p.add(arm, limb, V(D * .60 * fist, reach * .44, W * .28 * fist), V(D * .09, shoulder - reach * .79, z), body, {segments:6});
    p.add(arm, "box", V(D * .20, reach * .08, W * .22), V(D * .39, shoulder - reach * .91, z), edge);
    p.add(leg, limb, V(D * .53, hip * .8, W * .26), V(0, hip * .54, side * W * .22), body, {segments:6});
    p.add(leg, "box", V(D * .68, H * .07, W * .30), V(D * .10, H * .035, side * W * .22), body);
    p.add("head", "box", V(D * .04, head * .10, W * .055), V(D * .375, H - head * .48, side * W * .07), core, {glow:true});
    if (b.has("blocks")) {
      p.add(arm, "wedge", V(D * .72, H * .14, W * .33), V(0, shoulder + H * .06, z), edge, {turn:V(side * 12,0,0)});
      p.add("torso", "box", V(D * .1, H * .17, W * .27), V(D * .46, H * .67, side * W * .18), edge);
    }
    if (b.has("plates")) {
      p.add(arm, "wedge", V(D * .7, H * .18, W * .34), V(0, shoulder, z), edge);
      p.add(leg, "box", V(D * .12, hip * .55, W * .24), V(D * .31, hip * .53, side * W * .22), edge);
    }
    if (prisms) for (let i = 0; i < 3; i++)
      p.add(arm, "cone", V(D * .29, H * (.23 + i * .035), W * .13), V(-D * .1, shoulder + H * .04, z + side * i * W * .075), edge, {turn:V(side * (18 + i * 15),0,0),segments:4});
    if (logs) {
      p.add(arm, "cylinder", V(D * .64, H * .035, W * .29), V(0, shoulder - reach * .25, z), metal, {segments:7});
      p.add(arm, "cylinder", V(D * .66, H * .035, W * .31), V(D * .09, shoulder - reach * .77, z), metal, {segments:7});
    }
    if (b.has("handles")) {
      p.add("torso", "box", V(D * .24, H * .04, W * .25), V(0, H * .76, side * W * .42), edge);
      p.add("torso", "box", V(D * .24, H * .18, W * .055), V(0, H * .68, side * W * .54), edge);
      p.add("torso", "box", V(D * .24, H * .04, W * .25), V(0, H * .60, side * W * .42), edge);
    }
    if (b.has("rivets")) for (let i = 0; i < 3; i++)
      p.add("torso", "ellipsoid", V(H * .025, H * .025, H * .025), V(D * .46, H * (.52 + i * .09), side * W * .27), edge, {segments:5});
    if (b.has("chimneys"))
      p.add("torso", "cylinder", V(H * .1, H * .32, H * .1), V(-D * .34, H * .88, side * W * .21), metal, {segments:6});
    p.sockets.set(side < 0 ? "hand_l" : "hand_r", {bone:arm,at:V(D * .10,shoulder - reach,z),turn:ZERO,size:H * .55});
  }
  if (b.has("plates")) {
    p.add("torso", "box", V(D * .14, H * .32, W * .62), V(D * .45, H * .64, 0), metal);
    for (let i = -1; i <= 1; i++)
      p.add("torso", "box", V(D * .025, H * .17, W * .055), V(D * .535, H * .64, i * W * .12), joint);
  }
  if (b.has("vessel")) {
    p.add("torso", "cylinder", V(D * .69, H * .035, W * .55), V(0, H * .855, 0), joint, {segments:8});
    p.add("torso", "cylinder", V(D * .75, H * .04, W * .61), V(0, H * .83, 0), edge, {segments:8});
  }
  if (b.has("glaze")) for (let i = 0; i < 3; i++)
    p.add("torso", "box", V(D * .025, H * (.07 + i * .025), W * .1), V(D * .44, H * .66, (i - 1) * W * .17), edge);
  if (b.has("thumbprints")) for (let i = -1; i <= 1; i++)
    p.add("torso", "ellipsoid", V(D * .035, H * .06, W * .09), V(D * .45, H * .58, i * W * .16), joint, {segments:5});
  if (b.has("runestones")) for (let i = -1; i <= 1; i++)
    p.add("torso", "box", V(D * .03, H * .11, W * .025), V(D * .53, H * .67, i * W * .15), core, {glow:true});
  if (b.has("crest"))
    p.add("head", "wedge", V(D * .55, H * .09, W * .38), V(D * .08, H * 1.02, 0), edge);
  if (b.has("crown")) for (let i = -1; i <= 1; i++)
    p.add("head", "cone", V(D * .18, H * (.2 - Math.abs(i) * .07), W * .1), V(D * .02, H * 1.01, i * W * .14), edge, {segments:4});
  if (b.has("bindings")) for (let i = 0; i < 3; i++)
    p.add("torso", "box", V(D * .75, H * .025, W * .72), V(0, H * (.49 + i * .13), 0), metal);
  if (b.has("branches")) for (const side of [-1,1]) {
    p.add("torso", "cylinder", V(H * .055, H * .45, H * .055), V(-D * .2, H * .95, side * W * .32), body, {turn:V(side * 30,0,0),segments:5});
    p.add("torso", "cylinder", V(H * .04, H * .24, H * .04), V(-D * .2, H * 1.05, side * W * .42), edge, {turn:V(side * 65,0,0),segments:5});
  }
  if (b.has("vault")) {
    p.add("torso", "box", V(D * .16, H * .33, W * .62), V(D * .48, H * .61, 0), metal);
    p.add("torso", "box", V(D * .04, H * .26, W * .51), V(D * .58, H * .61, 0), body);
    p.add("torso", "cylinder", V(H * .19, H * .05, H * .19), V(D * .63, H * .62, 0), edge, {turn:V(0,90,0),segments:8});
    p.add("torso", "box", V(D * .04, H * .025, W * .39), V(D * .67, H * .62, 0), joint);
  }
  if (b.has("scrap")) {
    p.add("torso", "box", V(D * .95, H * .24, W * .44), V(D * .1, H * .69, -W * .16), metal, {turn:V(-12,0,0)});
    p.add("torso", "ellipsoid", V(D * .7, H * .29, W * .41), V(0, H * .61, W * .27), edge, {segments:5});
    p.add("arm_l", "box", V(D * .85, H * .2, W * .35), V(D * .1, shoulder - reach * .7, -W * .4), metal);
  }
  if (b.has("chains")) for (let i = 0; i < 6; i++)
    p.add("torso", "box", V(H * .035, H * .065, H * .06), V(D * .5, H * (.65 - i * .045), W * .24), metal, {turn:V(i % 2 * 40,0,0)});
  if (b.has("trinkets")) for (let i = -1; i <= 1; i++)
    p.add("torso", "ellipsoid", V(H * .08, H * .1, H * .08), V(D * .55, H * (.65 + Math.abs(i) * .1), i * W * .17), core, {segments:5});
  p.sockets.set("chest", {bone:"torso",at:V(D * .4,H * .63,0),turn:ZERO,size:H});
  p.sockets.set("head", {bone:"head",at:V(D * .1,H,0),turn:ZERO,size:head});
  p.swings = true;
}
