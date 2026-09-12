import http from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";

const root = process.cwd();
const port = 4173;
const mime = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8"
};

http.createServer(async (request, response) => {
  try {
    const urlPath = request.url === "/" ? "/prototype/" : request.url;
    const relative = urlPath.endsWith("/") ? `${urlPath}index.html` : urlPath;
    const safe = normalize(relative).replace(/^(\.\.(\/|\\|$))+/, "");
    const file = join(root, safe);
    const body = await readFile(file);
    response.writeHead(200, { "Content-Type": mime[extname(file)] ?? "application/octet-stream" });
    response.end(body);
  } catch {
    response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
    response.end("Not found");
  }
}).listen(port, "127.0.0.1", () => {
  process.stdout.write(`Bigimong prototype: http://127.0.0.1:${port}/prototype/\n`);
});
