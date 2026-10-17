// The contract between the launcher's main process and its page. Ambient (no imports or exports)
// so the page, which is a plain script, and the main process share one definition.

/** Which releases the launcher installs: hand-cut stable releases, or nightly pre-releases too. */
type Channel = 'stable' | 'nightly';

/** What the big button does right now. */
type PrimaryAction = 'install' | 'update' | 'switch' | 'play' | 'busy' | 'unavailable';

interface TokenView {
  /** Where the GitHub token comes from, if any. The token itself never reaches the page. */
  source: 'env' | 'settings' | 'none';
  /** How a token saved in settings is stored on disk. */
  storage: 'encrypted' | 'plain' | null;
}

interface SettingsView {
  address: string;
  channel: Channel;
  developer: boolean;
  autoUpdate: boolean;
  token: TokenView;
}

interface InstalledView {
  version: string;
  channel: string;
  installedAt: string;
}

interface RemoteView {
  version: string;
  channel: string;
  tag: string | null;
  pageUrl: string | null;
  publishedAt: string | null;
  /** Archive size for this platform, in bytes. */
  size: number;
}

interface FeedView {
  status: 'idle' | 'checking' | 'ready' | 'unreachable';
  /** Where builds come from, for people: the repository, or the override in use. */
  source: string;
  overridden: boolean;
  remote: RemoteView | null;
  /** Why there is nothing to install, when there is not. */
  message: string | null;
  /** What a player can do about it. */
  hint: string | null;
  checkedAt: string | null;
}

interface TaskView {
  kind: 'idle' | 'download' | 'verify' | 'extract' | 'finish';
  version: string | null;
  received: number;
  total: number;
  bytesPerSecond: number;
  /** The last install failure, kept until the next attempt. */
  error: string | null;
}

interface GameView {
  running: boolean;
  pid: number | null;
  lastExit: string | null;
  /** Why the last attempt to start the client failed, until the next attempt. */
  error: string | null;
}

interface NewsItemView {
  date: string | null;
  title: string;
  body: string;
  url: string | null;
}

interface NewsView {
  status: 'loading' | 'live' | 'cached' | 'bundled';
  motd: string | null;
  items: NewsItemView[];
  note: string | null;
}

interface SelfUpdateView {
  status: 'unsupported' | 'idle' | 'checking' | 'current' | 'downloading' | 'ready' | 'error';
  version: string | null;
  percent: number;
  message: string | null;
}

interface LauncherState {
  launcherVersion: string;
  platform: string;
  dataDir: string;
  settings: SettingsView;
  installed: InstalledView | null;
  feed: FeedView;
  task: TaskView;
  action: PrimaryAction;
  game: GameView;
  news: NewsView;
  selfUpdate: SelfUpdateView;
  /** Environment overrides in effect, as NAME=value lines (never the token). */
  overrides: string[];
}

type LogLevel = 'debug' | 'info' | 'warn' | 'error';

interface LogLine {
  at: string;
  level: LogLevel;
  text: string;
}

interface Outcome {
  ok: boolean;
  error?: string;
}

/** What the preload script exposes to the page as window.launcher. */
interface LauncherApi {
  getState(): Promise<LauncherState>;
  getLog(): Promise<LogLine[]>;
  onState(listener: (state: LauncherState) => void): void;
  onLog(listener: (line: LogLine) => void): void;
  setAddress(address: string): Promise<Outcome & { address?: string }>;
  setChannel(channel: Channel): Promise<void>;
  setDeveloper(on: boolean): Promise<void>;
  setAutoUpdate(on: boolean): Promise<void>;
  setToken(token: string): Promise<Outcome>;
  clearToken(): Promise<void>;
  checkForUpdates(): Promise<void>;
  install(): Promise<void>;
  cancel(): Promise<void>;
  play(): Promise<Outcome>;
  openExternal(url: string): Promise<void>;
  openDataFolder(): Promise<void>;
  applyLauncherUpdate(): Promise<void>;
  quit(): Promise<void>;
}

interface Window {
  launcher: LauncherApi;
}
