// THE LAUNCHER'S OWN UPDATE, through electron-updater's GitHub provider. It follows stable releases
// only, whatever channel the game is on: pre-releases are never offered and a newer install is never
// downgraded. The update downloads in the background, is checked against the sha512 that
// electron-builder publishes in latest.yml / latest-linux.yml, and installs on restart (or on quit,
// except in smoke and screenshot runs, which never install anything).
//
// Supported where electron-updater can replace the running copy: the NSIS install on Windows and the
// AppImage on Linux. A development run (npm start) reports that it does not self-update.
// HOLLOWWAKE_LAUNCHER_UPDATE_URL points it at a generic feed (a folder holding latest.yml and the
// installer) for trying the flow without publishing a release.

import { app } from 'electron';
import { autoUpdater, type ProgressInfo, type UpdateInfo } from 'electron-updater';
import type { Logger } from './log';

export interface SelfUpdateConfig {
  owner: string;
  repo: string;
  token: string | null;
  overrideUrl: string | null;
  /** Install a downloaded update when the launcher quits. Off for smoke and screenshot runs. */
  installOnQuit: boolean;
  log: Logger;
  onChange: (view: SelfUpdateView) => void;
}

export class SelfUpdater {
  view: SelfUpdateView = { status: 'idle', version: null, percent: 0, message: null };
  private wired = false;

  constructor(private readonly cfg: SelfUpdateConfig) {
    const unsupported = unsupportedReason();
    if (unsupported) {
      this.view = { status: 'unsupported', version: null, percent: 0, message: unsupported };
    }
  }

  get supported(): boolean {
    return this.view.status !== 'unsupported';
  }

  async check(): Promise<void> {
    if (!this.supported || this.view.status === 'checking' || this.view.status === 'downloading' || this.view.status === 'ready') {
      return;
    }
    this.wire();
    this.set({ status: 'checking', message: null });
    try {
      await autoUpdater.checkForUpdates();
    } catch (error) {
      this.fail(error);
    }
  }

  /** Quit and run the downloaded update; the launcher starts again afterwards. */
  apply(): void {
    if (this.view.status === 'ready') {
      autoUpdater.quitAndInstall(true, true);
    }
  }

  private wire(): void {
    if (this.wired) {
      return;
    }
    this.wired = true;
    const log = this.cfg.log;
    autoUpdater.logger = {
      info: (m: unknown) => log.debug(`[self-update] ${String(m)}`),
      warn: (m: unknown) => log.warn(`[self-update] ${String(m)}`),
      error: (m: unknown) => log.debug(`[self-update] ${String(m)}`),
      debug: (m: unknown) => log.debug(`[self-update] ${String(m)}`),
    };
    autoUpdater.autoDownload = true;
    autoUpdater.autoInstallOnAppQuit = this.cfg.installOnQuit;
    autoUpdater.disableWebInstaller = true;
    autoUpdater.allowPrerelease = false;
    autoUpdater.allowDowngrade = false;
    if (this.cfg.overrideUrl) {
      autoUpdater.setFeedURL({ provider: 'generic', url: this.cfg.overrideUrl });
    } else if (this.cfg.token) {
      // A token selects electron-updater's private GitHub provider (the API host, token auth).
      autoUpdater.setFeedURL({ provider: 'github', owner: this.cfg.owner, repo: this.cfg.repo, token: this.cfg.token });
    }
    autoUpdater.on('update-available', (info: UpdateInfo) => {
      log.info(`launcher update ${info.version} found; downloading`);
      this.set({ status: 'downloading', version: info.version, percent: 0, message: null });
    });
    autoUpdater.on('update-not-available', () => this.set({ status: 'current', message: null }));
    autoUpdater.on('download-progress', (p: ProgressInfo) => this.set({ status: 'downloading', percent: Math.round(p.percent) }));
    autoUpdater.on('update-downloaded', (info: UpdateInfo) => {
      log.info(`launcher update ${info.version} downloaded and verified; it installs on restart`);
      this.set({ status: 'ready', version: info.version, percent: 100, message: null });
    });
    autoUpdater.on('error', (error: Error) => this.fail(error));
  }

  private fail(error: unknown): void {
    const text = error instanceof Error ? error.message : String(error);
    let message = 'The launcher could not check for its own update.';
    if (/checksum mismatch/i.test(text)) {
      message = 'A launcher update failed verification and was discarded.';
    } else if (/\b404\b|ERR_UPDATER_LATEST_VERSION_NOT_FOUND|ERR_UPDATER_CHANNEL_FILE_NOT_FOUND/.test(text)) {
      message = this.cfg.token
        ? 'No stable launcher release is visible yet.'
        : 'No launcher release is visible: none is published yet, or the repository is private and needs a token.';
    } else if (/ENOTFOUND|EAI_AGAIN|ECONNREFUSED|ETIMEDOUT|net::ERR_/.test(text)) {
      message = 'The launcher could not reach GitHub to check for its own update.';
    } else if (/\b401\b|\b403\b/.test(text)) {
      message = 'GitHub refused the launcher update check; check the token.';
    }
    this.cfg.log.warn(`launcher self-update: ${firstLine(text)}`);
    this.set({ status: 'error', message });
  }

  private set(patch: Partial<SelfUpdateView>): void {
    this.view = { ...this.view, ...patch };
    this.cfg.onChange(this.view);
  }
}

function unsupportedReason(): string | null {
  if (!app.isPackaged) {
    return 'This is a development run; installed launchers update themselves.';
  }
  if (process.platform === 'linux' && !process.env['APPIMAGE']) {
    return 'Self-update needs the AppImage; this copy was not started from one.';
  }
  if (process.platform !== 'win32' && process.platform !== 'linux') {
    return 'Self-update is available on Windows and Linux.';
  }
  return null;
}

function firstLine(text: string): string {
  return text.split('\n')[0]?.slice(0, 300) ?? text;
}
