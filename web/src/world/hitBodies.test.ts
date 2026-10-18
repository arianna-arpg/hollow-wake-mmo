import {describe,it,expect} from 'vitest';
import fixture from '../../../tests/fixtures/hit-body-lines.json';
import {hitBodyLines,supportedHitBody} from './hitBodies';
import {applyDelta} from '../net/delta';
import type {EntityState} from '../net/protocol';
describe('server hit bodies',()=>{
 it.each(fixture)('$id agrees with the shared native line fixture',row=>{
  const actual=hitBodyLines(row.body,row.radius,row.height,16);expect(actual.length).toBe(row.lines.length);
  actual.forEach((p,i)=>p.forEach((n,k)=>expect(n).toBeCloseTo(row.lines[i]![k]!,5)));
 });
 it('declines unknown volume geometry',()=>{
  const body={id:'mod',aim:0,volumes:[{shape:'mod_shape',forward:0,left:0,up:.5,radius:1,half_height:.5}]};
  expect(supportedHitBody(body)).toBe(false);expect(hitBodyLines(body,1,2)).toEqual([]);
 });
 it('preserves a profile across size deltas and clears it explicitly',()=>{
  const before={key:1,hit_body:'orc_grunt',radius:.5,height:2,resources:{},resource_max:{}} as EntityState;
  const scaled=applyDelta(before,{key:1,radius:1,height:4});expect(scaled.hit_body).toBe('orc_grunt');
  expect(applyDelta(scaled,{key:1,hit_body:''}).hit_body).toBeUndefined();
 });
});
