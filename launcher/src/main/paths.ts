// Where the launcher keeps things. One per-user root holds the installed client builds, the
// downloads in flight and the launcher's own settings, logs and caches:
//
//   Windows  %LOCALAPPDATA%\HollowWakeMMO
//   Linux    $XDG_DATA_HOME/hollowwakemmo (~/.local/share/hollowwakemmo)
//   macOS    ~/Library/Application Support/HollowWakeMMO (development only; no macOS build ships)
//
// HOLLOWWAKE_DATA_DIR moves the whole root, which is how tests and trial runs stay out of a real install.

import os from 'node:os';
import path from 'node:path';

export interface LauncherPaths {
  root: string;
  /** Installed client builds, one folder per version, plus installed.json. */
  clientDir: string;
  installedFile: string;
  /** Archives being downloaded or waiting to be extracted. */
  downloadsDir: string;
  /** The launcher's own files. */
  launcherDir: string;
  settingsFile: string;
  logsDir: string;
  logFile: string;
  clientLogFile: string;
  newsCacheFile: string;
  /** Chromium's profile for the launcher window (Electron's userData). */
  electronDir: string;
}

export function dataRoot(
  env: NodeJS.ProcessEnv = process.env,
  platform: string = process.platform,
  home: string = os.homedir(),
): string {
  if (env['HOLLOWWAKE_DATA_DIR']) {
    return path.resolve(env['HOLLOWWAKE_DATA_DIR']);
  }
  if (platform === 'win32') {
    return path.join(env['LOCALAPPDATA'] || path.join(home, 'AppData', 'Local'), 'HollowWakeMMO');
  }
  if (platform === 'darwin') {
    return path.join(home, 'Library', 'Application Support', 'HollowWakeMMO');
  }
  return path.join(env['XDG_DATA_HOME'] || path.join(home, '.local', 'share'), 'hollowwakemmo');
}

export function launcherPaths(root: string): LauncherPaths {
  const clientDir = path.join(root, 'client');
  const launcherDir = path.join(root, 'launcher');
  const logsDir = path.join(launcherDir, 'logs');
  return {
    root,
    clientDir,
    installedFile: path.join(clientDir, 'installed.json'),
    downloadsDir: path.join(root, 'downloads'),
    launcherDir,
    settingsFile: path.join(launcherDir, 'settings.json'),
    logsDir,
    logFile: path.join(logsDir, 'launcher.log'),
    clientLogFile: path.join(logsDir, 'client.log'),
    newsCacheFile: path.join(launcherDir, 'news-cache.json'),
    electronDir: path.join(launcherDir, 'electron'),
  };
}
