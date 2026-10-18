import { el } from './dom';

export function codespaceLink(repository: string, branch: string): string | null {
  if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository) || !branch.trim() || branch.length > 255 || /[\x00-\x20~^:?*\[\\]/.test(branch) || branch.includes('..')) return null;
  return `https://codespaces.new/${repository}/tree/${encodeURIComponent(branch)}?quickstart=1`;
}

/** Pages is a client only. A chosen branch opens its own server/client together in Codespaces. */
export async function addPlayRibbon(parent: HTMLElement): Promise<void> {
  try {
    const response = await fetch(new URL('./play-catalog.json', window.location.href), { cache: 'no-store' });
    if (!response.ok) return;
    const data = await response.json() as { enabled?: boolean; repository?: string; branch?: string; commit?: string; branches?: string[] };
    if (!data.enabled || !data.repository || !codespaceLink(data.repository, 'main')) return;
    const input = el('input', { id: 'play-branch', list: 'play-branches', 'aria-label': 'Branch to play', spellcheck: 'false', autocomplete: 'off' });
    input.value = new URLSearchParams(window.location.search).get('branch') ?? 'main';
    const choices = el('datalist', { id: 'play-branches' });
    for (const branch of data.branches ?? ['main']) if (typeof branch === 'string') choices.append(el('option', { value: branch }));
    const launch = el('a', { class: 'play-branch-link', text: 'Open / resume playtest', target: '_blank', rel: 'noopener noreferrer' });
    const note = el('p', { class: 'play-ribbon-note' });
    const update = () => {
      const link = codespaceLink(data.repository!, input.value);
      if (link) launch.href = link;
      else launch.removeAttribute('href');
      launch.setAttribute('aria-disabled', String(!link));
      note.textContent = link ? 'Starts this branch’s server and game in a Codespace. First setup takes a few minutes; later visits resume it.' : 'Enter a valid branch name.';
    };
    input.addEventListener('input', update);
    update();
    const ribbon = el('section', { class: 'play-ribbon', 'aria-label': 'Browser playtest' },
      el('strong', { text: 'Play a branch' }),
      el('div', { class: 'play-ribbon-row' }, input, choices, launch), note,
      el('p', { class: 'play-ribbon-note', text: `Client on this page: ${data.branch ?? 'main'} ${data.commit?.slice(0, 7) ?? ''}. Connect below to an existing server.` }),
      el('a', { href: `https://github.com/${data.repository}/releases/latest`, text: 'Download desktop launcher', target: '_blank', rel: 'noopener noreferrer' }));
    parent.prepend(ribbon);
  } catch { /* An unavailable catalogue never prevents connecting to an existing server. */ }
}
