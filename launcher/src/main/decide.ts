// What the big button offers, from what is installed and what the feed holds for the chosen channel.
// An install only moves up within a channel; changing channel may move down (nightly to an older
// stable), because the player asked for that channel's build.

import { compareVersions } from './version';

export interface BuildRef {
  version: string;
  channel: string;
}

export function decideAction(installed: BuildRef | null, remote: BuildRef | null, busy: boolean): PrimaryAction {
  if (busy) {
    return 'busy';
  }
  if (!remote) {
    return installed ? 'play' : 'unavailable';
  }
  if (!installed) {
    return 'install';
  }
  const order = compareVersions(remote.version, installed.version);
  if (order > 0) {
    return 'update';
  }
  if (order < 0 && remote.channel !== installed.channel) {
    return 'switch';
  }
  return 'play';
}
