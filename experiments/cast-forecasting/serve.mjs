import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
const files = new Map([
  ['/', ['index.html', 'text/html; charset=utf-8']],
  ['/evidence/core-results.json', ['evidence/core-results.json', 'application/json']],
]);
const server = createServer(async (request, response) => {
  const file = files.get(new URL(request.url, 'http://localhost').pathname);
  if (request.method !== 'GET' || !file) { response.writeHead(404); response.end(); return; }
  try { const body = await readFile(new URL(file[0], import.meta.url)); response.writeHead(200, { 'Content-Type': file[1], 'Cache-Control': 'no-store' }); response.end(body); }
  catch { response.writeHead(500); response.end('Run the core probe to generate evidence.'); }
});
server.listen(Number(process.argv[2] ?? 8794), '127.0.0.1', () => console.log(`Cast evidence: http://127.0.0.1:${server.address().port}`));
