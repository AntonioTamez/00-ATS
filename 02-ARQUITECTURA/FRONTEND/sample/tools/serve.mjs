// A tiny static file server, so the sample runs in a browser with no build step: `npm start`, then open the URL it prints.
// Browsers refuse ES modules from file:// URLs, which is the only reason this exists.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('..', import.meta.url)));
const port = Number(process.env.PORT ?? 5173);
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8' };

createServer(async (request, response) => {
  const path = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
  const file = resolve(join(root, normalize(path === '/' ? '/index.html' : path)));
  const allowed = file.startsWith(root + sep) && !file.slice(root.length).split(sep).includes('node_modules');

  try {
    if (!allowed || !(extname(file) in types)) throw new Error('not served');
    const body = await readFile(file);
    response.writeHead(200, { 'content-type': types[extname(file)] });
    response.end(body);
  } catch {
    response.writeHead(404, { 'content-type': 'text/plain' });
    response.end('not found');
  }
}).listen(port, () => console.log(`http://localhost:${port}`));
