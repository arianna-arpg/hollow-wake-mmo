import { describe, expect, it } from 'vitest';
import { CONTENT_FILES } from './contentFiles';
import { parseYaml, parseYamlDocuments, plainScalar, YamlError } from './yaml';

function file(suffix: string): string {
  const key = Object.keys(CONTENT_FILES).find((k) => k.endsWith(suffix));
  if (key === undefined) {
    throw new Error(`no content file ${suffix}`);
  }
  return CONTENT_FILES[key];
}

describe('yaml', () => {
  it('types plain scalars as the server loader does and keeps quoted ones as text', () => {
    expect(plainScalar('20')).toBe(20);
    expect(plainScalar('-2')).toBe(-2);
    expect(plainScalar('0.75')).toBe(0.75);
    expect(plainScalar('1e3')).toBe(1000);
    expect(plainScalar('true')).toBe(true);
    expect(plainScalar('~')).toBeNull();
    expect(plainScalar('Ember')).toBe('Ember');
    expect(parseYaml('a: "20"\nb: 20\n')).toEqual({ a: '20', b: 20 });
  });

  it('reads block mappings, sequences of mappings and flow collections', () => {
    const doc = parseYaml([
      '# a comment',
      'kind: things',
      'entries:',
      '  - { id: one, tags: [a, b], color: "#ff0000" }  # trailing',
      '  - id: two',
      '    nested:',
      '      x: 1',
      '      list:',
      '        - 1.5',
      '        - "two"',
      '    flow: { a: [1, 2],',
      '            b: { c: null } }',
      '  - three',
    ].join('\n'));
    expect(doc).toEqual({
      kind: 'things',
      entries: [
        { id: 'one', tags: ['a', 'b'], color: '#ff0000' },
        { id: 'two', nested: { x: 1, list: [1.5, 'two'] }, flow: { a: [1, 2], b: { c: null } } },
        'three',
      ],
    });
  });

  it('reads quotes, escapes and sequences at their key indentation', () => {
    const doc = parseYaml([
      "single: 'it''s # not a comment'",
      'double: "line\\nnext \\"quoted\\""',
      'list:',
      '- a',
      '- b',
      'empty:',
    ].join('\n'));
    expect(doc).toEqual({ single: "it's # not a comment", double: 'line\nnext "quoted"', list: ['a', 'b'], empty: null });
  });

  it('folds a plain scalar that runs on to deeper lines', () => {
    expect(parseYaml('description: a long line\n  that keeps going\nnext: 1')).toEqual({ description: 'a long line that keeps going', next: 1 });
  });

  it('splits documents and refuses what the content never uses', () => {
    expect(parseYamlDocuments('a: 1\n---\nb: 2\n')).toEqual([{ a: 1 }, { b: 2 }]);
    expect(() => parseYaml('a: |\n  text')).toThrow(YamlError);
    expect(() => parseYaml('a: &x 1')).toThrow(YamlError);
    expect(() => parseYaml('a: [1, 2')).toThrow(YamlError);
  });

  it('reads every bundled content file into a kind and its entries', () => {
    const names = Object.keys(CONTENT_FILES);
    expect(names.length).toBeGreaterThan(0);
    for (const name of names) {
      const doc = (name.endsWith('.json') ? JSON.parse(CONTENT_FILES[name]) : parseYaml(CONTENT_FILES[name])) as { kind?: unknown; entries?: unknown };
      expect(typeof doc.kind, name).toBe('string');
      expect(doc.entries === undefined || Array.isArray(doc.entries) || typeof doc.entries === 'object', name).toBe(true);
    }
  });

  it('reads the content as the server does, spot-checked', () => {
    const attributes = parseYaml(file('/attributes.yaml')) as { entries: Array<Record<string, unknown>> };
    const might = attributes.entries.find((e) => e.id === 'might');
    expect(might).toMatchObject({ name: 'Might', category: 'core', base: 10, description: 'Physical force. Scales attacks and armor.' });
    const grace = attributes.entries.find((e) => e.id === 'grace');
    expect(grace?.description).toBe('Speed and precision. Scales accuracy, evasion and critical strikes.');
    const themes = parseYaml(file('/ui/themes.yaml')) as { entries: Array<Record<string, unknown>> };
    expect(themes.entries.find((t) => t.default === true)?.id).toBe('etched_stone');
  });
});
