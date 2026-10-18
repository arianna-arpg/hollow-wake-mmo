import type { CharacterListMsg } from '../../net/protocol';
import { str, type Obj } from '../logic/content';
import type { EntrySceneSpec, VesselChoice } from './spatialSelection';

export function entryChoices(list: CharacterListMsg, callings: Obj[], spec: EntrySceneSpec): VesselChoice[] {
  const find = (id: string) => callings.find(c => c.id === id);
  const choices: VesselChoice[] = list.characters.map(c => { const calling = find(c.calling); return {
    id: `character:${c.id}`, calling: c.calling, name: c.name,
    description: `${str(calling?.name, c.calling)} · Level ${c.level} · ${c.zone}`,
    look: str(calling?.look, c.calling), character: c.id, available: true, rank: 0,
  }; });
  for (const c of list.entry?.callings ?? list.callings.map(id => ({ id, available: true }))) {
    const calling = find(c.id);
    choices.push({ id: `calling:${c.id}`, calling: c.id, name: str(calling?.name, c.id),
      description: !c.available ? spec.labels.unavailable! : list.entry?.can_create === false ? spec.labels.full! : str(calling?.description),
      look: str(calling?.look, c.id), character: null, available: c.available && (list.entry?.can_create ?? true), rank: 1 });
  }
  return choices;
}
