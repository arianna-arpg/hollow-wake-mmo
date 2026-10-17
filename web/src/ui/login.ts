import type { CharacterListMsg, CharacterSummary } from '../net/protocol';
import { parseServerInput, type ServerAddress } from './address';
import { clear, el } from './dom';

export interface LoginHandlers {
  connect: (address: ServerAddress, account: string, token: string) => void;
  create: (name: string, calling: string) => void;
  enter: (characterId: string) => void;
}

/** Connect, log in, pick or create a character. Hidden once in the world. The desktop LoginPanel's twin. */
export class LoginPanel {
  readonly root: HTMLElement;
  private readonly host: HTMLInputElement;
  private readonly port: HTMLInputElement;
  private readonly secure: HTMLInputElement;
  private readonly account: HTMLInputElement;
  private readonly token: HTMLInputElement;
  private readonly connectButton: HTMLButtonElement;
  private readonly status: HTMLElement;
  private readonly characterBox: HTMLElement;
  private readonly list: HTMLElement;
  private readonly newName: HTMLInputElement;
  private readonly calling: HTMLSelectElement;
  private readonly enterButton: HTMLButtonElement;
  private path = '/ws';
  private selected: string | null = null;
  characters: CharacterSummary[] = [];
  callings: string[] = [];

  constructor(private readonly handlers: LoginHandlers) {
    this.host = el('input', { id: 'login-host', type: 'text', placeholder: 'server host or wss:// address', autocomplete: 'off', spellcheck: 'false' });
    this.port = el('input', { id: 'login-port', type: 'number', min: '1', max: '65535', placeholder: 'port' });
    this.secure = el('input', { id: 'login-secure', type: 'checkbox' });
    this.account = el('input', { id: 'login-account', type: 'text', autocomplete: 'username', spellcheck: 'false', maxlength: '32' });
    this.token = el('input', { id: 'login-token', type: 'password', autocomplete: 'off', placeholder: 'only if the server asks' });
    this.connectButton = el('button', { id: 'login-connect', type: 'submit', text: 'Connect' });
    this.status = el('div', { class: 'login-status', id: 'login-status', role: 'status', text: 'Not connected.' });
    this.list = el('div', { class: 'character-list', id: 'character-list', role: 'listbox' });
    this.enterButton = el('button', { id: 'login-enter', type: 'button', text: 'Enter the Weft' });
    this.newName = el('input', { id: 'login-new-name', type: 'text', placeholder: 'New character name', maxlength: '24', spellcheck: 'false' });
    this.calling = el('select', { id: 'login-calling' });
    const createButton = el('button', { id: 'login-create', type: 'button', text: 'Create' });

    const form = el('form', { class: 'login-form' },
      el('div', { class: 'login-row' },
        el('label', { for: 'login-host', text: 'Server' }), this.host,
        el('label', { for: 'login-port', class: 'compact', text: 'Port' }), this.port,
        el('label', { class: 'check' }, this.secure, 'wss')),
      el('div', { class: 'login-row' },
        el('label', { for: 'login-account', text: 'Account' }), this.account,
        el('label', { for: 'login-token', class: 'compact', text: 'Token' }), this.token),
      el('div', { class: 'login-row end' }, this.connectButton));
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

    this.characterBox = el('div', { class: 'character-box', hidden: '' },
      el('h2', { text: 'Characters' }),
      this.list,
      el('div', { class: 'login-row end' }, this.enterButton),
      el('div', { class: 'login-row' }, this.newName, this.calling, createButton));
    this.enterButton.addEventListener('click', () => {
      if (this.selected !== null) {
        this.handlers.enter(this.selected);
      }
    });
    const create = () => {
      const name = this.newName.value.trim();
      if (name.length >= 2 && this.calling.value) {
        this.handlers.create(name, this.calling.value);
        this.newName.value = '';
      } else {
        this.setStatus('Names are 2 to 24 letters; pick a calling.');
      }
    };
    createButton.addEventListener('click', create);
    this.newName.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        create();
      }
    });

    this.root = el('div', { class: 'login', id: 'login' },
      el('div', { class: 'login-panel' },
        el('h1', { text: 'Hollow Wake MMO' }),
        el('p', { class: 'tagline', text: 'the Weftmarches await' }),
        form,
        this.status,
        this.characterBox));
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
    clear(this.list);
    if (!this.characters.some((c) => c.id === this.selected)) {
      this.selected = this.characters[0]?.id ?? null;
    }
    for (const c of msg.characters) {
      const row = el('div', { class: 'character', role: 'option', tabindex: '0', 'data-id': c.id },
        el('span', { class: 'character-name', text: c.name }),
        el('span', { class: 'character-meta', text: `${c.calling}, level ${c.level}, zone ${c.zone}` }));
      const select = () => {
        this.selected = c.id;
        for (const other of this.list.children) {
          other.classList.toggle('selected', (other as HTMLElement).dataset.id === c.id);
          other.setAttribute('aria-selected', String((other as HTMLElement).dataset.id === c.id));
        }
      };
      row.addEventListener('click', select);
      row.addEventListener('dblclick', () => this.handlers.enter(c.id));
      row.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          this.handlers.enter(c.id);
        }
      });
      row.classList.toggle('selected', c.id === this.selected);
      row.setAttribute('aria-selected', String(c.id === this.selected));
      this.list.append(row);
    }
    if (msg.characters.length === 0) {
      this.list.append(el('div', { class: 'empty', text: 'No characters yet. Make one below.' }));
    }
    const current = this.calling.value;
    clear(this.calling);
    for (const calling of msg.callings) {
      this.calling.append(el('option', { value: calling, text: calling }));
    }
    if (msg.callings.includes(current)) {
      this.calling.value = current;
    }
    this.enterButton.disabled = msg.characters.length === 0;
    this.characterBox.hidden = false;
  }

  /** Scripted checks: pick a character by id and enter, as a click would. */
  enter(characterId: string): void {
    this.handlers.enter(characterId);
  }

  create(name: string, calling: string): void {
    this.handlers.create(name, calling);
  }
}
