// Scripted play-through of the web client against a running server, the browser twin of the Godot client's
// --smoke: logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, then
// checks held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the sheet,
// chat and weaves on the nearest hob through real keyboard and mouse input. Prints one SMOKE PASS or FAIL
// line per check, saves a screenshot per stage and exits non-zero when any check fails.
//
//   npm run smoke -- [--url http://localhost:7778/] [--shots DIR] [--account NAME]
//                    [--browser msedge|chrome|chromium] [--executable PATH] [--headed] [--full-states]
//
// --full-states runs the client without entity deltas (full entity states only), for comparison.
//
// It drives an installed Edge or Chrome through playwright-core, so nothing is downloaded. The page reads
// the client's state from window.hollowWake (src/app.ts, probe()); every action goes through input events.

import { mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { chromium } from 'playwright-core';

const args = process.argv.slice(2);
const opt = (name, fallback) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback;
};
const flag = (name) => args.includes(`--${name}`);

const url = opt('url', 'http://localhost:7778/');
const shots = resolve(opt('shots', 'shots'));
const account = opt('account', 'smoke-web');
const channel = opt('browser', process.platform === 'win32' ? 'msedge' : 'chrome');
const executablePath = opt('executable', undefined);

const results = [];
const started = Date.now();
const record = (name, pass, note) => {
  results.push({ name, pass, note });
  console.log(`SMOKE ${pass ? 'ok  ' : 'FAIL'} ${name}: ${note}`);
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

mkdirSync(shots, { recursive: true });
const browser = await chromium.launch({
  headless: !flag('headed'),
  ...(executablePath ? { executablePath } : channel === 'chromium' ? {} : { channel }),
  args: ['--ignore-gpu-blocklist', '--enable-unsafe-swiftshader'],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const pageErrors = [];
page.on('pageerror', (e) => pageErrors.push(e.message));
page.on('console', (m) => {
  if (m.type() === 'error') {
    pageErrors.push(m.text());
  }
});

const hw = (fn, arg) => page.evaluate(fn, arg);
const self = () => hw(() => window.hollowWake.self());
const counts = () => hw(() => window.hollowWake.counts);
const ui = () => hw(() => window.hollowWake.ui);
const rejected = async () => {
  const r = await hw(() => window.hollowWake.takeRejected());
  return r ? ` (rejected: ${r})` : '';
};
const flat = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const shot = async (name) => {
  await sleep(150);
  await page.screenshot({ path: join(shots, name) });
};

async function until(condition, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await condition()) {
      return true;
    }
    await sleep(100);
  }
  return condition();
}

async function nearest(where) {
  return hw((source) => {
    const test = new Function('e', `return (${source})(e);`);
    const me = window.hollowWake.self();
    if (!me) {
      return null;
    }
    return window.hollowWake.entities()
      .filter((e) => e.key !== me.key && test(e))
      .sort((a, b) => Math.hypot(a.x - me.x, a.y - me.y) - Math.hypot(b.x - me.x, b.y - me.y))[0] ?? null;
  }, where.toString());
}

/** The character keeps its place and its fights between runs; if it is down, R brings it back. */
async function ensureAlive() {
  if (!(await self())?.dead) {
    return true;
  }
  await page.keyboard.press('KeyR');
  return until(async () => (await self())?.dead === false, 15000);
}

/**
 * Walks toward a moving entity with move_to (as the desktop smoke does) until within reach. Server paths can
 * end against a blocking prop, so when the body stops making progress it steps aside and tries again.
 */
async function travelTo(key, within, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let lastSent = 0;
  let progressAt = Date.now();
  let best = Number.POSITIVE_INFINITY;
  let detour = 0;
  while (Date.now() < deadline) {
    const [me, target] = await hw((k) => [window.hollowWake.self(), window.hollowWake.entities().find((e) => e.key === k) ?? null], key);
    if (!me || !target) {
      return false;
    }
    if (me.dead) {
      await ensureAlive();
      lastSent = 0;
      continue;
    }
    const d = flat(me, target);
    if (d <= within) {
      await hw(() => window.hollowWake.stop());
      return true;
    }
    if (d < best - 0.3) {
      best = d;
      progressAt = Date.now();
    } else if (Date.now() - progressAt > 2500) {
      // Stuck: step sideways, alternating sides and widening, then head for the target again.
      detour++;
      const side = detour % 2 === 0 ? 1 : -1;
      const reach = 3 + detour;
      const nx = (target.y - me.y) / d;
      const ny = -(target.x - me.x) / d;
      await hw(([x, y]) => window.hollowWake.moveTo(x, y), [me.x + nx * side * reach, me.y + ny * side * reach]);
      await sleep(1800);
      progressAt = Date.now();
      best = Number.POSITIVE_INFINITY;
      lastSent = 0;
      continue;
    }
    if (Date.now() - lastSent > 1500) {
      lastSent = Date.now();
      await hw(([x, y]) => window.hollowWake.moveTo(x, y), [target.x, target.y]);
    }
    await sleep(100);
  }
  return false;
}

async function hover(key) {
  const s = await hw((k) => window.hollowWake.screenOf(k), key);
  if (s && s.visible) {
    await page.mouse.move(s.x, s.y);
    await sleep(120);
  }
  return s;
}

async function script() {
  await page.goto(`${url}${url.includes('?') ? '&' : '?'}account=${encodeURIComponent(account)}${flag('full-states') ? '&full-states' : ''}`);
  await page.waitForFunction(() => window.hollowWake !== undefined);
  await until(async () => (await page.inputValue('#login-host')).length > 0, 5000);
  const lists0 = (await counts()).characterLists;
  await page.click('#login-connect');
  const connected = await until(async () => (await counts()).characterLists > lists0, 20000);
  const granted = await hw(() => window.hollowWake.features);
  record('connect and log in', connected, `${await hw(() => window.hollowWake.status)} (features: ${granted.length > 0 ? granted.join(', ') : 'none'})`);
  if (!connected) {
    return;
  }

  let character = (await hw(() => window.hollowWake.characters())).find((c) => c.name === 'Smoke');
  if (!character) {
    const before = (await counts()).characterLists;
    await page.fill('#login-new-name', 'Smoke');
    await page.click('#login-create');
    await until(async () => (await counts()).characterLists > before, 20000);
    character = (await hw(() => window.hollowWake.characters())).find((c) => c.name === 'Smoke');
    record('create character', Boolean(character), character ? `${character.name} the ${character.calling}` : await hw(() => window.hollowWake.status));
    if (!character) {
      return;
    }
  }
  await page.click(`.character[data-id="${character.id}"]`);
  await page.click('#login-enter');
  const entered = await until(async () => hw(() => window.hollowWake.inWorld && window.hollowWake.self() !== null), 20000);
  const p = entered ? await self() : null;
  record('enter world', entered, p ? `${p.name} at ${p.x.toFixed(1)}, ${p.y.toFixed(1)}, ${p.z.toFixed(1)}` : await hw(() => window.hollowWake.status));
  if (!entered) {
    return;
  }

  const t0 = Date.now();
  const built = await until(async () => {
    const c = await counts();
    return c.chunksBuilt > 0 && c.chunksPending === 0;
  }, 90000);
  const c = await counts();
  record('terrain built', built, `${c.chunksBuilt} chunks (${c.chunksRequested} requested) in ${((Date.now() - t0) / 1000).toFixed(1)}s, ${c.entities} entities in view`);
  await sleep(800);
  await shot('01_isometric.png');

  // Held movement: W for a moment, then see where the server put us.
  if (!(await ensureAlive())) {
    record('respawn', false, 'the character is dead and R did not bring it back');
  }
  const w0 = await self();
  await page.keyboard.down('KeyW');
  await sleep(1500);
  await page.keyboard.up('KeyW');
  await sleep(600);
  const walked = flat(await self(), w0);
  record('wasd move', walked > 1, `moved ${walked.toFixed(1)} units${await rejected()}`);

  // Jump: watch our height for a lift.
  const z0 = (await self()).z;
  let zMax = z0;
  await page.keyboard.press('Space');
  const jumpUntil = Date.now() + 1500;
  while (Date.now() < jumpUntil) {
    zMax = Math.max(zMax, (await self()).z);
    await sleep(30);
  }
  record('jump', zMax - z0 > 0.15, `rose ${(zMax - z0).toFixed(2)} units${await rejected()}`);

  // Click-to-move: click the ground a few units away, in whichever direction is open.
  let moved = false;
  let tried = '';
  for (const [dx, dy] of [[5, 0], [0, 5], [-5, 0], [0, -5]]) {
    const from = await self();
    const s = await hw(([x, y]) => window.hollowWake.screenOfPoint(x, y, window.hollowWake.heightAt(x, y) ?? 0), [from.x + dx, from.y + dy]);
    await page.mouse.click(s.x, s.y);
    await sleep(2500);
    const d = flat(await self(), from);
    tried += `(${dx},${dy}) ${d.toFixed(1)}  `;
    if (d > 2) {
      moved = true;
      break;
    }
  }
  record('click to move', moved, `tried ${tried.trim()}${await rejected()}`);

  // Quest giver: walk up to the quartermaster, press F, and click it if F found someone nearer.
  const quartermaster = await nearest((e) => e.pattern === 'quartermaster' || e.name === 'Quartermaster');
  if (!quartermaster) {
    record('quartermaster offers quests', false, 'no quartermaster in view');
  } else {
    const reached = await travelTo(quartermaster.key, 1.4, 45000);
    await sleep(500);
    const offers0 = (await counts()).questOffers;
    await page.keyboard.press('KeyF');
    let shown = await until(async () => (await counts()).questOffers > offers0 && (await ui()).questsVisible, 3000);
    const viaF = await hw(() => window.hollowWake.lastInteracted);
    let how = `F interacted with ${viaF}`;
    if (!shown) {
      await hover(quartermaster.key);
      const s = await hw((k) => window.hollowWake.screenOf(k), quartermaster.key);
      await page.mouse.click(s.x, s.y);
      shown = await until(async () => (await counts()).questOffers > offers0 && (await ui()).questsVisible, 8000);
      how += ', then clicked the quartermaster';
    }
    const qs = await page.locator('#hud-quests .quest-button').count();
    record('quartermaster offers quests', reached && shown, `reached=${reached}, ${how}, panel shown=${shown} with ${qs} entries${await rejected()}`);
    await shot('02_quest_offer.png');
    await page.keyboard.press('Escape');
    await sleep(300);
    record('escape closes the quest panel', !(await ui()).questsVisible, `quests visible=${(await ui()).questsVisible}`);
  }

  // Cameras: V toggles, Q and E rotate.
  const mode0 = (await ui()).cameraMode;
  await page.keyboard.press('KeyV');
  await sleep(700);
  const toggled = (await ui()).cameraMode !== mode0;
  await shot('03_third_person.png');
  const yaw0 = (await ui()).cameraYaw;
  await page.keyboard.press('KeyQ');
  await sleep(200);
  const rotated = Math.abs((await ui()).cameraYaw - yaw0) > 0.01;
  await page.keyboard.press('KeyE');
  await sleep(200);
  const back = Math.abs((await ui()).cameraYaw - yaw0) < 0.01;
  await page.keyboard.press('KeyV');
  await sleep(400);
  const mode1 = (await ui()).cameraMode;
  record('camera toggle', toggled && mode1 === mode0, `${mode0} -> ${toggled ? 'switched' : 'unchanged'} -> ${mode1}`);
  record('camera rotate', rotated && back, `Q turned ${rotated ? 'yes' : 'no'}, E turned back ${back ? 'yes' : 'no'}`);
  for (let i = 0; i < 3; i++) {
    await page.mouse.wheel(0, -100);
  }

  // Sheet: C opens and closes.
  await page.keyboard.press('KeyC');
  await sleep(300);
  const sheetOpen = (await ui()).sheetVisible;
  await shot('04_sheet.png');
  await page.keyboard.press('KeyC');
  await sleep(300);
  record('character sheet', sheetOpen && !(await ui()).sheetVisible, `opened=${sheetOpen} closed=${!(await ui()).sheetVisible}`);

  // Chat: Enter focuses, Enter sends, the zone echoes it back.
  const line = `smoke check ${Date.now() % 100000}`;
  await page.keyboard.press('Enter');
  await page.keyboard.type(line);
  await page.keyboard.press('Enter');
  const echoed = await until(async () => (await page.locator('#hud-log').innerText()).includes(line), 5000);
  record('chat', echoed, echoed ? 'echoed by the zone' : 'no echo');

  // Weaves: walk up to the nearest hob, hover it and press the attack slots until the player's own hit lands.
  // Hobs wander and fight back, so each try walks up again first.
  const isHob = (e) => typeof e.pattern === 'string' && e.pattern.startsWith('hob') && !e.dead;
  let hob = await nearest(isHob);
  if (!hob) {
    record('weaves on a hob', false, `no hob among ${(await counts()).entities} entities in view`);
  } else {
    const hits0 = (await counts()).hitsDealt;
    const hoveredNames = new Set();
    let used = '';
    let reached = false;
    for (let attempt = 0; attempt < 10 && (await counts()).hitsDealt === hits0; attempt++) {
      await ensureAlive();
      const current = await hw((k) => window.hollowWake.entities().find((e) => e.key === k) ?? null, hob.key);
      if (!current || current.dead) {
        hob = (await nearest(isHob)) ?? hob;
      }
      reached = (await travelTo(hob.key, 2, 15000)) || reached;
      await hover(hob.key);
      hoveredNames.add((await ui()).target.split('  ')[0] || 'nothing');
      const slot = attempt % 2 === 0 ? 1 : 2;
      await page.keyboard.press(`Digit${slot}`);
      used += slot;
      await until(async () => (await counts()).hitsDealt > hits0, 1500);
    }
    const fought = await until(async () => (await counts()).hitsDealt > hits0, 3000);
    const lastHit = await hw(() => window.hollowWake.lastHit);
    record('weaves on a hob', fought && lastHit.includes('Hob'), `reached=${reached} hovered ${[...hoveredNames].join(', ')}; slots ${used}; hits dealt=${(await counts()).hitsDealt - hits0}, last on ${lastHit || 'nothing'}${await rejected()}`);
    await shot('05_combat.png');
  }

  // Leave the character in the hamlet, where the next run's quest check starts from.
  await ensureAlive();
  const home = await nearest((e) => e.pattern === 'quartermaster');
  if (home) {
    await travelTo(home.key, 3, 30000);
  }
}

try {
  await script();
} catch (error) {
  record('script', false, error instanceof Error ? error.message : String(error));
}

const fps = await page.evaluate(() => new Promise((done) => {
  let frames = 0;
  const t0 = performance.now();
  const step = () => {
    frames++;
    if (performance.now() - t0 < 2000) {
      requestAnimationFrame(step);
    } else {
      done(Math.round(frames / ((performance.now() - t0) / 1000)));
    }
  };
  requestAnimationFrame(step);
})).catch(() => 0);
const final = await counts().catch(() => ({}));
record('no page errors', pageErrors.length === 0, pageErrors.length === 0 ? 'none' : pageErrors.slice(0, 3).join(' | '));
await browser.close();

const passed = results.filter((r) => r.pass).length;
console.log('SMOKE SUMMARY');
for (const r of results) {
  console.log(`SMOKE ${r.pass ? 'PASS' : 'FAIL'} ${r.name}: ${r.note}`);
}
console.log(`SMOKE DONE ${passed}/${results.length} passed in ${((Date.now() - started) / 1000).toFixed(1)}s; ${fps} fps, ${final.frameMs ?? '?'} ms of work per frame, ${final.chunksBuilt ?? '?'} chunks and ${final.entities ?? '?'} entities at the end; shots in ${shots}`);
process.exit(passed === results.length ? 0 : 1);
