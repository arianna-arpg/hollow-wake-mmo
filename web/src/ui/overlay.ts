import type { EntityState } from '../net/protocol';
import { clear, el } from './dom';
import { lookOf } from './logic/content';
import type { Box } from './logic/layout';
import { css, type Skin } from './skin';
import './cluster.css';
import './overlay.css';

/**
 * What floats over the world around the cluster: the target's plate at the top centre, notices under it and
 * combat text above the hero, the chat in the lower left corner, and the connection overlay. The desktop
 * client's UI/Overlay.
 */

/** The target line and plate: the name in its rarity's colour, its level and use, its life and ward, its statuses. */
export class TargetPlate {
  readonly root: HTMLElement;
  private readonly name: HTMLElement;
  private readonly use: HTMLElement;
  private readonly bar: HTMLElement;
  private readonly life: HTMLElement;
  private readonly ward: HTMLElement;
  private readonly lifeText: HTMLElement;
  private readonly statuses: HTMLElement;
  private target: EntityState | null = null;
  private signature = '';

  constructor(private readonly skin: Skin) {
    this.name = el('div', { class: 'plate-name' });
    this.use = el('div', { class: 'plate-use muted' });
    this.life = el('div', { class: 'plate-life' });
    this.ward = el('div', { class: 'plate-ward' });
    this.lifeText = el('div', { class: 'plate-life-text num' });
    this.bar = el('div', { class: 'plate-bar' }, this.life, this.ward, this.lifeText);
    this.statuses = el('div', { class: 'plate-statuses' });
    this.root = el('div', { class: 'target-plate', id: 'hud-target', hidden: '' }, this.name, this.use, this.bar, this.statuses);
  }

  /** The target line as text, for scripted checks. */
  get line(): string {
    const t = this.target;
    if (t === null) {
      return '';
    }
    const life = t.resource_max.life !== undefined && t.resource_max.life > 0 ? `  ${Math.round(t.resources.life ?? 0)}/${Math.round(t.resource_max.life)}` : '';
    return `${t.name}${t.level > 0 ? ` [${t.level}]` : ''}${life}${t.interactable != null ? `  (${t.interactable})` : ''}`;
  }

  show(target: EntityState | null, viewW: number, scale: number): void {
    this.target = target;
    this.root.hidden = target === null;
    if (target === null) {
      return;
    }
    const width = this.skin.metric('plate_width', 340) * scale;
    this.root.style.left = `${(viewW - width) / 2}px`;
    this.root.style.top = '10px';
    this.root.style.width = `${width}px`;
    const s = this.skin;
    const special = target.rarity === 'rare' || target.rarity === 'unique' || target.rarity === 'champion';
    this.root.classList.toggle('emphasised', special);
    const color = target.dead ? s.state('dormant', '#777777') : target.is_player ? s.surface('accent') : target.rarity ? s.rarity(target.rarity) : s.surface('ink');
    this.name.style.color = css(color);
    const title = `${target.name}${target.level > 0 ? `  ${target.level}` : ''}`;
    if (this.name.textContent !== title) {
      this.name.textContent = title;
    }
    const use = target.interactable ? target.interactable.replace(/_/g, ' ') : '';
    this.use.textContent = use;
    this.use.hidden = use.length === 0;
    const max = target.resource_max.life ?? 0;
    this.bar.hidden = max <= 0;
    if (max > 0) {
      const cur = target.resources.life ?? 0;
      this.life.style.width = `${Math.min(100, Math.max(0, (cur / max) * 100))}%`;
      this.life.style.setProperty('--life', css(s.fill('life')));
      const wmax = target.resource_max.ward ?? 0;
      this.ward.hidden = wmax <= 0;
      if (wmax > 0) {
        this.ward.style.width = `${Math.min(100, Math.max(0, ((target.resources.ward ?? 0) / wmax) * 100))}%`;
        this.ward.style.background = css(s.fill('ward'));
      }
      this.lifeText.textContent = `${Math.round(cur)} / ${Math.round(max)}`;
    }
    // The statuses content does not hide, in the face content gives them.
    const shown = target.statuses.filter((status) => !lookOf(s.names, status).hidden);
    const signature = shown.join(',');
    if (signature !== this.signature) {
      this.signature = signature;
      clear(this.statuses);
      for (const status of shown) {
        const face = s.statusFace(status);
        const wrap = el('span', { class: 'plate-status', title: s.names.statuses.get(status)?.name ?? status });
        wrap.style.color = css(face.tint);
        wrap.append(s.icon(face.icon, 18));
        this.statuses.append(wrap);
      }
    }
    this.statuses.hidden = shown.length === 0;
  }
}

interface Floater {
  node: HTMLElement;
  at: number;
  drift: number;
}

/**
 * Notices at the top centre under the target plate, in the colour of their level, fading after the notice time;
 * and combat text rising above the hero: its hits, the hits it takes and its heals.
 */
export class Notices {
  readonly root: HTMLElement;
  private readonly notice: HTMLElement;
  private readonly floaters: Floater[] = [];
  private noticeAt = 0;
  private noticeUntil = 0;
  /** How long a notice stays (the notice time option). */
  seconds = 4;

  constructor(private readonly skin: Skin) {
    this.notice = el('div', { class: 'top-notice', id: 'hud-notice', role: 'status' });
    this.root = el('div', { class: 'layer notices' }, this.notice);
  }

  get current(): string {
    return this.notice.textContent ?? '';
  }

  show(text: string, level: string, now: number): void {
    this.notice.textContent = text;
    this.notice.dataset.level = level;
    this.notice.style.color = css(this.skin.notice(level));
    this.noticeAt = now;
    this.noticeUntil = now + this.seconds;
  }

  /** Text rising above the hero. */
  float(text: string, color: string, now: number, big: boolean): void {
    const node = el('div', { class: `floater num${big ? ' big' : ''}`, text });
    node.style.color = color;
    this.root.append(node);
    this.floaters.push({ node, at: now, drift: ((this.floaters.length % 5) - 2) * 9 });
    while (this.floaters.length > 24) {
      this.floaters.shift()?.node.remove();
    }
  }

  /** Lays out for this frame: the notice's fade, and the floaters above the hero's head (interface pixels) or hidden. */
  update(viewW: number, head: { x: number; y: number } | null, now: number): void {
    this.notice.style.width = `${viewW}px`;
    if (this.notice.textContent) {
      if (now >= this.noticeUntil) {
        this.notice.textContent = '';
      } else {
        const fadeIn = Math.min(1, Math.max(0, (now - this.noticeAt) / 0.2));
        const fadeOut = Math.min(1, Math.max(0, (this.noticeUntil - now) / 0.6));
        this.notice.style.opacity = String(Math.min(fadeIn, fadeOut));
      }
    }
    const life = this.skin.metric('combat_text_seconds', 1.3);
    for (let i = this.floaters.length - 1; i >= 0; i--) {
      const f = this.floaters[i];
      const t = (now - f.at) / life;
      if (t >= 1) {
        f.node.remove();
        this.floaters.splice(i, 1);
        continue;
      }
      if (head === null) {
        f.node.hidden = true;
        continue;
      }
      f.node.hidden = false;
      const x = head.x + f.drift + t * f.drift * 0.6;
      const y = head.y - 24 - t * 60;
      f.node.style.transform = `translate(${x}px, ${y}px) translate(-50%, -100%)`;
      f.node.style.opacity = String(1 - t * t);
    }
  }
}

/**
 * The chat and combat log in the lower left corner: a translucent framed log and the line Enter focuses. It
 * keeps to the corner beside the cluster, and stands above the cluster when the screen is too narrow.
 */
export class ChatPane {
  readonly root: HTMLElement;
  readonly log: HTMLElement;
  readonly input: HTMLInputElement;
  /** How many lines the log keeps (the chat history option). */
  cap = 200;

  constructor(
    private readonly skin: Skin,
    submit: (text: string) => void,
  ) {
    this.log = el('div', { class: 'chat-log', id: 'hud-log', 'aria-live': 'polite' });
    this.input = el('input', { class: 'chat-input', id: 'hud-chat-input', type: 'text', placeholder: 'Enter to chat', maxlength: '400', autocomplete: 'off' });
    this.input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        const text = this.input.value.trim();
        if (text.length > 0) {
          submit(text);
        }
        this.input.value = '';
        this.input.blur();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        this.input.value = '';
        this.input.blur();
      }
      e.stopPropagation();
    });
    this.root = el('div', { class: 'chat-pane pane-like', id: 'hud-chat' }, this.log, this.input);
  }

  get focused(): boolean {
    return document.activeElement === this.input;
  }

  focus(): void {
    this.input.focus();
  }

  get text(): string {
    return this.log.innerText;
  }

  /** One line: an optional speaker, the text, and a colour for notices. Everything goes in as text. */
  add(text: string, kind: string, from?: string, color?: string): void {
    // Kinds (chat, combat, system, notice and its level) become k- classes, clear of the interface's own.
    const row = el('div', { class: `chat-line ${kind.split(' ').filter((k) => k.length > 0).map((k) => `k-${k}`).join(' ')}` });
    if (color) {
      row.style.color = color;
    }
    if (from !== undefined) {
      const who = el('span', { class: 'chat-from', text: from });
      who.style.color = css(this.skin.surface('accent'));
      row.append(who, ' ');
    }
    row.append(text);
    const pinned = this.log.scrollTop + this.log.clientHeight >= this.log.scrollHeight - 4;
    this.log.append(row);
    while (this.log.childElementCount > Math.max(10, this.cap)) {
      this.log.firstElementChild?.remove();
    }
    if (pinned) {
      this.log.scrollTop = this.log.scrollHeight;
    }
  }

  place(viewW: number, viewH: number, cluster: Box, scale: number): void {
    const edge = this.skin.metric('edge', 14);
    let width = this.skin.metric('chat_width', 420) * Math.max(0.75, scale);
    const height = this.skin.metric('chat_height', 200) * Math.max(0.75, scale);
    const room = cluster.x - edge * 2;
    let bottom = viewH - edge;
    if (room >= 280) {
      width = Math.min(width, room);
    } else {
      bottom = cluster.y - edge;
    }
    this.root.style.left = `${edge}px`;
    this.root.style.top = `${bottom - height}px`;
    this.root.style.width = `${Math.min(width, viewW - edge * 2)}px`;
    this.root.style.height = `${height}px`;
  }
}

/** The connection overlay in the top right corner: the round trip, chunks and entities (the connection overlay option). */
export class NetOverlay {
  readonly root: HTMLElement;

  constructor() {
    this.root = el('div', { class: 'net-overlay num', id: 'hud-net', hidden: '' });
  }

  show(text: string, visible: boolean): void {
    this.root.hidden = !visible || text.length === 0;
    if (!this.root.hidden && this.root.textContent !== text) {
      this.root.textContent = text;
    }
  }
}
