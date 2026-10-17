import './interface.css';
import { el } from './dom';
import type { Skin } from './skin';

/**
 * The interface's building blocks in the theme's style: a framed pane with its header (a title or a book's tabs,
 * a close glyph, a grip), buttons, wells, labels and glyphs. The look is CSS over the theme's custom properties
 * (interface.css); the frame style (stone or brass) is the interface root's data-frame. The desktop client's
 * UI/Widgets.
 */

export interface TabInfo {
  id: string;
  title: string;
  active: boolean;
  fresh: boolean;
}

export interface PaneParts {
  root: HTMLElement;
  head: HTMLElement;
  title: HTMLElement;
  tabs: HTMLElement;
  close: HTMLButtonElement | null;
  body: HTMLElement;
}

/** A framed pane: the textured ground, the frame, a header with the title (or tabs) and a close glyph, and a body. */
export function makePane(skin: Skin, title: string, options: { closable?: boolean; className?: string; draggable?: boolean } = {}): PaneParts {
  const titleEl = el('div', { class: 'pane-title', text: title });
  const tabs = el('div', { class: 'pane-tabs', role: 'tablist' });
  let close: HTMLButtonElement | null = null;
  const head = el('div', { class: `pane-head${options.draggable ? ' grip' : ''}` }, titleEl, tabs);
  if (options.closable !== false) {
    close = el('button', { class: 'pane-close', type: 'button', title: 'Close', 'aria-label': 'Close', tabindex: '-1' });
    close.append(skin.icon('close', 16));
    head.append(close);
  }
  const body = el('div', { class: 'pane-body' });
  const root = el('section', { class: `pane${options.className ? ` ${options.className}` : ''}` }, head, body);
  return { root, head, title: titleEl, tabs, close, body };
}

/** Draws a book's tab strip in a pane's header; a single leaf shows its title instead. */
export function setTabs(parts: PaneParts, title: string, tabs: readonly TabInfo[], select: (id: string) => void): void {
  parts.title.textContent = title;
  const signature = tabs.map((t) => `${t.id}:${t.title}:${t.active}:${t.fresh}`).join('|');
  if (parts.tabs.dataset.signature === signature) {
    return;
  }
  parts.tabs.dataset.signature = signature;
  parts.tabs.replaceChildren();
  parts.title.hidden = tabs.length >= 2;
  parts.tabs.hidden = tabs.length < 2;
  if (tabs.length < 2) {
    return;
  }
  for (const tab of tabs) {
    const b = el('button', { class: `tab${tab.active ? ' active' : ''}${tab.fresh ? ' fresh' : ''}`, type: 'button', role: 'tab', 'aria-selected': String(tab.active), tabindex: '-1', 'data-tab': tab.id, text: tab.title });
    b.addEventListener('click', () => select(tab.id));
    parts.tabs.append(b);
  }
}

/** A themed button. */
export function button(text: string, onClick: () => void, options: { title?: string; disabled?: boolean; className?: string; pressed?: boolean } = {}): HTMLButtonElement {
  const b = el('button', { class: `btn${options.className ? ` ${options.className}` : ''}${options.pressed ? ' pressed' : ''}`, type: 'button', tabindex: '-1', text });
  if (options.title) {
    b.title = options.title;
  }
  b.disabled = options.disabled === true;
  b.addEventListener('click', (e) => {
    e.stopPropagation();
    onClick();
  });
  return b;
}

/** A line of text in the theme's ink (muted, accent, good or bad by class). */
export function line(text: string, className = ''): HTMLElement {
  return el('div', { class: `line${className ? ` ${className}` : ''}`, text });
}

/** A section heading inside a pane. */
export function heading(text: string): HTMLElement {
  return el('h3', { class: 'pane-heading', text });
}

/** A sunken well, the ground of slots, bars and lists. */
export function well(className = '', ...children: Node[]): HTMLElement {
  return el('div', { class: `well${className ? ` ${className}` : ''}` }, ...children);
}

/** A glyph of the icon set wrapped in a span, tinted by the text colour. */
export function glyph(skin: Skin, icon: string, size: number, className = 'glyph-box'): HTMLElement {
  const span = el('span', { class: className });
  span.append(skin.icon(icon, size));
  return span;
}

/** A small number badge (pips). */
export function badge(text: string): HTMLElement {
  return el('span', { class: 'badge', text });
}

/** Prices as a player reads them: "12 Gold, 3 Embers". */
export function priceText(price: Readonly<Record<string, number>> | undefined | null, nameOf: (id: string) => string): string {
  return Object.entries(price ?? {}).map(([id, amount]) => `${Math.round(amount)} ${nameOf(id)}`).join(', ');
}
