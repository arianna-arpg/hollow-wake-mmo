// The launcher log: a file under launcher/logs (rotated at a megabyte), a ring of recent lines for
// the page, and listeners that push new lines to it. Debug lines are kept only in Developer mode.

import { appendFileSync, mkdirSync, renameSync, rmSync, statSync } from 'node:fs';
import path from 'node:path';

const MAX_FILE_BYTES = 1024 * 1024;
const RING = 400;

export class Logger {
  private readonly lines: LogLine[] = [];
  private readonly listeners = new Set<(line: LogLine) => void>();
  verbose = false;

  constructor(private readonly file: string | null) {
    if (file) {
      try {
        mkdirSync(path.dirname(file), { recursive: true });
        if ((statSync(file, { throwIfNoEntry: false })?.size ?? 0) > MAX_FILE_BYTES) {
          rmSync(`${file}.1`, { force: true });
          renameSync(file, `${file}.1`);
        }
      } catch {
        // A log that cannot be written must never stop the launcher.
      }
    }
  }

  debug(text: string): void {
    this.write('debug', text);
  }

  info(text: string): void {
    this.write('info', text);
  }

  warn(text: string): void {
    this.write('warn', text);
  }

  error(text: string): void {
    this.write('error', text);
  }

  recent(): LogLine[] {
    return this.lines.filter((line) => this.verbose || line.level !== 'debug');
  }

  subscribe(listener: (line: LogLine) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private write(level: LogLevel, text: string): void {
    if (level === 'debug' && !this.verbose) {
      return;
    }
    const line: LogLine = { at: new Date().toISOString(), level, text };
    this.lines.push(line);
    if (this.lines.length > RING) {
      this.lines.splice(0, this.lines.length - RING);
    }
    if (this.file) {
      try {
        appendFileSync(this.file, `${line.at} ${level.toUpperCase().padEnd(5)} ${text}\n`, 'utf8');
      } catch {
        // See above.
      }
    }
    if (process.env['HOLLOWWAKE_LOG_STDOUT'] === '1') {
      process.stdout.write(`${level.toUpperCase().padEnd(5)} ${text}\n`);
    }
    for (const listener of this.listeners) {
      listener(line);
    }
  }
}
