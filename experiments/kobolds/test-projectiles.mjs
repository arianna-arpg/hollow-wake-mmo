// Focused real gameplay acceptance; these cases also run in the normal core suite.
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../..', import.meta.url));
const args = ['test', 'tests/HollowWake.Core.Tests/HollowWake.Core.Tests.csproj',
  '--artifacts-path', path.join(root, 'experiments/kobolds/artifacts'),
  '--filter', 'FullyQualifiedName~KoboldProjectileTests', '--logger', 'console;verbosity=normal'];
const child = spawn('dotnet', args, { cwd: root, stdio: 'inherit', windowsHide: true });
child.on('error', error => { console.error(error); process.exitCode = 1; });
child.on('exit', (code, signal) => { process.exitCode = signal ? 1 : (code ?? 1); });
