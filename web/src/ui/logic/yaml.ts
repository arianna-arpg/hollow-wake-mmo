/**
 * The YAML the content is written in, read in the browser: block mappings and sequences, flow mappings and
 * sequences (which may span lines), plain, single- and double-quoted scalars, comments and document markers.
 * Scalars are typed as the server's loader types them (YamlToJson): plain scalars become numbers, booleans or
 * null where they parse as such; quoted scalars always stay strings. Anchors, tags and block scalars are not
 * used by the content and are refused.
 */

export type YamlValue = null | boolean | number | string | YamlValue[] | { [key: string]: YamlValue };

interface Line {
  indent: number;
  text: string;
  no: number;
}

export class YamlError extends Error {}

/** Every document in a text. */
export function parseYamlDocuments(text: string): YamlValue[] {
  const docs: Line[][] = [[]];
  text.split(/\r?\n/).forEach((raw, i) => {
    if (/^(---|\.\.\.)\s*(#.*)?$/.test(raw)) {
      docs.push([]);
      return;
    }
    const stripped = stripComment(raw);
    if (stripped.trim().length === 0) {
      return;
    }
    const indent = stripped.length - stripped.trimStart().length;
    if (stripped.slice(0, indent).includes('\t')) {
      throw new YamlError(`line ${i + 1}: tabs are not allowed for indentation`);
    }
    docs[docs.length - 1].push({ indent, text: stripped.trimEnd().slice(indent), no: i + 1 });
  });
  return docs.filter((d) => d.length > 0).map((lines) => new Parser(lines).document());
}

/** The first document of a text, or null when it has none. */
export function parseYaml(text: string): YamlValue {
  return parseYamlDocuments(text)[0] ?? null;
}

/** Removes a comment: a # at the start or after whitespace, outside quotes. */
function stripComment(line: string): string {
  let quote: string | null = null;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (quote !== null) {
      if (c === '\\' && quote === '"') {
        i++;
      } else if (c === quote) {
        if (quote === "'" && line[i + 1] === "'") {
          i++;
        } else {
          quote = null;
        }
      }
      continue;
    }
    if (c === '"' || c === "'") {
      // A quote starts a quoted scalar only where a scalar can start.
      const before = line.slice(0, i).trimEnd();
      if (before === '' || /[:,[{-]$/.test(before) || before.endsWith('- ')) {
        quote = c;
      }
      continue;
    }
    if (c === '#' && (i === 0 || line[i - 1] === ' ' || line[i - 1] === '\t')) {
      return line.slice(0, i);
    }
  }
  return line;
}

/** Types a plain scalar as the server's loader does. */
export function plainScalar(v: string): YamlValue {
  switch (v) {
    case '':
    case '~':
    case 'null':
    case 'Null':
    case 'NULL':
      return null;
    case 'true':
    case 'True':
    case 'TRUE':
      return true;
    case 'false':
    case 'False':
    case 'FALSE':
      return false;
  }
  if (/^[-+]?\d+$/.test(v)) {
    return Number(v);
  }
  if (/^0x[0-9a-fA-F]+$/.test(v)) {
    return parseInt(v.slice(2), 16);
  }
  if (/^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(v)) {
    return Number(v);
  }
  return v;
}

class Parser {
  private i = 0;

  constructor(private readonly lines: Line[]) {}

  document(): YamlValue {
    const value = this.block(this.lines[0].indent);
    if (this.i < this.lines.length) {
      throw new YamlError(`line ${this.lines[this.i].no}: unexpected indentation`);
    }
    return value;
  }

  private get line(): Line | undefined {
    return this.lines[this.i];
  }

  private block(indent: number): YamlValue {
    const line = this.line;
    if (line === undefined || line.indent < indent) {
      return null;
    }
    return isSeqItem(line.text) ? this.sequence(line.indent) : this.mapping(line.indent);
  }

  private sequence(indent: number): YamlValue[] {
    const items: YamlValue[] = [];
    while (this.line !== undefined && this.line.indent === indent && isSeqItem(this.line.text)) {
      const line = this.line;
      const rest = line.text.slice(1).trimStart();
      const column = line.indent + (line.text.length - rest.length);
      if (rest.length === 0) {
        this.i++;
        items.push(this.block(indent + 1));
        continue;
      }
      if (isSeqItem(rest)) {
        // "- - x": a nested sequence starting inline; re-read the rest as its own line.
        this.lines[this.i] = { indent: column, text: rest, no: line.no };
        items.push(this.sequence(column));
        continue;
      }
      if (startsFlow(rest) || isQuoted(rest) || keyOf(rest) === null) {
        this.i++;
        items.push(this.inlineValue(rest, line, indent));
        continue;
      }
      // "- key: value": a mapping whose keys stand at the column after the dash.
      this.lines[this.i] = { indent: column, text: rest, no: line.no };
      items.push(this.mapping(column));
    }
    return items;
  }

  private mapping(indent: number): { [key: string]: YamlValue } {
    const map: { [key: string]: YamlValue } = {};
    while (this.line !== undefined && this.line.indent === indent && !isSeqItem(this.line.text)) {
      const line = this.line;
      const kv = keyOf(line.text);
      if (kv === null) {
        throw new YamlError(`line ${line.no}: expected "key: value"`);
      }
      const [key, rest] = kv;
      this.i++;
      if (rest.length === 0) {
        const next = this.line;
        // A sequence may sit at the same indentation as its key.
        if (next !== undefined && (next.indent > indent || (next.indent === indent && isSeqItem(next.text)))) {
          map[key] = this.block(next.indent);
        } else {
          map[key] = null;
        }
        continue;
      }
      map[key] = this.inlineValue(rest, line, indent);
    }
    return map;
  }

  /** A value on the line of its key or dash: a flow collection, a quoted scalar or a plain one (which may run on). */
  private inlineValue(rest: string, line: Line, parentIndent: number): YamlValue {
    if (rest.startsWith('|') || rest.startsWith('>')) {
      throw new YamlError(`line ${line.no}: block scalars are not supported`);
    }
    if (rest.startsWith('&') || rest.startsWith('*') || rest.startsWith('!')) {
      throw new YamlError(`line ${line.no}: anchors, aliases and tags are not supported`);
    }
    if (startsFlow(rest)) {
      let text = rest;
      while (!balanced(text)) {
        const next = this.line;
        if (next === undefined) {
          throw new YamlError(`line ${line.no}: unclosed flow collection`);
        }
        text += ' ' + next.text;
        this.i++;
      }
      const flow = new Flow(text, line.no);
      const value = flow.value();
      flow.end();
      return value;
    }
    if (isQuoted(rest)) {
      const flow = new Flow(rest, line.no);
      const value = flow.quoted();
      flow.end();
      return value;
    }
    // A plain scalar continues on more deeply indented lines, folded with spaces.
    let text = rest;
    while (this.line !== undefined && this.line.indent > parentIndent && keyOf(this.line.text) === null && !isSeqItem(this.line.text)) {
      text += ' ' + this.line.text.trim();
      this.i++;
    }
    return plainScalar(text.trim());
  }
}

function isSeqItem(text: string): boolean {
  return text === '-' || text.startsWith('- ');
}

function startsFlow(text: string): boolean {
  return text.startsWith('{') || text.startsWith('[');
}

function isQuoted(text: string): boolean {
  return text.startsWith('"') || text.startsWith("'");
}

/** Splits "key: rest" (plain or quoted key); null when the line is not a key. */
function keyOf(text: string): [string, string] | null {
  if (isQuoted(text)) {
    const flow = new Flow(text, 0);
    const key = flow.quoted();
    const after = text.slice(flow.position).trimStart();
    return after.startsWith(':') ? [key, after.slice(1).trim()] : null;
  }
  const m = /^([^\s:[\]{},#][^:]*?)\s*:(\s|$)/.exec(text);
  if (m === null) {
    return null;
  }
  return [m[1], text.slice(m[0].length).trim()];
}

/** Whether every bracket outside quotes is closed. A quote opens only where a scalar starts (not in "Hob's"). */
function balanced(text: string): boolean {
  let depth = 0;
  let quote: string | null = null;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quote !== null) {
      if (c === '\\' && quote === '"') {
        i++;
      } else if (c === quote) {
        if (quote === "'" && text[i + 1] === "'") {
          i++;
        } else {
          quote = null;
        }
      }
      continue;
    }
    if (c === '"' || c === "'") {
      const before = text.slice(0, i).trimEnd();
      if (before === '' || /[:,[{]$/.test(before)) {
        quote = c;
      }
    } else if (c === '{' || c === '[') {
      depth++;
    } else if (c === '}' || c === ']') {
      depth--;
    }
  }
  return depth <= 0;
}

/** A scanner over flow text: collections, quoted and plain scalars. */
class Flow {
  position = 0;

  constructor(private readonly text: string, private readonly no: number) {}

  private error(message: string): YamlError {
    return new YamlError(`line ${this.no}: ${message}`);
  }

  private ws(): void {
    while (this.position < this.text.length && /\s/.test(this.text[this.position])) {
      this.position++;
    }
  }

  end(): void {
    this.ws();
    if (this.position < this.text.length) {
      throw this.error(`unexpected "${this.text.slice(this.position)}"`);
    }
  }

  value(): YamlValue {
    this.ws();
    const c = this.text[this.position];
    if (c === '{') {
      return this.map();
    }
    if (c === '[') {
      return this.seq();
    }
    if (c === '"' || c === "'") {
      return this.quoted();
    }
    return plainScalar(this.plain(false));
  }

  private map(): { [key: string]: YamlValue } {
    const map: { [key: string]: YamlValue } = {};
    this.position++;
    for (;;) {
      this.ws();
      if (this.text[this.position] === '}') {
        this.position++;
        return map;
      }
      const c = this.text[this.position];
      const key = c === '"' || c === "'" ? this.quoted() : this.plain(true);
      this.ws();
      if (this.text[this.position] !== ':') {
        throw this.error(`expected ":" after "${key}"`);
      }
      this.position++;
      this.ws();
      const next = this.text[this.position];
      map[key] = next === ',' || next === '}' ? null : this.value();
      this.ws();
      if (this.text[this.position] === ',') {
        this.position++;
      } else if (this.text[this.position] !== '}') {
        throw this.error('expected "," or "}"');
      }
    }
  }

  private seq(): YamlValue[] {
    const items: YamlValue[] = [];
    this.position++;
    for (;;) {
      this.ws();
      if (this.text[this.position] === ']') {
        this.position++;
        return items;
      }
      items.push(this.value());
      this.ws();
      if (this.text[this.position] === ',') {
        this.position++;
      } else if (this.text[this.position] !== ']') {
        throw this.error('expected "," or "]"');
      }
    }
  }

  quoted(): string {
    const q = this.text[this.position];
    let out = '';
    this.position++;
    while (this.position < this.text.length) {
      const c = this.text[this.position];
      if (q === '"' && c === '\\') {
        const e = this.text[this.position + 1];
        out += e === 'n' ? '\n' : e === 't' ? '\t' : e === 'r' ? '\r' : e === '0' ? '\0' : e;
        this.position += 2;
        continue;
      }
      if (c === q) {
        if (q === "'" && this.text[this.position + 1] === "'") {
          out += "'";
          this.position += 2;
          continue;
        }
        this.position++;
        return out;
      }
      out += c;
      this.position++;
    }
    throw this.error('unclosed quoted scalar');
  }

  /** A plain scalar in flow: up to a comma or a closing bracket (and a colon, for keys). */
  private plain(key: boolean): string {
    const start = this.position;
    while (this.position < this.text.length) {
      const c = this.text[this.position];
      if (c === ',' || c === '}' || c === ']' || (key && c === ':')) {
        break;
      }
      if (!key && c === ':' && /\s/.test(this.text[this.position + 1] ?? ' ')) {
        throw this.error('a plain value in a flow collection cannot hold ": "');
      }
      this.position++;
    }
    return this.text.slice(start, this.position).trim();
  }
}
