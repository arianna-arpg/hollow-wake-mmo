import { expect, test } from 'vitest';
import { resumeCharacter } from './quickPlay';

test('quick play resumes only a known or unambiguous character', () => {
  expect(resumeCharacter([], null)).toBeNull();
  expect(resumeCharacter(['only'], null)).toBe('only');
  expect(resumeCharacter(['first', 'last'], 'last')).toBe('last');
  expect(resumeCharacter(['first', 'last'], 'deleted')).toBeNull();
  expect(resumeCharacter(['first', 'last'], null)).toBeNull();
});
