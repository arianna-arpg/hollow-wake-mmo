import type { Int64, ItemInfo, Message } from '../net/protocol';
import type { HeroState } from './logic/heroState';
import type { Settings } from './logic/options';
import type { Skin } from './skin';

/**
 * What a page or a world pane reads and how it acts, and what the interface asks of it: the browser twin of the
 * desktop client's UI/Book/LeafView.cs and UI/Core/Carry.cs. Leaves are plain DOM; they draw what the server
 * sent with the skin's look and raise intents through the context, never folding a number the server sends.
 */

/** Where a carried item came from: a board (the bag or a container) at a cell, or an equipment slot. */
export interface CarrySource {
  container: string;
  x: number;
  y: number;
  slot?: string | null;
}

/**
 * An item picked up from a board or the doll: carried while the button is held (drag) or after a click
 * (click-lift), until it is dropped on a target or cancelled. Dropping sends one intent; the server answers
 * with the new state.
 */
export interface Carry {
  item: ItemInfo;
  from: CarrySource;
  /** The footprint in cells. */
  w: number;
  h: number;
  /** Lifted by a click rather than held: the next click drops it. */
  lifted: boolean;
  /** The button is still down from the press that picked it up. */
  pressed: boolean;
  /** Where the press happened, in client pixels. */
  pressX: number;
  pressY: number;
}

/** A surface an item can be dropped on: a board, the doll, a weave socket, a vendor's counter. */
export interface DropTarget {
  /** Whether the pointer (client pixels) is over this target and the target is showing. */
  covers(x: number, y: number): boolean;
  /** Tints what the carried item would do here. */
  hover(carry: Carry, x: number, y: number): void;
  /** Clears any tint. */
  leave(): void;
  /** Sends the intent for dropping here; false when the drop means nothing here. */
  drop(carry: Carry, x: number, y: number): boolean;
}

/** What a board, the doll or a page's item asks of the interface around it. Points are client pixels. */
export interface ItemHost {
  readonly skin: Skin;
  /** The item being carried, once it has moved or been lifted. */
  readonly carrying: Carry | null;
  /** Shows the item's card at the pointer, or hides it (null). */
  hoverItem(item: ItemInfo | null, x: number, y: number, container: string): void;
  /** A left press on an item: a drag or a click-lift begins. */
  pressItem(item: ItemInfo, from: CarrySource, x: number, y: number): void;
  /** A right click on an item: its quick actions. */
  quickActions(item: ItemInfo, container: string, x: number, y: number): void;
  /** One move_item intent. */
  sendMove(uid: Int64, container: string, x: number, y: number, slot?: string | null): void;
}

export interface MenuAction {
  label: string;
  act: () => void;
}

/** What every leaf is given. A theme change builds new leaves with a new context. */
export interface LeafContext {
  readonly skin: Skin;
  readonly hero: HeroState;
  readonly items: ItemHost;
  readonly settings: Settings;
  /** Sends an intent to the server. */
  send(message: Message): void;
  /** Opens a small menu of actions at a client point (right-click quick actions). */
  menu(x: number, y: number, actions: MenuAction[]): void;
  /** A notice at the top of the screen and a line in the chat. */
  notify(text: string, level: string): void;
  /** Local seconds, as of this frame. */
  readonly now: number;
}

/** The content of a leaf of a book: a development page or a pane the world opened. */
export interface LeafView {
  /** The leaf's element; the book places it in its body. */
  readonly root: HTMLElement;
  /** The width the leaf would like beside the core pane, in interface pixels. */
  readonly preferredWidth: number;
  /** Called every frame while the leaf is drawn; rebuild only when what it reads changed. */
  refresh(): void;
  /** Drop targets the leaf offers while an item is carried. */
  dropTargets(): DropTarget[];
  /** Escape reaches the front leaf first; true when the leaf used it (a capture ended, changes undone). */
  onEscape(): boolean;
  /** Lets go of listeners and timers when the leaf closes or the interface is rebuilt. */
  dispose(): void;
}
