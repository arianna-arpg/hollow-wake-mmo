// Run after the Pages build. Branch names come from git; no GitHub token enters the web bundle.
import { execFileSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

const repository = process.env.GITHUB_REPOSITORY;
if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository ?? '')) throw new Error('GITHUB_REPOSITORY is required');
const git = args => execFileSync('git', args, { encoding: 'utf8' }).trim();
const branches = git(['for-each-ref', '--format=%(refname:strip=3)', 'refs/remotes/origin']).split('\n').filter(b => b && b !== 'HEAD');
const branch = process.env.GITHUB_HEAD_REF || process.env.GITHUB_REF_NAME || 'main';
writeFileSync('dist/play-catalog.json', JSON.stringify({ enabled: true, repository, branch, commit: git(['rev-parse', 'HEAD']), branches: [...new Set(['main', ...branches])].sort() }, null, 2) + '\n');
