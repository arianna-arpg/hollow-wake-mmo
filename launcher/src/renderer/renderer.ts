// The launcher page. It renders whatever state the main process sends and turns clicks into calls
// on window.launcher; it decides nothing itself. Text from the network (news, errors) only ever goes
// in through textContent.

(() => {
  const api = window.launcher;
  const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;

  const el = {
    launcherVersion: $('launcher-version'),
    selfUpdate: $('self-update'),
    selfUpdateApply: $<HTMLButtonElement>('self-update-apply'),
    overrides: $('overrides'),
    newsNote: $('news-note'),
    motd: $('motd'),
    newsList: $('news-list'),
    address: $<HTMLInputElement>('address'),
    addressError: $('address-error'),
    channelButtons: Array.from(document.querySelectorAll<HTMLButtonElement>('[data-channel]')),
    channelBlurb: $('channel-blurb'),
    installedVersion: $('installed-version'),
    remoteVersion: $('remote-version'),
    statusTitle: $('status-title'),
    statusDetail: $('status-detail'),
    statusHint: $('status-hint'),
    progress: $('progress'),
    progressFill: $('progress-fill'),
    progressText: $('progress-text'),
    primary: $<HTMLButtonElement>('primary'),
    secondary: $<HTMLButtonElement>('secondary'),
    check: $<HTMLButtonElement>('check'),
    playError: $('play-error'),
    developer: $<HTMLInputElement>('developer'),
    autoUpdate: $<HTMLInputElement>('auto-update'),
    token: $<HTMLInputElement>('token'),
    tokenSave: $<HTMLButtonElement>('token-save'),
    tokenClear: $<HTMLButtonElement>('token-clear'),
    tokenNote: $('token-note'),
    openData: $<HTMLButtonElement>('open-data'),
    dataDir: $('data-dir'),
    platform: $('platform'),
    quit: $<HTMLButtonElement>('quit'),
    logPanel: $('log-panel'),
    log: $('log'),
  };

  const CHANNEL_BLURB: Record<Channel, string> = {
    stable: 'Hand-cut releases.',
    nightly: 'Nightly pre-releases for testers: the newest build, the least tested.',
  };

  let state: LauncherState | null = null;
  let newsKey = '';

  // ------------------------------------------------------------------ rendering

  function render(s: LauncherState): void {
    state = s;
    el.launcherVersion.textContent = `Launcher ${s.launcherVersion}`;
    el.platform.textContent = s.platform;
    el.dataDir.textContent = s.dataDir;
    renderSelfUpdate(s.selfUpdate);
    el.overrides.hidden = s.overrides.length === 0;
    el.overrides.textContent = s.overrides.length ? `Test overrides in effect: ${s.overrides.join('  |  ')}` : '';
    el.overrides.title = s.overrides.join('\n');

    if (document.activeElement !== el.address) {
      el.address.value = s.settings.address;
    }
    for (const button of el.channelButtons) {
      button.setAttribute('aria-checked', String(button.dataset['channel'] === s.settings.channel));
      button.disabled = s.task.kind !== 'idle';
    }
    el.channelBlurb.textContent = s.feed.overridden ? 'The feed override decides the build; the channel does not apply.' : CHANNEL_BLURB[s.settings.channel];
    el.developer.checked = s.settings.developer;
    el.autoUpdate.checked = s.settings.autoUpdate;
    document.body.classList.toggle('developer', s.settings.developer);
    el.logPanel.hidden = !s.settings.developer;
    renderToken(s.settings.token);
    renderNews(s.news);
    renderStatus(s);
  }

  function renderSelfUpdate(u: SelfUpdateView): void {
    const text: Record<SelfUpdateView['status'], string> = {
      unsupported: 'Launcher self-update: off in this build',
      idle: '',
      checking: 'Checking for a launcher update...',
      current: 'Launcher is up to date',
      downloading: `Downloading launcher ${u.version ?? ''}: ${u.percent}%`,
      ready: '',
      error: u.message ?? 'Launcher update check failed',
    };
    el.selfUpdate.textContent = text[u.status];
    el.selfUpdate.title = u.message ?? '';
    el.selfUpdateApply.hidden = u.status !== 'ready';
    el.selfUpdateApply.textContent = `Restart to update the launcher to ${u.version ?? 'the new version'}`;
  }

  function renderToken(t: TokenView): void {
    const fromEnv = t.source === 'env';
    el.token.disabled = fromEnv;
    el.tokenSave.disabled = fromEnv;
    el.tokenClear.hidden = t.source !== 'settings';
    if (fromEnv) {
      el.tokenNote.textContent = 'Using the token from HOLLOWWAKE_GITHUB_TOKEN.';
    } else if (t.source === 'settings') {
      el.tokenNote.textContent = t.storage === 'encrypted'
        ? 'A token is saved, encrypted with the system key store.'
        : 'A token is saved unencrypted: this system offers no key store.';
    } else {
      el.tokenNote.textContent = 'Only needed while the repository is private. It is sent to the GitHub API and nowhere else.';
    }
  }

  function renderNews(n: NewsView): void {
    el.newsNote.textContent = n.note ?? '';
    el.motd.hidden = !n.motd;
    el.motd.textContent = n.motd ?? '';
    const key = JSON.stringify([n.status === 'loading', n.items]);
    if (key === newsKey) {
      return;
    }
    newsKey = key;
    el.newsList.replaceChildren();
    if (n.status === 'loading') {
      el.newsList.append(item(null, 'Loading news...', '', null));
      return;
    }
    if (n.items.length === 0) {
      el.newsList.append(item(null, 'No news yet.', '', null));
      return;
    }
    for (const entry of n.items) {
      el.newsList.append(item(entry.date, entry.title, entry.body, entry.url));
    }
  }

  function item(date: string | null, title: string, body: string, url: string | null): HTMLLIElement {
    const li = document.createElement('li');
    li.className = 'news-item';
    if (date) {
      const d = document.createElement('div');
      d.className = 'news-date';
      d.textContent = date;
      li.append(d);
    }
    const h = document.createElement('h3');
    h.className = 'news-title';
    h.textContent = title;
    li.append(h);
    if (body) {
      const p = document.createElement('p');
      p.className = 'news-body';
      p.textContent = body;
      li.append(p);
    }
    if (url) {
      const a = document.createElement('button');
      a.type = 'button';
      a.className = 'news-link';
      a.textContent = 'Read more';
      a.title = url;
      a.addEventListener('click', () => void api.openExternal(url));
      li.append(a);
    }
    return li;
  }

  function renderStatus(s: LauncherState): void {
    const remote = s.feed.remote;
    el.installedVersion.textContent = s.installed ? `${s.installed.version}` : 'none';
    el.installedVersion.title = s.installed ? `${s.installed.channel}, installed ${s.installed.installedAt.slice(0, 10)}` : '';
    el.remoteVersion.textContent = s.feed.status === 'checking' ? 'checking...' : remote ? remote.version : 'none';
    el.remoteVersion.title = remote?.tag ?? '';

    let title = '';
    let tone: '' | 'good' | 'warn' | 'bad' = '';
    let detail = '';
    let hint = '';
    const t = s.task;
    const unreachable = s.feed.status === 'unreachable';

    if (t.kind !== 'idle') {
      const what = t.version ?? '';
      title = { download: `Downloading ${what}`, verify: `Verifying ${what}`, extract: `Installing ${what}`, finish: `Finishing ${what}` }[t.kind];
      detail = t.kind === 'download' ? `${bytes(t.received)} of ${bytes(t.total)}${t.bytesPerSecond ? ` at ${bytes(t.bytesPerSecond)}/s` : ''}`
        : t.kind === 'verify' ? 'Checking the sha256 against the release manifest.'
          : t.kind === 'extract' ? `${t.received} files unpacked` : '';
    } else if (s.game.running) {
      title = 'The game is running';
      tone = 'good';
      detail = `Started for server ${s.settings.address} (pid ${s.game.pid ?? '?'}). The launcher can stay open or close.`;
    } else if (t.error) {
      title = t.error === 'The update was cancelled.' ? 'Update cancelled' : 'Update failed';
      tone = 'bad';
      detail = t.error === 'The update was cancelled.' ? '' : t.error;
      hint = s.installed ? `The installed build ${s.installed.version} still plays.` : '';
    } else {
      switch (s.action) {
        case 'install':
          title = `Ready to install ${remote?.version ?? ''}`;
          detail = remote ? `${bytes(remote.size)} download, ${remote.channel} channel.` : '';
          break;
        case 'update':
          title = `Update available: ${remote?.version ?? ''}`;
          tone = 'warn';
          detail = `Installed: ${s.installed?.version ?? 'none'}. ${remote ? `${bytes(remote.size)} download.` : ''}`;
          break;
        case 'switch':
          title = `Switch to ${remote?.channel ?? ''} ${remote?.version ?? ''}`;
          tone = 'warn';
          detail = `Installed: ${s.installed?.channel ?? ''} ${s.installed?.version ?? ''}.`;
          break;
        case 'play':
          if (unreachable) {
            title = 'Update feed unreachable';
            tone = 'warn';
            detail = s.feed.message ?? '';
            hint = [s.feed.hint, `You can still play the installed build ${s.installed?.version ?? ''}.`].filter(Boolean).join(' ');
          } else if (s.feed.message) {
            title = 'Installed build ready';
            detail = s.feed.message;
            hint = s.feed.hint ?? '';
          } else {
            title = s.feed.status === 'checking' ? 'Checking for updates...' : 'Up to date';
            tone = s.feed.status === 'checking' ? '' : 'good';
            detail = s.installed ? `${s.installed.version} (${s.installed.channel})` : '';
          }
          break;
        case 'unavailable':
          if (s.feed.status === 'checking' || s.feed.status === 'idle') {
            title = 'Checking for updates...';
          } else if (unreachable) {
            title = 'Update feed unreachable';
            tone = 'bad';
            detail = s.feed.message ?? '';
            hint = s.feed.hint ?? '';
          } else {
            title = 'Nothing to install yet';
            tone = 'warn';
            detail = s.feed.message ?? 'The feed has no build for this computer.';
            hint = s.feed.hint ?? '';
          }
          break;
        default:
          break;
      }
    }
    if (s.game.lastExit && !s.game.running && t.kind === 'idle' && !t.error && tone !== 'bad') {
      hint = [hint, s.game.lastExit].filter(Boolean).join(' ');
    }
    el.statusTitle.textContent = title;
    el.statusTitle.className = `status-title ${tone}`;
    el.statusDetail.textContent = detail;
    el.statusHint.textContent = hint;

    const showProgress = t.kind !== 'idle';
    el.progress.hidden = !showProgress;
    if (showProgress) {
      const determinate = t.kind === 'download' && t.total > 0;
      el.progress.classList.toggle('indeterminate', !determinate);
      el.progressFill.style.width = determinate ? `${Math.min(100, (t.received / t.total) * 100).toFixed(1)}%` : '';
      el.progressText.textContent = determinate ? `${Math.floor((t.received / t.total) * 100)}%` : '';
    }

    const p = el.primary;
    p.disabled = false;
    p.classList.remove('quiet');
    switch (s.action) {
      case 'install':
        p.textContent = 'Install';
        break;
      case 'update':
        p.textContent = 'Update';
        break;
      case 'switch':
        p.textContent = 'Switch';
        break;
      case 'busy':
        p.textContent = 'Cancel';
        p.classList.add('quiet');
        break;
      case 'play':
        p.textContent = s.game.running ? 'Playing' : 'Play';
        p.disabled = s.game.running;
        break;
      case 'unavailable':
        p.textContent = s.feed.status === 'checking' || s.feed.status === 'idle' ? 'Checking' : 'Retry';
        p.disabled = s.feed.status === 'checking' || s.feed.status === 'idle';
        p.classList.add('quiet');
        break;
      default:
        break;
    }
    const canPlayOld = (s.action === 'update' || s.action === 'switch') && s.installed !== null && !s.game.running;
    el.secondary.hidden = !canPlayOld;
    el.secondary.textContent = canPlayOld ? `Play ${s.installed?.version ?? ''} instead` : '';
    el.check.disabled = s.feed.status === 'checking' || t.kind !== 'idle';
    el.playError.textContent = s.game.error ?? '';
  }

  function bytes(n: number): string {
    if (!Number.isFinite(n) || n <= 0) {
      return '0 B';
    }
    const units = ['B', 'KB', 'MB', 'GB'];
    let value = n;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
      value /= 1024;
      unit++;
    }
    return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
  }

  function appendLog(line: LogLine): void {
    const row = document.createElement('div');
    row.className = line.level;
    row.textContent = `${line.at.slice(11, 19)} ${line.level.toUpperCase().padEnd(5)} ${line.text}`;
    const atBottom = el.log.scrollTop + el.log.clientHeight >= el.log.scrollHeight - 4;
    el.log.append(row);
    while (el.log.childElementCount > 400) {
      el.log.firstElementChild?.remove();
    }
    if (atBottom) {
      el.log.scrollTop = el.log.scrollHeight;
    }
  }

  // ------------------------------------------------------------------ actions

  async function primary(): Promise<void> {
    if (!state) {
      return;
    }
    el.playError.textContent = '';
    switch (state.action) {
      case 'install':
      case 'update':
      case 'switch':
        await api.install();
        break;
      case 'busy':
        await api.cancel();
        break;
      case 'play':
        await playNow();
        break;
      case 'unavailable':
        await api.checkForUpdates();
        break;
      default:
        break;
    }
  }

  async function playNow(): Promise<void> {
    if (!(await commitAddress())) {
      return;
    }
    const result = await api.play();
    if (!result.ok) {
      el.playError.textContent = result.error ?? 'The game could not start.';
    }
  }

  async function commitAddress(): Promise<boolean> {
    const result = await api.setAddress(el.address.value);
    el.address.classList.toggle('invalid', !result.ok);
    el.addressError.textContent = result.ok ? '' : result.error ?? 'That address is not valid.';
    if (result.ok && result.address) {
      el.address.value = result.address;
    }
    return result.ok;
  }

  el.primary.addEventListener('click', () => void primary());
  el.secondary.addEventListener('click', () => void playNow());
  el.check.addEventListener('click', () => void api.checkForUpdates());
  el.address.addEventListener('change', () => void commitAddress());
  el.address.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      el.address.blur();
    }
  });
  for (const button of el.channelButtons) {
    button.addEventListener('click', () => {
      const channel = button.dataset['channel'];
      if (channel === 'stable' || channel === 'nightly') {
        void api.setChannel(channel);
      }
    });
  }
  el.developer.addEventListener('change', () => void api.setDeveloper(el.developer.checked));
  el.autoUpdate.addEventListener('change', () => void api.setAutoUpdate(el.autoUpdate.checked));
  el.tokenSave.addEventListener('click', async () => {
    const result = await api.setToken(el.token.value);
    if (result.ok) {
      el.token.value = '';
    } else {
      el.tokenNote.textContent = result.error ?? 'The token was not saved.';
    }
  });
  el.tokenClear.addEventListener('click', () => void api.clearToken());
  el.openData.addEventListener('click', () => void api.openDataFolder());
  el.selfUpdateApply.addEventListener('click', () => void api.applyLauncherUpdate());
  el.quit.addEventListener('click', () => void api.quit());

  api.onState(render);
  api.onLog(appendLog);
  void api.getState().then(render);
  void api.getLog().then((lines) => lines.forEach(appendLog));
})();
