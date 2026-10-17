import { decode, encode } from './codec';
import type { Message } from './protocol';

const CLOSE_REASONS: Record<number, string> = {
  1000: 'closed',
  1001: 'server going away',
  1003: 'server refused a message',
  1006: 'connection lost',
  1009: 'message too big',
  1011: 'server error',
};

/**
 * The WebSocket to the game server. Binary frames only, each one whole message; decoded messages go to the
 * handler as they arrive. Mirrors the desktop NetClient: it moves messages and knows nothing about them.
 */
export class Connection {
  private socket: WebSocket | null = null;
  bytesIn = 0;
  bytesOut = 0;

  constructor(
    private readonly onMessage: (message: Message) => void,
    private readonly onClosed: (reason: string) => void,
  ) {}

  get open(): boolean {
    return this.socket?.readyState === WebSocket.OPEN;
  }

  /** Resolves once the socket is open; rejects with a readable reason when it never opens. */
  connect(url: string): Promise<void> {
    this.close('reconnecting');
    return new Promise((resolve, reject) => {
      let socket: WebSocket;
      try {
        socket = new WebSocket(url);
      } catch (error) {
        reject(error instanceof Error ? error : new Error(String(error)));
        return;
      }
      socket.binaryType = 'arraybuffer';
      this.socket = socket;
      let opened = false;
      socket.onopen = () => {
        opened = true;
        resolve();
      };
      socket.onmessage = (event: MessageEvent) => {
        if (!(event.data instanceof ArrayBuffer)) {
          return;
        }
        this.bytesIn += event.data.byteLength;
        let message: Message;
        try {
          message = decode(event.data);
        } catch (error) {
          console.warn('unreadable message from the server', error);
          return;
        }
        this.onMessage(message);
      };
      socket.onclose = (event: CloseEvent) => {
        if (this.socket !== socket) {
          return;
        }
        this.socket = null;
        const reason = event.reason || CLOSE_REASONS[event.code] || `closed (${event.code})`;
        if (opened) {
          this.onClosed(reason);
        } else {
          reject(new Error(`could not connect to ${url}: ${reason}`));
        }
      };
    });
  }

  send(message: Message): void {
    const socket = this.socket;
    if (socket === null || socket.readyState !== WebSocket.OPEN) {
      return;
    }
    const bytes = encode(message);
    this.bytesOut += bytes.byteLength;
    socket.send(bytes);
  }

  close(reason: string): void {
    const socket = this.socket;
    this.socket = null;
    if (socket !== null && socket.readyState <= WebSocket.OPEN) {
      socket.close(1000, reason.slice(0, 100));
    }
  }
}
