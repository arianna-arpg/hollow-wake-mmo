import type { CharacterListMsg, CharacterSummary } from '../net/protocol';
import { parseServerInput, type ServerAddress } from './address';
import { el } from './dom';
import { createEntryView, type AccountEntryView } from './entry/entryViews';
import type { AppearanceService } from '../world/entities';
import type { Obj } from './logic/content';
import type { Settings } from './logic/options';

export interface LoginHandlers {
  connect: (address: ServerAddress, account: string, token: string) => void;
  create: (name: string, calling: string, request?: string) => void;
  leave: () => void;
  enter: (characterId: string) => void;
  /** Opens the Options pane over the login screen. */
  options: () => void;
}

/**
 * Connect, log in, pick or create a character, or open Options. Hidden once in the world; drawn in the theme's
 * colours. The desktop LoginPanel's twin.
 */
export class LoginPanel {
  readonly root: HTMLElement;
  private readonly host: HTMLInputElement;
  private readonly port: HTMLInputElement;
  private readonly secure: HTMLInputElement;
  private readonly account: HTMLInputElement;
  private readonly token: HTMLInputElement;
  private readonly connectButton: HTMLButtonElement;
  private readonly status: HTMLElement;
  entry: AccountEntryView | null = null;
  private entryId: string | null = null;
  private path = '/ws';
  characters: CharacterSummary[] = [];
  callings: string[] = [];

  constructor(private readonly handlers: LoginHandlers, private readonly kinds: Map<string, Obj[]>, private readonly appearance: AppearanceService, private readonly settings: Settings, private readonly suspended: () => boolean) {
    this.host = el('input', { id: 'login-host', type: 'text', placeholder: 'server host or wss:// address', autocomplete: 'off', spellcheck: 'false' });
    this.port = el('input', { id: 'login-port', type: 'number', min: '1', max: '65535', placeholder: 'port' });
    this.secure = el('input', { id: 'login-secure', type: 'checkbox' });
    this.account = el('input', { id: 'login-account', type: 'text', autocomplete: 'username', spellcheck: 'false', maxlength: '32' });
    this.token = el('input', { id: 'login-token', type: 'password', autocomplete: 'off', placeholder: 'only if the server asks' });
    this.connectButton = el('button', { id: 'login-connect', type: 'submit', text: 'Connect' });
    this.status = el('div', { class: 'login-status', id: 'login-status', role: 'status', text: 'Not connected.' });
    const optionsButton = el('button', { id: 'login-options', type: 'button', text: 'Options' });
    optionsButton.addEventListener('click', () => this.handlers.options());

    const form = el('form', { class: 'login-form' },
      el('div', { class: 'login-row' },
        el('label', { for: 'login-host', text: 'Server' }), this.host,
        el('label', { for: 'login-port', class: 'compact', text: 'Port' }), this.port,
        el('label', { class: 'check' }, this.secure, 'wss')),
      el('div', { class: 'login-row' },
        el('label', { for: 'login-account', text: 'Account' }), this.account,
        el('label', { for: 'login-token', class: 'compact', text: 'Token' }), this.token),
      el('div', { class: 'login-row end' }, optionsButton, this.connectButton));
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      this.submit();
    });
    // A pasted wss:// or https:// address fills the other fields, so what the player sees is what connects.
    this.host.addEventListener('change', () => {
      if (this.host.value.includes('://')) {
        this.setAddress(parseServerInput(this.host.value, this.address()));
      }
    });

    this.root = el('div', { class: 'login', id: 'login' },
      el('div', { class: 'login-panel' },
        el('img', { src: './icon.svg', alt: 'Hollow Wake insignia', width: '48', height: '48' }),
        el('h1', { text: 'Hollow Wake MMO' }),
        el('p', { class: 'tagline', text: 'the Weftmarches await' }),
        form,
        this.status));
  }

  address(): ServerAddress {
    const port = Number.parseInt(this.port.value, 10);
    const base: ServerAddress = { host: this.host.value.trim(), port: Number.isFinite(port) ? port : 0, secure: this.secure.checked, path: this.path };
    return base.host.includes('://') ? parseServerInput(base.host, base) : base;
  }

  setAddress(a: ServerAddress): void {
    this.host.value = a.host;
    this.port.value = a.port > 0 ? String(a.port) : '';
    this.secure.checked = a.secure;
    this.path = a.path;
  }

  setAccount(account: string): void {
    this.account.value = account;
  }

  get accountName(): string {
    return this.account.value.trim();
  }

  private submit(): void {
    const address = this.address();
    if (address.host.length === 0) {
      this.setStatus('Enter the server address: a host and port, or a wss:// address.');
      this.host.focus();
      return;
    }
    this.handlers.connect(address, this.account.value.trim(), this.token.value);
  }

  /** Same as pressing Connect; scripted checks drive the panel through this. */
  connect(): void {
    this.submit();
  }

  setStatus(text: string): void {
    this.status.textContent = text;
    this.entry?.error(text);
  }

  get statusText(): string {
    return this.status.textContent ?? '';
  }

  setBusy(busy: boolean): void {
    this.connectButton.disabled = busy;
  }

  show(visible: boolean): void {
    this.root.hidden = !visible;
  }

  showCharacters(msg: CharacterListMsg): void {
    this.characters = msg.characters;
    this.callings = msg.callings;
    const flow = this.kinds.get('entry_flows')?.find(f => f.default === true);
    const stages = flow?.stages as unknown as {kind: string; presentation: string}[];
    const stage = msg.entry ?? stages?.[0];
    const spec = this.kinds.get('ui_entry_scenes')?.find(s => s.id === stage?.presentation)?.meta as Obj;
    if (!stage || !spec) { this.reset(); this.setStatus('This entry stage needs a compatible client.'); return; }
    const id = stage.kind + ':' + stage.presentation;
    if (!this.entry || this.entryId !== id) {
      this.entry?.dispose();
      this.entry = createEntryView(stage.kind, { spec, kinds: this.kinds, appearance: this.appearance, settings: this.settings, handlers: this.handlers, suspended: this.suspended });
      this.entryId = id;
      if (!this.entry) { this.reset(); this.setStatus('This entry activity needs a compatible client.'); return; }
      this.root.append(this.entry.root);
    }
    (this.root.querySelector('.login-panel') as HTMLElement).hidden = true;
    this.entry.show(msg);
  }

  reset(): void {
    this.entry?.dispose(); this.entry = null; this.entryId = null;
    (this.root.querySelector('.login-panel') as HTMLElement).hidden = false;
    this.characters = []; this.callings = [];
  }
  focusCharacter(id: string): void { this.entry?.focusCharacter(id); }

  /** Scripted checks: pick a character by id and enter, as a click would. */
  enter(characterId: string): void {
    this.handlers.enter(characterId);
  }

  create(name: string, calling: string): void {
    this.handlers.create(name, calling);
  }
}
