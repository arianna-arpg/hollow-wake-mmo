import './style.css';
import type { App } from './app';
import { loadConfig } from './config';
import { addPlayRibbon } from './ui/playRibbon';
import { LoadingScreen } from './loading/loadingScreen';
import { presentation } from './loading/content';

declare global {
  interface Window {
    /** Read-only client state for the scripted smoke run (scripts/smoke.mjs) and the console. */
    hollowWake?: ReturnType<App['probe']>;
  }
}

const root = document.getElementById('app');
if (root === null) {
  throw new Error('#app is missing from index.html');
}

let app: App | undefined;
const loading = new LoadingScreen(() => app?.releaseLoadingInput(), () => app?.reducedLoadingMotion ?? false);
const boot = loading.begin({ kind: 'entry', label: presentation.labels.boot });
await boot.paint();
try {
  const [{ App: Client, prepareContent }, config] = await Promise.all([import('./app'), loadConfig()]);
  await prepareContent();
  if (boot.current) {
    app = new Client(root, config, loading);
    window.hollowWake = app.probe();
    void addPlayRibbon(app.login.root.querySelector('.login-panel')!);
    app.start();
    await app.prefill();
    boot.finish();
    const preview = new URLSearchParams(location.search).get('loadingPreview');
    if (preview === 'down' || preview === 'right' || preview === 'left') {
      loading.begin({ kind: 'entry', direction: preview, label: presentation.labels.boot, cancel: () => loading.close() });
    }
  }
} catch (error) {
  boot.fail(error instanceof Error ? error.message : String(error), () => location.reload());
}
