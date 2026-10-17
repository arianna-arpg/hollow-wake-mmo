// Starting the game client. The launcher's whole part in a session: run the installed executable
// with --host and --port from the address field, send its output to launcher/logs/client.log, and
// notice when it exits. It never talks to the game server itself.

import { spawn, type ChildProcess } from 'node:child_process';
import { closeSync, mkdirSync, openSync, readSync, statSync } from 'node:fs';
import path from 'node:path';

export interface GameLaunch {
  executable: string;
  cwd: string;
  host: string;
  port: number;
  logFile: string;
}

/** Spawn the client detached, so closing the launcher leaves the game running. Resolves once it has started. */
export function launchGame(o: GameLaunch): Promise<ChildProcess> {
  mkdirSync(path.dirname(o.logFile), { recursive: true });
  const out = openSync(o.logFile, 'w');
  return new Promise((resolve, reject) => {
    let child: ChildProcess;
    try {
      child = spawn(o.executable, ['--', '--host', o.host, '--port', String(o.port), '--quick-play'], {
        cwd: o.cwd,
        detached: true,
        stdio: ['ignore', out, out],
        windowsHide: false,
        env: gameEnvironment(process.env),
      });
    } catch (error) {
      closeSync(out);
      reject(error);
      return;
    }
    child.once('spawn', () => {
      closeSync(out);
      child.unref();
      resolve(child);
    });
    child.once('error', (error) => {
      try {
        closeSync(out);
      } catch {
        // Already closed after spawn.
      }
      reject(error);
    });
  });
}

/** The launcher's environment minus what belongs to it alone: Electron's switches and the GitHub token. */
export function gameEnvironment(env: NodeJS.ProcessEnv): NodeJS.ProcessEnv {
  const clean: NodeJS.ProcessEnv = {};
  for (const [key, value] of Object.entries(env)) {
    if (key.startsWith('ELECTRON_') || key === 'HOLLOWWAKE_GITHUB_TOKEN') {
      continue;
    }
    clean[key] = value;
  }
  return clean;
}

/** Follow a growing text file, calling onLine for each complete line. Returns a stop function. */
export function tailFile(file: string, onLine: (line: string) => void, intervalMs = 500): () => void {
  let offset = 0;
  let rest = '';
  const timer = setInterval(() => {
    let size: number;
    try {
      size = statSync(file).size;
    } catch {
      return;
    }
    if (size < offset) {
      offset = 0;
      rest = '';
    }
    if (size === offset) {
      return;
    }
    const length = Math.min(size - offset, 256 * 1024);
    const buffer = Buffer.alloc(length);
    let fd: number | null = null;
    try {
      fd = openSync(file, 'r');
      const read = readSync(fd, buffer, 0, length, offset);
      offset += read;
      const lines = (rest + buffer.subarray(0, read).toString('utf8')).split(/\r?\n/);
      rest = lines.pop() ?? '';
      for (const line of lines) {
        if (line.trim()) {
          onLine(line);
        }
      }
    } catch {
      // The file can be briefly unavailable on Windows while the game writes; try again next tick.
    } finally {
      if (fd !== null) {
        closeSync(fd);
      }
    }
  }, intervalMs);
  return () => clearInterval(timer);
}
