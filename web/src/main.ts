import './style.css';
import { App } from './app';
import { loadConfig } from './config';
import { addPlayRibbon } from './ui/playRibbon';

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

function fail(message: string): void {
  const box = document.createElement('div');
  box.className = 'fatal';
  box.textContent = message;
  root!.append(box);
}

const config = await loadConfig();
let app: App;
try {
  app = new App(root, config);
} catch (error) {
  fail(`Hollow Wake MMO needs WebGL, which this browser could not start: ${error instanceof Error ? error.message : String(error)}`);
  throw error;
}
window.hollowWake = app.probe();
void addPlayRibbon(app.login.root.querySelector('.login-panel')!);
app.start();
await app.prefill();
