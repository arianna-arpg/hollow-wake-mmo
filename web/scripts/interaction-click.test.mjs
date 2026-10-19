import { describe, it, expect } from 'vitest';
import { approachAndClick } from './interaction-click.mjs';

function scenario(samples, approachResult = true) {
  let time = 0, index = 0;
  const clicks = [], budgets = [];
  return {
    clicks, budgets,
    args: {
      within: 1.6, timeoutMs: 300,
      approach: async (budget) => { budgets.push(budget); return approachResult; },
      aim: async () => { time += 20; },
      observe: async () => samples[Math.min(index++, samples.length - 1)],
      click: async (screen) => { clicks.push(screen); },
      wait: async (ms) => { time += ms; },
      now: () => time,
    },
  };
}
const near = { self: { x: 0, y: 0 }, target: { key: 7, x: 1.3, y: 0 }, hovered: 7, screen: { x: 500, y: 300, visible: true } };

describe('scripted interaction approach', () => {
  it('reapproaches a villager that moved away during aiming and clicks only the fresh nearby sample', async () => {
    const run = scenario([{ ...near, target: { key: 7, x: 3.9, y: 0 } }, near]);
    expect(await approachAndClick(run.args)).toBe(true);
    expect(run.budgets).toEqual([300, 180]);
    expect(run.clicks).toEqual([near.screen]);
  });
  it('never clicks a different body under the pointer and stops at the original deadline', async () => {
    const run = scenario([{ ...near, hovered: 8 }]);
    expect(await approachAndClick(run.args)).toBe(false);
    expect(run.clicks).toEqual([]);
    expect(run.budgets).toEqual([300, 180, 60]);
  });
  it('does not interact if the approach failed or the target disappeared', async () => {
    for (const run of [scenario([near], false), scenario([{ ...near, target: null }])]) {
      expect(await approachAndClick(run.args)).toBe(false);
      expect(run.clicks).toEqual([]);
    }
  });
});
