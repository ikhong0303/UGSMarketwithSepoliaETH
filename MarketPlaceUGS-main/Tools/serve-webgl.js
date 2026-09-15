// Zero-install localhost server for the uncompressed WebGL build. No public network binding.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../Builds/WebGL');
const types = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png' };
http.createServer((req, res) => {
  let filename;
  try { filename = path.resolve(root, '.' + decodeURIComponent(new URL(req.url, 'http://localhost').pathname)); }
  catch (_) { res.writeHead(400).end(); return; }
  if (filename !== root && !filename.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
  if (filename === root) filename = path.join(root, 'index.html');
  fs.stat(filename, (error, stat) => {
    if (error || !stat.isFile()) { res.writeHead(404).end('Build first: Simple Market > 2. Build WebGL'); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(filename)] || 'application/octet-stream', 'Cache-Control': 'no-store', 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
    const stream = fs.createReadStream(filename); stream.on('error', () => res.destroy()); stream.pipe(res);
  });
}).listen(8080, '127.0.0.1', () => console.log('Simple Market: http://localhost:8080 (Ctrl+C to stop)'));
