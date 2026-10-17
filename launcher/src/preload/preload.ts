// The page's only door into the main process. Runs sandboxed: nothing but these calls crosses over,
// and no Node or Electron object reaches the page.

import { contextBridge, ipcRenderer } from 'electron';

const call = (channel: string, ...args: unknown[]) => ipcRenderer.invoke(`launcher:${channel}`, ...args);

const api: LauncherApi = {
  getState: () => call('getState'),
  getLog: () => call('getLog'),
  onState: (listener) => {
    ipcRenderer.on('launcher:state', (_event, state: LauncherState) => listener(state));
  },
  onLog: (listener) => {
    ipcRenderer.on('launcher:log', (_event, line: LogLine) => listener(line));
  },
  setAddress: (address) => call('setAddress', address),
  setChannel: (channel) => call('setChannel', channel),
  setDeveloper: (on) => call('setDeveloper', on),
  setAutoUpdate: (on) => call('setAutoUpdate', on),
  setToken: (token) => call('setToken', token),
  clearToken: () => call('clearToken'),
  checkForUpdates: () => call('checkForUpdates'),
  install: () => call('install'),
  cancel: () => call('cancel'),
  play: () => call('play'),
  openExternal: (url) => call('openExternal', url),
  openDataFolder: () => call('openDataFolder'),
  applyLauncherUpdate: () => call('applyLauncherUpdate'),
  quit: () => call('quit'),
};

contextBridge.exposeInMainWorld('launcher', api);
