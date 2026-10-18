// Scripted play-through of the web client against a running server, the browser twin of the Godot client's
// --smoke: logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, then
// checks held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the character
// page, chat, the core pane and every ribbon page, a villager's conversation, a vendor's trade, Options
// (switching the theme, rebinding a key and using it, scaling the interface, resetting a group) and weaves on
// the nearest hob through real keyboard and mouse input. Prints one SMOKE PASS or FAIL line per check, saves a
// screenshot per stage and exits non-zero when any check fails.
//
//   npm run smoke -- [--url http://localhost:7778/] [--shots DIR] [--account NAME]
//                    [--browser msedge|chrome|chromium] [--executable PATH] [--headed] [--full-states]
//
// --full-states runs the client without entity deltas (full entity states only), for comparison. The page
// runs under its own settings profile (smoke), started from the content's defaults each run.
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

/** Clicks an entity where it is drawn, as a player would. */
async function clickEntity(key) {
  await hover(key);
  const s = await hw((k) => window.hollowWake.screenOf(k), key);
  if (s && s.visible) {
    await page.mouse.click(s.x, s.y);
  }
}

const statesOf = async (type) => (await counts()).states[type] ?? 0;

/** Presses Escape until the core pane and every pane are closed (Escape with nothing open would open Options). */
async function closeEverything() {
  for (let i = 0; i < 8; i++) {
    const u = await ui();
    if (!u.coreOpen && u.worldPanes.length === 0) {
      return;
    }
    await page.keyboard.press('Escape');
    await sleep(150);
  }
}

/**
 * The core pane: the inventory key opens it with the bag, the doll and the wallet; each ribbon the hero has
 * shows its page; Escape closes the front page, then the core pane last.
 */
async function corePaneAndPages() {
  await closeEverything();
  await page.keyboard.press('KeyI');
  const open = await until(async () => (await ui()).coreOpen, 3000);
  await sleep(400);
  const dollSlots = await page.locator('#hud-doll .doll-slot').count();
  const chips = await page.locator('.rail .chip').count();
  const u = await ui();
  await shot('06_core_pane.png');
  record('core pane', open && dollSlots > 0, `open=${open}, ${u.bagItems} bag items, ${dollSlots} doll slots, ${chips} wallet chips, ribbons ${u.ribbonPages.join(' ')}`);
  const shown = [];
  for (const id of ['weaves', 'passives', 'character', 'reliquary', 'stash']) {
    const ribbon = page.locator(`.ribbon[data-page="${id}"]`);
    if ((await ribbon.count()) === 0 || ((await ribbon.getAttribute('class')) ?? '').includes('sealed')) {
      shown.push(`${id}=${(await ribbon.count()) === 0 ? 'absent' : 'sealed'}`);
      continue;
    }
    await ribbon.click();
    const ok = await until(async () => (await ui()).shownPages.includes(id), 2000);
    await sleep(350);
    await shot(`07_page_${id}.png`);
    shown.push(`${id}=${ok ? 'shown' : 'not shown'}`);
  }
  record('ribbon pages', ['weaves', 'passives', 'character'].every((id) => shown.includes(`${id}=shown`)), shown.join(', '));
  const escapeState = (u) => JSON.stringify({ core: u.coreOpen, options: u.optionsOpen, tray: u.trayOpen, pages: u.shownPages, world: u.worldPanes });
  const escapeSteps = [];
  for (let i = 0; i < 8 && (await ui()).coreOpen; i++) {
    const before = escapeState(await ui());
    await page.keyboard.press('Escape');
    // Wait for the observed transition before another press; a busy renderer/server can lag
    // the old fixed delay. Keep the same bounded steps and require all panes to actually close.
    const changed = await until(async () => escapeState(await ui()) !== before, 2000);
    escapeSteps.push({ before, after: escapeState(await ui()), changed });
    await sleep(150);
  }
  const closed = !(await ui()).coreOpen && !(await ui()).optionsOpen;
  const focus = closed ? '' : await hw(() => `${document.activeElement?.tagName}#${document.activeElement?.id}`);
  record('escape closes the core pane last', closed, `core open=${(await ui()).coreOpen}${closed ? '' : `; focus=${focus}; transitions=${JSON.stringify(escapeSteps)}`}`);
}

/** A villager: walk up, click it, read its line and answer the first open choice. */
async function talkToAVillager() {
  const villager = await nearest((e) => e.pattern === 'villager' || e.interactable === 'talk');
  if (!villager) {
    record('talk to a villager', false, 'no villager in view');
    return;
  }
  const reached = await travelTo(villager.key, 1.6, 45000);
  await sleep(400);
  const before = await statesOf('dialogue_state');
  await clickEntity(villager.key);
  const shown = await until(async () => (await statesOf('dialogue_state')) > before && (await ui()).worldPanes.includes('dialogue'), 6000);
  await sleep(300);
  await shot('08_dialogue.png');
  let line = 'no dialogue_state arrived';
  if (shown) {
    const speaker = await page.locator('.dialogue-pane .dialogue-speaker').first().innerText().catch(() => '');
    const said = await page.locator('.dialogue-pane .dialogue-line').first().innerText().catch(() => '');
    line = `${speaker}: "${said.slice(0, 48)}"`;
    const choice = page.locator('.dialogue-pane .dialogue-choice:not([disabled])').first();
    if ((await choice.count()) > 0) {
      const asked = await statesOf('dialogue_state');
      const text = await choice.innerText();
      await choice.click();
      const answered = await until(async () => (await statesOf('dialogue_state')) > asked, 5000);
      line += `, chose "${text.slice(0, 40)}" (answered=${answered})`;
    }
  }
  record('talk to a villager', reached && shown, `reached=${reached}, ${line}${await rejected()}`);
  await closeEverything();
}

/** The quartermaster's trade: talk, ask to trade, see the stock, and buy the first thing the hero can afford. */
async function tradeWithAVendor() {
  const quartermaster = await nearest((e) => e.pattern === 'quartermaster' || e.name === 'Quartermaster');
  if (!quartermaster) {
    record('trade with a vendor', false, 'no quartermaster in view');
    return;
  }
  const reached = await travelTo(quartermaster.key, 1.4, 45000);
  await sleep(400);
  const vendors = await statesOf('vendor_state');
  const talked = await statesOf('dialogue_state');
  await clickEntity(quartermaster.key);
  await until(async () => (await statesOf('dialogue_state')) > talked && (await ui()).worldPanes.includes('dialogue'), 6000);
  // The dialogue pane may sit behind the quest offers in one book: bring it to the front.
  const tab = page.locator('.pane-tabs .tab', { hasText: 'Quartermaster' });
  if ((await tab.count()) > 0) {
    await tab.first().click();
    await sleep(200);
  }
  const trade = page.locator('.dialogue-pane .dialogue-choice[data-choice="trade"]');
  if ((await trade.count()) > 0) {
    await trade.first().click();
  }
  const opened = await until(async () => (await statesOf('vendor_state')) > vendors && (await ui()).worldPanes.includes('vendor'), 6000);
  await sleep(300);
  // The vendor arrives as a fresh tab behind the conversation that opened it: look at it.
  const vendorTab = page.locator('.pane-tabs .tab[data-tab^="vendor:"]');
  if ((await vendorTab.count()) > 0) {
    await vendorTab.first().click();
    await sleep(300);
  }
  const stock = await page.locator('.vendor-pane .vendor-stock').count();
  let bought = 'nothing the hero can afford';
  const buy = page.locator('.vendor-pane .vendor-buy:not([disabled])').first();
  if (opened && (await buy.count()) > 0) {
    const bag0 = (await ui()).bagItems;
    await buy.click();
    const ok = await until(async () => (await ui()).bagItems !== bag0, 3000);
    bought = ok ? 'bought one' : `asked to buy${await rejected()}`;
  }
  await shot('09_vendor.png');
  record('trade with a vendor', reached && opened && stock > 0, `reached=${reached}, vendor open=${opened}, ${stock} stock rows, ${bought}`);
  await closeEverything();
}

/**
 * Options: open it, switch the theme and see the frame change, find a key by search, rebind it and use it,
 * scale the interface with the core pane open and see the panes refit above the cluster, and reset a group,
 * all through the pane's own controls.
 */
async function optionsSteps() {
  await closeEverything();
  await page.keyboard.press('F10');
  const open = await until(async () => (await ui()).optionsOpen, 3000);
  await sleep(400);
  await shot('11_options.png');
  record('options open', open, `open=${open}, ${await page.locator('.options-pane .option-row').count()} rows`);
  if (!open) {
    return;
  }
  const frameOf = () => page.evaluate(() => document.getElementById('interface')?.getAttribute('data-frame') ?? '');
  const frame0 = await frameOf();
  await page.locator('.option-row[data-entry="theme"] select').selectOption('runic_brass');
  const themed = await until(async () => (await ui()).theme === 'runic_brass', 3000);
  await sleep(400);
  const frame1 = await frameOf();
  await shot('12_theme.png');
  record('options switch the theme', themed && frame0 !== frame1, `theme ${(await ui()).theme}, frame ${frame0} -> ${frame1}`);

  await page.locator('input.options-search').fill('Switch camera');
  await sleep(300);
  let rebound = false;
  const slot = page.locator('.option-row[data-entry="key.camera_toggle"] .key-slot[data-slot="0"]');
  if ((await slot.count()) > 0) {
    await slot.click();
    await sleep(200);
    await page.keyboard.press('KeyB');
    await sleep(300);
    rebound = (await hw(() => window.hollowWake.bindings('camera_toggle'))).primary === 'B';
  }
  await shot('13_rebound.png');
  await page.locator('.options-done').click();
  await until(async () => !(await ui()).optionsOpen, 3000);
  const mode0 = (await ui()).cameraMode;
  await page.keyboard.press('KeyB');
  await sleep(500);
  const turned = (await ui()).cameraMode !== mode0;
  await page.keyboard.press('KeyB');
  await sleep(400);
  record('options rebind a key', rebound && turned, `camera switch on B=${rebound}, B turned the camera=${turned}`);

  await page.keyboard.press('F10');
  await until(async () => (await ui()).optionsOpen, 3000);
  await page.keyboard.press('KeyI');
  await sleep(300);
  await page.locator('.options-page[data-page="interface"]').click();
  await sleep(300);
  const u0 = await ui();
  const before = u0.cluster.w * u0.uiScale;
  await page.locator('.option-row[data-entry="ui_scale"] input[type="range"]').evaluate((input) => {
    input.value = '1.25';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await sleep(600);
  const u1 = await ui();
  const after = u1.cluster.w * u1.uiScale;
  const fits = u1.rects !== null && u1.rects.core.y + u1.rects.core.h <= u1.cluster.y + 0.5;
  await shot('14_scale.png');
  record('options scale the interface', Math.abs(u1.uiScale - 1.25) < 0.01 && after > before + 10 && fits,
    `scale ${u1.uiScale.toFixed(2)}, cluster ${before.toFixed(0)} -> ${after.toFixed(0)} px wide, panes above the cluster=${fits}`);

  await page.locator('.group-reset[data-group="look"]').first().click();
  await sleep(600);
  const theme = await hw(() => window.hollowWake.setting('theme'));
  const scale = await hw(() => window.hollowWake.setting('ui_scale'));
  await shot('15_reset.png');
  record('options reset a group', theme === 'etched_stone' && scale === 1, `theme ${theme}, scale ${scale}`);

  // Put the key back through its row and close everything.
  await page.locator('input.options-search').fill('Switch camera');
  await sleep(300);
  const reset = page.locator('.option-row[data-entry="key.camera_toggle"] .row-reset');
  if ((await reset.count()) > 0 && (await reset.isVisible())) {
    await reset.click();
  }
  await page.locator('.options-done').click();
  await until(async () => !(await ui()).optionsOpen, 3000);
  await closeEverything();
}

/** Samples two seconds of frames: frame time percentiles, the renderer's draw calls and triangles, and the bodies drawn. */
async function measure(mode) {
  const frames = await page.evaluate(() => new Promise((done) => {
    const times = [];
    let last = performance.now();
    const t0 = last;
    const step = () => {
      const now = performance.now();
      times.push(now - last);
      last = now;
      if (now - t0 < 2000) {
        requestAnimationFrame(step);
      } else {
        done(times);
      }
    };
    requestAnimationFrame(step);
  }));
  frames.sort((a, b) => a - b);
  const at = (q) => (frames.length === 0 ? 0 : frames[Math.min(frames.length - 1, Math.floor(q * frames.length))]);
  const r = await hw(() => window.hollowWake.render);
  const c = await counts();
  const fps = Math.round(frames.length / 2);
  const line = `${mode}: ${fps} fps, frame ms p50 ${at(0.5).toFixed(2)} p95 ${at(0.95).toFixed(2)} max ${frames[frames.length - 1].toFixed(2)}, ${c.frameMs} ms of work per frame; `
    + `${r.drawCalls} draw calls, ${r.triangles} triangles; ${r.bodies} bodies (${r.cards} cards, ${r.bodyTriangles} body triangles), ${r.atlases} atlases ${(r.atlasBytes / 1024 / 1024).toFixed(2)} MiB, ${r.textures} textures; ${c.entities} entities, ${c.chunksBuilt} chunks`;
  console.log(`SMOKE measure ${line}`);
  return { line, p95: at(0.95), bodies: r.bodies, cards: r.cards };
}

/**
 * The look's stages on the busiest scene, the hamlet on entry: the measurement line for painted geometry and for
 * directional cards, then the readability checks of the visual language (grayscale, reduced motion) and the day
 * at night and evening through the developer options.
 */
async function lookStages() {
  const set = (id, value) => hw(([k, v]) => window.hollowWake.setSetting(k, v), [id, value]);
  const geometry = await measure('geometry');
  record('bodies as geometry', geometry.bodies > 0 && geometry.p95 < 50, geometry.line);
  await set('shadows', false);
  await sleep(300);
  const unshadowed = await measure('geometry, no shadows');
  await set('shadows', true);
  record('bodies without shadows', unshadowed.bodies > 0, unshadowed.line);
  await set('bodies', 'cards');
  const baked = await until(async () => {
    const r = await hw(() => window.hollowWake.render);
    return r.bakesPending === 0 && r.cards > 0;
  }, 20000);
  await sleep(500);
  const cards = await measure('cards');
  await shot('16_cards.png');
  record('bodies as cards', baked && cards.cards > 0 && cards.p95 < 50, cards.line);
  await set('bodies', 'auto');
  await sleep(300);
  await set('grayscale', true);
  await sleep(300);
  await shot('17_grayscale.png');
  await set('grayscale', false);
  await set('reduce_motion', true);
  await sleep(300);
  await shot('18_reduced_motion.png');
  await set('reduce_motion', false);
  await set('day_lock', true);
  await set('day_time', 1);
  await sleep(400);
  const night = (await hw(() => window.hollowWake.render)).timeOfDay;
  await shot('19_night.png');
  await set('day_time', 18.5);
  await sleep(400);
  const evening = (await hw(() => window.hollowWake.render)).timeOfDay;
  await shot('20_evening.png');
  await set('day_lock', false);
  await sleep(300);
  const server = (await hw(() => window.hollowWake.render)).timeOfDay;
  record('day cycle', Math.abs(night - 1 / 24) < 0.01 && Math.abs(evening - 18.5 / 24) < 0.01, `night at ${night.toFixed(2)} of the day, evening at ${evening.toFixed(2)}, the server's day at ${server.toFixed(2)}`);
}

async function script() {
  await page.goto(`${url}${url.includes('?') ? '&' : '?'}account=${encodeURIComponent(account)}&profile=smoke&fresh-settings${flag('full-states') ? '&full-states' : ''}`);
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
    if (opt('calling', '')) await page.selectOption('#login-calling', opt('calling', ''));
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
  await lookStages();

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
      // Whoever F found may have opened a pane over the quartermaster: close it, then click the quartermaster.
      await closeEverything();
      await clickEntity(quartermaster.key);
      shown = await until(async () => (await counts()).questOffers > offers0 && (await ui()).questsVisible, 8000);
      how += ', then clicked the quartermaster';
    }
    const qs = await page.locator('.quest-pane .quest-button').count();
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

  await corePaneAndPages();
  await talkToAVillager();
  await tradeWithAVendor();
  await optionsSteps();

  // Weaves: walk up to the nearest hob, hover it and press the attack slots until the player's own hit lands.
  if (flag('cast-cues')) {
    const phases = new Set();
    for (let attempt = 0; attempt < 3 && !phases.has('impact'); attempt++) {
      const target = await nearest(e => typeof e.pattern === 'string' && e.pattern.startsWith('hob') && !e.dead);
      if (!target) break;
      await travelTo(target.key, 3, 45000);
      await hover(target.key);
      await page.keyboard.press('Digit1');
      const end = Date.now() + 4000;
      while (Date.now() < end) {
        const cues = await hw(() => window.hollowWake.castCues().filter(c => String(c.source) === String(window.hollowWake.self()?.key)));
        for (const cue of cues) if (!phases.has(cue.phase)) {
          phases.add(cue.phase);
          await page.screenshot({ path: join(shots, `cast_cue_${cue.phase}.png`) });
        }
        await sleep(15);
      }
    }
    record('Cast cue presentation', ['start', 'release', 'impact'].every(p => phases.has(p)), `phases: ${[...phases].join(', ')}`);
  }
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
