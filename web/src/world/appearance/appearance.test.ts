import { describe, expect, it } from 'vitest';
import { bundledKinds } from '../../ui/logic/content';
import { animate, freshVisualState, Pose } from './animator';
import { buildBody } from './bodyFamilies';
import { buildPart } from './bodyPlan';
import { DayCycle } from './dayCycle';
import { lay, wear } from './gearFamilies';
import { AppearanceCatalog, jitter, scaleOf, variationUnit, wornFromDescriptor } from './looks';
import { SurfaceLook } from './surfaceLook';

/**
 * The appearance content and the shared body logic as the browser reads them, mirroring the desktop's
 * AppearanceTests so both clients agree on the same looks, the same geometry and the same poses.
 */

const kinds = bundledKinds();
const catalog = new AppearanceCatalog(kinds);
const patterns = kinds.get('patterns') ?? [];

describe('appearance catalog', () => {
  it('finds a look for every pattern the world spawns, not a fallback', () => {
    const missing: string[] = [];
    for (const p of patterns) {
      const id = String(p.id);
      const tags = catalog.patternTags(id);
      const concrete = ['summonable', 'device', 'npc', 'prop', 'flora', 'monster'].some((t) => tags.has(t)) && id !== 'device' && id !== 'monster';
      if (!concrete) {
        continue;
      }
      const look = catalog.resolve(typeof p.look === 'string' ? p.look : id, id);
      if (!look || look.id.endsWith('_fallback')) {
        missing.push(`${id} -> ${look?.id ?? 'nothing'}`);
      }
    }
    expect(missing).toEqual([]);
  });

  it('resolves looks by id, by tags and for places and drops', () => {
    expect(catalog.resolve('hob_howler', 'hob_howler')?.id).toBe('hob_howler');
    expect(catalog.resolve('made_up_beast', 'made_up_beast', ['beast', 'creature'])?.id).toBe('beast_fallback');
    expect(catalog.resolve('poi:knot', null)?.id).toBe('marker_knot');
    expect(catalog.resolve('poi:hamlet', null)?.id).toBe('marker');
    expect(catalog.resolve('drop:short_sword', null)?.body).toBe('drop');
    expect(catalog.resolve('fount', null)?.body).toBe('fount');
    expect(catalog.resolve('nothing_at_all', 'nothing_at_all')).toBeNull();
    for (const c of kinds.get('callings') ?? []) {
      expect(catalog.resolve(String(c.look ?? c.id), null)?.body).toBe('biped');
    }
  });

  it('finds gear for every item base and the self equipment slots', () => {
    for (const item of kinds.get('items') ?? []) {
      expect(catalog.gearForItem(String(item.id)), String(item.id)).not.toBeNull();
    }
    expect(catalog.gearForItem('short_sword')?.body).toBe('blade');
    expect(catalog.gearForItem('dagger')?.id).toBe('gear_dagger');
    expect(catalog.gearForItem('iron_ore')?.body).toBe('sack');
    expect(catalog.gearFor('head', 'armor.head', ['heavy'])?.body).toBe('helm');
    expect(catalog.gearFor('chest', 'armor.chest', ['cloth', 'caster'])?.body).toBe('robe');
    const brigand = catalog.defaultGear(catalog.byIdOf('loosed_brigand')!, 'loosed_brigand');
    expect(brigand.get('main_hand')?.itemClass).toBe('weapon.sword');
    const holdfast = catalog.defaultGear(catalog.byIdOf('holdfast')!, null);
    expect(holdfast.get('off_hand')?.itemClass).toBe('shield.buckler');
  });

  it('relates factions to the players from the factions content', () => {
    expect(catalog.relationOf('wilds')).toBe('hostile');
    expect(catalog.relationOf('weftmarch_compact')).toBe('friendly');
    expect(catalog.relationOf(undefined)).toBe('neutral');
  });

  it('dresses other players from the wire descriptor, the self from its equipment, the rest from the kit', () => {
    const holdfast = catalog.resolve('holdfast', null)!;
    // nothing from the server: the Calling's kit
    expect(catalog.wornBy(holdfast, null, undefined, undefined).get('off_hand')?.itemClass).toBe('shield.buckler');
    expect(catalog.wornBy(holdfast, null, [], undefined).size).toBe(0);
    expect(catalog.wornBy(holdfast, null, ['main_hand=weapon.sword'], {}).size).toBe(0);
    // the descriptor's slots, classes and tags read as the item's own would, and the class's trailing word is a tag
    const worn = catalog.wornBy(holdfast, null, ['chest=armor.chest cloth caster', 'main_hand=weapon.wand caster one_hand focus_weapon', 'head=armor.head.heavy'], undefined);
    expect([...worn.keys()].sort()).toEqual(['chest', 'head', 'main_hand']);
    expect(catalog.gearFor('chest', worn.get('chest')!.itemClass, worn.get('chest')!.tags)?.body).toBe('robe');
    expect(catalog.gearFor('main_hand', worn.get('main_hand')!.itemClass, worn.get('main_hand')!.tags)?.body).toBe('wand');
    expect(catalog.gearFor('head', worn.get('head')!.itemClass, worn.get('head')!.tags)?.id).toBe('gear_helm');
    expect(worn.has('off_hand')).toBe(false);
    // the owner's own equipment wins over the descriptor
    const own = { main_hand: { uid: 1, base: 'dagger', name: 'Dagger', rarity: 'common', count: 1, level: 1, slot: 'main_hand', lines: [], local: {} } };
    const self = catalog.wornBy(holdfast, null, ['main_hand=weapon.sword melee blade one_hand'], own);
    expect(self.get('main_hand')?.itemClass).toBe('weapon.dagger');
    expect(self.get('main_hand')?.tags).toContain('finesse');
    // entries without a slot or a class say nothing
    expect(wornFromDescriptor(['nonsense', '=weapon.sword', 'belt=', 'belt=   ']).size).toBe(0);
  });
});

describe('bodies', () => {
  it('builds a finite body for every entry and wears gear on a biped', () => {
    for (const def of catalog.all.filter((d) => d.slot === null)) {
      const plan = buildBody(def, 12345, 1.8, 0.45);
      expect(plan.parts.length, def.id).toBeGreaterThan(0);
      expect(plan.height).toBeGreaterThan(0);
      for (const part of plan.parts) {
        expect(plan.bones.some((b) => b.name === part.bone)).toBe(true);
        const built = buildPart(part);
        expect(built.positions.length).toBeGreaterThan(0);
        expect(built.positions.every((v) => Number.isFinite(v))).toBe(true);
      }
    }
    const hob = buildBody(catalog.byIdOf('hob')!, 7, 1.1, 0.45);
    const before = hob.parts.length;
    wear(hob, catalog.gearFor('main_hand', 'weapon.sword', ['melee'])!, 'main_hand', 7);
    expect(hob.parts.length).toBeGreaterThan(before);
    expect(hob.swings).toBe(true);
    const drop = buildBody(catalog.byIdOf('drop')!, 9, 0.3, 0.3);
    lay(drop, catalog.gearForItem('short_sword')!, 9);
    expect(drop.parts.length).toBeGreaterThan(1);
  });

  it('seeds variation from the key within bounds, the same as the desktop', () => {
    expect(variationUnit(42, 1)).toBe(variationUnit(42, 1));
    expect(variationUnit(42, 1)).not.toBe(variationUnit(43, 1));
    const v = { hue: 0.05, value: 0.1, scale: 0.1 };
    for (let key = 1; key < 100; key++) {
      const s = scaleOf(v, key);
      expect(s).toBeGreaterThanOrEqual(0.9);
      expect(s).toBeLessThanOrEqual(1.1);
      const c = jitter([0.5, 0.3, 0.2], v, key);
      expect(c.every((x) => x >= 0 && x <= 1)).toBe(true);
    }
  });

  it('builds the same geometry as the desktop for a known part', () => {
    // the first triangle of a unit box: the bottom face, counter-clockwise from below
    const built = buildPart({ bone: 'root', shape: 'box', size: [1, 1, 1], at: [0, 0, 0], turn: [0, 0, 0], color: [1, 1, 1], taper: 1, glow: false, segments: 8 });
    expect([...built.positions.slice(0, 9)]).toEqual([-0.5, -0.5, -0.5, 0.5, -0.5, 0.5, -0.5, -0.5, 0.5]);
    expect([...built.normals.slice(0, 3)]).toEqual([0, -1, 0]);
    expect(built.positions.length).toBe(12 * 3 * 3);
  });
});

describe('animator', () => {
  it('poses every rig in every state with numbers, swings and falls', () => {
    const motion = catalog.motion;
    const pose = new Pose();
    for (const rig of ['biped', 'quadruped', 'wisp', 'crawler', 'tree', 'knot', 'static']) {
      for (const swings of [true, false]) {
        const states = [
          {},
          { speed: 5, walkPhase: 1.2 },
          { casting: true, castProgress: 0.6, castMode: 'cast' },
          { casting: true, castMode: 'channel' },
          { casting: true, castMode: 'guard' },
          { releaseAge: 0.05 },
          { hitAge: 0.05, hitStrength: 0.8, hitFrom: 1.2 },
          { grounded: false, leaping: true },
          { dead: true, deadFor: 0.3 },
          { dead: true, deadFor: 30 },
          { reducedMotion: true, speed: 3, hitAge: 0.02 },
        ];
        for (const over of states) {
          animate({ ...freshVisualState(), rig, swings, time: 12.5, ...over }, motion, pose);
          for (const [bone, p] of pose.bones) {
            expect(Number.isFinite(p.turn[0] + p.turn[1] + p.turn[2] + p.offset[0] + p.offset[1] + p.offset[2] + p.scale), `${rig} ${bone}`).toBe(true);
            expect(p.scale).toBeGreaterThan(0);
          }
        }
      }
    }
    animate({ ...freshVisualState(), rig: 'biped', swings: true, time: 1, casting: true, castProgress: 1, castMode: 'cast' }, motion, pose);
    expect(pose.of('arm_r').turn[1]).toBeLessThan(-30);
    animate({ ...freshVisualState(), rig: 'biped', swings: true, time: 1, releaseAge: 0.15 }, motion, pose);
    expect(pose.of('arm_r').turn[1]).toBeGreaterThan(30);
    animate({ ...freshVisualState(), rig: 'biped', time: 1, hitAge: 0.01, hitStrength: 1 }, motion, pose);
    expect(pose.flash).toBeGreaterThan(0.5);
    animate({ ...freshVisualState(), rig: 'biped', time: 1, dead: true, deadFor: 5 }, motion, pose);
    expect(Math.abs(pose.of('root').turn[1])).toBeGreaterThan(80);
    animate({ ...freshVisualState(), rig: 'biped', time: 1, reducedMotion: true }, motion, pose);
    expect(pose.of('head').turn[2]).toBe(0);
  });
});

describe('surfaces and the day', () => {
  it('colours cells by tags and flags with a stable jitter', () => {
    const look = new SurfaceLook(catalog.surfaces);
    const meadow = look.cell(new Set(['grass', 'meadow']), 1);
    const grass = look.cell(new Set(['grass']), 1);
    expect(meadow).not.toEqual(grass);
    expect(look.cell(new Set(['grass']), 1 | 8)).not.toEqual(grass);
    expect(look.structure(new Set(['wall', 'structure']), 16)?.roof).toBeNull();
    expect(look.structure(new Set(['house', 'roof']), 16)?.roof).not.toBeNull();
    expect(look.structure(new Set(['grass']), 1)).toBeNull();
    for (let x = -10; x < 10; x++) {
      for (let y = -10; y < 10; y++) {
        const j = look.jitter(x, y, 1);
        expect(j).toBeGreaterThan(0.8);
        expect(j).toBeLessThan(1.2);
        expect(look.jitter(x, y, 1)).toBe(j);
        expect(look.jitter(x, y, 16)).toBe(1);
      }
    }
  });

  it('wraps the day and blends the sky continuously', () => {
    const day = new DayCycle(catalog.world);
    expect(day.timeOfDay(0)).toBeCloseTo(day.timeOfDay(day.lengthSeconds), 4);
    const noon = day.at(0.5);
    const midnight = day.at(0);
    expect(noon.sunEnergy).toBeGreaterThan(midnight.sunEnergy);
    expect(noon.sunPitch).toBeGreaterThan(30);
    expect(midnight.sunPitch).toBe(0);
    expect(midnight.fogDensity).toBeGreaterThan(noon.fogDensity);
    let last = day.at(0);
    for (let f = 0.01; f <= 1.0001; f += 0.01) {
      const s = day.at(f);
      expect(Math.abs(s.sky[0] - last.sky[0])).toBeLessThan(0.1);
      last = s;
    }
    expect(day.at(0).sky).toEqual(day.at(1).sky);
  });

  it('holds a stopped server day and preserves a subsecond day rate', () => {
    const day = new DayCycle(catalog.world);
    for (const length of [0, -1]) {
      day.observeServer(length, 0.5, 100);
      expect(day.timeOfDay(100.25)).toBeCloseTo(0.5, 4);
      day.observeServer(undefined, 0.5, 200);
      expect(day.timeOfDay(200.25)).toBeCloseTo(0.5, 4);
    }
    day.observeServer(0.5, 0.25, 300);
    expect(day.timeOfDay(300.25)).toBeCloseTo(0.75, 4);
  });

  it('follows the server day when it says and folds the clock otherwise', () => {
    const day = new DayCycle(catalog.world);
    expect(day.server).toBeNull();
    const folded = day.timeOfDay(100);
    expect(day.dayLengthSeconds).toBe(day.lengthSeconds);
    // enter_world: the length and the fraction as of a server time; the fraction advances with the clock and wraps
    day.observeServer(600, 0.25, 100);
    expect(day.timeOfDay(100)).toBeCloseTo(0.25, 4);
    expect(day.timeOfDay(250)).toBeCloseTo(0.5, 4);
    expect(day.timeOfDay(580)).toBeCloseTo(0.05, 4);
    expect(day.dayLengthSeconds).toBe(600);
    // the self state: a fraction alone keeps the length; no fraction changes nothing
    day.observeServer(undefined, 0.9, 1000);
    expect(day.timeOfDay(1000)).toBeCloseTo(0.9, 4);
    expect(day.dayLengthSeconds).toBe(600);
    day.observeServer(undefined, undefined, 2000);
    expect(day.timeOfDay(1000)).toBeCloseTo(0.9, 4);
    // a server that never says: the clock folded by the content's day again
    day.forgetServer();
    expect(day.server).toBeNull();
    expect(day.timeOfDay(100)).toBeCloseTo(folded, 4);
  });
});
