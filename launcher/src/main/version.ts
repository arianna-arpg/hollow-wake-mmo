// Versions are semver. Stable releases are X.Y.Z; nightly pre-releases are X.Y.Z-nightly.DATE.RUN,
// which sort below X.Y.Z and above every earlier release, so a nightly never outranks the stable it
// leads up to and a stable always supersedes its nightlies.

import semver from 'semver';

/** A version string from a tag, a manifest or an install record, normalised; null when not semver. */
export function parseVersion(text: unknown): string | null {
  if (typeof text !== 'string') {
    return null;
  }
  const cleaned = semver.clean(text.trim().replace(/^v/i, ''));
  return cleaned && semver.valid(cleaned) ? cleaned : null;
}

/** Negative when a is older than b, zero when equal, positive when newer. */
export function compareVersions(a: string, b: string): number {
  return semver.compare(a, b);
}

/** The channel a version belongs to, judged from its shape. */
export function channelOfVersion(version: string): Channel {
  return (semver.prerelease(version) ?? []).some((part) => String(part).toLowerCase() === 'nightly') ? 'nightly' : 'stable';
}
