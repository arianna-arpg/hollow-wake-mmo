/**
 * The content the interface reads, bundled at build time from the repository's content/core: the theme,
 * pages, layout and sheet, and the names it labels things with (attributes, statuses, Threads, currencies,
 * slots, the passive constellation). The desktop client reads the same files from disk. A server running
 * other layers changes what the server sends, not these labels.
 */
export const CONTENT_FILES: Record<string, string> = import.meta.glob('../../../../content/core/**/*.{yaml,yml,json}', {
  query: '?raw',
  import: 'default',
  eager: true,
});
