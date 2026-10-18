import { expect, test } from 'vitest';
import { codespaceLink } from './playRibbon';

test('branch names are encoded in a resumable Codespace link', () => {
  expect(codespaceLink('owner/repo', 'codex/quick-play')).toBe('https://codespaces.new/owner/repo/tree/codex%2Fquick-play?quickstart=1');
  expect(codespaceLink('https://evil.invalid', 'main')).toBeNull();
  expect(codespaceLink('owner/repo', '../main')).toBeNull();
  expect(codespaceLink('owner/repo', 'main?x=y')).toBeNull();
  expect(codespaceLink('owner/repo', '')).toBeNull();
});
