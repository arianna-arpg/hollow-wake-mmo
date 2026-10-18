import type { CharacterListMsg } from '../../net/protocol';
import type { AppearanceService } from '../../world/entities';
import type { Obj } from '../logic/content';
import type { Settings } from '../logic/options';
import { EntryScene, type EntryHandlers } from './entryScene';
import type { EntrySceneSpec } from './spatialSelection';

export interface AccountEntryView {
  readonly root: HTMLElement;
  show(list: CharacterListMsg): void;
  error(text: string): void;
  focusCharacter(id: string): void;
  dispose(): void;
  snapshot?(): unknown;
}
export interface EntryViewContext {
  spec: Obj; kinds: Map<string, Obj[]>; appearance: AppearanceService; settings: Settings;
  handlers: EntryHandlers; suspended: () => boolean;
}
type Factory = (context: EntryViewContext) => AccountEntryView;
const views = new Map<string, Map<string, Factory>>([
  ['vessels', new Map([['spatial_vessels', c => new EntryScene(c.spec as unknown as EntrySceneSpec, c.kinds, c.appearance, c.settings, c.handlers, c.suspended)]])],
]);
export function registerEntryView(stage: string, activity: string, factory: Factory): void {
  let activities = views.get(stage); if (!activities) { activities = new Map(); views.set(stage, activities); }
  if (activities.has(activity)) throw new Error(`Entry view already registered: ${stage}/${activity}`);
  activities.set(activity, factory);
}
export function createEntryView(stage: string, context: EntryViewContext): AccountEntryView | null {
  return views.get(stage)?.get(String(context.spec.activity))?.(context) ?? null;
}
