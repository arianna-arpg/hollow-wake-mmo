// ---------------------------------------------------------------------------------------------
// HOLLOW WAKE MMO LAUNCHER: Electron main process.
//
// A plain game launcher. On start it reads the news, asks the release feed (GitHub Releases for the
// repository named in package.json) for the newest build on the chosen channel, compares it with the
// build installed under the per-user data folder, and installs it: download with progress, verify
// the sha256 the manifest lists, extract, switch. Play starts the installed client with --host and
// --port from the address field, online or not. The launcher holds no game logic and never talks to
// the game server. Its own updates come from electron-updater, stable releases only.
//
// Module map: feed.ts (which release), download.ts + extract.ts + installer.ts (getting it on disk),
// game.ts (starting it), news.ts, settings.ts, selfupdate.ts, decide.ts (the big button), smoke.ts.
// The page is renderer/; preload/ is its only door into this process. docs/design/launcher.md is the
// contract.
//
// Environment overrides, for testing and mirrors (all optional):
//   HOLLOWWAKE_GITHUB_TOKEN        token for a private repository (also settable in the page)
//   HOLLOWWAKE_FEED                a manifest.json (path or URL) to install from instead of GitHub
//   HOLLOWWAKE_GITHUB_API          REST root instead of https://api.github.com
//   HOLLOWWAKE_REPO                owner/name instead of package.json's repository
//   HOLLOWWAKE_NEWS                news.json (path or URL) instead of the repository's
//   HOLLOWWAKE_DATA_DIR            data folder instead of the per-user default
//   HOLLOWWAKE_LAUNCHER_UPDATE_URL generic electron-updater feed for the launcher's own update
//   HOLLOWWAKE_LOG_STDOUT=1        copy the log to stdout
// Flags: --play (start an installed game immediately; wait for installation if absent), --offline (no
// network), --smoke-test, --screenshot FILE, --screenshot-delay MS.
// ---------------------------------------------------------------------------------------------

import { app, BrowserWindow, ipcMain, safeStorage, shell, type IpcMainInvokeEvent } from 'electron';
import { readFileSync } from 'node:fs';
import { rm } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { parseAddress } from './address';
import { decideAction } from './decide';
import { FeedError, listBuilds, newsRequest, resolveRelease, type FeedConfig, type FeedRelease } from './feed';
import { launchGame, tailFile } from './game';
import { describeError, toUrl } from './http';
import { buildDir, executablePath, installRelease, pruneBuilds, readInstalled, type InstalledRecord } from './installer';
import { Logger } from './log';
import { platformKey } from './manifest';
import { loadNews } from './news';
import { dataRoot, launcherPaths } from './paths';
import { SelfUpdater } from './selfupdate';
import { loadSettings, openToken, saveSettings, sealToken, type Cipher, type Settings } from './settings';
import { runSmoke } from './smoke';

// ------------------------------------------------------------------------------- setup

const flags = parseFlags(process.argv.slice(1));
const env = process.env;
const paths = launcherPaths(dataRoot());
app.setPath('userData', paths.electronDir);
app.enableSandbox();

const log = new Logger(paths.logFile);
const PLATFORM = platformKey();
const REPOSITORY = repository();
const USER_AGENT = `HollowWakeMMO-Launcher/${app.getVersion()} (${PLATFORM})`;
const PAGE = path.join(__dirname, '..', 'renderer', 'index.html');
const BUNDLED_NEWS = path.join(__dirname, '..', 'news.json');
const ICON = path.join(__dirname, '..', 'icon.png');

let settings: Settings;
let token: string | null = null;
let tokenSource: TokenView['source'] = 'none';
let installed: InstalledRecord | null = null;
let release: FeedRelease | null = null;
let feed: Omit<FeedView, 'remote'> = { status: 'idle', source: '', overridden: false, message: null, hint: null, checkedAt: null };
let feedGeneration = 0;
const IDLE_TASK: TaskView = { kind: 'idle', version: null, received: 0, total: 0, bytesPerSecond: 0, error: null };
let task: TaskView = IDLE_TASK;
let installAbort: AbortController | null = null;
const autoAttempted = new Set<string>();
let game: GameView = { starting: false, running: false, pid: null, lastExit: null, error: null };
let builds: BuildChoice[] = [];
let buildsError: string | null = null;
let playOnReady = flags.play && !flags.smoke && !flags.screenshot;
let stopTail: (() => void) | null = null;
let news: NewsView = { status: 'loading', motd: null, items: [], note: null };
let selfUpdater: SelfUpdater;
let win: BrowserWindow | null = null;
const pageErrors: string[] = [];

const cipher: Cipher = {
  available: () => {
    if (!safeStorage.isEncryptionAvailable()) {
      return false;
    }
    // On Linux without a key ring, Electron falls back to a fixed key; that is not encryption.
    return process.platform !== 'linux' || safeStorage.getSelectedStorageBackend() !== 'basic_text';
  },
  encrypt: (text) => safeStorage.encryptString(text).toString('base64'),
  decrypt: (data) => safeStorage.decryptString(Buffer.from(data, 'base64')),
};

if (!flags.smoke && !flags.screenshot && !app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', (_event, argv) => {
    if (win) {
      if (win.isMinimized()) {
        win.restore();
      }
      win.focus();
    }
    if (argv.includes('--play') && settings && installed) void play();
  });
  app.whenReady().then(start).catch((error: unknown) => {
    log.error(`the launcher failed to start: ${describeError(error)}`);
    app.exit(1);
  });
}

app.on('window-all-closed', () => app.quit());
app.on('before-quit', () => {
  installAbort?.abort(new Error('the launcher is closing'));
  stopTail?.();
});

async function start(): Promise<void> {
  settings = await loadSettings(paths.settingsFile);
  log.verbose = settings.developer;
  resolveToken();
  log.info(`Hollow Wake MMO launcher ${app.getVersion()} on ${PLATFORM}; data in ${paths.root}`);
  for (const line of overrides()) {
    log.warn(`override in effect: ${line}`);
  }
  if (!flags.smoke && !flags.screenshot) {
    // Only this (single) instance downloads, so anything left here is from a run that was cut short.
    await rm(paths.downloadsDir, { recursive: true, force: true }).catch(() => undefined);
  }
  installed = await readInstalled(paths, (t) => log.warn(t));
  if (installed) {
    log.info(`installed build: ${installed.version} (${installed.channel})`);
    await pruneBuilds(paths, installed.dir, (t) => log.debug(t));
  } else {
    log.info('no client build is installed yet');
  }
  selfUpdater = new SelfUpdater({
    owner: REPOSITORY.owner,
    repo: REPOSITORY.repo,
    token: () => token,
    overrideUrl: env['HOLLOWWAKE_LAUNCHER_UPDATE_URL'] || null,
    installOnQuit: !flags.smoke && !flags.screenshot,
    log,
    onChange: () => push(),
  });
  log.subscribe((line) => win?.webContents.send('launcher:log', line));
  registerIpc();
  createWindow();
  // Start an installed build immediately. Feed/news requests never gate this shortcut.
  if (playOnReady && installed && !flags.smoke && !flags.screenshot) {
    playOnReady = false;
    void play();
  }
  void refreshNews();
  void checkFeed();
  if (!flags.offline && !flags.smoke) {
    setTimeout(() => void selfUpdater.check(), flags.screenshot ? 500 : 4000);
  }
}

// ------------------------------------------------------------------------------- window

function createWindow(): void {
  const showWindow = !flags.smoke || flags.screenshot !== null;
  win = new BrowserWindow({
    width: 1080,
    height: 740,
    minWidth: 900,
    minHeight: 660,
    show: false,
    title: 'Hollow Wake MMO',
    backgroundColor: '#100e16',
    icon: ICON,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, '..', 'preload', 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      spellcheck: false,
      devTools: true,
    },
  });
  win.removeMenu();
  const contents = win.webContents;
  contents.setWindowOpenHandler(() => ({ action: 'deny' }));
  contents.on('will-navigate', (event) => event.preventDefault());
  contents.on('before-input-event', (event, input) => {
    if (input.type === 'keyDown' && settings.developer && (input.key === 'F12' || (input.control && input.shift && input.key.toLowerCase() === 'i'))) {
      contents.toggleDevTools();
      event.preventDefault();
    }
  });
  contents.on('console-message', (details) => {
    if (details.level === 'error') {
      pageErrors.push(details.message);
      log.error(`page: ${details.message}`);
    }
  });
  contents.on('render-process-gone', (_event, details) => log.error(`the page process ended (${details.reason})`));
  win.once('ready-to-show', () => {
    if (showWindow) {
      win?.show();
    }
    if (settings.developer && !flags.smoke && !flags.screenshot) {
      contents.openDevTools({ mode: 'detach' });
    }
  });
  win.on('closed', () => {
    win = null;
  });
  contents.once('did-finish-load', () => {
    if (flags.smoke || flags.screenshot) {
      void runSmoke(win!, {
        checks: flags.smoke,
        screenshot: flags.screenshot,
        delayMs: flags.screenshotDelay,
        version: app.getVersion(),
        settled: () => feed.status !== 'checking' && feed.status !== 'idle' && news.status !== 'loading',
        pageErrors,
      })
        .catch((error: unknown) => {
          process.stdout.write(`SMOKE launcher FAIL: ${describeError(error)}\n`);
          return 1;
        })
        .then((code) => app.exit(code));
    }
  });
  void win.loadFile(PAGE);
}

// ------------------------------------------------------------------------------- state

let pushTimer: NodeJS.Timeout | null = null;

/** Send the state to the page, coalescing bursts (download progress) into one message. */
function push(): void {
  if (pushTimer) {
    return;
  }
  pushTimer = setTimeout(() => {
    pushTimer = null;
    win?.webContents.send('launcher:state', snapshot());
  }, 40);
}

function snapshot(): LauncherState {
  const remote = remoteView();
  return {
    builds,
    buildsError,
    launcherVersion: app.getVersion(),
    platform: PLATFORM,
    dataDir: paths.root,
    settings: {
      address: settings.address,
      channel: settings.channel,
      developer: settings.developer,
      autoUpdate: settings.autoUpdate,
      releaseTag: settings.releaseTag,
      token: { source: tokenSource, storage: settings.token ? (settings.token.encrypted ? 'encrypted' : 'plain') : null },
    },
    installed: installed ? { version: installed.version, channel: installed.channel, installedAt: installed.installedAt } : null,
    feed: { ...feed, remote },
    task,
    action: decideAction(installed, remote, task.kind !== 'idle', !!settings.releaseTag),
    game,
    news,
    selfUpdate: selfUpdater.view,
    overrides: overrides(),
  };
}

function remoteView(): RemoteView | null {
  if (!release) {
    return null;
  }
  const build = release.manifest.platforms[PLATFORM];
  if (!build || !release.archives[PLATFORM]) {
    return null;
  }
  return {
    version: release.manifest.version,
    channel: release.manifest.channel,
    tag: release.tag,
    pageUrl: release.pageUrl,
    publishedAt: release.publishedAt,
    size: build.size,
  };
}

// ------------------------------------------------------------------------------- feed

function feedConfig(): FeedConfig {
  return {
    owner: REPOSITORY.owner,
    repo: REPOSITORY.repo,
    api: (env['HOLLOWWAKE_GITHUB_API'] || 'https://api.github.com').replace(/\/+$/, ''),
    override: env['HOLLOWWAKE_FEED'] || null,
    token,
    userAgent: USER_AGENT,
    timeoutMs: 15_000,
    tag: settings.releaseTag,
  };
}

function feedSource(cfg: FeedConfig): string {
  return cfg.override ? `override ${toUrl(cfg.override).href}` : `GitHub Releases, ${cfg.owner}/${cfg.repo}`;
}

async function checkFeed(): Promise<void> {
  const generation = ++feedGeneration;
  const cfg = feedConfig();
  feed = { ...feed, status: 'checking', source: feedSource(cfg), overridden: !!cfg.override, message: null, hint: null };
  release = null;
  buildsError = null;
  push();
  if (flags.offline) {
    feed = { ...feed, status: 'unreachable', message: 'Offline mode: the release feed was not checked.', hint: null, checkedAt: new Date().toISOString() };
    push();
    maybePlayOnStart();
    return;
  }
  log.debug(`checking ${feed.source} on the ${settings.channel} channel`);
  // Catalogue failure must not hold up the selected build or an already installed client.
  void listBuilds(cfg, settings.channel, fetch).then(found => {
    if (generation === feedGeneration) { builds = found; push(); }
  }).catch(error => {
    if (generation === feedGeneration) { buildsError = describeError(error); push(); }
  });
  try {
    const found = await resolveRelease(cfg, settings.channel, fetch);
    if (generation !== feedGeneration) {
      return;
    }
    release = found;
    const { manifest } = found;
    const tagVersion = found.tag ? found.tag.replace(/^v/, '') : null;
    if (tagVersion && tagVersion !== manifest.version) {
      log.warn(`release ${found.tag} carries a manifest for ${manifest.version}; the manifest wins`);
    }
    const build = manifest.platforms[PLATFORM];
    const missing = !build ? `Release ${manifest.version} has no build for ${PLATFORM}.` : !found.archives[PLATFORM] ? `Release ${manifest.version} lists ${build.file} but does not carry it.` : null;
    feed = {
      ...feed,
      status: 'ready',
      message: missing,
      hint: missing ? `Builds in it: ${Object.keys(manifest.platforms).join(', ')}.` : null,
      checkedAt: new Date().toISOString(),
    };
    log.info(`feed: ${manifest.version} (${manifest.channel}${found.tag ? `, ${found.tag}` : ''})${missing ? `; ${missing}` : ''}`);
  } catch (error) {
    if (generation !== feedGeneration) {
      return;
    }
    const known = error instanceof FeedError;
    feed = {
      ...feed,
      status: 'unreachable',
      message: known ? error.message : `The release feed failed: ${describeError(error)}.`,
      hint: known ? error.hint : null,
      checkedAt: new Date().toISOString(),
    };
    log.warn(`feed unreachable: ${feed.message}${feed.hint ? ` ${feed.hint}` : ''}`);
  }
  push();
  maybeAutoInstall();
  maybePlayOnStart();
}

/** --play with no installed build: start after the initial check and automatic installation. */
function maybePlayOnStart(): void {
  if (!playOnReady || feed.status === 'checking' || feed.status === 'idle' || task.kind !== 'idle') {
    return;
  }
  playOnReady = false;
  if (installed) {
    void play();
  } else {
    log.warn('--play: no build is installed, so there is nothing to start');
  }
}

function maybeAutoInstall(): void {
  const remote = remoteView();
  if (!settings.autoUpdate || settings.releaseTag || game.running || game.starting || flags.smoke || !remote || autoAttempted.has(remote.version)) {
    return;
  }
  const action = decideAction(installed, remote, task.kind !== 'idle');
  if (action === 'install' || action === 'update' || action === 'switch') {
    autoAttempted.add(remote.version);
    log.info(`${action === 'install' ? 'installing' : 'updating to'} ${remote.version} automatically`);
    void startInstall();
  }
}

// ------------------------------------------------------------------------------- install

async function startInstall(): Promise<void> {
  const target = release;
  const remote = remoteView();
  if (task.kind !== 'idle' || game.running || game.starting || !target || !remote || remote.version === installed?.version) {
    return;
  }
  installAbort = new AbortController();
  const signal = installAbort.signal;
  const meter = speedMeter();
  task = { kind: 'download', version: remote.version, received: 0, total: remote.size, bytesPerSecond: 0, error: null };
  push();
  try {
    const record = await installRelease({
      release: target,
      platform: PLATFORM,
      paths,
      fetchImpl: fetch,
      signal,
      // Play remains available during downloads. Keep the previous build until the next startup;
      // pruning based on game.running at download start races a later click on Play.
      prune: false,
      log: (t) => log.info(t),
      onProgress: (p) => {
        task = {
          ...task,
          kind: p.phase,
          received: p.received,
          total: p.total,
          bytesPerSecond: p.phase === 'download' ? meter(p.received) : 0,
        };
        push();
      },
    });
    installed = record;
    task = IDLE_TASK;
    log.info(`ready to play ${record.version}`);
  } catch (error) {
    const cancelled = signal.aborted;
    const reason = describeError(error);
    task = { ...IDLE_TASK, error: cancelled ? 'The update was cancelled.' : `${reason.charAt(0).toUpperCase()}${reason.slice(1)}.` };
    if (cancelled) {
      log.info('the update was cancelled');
    } else {
      log.error(`the update failed: ${reason}`);
    }
  } finally {
    installAbort = null;
    push();
    maybePlayOnStart();
  }
}

/** Bytes per second over the last few seconds of progress reports. */
function speedMeter(): (received: number) => number {
  const samples: Array<{ at: number; received: number }> = [];
  return (received) => {
    const now = Date.now();
    samples.push({ at: now, received });
    while (samples.length > 2 && now - (samples[0]?.at ?? now) > 3000) {
      samples.shift();
    }
    const first = samples[0];
    const span = first ? (now - first.at) / 1000 : 0;
    return first && span > 0.2 ? Math.round((received - first.received) / span) : 0;
  };
}

// ------------------------------------------------------------------------------- play

/** Start the installed build; a failure is kept in the state so the page shows it whoever asked. */
async function play(): Promise<Outcome> {
  const outcome = await startGame();
  game = { ...game, error: outcome.ok ? null : outcome.error ?? null };
  push();
  return outcome;
}

async function startGame(): Promise<Outcome> {
  if (!installed) {
    return { ok: false, error: 'No game build is installed yet.' };
  }
  if (game.running || game.starting) {
    return { ok: false, error: 'The game is already running.' };
  }
  const parsed = parseAddress(settings.address);
  if (!parsed.ok) {
    return { ok: false, error: parsed.error };
  }
  const build = installed;
  game = { ...game, starting: true, error: null };
  push();
  try {
    const child = await launchGame({
      executable: executablePath(paths, build),
      cwd: buildDir(paths, build),
      host: parsed.address.host,
      port: parsed.address.port,
      logFile: paths.clientLogFile,
    });
    game = { starting: false, running: true, pid: child.pid ?? null, lastExit: game.lastExit, error: null };
    log.info(`started the client ${build.version} (pid ${child.pid}) for ${parsed.address.text}`);
    if (settings.developer) {
      stopTail = tailFile(paths.clientLogFile, (line) => log.info(`[client] ${line}`));
    }
    child.once('exit', (code, signal) => {
      stopTail?.();
      stopTail = null;
      const how = signal ? `was stopped (${signal})` : `exited with code ${code}`;
      game = { starting: false, running: false, pid: null, lastExit: `The client ${how}.`, error: null };
      const line = `the client ${how}; its output is in ${paths.clientLogFile}`;
      if (code === 0) {
        log.info(line);
      } else {
        log.warn(line);
      }
      push();
    });
    push();
    return { ok: true };
  } catch (error) {
    game = { ...game, starting: false };
    const message = `The client could not start: ${describeError(error)}.`;
    log.error(message);
    return { ok: false, error: message };
  }
}

// ------------------------------------------------------------------------------- news

async function refreshNews(): Promise<void> {
  const request = flags.offline ? null : newsRequest(feedConfig(), env['HOLLOWWAKE_NEWS'] || null);
  news = await loadNews({
    url: request ? new URL(request.url) : null,
    headers: request?.headers ?? {},
    cacheFile: paths.newsCacheFile,
    bundledFile: BUNDLED_NEWS,
    timeoutMs: 10_000,
    fetchImpl: fetch,
  });
  if (news.note) {
    log.info(news.note);
  }
  push();
}

// ------------------------------------------------------------------------------- settings

function resolveToken(): void {
  const fromEnv = env['HOLLOWWAKE_GITHUB_TOKEN']?.trim();
  if (fromEnv) {
    token = fromEnv;
    tokenSource = 'env';
    return;
  }
  token = openToken(settings.token, cipher);
  tokenSource = token ? 'settings' : 'none';
  if (settings.token && !token) {
    log.warn('the saved GitHub token could not be decrypted on this machine; enter it again');
  }
}

async function persist(): Promise<void> {
  try {
    await saveSettings(paths.settingsFile, settings);
  } catch (error) {
    log.error(`could not save settings: ${describeError(error)}`);
  }
}

function overrides(): string[] {
  const names = ['HOLLOWWAKE_FEED', 'HOLLOWWAKE_GITHUB_API', 'HOLLOWWAKE_REPO', 'HOLLOWWAKE_NEWS', 'HOLLOWWAKE_DATA_DIR', 'HOLLOWWAKE_LAUNCHER_UPDATE_URL'];
  const lines = names.filter((n) => env[n]).map((n) => `${n}=${env[n]}`);
  if (env['HOLLOWWAKE_GITHUB_TOKEN']) {
    lines.push('HOLLOWWAKE_GITHUB_TOKEN is set');
  }
  return lines;
}

function repository(): { owner: string; repo: string } {
  const fromEnv = env['HOLLOWWAKE_REPO'];
  let text = fromEnv ?? '';
  if (!text) {
    try {
      const pkg = JSON.parse(readFileSync(path.join(app.getAppPath(), 'package.json'), 'utf8')) as { repository?: string | { url?: string } };
      text = typeof pkg.repository === 'string' ? pkg.repository : pkg.repository?.url ?? '';
    } catch {
      text = '';
    }
  }
  const match = /([A-Za-z0-9_.-]+)\/([A-Za-z0-9_.-]+?)(\.git)?$/.exec(text.replace(/^github:/, ''));
  return match ? { owner: match[1] ?? '', repo: match[2] ?? '' } : { owner: 'arianna-arpg', repo: 'hollow-wake-mmo' };
}

// ------------------------------------------------------------------------------- the page's requests

function registerIpc(): void {
  const handle = <A extends unknown[], R>(channel: string, fn: (...args: A) => R | Promise<R>) => {
    ipcMain.handle(`launcher:${channel}`, (event: IpcMainInvokeEvent, ...args: unknown[]) => {
      if (!fromOurPage(event)) {
        throw new Error('refused: not the launcher page');
      }
      return fn(...(args as A));
    });
  };

  handle('getState', () => snapshot());
  handle('getLog', () => log.recent());

  handle('selectBuild', async (tag: unknown) => {
    if (task.kind !== 'idle') return { ok: false, error: 'Wait for the download or cancel it before choosing another build.' };
    if (tag !== null && (typeof tag !== 'string' || !builds.some(b => b.tag === tag))) {
      return { ok: false, error: 'Choose a published build from the list.' };
    }
    settings.releaseTag = tag as string | null;
    // Invalidate synchronously: a click on Install while settings are saving must not install
    // the release that belonged to the previous selection.
    invalidateFeed();
    await persist();
    void checkFeed();
    return { ok: true };
  });

  handle('setAddress', async (input: unknown) => {
    const parsed = parseAddress(typeof input === 'string' ? input : '');
    if (!parsed.ok) {
      return { ok: false, error: parsed.error };
    }
    if (settings.address !== parsed.address.text) {
      settings.address = parsed.address.text;
      await persist();
      log.info(`server address set to ${settings.address}`);
      push();
    }
    return { ok: true, address: settings.address };
  });

  handle('setChannel', async (input: unknown) => {
    if (task.kind !== 'idle' || (input !== 'stable' && input !== 'nightly') || input === settings.channel) {
      return;
    }
    settings.channel = input;
    settings.releaseTag = null;
    builds = [];
    invalidateFeed();
    await persist();
    log.info(`channel set to ${input}`);
    void checkFeed();
  });

  handle('setDeveloper', async (on: unknown) => {
    settings.developer = on === true;
    log.verbose = settings.developer;
    await persist();
    log.info(`Developer mode ${settings.developer ? 'on: DevTools (F12) and verbose logs' : 'off'}`);
    if (win) {
      if (settings.developer) {
        win.webContents.openDevTools({ mode: 'detach' });
      } else {
        win.webContents.closeDevTools();
      }
    }
    push();
  });

  handle('setAutoUpdate', async (on: unknown) => {
    settings.autoUpdate = on === true;
    await persist();
    push();
    maybeAutoInstall();
  });

  handle('setToken', async (input: unknown) => {
    const value = typeof input === 'string' ? input.trim() : '';
    if (!/^[A-Za-z0-9_.-]{20,255}$/.test(value)) {
      return { ok: false, error: 'That does not look like a GitHub token.' };
    }
    settings.token = sealToken(value, cipher);
    await persist();
    resolveToken();
    log.info(`GitHub token saved (${settings.token.encrypted ? 'encrypted' : 'unencrypted: no key store is available'})`);
    void checkFeed();
    void refreshNews();
    void selfUpdater.check();
    return { ok: true };
  });

  handle('clearToken', async () => {
    settings.token = null;
    await persist();
    resolveToken();
    log.info('GitHub token removed');
    void checkFeed();
    void refreshNews();
  });

  handle('checkForUpdates', () => {
    autoAttempted.clear();
    void checkFeed();
    void refreshNews();
    void selfUpdater.check();
  });

  handle('install', () => void startInstall());
  handle('cancel', () => installAbort?.abort(new Error('cancelled')));
  handle('play', () => play());

  handle('openExternal', async (url: unknown) => {
    if (typeof url === 'string' && /^https:\/\//.test(url)) {
      await shell.openExternal(url);
    }
  });
  handle('openDataFolder', async () => {
    await shell.openPath(paths.root);
  });
  handle('applyLauncherUpdate', () => selfUpdater.apply());
  handle('quit', () => app.quit());
}

function invalidateFeed(): void {
  feedGeneration++;
  release = null;
  feed = { ...feed, status: 'checking', message: null, hint: null };
  push();
}

function fromOurPage(event: IpcMainInvokeEvent): boolean {
  try {
    const normal = (url: string) => decodeURIComponent(url).replace(/[?#].*$/, '').toLowerCase();
    return normal(event.senderFrame?.url ?? '') === normal(pathToFileURL(PAGE).href);
  } catch {
    return false;
  }
}

// ------------------------------------------------------------------------------- flags

interface Flags {
  smoke: boolean;
  play: boolean;
  offline: boolean;
  screenshot: string | null;
  screenshotDelay: number;
}

function parseFlags(args: string[]): Flags {
  const value = (name: string): string | null => {
    for (let i = 0; i < args.length; i++) {
      const arg = args[i] ?? '';
      if (arg === name) {
        return args[i + 1] ?? null;
      }
      if (arg.startsWith(`${name}=`)) {
        return arg.slice(name.length + 1);
      }
    }
    return null;
  };
  const screenshot = value('--screenshot');
  return {
    smoke: args.some((a) => a === '--smoke-test' || a.startsWith('--smoke-test=')),
    play: args.includes('--play'),
    offline: args.includes('--offline'),
    screenshot: screenshot ? path.resolve(screenshot) : null,
    screenshotDelay: Number(value('--screenshot-delay')) || 0,
  };
}
