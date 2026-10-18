import content from '../../../content/core/ui/loading.json';
export interface WispPart { kind: string; x?: number; y?: number; scale?: number; params?: Record<string, number> }
export const presentation = content.entries.find(e => e.default)!.meta as Omit<typeof content.entries[0]['meta'], 'pickups' | 'wisp'> & {
  pickups: Record<string, { name: string; boostGates: number; size: number; radiance: number; flicker: number }>;
  wisp: { radius: number; palette: Record<string, string>; parts: WispPart[] };
};
export const encipheredTitle = presentation.labels.title;
