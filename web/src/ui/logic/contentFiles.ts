/**
 * The content the interface reads, bundled at build time from the repository's content/core: the theme,
 * pages, layout and sheet, and the names it labels things with (attributes, statuses, Threads, currencies,
 * slots, the passive constellation). The desktop client reads the same files from disk. A server running
 * other layers changes what the server sends, not these labels.
 */
export const CONTENT_FILES: Record<string, string> = import.meta.glob(['../../../../content/core/**/*.{yaml,yml,json}', '!../../../../content/core/ui/ability-icons.json'], {
  query: '?raw',
  import: 'default',
  eager: true,
});

// Artwork is a separate hashed asset, preloaded under the boot cover before constructing the UI.
const artwork = import.meta.glob('../../../../content/core/ui/ability-icons.json', {query:'?raw', import:'default'});
let pending: Promise<void> | undefined;
export function preloadAbilityArtwork(): Promise<void> {
  return pending ??= Promise.all(Object.entries(artwork).map(async ([path, load]) => { CONTENT_FILES[path] = await load() as string; })).then(() => {});
}
