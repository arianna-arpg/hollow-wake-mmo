/** Resume a known character, or an unambiguous single character; never invent a character or calling. */
export function resumeCharacter(ids: string[], remembered: string | null): string | null {
  return remembered !== null && ids.includes(remembered) ? remembered : ids.length === 1 ? ids[0]! : null;
}
