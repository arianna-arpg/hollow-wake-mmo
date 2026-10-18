import { describe, expect, it } from 'vitest';
import fixtures from '../../../../tests/fixtures/ability-icons.json';
import type { FaceInfo } from '../../net/protocol';
import { AbilityIcons, type AbilityFace } from './abilityIcons';
import { bundledKinds, type Obj } from './content';
import type { ItemInfo } from '../../net/protocol';

const catalog = (...extra: Array<[string, Obj]>): AbilityIcons => {
  const kinds = new Map(bundledKinds());
  for (const [kind, entry] of extra) kinds.set(kind, [...(kinds.get(kind) ?? []), entry]);
  return new AbilityIcons(kinds);
};
const item = (form: ItemInfo['form']): ItemInfo => ({uid:1,base:'memory',name:'Renamed',rarity:'common',count:1,level:1,lines:[],local:{},form});

describe('shared ability artwork', () => {
  it('resolves every imported skill and support to nonempty vectors without text', () => {
    const icons = catalog();
    for (const id of icons.art.keys()) {
      if (!id.startsWith('weaves.') && !id.startsWith('glyphs.')) continue;
      const dot = id.indexOf('.');
      const face = icons.resolve(id.slice(0, dot), id.slice(dot + 1));
      expect(icons.art.get(face.art)?.layers?.length, id).toBeGreaterThan(0);
      expect(icons.svg(face)).not.toContain('<text');
      expect(face.color).toMatch(/^#[\da-f]+$/i);
    }
  });
  it('shares memory and seat identity while distinguishing preset/form ID collisions', () => {
    const icons = catalog(['forms',{id:'ember_bolt',meta:{ability_icon:'guard'}}]);
    const memory = item({id:'ember_bolt',name:'Translated',level:1,sockets:2,weave:'ember_bolt'});
    expect(icons.item(memory)).toEqual(icons.seat('ember_bolt','bolt','ember_bolt',null));
    expect(icons.item(memory)?.art).toBe('prototype/skill:firebolt');
    const plain = item({...memory.form!,weave:undefined});
    expect(icons.item(plain)?.art).toBe('prototype/guard');
    expect(icons.item(plain)).toEqual(icons.seat('ember_bolt[]+','ember_bolt',null,null));
  });
  it('uses converted identity and preserves legacy alternate hints', () => {
    const icons = catalog();
    expect(icons.seat('ember_bolt','bolt','ember_bolt',{label:'Arbitrary',weave:'tide_bolt',form:'bolt'})).toEqual(icons.resolve('weaves','tide_bolt'));
    expect(icons.seat('ember_bolt','bolt',null,{label:'Recall',icon:'recall'}).art).toBe('recall');
    expect(icons.seat('ember_bolt','bolt',null,{label:'Unknown'}).art).toBe('arcane');
  });
  it('supports future content, explicit overrides and support badges without name matching', () => {
    const icons = catalog(['forms',{id:'new_form',name:'Not a picture key',tags:['melee']}],
      ['weaves',{id:'new_weave',form:'new_form',color:'#123456'}],
      ['glyphs',{id:'new_support',color:'#abcdef',meta:{ability_icon:'recall'}}]);
    expect(icons.resolve('weaves','new_weave')).toEqual({art:'prototype/sweep',color:'#123456',badge:null});
    expect(icons.resolve('glyphs','new_support')).toEqual({art:'prototype/recall',color:'#abcdef',badge:'badge.support'});
    expect(icons.resolve('forms','misleading_fireball_name').art).toBe('prototype/arcane');
  });
  it('preserves layer transforms and escapes markup', () => {
    const icons = catalog(['ui_ability_icons',{id:'unsafe',layers:[{path:'M0 0"/><script>bad</script>',stroke:'ink',transform:[1,0,0,1,2,3]}]}]);
    const svg = icons.svg({art:'unsafe',color:'#abc',badge:null});
    expect(svg).toContain('matrix(1 0 0 1 2 3)');
    expect(svg).not.toContain('<script>');
    expect(svg).toContain('&lt;script&gt;');
    expect(svg).toContain('stroke-width="2.15"');
  });
});

describe('shared native/browser fixtures', () => {
  const icons = new AbilityIcons(new Map(Object.entries(fixtures.kinds)) as Map<string, Obj[]>);
  for (const c of fixtures.cases as Array<{name: string; kind?: string; id?: string; threads?: string[]; seat?: {key: string|null; form?: string; preset?: string; converted?: FaceInfo}; face: AbilityFace; tone?: string; svg: string}>) {
    it(c.name, () => {
      const face = c.seat ? icons.seat(c.seat.key, c.seat.form, c.seat.preset, c.seat.converted ?? null, c.threads) : icons.resolve(c.kind!, c.id, c.threads);
      expect(face).toEqual(c.face);
      expect(icons.svg({...face, tone:c.tone})).toBe(c.svg);
    });
  }
});
